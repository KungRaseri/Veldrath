import { existsSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { scanSuite, withoutComments, type SourcePath } from '#lib/testing/source-paths';
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
 * So this suite reads the call sites instead of the server. It scans its siblings — and the suites
 * listed in `EXTERNAL_SUITES` — for `readRepoFile('…')`, `walkFiles('…')` and
 * `join(repoRoot(), '…')`, resolves the argument (a quoted literal, a template literal with no
 * interpolation, or any `const`/`let`/`var` holding one, whatever the casing) and checks the path
 * still exists. A move now fails **here**, in one assertion that names the moved path and the suite
 * that reads it, instead of taking the guarding suite down with an ENOENT.
 *
 * The extraction is deliberately static. A `readRepoFile(file)` call fed by `walkFiles('…')` is not
 * followed — the directory literal on the `walkFiles` call is what gets checked — but an argument
 * the guard cannot resolve at all is itself a failure, so a future suite cannot quietly opt out.
 * `#lib/testing/source-paths` holds the reader; its own test pins the resolution rules.
 */

const PARITY_DIR = 'web/client/src/lib/parity';

/**
 * This file reads the other suites, so it must not scan itself: its own patterns contain the call
 * shapes it looks for, and a self-reference would be checking the extractor against itself.
 */
const SELF = `${PARITY_DIR}/source-paths.test.ts`;

/**
 * Source-reading suites outside `PARITY_DIR`, each with the reason it is scanned.
 *
 * The folder above is walked, not listed, so a new parity suite is covered the moment it is added.
 * A suite elsewhere that reads the server is not, and `payload-contract.test.ts` is exactly that:
 * it parses the payloads `GameHub.cs` broadcasts, so a hub move breaks it the same way. Listing the
 * whole of `src/lib/game/` would drag in suites that never touch repo source, so the list is
 * per-suite and every entry carries the reason it is here. An entry is removed only when its suite
 * is deleted or stops reading repo source — never to make the guard green.
 *
 * `web/client/src/lib/testing/harness.test.ts` is deliberately absent: it exercises the helpers in
 * `source.ts`, not a server contract, so a repository move is not a parity failure there.
 */
const EXTERNAL_SUITES: ReadonlyArray<{ suite: string; reason: string }> = [
	{
		suite: 'web/client/src/lib/game/payload-contract.test.ts',
		reason:
			'compares the payloads Veldrath.Server/Hubs/GameHub.cs broadcasts with the interfaces in ' +
			'web/client/src/lib/game/types.ts, so it depends on the same repository layout as a parity suite'
	}
];

const PARITY_SUITES = walkFiles(PARITY_DIR, ['.test.ts']).filter((suite) => suite !== SELF);
const EXTERNAL_PATHS = EXTERNAL_SUITES.map(({ suite }) => suite);

/**
 * Every suite to scan. An external suite whose file is gone is reported by its own assertion
 * instead of being read here — a module-scope ENOENT would take this guard down with it.
 */
const SUITES = [
	...PARITY_SUITES,
	...EXTERNAL_PATHS.filter((suite) => existsSync(join(repoRoot(), suite)))
];
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
				'literal path (or a constant holding one) passed to readRepoFile()/walkFiles().'
		).toEqual([]);
	});

	it('scans every suite outside the parity folder that it lists', () => {
		const missing = EXTERNAL_PATHS.filter((suite) => !existsSync(join(repoRoot(), suite)));
		const unexplained = EXTERNAL_SUITES.filter(({ reason }) => reason.trim().length < 40).map(
			({ suite }) => suite
		);

		expect(
			missing,
			'These suites are listed as source readers but do not exist. Point EXTERNAL_SUITES at the suite ' +
				'that took over, or drop the entry — leaving a stale one silently stops guarding its paths:'
		).toEqual([]);
		expect(
			unexplained,
			'Every suite outside the parity folder carries the reason it is scanned, so the next reader can ' +
				'tell a covered suite from an exemption:'
		).toEqual([]);
	});

	it('reads only paths that still exist under the repository root', () => {
		const missing = PATHS.filter(({ expected }) => expected === 'present')
			.filter(isMissing)
			.map(describePath);

		expect(
			missing,
			'the parity suites read server and client sources off disk, so every path they name is a dependency ' +
				"on this repository's layout. When one of them moves, its suite stops guarding: readRepoFile throws " +
				'at module scope and vitest reports "Tests  no tests", which reads like a broken harness rather ' +
				'than a missing check. Update the call site named below in the same commit as the move:'
		).toEqual([]);
	});

	it('keeps the paths a suite expects to stay gone gone', () => {
		const resurrected = PATHS.filter(({ expected }) => expected === 'absent')
			.filter(({ path }) => existsSync(join(repoRoot(), path)))
			.map(describePath);

		expect(
			resurrected,
			'These paths are asserted absent on purpose — a deleted duplicate that came back, for example. ' +
				'Whatever recreated them either belongs somewhere else or makes the assertion in the suite named ' +
				'below stale; decide which, and do not delete the assertion to silence this test:'
		).toEqual([]);
	});

	it('fails on a path it cannot resolve statically', () => {
		expect(
			UNRESOLVED,
			'These calls pass an argument this guard cannot read, so the path they depend on is unchecked. Pass a ' +
				'quoted literal, a template literal with no ${…}, or a constant holding one, or derive the path ' +
				"from a walkFiles('…') directory so the directory literal is what gets checked:"
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
