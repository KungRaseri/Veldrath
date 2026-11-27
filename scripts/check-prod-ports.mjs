#!/usr/bin/env node
/**
 * A production stack publishes no host port. This script proves it and fails when it stops
 * being true.
 *
 * Why it exists: `docker-compose.prod.yml` used to say `ports: []` for `server`, `web` and
 * `postgres`, and nothing at all for `seq`, `prometheus` and `grafana`. That reads like "no
 * host ports", and it rendered six: 5433 (postgres), 8081 (web), 9000 (server), 9001 (seq),
 * 9002 (grafana), 9003 (prometheus). Compose merges `ports` lists across `-f` files, and an
 * empty list contributes nothing instead of clearing what the base file declared; only the
 * `!reset` tag drops an inherited list. The firewall does not save you: Docker writes its own
 * DOCKER/FORWARD rules, so a published port is reachable even where ufw's INPUT chain is
 * `default deny`. docs/deployment.md, "Firewall and published ports", is the human half of
 * this check.
 *
 * What counts as a publication: any entry under a service's `ports:` key, in either syntax
 * (short `"5433:5432"`, long `target`/`published`). A bare container port (`- "8080"`) is also
 * a publication — Compose gives it an ephemeral host port — and is reported as `random`.
 * `expose:` is not a publication and is ignored. `network_mode: host` publishes everything.
 *
 * Modes:
 *
 *   node scripts/check-prod-ports.mjs
 *       Static mode (default, and what CI runs). Reads the compose files, applies Compose's
 *       documented merge rule for `ports`, and checks the result against ALLOWLIST. It needs
 *       no Docker: the Forgejo job image ships the docker CLI but no compose plugin, and the
 *       runner bind-mounts only /var/run/docker.sock (`valid_volumes` in its config), so a
 *       live `docker compose config` is not possible inside CI.
 *
 *   node scripts/check-prod-ports.mjs --render <file>
 *       Checks a config rendered by `docker compose ... config --format json`, i.e. Compose's
 *       own verdict. This is how static mode is parity-tested against the real thing
 *       (scripts/tests/prod-ports.test.mjs) and how a human double-checks the gate locally:
 *
 *         docker compose -f docker-compose.yml -f docker-compose.prod.yml \
 *           config --format json > /tmp/prod.json
 *         node scripts/check-prod-ports.mjs --render /tmp/prod.json
 *
 * Exit codes: 0 clean, 1 a publication outside the allowlist (or a missing expected service),
 * 2 a usage, read or parse error.
 */

import { existsSync, readFileSync } from 'node:fs';
import { dirname, isAbsolute, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');

/**
 * The reviewed allowlist. Every entry needs a one-line justification in the table in
 * docs/deployment.md, "Allowlist". Empty on purpose: production publishes nothing, and
 * Coolify reaches each container over the Docker network.
 *
 * @type {Record<string, number[]>}
 */
export const ALLOWLIST = {};

/** Services the production overlay is expected to keep. Losing one is a finding, not a no-op. */
export const EXPECTED_SERVICES = [
	'discord',
	'grafana',
	'postgres',
	'prometheus',
	'seq',
	'server',
	'web'
];

const DEFAULTS = {
	base: 'docker-compose.yml',
	overlay: 'docker-compose.prod.yml',
	extra: ['docker-compose.coolify.yml']
};

// ---------------------------------------------------------------------------
// A reader for the YAML subset these compose files use: block mappings, block
// sequences, quoted scalars, flow sequences of quoted scalars, and the `!reset`
// tag. Deliberately not a general YAML parser — it extracts only what decides a
// host publication, and `--render` exists to keep it honest against Compose.
// ---------------------------------------------------------------------------

/** Strips a trailing `# comment` that is not inside quotes. */
function stripComment(line) {
	let inSingle = false;
	let inDouble = false;
	for (let i = 0; i < line.length; i += 1) {
		const char = line[i];
		if (char === "'" && !inDouble) inSingle = !inSingle;
		else if (char === '"' && !inSingle) inDouble = !inDouble;
		else if (char === '#' && !inSingle && !inDouble && (i === 0 || /\s/.test(line[i - 1]))) {
			return line.slice(0, i);
		}
	}
	return line;
}

/** Removes one layer of matching quotes. */
function unquote(value) {
	const text = value.trim();
	if (text.length >= 2) {
		const first = text[0];
		const last = text[text.length - 1];
		if ((first === '"' && last === '"') || (first === "'" && last === "'")) {
			return text.slice(1, -1);
		}
	}
	return text;
}

/** Splits `["9000:8080", "8081"]` into its unquoted members. */
export function parseFlowSequence(value) {
	const inner = value
		.trim()
		.replace(/^\[/, '')
		.replace(/\]$/, '')
		.trim();
	if (!inner) return [];
	const members = [];
	let current = '';
	let inSingle = false;
	let inDouble = false;
	for (const char of inner) {
		if (char === "'" && !inDouble) inSingle = !inSingle;
		else if (char === '"' && !inSingle) inDouble = !inDouble;
		if (char === ',' && !inSingle && !inDouble) {
			members.push(current);
			current = '';
		} else {
			current += char;
		}
	}
	members.push(current);
	return members.map(unquote).filter((member) => member.length > 0);
}

/** Splits `9000-9005` into its members; anything else is a single value. */
function expandPublished(value) {
	const match = /^(\d+)-(\d+)$/.exec(value);
	if (!match) return [value];
	const found = [];
	for (let port = Number(match[1]); port <= Number(match[2]); port += 1) found.push(String(port));
	return found;
}

/**
 * Published host ports of a short-syntax entry.
 *
 * `"5433:5432"` publishes 5433, `"127.0.0.1:8080:80"` publishes 8080, and a bare `"8080"`
 * publishes an ephemeral host port, reported as `random` because no fixed port can be
 * reviewed.
 *
 * @param {string} value
 * @returns {string[]}
 */
export function publicationsOfShortSyntax(value) {
	const withoutProtocol = value.replace(/\/(tcp|udp|sctp)$/i, '');
	const parts = withoutProtocol.split(':');
	if (parts.length === 1) return ['random'];
	const published = parts.length === 2 ? parts[0] : parts[1];
	return expandPublished(published);
}

/**
 * Reads one compose file into a map of service → publications.
 *
 * @param {string} text
 * @returns {Map<string, {publications: Array<{raw: string, published: string[]}>, networkMode: string|null, reset: boolean}>}
 */
export function readServicePublications(text) {
	const services = new Map();
	let inServices = false;
	let current = null;
	let portsIndent = -1;
	let entryIndent = -1;
	/** @type {{raw: string, published: string[]}|null} */
	let entry = null;

	const flush = () => {
		if (current && entry) current.publications.push(entry);
		entry = null;
	};
	const ensure = (name) => {
		if (!services.has(name)) {
			services.set(name, { publications: [], networkMode: null, reset: false });
		}
		return services.get(name);
	};

	for (const rawLine of text.split(/\r?\n/)) {
		const line = stripComment(rawLine);
		if (!line.trim()) continue;
		const indent = line.length - line.trimStart().length;
		const content = line.trim();

		if (indent === 0) {
			flush();
			portsIndent = -1;
			entryIndent = -1;
			current = null;
			inServices = /^services:\s*$/.test(content);
			continue;
		}
		if (!inServices) continue;

		if (indent === 2) {
			flush();
			portsIndent = -1;
			entryIndent = -1;
			const match = /^([A-Za-z0-9_.-]+):\s*(.*)$/.exec(content);
			current = match ? ensure(match[1]) : null;
			continue;
		}
		if (!current) continue;

		// Inside an open `ports:` block.
		if (portsIndent !== -1) {
			if (indent === entryIndent && content.startsWith('- ')) {
				flush();
				const item = content.slice(2).trim();
				const asMap = /^([A-Za-z0-9_]+):\s*(.*)$/.exec(item);
				if (asMap) {
					entry = { raw: item, published: [] };
					applyPortField(entry, asMap[1], asMap[2]);
				} else {
					const value = unquote(item);
					entry = { raw: value, published: publicationsOfShortSyntax(value) };
					flush();
				}
				continue;
			}
			if (entry && indent > entryIndent) {
				const field = /^([A-Za-z0-9_]+):\s*(.*)$/.exec(content);
				if (field) {
					applyPortField(entry, field[1], field[2]);
					continue;
				}
			}
			flush();
			portsIndent = -1;
			entryIndent = -1;
		}

		const key = /^([A-Za-z0-9_.-]+):\s*(.*)$/.exec(content);
		if (!key) continue;
		const [, name, rest] = key;

		if (name === 'network_mode') {
			current.networkMode = unquote(rest);
			continue;
		}
		if (name !== 'ports') continue;

		const value = rest.trim();
		const reset = /^!reset\b/.test(value);
		const inline = reset ? value.replace(/^!reset\b/, '').trim() : value;
		if (reset) current.reset = true;

		if (inline.startsWith('[')) {
			for (const item of parseFlowSequence(inline)) {
				current.publications.push({
					raw: item,
					published: publicationsOfShortSyntax(item)
				});
			}
			continue;
		}
		if (inline) {
			// A bare scalar under `ports:` is not valid Compose, but a reader that silently
			// dropped it would be a hole in this gate.
			const scalar = unquote(inline);
			current.publications.push({ raw: scalar, published: publicationsOfShortSyntax(scalar) });
			continue;
		}
		portsIndent = indent;
		entryIndent = indent + 2;
	}
	flush();

	return services;
}

function applyPortField(entry, name, rawValue) {
	const value = unquote(rawValue);
	if (name === 'published') entry.published = expandPublished(value);
	else if (name === 'target') entry.target = value;
	else if (name === 'protocol') entry.protocol = value;
}

/**
 * Applies Compose's documented merge for `ports` to the base/overlay pair:
 *
 * - overlay `ports` with `!reset` → the overlay list replaces the base list;
 * - overlay `ports` without `!reset` → Compose appends, it does not replace;
 * - no `ports` key in the overlay → the base list survives untouched.
 *
 * @param {ReturnType<typeof readServicePublications>} base
 * @param {ReturnType<typeof readServicePublications>} overlay
 * @param {string} baseName label reported for inherited publications
 * @param {string} overlayName label reported for overlay publications
 */
export function mergePublications(base, overlay, baseName, overlayName) {
	const merged = new Map();
	for (const name of new Set([...base.keys(), ...overlay.keys()])) {
		const baseService = base.get(name) ?? { publications: [], networkMode: null, reset: false };
		const overlayService = overlay.get(name) ?? {
			publications: [],
			networkMode: null,
			reset: false
		};
		merged.set(name, {
			networkMode: overlayService.networkMode ?? baseService.networkMode ?? null,
			publications: [
				...(overlayService.reset ? [] : baseService.publications).map((publication) => ({
					...publication,
					source: baseName,
					inherited: true
				})),
				...overlayService.publications.map((publication) => ({
					...publication,
					source: overlayName,
					inherited: false
				}))
			]
		});
	}
	return merged;
}

/**
 * Findings for a merged service map.
 *
 * @param {Map<string, {networkMode: string|null, publications: Array<{raw: string, published: string[], source?: string, inherited?: boolean}>}>} merged
 * @param {Record<string, number[]>} allowlist
 * @param {string} overlayName file that must carry the `!reset`
 */
export function findingsFor(merged, allowlist, overlayName) {
	const findings = [];
	for (const [service, entry] of merged) {
		if ((entry.networkMode ?? '').replace(/^["']|["']$/g, '') === 'host') {
			findings.push({
				service,
				published: 'host',
				reason: 'network_mode: host exposes every container port on the host'
			});
		}
		const allowed = new Set((allowlist[service] ?? []).map(String));
		for (const publication of entry.publications) {
			for (const published of publication.published) {
				if (allowed.has(published)) continue;
				const origin = publication.inherited
					? ` (inherited from ${publication.source}; only \`ports: !reset []\` in ${overlayName} drops it)`
					: publication.source
						? ` (${publication.source})`
						: '';
				findings.push({
					service,
					published,
					reason: `publishes host port ${published}${origin}`
				});
			}
		}
	}
	return findings;
}

// ---------------------------------------------------------------------------
// CLI
// ---------------------------------------------------------------------------

function die(message, code = 2) {
	console.error(`error: ${message}`);
	process.exit(code);
}

function readText(path) {
	if (!existsSync(path)) die(`cannot read ${path}`);
	return readFileSync(path, 'utf8');
}

function fromRepo(path) {
	return isAbsolute(path) ? path : resolve(repoRoot, path);
}

function label(path) {
	return path.startsWith(repoRoot + '/') ? relative(repoRoot, path) : path;
}

function usage() {
	console.log(`usage: node scripts/check-prod-ports.mjs [options]

  --base <file>      base compose file (default ${DEFAULTS.base})
  --overlay <file>   production overlay (default ${DEFAULTS.overlay})
  --extra <file>     a further production compose stack that must publish nothing
                     (repeatable; default ${DEFAULTS.extra.join(', ')})
  --no-extra         check the base + overlay pair only
  --render <file>    check a \`docker compose config --format json\` render instead of
                     parsing the files
  --quiet            only print failures
  --help             this text`);
}

/** Reads the CLI into a plain options object, so a flag's value is never read as a file. */
function parseArgs(argv) {
	const options = {
		base: DEFAULTS.base,
		overlay: DEFAULTS.overlay,
		extra: [],
		render: null,
		quiet: false,
		noExtra: false
	};
	for (let index = 0; index < argv.length; index += 1) {
		const flag = argv[index];
		if (flag === '--quiet') options.quiet = true;
		else if (flag === '--no-extra') options.noExtra = true;
		else if (flag === '--base') options.base = argv[(index += 1)];
		else if (flag === '--overlay') options.overlay = argv[(index += 1)];
		else if (flag === '--extra') options.extra.push(argv[(index += 1)]);
		else if (flag === '--render') options.render = argv[(index += 1)];
		else if (flag === '--help') options.help = true;
		else die(`unknown option ${flag} (try --help)`);
	}
	if (options.extra.length === 0 && !options.render && !options.noExtra) {
		options.extra = [...DEFAULTS.extra];
	}
	return options;
}

function renderFindings(path) {
	let render;
	try {
		render = JSON.parse(readText(path));
	} catch (error) {
		die(`cannot parse the rendered config ${path}: ${error.message}`);
	}
	const merged = new Map();
	for (const [name, service] of Object.entries(render.services ?? {})) {
		merged.set(name, {
			networkMode: service.network_mode ?? null,
			publications: (service.ports ?? []).map((port) => ({
				raw: JSON.stringify(port),
				published:
					port.published === undefined || port.published === null || port.published === ''
						? ['random']
						: [String(port.published)],
				source: label(path)
			}))
		});
	}
	return {
		findings: findingsFor(merged, ALLOWLIST, label(path)),
		summary: `${merged.size} services in ${label(path)}`
	};
}

function staticFindings(options) {
	const basePath = fromRepo(options.base);
	const overlayPath = fromRepo(options.overlay);
	const baseName = label(basePath);
	const overlayName = label(overlayPath);
	const merged = mergePublications(
		readServicePublications(readText(basePath)),
		readServicePublications(readText(overlayPath)),
		baseName,
		overlayName
	);

	const findings = [];
	for (const service of EXPECTED_SERVICES) {
		if (!merged.has(service)) {
			findings.push({
				service,
				published: '-',
				reason: `service is missing from the ${baseName} + ${overlayName} merge`
			});
		}
	}
	findings.push(...findingsFor(merged, ALLOWLIST, overlayName));

	// A further production stack has no overlay to hide behind: it must publish nothing itself.
	for (const extra of options.extra.map(fromRepo)) {
		const extraName = label(extra);
		const services = readServicePublications(readText(extra));
		const asMerged = new Map(
			[...services].map(([name, entry]) => [
				name,
				{
					networkMode: entry.networkMode,
					publications: entry.publications.map((publication) => ({
						...publication,
						source: extraName,
						inherited: false
					}))
				}
			])
		);
		findings.push(...findingsFor(asMerged, ALLOWLIST, extraName));
	}

	return {
		findings,
		summary: `${merged.size} services in ${baseName} + ${overlayName}, plus ${options.extra.length} extra stack(s)`
	};
}

function main() {
	const options = parseArgs(process.argv.slice(2));
	if (options.help) {
		usage();
		process.exit(0);
	}

	const { findings, summary } = options.render
		? renderFindings(fromRepo(options.render))
		: staticFindings(options);

	if (findings.length > 0) {
		console.error(`✗ production publishes ${findings.length} host port(s) outside the allowlist:`);
		for (const finding of findings) console.error(`  × ${finding.service}: ${finding.reason}`);
		console.error('');
		console.error('  fix: drop the publication, or clear an inherited one with `ports: !reset []`');
		console.error('       in docker-compose.prod.yml. A port that must stay published goes in');
		console.error('       ALLOWLIST (scripts/check-prod-ports.mjs) and in the allowlist table of');
		console.error('       docs/deployment.md with a one-line justification.');
		process.exit(1);
	}

	if (!options.quiet) console.log(`ok: ${summary}; 0 host ports published`);
	process.exit(0);
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
	main();
}
