import { describe, expect, it } from 'vitest';
import { scanSuite, withoutComments } from '#lib/testing/source-paths';

/**
 * The resolution rules of the source-path reader, pinned on fixtures.
 *
 * `src/lib/parity/source-paths.test.ts` runs these rules over the real suites, which means a rule
 * that quietly stops working — as the constant table did when it only read `UPPER_SNAKE` names —
 * still leaves that guard green while a path in a suite goes unchecked. These fixtures fail first.
 */

const SUITE = 'src/lib/parity/example.test.ts';

const scan = (source: string) => scanSuite(SUITE, source);

const resolved = (source: string) =>
	scan(source).paths.map(({ path, kind, expected, caller, helper }) => ({
		path,
		kind,
		expected,
		caller,
		helper
	}));

describe('parity source-path reader', () => {
	it('resolves a quoted literal and reports the line it sits on', () => {
		expect(resolved("import 'x';\nreadRepoFile('web/client/src/routes/theme.css');")).toEqual([
			{
				path: 'web/client/src/routes/theme.css',
				kind: 'file',
				expected: 'present',
				caller: `${SUITE}:2`,
				helper: 'readRepoFile'
			}
		]);
	});

	it('resolves a double-quoted literal', () => {
		expect(resolved('readRepoFile("docs/design/tokens.json");')).toEqual([
			{
				path: 'docs/design/tokens.json',
				kind: 'file',
				expected: 'present',
				caller: `${SUITE}:1`,
				helper: 'readRepoFile'
			}
		]);
	});

	it('resolves a constant whatever the casing of its name', () => {
		const source = [
			"const UPPER_SNAKE = 'a/upper.css';",
			"const lowerCase = 'a/lower.css';",
			"const snake_case = 'a/snake.css';",
			'readRepoFile(UPPER_SNAKE);',
			'readRepoFile(lowerCase);',
			'readRepoFile(snake_case);'
		].join('\n');

		expect(scan(source).paths.map(({ path }) => path)).toEqual([
			'a/upper.css',
			'a/lower.css',
			'a/snake.css'
		]);
		expect(scan(source).unresolved).toEqual([]);
	});

	it('resolves a template literal with no interpolation', () => {
		expect(resolved('readRepoFile(`web/client/src/lib/game/types.ts`);')).toEqual([
			{
				path: 'web/client/src/lib/game/types.ts',
				kind: 'file',
				expected: 'present',
				caller: `${SUITE}:1`,
				helper: 'readRepoFile'
			}
		]);
	});

	it('reports a template literal that interpolates instead of skipping it', () => {
		const result = scan('readRepoFile(`a/${name}.css`);');

		expect(result.paths).toEqual([]);
		expect(result.unresolved).toEqual([`${SUITE}:1 — readRepoFile(\`a/\${name}.css\`)`]);
	});

	it('accepts a loop variable over a walkFiles directory and checks the directory', () => {
		const source = [
			"for (const file of walkFiles('web/client/src', ['.ts'])) {",
			'\treadRepoFile(file);',
			'}'
		].join('\n');

		expect(resolved(source)).toEqual([
			{
				path: 'web/client/src',
				kind: 'directory',
				expected: 'present',
				caller: `${SUITE}:1`,
				helper: 'walkFiles'
			}
		]);
		expect(scan(source).unresolved).toEqual([]);
	});

	it('accepts an arrow parameter over a walkFiles directory', () => {
		const source =
			"walkFiles('web/client/tests', ['.e2e.ts'])\n\t.map((file) => readRepoFile(file));";

		expect(scan(source).unresolved).toEqual([]);
		expect(scan(source).paths.map(({ path }) => path)).toEqual(['web/client/tests']);
	});

	it('reports any other bound identifier instead of assuming it is derived', () => {
		const source = 'const mystery = pickPath();\nreadRepoFile(mystery);';

		expect(scan(source).paths).toEqual([]);
		expect(scan(source).unresolved).toEqual([`${SUITE}:2 — readRepoFile(mystery)`]);
	});

	it('reports a call expression it cannot read rather than dropping the call site', () => {
		expect(scan("readRepoFile(join('a', 'b'));").unresolved).toEqual([
			`${SUITE}:1 — readRepoFile(join)`
		]);
	});

	it('reads the polarity of a join(repoRoot()) assertion', () => {
		const source = [
			"expect(existsSync(join(repoRoot(), 'gone/file.cs')), 'stays gone').toBe(false);",
			"expect(existsSync(join(repoRoot(), 'here/file.svelte'))).toBe(true);"
		].join('\n');

		expect(
			resolved(source).map(({ path, expected, helper }) => ({ path, expected, helper }))
		).toEqual([
			{ path: 'gone/file.cs', expected: 'absent', helper: 'join(repoRoot())' },
			{ path: 'here/file.svelte', expected: 'present', helper: 'join(repoRoot())' }
		]);
	});

	it('blanks comments without moving the line numbers', () => {
		const source = withoutComments(
			"// readRepoFile('used/to/live/here.css')\nreadRepoFile('real/path.css');"
		);
		const result = scanSuite(SUITE, source);

		expect(result.paths.map(({ path, caller }) => [path, caller])).toEqual([
			['real/path.css', `${SUITE}:2`]
		]);
	});
});
