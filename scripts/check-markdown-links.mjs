#!/usr/bin/env node
/**
 * Validates the relative links in every tracked Markdown file.
 *
 * The repository has been through a large restructure (Blazor removal, plan pruning, web
 * migration), which repeatedly left docs pointing at files that no longer exist. This check is
 * deliberately offline: it only resolves relative paths, so it never flakes on network access or
 * third-party rate limits. External URLs are ignored.
 *
 * Skipped on purpose:
 *   - `web/llms/**` — vendored upstream documentation with its own broken links.
 *   - `wiki/**`     — a git submodule with its own repository and CI.
 *   - build output, dependency, and coverage directories.
 *
 * A line containing `link-ignore` is skipped, for intentional references to files that never
 * live in the repository (for example a path a reader is expected to create).
 *
 * Usage: `node scripts/check-markdown-links.mjs` (from the repository root).
 */

import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');

const SKIP_DIRECTORY_NAMES = new Set([
	'.git',
	'.idea',
	'.svelte-kit',
	'.vs',
	'bin',
	'build',
	'build-desktop',
	'CoverageReport',
	'node_modules',
	'obj',
	'TestResults'
]);

const SKIP_PATH_PREFIXES = ['web/llms', 'wiki'];

const LINK_PATTERN = /!?\[[^\]]*\]\(([^)\s]+)(?:\s+"[^"]*")?\)/g;

/** Recursively collects Markdown files under `directory`. */
function collectMarkdownFiles(directory) {
	const found = [];

	for (const entry of readdirSync(directory, { withFileTypes: true })) {
		if (entry.name.startsWith('.') && entry.name !== '.github') continue;

		const absolute = join(directory, entry.name);
		const repositoryPath = relative(repoRoot, absolute).split(sep).join('/');

		if (entry.isDirectory()) {
			if (SKIP_DIRECTORY_NAMES.has(entry.name)) continue;
			if (SKIP_PATH_PREFIXES.some((prefix) => repositoryPath === prefix || repositoryPath.startsWith(`${prefix}/`)))
				continue;
			found.push(...collectMarkdownFiles(absolute));
			continue;
		}

		if (entry.isFile() && entry.name.toLowerCase().endsWith('.md')) found.push(absolute);
	}

	return found;
}

/** True when the link target does not point at a file in this repository. */
function isExternal(target) {
	return (
		/^[a-z][a-z0-9+.-]*:/i.test(target) || // scheme: https:, mailto:, tel:
		target.startsWith('//') ||
		target.startsWith('#') ||
		target.includes('{{') ||
		target.includes('<') ||
		target.includes('$')
	);
}

const files = collectMarkdownFiles(repoRoot).sort();
const problems = [];

for (const file of files) {
	const contents = readFileSync(file, 'utf8');
	const lines = contents.split(/\r?\n/u);
	const displayPath = relative(repoRoot, file).split(sep).join('/');

	lines.forEach((line, index) => {
		if (line.includes('link-ignore')) return;

		for (const match of line.matchAll(LINK_PATTERN)) {
			const rawTarget = match[1].trim();
			if (!rawTarget || isExternal(rawTarget)) continue;

			// Strip a query string or fragment and the repository's `path:line` suffix
			// (`../Veldrath.Server/Program.cs:101`); only the file path matters here.
			const targetPath = decodeURIComponent(rawTarget.split('#')[0].split('?')[0]).replace(
				/:\d+(?:-\d+)?$/u,
				''
			);
			if (!targetPath) continue;

			const resolved = resolve(dirname(file), targetPath);
			if (existsSync(resolved) && statSync(resolved)) continue;

			problems.push(`${displayPath}:${index + 1} -> ${rawTarget}`);
		}
	});
}

if (problems.length > 0) {
	console.error(`Broken relative links (${problems.length}):`);
	for (const problem of problems) console.error(`  ${problem}`);
	process.exit(1);
}

console.log(`Checked ${files.length} Markdown files: no broken relative links.`);
