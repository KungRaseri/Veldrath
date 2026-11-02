import { describe, expect, it } from 'vitest';
import { brandDiscordPath, brandGithubPath, brandGooglePath } from '#lib/brand-icons';

/**
 * The brand glyphs are vendored SVG paths rather than a bundled icon set. They are long single
 * lines that are easy to truncate by accident and invisible until a login button renders blank, so
 * their shape is pinned.
 */
describe('brand icon paths', () => {
	it.each([
		['github', brandGithubPath],
		['discord', brandDiscordPath],
		['google', brandGooglePath]
	])('vendors a complete %s path', (_name, path) => {
		expect(path.startsWith('M')).toBe(true);
		expect(path.length).toBeGreaterThan(200);
		expect(path).not.toContain('\n');
	});

	it('keeps the three providers distinct', () => {
		const paths = new Set([brandGithubPath, brandDiscordPath, brandGooglePath]);

		expect(paths.size).toBe(3);
	});
});
