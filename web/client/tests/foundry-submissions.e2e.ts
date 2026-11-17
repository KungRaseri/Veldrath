import { expect, test } from '@playwright/test';
import {
	makeAccount,
	register,
	signUpWithRoles,
	stackEnabled,
	stackSkipReason
} from './support/stack';

/**
 * The community contribution loop, against a real server: a player who holds no permissions submits a
 * power through the schema-driven form, a Curator approves it, the catalog gains the entry, and the
 * submitter hears about the decision in the inbox they already use.
 *
 * This is the workflow the portal exists for, and every step crosses the SvelteKit server: the submit
 * action and the review action both call `gameApiFetch`, and the promotion writes the entity through the
 * content write path. Nothing here can be mocked at the browser.
 */
test.describe('foundry submissions journey', () => {
	test.skip(!stackEnabled, stackSkipReason);
	test.describe.configure({ timeout: 180_000 });

	test('a player submits, a curator approves, the catalog gains the entry and the player is told', async ({
		browser
	}) => {
		const stamp = `${Date.now().toString(36)}`;
		const title = `E2E Gift ${stamp}`;
		const slug = `e2e-gift-${stamp}`;
		const notes = 'Reads well; approved for the catalog.';

		// ── The player: no roles at all, which is the point ──────────────────────────────────────
		const playerContext = await browser.newContext();
		const player = await playerContext.newPage();
		const playerAccount = makeAccount('e2eplayer');
		await register(player, playerAccount);

		// The picker offers a review submission to every signed-in account; direct create is editor-only.
		await player.goto('/new');
		await expect(player.getByRole('heading', { level: 1, name: 'New content' })).toBeVisible();
		await player
			.getByRole('listitem')
			.filter({ hasText: 'Abilities' })
			.getByRole('link', { name: 'Submit for review' })
			.click();

		await expect(player.getByRole('heading', { level: 1, name: 'New submission' })).toBeVisible();
		await player.locator('input[name="title"]').fill(title);
		await player.locator('textarea[name="description"]').fill('A small gift for the archive.');
		await player.locator('#submit-displayName').fill(title);
		await player.locator('#submit-slug').fill(slug);
		await player.locator('#submit-typeKey').fill('active/offensive');
		await player.locator('#submit-powerType').selectOption('active');
		await player.getByRole('button', { name: 'Submit for review' }).click();

		// The submission opens where the decision will be recorded, and it starts Pending.
		await expect(player.getByRole('heading', { level: 1, name: title })).toBeVisible({
			timeout: 20_000
		});
		await expect(player.getByText('Pending').first()).toBeVisible();
		const submissionUrl = new URL(player.url());
		const submissionId = submissionUrl.pathname.split('/').pop()!;

		// The default feed is the caller's own rows; a player is never offered Everyone's.
		await player.goto('/submissions');
		await expect(player.getByRole('link', { name: title })).toBeVisible();
		await expect(player.getByRole('link', { name: /everyone/i })).toHaveCount(0);

		// A player cannot open the queue they have no permission for.
		const queue = await player.goto('/review');
		expect(queue?.status()).toBe(403);

		// ── The curator decides ─────────────────────────────────────────────────────────────────
		const curatorContext = await browser.newContext();
		const curator = await curatorContext.newPage();
		await signUpWithRoles(curator, ['Curator'], 'e2ecurator');

		await curator.goto('/review');
		await expect(curator.getByRole('heading', { level: 1, name: 'Review' })).toBeVisible();
		await expect(curator.getByRole('link', { name: title })).toBeVisible({ timeout: 20_000 });
		await curator.getByRole('link', { name: title }).click();
		await expect(curator.getByRole('heading', { name: 'Curator decision' })).toBeVisible();
		await curator.locator('textarea[name="notes"]').fill(notes);
		await curator.getByRole('button', { name: 'Approve', exact: true }).click();

		// The decision is recorded on the submission itself.
		await expect(curator.getByText('Approved').first()).toBeVisible({ timeout: 20_000 });

		// Approving writes the payload to the catalog: the entry now exists and is readable.
		await curator.goto(`/content/ability/${slug}`);
		await expect(curator.getByRole('heading', { level: 1, name: title })).toBeVisible({
			timeout: 20_000
		});
		// A curator decides; it does not author. `canEdit` is `content.manage`, which this role does not hold.
		await expect(curator.getByRole('link', { name: 'Edit this entity' })).toHaveCount(0);

		// ── The submitter is told, and can clear the inbox ──────────────────────────────────────
		await player.goto('/notifications');
		const notification = player.getByRole('listitem').filter({ hasText: title });
		await expect(notification).toBeVisible({ timeout: 20_000 });
		await expect(notification.getByText(/approved/i)).toBeVisible();
		await notification.getByRole('button', { name: 'Mark read' }).click();
		await expect(
			player
				.getByRole('listitem')
				.filter({ hasText: title })
				.getByRole('button', { name: 'Mark read' })
		).toHaveCount(0, { timeout: 20_000 });

		// The decision is also on the player's own view of the submission.
		await player.goto(`/submissions/${submissionId}`);
		await expect(player.getByText('Approved').first()).toBeVisible();
		await expect(player.getByText(notes)).toBeVisible();

		await playerContext.close();
		await curatorContext.close();
	});
});
