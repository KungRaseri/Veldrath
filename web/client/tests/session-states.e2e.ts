import { expect, test } from '@playwright/test';
import { makeAccount, register, stackEnabled, stackSkipReason } from './support/stack';

/**
 * The session states a person actually reaches — not the two tidy ones (signed in, anonymous).
 *
 * A session that expires mid-flow is the most common way a player meets the guard: they click a link, get
 * sent to `/login`, sign in, and should land back on the page they asked for. The nested path is the part
 * worth pinning: a redirect that drops the path or the query is a papercut every user hits, and the guard
 * against an off-site redirect lives in `#lib/navigation`.
 */
test.describe('session states journey', () => {
	test.skip(!stackEnabled, stackSkipReason);
	test.describe.configure({ timeout: 120_000 });

	test('a cleared session returns the visitor to the nested page they asked for, query included', async ({
		page,
		context
	}) => {
		const account = makeAccount('e2esession');
		await register(page, account);

		// Sign in, then lose the session the way an expiry would: the cookies are simply gone.
		await context.clearCookies();

		await page.goto('/content/ability?page=1');
		await expect(page).toHaveURL(/\/login\?redirectTo=/);
		// The guard keeps the whole local target, query and all — not just the path.
		const destination = new URL(page.url()).searchParams.get('redirectTo');
		expect(destination).toBe('/content/ability?page=1');

		await page.locator('input[autocomplete="username"]').fill(account.username);
		await page.locator('input[autocomplete="current-password"]').fill(account.password);
		await page.getByRole('button', { name: 'Sign in' }).click();

		// Back where they were, not on the game page.
		await expect(page).toHaveURL(/\/content\/ability\?page=1$/, { timeout: 30_000 });
		await expect(page.getByRole('heading', { level: 1, name: 'ability' })).toBeVisible();
	});

	test('a play URL that is not a character id sends the player to their roster', async ({
		page
	}) => {
		await register(page, makeAccount('e2eplayid'));

		await page.goto('/play/not-a-character-id');
		await expect(page).toHaveURL(/\/characters$/, { timeout: 20_000 });
		await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
	});
});
