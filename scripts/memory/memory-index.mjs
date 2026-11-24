#!/usr/bin/env node
/**
 * memory-index.mjs — keeps `.github/agent-memory/veldrath-memory.md` in step with a template, and
 * validates the notes it points at.
 *
 * The hub is an index, not a log, and the index is the **folder**: one note per work item in
 * `work-items/`, one note per stable topic in `areas/`. Each carries YAML front matter:
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
 * The hub carries **no** generated table. A table of the notes is a pure function of that front
 * matter, and committing it made every note-bearing branch rewrite the same rows — one shared
 * write surface, so any two memory pull requests in flight conflicted there. The hub is instead
 * the template verbatim, which makes adding a note a single new path and nothing else.
 *
 * Usage (from the repository root):
 *
 *   node scripts/memory/memory-index.mjs --write     copy the template over the hub
 *   node scripts/memory/memory-index.mjs --check     exit 1 unless the hub is the template verbatim
 *                                                    and every note's front matter is valid (CI gate)
 *   node scripts/memory/memory-index.mjs --list      print the notes as a table, newest first, to
 *                                                    stdout — read-only, never written anywhere
 *
 * The one-off `--verify --from/--rewritten` audit that proved the file split lost no line is gone
 * deliberately: once the split landed, `veldrath-memory.md` at any ref is the generated hub, so the
 * audit had nothing left to compare it with.
 *
 * No dependencies: node's own fs and path only.
 */

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
const HEADER = '| Date | Area | Card | PR | Title | File |';
const SEPARATOR = '| --- | --- | --- | --- | --- | --- |';

const die = (message) => {
	console.error(`memory-index: ${message}`);
	process.exit(1);
};

const rel = (path) => relative(MEMORY, path).split(sep).join('/');

/** A problem with one note: reported per file, and never fatal to the rest of the run. */
class NoteError extends Error {}

const fail = (message) => {
	throw new NoteError(message);
};

// ------------------------------------------------------------------ front matter

/**
 * Parses the flat YAML subset the notes use: `key: value`, one per line, values optionally
 * double-quoted. Returns { data, body, bodyLine } where bodyLine is the 1-based line number on
 * which the body starts in the file.
 */
function parseFrontMatter(text, where) {
	if (!text.startsWith('---\n')) fail(`${where}: missing YAML front matter (the first line must be "---")`);
	const lines = text.split('\n');
	const close = lines.indexOf('---', 1);
	if (close < 0) fail(`${where}: front matter is not closed with "---"`);

	const data = {};
	for (let index = 1; index < close; index += 1) {
		const line = lines[index];
		const match = /^([a-z][a-z0-9_-]*):(?:[ \t]+(.*))?$/.exec(line);
		if (!match) fail(`${where}:${index + 1}: front matter line is not "key: value": ${line}`);
		const key = match[1];
		let value = (match[2] ?? '').trim();
		if (value.startsWith('"')) {
			try {
				value = JSON.parse(value);
			} catch {
				fail(`${where}:${index + 1}: front matter value for "${key}" is not valid JSON`);
			}
		}
		if (key in data) fail(`${where}:${index + 1}: duplicate front matter key "${key}"`);
		data[key] = value;
	}

	return { data, body: lines.slice(close + 1).join('\n'), bodyLine: close + 2 };
}

function validate(entry) {
	const { file, kind, data } = entry;
	const where = rel(file) || file;

	if (!KINDS.has(dirname(rel(file)).split('/')[0]) || dirname(rel(file)) === '.') fail(`${where}: notes must live in work-items/ or areas/`);
	if (!data.kind) fail(`${where}: front matter has no "kind" (expected "${kind}")`);
	if (data.kind !== kind) fail(`${where}: "kind" is "${data.kind}" but the file is in ${rel(dirname(file))}/ (expected "${kind}")`);
	if (!data.title) fail(`${where}: front matter has no "title"`);
	if (!data.date) fail(`${where}: front matter has no "date" (use "undated" when the note has none)`);
	if (data.date !== 'undated' && !DATE_RE.test(data.date)) fail(`${where}: "date" is "${data.date}", neither yyyy-mm-dd nor "undated"`);
	if (!data.area) fail(`${where}: front matter has no "area" (one of ${AREAS.join(', ')})`);
	if (!AREAS.includes(data.area)) fail(`${where}: "area" is "${data.area}", not one of ${AREAS.join(', ')}`);
	if (data.card && !CARD_RE.test(data.card)) fail(`${where}: "card" is "${data.card}", not a t_xxxxxxxx id`);
	if (data.pr && !/^\d+$/.test(String(data.pr))) fail(`${where}: "pr" is "${data.pr}", not a pull request number`);
	const expected = kind === 'work-item' ? /^(\d{4}-\d{2}-\d{2}|undated)-(t_[0-9a-f]{8}-)?[a-z0-9-]+\.md$/ : /^[a-z0-9-]+\.md$/;
	const name = rel(file).split('/').pop();
	if (!expected.test(name)) fail(`${where}: the file name does not follow the naming rule`);
	if (kind === 'work-item') {
		const [, prefix] = /^(\d{4}-\d{2}-\d{2}|undated)/.exec(name);
		if (prefix !== data.date) fail(`${where}: the file name says "${prefix}" but "date" says "${data.date}"`);
		if (data.card && !name.startsWith(`${prefix}-${data.card}-`)) fail(`${where}: the file name does not carry "card: ${data.card}"`);
	}
}

/**
 * Reads every note, collecting a message per broken file rather than stopping at the first: one
 * bad note must not hide the others, and it must never stop a branch that added a good one.
 */
function readEntries() {
	const entries = [];
	const problems = [];
	for (const [dirName, kind] of KINDS) {
		const dir = join(MEMORY, dirName);
		if (!existsSync(dir)) continue;
		for (const name of readdirSync(dir).sort()) {
			const where = `.github/agent-memory/${dirName}/${name}`;
			if (!name.endsWith('.md')) {
				problems.push(`${where}: only .md notes belong in ${dirName}/`);
				continue;
			}
			const file = join(dir, name);
			const text = readFileSync(file, 'utf8');
			try {
				const { data, body, bodyLine } = parseFrontMatter(text, where);
				if (body.trim() === '') fail(`${where}: front matter with no note below it`);
				const entry = { file, kind, data, text, body, bodyLine };
				validate(entry);
				entries.push(entry);
			} catch (error) {
				if (!(error instanceof NoteError)) throw error;
				problems.push(error.message);
			}
		}
	}
	return { entries, problems };
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

/** The table `--list` prints. Nothing writes it into a file: that is what keeps notes conflict-free. */
function renderTable(entries) {
	const rows = [...entries].sort(compare).map((entry) => {
		const path = rel(entry.file);
		return `| ${entry.data.date === 'undated' ? '—' : entry.data.date} | ${entry.data.area} | ${
			entry.data.card ? `\`${entry.data.card}\`` : ''
		} | ${entry.data.pr ? `#${entry.data.pr}` : ''} | ${escapeCell(entry.data.title)} | [${path}](${path}) |`;
	});
	return [HEADER, SEPARATOR, ...rows].join('\n');
}

function readTemplate() {
	try {
		return readFileSync(TEMPLATE, 'utf8');
	} catch {
		die(`cannot read ${TEMPLATE_REPO_PATH} (the hub is generated from it)`);
	}
}

const counts = (entries) => {
	const byKind = entries.reduce((acc, entry) => ({ ...acc, [entry.kind]: (acc[entry.kind] ?? 0) + 1 }), {});
	return `${byKind['work-item'] ?? 0} work items, ${byKind.area ?? 0} areas`;
};

// ------------------------------------------------------------------ modes

function write() {
	const { entries, problems } = readEntries();
	const next = readTemplate();
	let current = '';
	try {
		current = readFileSync(HUB, 'utf8');
	} catch {
		// A missing hub is simply a hub that needs writing.
	}
	if (next !== current) writeFileSync(HUB, next);
	console.log(`memory-index: ${counts(entries)} — ${next === current ? 'already current' : 'wrote'} ${HUB_REPO_PATH}`);
	for (const problem of problems) console.error(`memory-index: ${problem}`);
	if (problems.length) {
		console.error(`memory-index: ${problems.length} note(s) have invalid front matter`);
		process.exit(1);
	}
}

function check() {
	const problems = [];
	const { entries, problems: noteProblems } = readEntries();
	problems.push(...noteProblems);

	const next = readTemplate();
	let current;
	try {
		current = readFileSync(HUB, 'utf8');
	} catch {
		die(`${HUB_REPO_PATH} is missing; run: node scripts/memory/memory-index.mjs --write`);
	}
	if (next !== current) {
		const currentLines = current.split('\n');
		const nextLines = next.split('\n');
		const at = nextLines.findIndex((line, index) => line !== currentLines[index]);
		const heading = `${HUB_REPO_PATH} is not ${TEMPLATE_REPO_PATH} verbatim`;
		if (at < 0) {
			const extra = currentLines.length - nextLines.length;
			problems.push(`${heading}: the committed file has ${extra} line(s) the template does not (a hand edit at the end?)`);
		} else {
			problems.push(
				`${heading}: first difference at line ${at + 1}\n    committed: ${JSON.stringify(currentLines[at] ?? '')}\n    template:  ${JSON.stringify(nextLines[at] ?? '')}`
			);
		}
	}

	if (problems.length) {
		for (const problem of problems) console.error(`memory-index: ${problem}`);
		console.error(`memory-index: ${problems.length} problem(s); the hub is edited only in ${TEMPLATE_REPO_PATH}, then --write`);
		process.exit(1);
	}
	console.log(`memory-index: ${counts(entries)} valid; ${HUB_REPO_PATH} is ${TEMPLATE_REPO_PATH} verbatim`);
}

function list() {
	const { entries, problems } = readEntries();
	if (problems.length) {
		for (const problem of problems) console.error(`memory-index: ${problem}`);
		console.error(`memory-index: ${problems.length} note(s) have invalid front matter`);
		process.exit(1);
	}
	console.log(renderTable(entries));
}

// ------------------------------------------------------------------ main

const mode = process.argv.slice(2).find((arg) => arg.startsWith('--')) ?? '--write';

switch (mode) {
	case '--write':
		write();
		break;
	case '--check':
		check();
		break;
	case '--list':
		list();
		break;
	default:
		die(`unknown mode "${mode}" (use --write, --check or --list)`);
}
