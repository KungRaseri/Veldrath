import { expect, test } from '@playwright/test';
import {
	makeAccount,
	register,
	signUpWithRoles,
	stackEnabled,
	stackSkipReason
} from './support/stack';

/**
 * Moderation actions against a real account: warn, mute and unmute a player, and see the state the
 * moderator changed on the page. Kick is deliberately not asserted — it acts on live sessions, and this
 * suite never opens the hub, so a kick could only prove that "nobody online" is refused.
 *
 * The target is a real registered player rather than seeded data, so the chips ("1 warnings", "Muted")
 * are the server's answer about that account and not a fixture.
 */
test.describe('moderation actions journey', () => {
	test.skip(!stackEnabled, stackSkipReason);
	test.describe.configure({ timeout: 180_000 });

	test('a moderator warns and mutes a player, and the player page reflects it', async ({
		browser
	}) => {
		// The target: a normal player who never enters the game.
		const playerContext = await browser.newContext();
		const player = await playerContext.newPage();
		const target = makeAccount('e2etarget');
		await register(player, target);

		const modContext = await browser.newContext();
		const moderator = await modContext.newPage();
		await signUpWithRoles(moderator, ['Moderator'], 'e2emod');

		// Find the player through the moderation search, which is the real path to a user page.
		await moderator.goto('/mod/users');
		await moderator.locator('input[name="search"]').fill(target.username);
		await moderator.getByRole('button', { name: 'Apply' }).click();
		await moderator.getByRole('link', { name: target.username }).click();
		await expect(moderator.getByRole('heading', { level: 1, name: target.username })).toBeVisible({
			timeout: 20_000
		});
		await expect(moderator.getByText('0 warnings')).toBeVisible();

		// ── Warn ────────────────────────────────────────────────────────────────────────────────
		const warnForm = moderator
			.locator('form')
			.filter({ has: moderator.getByRole('button', { name: 'Issue warning' }) });
		await warnForm.locator('input[name="reason"]').fill('Testing the warning path.');
		await moderator.getByRole('button', { name: 'Issue warning' }).click();
		await expect(moderator.getByText('1 warnings')).toBeVisible({ timeout: 20_000 });

		// ── Mute with a duration, then lift it ──────────────────────────────────────────────────
		// Scoped by the duration input: "Unmute" also contains "Mute", so a text match would be ambiguous.
		const muteForm = moderator
			.locator('form')
			.filter({ has: moderator.locator('input[name="durationMinutes"]') });
		await muteForm.locator('input[name="reason"]').fill('Testing the mute path.');
		await muteForm.locator('input[name="durationMinutes"]').fill('30');
		await moderator.getByRole('button', { name: 'Mute', exact: true }).click();
		await expect(moderator.getByText('Muted', { exact: true })).toBeVisible({ timeout: 20_000 });

		await moderator.getByRole('button', { name: 'Unmute' }).click();
		await expect(moderator.getByText('Muted', { exact: true })).toHaveCount(0, { timeout: 20_000 });

		// ── Reports: the queue renders for a moderator, resolved or empty ───────────────────────
		await moderator.goto('/mod/reports');
		await expect(moderator.getByRole('heading', { level: 1 })).toBeVisible();
		await expect(moderator.getByRole('alert')).toHaveCount(0);

		await playerContext.close();
		await modContext.close();
	});
});
