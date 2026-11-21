#!/usr/bin/env node
/**
 * Tests for `scripts/check-content-names.mjs`.
 *
 * Same discipline as `scripts/tests/e2e-reporting.test.mjs`: run the real script against a fixture
 * tree and assert what it did. No mocking of the thing under test. The fixture lives in
 * `scripts/tests/fixtures/content-names/`; each test copies it, mutates the copy, and runs the
 * checker with `--root`, so the negative controls are real collisions rather than hand-built
 * objects.
 *
 * The regression this suite exists for is D5's: `Orneld` one letter from `Orveld`, and `Gaelraeth`
 * one letter from `Kael-Raeth` and `Vaelraeth`. Both had passed every earlier check because those
 * compared authored slugs against seeded slugs and never read the naming authority at all.
 *
 * Run with: node --test scripts/tests/content-names.test.mjs
 */
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { cpSync, mkdtempSync, readFileSync, rmSync, unlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { after, describe, test } from 'node:test';

import { normalizeName, withinDistance } from '../check-content-names.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, '..', '..');
const script = join(repoRoot, 'scripts', 'check-content-names.mjs');
const fixture = join(here, 'fixtures', 'content-names');

const AUTHORED = 'docs/design/content';
const AUTHORITY = 'docs/lore/calethic-language.md';
const ALLOWLIST = 'scripts/content-name-collisions.allow.json';

const workDirectories = [];

/** Copies the fixture tree into a fresh temporary root and returns it. */
function copyFixture() {
	const root = mkdtempSync(join(tmpdir(), 'content-names-'));
	cpSync(fixture, root, { recursive: true });
	workDirectories.push(root);
	return root;
}

after(() => {
	for (const directory of workDirectories) rmSync(directory, { recursive: true, force: true });
});

/** Runs the checker against a tree. */
function check(root, ...args) {
	const result = spawnSync(process.execPath, [script, '--root', root, ...args], { encoding: 'utf8' });
	return { status: result.status, stdout: result.stdout ?? '', stderr: result.stderr ?? '' };
}

/** Reads a file inside a fixture copy. */
function read(root, relative) {
	return readFileSync(join(root, relative), 'utf8');
}

/** Writes a file inside a fixture copy. */
function write(root, relative, text) {
	writeFileSync(join(root, relative), text);
}

/** Replaces one exact span, so a test fails loudly if the fixture moved. */
function replace(root, relative, before, after) {
	const text = read(root, relative);
	assert.ok(text.includes(before), `fixture ${relative} no longer contains ${JSON.stringify(before)}`);
	write(root, relative, text.replace(before, after));
}

/** Inserts a table row directly below the header separator of the first table in a file. */
function addTableRow(root, relative, row) {
	const lines = read(root, relative).split('\n');
	const separator = lines.findIndex((line) => /^\s*\|[\s:|-]+\|\s*$/.test(line) && line.includes('---'));
	assert.ok(separator > 0, `fixture ${relative} has no table`);
	lines.splice(separator + 1, 0, row);
	write(root, relative, lines.join('\n'));
}

/** The fixture's own allowlist entry: `Kael-Raeth` and `Vaelraeth` are both attested and one letter
 * apart in the real naming authority, so the fixture has to record that decision too. */
const CANON_PAIR = {
	names: ['Kael-Raeth', 'Vaelraeth'],
	reason: 'Both are attested forms in the naming authority; the one-letter gap is canon, not a coinage.'
};

/** Writes an allowlist file, keeping the fixture's canon pair unless a test drops it. */
function writeAllowlist(root, allow = [CANON_PAIR]) {
	write(root, ALLOWLIST, `${JSON.stringify({ allow }, null, '	')}\n`);
}

describe('normalisation', () => {
	test('folds case, hyphens, apostrophes and diacritics before measuring', () => {
		assert.equal(normalizeName('Kael-Raeth'), normalizeName('kael raeth').replace(/ /g, ''));
		assert.equal(normalizeName('Vaelraeth'), normalizeName('VAÉLRAETH'));
		assert.equal(normalizeName('The Drove Road'), normalizeName('Drove Road'));
	});

	test('measures distance and abandons a pair beyond the limit', () => {
		assert.equal(withinDistance('orneld', 'orveld', 1), 1);
		assert.equal(withinDistance('gaelraeth', 'kaelraeth', 1), 1);
		assert.equal(withinDistance('brintal', 'brendal', 1), -1);
		assert.equal(withinDistance('same', 'same', 1), 0);
	});
});

describe('a clean tree', () => {
	test('passes and states what it read', () => {
		const root = copyFixture();
		const result = check(root);

		assert.equal(result.status, 0, result.stdout);
		assert.match(result.stdout, /No collisions/);
		assert.match(result.stdout, /authored names: {2}\d+ from 4 document\(s\)/);
		assert.match(result.stdout, new RegExp(`authority forms: \\d+ from ${AUTHORITY}`));
		assert.match(result.stdout, /seeded names: {4}\d+ slugs, \d+ display names from 1 seeder\(s\)/);
		assert.match(result.stdout, /allowlist: {7}1 entry/);
	});

	test('skips a document that is not there, and says so', () => {
		const root = copyFixture();
		unlinkSync(join(root, `${AUTHORED}/pool-matrix.md`));

		const result = check(root);

		assert.equal(result.status, 0, result.stdout);
		assert.match(result.stdout, /not present: {5}docs\/design\/content\/pool-matrix\.md \(skipped\)/);
	});
});

describe('the D5 collisions', () => {
	test('fails on Orneld against Orveld, naming both sides, the distance and both locations', () => {
		const root = copyFixture();
		addTableRow(root, `${AUTHORED}/wound-spine.md`, '| 3 | `orneld` | **Orneld** | 24–26 |');

		const result = check(root);

		assert.equal(result.status, 1, result.stdout);
		assert.match(result.stdout, /FAIL {2}distance 1 \(authored\/authority\): "Orneld" ↔ "Orveld"/);
		assert.match(result.stdout, /Orneld\s+— docs\/design\/content\/wound-spine\.md:\d+/);
		assert.match(result.stdout, /Orveld\s+— docs\/lore\/calethic-language\.md:\d+ {2}\[attested form/);
		assert.match(result.stdout, /1 collision\(s\)(, 1 allowlisted)?\./);
	});

	test('fails on Gaelraeth against both attested threshold names', () => {
		const root = copyFixture();
		addTableRow(root, `${AUTHORED}/towns.md`, '| Gaelraeth | *gael* + *raeth* | "the water\'s threshold" |');
		writeAllowlist(root, []);

		const result = check(root);

		assert.equal(result.status, 1, result.stdout);
		assert.match(result.stdout, /"Gaelraeth" ↔ "Kael-Raeth"/);
		assert.match(result.stdout, /"Gaelraeth" ↔ "Vaelraeth"/);
		// The byname is one definition with two locations in the authored set: the naming table and
		// the placement row. Both are reported.
		assert.match(result.stdout, /Gaelraeth {3}— docs\/design\/content\/towns\.md:\d+ {2}\[table column "Name"\]/);
	});

	test('reports the one-letter gap inside the authority itself', () => {
		const root = copyFixture();
		addTableRow(root, `${AUTHORED}/wound-spine.md`, '| 1 | `vaelraeth` | **Vaelraeth** | 23–24 |');

		// Kael-Raeth and Vaelraeth are both attested and one letter apart. The fixture allowlist
		// says so; without it the pair is a failure no authored rename could clear.
		const allowlisted = check(root);
		assert.equal(allowlisted.status, 0, allowlisted.stdout);
		assert.match(allowlisted.stdout, /ALLOWED {2}distance 1 \(authored\/authority\): "Kael-Raeth" ↔ "Vaelraeth"/);
		assert.match(allowlisted.stdout, /reason: Both are attested forms/);

		const raw = check(root, '--no-allowlist');
		assert.equal(raw.status, 1, raw.stdout);
		assert.match(raw.stdout, /FAIL {2}distance 1 .*"Kael-Raeth" ↔ "Vaelraeth"/);
	});
});

describe('the allowlist', () => {
	test('leaves the pair visible on every run rather than silencing it', () => {
		const root = copyFixture();
		addTableRow(root, `${AUTHORED}/wound-spine.md`, '| 3 | `orneld` | **Orneld** | 24–26 |');
		writeAllowlist(root, [
			CANON_PAIR,
			{ names: ['Orneld', 'Orveld'], reason: 'The zone is renamed on its own card; the pair stays visible until it lands.' }
		]);

		const allowlisted = check(root);
		assert.equal(allowlisted.status, 0, allowlisted.stdout);
		assert.match(allowlisted.stdout, /ALLOWED {2}distance 1/);
		assert.match(allowlisted.stdout, /reason: The zone is renamed on its own card/);

		const raw = check(root, '--no-allowlist');
		assert.equal(raw.status, 1, raw.stdout);
		assert.match(raw.stdout, /FAIL {2}distance 1 .*"Orneld" ↔ "Orveld"/);
	});

	test('refuses an entry with no reason', () => {
		const root = copyFixture();
		writeAllowlist(root, [{ names: ['Orneld', 'Orveld'] }]);

		const result = check(root);

		assert.equal(result.status, 1, result.stdout);
		assert.match(result.stdout, /"reason" is required and must say why the pair is deliberate/);
	});

	test('refuses a placeholder reason', () => {
		const root = copyFixture();
		writeAllowlist(root, [{ names: ['Orneld', 'Orveld'], reason: 'TODO' }]);

		const result = check(root);

		assert.equal(result.status, 1, result.stdout);
		assert.match(result.stdout, /"reason" is a placeholder/);
	});

	test('refuses an entry that does not name exactly two names', () => {
		const root = copyFixture();
		writeAllowlist(root, [{ names: ['Orneld'], reason: 'Only one name listed, so the pair is not stated.' }]);

		const result = check(root);

		assert.equal(result.status, 1, result.stdout);
		assert.match(result.stdout, /"names" must list exactly two names/);
	});

	test('reports an entry that no longer matches anything as stale, without failing the build', () => {
		const root = copyFixture();
		writeAllowlist(root, [
			CANON_PAIR,
			{ names: ['Orneld', 'Orveld'], reason: 'The rename already landed, so this permit is spent.' }
		]);

		const result = check(root);

		assert.equal(result.status, 0, result.stdout);
		assert.match(result.stdout, /STALE {2}allowlist entry "Orneld" ↔ "Orveld" matches no current collision — delete it/);
	});
});

describe('the seeded catalogue', () => {
	test('fails when a seeded display name is reused under a second slug', () => {
		const root = copyFixture();
		addTableRow(root, `${AUTHORED}/archetypes.md`, '| `giant-rat-copy` | Giant Rat | A copy that should not exist. | 1–3 | yes |');
		writeAllowlist(root, []);

		const result = check(root);

		assert.equal(result.status, 1, result.stdout + result.stderr);
		assert.match(result.stdout, /distance 0 \(authored\/authored\): "Giant Rat \(giant-rat\)" ↔ "Giant Rat \(giant-rat-copy\)"/);
		assert.match(result.stdout, /Giant Rat \(giant-rat\)\s+— docs\/design\/content\/archetypes\.md:\d+/);
	});

	test('fails when an authored name is one letter from a seeded display name', () => {
		const root = copyFixture();
		addTableRow(root, `${AUTHORED}/archetypes.md`, '| `giant-rats` | Giant Rats | A near miss of a seeded name. | 1–3 | yes |');
		writeAllowlist(root, []);

		const result = check(root);

		assert.equal(result.status, 1, result.stdout + result.stderr);
		assert.match(result.stdout, /"Giant Rat" ↔ "Giant Rats"/);
		assert.match(result.stdout, /RealmEngine\.Data\/Seeders\/ArchetypeSeeder\.cs:\d+ {2}\[seeded display name\]/);
	});
});
