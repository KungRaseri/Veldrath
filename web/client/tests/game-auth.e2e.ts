import { expect, test } from '@playwright/test';

test('character select requires authentication', async ({ page }) => {
	await page.goto('/characters');
	await expect(page).toHaveURL(/\/login/);
});

test('play route requires authentication and a valid character id', async ({ page }) => {
	await page.goto('/play/00000000-0000-0000-0000-000000000000');
	await expect(page).toHaveURL(/\/login/);
});

test('legacy game route redirects to character select', async ({ page }) => {
	await page.goto('/game');
	await expect(page).toHaveURL(/\/characters|\/login/);
});
