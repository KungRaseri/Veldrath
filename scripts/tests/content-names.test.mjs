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
 * `fixtures/content-names-f2-landed/` is the second tree that matters: the documents after PR #133
 * record the renames in prose and bold the retired names, and the authority's §7 Session Log bolds
 * them too. On that tree `Orveld` is both an attested form and an authored citation one letter from
 * the attested `Gorveld`, so the check must treat a citation as canon rather than a coinage.
 *
 * It also pins the rule that replaced the `Orneld`/`Orveld` permit. A bold name inside a rename note
 * is a citation of a past state, so it is reported and never compared; a permit would have silenced
 * the pair everywhere, including a zone table that coined the retired name all over again. The
 * negative controls below are the difference between the two: the note's citation passes, a table
 * row — even one abutting the note — and an ordinary-prose bold assertion fail.
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

import { extractAuthorityNames, findCollisions, historicalTableRanges, normalizeName, provenanceNoteRanges, withinDistance } from '../check-content-names.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, '..', '..');
const script = join(repoRoot, 'scripts', 'check-content-names.mjs');
const fixture = join(here, 'fixtures', 'content-names');
const f2Fixture = join(here, 'fixtures', 'content-names-f2-landed');

const AUTHORED = 'docs/design/content';
const AUTHORITY = 'docs/lore/calethic-language.md';
const ALLOWLIST = 'scripts/content-name-collisions.allow.json';

const workDirectories = [];

/** Copies a fixture tree into a fresh temporary root and returns it. */
function copyTree(source) {
	const root = mkdtempSync(join(tmpdir(), 'content-names-'));
	cpSync(source, root, { recursive: true });
	workDirectories.push(root);
	return root;
}

/** Copies the D5 fixture tree. */
function copyFixture() {
	return copyTree(fixture);
}

/** Copies the F2-landed fixture tree. */
function copyF2Fixture() {
	return copyTree(f2Fixture);
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

/** Writes an allowlist file. */
function writeAllowlist(root, allow = []) {
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
		assert.match(result.stdout, /seeded names: {4}\d+ slugs, \d+ display names from 2 seeder\(s\)/);
		assert.match(result.stdout, /allowlist: {7}0 entries/);
	});

	test('treats an authored citation of an attested form as canon, not a coinage', () => {
		const root = copyFixture();

		// The fixture authors `Vaelraeth` (a zone name) and attests both `Vaelraeth` and
		// `Kael-Raeth`, one letter apart in canon. It is a property of the lexicon, so it is not
		// reported as an authored collision and needs no allowlist permit.
		const result = check(root, '--no-allowlist');

		assert.equal(result.status, 0, result.stdout);
		assert.doesNotMatch(result.stdout, /"Kael-Raeth" ↔ "Vaelraeth"/);
	});

	test('fails when an authored document is missing instead of passing on a partial input', () => {
		const root = copyFixture();
		unlinkSync(join(root, `${AUTHORED}/pool-matrix.md`));

		const result = check(root);

		assert.equal(result.status, 1);
		assert.match(result.stderr, /FAIL {2}the authored document is missing: docs\/design\/content\/pool-matrix\.md/);
	});

	test('fails when a seeder directory is missing instead of passing on a partial input', () => {
		const root = copyFixture();
		rmSync(join(root, 'Veldrath.Server/Data/Seeders'), { recursive: true, force: true });

		const result = check(root);

		assert.equal(result.status, 1);
		assert.match(result.stderr, /FAIL {2}the seeder directory is missing: Veldrath\.Server\/Data\/Seeders/);
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

	test('does not report two authored citations of attested canon against each other', () => {
		const root = copyFixture();
		// `Kael-Raeth` is attested; authoring it (as a rename note does) makes both sides of the
		// canon pair authored. Two citations are not an authored naming decision, so the pair is
		// not reported — no permit needed, and the decision stays with the naming authority.
		addTableRow(root, `${AUTHORED}/wound-spine.md`, '| 2 | `kaelraeth` | **Kael-Raeth** | 24–25 |');

		const raw = check(root, '--no-allowlist');

		assert.equal(raw.status, 0, raw.stdout);
		assert.doesNotMatch(raw.stdout, /"Kael-Raeth" ↔ "Vaelraeth"/);
	});
});

describe('the allowlist', () => {
	test('leaves the pair visible on every run rather than silencing it', () => {
		const root = copyFixture();
		addTableRow(root, `${AUTHORED}/wound-spine.md`, '| 3 | `orneld` | **Orneld** | 24–26 |');
		writeAllowlist(root, [
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

describe('the tree where F2 has landed', () => {
	test('passes: the rename notes cite canon instead of coining it', () => {
		const root = copyF2Fixture();

		const result = check(root);

		assert.equal(result.status, 0, result.stdout + result.stderr);
		assert.doesNotMatch(result.stdout, /FAIL/);
		// `Orveld` is attested and the rename note also bolds it; `Gorveld` is attested and one
		// letter away. Both are canon, so no author can rename their way out — it must not fail.
		assert.doesNotMatch(result.stdout, /Gorveld/);
		assert.match(result.stdout, /No collisions\. 1 allowlisted pair\(s\)/);
		// No permit covers the retirement note any more, and none is stale: the rule replaced it
		// outright rather than scoping it to a line that the next edit would move.
		assert.doesNotMatch(result.stdout, /Orneld/);
		assert.doesNotMatch(result.stdout, /STALE/);
	});

	test('treats the retired name in a rename note as a citation, not a declaration', () => {
		const root = copyF2Fixture();

		const raw = check(root, '--no-allowlist');

		assert.equal(raw.status, 1, raw.stdout);
		assert.match(raw.stdout, /plus \d+ citation\(s\) in a rename note \(not compared\)/);
		assert.doesNotMatch(raw.stdout, /"Orneld" ↔ "Orveld"/);
		// The only coinage collision left is the one D5 declined to block: no permit is spent on the
		// retirement note any more.
		assert.match(raw.stdout, /FAIL {2}distance 1 \(authored\/authority\): "Draekeld" ↔ "Drakeld"/);
		assert.match(raw.stdout, /1 collision\(s\)\./);
	});

	test('names the exempted citation and where it sits', () => {
		const root = copyF2Fixture();

		const result = check(root, '--verbose');

		assert.equal(result.status, 0, result.stdout + result.stderr);
		const citations = result.stdout.split('Citations in a rename note')[1] ?? '';
		assert.match(citations, /Orneld {2}— docs\/design\/content\/wound-spine\.md:26/);
	});

	test('fails when a zone table coins the retired name again', () => {
		const root = copyF2Fixture();
		addTableRow(root, `${AUTHORED}/wound-spine.md`, '| 6 | `orneld` | **Orneld** | 26–27 |');

		const result = check(root);

		assert.equal(result.status, 1, result.stdout);
		assert.match(result.stdout, /FAIL {2}distance 1 .*"Orneld" ↔ "Orveld"/);
		// Named at the table row, not at the note: the row is the declaration that fails.
		assert.match(
			result.stdout,
			/Orneld\s+— docs\/design\/content\/wound-spine\.md:\d+ {2}\[table column "display name \(Calethic\)"\]/
		);
	});

	test('fails when the retired name is bolded in ordinary prose', () => {
		const root = copyF2Fixture();
		const text = read(root, `${AUTHORED}/wound-spine.md`);
		write(
			root,
			`${AUTHORED}/wound-spine.md`,
			`${text}\n## 6. Elsewhere\n\nThe old survey still calls the third zone **Orneld**; the rename retired that name.\n`
		);

		const result = check(root);

		assert.equal(result.status, 1, result.stdout);
		assert.match(result.stdout, /"Orneld" ↔ "Orveld"/);
		assert.match(result.stdout, /\[bold prose assertion\]/);
	});

	test('fails when a table row abuts the rename note', () => {
		const root = copyF2Fixture();
		const text = read(root, `${AUTHORED}/wound-spine.md`);
		// No blank line between the note and the table. The note's range ends at the table, so the
		// row is a declaration and the citation rule cannot be used to smuggle a name back in.
		const table = '| # | slug | display name (Calethic) | band |\n|---|---|---|---|\n| 6 | `orneld` | **Orneld** | 26–27 |\n';
		write(root, `${AUTHORED}/wound-spine.md`, `${text}${table}`);

		const result = check(root);

		assert.equal(result.status, 1, result.stdout);
		assert.match(result.stdout, /"Orneld" ↔ "Orveld"/);
		assert.match(result.stdout, /\[table column "display name \(Calethic\)"\]/);
	});

	test('still fails on a new coinage of the renamed zone', () => {
		const root = copyF2Fixture();
		addTableRow(root, `${AUTHORED}/wound-spine.md`, '| 5 | `orndaelm` | **Orndaelm** | 24–26 |');

		const result = check(root);

		assert.equal(result.status, 1, result.stdout);
		assert.match(result.stdout, /"Orndaeld" ↔ "Orndaelm"/);
		assert.match(result.stdout, /Orndaeld {2}— docs\/design\/content\/wound-spine\.md:\d+/);
	});

	test('does not read the session log as an attestation', () => {
		const text = readFileSync(join(f2Fixture, AUTHORITY), 'utf8');
		const attested = extractAuthorityNames(text, AUTHORITY).map((entry) => entry.name);

		assert.ok(historicalTableRanges(text).length > 0, 'the fixture must have a dated table');
		assert.ok(attested.includes('Orveld'), 'the body attests Orveld');
		assert.ok(!attested.includes('Orneld'), 'the session log only cites Orneld as retired');
		assert.ok(!attested.includes('Gaelraeth'), 'the session log only cites Gaelraeth as retired');
	});
});

describe('rename notes', () => {
	test('reads a bold Renamed/Retired lede as a note, including a bulleted one', () => {
		const text = readFileSync(join(f2Fixture, AUTHORED, 'wound-spine.md'), 'utf8');

		assert.deepEqual(provenanceNoteRanges(text), [[26, 29]]);
		assert.deepEqual(provenanceNoteRanges('- **Retired:** it stood as **Orneld**.\n'), [[1, 1]]);
	});

	test('ends a note at a table row instead of swallowing the table below it', () => {
		const text = '**Renamed 2026-11-20.** It stood as **Orneld**.\n| # | name |\n|---|---|\n| 1 | **Orneld** |\n';

		assert.deepEqual(provenanceNoteRanges(text), [[1, 1]]);
	});

	test('does not treat an ordinary bold assertion as a rename note', () => {
		assert.deepEqual(provenanceNoteRanges('Zone three is **Orneld** in the older draft.\n'), []);
	});

	test('reads a bulleted note’s later items as the same note', () => {
		const text = '- **Renamed 2026-11-20.** It stood as **Orneld**.\n- The byname **Gaelraeth** was older still.\n\nProse resumes here.\n';

		assert.deepEqual(provenanceNoteRanges(text), [[1, 2]]);
	});
});

describe('citations are compared with nothing', () => {
	const seeded = {
		slugs: [{ name: 'ornelda', file: 'Seeder.cs', line: 20, where: 'seeded slug' }],
		displayNames: [{ name: 'Ornelda', file: 'Seeder.cs', line: 21, where: 'seeded display name' }]
	};

	test('a rename-note citation is not reported against a seeded name or slug', () => {
		const citation = {
			name: 'Orneld',
			slug: 'orneld',
			file: 'docs/design/content/wound-spine.md',
			line: 188,
			where: 'bold prose assertion',
			citation: true
		};

		assert.deepEqual(findCollisions([citation], [], seeded), []);
	});

	test('the same name as a declaration is reported against both seeded sets', () => {
		const declaration = {
			name: 'Orneld',
			slug: 'orneld',
			file: 'docs/design/content/wound-spine.md',
			line: 146,
			where: 'table column "display name (Calethic)"'
		};

		const kinds = findCollisions([declaration], [], seeded).map((collision) => collision.kind).sort();
		assert.deepEqual(kinds, ['authored/seeded-name', 'authored/seeded-slug']);
	});
});
