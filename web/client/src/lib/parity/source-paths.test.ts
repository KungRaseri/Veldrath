import { existsSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { readRepoFile, repoRoot, walkFiles } from '#lib/testing/source';

/**
 * Parity source-path guard.
 *
 * Every suite in this folder verifies the server by reading C#/TS source off disk. That makes each
 * path in a suite a dependency on the layout of this repository — and the failure it produces when
 * the layout moves is the wrong shape. `readRepoFile` throws at module scope, before any `it()`:
 *
 * ```text
 * FAIL  src/lib/parity/content-schema.test.ts
 * Error: ENOENT: no such file or directory, open '…/ContentEntityTypes.cs'
 *  Tests  no tests
 * ```
 *
 * Nothing else in the repo notices: the C# compiles, the web app builds, and the only signal is a
 * parity job that reds out naming one path with no hint that a guard evaporated rather than failed.
 * `content-schema.test.ts` spent a week in exactly that state, after `ContentEntityTypes.cs` moved to
 * `RealmEngine.Data/Content/`.
 *
 * So this suite reads the call sites instead of the server. It scans its siblings for
 * `readRepoFile('…')`, `walkFiles('…')` and `join(repoRoot(), '…')`, resolves the argument (a quoted
 * literal, or an `UPPER_SNAKE` constant holding one) and checks the path still exists. A move now
 * fails **here**, in one assertion that names the moved path and the suite that reads it, instead of
 * taking the guarding suite down with an ENOENT.
 *
 * The extraction is deliberately static. A `readRepoFile(file)` call fed by `walkFiles('…')` is not
 * followed — the directory literal on the `walkFiles` call is what gets checked — but an argument the
 * guard cannot resolve at all is itself a failure, so a future suite cannot quietly opt out.
 */

const PARITY_DIR = 'web/client/src/lib/parity';

/**
 * This file reads the other suites, so it must not scan itself: its own patterns contain the call
 * shapes it looks for, and a self-reference would be checking the extractor against itself.
 */
const SELF = `${PARITY_DIR}/source-paths.test.ts`;

/** One path a suite depends on, together with the call site that depends on it. */
interface SourcePath {
	/** The repo-relative path. */
	path: string;
	/** What the helper needs it to be. */
	kind: 'file' | 'directory';
	/** `src/lib/parity/hub-methods.test.ts:15` — the suite that reads it, and where. */
	caller: string;
	/** The helper the suite called. */
	helper: string;
}

/** The argument of a call: a quoted literal, a bare identifier, or neither. */
interface CallArgument {
	literal: string | null;
	identifier: string | null;
	index: number;
}

/** Every argument of every `call(…)` in `source`, captured as a literal or a bare identifier. */
function argumentsOf(source: string, call: string): CallArgument[] {
	const pattern = new RegExp(`\\b${call}\\(\\s*(?:'([^']*)'|"([^"]*)"|([A-Za-z_$][\\w$]*))`, 'g');

	return [...source.matchAll(pattern)].map((match) => {
		const literal = match[1] ?? match[2];

		return {
			literal: literal ?? null,
			identifier: literal === undefined ? (match[3] ?? null) : null,
			index: match.index ?? 0
		};
	});
}

/** `const THEME_CSS = '…';` — the way a suite names the file it reads. */
function stringConstants(source: string): Map<string, string> {
	const pattern = /\bconst\s+([A-Z][A-Z0-9_]*)\s*=\s*'([^']*)'\s*;/g;

	return new Map([...source.matchAll(pattern)].map((match) => [match[1], match[2]]));
}

/**
 * Identifiers the suite binds itself: locals, loop variables and arrow parameters.
 *
 * `readRepoFile(file)` inside `for (const file of walkFiles('…'))` is not statically knowable, but it
 * is derived from a directory literal this guard does check — the only shape it accepts.
 */
function boundNames(source: string): Set<string> {
	const names = new Set<string>();

	for (const [, name] of source.matchAll(/\b(?:const|let|var)\s+([A-Za-z_$][\w$]*)/g))
		names.add(name);
	for (const [, name] of source.matchAll(/\(\s*([A-Za-z_$][\w$]*)\s*\)\s*=>/g)) names.add(name);

	return names;
}

/** The paths one suite reads, and the calls whose argument cannot be resolved statically. */
function scanSuite(suite: string, source: string): { paths: SourcePath[]; unresolved: string[] } {
	const constants = stringConstants(source);
	const bound = boundNames(source);
	const paths: SourcePath[] = [];
	const unresolved: string[] = [];

	const at = (index: number) => `${suite}:${source.slice(0, index).split('\n').length}`;

	for (const [call, kind] of [
		['readRepoFile', 'file'],
		['walkFiles', 'directory']
	] as const) {
		for (const argument of argumentsOf(source, call)) {
			const path =
				argument.literal ?? (argument.identifier ? constants.get(argument.identifier) : undefined);

			if (path === undefined) {
				if (!argument.identifier || !bound.has(argument.identifier)) {
					unresolved.push(`${at(argument.index)} — ${call}(${argument.identifier ?? '…'})`);
				}
				continue;
			}

			paths.push({ path, kind, caller: at(argument.index), helper: call });
		}
	}

	// `join(repoRoot(), '…')` is the escape hatch a suite uses to stat one path directly.
	for (const match of source.matchAll(/\bjoin\(\s*repoRoot\(\s*\)\s*,\s*'([^']+)'/g)) {
		paths.push({
			path: match[1],
			kind: 'file',
			caller: at(match.index ?? 0),
			helper: 'join(repoRoot())'
		});
	}

	return { paths, unresolved };
}

/**
 * The suite source with comment text blanked out.
 *
 * Comments are prose *about* paths — the reason a suite reads a file lives in them, and a suite is
 * allowed to name the path it used to read there. Scanning code only keeps a comment from being
 * mistaken for a call site. Newlines are preserved so the line numbers in the messages stay true.
 */
function withoutComments(source: string): string {
	return source
		.replace(/\/\*[\s\S]*?\*\//g, (comment) => comment.replace(/[^\n]/g, ''))
		.replace(/\/\/[^\n]*/g, '');
}

const SUITES = walkFiles(PARITY_DIR, ['.test.ts']).filter((suite) => suite !== SELF);
const SCAN = SUITES.map((suite) => {
	const source = withoutComments(readRepoFile(suite));

	return { suite, source, ...scanSuite(suite, source) };
});
const PATHS = SCAN.flatMap(({ paths }) => paths);
const UNRESOLVED = SCAN.flatMap(({ unresolved }) => unresolved);

/** `true` when the path is gone, or is no longer the file/directory the helper needs. */
function isMissing({ path, kind }: SourcePath): boolean {
	const absolute = join(repoRoot(), path);
	if (!existsSync(absolute)) return true;

	return kind === 'directory' ? !statSync(absolute).isDirectory() : !statSync(absolute).isFile();
}

const describePath = ({ path, kind, caller, helper }: SourcePath) =>
	`${kind} "${path}" — ${helper} at ${caller}`;

describe('parity source paths', () => {
	it('checks at least one path for every suite that reads source', () => {
		const unchecked = SCAN.filter(({ paths }) => paths.length === 0).map(({ suite }) => suite);

		expect(
			SUITES.length,
			'No parity suites were found — the guard has nothing to check.'
		).toBeGreaterThan(0);
		expect(
			unchecked,
			'A source-reading suite whose paths this guard cannot see is a suite nobody is guarding. Give it a ' +
				'literal path (or an UPPER_SNAKE constant holding one) passed to readRepoFile()/walkFiles().'
		).toEqual([]);
	});

	it('reads only paths that still exist under the repository root', () => {
		const missing = PATHS.filter(isMissing).map(describePath);

		expect(
			missing,
			'the parity suites read server and client sources off disk, so every path they name is a dependency ' +
				"on this repository's layout. When one of them moves, its suite stops guarding: readRepoFile throws " +
				'at module scope and vitest reports "Tests  no tests", which reads like a broken harness rather ' +
				'than a missing check. Update the call site named below in the same commit as the move:'
		).toEqual([]);
	});

	it('fails on a path it cannot resolve statically', () => {
		expect(
			UNRESOLVED,
			'These calls pass an argument this guard cannot read, so the path they depend on is unchecked. Pass a ' +
				"quoted literal or an UPPER_SNAKE constant holding one, or derive the path from a walkFiles('…') " +
				'directory so the directory literal is what gets checked:'
		).toEqual([]);
	});

	it('never resolves a fixture from the working directory', () => {
		const offenders = SCAN.filter(({ source }) => /\bprocess\.cwd\(\)/.test(source)).map(
			({ suite }) => suite
		);

		expect(
			offenders,
			'This suite runs with its cwd at web/client, web or the repository root depending on the caller, so ' +
				"process.cwd() is not the repository root: resolve('..', '..') from web/ leaves the repo entirely. " +
				'Use repoRoot()/readRepoFile() from #lib/testing/source instead:'
		).toEqual([]);
	});
});
