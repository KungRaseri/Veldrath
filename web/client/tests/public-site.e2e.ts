import { expect, test } from '@playwright/test';

test('home page renders the public landing content', async ({ page }) => {
	await page.goto('/');
	await expect(page.getByRole('heading', { name: 'Veldrath', level: 1 })).toBeVisible();
	await expect(page.getByRole('link', { name: 'Explore the lore' })).toBeVisible();
});

test('lore index renders', async ({ page }) => {
	await page.goto('/lore');
	await expect(page.getByRole('heading', { name: 'Lore', level: 1 })).toBeVisible();
});

test('patch notes index renders', async ({ page }) => {
	await page.goto('/patch-notes');
	await expect(page.getByRole('heading', { name: 'Patch notes', level: 1 })).toBeVisible();
});

test('community page renders', async ({ page }) => {
	await page.goto('/community');
	await expect(page.getByRole('heading', { name: 'Community', level: 1 })).toBeVisible();
	// Scope to the heading: the offline-state copy also contains "game server".
	await expect(page.getByRole('heading', { name: 'Game server' })).toBeVisible();
});

test('forgot password page renders', async ({ page }) => {
	await page.goto('/forgot-password');
	await expect(page.getByRole('heading', { name: 'Forgot your password?' })).toBeVisible();
});

test('account page requires authentication', async ({ page }) => {
	await page.goto('/account');
	await expect(page).toHaveURL(/\/login/);
});
