import { expect, test } from '@playwright/test';

/**
 * The Foundry map editor, against a real server and a real staff account.
 *
 * Unlike the public specs, this one cannot create its own account: authoring needs `content.manage` and
 * reviewing needs the Curator role, and granting either is an admin action rather than something a test
 * may do. It therefore runs only when pointed at an account that already has them — set `E2E_FOUNDRY=1`,
 * `E2E_FOUNDRY_USER` and `E2E_FOUNDRY_PASSWORD` — and is skipped otherwise, so `npm run test` stays
 * server-free and never fails for a reason that is not about the code.
 *
 * The unit and component suites carry the detail: canvas rules, edge rules, node rules, the inspector,
 * the lifecycle verbs and the review gate are all covered there and in `Veldrath.Server.Tests`. What this
 * spec adds is proof that the real page, the real session and the real API agree on one flow.
 */
const enabled = process.env.E2E_FOUNDRY === '1';
const username = process.env.E2E_FOUNDRY_USER ?? '';
const password = process.env.E2E_FOUNDRY_PASSWORD ?? '';

test.describe('foundry map editor', () => {
	test.skip(!enabled || !username || !password, 'needs E2E_FOUNDRY=1 and a staff account');

	test('signs in, opens a seeded graph, validates it and shows the result', async ({ page }) => {
		await page.goto('/login');
		await page.locator('input[autocomplete="username"]').fill(username);
		await page.locator('input[autocomplete="current-password"]').fill(password);
		await page.getByRole('button', { name: 'Sign in' }).click();
		await page.waitForURL((url) => !url.pathname.startsWith('/login'), { timeout: 20_000 });

		// The editor is reachable from the Foundry navigation, not only by typing the URL.
		await page.getByRole('link', { name: 'Maps' }).click();
		await expect(page.getByRole('heading', { level: 1, name: 'Maps' })).toBeVisible();
		await expect(page.getByRole('columnheader', { name: 'State' })).toBeVisible();

		await page.locator('tbody tr a.link').first().click();

		// One draft, one writer: the canvas, the metadata form and the inspector all read the same object.
		await expect(page.getByRole('region', { name: 'Map details' })).toBeVisible();
		await expect(page.getByTestId('map-canvas')).toBeVisible();
		await expect(page.getByRole('region', { name: 'Edges' })).toBeVisible();

		// Validating runs the world validator without writing, and the banner says which happened.
		await page.getByRole('button', { name: 'Validate' }).click();
		await expect(
			page.getByText(/No errors — a save would accept this graph|This graph would be refused/)
		).toBeVisible();
	});
});
