import { expect, test } from '@playwright/test';
import {
	apiBase,
	gameToken,
	makeAccount,
	register,
	signUpWithRoles,
	stackEnabled,
	stackSkipReason
} from './support/stack';

/**
 * Moderation against real accounts: warn, mute and unmute a player, and take a player's report from
 * submitted to resolved.
 *
 * The report is created through `POST /api/reports` with the reporter's own minted game token, because a
 * player has no page that files one — the game client does. That is setup, not a shortcut around the
 * journey: the assertion is that the moderator's queue shows the report and resolving it removes it.
 *
 * Kick is deliberately not asserted: it acts on live sessions and this suite never opens the hub, so a
 * kick could only prove "nobody online".
 */
test.describe('moderation actions journey', () => {
	test.skip(!stackEnabled, stackSkipReason);
	test.describe.configure({ timeout: 180_000 });

	test('a moderator warns and mutes a player, then resolves that player’s report', async ({
		browser,
		request
	}) => {
		// The target: a normal player who never enters the game.
		const playerContext = await browser.newContext();
		const player = await playerContext.newPage();
		const target = makeAccount('e2etarget');
		await register(player, target);

		// A second player files a report against the target, the way the game client would.
		const reporterContext = await browser.newContext();
		const reporter = await reporterContext.newPage();
		await register(reporter, makeAccount('e2ereporter'));
		const reason = `E2E report ${Date.now().toString(36)}`;
		const submitted = await request.post(`${apiBase}/api/reports`, {
			headers: { authorization: `Bearer ${await gameToken(reporter)}` },
			data: { targetUsername: target.username, reason }
		});
		expect(submitted.status(), await submitted.text()).toBe(201);

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

		// The detail page is deep-linkable, not only reachable by clicking the row:
		// route /mod/users/[id] — the row above resolves that id, and this re-opens it by URL.
		const userPath = new URL(moderator.url()).pathname;
		await moderator.goto(userPath);
		await expect(moderator.getByRole('heading', { level: 1, name: target.username })).toBeVisible();
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

		// ── The report: in the unresolved queue, then resolved out of it ────────────────────────
		await moderator.goto('/mod/reports?resolved=false');
		const report = moderator.getByRole('row').filter({ hasText: reason });
		await expect(report).toBeVisible({ timeout: 20_000 });
		await report.getByRole('button', { name: 'Resolve' }).click();

		// Resolving redirects to the unfiltered queue, so the row stays — with its status changed. That is
		// the assertion: the report is resolved, not that it vanished from a view it was never in.
		await expect(moderator).toHaveURL(/\/mod\/reports$/, { timeout: 20_000 });
		await expect(
			moderator.getByRole('row').filter({ hasText: reason }).getByText('Resolved')
		).toBeVisible({ timeout: 20_000 });
		await expect(
			moderator
				.getByRole('row')
				.filter({ hasText: reason })
				.getByRole('button', { name: 'Resolve' })
		).toHaveCount(0);

		await playerContext.close();
		await reporterContext.close();
		await modContext.close();
	});

	test('a kick reaches the player inside the game, with the moderator’s reason', async ({
		browser,
		request
	}) => {
		// A player who is actually in the world: the kick acts on a live hub session.
		const playerContext = await browser.newContext();
		const player = await playerContext.newPage();
		const target = makeAccount('e2ekick');
		await register(player, target);

		const token = await gameToken(player);
		const created = await request.post(`${apiBase}/api/characters`, {
			headers: { authorization: `Bearer ${token}` },
			data: {
				name: `Kick${Date.now().toString(36)}`.slice(0, 20),
				className: 'Warrior',
				difficultyMode: 'normal'
			}
		});
		expect(created.status(), await created.text()).toBe(201);
		const character = (await created.json()) as { id: string };

		await player.goto(`/play/${character.id}`);
		// The map renders only once the hub has connected and pushed a snapshot.
		await expect(player.locator('.map-node-cell button').first()).toBeVisible({ timeout: 30_000 });

		const modContext = await browser.newContext();
		const moderator = await modContext.newPage();
		await signUpWithRoles(moderator, ['Moderator'], 'e2ekicker');

		const reason = `E2E kick ${Date.now().toString(36)}`;
		await moderator.goto(`/mod/users?search=${encodeURIComponent(target.username)}`);
		await moderator.getByRole('link', { name: target.username }).click();
		await expect(moderator.getByRole('heading', { level: 1, name: target.username })).toBeVisible({
			timeout: 20_000
		});
		const kickForm = moderator
			.locator('form')
			.filter({ has: moderator.getByRole('button', { name: 'Kick sessions' }) });
		await kickForm.locator('input[name="reason"]').fill(reason);
		await moderator.getByRole('button', { name: 'Kick sessions' }).click();

		// The kicked client shows the reason the moderator typed — the hub's `OnKicked` path.
		await expect(player.getByText(reason)).toBeVisible({ timeout: 30_000 });

		await playerContext.close();
		await modContext.close();
	});
});
