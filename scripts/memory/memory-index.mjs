#!/usr/bin/env node
/**
 * memory-index.mjs — the generator for `.github/agent-memory/veldrath-memory.md`.
 *
 * The hub file is an index, not a log. Every note belonging to one work item is its
 * own file in `work-items/`, every stable topic is its own file in `areas/`, and the
 * table in the hub is generated from their YAML front matter:
 *
 *     ---
 *     kind: work-item          # or: area
 *     date: 2026-11-23         # yyyy-mm-dd, or "undated"
 *     area: content            # content | engine | web | map | auth | infra
 *     card: t_d97538ea         # optional
 *     pr: 244                  # optional
 *     title: "Progression is server-side; a level-up moves the health bar"
 *     ---
 *
 * Two branches that each add a file never touch the same path, so they cannot
 * conflict; the table is the only shared region, and regenerating it is one command.
 * The hub is rendered **whole** from `scripts/memory/veldrath-memory.template.md` plus
 * that table — the committed hub is never read as input — so `--check` catches a hand
 * edit in the preamble, in the table, or appended after the closing marker.
 *
 * Usage (from the repository root):
 *
 *   node scripts/memory/memory-index.mjs --write     rewrite the whole hub
 *   node scripts/memory/memory-index.mjs --check     exit 1 unless the file is byte-for-byte
 *                                                    what --write would produce (CI gate)
 *   node scripts/memory/memory-index.mjs --verify --from <git-ref> [--rewritten <path>]
 *                                                    one-off migration audit: every
 *                                                    non-blank line of the pre-split hub
 *                                                    is still present exactly once
 *
 * No dependencies: node's own fs, path and child_process only.
 */

import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { dirname, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const MEMORY = join(ROOT, '.github', 'agent-memory');
const HUB = join(MEMORY, 'veldrath-memory.md');
const HUB_REPO_PATH = '.github/agent-memory/veldrath-memory.md';
const TEMPLATE = join(ROOT, 'scripts', 'memory', 'veldrath-memory.template.md');
const TEMPLATE_REPO_PATH = 'scripts/memory/veldrath-memory.template.md';
const KINDS = new Map([
	['work-items', 'work-item'],
	['areas', 'area']
]);
const AREAS = ['content', 'engine', 'web', 'map', 'auth', 'infra'];
const DATE_RE = /^\d{4}-\d{2}-\d{2}$/;
const CARD_RE = /^t_[0-9a-f]{8}$/;
const BEGIN = '<!-- memory-index:begin -->';
const END = '<!-- memory-index:end -->';
const HEADER = '| Date | Area | Card | PR | Title | File |';
const SEPARATOR = '| --- | --- | --- | --- | --- | --- |';

const die = (message) => {
	console.error(`memory-index: ${message}`);
	process.exit(1);
};

const rel = (path) => relative(MEMORY, path).split(sep).join('/');

// ------------------------------------------------------------------ front matter

/**
 * Parses the flat YAML subset the notes use: `key: value`, one per line, values
 * optionally double-quoted. Returns { data, body, bodyLine } where bodyLine is the
 * 1-based line number on which the body starts in the file.
 */
function parseFrontMatter(text, file) {
	if (!text.startsWith('---\n')) die(`${file}: missing YAML front matter (the first line must be "---")`);
	const lines = text.split('\n');
	const close = lines.indexOf('---', 1);
	if (close < 0) die(`${file}: front matter is not closed with "---"`);

	const data = {};
	for (const line of lines.slice(1, close)) {
		const match = /^([a-z][a-z0-9_-]*):(?:[ \t]+(.*))?$/.exec(line);
		if (!match) die(`${file}:${lines.indexOf(line) + 1}: front matter line is not "key: value": ${line}`);
		const key = match[1];
		let value = (match[2] ?? '').trim();
		if (value.startsWith('"')) {
			try {
				value = JSON.parse(value);
			} catch {
				die(`${file}: front matter value for "${key}" is not valid JSON`);
			}
		} else if (value === '') {
			value = '';
		}
		if (key in data) die(`${file}: duplicate front matter key "${key}"`);
		data[key] = value;
	}

	return { data, body: lines.slice(close + 1).join('\n'), bodyLine: close + 2 };
}

function validate(entry) {
	const { file, kind, data } = entry;
	const where = rel(file) || file;

	if (!KINDS.has(dirname(rel(file)).split('/')[0]) || dirname(rel(file)) === '.') {
		die(`${where}: notes must live in work-items/ or areas/`);
	}
	if (!data.kind) die(`${where}: front matter has no kind`);
	if (data.kind !== kind) die(`${where}: kind is "${data.kind}" but the file is in ${rel(dirname(file))}/ (expected "${kind}")`);
	if (!data.title) die(`${where}: front matter has no title`);
	if (!data.date) die(`${where}: front matter has no date (use "undated" when the note has none)`);
	if (data.date !== 'undated' && !DATE_RE.test(data.date)) die(`${where}: date "${data.date}" is neither yyyy-mm-dd nor "undated"`);
	if (!data.area) die(`${where}: front matter has no area`);
	if (!AREAS.includes(data.area)) die(`${where}: area "${data.area}" is not one of ${AREAS.join(', ')}`);
	if (data.card && !CARD_RE.test(data.card)) die(`${where}: card "${data.card}" is not a t_xxxxxxxx id`);
	if (data.pr && !/^\d+$/.test(String(data.pr))) die(`${where}: pr "${data.pr}" is not a pull request number`);
	const expected = kind === 'work-item' ? /^(\d{4}-\d{2}-\d{2}|undated)-(t_[0-9a-f]{8}-)?[a-z0-9-]+\.md$/ : /^[a-z0-9-]+\.md$/;
	const name = rel(file).split('/').pop();
	if (!expected.test(name)) die(`${where}: file name does not follow the naming rule`);
	if (kind === 'work-item') {
		const [, prefix] = /^(\d{4}-\d{2}-\d{2}|undated)/.exec(name);
		if (prefix !== data.date) die(`${where}: the file name says "${prefix}" but date: says "${data.date}"`);
		if (data.card && !name.startsWith(`${prefix}-${data.card}-`)) die(`${where}: the file name does not carry card "${data.card}"`);
	}
}

function readEntries() {
	const entries = [];
	for (const [dirName, kind] of KINDS) {
		const dir = join(MEMORY, dirName);
		if (!existsSync(dir)) continue;
		for (const name of readdirSync(dir).sort()) {
			if (!name.endsWith('.md')) die(`.github/agent-memory/${dirName}/${name}: only .md notes belong in ${dirName}/`);
			const file = join(dir, name);
			const text = readFileSync(file, 'utf8');
			const { data, body, bodyLine } = parseFrontMatter(text, `.github/agent-memory/${dirName}/${name}`);
			if (body.trim() === '') die(`${rel(file)}: front matter with no note below it`);
			const entry = { file, kind, data, text, body, bodyLine };
			validate(entry);
			entries.push(entry);
		}
	}
	return entries;
}

// ------------------------------------------------------------------ rendering

const escapeCell = (value) => String(value).replace(/\|/g, '\\|').replace(/\r?\n/g, ' ');

function compare(a, b) {
	const aDated = a.data.date !== 'undated';
	const bDated = b.data.date !== 'undated';
	// Newest first; notes without a date of their own come last.
	if (aDated !== bDated) return aDated ? -1 : 1;
	if (aDated && a.data.date !== b.data.date) return a.data.date < b.data.date ? 1 : -1;
	if (a.kind !== b.kind) return a.kind === 'work-item' ? -1 : 1;
	const ra = rel(a.file);
	const rb = rel(b.file);
	return ra < rb ? -1 : ra > rb ? 1 : 0;
}

function renderTable(entries) {
	const rows = [...entries].sort(compare).map((entry) => {
		const path = rel(entry.file);
		return `| ${entry.data.date === 'undated' ? '—' : entry.data.date} | ${entry.data.area} | ${
			entry.data.card ? `\`${entry.data.card}\`` : ''
		} | ${entry.data.pr ? `#${entry.data.pr}` : ''} | ${escapeCell(entry.data.title)} | [${path}](${path}) |`;
	});
	return [HEADER, SEPARATOR, ...rows].join('\n');
}

/**
 * Renders the complete hub from the template plus the table built from the notes.
 *
 * The committed hub is never consulted: the output depends only on the template and the
 * note front matter, so `--check` compares the whole file byte-for-byte and a hand edit
 * anywhere — above the markers, in the table, or appended after `END` — is a difference.
 */
function renderHub(entries) {
	let template;
	try {
		template = readFileSync(TEMPLATE, 'utf8');
	} catch {
		die(`cannot read ${TEMPLATE_REPO_PATH} (the hub is generated from it)`);
	}
	const begin = template.indexOf(BEGIN);
	const end = template.indexOf(END);
	if (begin < 0 || end < 0) die(`${TEMPLATE_REPO_PATH}: the table region is marked by ${BEGIN} and ${END}`);
	if (end < begin) die(`${TEMPLATE_REPO_PATH}: ${END} appears before ${BEGIN}`);
	if (template.indexOf(BEGIN, begin + BEGIN.length) >= 0) die(`${TEMPLATE_REPO_PATH}: ${BEGIN} appears more than once`);
	if (template.indexOf(END, end + END.length) >= 0) die(`${TEMPLATE_REPO_PATH}: ${END} appears more than once`);
	return `${template.slice(0, begin + BEGIN.length)}\n${renderTable(entries)}\n${template.slice(end)}`;
}

// ------------------------------------------------------------------ modes

function write() {
	const entries = readEntries();
	const next = renderHub(entries);
	let current = '';
	try {
		current = readFileSync(HUB, 'utf8');
	} catch {
		// A missing hub is simply a hub that needs writing.
	}
	if (next !== current) writeFileSync(HUB, next);
	const counts = entries.reduce((acc, entry) => ({ ...acc, [entry.kind]: (acc[entry.kind] ?? 0) + 1 }), {});
	console.log(
		`memory-index: ${counts['work-item'] ?? 0} work items, ${counts.area ?? 0} areas — ${next === current ? 'already current' : 'wrote'} ${HUB_REPO_PATH}`
	);
}

function check() {
	const entries = readEntries();
	const next = renderHub(entries);
	let current;
	try {
		current = readFileSync(HUB, 'utf8');
	} catch {
		die(`${HUB_REPO_PATH} is missing; run: node scripts/memory/memory-index.mjs --write`);
	}
	if (next === current) {
		console.log(`memory-index: ${HUB_REPO_PATH} matches the generator (${entries.length} rows, whole file)`);
		return;
	}
	const currentLines = current.split('\n');
	const nextLines = next.split('\n');
	const at = nextLines.findIndex((line, index) => line !== currentLines[index]);
	console.error(`memory-index: ${HUB_REPO_PATH} is not what the generator produces.`);
	if (at < 0) {
		const extra = currentLines.length - nextLines.length;
		console.error(`  the committed file has ${extra} line(s) the generator does not produce (a hand edit below the table?)`);
		console.error(`    committed: ${JSON.stringify(currentLines[nextLines.length - 1] ?? '')}`);
	} else {
		console.error(`  first difference at line ${at + 1}:`);
		console.error(`    committed: ${JSON.stringify(currentLines[at] ?? '')}`);
		console.error(`    generated: ${JSON.stringify(nextLines[at] ?? '')}`);
	}
	console.error(`  the file is generated from ${TEMPLATE_REPO_PATH}; do not hand-edit it.`);
	console.error('  run: node scripts/memory/memory-index.mjs --write');
	process.exit(1);
}

// ------------------------------------------------------------------ verify

const normalise = (line) => line.replace(/\]\((?:\.\.\/)+/g, '](');

function gitShow(ref, path) {
	try {
		return execFileSync('git', ['show', `${ref}:${path}`], { cwd: ROOT, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
	} catch {
		die(`cannot read ${path} at ${ref} (is the ref fetched?)`);
	}
}

function countLines(lines) {
	const counts = new Map();
	for (const line of lines) counts.set(line, (counts.get(line) ?? 0) + 1);
	return counts;
}

function verify(options) {
	if (!options.from) die('--verify needs --from <git-ref> (for example origin/development)');
	const originalRaw = gitShow(options.from, HUB_REPO_PATH);
	const originalLines = originalRaw.split('\n');
	if (originalLines[originalLines.length - 1] === '') originalLines.pop();
	const original = [];
	originalLines.forEach((line, index) => {
		if (line.trim() !== '') original.push({ index, line: normalise(line) });
	});
	// The hub's own header is the preamble before the first note heading. It is generated
	// from the template, so it is allowed to change — no note content lives there.
	const firstNoteHeading = originalLines.findIndex((line) => /^#{2,3} /.test(line));

	const entries = readEntries();
	const rewritten = new Set(options.rewritten.map((path) => resolve(ROOT, path)));
	const sources = [];
	for (const entry of entries) {
		const lines = entry.text.split('\n').slice(entry.bodyLine - 1);
		for (const line of lines) if (line.trim() !== '') sources.push({ file: entry.file, line: normalise(line) });
	}
	// The hub's preserved preamble is part of the move too; its new prose is not.
	const hubLines = readFileSync(HUB, 'utf8')
		.split('\n')
		.filter((line) => line.trim() !== '')
		.map(normalise);
	for (const line of hubLines) sources.push({ file: HUB, line });

	// Walk the original against the notes, spending one note occurrence per original
	// line; then walk the notes against the original. Whatever is left over on either
	// side is a lost or an added line.
	const unionBudget = countLines(sources.map((source) => source.line));
	const missing = [];
	for (const { index, line } of original) {
		const left = unionBudget.get(line) ?? 0;
		if (left > 0) unionBudget.set(line, left - 1);
		else missing.push({ index, line });
	}
	const budget = countLines(original.map(({ line }) => line));
	const added = [];
	for (const source of sources) {
		const left = budget.get(source.line) ?? 0;
		if (left > 0) budget.set(source.line, left - 1);
		else added.push(source);
	}

	// A missing line is permitted when its nearest uniquely-owned neighbour belongs to a
	// file that was rewritten on purpose, so a rewrite need not be described line by line.
	const owners = new Map();
	for (const source of sources) {
		if (source.file === HUB) continue;
		const files = owners.get(source.line) ?? new Set();
		files.add(source.file);
		owners.set(source.line, files);
	}
	const originalStrings = original.map(({ line }) => line);
	const attribute = (line) => {
		const index = originalStrings.indexOf(line);
		if (index < 0) return null;
		for (let distance = 1; distance < 200; distance += 1) {
			for (const i of [index + distance, index - distance]) {
				if (i < 0 || i >= originalStrings.length) continue;
				const files = owners.get(originalStrings[i]);
				if (files && files.size === 1) return [...files][0];
			}
		}
		return null;
	};

	const permittedMissing = [];
	const lostMissing = [];
	for (const { index, line } of missing) {
		const file = attribute(line);
		// The header is the hub's, and the hub is generated from the template: permitted.
		if (firstNoteHeading > 0 && index < firstNoteHeading) permittedMissing.push({ file: HUB, line });
		else if (file && rewritten.has(file)) permittedMissing.push({ file, line });
		else lostMissing.push({ file, line });
	}
	const addedByFile = new Map();
	for (const source of added) {
		addedByFile.set(source.file, [...(addedByFile.get(source.file) ?? []), source.line]);
	}
	const hubAdded = addedByFile.get(HUB) ?? [];
	const duplicated = [...addedByFile]
		.filter(([file]) => file !== HUB && !rewritten.has(file))
		.flatMap(([, lines]) => lines);

	const files = entries.length;
	const workItems = entries.filter((entry) => entry.kind === 'work-item').length;
	const areas = entries.filter((entry) => entry.kind === 'area').length;
	console.log(`original   ${options.from}:${HUB_REPO_PATH} — ${originalLines.length} lines, ${original.length} non-blank`);
	console.log(`split      ${workItems} work-item files + ${areas} area files (${files} total)`);
	console.log(`hub        ${hubLines.length} non-blank lines (${hubAdded.length} new, the rest carried from the original)`);
	console.log(`lost       ${lostMissing.length}`);
	console.log(`duplicated ${duplicated.length}`);
	if (permittedMissing.length) {
		const files = [...new Set(permittedMissing.map(({ file }) => (file === HUB ? HUB_REPO_PATH : rel(file))))];
		console.log(`permitted  ${permittedMissing.length} line(s) rewritten on purpose in ${files.join(', ')}:`);
		for (const { file, line } of permittedMissing) console.log(`  - ${rel(file)}: ${line}`);
	}
	if (lostMissing.length) {
		console.error(`lost: ${lostMissing.length} line(s) of the original are in neither the notes nor the hub:`);
		for (const { line } of lostMissing.slice(0, 40)) console.error(`  - ${line}`);
	}
	if (duplicated.length) {
		console.error('duplicated: lines below are in a split file but not in the original:');
		for (const line of duplicated.slice(0, 40)) console.error(`  - ${line}`);
	}
	if (lostMissing.length || duplicated.length) process.exit(1);
	console.log('verify     OK — 0 lines lost, 0 duplicated');
}

// ------------------------------------------------------------------ main

const argv = process.argv.slice(2);
const options = { from: null, rewritten: [] };
const mode = argv.find((arg) => arg.startsWith('--')) ?? '--write';
for (let i = 0; i < argv.length; i += 1) {
	if (argv[i] === '--from') options.from = argv[++i];
	else if (argv[i] === '--rewritten') options.rewritten.push(argv[++i]);
}

switch (mode) {
	case '--write':
		write();
		break;
	case '--check':
		check();
		break;
	case '--verify':
		verify(options);
		break;
	default:
		die(`unknown mode "${mode}" (use --write, --check or --verify)`);
}
