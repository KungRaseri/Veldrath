import { expect, test } from '@playwright/test';
import {
	makeAccount,
	register,
	signUpWithRoles,
	stackEnabled,
	stackSkipReason
} from './support/stack';

/**
 * The admin console, and the thing it exists to do: grant someone a role and have it take effect.
 *
 * The round trip is the point — an Admin grants Curator, the player's next request in a *different* browser
 * context sees the review queue, and revoking it takes the queue away again. That is only true because the
 * admin endpoints invalidate the account's cached access snapshot; a regression there would leave the role
 * looking granted in the console while the player still saw nothing.
 */
test.describe('admin console journey', () => {
	test.skip(!stackEnabled, stackSkipReason);
	test.describe.configure({ timeout: 180_000 });

	test('an admin grants a role that takes effect in the player’s own session, then revokes it', async ({
		browser
	}) => {
		// The player: signed in, no roles, and specifically no review queue.
		const playerContext = await browser.newContext();
		const player = await playerContext.newPage();
		const target = makeAccount('e2elegacy');
		await register(player, target);
		await player.goto('/foundry');
		await expect(player.getByRole('link', { name: 'Review queue' })).toHaveCount(0);

		// The admin.
		const adminContext = await browser.newContext();
		const admin = await adminContext.newPage();
		await signUpWithRoles(admin, ['Admin'], 'e2econsole');

		await admin.goto(`/admin/users?search=${encodeURIComponent(target.username)}`);
		await admin.getByRole('link', { name: target.username }).click();
		await expect(admin.getByRole('heading', { level: 1, name: target.username })).toBeVisible({
			timeout: 20_000
		});

		// The detail page is deep-linkable, not only reachable by clicking the row:
		// route /admin/users/[id] — the row above resolves that id, and this re-opens it by URL.
		const userPath = new URL(admin.url()).pathname;
		await admin.goto(userPath);
		await expect(admin.getByRole('heading', { level: 1, name: target.username })).toBeVisible();
		const roleForm = admin
			.locator('form')
			.filter({ has: admin.getByRole('button', { name: 'Assign' }) });
		await roleForm.locator('select[name="role"]').selectOption('Curator');
		await admin.getByRole('button', { name: 'Assign' }).click();
		await expect(admin.getByRole('button', { name: 'Revoke Curator' })).toBeVisible({
			timeout: 20_000
		});

		// The grant is live for the player's own session, without signing in again.
		await player.reload();
		await expect(player.getByRole('link', { name: 'Review queue' })).toBeVisible({
			timeout: 20_000
		});
		const queue = await player.goto('/review');
		expect(queue?.status()).toBe(200);

		// Revoking takes it away again.
		await admin.getByRole('button', { name: 'Revoke Curator' }).click();
		await expect(admin.getByRole('button', { name: 'Revoke Curator' })).toHaveCount(0, {
			timeout: 20_000
		});
		const revoked = await player.goto('/review');
		expect(revoked?.status()).toBe(403);

		// ── The console's own pages load for an admin ───────────────────────────────────────────
		for (const route of [
			'/admin',
			'/admin/roles',
			'/admin/sessions',
			'/admin/server',
			'/admin/status',
			'/admin/reports'
		]) {
			const response = await admin.goto(route);
			expect(response?.status(), `${route} should render`).toBe(200);
			await expect(admin.getByRole('heading', { level: 1 })).toBeVisible();
			await expect(admin.getByRole('alert')).toHaveCount(0);
		}

		await playerContext.close();
		await adminContext.close();
	});
});
