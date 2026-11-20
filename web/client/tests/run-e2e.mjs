#!/usr/bin/env node
/**
 * Run one named set of Playwright specs from `tests/e2e-manifest.json`.
 *
 * The manifest is the single source of truth for which spec runs where, because the same list
 * used to be copied by hand into `web/client/package.json` and `.github/workflows/ci-web.yml`
 * — and the copies had already drifted (`tests/encounters.e2e.ts` ran locally and never in CI).
 *
 * This runner also points the reporters at per-run files, so the stack-free run, the stack run
 * and the outage run in one job cannot overwrite each other's JSON, JUnit, HTML report or
 * traces. See `plans/e2e-reporting.md`.
 *
 * Usage:
 *   node tests/run-e2e.mjs <run>              run every spec the manifest lists for <run>
 *   node tests/run-e2e.mjs <run> --list       print the resolved spec list and exit
 *   node tests/run-e2e.mjs <run> --dry-run    print the command and the env it would use
 *   node tests/run-e2e.mjs <run> --only tests/harvest.e2e.ts
 *                                             run exactly one spec of that run (debugging)
 *   node tests/run-e2e.mjs <run> -- --grep foo
 *                                             pass the remaining arguments to Playwright
 *
 * Exit codes: 0/1 from Playwright, 2 for a usage or manifest error, so a broken manifest is
 * never mistaken for a failing test.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * One run in `tests/e2e-manifest.json`.
 * @typedef {object} E2ERun
 * @property {string[]} specs    spec paths, relative to `web/client`
 * @property {Record<string, string>} [env]   defaults only; the environment wins
 * @property {number} [workers]  Playwright worker count for this run
 * @property {string[]} [requires] env vars that must be '1' or the run is refused
 */

/**
 * The whole manifest.
 * @typedef {object} E2EManifest
 * @property {Record<string, E2ERun>} runs
 * @property {Record<string, string>} [gates] why each gate is required
 */

const here = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(here, '..');
const require = createRequire(import.meta.url);

const reportRoot = resolve(clientRoot, process.env.E2E_REPORT_DIR ?? 'test-results');
const htmlRoot = resolve(clientRoot, process.env.E2E_HTML_DIR ?? 'playwright-report');

/**
 * Print a usage/manifest error and exit 2.
 * @param {string} message what went wrong
 * @returns {never} never returns — it exits, which is what lets callers treat it as terminal
 */
function fail(message) {
	console.error(`error: ${message}`);
	process.exit(2);
}

/**
 * @returns {E2EManifest} the parsed manifest
 */
function readManifest() {
	const path = join(here, 'e2e-manifest.json');
	if (!existsSync(path)) fail(`no manifest at ${path}`);
	try {
		return JSON.parse(readFileSync(path, 'utf8'));
	} catch (error) {
		fail(`${path} is not valid JSON: ${error instanceof Error ? error.message : String(error)}`);
	}
}

function resolvePlaywrightCli() {
	// `playwright/cli.js` is not an exported subpath (`ERR_PACKAGE_PATH_NOT_EXPORTED`), so
	// resolve the package entry point and take `cli.js` beside it. Both packages keep the CLI at
	// the package root, and `playwright` is the one that owns `test`.
	const candidates = ['playwright', '@playwright/test'];
	for (const id of candidates) {
		try {
			const cli = join(dirname(require.resolve(id)), 'cli.js');
			if (existsSync(cli)) return cli;
		} catch {
			// try the next package; a workspace may have pruned one of them
		}
	}
	fail('cannot resolve the Playwright CLI — is `npm ci` done in web/?');
}

const argv = process.argv.slice(2);
const passthrough = argv.includes('--') ? argv.slice(argv.indexOf('--') + 1) : [];
const own = argv.includes('--') ? argv.slice(0, argv.indexOf('--')) : argv;

const listOnly = own.includes('--list');
const dryRun = own.includes('--dry-run');
const onlyIndex = own.indexOf('--only');
const only = onlyIndex === -1 ? null : own[onlyIndex + 1];

const positional = own.filter(
	(arg, index) => !arg.startsWith('--') && (onlyIndex === -1 || index !== onlyIndex + 1)
);

if (onlyIndex !== -1 && !only) fail('--only needs a spec path');

const manifest = readManifest();
const runs = manifest.runs ?? {};
const runNames = Object.keys(runs);

if (positional.length === 0) {
	console.error(`usage: node tests/run-e2e.mjs <${runNames.join('|')}>`);
	console.error(
		`runs: ${runNames.map((name) => `${name} (${runs[name].specs.length} specs)`).join(', ')}`
	);
	process.exit(2);
}
if (positional.length > 1) fail(`expected one run name, got: ${positional.join(' ')}`);

const runName = positional[0];
const run = runs[runName];
if (!run) fail(`no run named "${runName}" in the manifest (known: ${runNames.join(', ')})`);
if (!Array.isArray(run.specs) || run.specs.length === 0) fail(`run "${runName}" lists no specs`);

// Every spec named in the run has to exist, or the run silently tests less than it claims.
const missing = run.specs.filter((spec) => !existsSync(resolve(clientRoot, spec)));
if (missing.length > 0) fail(`run "${runName}" names missing spec files: ${missing.join(', ')}`);

if (only && !run.specs.includes(only)) fail(`"${only}" is not part of run "${runName}"`);

const specs = only ? [only] : run.specs;
const label = runName;

// The manifest's env is a set of *defaults* for a local isolated stack; anything already set in the
// environment wins, because CI runs the same specs against its own ports (a Postgres service
// container and the API on 9000) and must not be dragged onto 4273/9100/55432 by the manifest.
//
// Typed as a string map on purpose: svelte-check type-checks this file (the tsconfig has `checkJs`), and
// without the annotation `env[gate]` is an implicit-any index error — which is how this file broke the
// `Build and Test` job while `node --test` was perfectly happy with it.
/** @type {Record<string, string | undefined>} */
const env = {
	...(run.env ?? {}),
	...process.env,
	// Reporters read these (see playwright.config.ts). Per-run names are the whole point: two
	// Playwright invocations in one CI job would otherwise truncate each other's files.
	PW_RUN_NAME: runName,
	PW_JSON_REPORT: join(reportRoot, `e2e-${label}.json`),
	PW_JUNIT_REPORT: join(reportRoot, `e2e-${label}.xml`),
	PW_HTML_REPORT: join(htmlRoot, label),
	PW_OUTPUT_DIR: join(reportRoot, label),
	...(process.env.CI ? { CI: 'true' } : {})
};

// `--list` and `--dry-run` must work on a checkout without dependencies installed, so the CLI is
// resolved only when a run is actually started.
if (listOnly) {
	console.log(specs.join('\n'));
	process.exit(0);
}

// The gate is evaluated against the *effective* env, not process.env: the manifest supplies the defaults, so a
// plain `npm run test:e2e:stack` has them even though nothing exported them — reading process.env made the
// documented local command refuse to start while CI (which does export them) was the only place it worked.
// An explicitly empty value (`E2E_STACK=`) is still the way to ask for the refusal, which is what the tooling
// test uses to prove the message.
if (run.requires?.length) {
	const unmet = run.requires.filter((gate) => (env[gate] ?? '') !== '1');
	if (unmet.length > 0) {
		const why = unmet.map((gate) => `${gate}: ${manifest.gates?.[gate] ?? 'required'}`).join('; ');
		console.error(`error: run "${runName}" needs ${why}`);
		console.error('The specs would skip themselves; refusing to produce an all-skipped report.');
		process.exit(2);
	}
}

function buildCommand() {
	const cliArgs = [resolvePlaywrightCli(), 'test', ...specs];
	if (typeof run.workers === 'number') cliArgs.push(`--workers=${run.workers}`);
	cliArgs.push(...passthrough);
	return cliArgs;
}

if (dryRun) {
	console.log(`run:    ${runName}`);
	console.log(`specs:  ${specs.length}`);
	console.log(`env:    ${JSON.stringify({ ...(run.env ?? {}), PW_RUN_NAME: runName }, null, 2)}`);
	console.log(`json:   ${env.PW_JSON_REPORT}`);
	console.log(`junit:  ${env.PW_JUNIT_REPORT}`);
	console.log(`html:   ${env.PW_HTML_REPORT}`);
	console.log(`traces: ${env.PW_OUTPUT_DIR}`);
	console.log(`command: node ${buildCommand().join(' ')}`);
	process.exit(0);
}

const cliArgs = buildCommand();

console.log(`[e2e] run "${runName}": ${specs.length} spec(s), reports → ${env.PW_JSON_REPORT}`);

const result = spawnSync(process.execPath, cliArgs, {
	cwd: clientRoot,
	env,
	stdio: 'inherit'
});

if (result.error) fail(`could not start Playwright: ${result.error.message}`);
process.exit(result.status ?? 1);
