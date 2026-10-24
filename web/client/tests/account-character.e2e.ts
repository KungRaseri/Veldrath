import { expect, test } from '@playwright/test';

test('character creation requires authentication', async ({ page }) => {
	await page.goto('/characters/new');
	await expect(page).toHaveURL(/\/login/);
});

test('account page requires authentication', async ({ page }) => {
	await page.goto('/account');
	await expect(page).toHaveURL(/\/login/);
});

test('login supports email or username and links to password reset', async ({ page }) => {
	await page.goto('/login');

	await expect(page.getByLabel('Email or username')).toBeVisible();
	await expect(page.getByRole('link', { name: 'Forgot password?' })).toBeVisible();
});

test('register collects a username and mentions email verification', async ({ page }) => {
	await page.goto('/register');

	await expect(page.getByLabel('Username')).toBeVisible();
	await expect(page.getByText(/confirmation link/i)).toBeVisible();
});

test('forgot password page renders the reset request form', async ({ page }) => {
	await page.goto('/forgot-password');

	await expect(page.getByRole('heading', { name: 'Forgot your password?' })).toBeVisible();
	await expect(page.getByLabel('Email')).toBeVisible();
	await expect(page.getByRole('button', { name: 'Send reset link' })).toBeVisible();
});

test('verify-email explains a missing token and offers a resend', async ({ page }) => {
	await page.goto('/verify-email');

	await expect(page.getByRole('heading', { name: 'Verification link incomplete' })).toBeVisible();
	await expect(page.getByRole('button', { name: 'Send a new verification link' })).toBeVisible();
});

test('reset-password without a token points back to forgot password', async ({ page }) => {
	await page.goto('/reset-password');

	await expect(page.getByRole('heading', { name: 'Choose a new password' })).toBeVisible();
	await expect(page.getByRole('link', { name: 'forgot password' })).toBeVisible();
});
