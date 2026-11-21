/**
 * Static reading of the paths a source-reading test suite depends on.
 *
 * The parity suites compare the C# server and the TypeScript client by reading both off disk, so
 * every path in one of them is a dependency on this repository's layout — and the failure it
 * produces when that layout moves is the wrong shape: `readRepoFile` throws at module scope, before
 * any `it()`, and vitest reports `Tests  no tests`, which reads like a broken harness rather than a
 * missing check.
 *
 * `scanSuite` reads the call sites instead of the sources. It resolves three shapes:
 *
 * - a quoted literal: `readRepoFile('Veldrath.Server/Hubs/GameHub.cs')`
 * - a template literal with no interpolation: ``readRepoFile(`web/client/src/routes/theme.css`)``
 * - an identifier bound to either by a `const`/`let`/`var` declaration, whatever the casing
 *
 * A read of a `walkFiles('…')` entry is accepted as derived — `for (const file of walkFiles('…'))`
 * or `walkFiles('…').map((file) => …)` — because the directory literal is what gets checked. Every
 * other argument is reported as unresolved instead of being skipped, so a suite cannot opt out of
 * the guard by holding its path in an expression this reader does not follow.
 */

/** One path a suite depends on, together with the call site that depends on it. */
export interface SourcePath {
	/** The repo-relative path. */
	path: string;
	/** What the helper needs it to be. */
	kind: 'file' | 'directory';
	/** Whether the suite expects the path to be there, or to stay gone. */
	expected: 'present' | 'absent';
	/** `src/lib/parity/hub-methods.test.ts:15` — the suite that reads it, and where. */
	caller: string;
	/** The helper the suite called. */
	helper: string;
}

/** The paths one suite reads, and the calls whose argument cannot be resolved statically. */
export interface SuiteScan {
	/** Everything the suite reads, each with the call site that reads it. */
	paths: SourcePath[];
	/** `<suite>:<line> — readRepoFile(…)` for each argument this reader cannot resolve. */
	unresolved: string[];
}

/** The argument of a call: a static path, a template with holes, a bare identifier, or neither. */
interface CallArgument {
	/** The path the argument names, when it is knowable without running the code. */
	literal: string | null;
	/** Raw text of a template literal that interpolates — its path exists only at runtime. */
	template: string | null;
	/** The name of the variable passed instead of a path. */
	identifier: string | null;
	/** Where the call starts, for the line number in the message. */
	index: number;
}

/** Every argument of every `call(…)` in `source`, captured as a literal, a template or an identifier. */
function argumentsOf(source: string, call: string): CallArgument[] {
	const pattern = new RegExp(
		`\\b${call}\\(\\s*(?:'([^']*)'|"([^"]*)"|\`([^\`]*)\`|([A-Za-z_$][\\w$]*))`,
		'g'
	);

	return [...source.matchAll(pattern)].map((match) => {
		const quoted = match[1] ?? match[2];
		const template = match[3];
		const index = match.index ?? 0;

		if (quoted !== undefined) return { literal: quoted, template: null, identifier: null, index };

		if (template !== undefined) {
			const interpolates = template.includes('${');

			return {
				literal: interpolates ? null : template,
				template: interpolates ? template : null,
				identifier: null,
				index
			};
		}

		return { literal: null, template: null, identifier: match[4] ?? null, index };
	});
}

/**
 * `const THEME_CSS = '…';` — every variable bound straight to a quoted literal, whatever the casing.
 *
 * Casing carries no meaning here: `themeCss`, `theme_css` and `THEME_CSS` name the same file, and a
 * table that only saw `UPPER_SNAKE` made a lower-case constant a way to disappear from the scan.
 * Both quote styles are read for the same reason — the answer to "which path is this" does not
 * depend on which quote character the author typed.
 */
function stringConstants(source: string): Map<string, string> {
	const pattern =
		/\b(?:const|let|var)\s+([A-Za-z_$][\w$]*)\s*=\s*(?:'([^']*)'|"([^"]*)")\s*(?:;|\n)/g;

	return new Map([...source.matchAll(pattern)].map((match) => [match[1], match[2] ?? match[3]]));
}

/**
 * Identifiers bound to an entry of a `walkFiles('…')` directory this reader does check.
 *
 * `for (const file of walkFiles('…')) readRepoFile(file)` and
 * `walkFiles('…').map((file) => readRepoFile(file))` are the accepted derived shapes: the directory
 * literal is the path that gets verified and the identifier is only a name for one of its files. No
 * other binding earns that trust — a local that cannot be traced back to a literal is reported, so
 * this reader fails closed rather than assuming a suite knows what it is doing.
 */
function derivedFromWalk(source: string): Set<string> {
	const names = new Set<string>();

	for (const [, name] of source.matchAll(
		/\bfor\s*\(\s*(?:const|let|var)\s+([A-Za-z_$][\w$]*)\s+of\s+walkFiles\s*\(/g
	))
		names.add(name);

	for (const [, name] of source.matchAll(
		/\bwalkFiles\s*\([^()]*\)\s*\.\s*\w+\s*\(\s*\(\s*([A-Za-z_$][\w$]*)\s*\)/g
	))
		names.add(name);

	return names;
}

/**
 * Whether the assertion around `index` expects the path to be there or to stay gone.
 *
 * `content-schema.test.ts` asserts `existsSync(join(repoRoot(), '…/ContentEntityTypes.cs'))` is
 * `.toBe(false)`: that file is deliberately absent, because a second copy of the type-key map must
 * not come back. A reader that only understands "every path must exist" calls that assertion a
 * broken suite. The matcher is read from the end of the same `expect(…)` call, so a later assertion
 * cannot flip the polarity of this one.
 */
function expectationOf(source: string, index: number): 'present' | 'absent' {
	const matcher =
		/^[\s\S]{0,500}?\)\s*\.(toBe\(\s*(true|false)\s*\)|toBeFalsy\(\)|toBeTruthy\(\))/.exec(
			source.slice(index)
		);

	const matcherIsAbsent = matcher?.[2] === 'false' || matcher?.[1].startsWith('toBeFalsy');

	return matcherIsAbsent ? 'absent' : 'present';
}

/** The paths one suite reads, and the calls whose argument cannot be resolved statically. */
export function scanSuite(suite: string, source: string): SuiteScan {
	const constants = stringConstants(source);
	const derived = derivedFromWalk(source);
	const paths: SourcePath[] = [];
	const unresolved: string[] = [];

	const at = (index: number) => `${suite}:${source.slice(0, index).split('\n').length}`;

	for (const [call, kind] of [
		['readRepoFile', 'file'],
		['walkFiles', 'directory']
	] as const) {
		for (const argument of argumentsOf(source, call)) {
			const where = at(argument.index);

			if (argument.template !== null) {
				unresolved.push(`${where} — ${call}(\`${argument.template}\`)`);
				continue;
			}

			const path =
				argument.literal ?? (argument.identifier ? constants.get(argument.identifier) : undefined);

			if (path !== undefined) {
				paths.push({ path, kind, expected: 'present', caller: where, helper: call });
				continue;
			}

			// An entry of a `walkFiles('…')` directory: that literal is already in `paths`.
			if (argument.identifier && derived.has(argument.identifier)) continue;

			unresolved.push(`${where} — ${call}(${argument.identifier ?? '…'})`);
		}
	}

	// `join(repoRoot(), '…')` is the escape hatch a suite uses to stat one path directly.
	for (const match of source.matchAll(/\bjoin\(\s*repoRoot\(\s*\)\s*,\s*'([^']+)'/g)) {
		const index = match.index ?? 0;

		paths.push({
			path: match[1],
			kind: 'file',
			expected: expectationOf(source, index),
			caller: at(index),
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
export function withoutComments(source: string): string {
	return source
		.replace(/\/\*[\s\S]*?\*\//g, (comment) => comment.replace(/[^\n]/g, ''))
		.replace(/\/\/[^\n]*/g, '');
}
