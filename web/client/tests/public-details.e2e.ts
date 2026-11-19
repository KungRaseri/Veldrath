import { expect, test } from '@playwright/test';
import { stackEnabled, stackSkipReason } from './support/stack';

/**
 * The public reading surfaces against a real server: lore, patch notes, the leaderboard and a player
 * profile. These pages render seeded editorial and game data, so they need the stack — a static build
 * with no API answers every one of them with an error state instead.
 *
 * The assertion is deliberately "rows or the honest empty state", because both are correct pages; what
 * would be wrong is an error banner or a blank body. The detail routes are exercised through their real
 * failure path (an unknown slug is a 404), which is the part a person actually hits.
 */
test.describe('public reading surfaces', () => {
	test.skip(!stackEnabled, stackSkipReason);
	test.describe.configure({ timeout: 120_000 });

	test('lore, patch notes, leaderboard and profiles render their published state', async ({
		page
	}) => {
		await page.goto('/lore');
		await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
		await expect(page.getByRole('alert')).toHaveCount(0);
		// Scoped to the page body on purpose: an unscoped link locator also matches the site brand in
		// the header, and on a fresh database (no editorial content yet) that made this resolve to two
		// elements and fail as a strict-mode violation rather than testing the page.
		await expect(
			page
				.getByRole('main')
				.getByRole('link', { name: /./ })
				.first()
				.or(page.getByRole('main').getByText(/no .*(entries|articles|lore)/i))
		).toBeVisible({ timeout: 20_000 });

		await page.goto('/patch-notes');
		await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
		await expect(page.getByRole('alert')).toHaveCount(0);

		await page.goto('/leaderboard');
		await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
		await expect(page.getByRole('alert')).toHaveCount(0);

		// The hardcore board is its own page: fallen characters, paged.
		await page.goto('/leaderboard/hardcore');
		await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
		await expect(page.getByRole('alert')).toHaveCount(0);

		// An unknown article is a 404, not a blank page or a 500.
		const missingLore = await page.goto('/lore/e2e-does-not-exist');
		expect(missingLore?.status()).toBe(404);

		const missingPatchNote = await page.goto('/patch-notes/e2e-does-not-exist');
		expect(missingPatchNote?.status()).toBe(404);

		// A profile id that is not an account is a 404, not an empty profile with someone else's name on it.
		const ghost = await page.goto('/players/00000000-0000-0000-0000-000000000000');
		expect(ghost?.status()).toBe(404);

		// The community page reports the server it asked about, so it is proof the API answered.
		await page.goto('/community');
		await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
	});
});
