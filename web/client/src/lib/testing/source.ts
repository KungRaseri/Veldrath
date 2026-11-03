import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { dirname, join, relative, sep } from 'node:path';

/**
 * Source-reading helpers for the parity tests.
 *
 * The parity suite compares the C# server and the TypeScript client by reading both. Nothing
 * generates one from the other, so a renamed hub method or a dropped endpoint is invisible to
 * the compiler and only shows up at runtime — these helpers make it visible in CI instead.
 */

/** Walks up from `start` until the repository root (the folder holding both projects) is found. */
export function repoRoot(start: string = process.cwd()): string {
	let current = start;

	for (let depth = 0; depth < 8; depth += 1) {
		if (
			existsSync(join(current, 'Veldrath.Server')) &&
			existsSync(join(current, 'web', 'client'))
		) {
			return current;
		}
		current = dirname(current);
	}

	throw new Error(`Could not locate the repository root from ${start}.`);
}

/** Reads a repo-relative file as UTF-8. */
export function readRepoFile(relativePath: string): string {
	return readFileSync(join(repoRoot(), relativePath), 'utf8');
}

/** Removes `//` and `/* *\/` comments so brace and comma scanning is not confused by prose. */
export function stripComments(source: string): string {
	return source.replace(/\/\*[\s\S]*?\*\//g, '').replace(/\/\/[^\n]*/g, '');
}

/** Returns everything between the `{` at `openIndex` and its matching `}`, or `null`. */
export function extractBracedBlock(source: string, openIndex: number): string | null {
	let depth = 0;

	for (let index = openIndex; index < source.length; index += 1) {
		const character = source[index];
		if (character === '{') depth += 1;
		else if (character === '}') {
			depth -= 1;
			if (depth === 0) return source.slice(openIndex + 1, index);
		}
	}

	return null;
}

/** All matches of `pattern`, as arrays of capture groups. */
export function matchAll(source: string, pattern: RegExp): string[][] {
	return [...source.matchAll(pattern)].map((match) => match.slice(1));
}

/**
 * Every file under `directory` (repo-relative) whose path ends with one of `suffixes`.
 *
 * Returned paths are repo-relative and use forward slashes, so they compare cleanly against
 * configuration strings on any platform.
 */
export function walkFiles(directory: string, suffixes: string[]): string[] {
	const absolute = join(repoRoot(), directory);
	const found: string[] = [];

	for (const entry of readdirSync(absolute, { recursive: true, withFileTypes: true })) {
		if (!entry.isFile()) continue;

		const path = join(entry.parentPath, entry.name);
		if (!suffixes.some((suffix) => path.endsWith(suffix))) continue;

		found.push(relative(repoRoot(), path).split(sep).join('/'));
	}

	return found.sort();
}
