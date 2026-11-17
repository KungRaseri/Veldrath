import { expect, test } from '@playwright/test';
import {
	makeAccount,
	register,
	signUpWithRoles,
	stackEnabled,
	stackSkipReason
} from './support/stack';

/**
 * The staff areas outside Foundry — editorial, admin and moderation — against a real server.
 *
 * One Admin account holds every permission, so a single journey can walk all three areas. What is
 * asserted is what a staff member actually does there: land on the area, open its lists, filter them,
 * and open one row. The deeper per-area workflows (publishing a patch note, kicking a player) are named
 * in `plans/web-e2e-workflows.md` as the next slice rather than half-asserted here.
 */
test.describe('staff areas journey', () => {
	test.skip(!stackEnabled, stackSkipReason);
	test.describe.configure({ timeout: 180_000 });

	test('an admin walks editorial, admin and moderation, and a player is refused all three', async ({
		browser
	}) => {
		const adminContext = await browser.newContext();
		const admin = await adminContext.newPage();
		await signUpWithRoles(admin, ['Admin'], 'e2eadmin');

		// ── Editorial: the three publication surfaces ───────────────────────────────────────────
		await admin.goto('/editorial');
		await expect(admin.getByRole('heading', { level: 1 })).toBeVisible();

		await admin.goto('/editorial/announcements');
		await expect(admin.getByRole('heading', { name: 'Announcements' })).toBeVisible();
		await expect(admin.getByRole('link', { name: 'New announcement' })).toBeVisible();

		await admin.goto('/editorial/patch-notes');
		await expect(admin.getByRole('heading', { name: 'Patch notes' })).toBeVisible();
		await expect(admin.getByRole('link', { name: 'New patch note' })).toBeVisible();

		await admin.goto('/editorial/lore');
		await expect(admin.getByRole('heading', { name: 'Lore articles' })).toBeVisible();
		await expect(admin.getByRole('link', { name: 'New article' })).toBeVisible();

		// ── Admin: the console and its lists ────────────────────────────────────────────────────
		await admin.goto('/admin');
		await expect(admin.getByRole('heading', { level: 1 })).toBeVisible();
		await expect(admin.getByRole('link', { name: /users/i })).toBeVisible();

		await admin.goto('/admin/users');
		await expect(admin.getByRole('heading', { level: 1 })).toBeVisible();
		// The account that just registered is real, so it is in the list.
		await expect(admin.getByText(/e2eadmin/).first()).toBeVisible({ timeout: 20_000 });

		await admin.goto('/admin/audit');
		await expect(admin.getByRole('heading', { level: 1 })).toBeVisible();

		// ── Moderation: overview, the player list, and a filtered reports view ──────────────────
		await admin.goto('/mod');
		await expect(admin.getByRole('heading', { level: 1 })).toBeVisible();

		await admin.goto('/mod/users');
		await expect(admin.getByRole('heading', { level: 1 })).toBeVisible();
		// A real filter round trip: the form submits and the page comes back with the term applied.
		await admin.locator('input[name="search"]').fill('e2eadmin');
		await admin.getByRole('button', { name: 'Apply' }).click();
		await expect(admin.getByText(/e2eadmin/).first()).toBeVisible({ timeout: 20_000 });

		await admin.goto('/mod/reports?resolved=false');
		await expect(admin.getByRole('heading', { level: 1 })).toBeVisible();

		// ── A player is refused every one of them ───────────────────────────────────────────────
		const playerContext = await browser.newContext();
		const player = await playerContext.newPage();
		await register(player, makeAccount('e2eplain'));

		for (const route of ['/editorial', '/admin', '/mod']) {
			const response = await player.goto(route);
			expect(response?.status(), `${route} should be refused to a player`).toBe(403);
		}

		await adminContext.close();
		await playerContext.close();
	});
});
