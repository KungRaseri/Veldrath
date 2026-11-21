#!/usr/bin/env node
/**
 * Fails when two authored names are one letter apart.
 *
 * D5 found `Orneld` one letter from `Orveld` (the being's Calethic name) and `Gaelraeth` one
 * letter from both `Kael-Raeth` and `Vaelraeth` — the two threshold names the lexicon attests.
 * Every earlier check missed them for one reason: they compared authored **slugs** against the
 * **seeded** slugs. The naming authority is prose (`docs/lore/calethic-language.md`), holds no
 * slugs at all, and was therefore never in scope. This check puts it in scope.
 *
 * What it compares, in three directions:
 *   1. every authored name against every attested form and named entity in the naming authority,
 *   2. every authored name against every seeded slug and seeded display name (`RealmEngine.Data/Seeders/*.cs`),
 *   3. every authored name against every other authored name.
 *
 * A pair fails at edit distance **1**, after normalisation: case-insensitive, hyphens and
 * apostrophes dropped, diacritics folded, a leading article dropped. Distance 0 is not a failure
 * when both sides are the same name being cited — the roster restates seeded display names, and a
 * zone may legitimately use an attested form (`Vaelraeth`). It *is* a failure when two entries
 * carry different slugs and still spell the same name: that is one name for two entities.
 *
 * Where an authored name is defined (the extraction rules — a name is only checked if a document
 * declares it as a name):
 *   - a Markdown table column headed `name`, `display name…`, `byname` or `Calethic`;
 *   - a `displayName:` value in a fenced YAML example;
 *   - a bold, single-token, capitalised word (`**Orneld**`) — the design set's way of asserting a
 *     coined form in prose rather than in a table.
 * Prose that merely mentions a name does not declare it. That is deliberate: a check that guesses
 * at capitalised words in sentences produces a wall of English nouns and gets switched off.
 *
 * Allowlist: `scripts/content-name-collisions.allow.json`. Every entry names a pair and carries a
 * written reason, so a deliberately similar pair is a decision on the record, not a silenced
 * warning. An entry that no longer matches anything is reported as stale rather than failing —
 * F2's renames land on their own card, and the ordering of two merges must not decide whether CI
 * is red. Entries are printed on every run, so a temporary one stays visible.
 *
 * Usage:
 *   node scripts/check-content-names.mjs                 # the gate
 *   node scripts/check-content-names.mjs --no-allowlist  # show every collision, ignore decisions
 *   node scripts/check-content-names.mjs --root <dir>    # check another tree (the tests use this)
 *   node scripts/check-content-names.mjs --verbose       # list the extracted names by source
 */

import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

/** Documents that author names. Missing ones are reported and skipped, never silently ignored. */
export const AUTHORED_DOCUMENTS = [
	'docs/design/content/archetypes.md',
	'docs/design/content/towns.md',
	'docs/design/content/wound-spine.md',
	'docs/design/content/pool-matrix.md'
];

/** The naming authority: attested forms, roots and the named-entity pass. */
export const NAMING_AUTHORITY = 'docs/lore/calethic-language.md';

/** Seeded slugs and display names live here, one file per content family. */
export const SEEDERS_DIRECTORY = 'RealmEngine.Data/Seeders';

/** Pairs that are deliberately similar. Every entry must carry a reason. */
export const ALLOWLIST_PATH = 'scripts/content-name-collisions.allow.json';

const MIN_REASON_LENGTH = 15;
const PLACEHOLDER_REASONS = /^(tbd|todo|n\/?a|none|because|deliberate|known)\b/i;

/**
 * Folds a name to its comparison form: case-insensitive, hyphens and apostrophes dropped,
 * diacritics folded, a leading article dropped, other punctuation turned into word breaks.
 * @param {string} raw The name as written.
 * @returns {string} The normalised key.
 */
export function normalizeName(raw) {
	let value = raw
		.normalize('NFD')
		.replace(/[\u0300-\u036f]/g, '')
		.replace(/[\u2018\u2019']/g, '')
		.replace(/[-\u2010-\u2015]/g, '')
		.toLowerCase()
		.replace(/[^a-z ]+/g, ' ')
		.replace(/\s+/g, ' ')
		.trim();

	if (value.startsWith('the ')) value = value.slice(4);

	return value;
}

/**
 * The form a name takes when it is compared to a slug: the space between words is dropped as well,
 * because a slug is the words run together (`giant-rat` ↔ `Giant Rat`).
 * @param {string} raw The name as written.
 * @returns {string} The slug-comparable key.
 */
export function slugForm(raw) {
	return normalizeName(raw).replace(/ /g, '');
}

/**
 * Levenshtein distance, abandoned as soon as it must exceed `max`.
 * @param {string} left First string.
 * @param {string} right Second string.
 * @param {number} max Largest distance worth reporting.
 * @returns {number} The distance, or -1 when it is larger than `max`.
 */
export function withinDistance(left, right, max) {
	if (left === right) return 0;
	if (Math.abs(left.length - right.length) > max) return -1;

	let previous = Array.from({ length: right.length + 1 }, (_, index) => index);
	for (let i = 1; i <= left.length; i += 1) {
		const current = [i];
		let rowMinimum = i;
		for (let j = 1; j <= right.length; j += 1) {
			const substitution = previous[j - 1] + (left[i - 1] === right[j - 1] ? 0 : 1);
			const insertion = current[j - 1] + 1;
			const deletion = previous[j] + 1;
			const value = Math.min(substitution, insertion, deletion);
			current.push(value);
			if (value < rowMinimum) rowMinimum = value;
		}
		if (rowMinimum > max) return -1;
		previous = current;
	}

	const distance = previous[right.length];
	return distance <= max ? distance : -1;
}

/** Builds a line-start index so a match offset can be reported as a line number. */
function lineIndex(text) {
	const starts = [0];
	for (let index = 0; index < text.length; index += 1) {
		if (text[index] === '\n') starts.push(index + 1);
	}
	return starts;
}

/** Reports the 1-based line number containing `offset`. */
function lineAt(starts, offset) {
	let low = 0;
	let high = starts.length - 1;
	while (low < high) {
		const middle = Math.ceil((low + high) / 2);
		if (starts[middle] <= offset) low = middle;
		else high = middle - 1;
	}
	return low + 1;
}

/** True when a cleaned cell looks like a proper name and nothing else. */
export function isProperName(value, { maxWords = 4, requireLowerCase = false } = {}) {
	if (!/^\p{Lu}[\p{L}'\u2019\- ]+$/u.test(value)) return false;
	if (value.replace(/[^\p{L}]/gu, '').length < 3) return false;
	if (value.split(/\s+/).length > maxWords) return false;
	if (requireLowerCase && !/\p{Ll}/u.test(value)) return false;
	return true;
}

/**
 * Splits a table cell into candidate names, removing the decoration the design set uses around
 * them: bold/italic markers, backticks, the † and § provenance marks, and trailing annotations
 * such as `(new)` or `(exists)`.
 * @param {string} cell The raw cell text.
 * @param {{ maxWords?: number, requireLowerCase?: boolean }} [options] Name shape to accept.
 * @returns {string[]} Candidate names.
 */
export function candidateNames(cell, options = {}) {
	const cleaned = cell
		.replace(/[`*_]/g, '')
		.replace(/[\u2020\u2021\u00a7\u00b6]/g, '')
		.replace(/\s*\((?:new|exists|existing|seeded)[^)]*\)\s*/gi, ' ')
		.replace(/\s*\([^)]*\)\s*$/, '')
		.replace(/\s+/g, ' ')
		.trim();

	if (!cleaned) return [];

	return cleaned
		.split(/\s*(?:,|\/|\band\b)\s*/)
		.map((part) => part.trim())
		.filter((part) => isProperName(part, options));
}

/** True when a table header names a column that holds names. */
function isNameColumn(header) {
	const text = header.replace(/[`*]/g, '').toLowerCase().trim();
	if (/^calethic\b/.test(text)) return true;
	return /(^|[^a-z])names?($|[^a-z])/.test(text);
}

/** True when a table header names a column that holds slugs. */
function isSlugColumn(header) {
	return /^slug\b/.test(header.replace(/[`*]/g, '').toLowerCase().trim());
}

/**
 * Parses every GitHub-flavoured Markdown table in a document.
 * @param {string} text The document.
 * @returns {{ header: string[], rows: { cells: string[], line: number }[], line: number }[]} Tables.
 */
export function parseTables(text) {
	const lines = text.split('\n');
	const tables = [];

	for (let index = 0; index < lines.length - 1; index += 1) {
		const header = lines[index];
		const separator = lines[index + 1];
		if (!header.trim().startsWith('|')) continue;
		if (!/^\s*\|[\s:|-]+\|\s*$/.test(separator) || !separator.includes('---')) continue;

		const headerCells = splitRow(header);
		const rows = [];
		let cursor = index + 2;
		while (cursor < lines.length && lines[cursor].trim().startsWith('|')) {
			rows.push({ cells: splitRow(lines[cursor]), line: cursor + 1 });
			cursor += 1;
		}

		tables.push({ header: headerCells, rows, line: index + 1 });
		index = cursor - 1;
	}

	return tables;
}

/**
 * Finds the table cell that contains a character offset within one line.
 * @param {string} line The raw line.
 * @param {number} offset Offset within the line.
 * @returns {{ index: number, text: string } | null} The cell, or null when the line is not a table row.
 */
function locateCell(line, offset) {
	if (!line.trim().startsWith('|')) return null;

	let index = 0;
	for (let position = 0; position < offset && position < line.length; position += 1) {
		if (line[position] === '\\') {
			position += 1;
		} else if (line[position] === '|') {
			index += 1;
		}
	}

	const cells = splitRow(line);
	const safe = Math.max(0, Math.min(index - 1, cells.length - 1));
	return { index: safe, text: cells[safe] ?? '' };
}

/** Splits one table row into trimmed cells. */
function splitRow(line) {
	const trimmed = line.trim().replace(/^\|/, '').replace(/\|$/, '');
	const cells = [];
	let current = '';
	for (let index = 0; index < trimmed.length; index += 1) {
		const character = trimmed[index];
		if (character === '\\' && trimmed[index + 1] === '|') {
			current += '|';
			index += 1;
		} else if (character === '|') {
			cells.push(current.trim());
			current = '';
		} else {
			current += character;
		}
	}
	cells.push(current.trim());
	return cells;
}

/**
 * Extracts the names a content document declares.
 * @param {string} text The document.
 * @param {string} file Repository-relative path, for the report.
 * @returns {{ name: string, slug: string | null, file: string, line: number, where: string }[]} Names.
 */
export function extractAuthoredNames(text, file) {
	const starts = lineIndex(text);
	const found = [];

	for (const table of parseTables(text)) {
		const nameColumns = table.header
			.map((header, index) => (isNameColumn(header) ? index : -1))
			.filter((index) => index >= 0);
		const slugColumn = table.header.findIndex(isSlugColumn);
		if (nameColumns.length === 0) continue;

		for (const row of table.rows) {
			const slug = slugColumn >= 0 ? row.cells[slugColumn]?.replace(/[`*]|\s*\([^)]*\)/g, '').trim() : null;
			for (const column of nameColumns) {
				const cell = row.cells[column];
				if (!cell) continue;
				for (const name of candidateNames(cell)) {
					found.push({
						name,
						slug: slug && slug.length > 0 ? slug : null,
						file,
						line: row.line,
						where: `table column "${table.header[column].replace(/[`*]/g, '')}"`
					});
				}
			}
		}
	}

	// A fenced YAML example declares a display name the importer would write verbatim.
	const yamlPattern = /^\s*displayName:\s*([^#\n]+?)\s*(?:#.*)?$/gm;
	for (const match of text.matchAll(yamlPattern)) {
		for (const name of candidateNames(match[1])) {
			found.push({
				name,
				slug: null,
				file,
				line: lineAt(starts, match.index),
				where: 'fenced YAML `displayName:`'
			});
		}
	}

	// Bold single tokens: how the design set asserts a coined Calethic form in prose.
	const boldPattern = /\*\*(\p{Lu}[\p{L}'\u2019-]{2,23})\*\*/gu;
	for (const match of text.matchAll(boldPattern)) {
		const line = lineAt(starts, match.index);
		const lineText = text.slice(starts[line - 1], starts[line] ?? text.length);
		const cell = locateCell(lineText, match.index - starts[line - 1]);

		// A bold token in a table's first column is a row label ("**Type** | Wilderness"). A bold
		// token that follows a code identifier in the same cell is a rule severity label
		// ("`pack-family-unknown` — **Error**"). Neither asserts a name.
		if (cell && cell.index === 0) continue;
		if (cell) {
			const positionInCell = cell.text.indexOf(`**${match[1]}**`);
			if (positionInCell > 0 && cell.text.slice(0, positionInCell).includes('`')) continue;
		}

		// "**Zones** are zone slugs", "**Calethic** vocabulary": an emphasised English word that
		// runs straight into a lowercase word is being stressed in a sentence, not declared as a
		// name. A declared name is followed by punctuation, a dash, or the end of the line/cell.
		const after = text.slice(match.index + match[0].length).split('\n')[0];
		if (/^\s+\p{Ll}/u.test(after)) continue;

		found.push({
			name: match[1],
			slug: null,
			file,
			line,
			where: 'bold prose assertion'
		});
	}

	return found;
}

/**
 * Extracts every attested form and named entity from the naming authority.
 *
 * The authority marks Calethic forms with emphasis — `*Orveld*`, `**Vaelraeth**` — and names its
 * entities in the §5 heading tree. Both are read; the plain prose is not, because it is English
 * prose about Calethic and would contribute nothing but nouns.
 * @param {string} text The document.
 * @param {string} file Repository-relative path, for the report.
 * @returns {{ name: string, file: string, line: number, where: string }[]} Attested names.
 */
export function extractAuthorityNames(text, file) {
	const starts = lineIndex(text);
	const found = [];

	const emphasis = /\*\*([^*\n]+?)\*\*|\*([^*\n]+?)\*/g;
	for (const match of text.matchAll(emphasis)) {
		const value = (match[1] ?? match[2]).trim();
		const bold = Boolean(match[1]);
		// A bold multi-word span is a table label ("Calethic (working name)") or an English
		// phrase; a bold single token or hyphenated compound is a Calethic form.
		if (bold && /[()]/.test(value)) continue;
		for (const name of candidateNames(value, { maxWords: 2, requireLowerCase: true })) {
			found.push({
				name,
				file,
				line: lineAt(starts, match.index),
				where: bold ? 'attested form (bold)' : 'attested form (italic)'
			});
		}
	}

	// §5's heading tree names the entities: "#### The Korreld Exchange (sub-location in Crestfall)".
	let topSection = '';
	const lines = text.split('\n');
	for (let index = 0; index < lines.length; index += 1) {
		const line = lines[index];
		const top = /^##\s+(.+?)\s*$/.exec(line);
		if (top) topSection = top[1];
		const heading = /^#{3,4}\s+(.+?)\s*$/.exec(line);
		if (!heading || !topSection.startsWith('5.')) continue;
		const value = heading[1].split('\u2014')[0].replace(/\s*\([^)]*\)\s*/g, ' ').trim();
		for (const name of candidateNames(value, { requireLowerCase: true })) {
			found.push({ name, file, line: index + 1, where: 'named entity (§5 heading)' });
		}
	}

	return found;
}

/**
 * Extracts every seeded slug and display name from the C# seeders.
 * @param {{ file: string, text: string }[]} seeders Seeder files, repository-relative path and text.
 * @returns {{ slugs: {name:string,file:string,line:number,where:string}[], displayNames: {name:string,file:string,line:number,where:string}[] }} Seeded values.
 */
export function extractSeededNames(seeders) {
	const slugs = [];
	const displayNames = [];

	for (const { file, text } of seeders) {
		const starts = lineIndex(text);
		for (const match of text.matchAll(/\bSlug\s*=\s*"([^"]+)"/g)) {
			slugs.push({ name: match[1], file, line: lineAt(starts, match.index), where: 'seeded slug' });
		}
		for (const match of text.matchAll(/\bRefId\s*=\s*"([^"]+)"/g)) {
			slugs.push({ name: match[1], file, line: lineAt(starts, match.index), where: 'seeded map RefId' });
		}
		for (const match of text.matchAll(/\bDisplayName\s*=\s*"([^"]+)"/g)) {
			displayNames.push({
				name: match[1],
				file,
				line: lineAt(starts, match.index),
				where: 'seeded display name'
			});
		}
	}

	return { slugs, displayNames };
}

/**
 * Reads the allowlist. Every entry must name a pair and carry a written reason.
 * @param {string} path Absolute path to the allowlist.
 * @returns {{ entries: { names: string[], reason: string, ticket: string | null }[], errors: string[] }} Result.
 */
export function loadAllowlist(path) {
	if (!existsSync(path)) return { entries: [], errors: [] };

	let parsed;
	try {
		parsed = JSON.parse(readFileSync(path, 'utf8'));
	} catch (error) {
		return { entries: [], errors: [`${ALLOWLIST_PATH}: not valid JSON — ${error.message}`] };
	}

	const allowed = Array.isArray(parsed.allow) ? parsed.allow : null;
	if (!allowed) {
		return { entries: [], errors: [`${ALLOWLIST_PATH}: expected an "allow" array`] };
	}

	const entries = [];
	const errors = [];

	for (const [index, entry] of allowed.entries()) {
		const label = `${ALLOWLIST_PATH}: allow[${index}]`;
		const names = Array.isArray(entry?.names) ? entry.names.filter((name) => typeof name === 'string' && name.trim()) : [];

		if (names.length !== 2) {
			errors.push(`${label}: "names" must list exactly two names`);
			continue;
		}

		const reason = typeof entry.reason === 'string' ? entry.reason.trim() : '';
		if (PLACEHOLDER_REASONS.test(reason)) {
			errors.push(`${label}: "reason" is a placeholder — write the decision down`);
			continue;
		}
		if (reason.length < MIN_REASON_LENGTH) {
			errors.push(`${label}: "reason" is required and must say why the pair is deliberate (at least ${MIN_REASON_LENGTH} characters)`);
			continue;
		}

		entries.push({
			names: names.map((name) => name.trim()),
			reason,
			ticket: typeof entry.ticket === 'string' && entry.ticket.trim() ? entry.ticket.trim() : null
		});
	}

	return { entries, errors };
}

/** A stable key for a pair of names, whatever order the file wrote them in. */
function pairKey(left, right) {
	return [normalizeName(left), normalizeName(right)].sort().join('\u0000');
}

/**
 * Compares authored names against the authority, the catalogue and each other.
 * @param {{ name: string, slug: string | null, file: string, line: number, where: string }[]} authored Authored names.
 * @param {{ name: string, file: string, line: number, where: string }[]} authority Authority forms.
 * @param {{ slugs: any[], displayNames: any[] }} seeded Seeded values.
 * @returns {{ distance: number, kind: string, left: any, right: any }[]} Collisions.
 */
export function findCollisions(authored, authority, seeded) {
	const collisions = [];

	const report = (distance, kind, left, right) => collisions.push({ distance, kind, left, right });

	// Authored against each other.
	for (let i = 0; i < authored.length; i += 1) {
		for (let j = i + 1; j < authored.length; j += 1) {
			const left = authored[i];
			const right = authored[j];
			if (left.file === right.file && left.line === right.line) continue;

			const distance = withinDistance(normalizeName(left.name), normalizeName(right.name), 1);
			if (distance === 1) {
				report(1, 'authored/authored', left, right);
			} else if (distance === 0 && left.slug && right.slug && left.slug !== right.slug) {
				// One spelling, two identities.
				report(0, 'authored/authored', left, right);
			}
		}
	}

	// Authored against the naming authority. An exact match is the document citing the authority.
	for (const entry of dedupeByKey(authority, normalizeName)) {
		for (const name of authored) {
			const distance = withinDistance(normalizeName(name.name), entry.key, 1);
			if (distance === 1) report(1, 'authored/authority', name, entry.source);
		}
	}

	// Authored against the seeded display names, and against the seeded slugs.
	for (const entry of dedupeByKey(seeded.displayNames, normalizeName)) {
		for (const name of authored) {
			const distance = withinDistance(normalizeName(name.name), entry.key, 1);
			if (distance === 1) report(1, 'authored/seeded-name', name, entry.source);
		}
	}

	for (const entry of dedupeByKey(seeded.slugs, slugForm)) {
		for (const name of authored) {
			const distance = withinDistance(slugForm(name.name), entry.key, 1);
			if (distance === 1) report(1, 'authored/seeded-slug', name, entry.source);
		}
	}

	return collisions;
}

/** Collapses entries that normalise to the same key, keeping the first location. */
function dedupeByKey(entries, keyOf) {
	const byKey = new Map();
	for (const entry of entries) {
		const key = keyOf(entry.name);
		if (!key) continue;
		if (!byKey.has(key)) byKey.set(key, { key, source: entry });
	}
	return [...byKey.values()];
}

/**
 * Merges the same pair found in more than one direction into one report, so a name claimed by two
 * documents is listed with every place it is defined rather than once per comparison.
 * @param {{ distance: number, kind: string, left: any, right: any }[]} collisions Raw collisions.
 * @returns {{ key: string, distance: number, kinds: string[], names: { name: string, locations: { file: string, line: number, where: string }[] }[] }[]} Merged.
 */
export function mergeCollisions(collisions) {
	const byPair = new Map();

	for (const collision of collisions) {
		const key = pairKey(collision.left.name, collision.right.name);
		if (!byPair.has(key)) {
			byPair.set(key, { key, distance: collision.distance, kinds: new Set(), names: [] });
		}

		const record = byPair.get(key);
		record.distance = Math.min(record.distance, collision.distance);
		record.kinds.add(collision.kind);

		for (const side of [collision.left, collision.right]) {
			const spelling = normalizeName(side.name);
			const sideSlug = side.slug ?? null;
			// The same spelling from two extraction rules (a table cell and a bold assertion) is one
			// name; only two *different* known slugs make it two identities wearing one spelling.
			let entry = record.names.find(
				(candidate) =>
					normalizeName(candidate.name) === spelling &&
					(candidate.slug === null || sideSlug === null || candidate.slug === sideSlug)
			);
			if (!entry) {
				entry = { name: side.name, slug: sideSlug, locations: [] };
				record.names.push(entry);
			} else if (entry.slug === null && sideSlug) {
				entry.slug = sideSlug;
			}

			const already = entry.locations.some((known) => known.file === side.file && known.line === side.line);
			if (!already) entry.locations.push({ file: side.file, line: side.line, where: side.where });
		}
	}

	return [...byPair.values()]
		.map((record) => ({
			key: record.key,
			distance: record.distance,
			kinds: [...record.kinds].sort(),
			names: [...record.names].sort(
				(left, right) =>
					normalizeName(left.name).localeCompare(normalizeName(right.name)) ||
					String(left.slug).localeCompare(String(right.slug))
			)
		}))
		.sort((left, right) => left.key.localeCompare(right.key));
}

/** Reads the repository-relative seeders. */
function readSeeders(root) {
	const directory = join(root, SEEDERS_DIRECTORY);
	if (!existsSync(directory)) return [];

	return readdirSync(directory)
		.filter((name) => name.endsWith('.cs'))
		.sort()
		.map((name) => {
			const path = join(directory, name);
			return {
				file: `${SEEDERS_DIRECTORY}/${name}`.split(sep).join('/'),
				text: readFileSync(path, 'utf8')
			};
		});
}

/** Parses the command line. */
function parseArguments(argv) {
	const options = { allowlist: true, root: null, verbose: false };

	for (let index = 0; index < argv.length; index += 1) {
		const argument = argv[index];
		if (argument === '--no-allowlist') options.allowlist = false;
		else if (argument === '--verbose') options.verbose = true;
		else if (argument === '--root') {
			options.root = argv[index + 1];
			index += 1;
		} else if (argument.startsWith('--root=')) {
			options.root = argument.slice('--root='.length);
		} else {
			process.stderr.write(`unknown argument "${argument}"\n`);
			process.exit(2);
		}
	}

	return options;
}

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');

function main() {
	const options = parseArguments(process.argv.slice(2));
	const root = options.root ? resolve(options.root) : repoRoot;

	const authored = [];
	const missingDocuments = [];
	for (const document of AUTHORED_DOCUMENTS) {
		const path = join(root, document);
		if (!existsSync(path)) {
			missingDocuments.push(document);
			continue;
		}
		authored.push(...extractAuthoredNames(readFileSync(path, 'utf8'), document));
	}

	const authorityPath = join(root, NAMING_AUTHORITY);
	if (!existsSync(authorityPath)) {
		process.stderr.write(`FAIL  the naming authority is missing: ${NAMING_AUTHORITY}\n`);
		process.exit(1);
	}
	const authority = extractAuthorityNames(readFileSync(authorityPath, 'utf8'), NAMING_AUTHORITY);

	const seeders = readSeeders(root);
	const seeded = extractSeededNames(seeders);

	const allowlist = options.allowlist
		? loadAllowlist(join(root, ALLOWLIST_PATH))
		: { entries: [], errors: [] };

	const collisions = mergeCollisions(findCollisions(authored, authority, seeded));

	// A name that is itself an attested form is worth saying so next to a collision: the pair
	// `Kael-Raeth` ↔ `Vaelraeth` is a distance inside the authority, not a coined name that strayed.
	const attestedByKey = new Map(dedupeByKey(authority, normalizeName).map((entry) => [entry.key, entry.source]));
	for (const collision of collisions) {
		for (const side of collision.names) {
			const attested = attestedByKey.get(normalizeName(side.name));
			if (!attested) continue;
			const known = side.locations.some(
				(location) => location.file === attested.file && location.line === attested.line
			);
			if (!known) side.locations.push({ ...attested, where: `${attested.where} — same form` });
		}
	}

	const decisions = new Map();
	for (const entry of allowlist.entries) decisions.set(pairKey(entry.names[0], entry.names[1]), entry);

	const blocked = [];
	const allowed = [];
	const usedDecisions = new Set();

	for (const collision of collisions) {
		const decision = decisions.get(collision.key);
		if (decision) {
			usedDecisions.add(collision.key);
			allowed.push({ collision, decision });
		} else {
			blocked.push(collision);
		}
	}

	const lines = [];
	lines.push('Content name collision check');
	lines.push(
		`  authored names:  ${authored.length} from ${AUTHORED_DOCUMENTS.length - missingDocuments.length} document(s)`
	);
	lines.push(`  authority forms: ${authority.length} from ${NAMING_AUTHORITY}`);
	lines.push(
		`  seeded names:    ${seeded.slugs.length} slugs, ${seeded.displayNames.length} display names from ${seeders.length} seeder(s)`
	);
	if (options.allowlist) lines.push(`  allowlist:       ${allowlist.entries.length} entr(y/ies) from ${ALLOWLIST_PATH}`);
	else lines.push('  allowlist:       ignored (--no-allowlist)');
	for (const document of missingDocuments) lines.push(`  not present:     ${document} (skipped)`);
	lines.push('');

	for (const error of allowlist.errors) lines.push(`FAIL  ${error}`);
	if (allowlist.errors.length > 0) lines.push('');

	for (const { collision, decision } of allowed) {
		lines.push(describe(collision, 'ALLOWED'));
		lines.push(`      reason: ${decision.reason}${decision.ticket ? ` (${decision.ticket})` : ''}`);
	}

	for (const collision of blocked) lines.push(describe(collision, 'FAIL'));

	for (const entry of allowlist.entries) {
		if (usedDecisions.has(pairKey(entry.names[0], entry.names[1]))) continue;
		lines.push(
			`STALE  allowlist entry "${entry.names[0]}" \u2194 "${entry.names[1]}" matches no current collision — delete it`
		);
	}

	if (options.verbose) {
		lines.push('');
		lines.push('Authored names by document:');
		for (const document of AUTHORED_DOCUMENTS) {
			const names = authored.filter((entry) => entry.file === document);
			if (names.length === 0) continue;
			lines.push(`  ${document} (${names.length})`);
			for (const name of names) lines.push(`    ${name.name}  :${name.line}  ${name.where}`);
		}
	}

	lines.push('');
	if (blocked.length > 0 || allowlist.errors.length > 0) {
		lines.push(
			`${blocked.length} collision(s)${allowed.length > 0 ? `, ${allowed.length} allowlisted` : ''}. ` +
				'Two names one letter apart read as a typo of each other; rename one, or record the pair in ' +
				`${ALLOWLIST_PATH} with a reason.`
		);
		process.stdout.write(`${lines.join('\n')}\n`);
		process.exit(1);
	}

	lines.push(
		`No collisions.${allowed.length > 0 ? ` ${allowed.length} allowlisted pair(s) printed above.` : ''}`
	);
	process.stdout.write(`${lines.join('\n')}\n`);
}

/** One collision, with both sides, the distance, and every place each name is defined. */
function describe(collision, verdict) {
	const { distance, kinds, names } = collision;
	// Two identities wearing one spelling need their slugs in the heading; a distance-1 pair does not.
	const label = (side) => (distance === 0 && side?.slug ? `${side.name} (${side.slug})` : (side?.name ?? '?'));
	const [left, right] = names;
	const heading = `${verdict}  distance ${distance} (${kinds.join(' + ')}): "${label(left)}" \u2194 "${label(right)}"`;
	const width = Math.max(...names.map((side) => label(side).length));
	const lines = [heading];

	for (const side of names) {
		const shown = side.locations.slice(0, 4);
		shown.forEach((location, index) => {
			const nameLabel = index === 0 ? label(side).padEnd(width) : ''.padEnd(width);
			lines.push(`      ${nameLabel}  \u2014 ${location.file}:${location.line}  [${location.where}]`);
		});
		if (side.locations.length > shown.length) {
			lines.push(
				`      ${''.padEnd(width)}  \u2014 and ${side.locations.length - shown.length} more occurrence(s)`
			);
		}
	}

	return lines.join('\n');
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main();
