import { expect, test } from '@playwright/test';
import {
	makeAccount,
	register,
	signUpWithRoles,
	stackEnabled,
	stackSkipReason
} from './support/stack';

/**
 * The reviewer's actual decision: a submission that **changes** an existing entry.
 *
 * Approving is only safe if the reviewer can see what it will write, so the submission page renders a
 * field-by-field diff of the stored entry against the payload. This spec submits a rename of a seeded
 * ability and drives the whole promotion: the diff names the field, the Curator approves, and the catalog
 * serves the new value.
 */
test.describe('review diff journey', () => {
	test.skip(!stackEnabled, stackSkipReason);
	test.describe.configure({ timeout: 180_000 });

	test('a rename is diffed before approval and lands in the catalog after it', async ({
		browser
	}) => {
		const stamp = Date.now().toString(36);
		const title = `E2E Bite rename ${stamp}`;
		const proposedName = `Bite Revised ${stamp}`;

		// ── The player proposes a change to an entry that already exists ────────────────────────
		const playerContext = await browser.newContext();
		const player = await playerContext.newPage();
		await register(player, makeAccount('e2ediff'));

		await player.goto('/submissions/new?type=ability');
		await expect(player.getByRole('heading', { level: 1, name: 'New submission' })).toBeVisible();
		await player.locator('input[name="title"]').fill(title);
		await player.locator('textarea[name="description"]').fill('Renames the seeded bite.');
		await player.locator('#submit-displayName').fill(proposedName);
		await player.locator('#submit-slug').fill('bite');
		await player.locator('#submit-typeKey').fill('active/offensive');
		await player.locator('#submit-powerType').selectOption('active');
		await player.getByRole('button', { name: 'Submit for review' }).click();
		await expect(player.getByRole('heading', { level: 1, name: title })).toBeVisible({
			timeout: 20_000
		});

		// ── The Curator reads the diff, not a JSON blob ────────────────────────────────────────
		const curatorContext = await browser.newContext();
		const curator = await curatorContext.newPage();
		await signUpWithRoles(curator, ['Curator'], 'e2ediffrev');

		await curator.goto('/review');
		await curator.getByRole('link', { name: title }).click();
		await expect(curator.getByRole('heading', { name: 'What approval writes' })).toBeVisible({
			timeout: 20_000
		});
		// The slug line says this replaces the stored entry rather than creating one.
		await expect(curator.getByText('this would replace the stored entry')).toBeVisible();

		const row = curator.getByRole('row').filter({ hasText: 'Display Name' });
		await expect(row).toContainText('Bite');
		await expect(row).toContainText(proposedName);

		await curator.locator('textarea[name="notes"]').fill('Rename approved.');
		await curator.getByRole('button', { name: 'Approve', exact: true }).click();
		await expect(curator.getByText('Approved').first()).toBeVisible({ timeout: 20_000 });

		// ── The catalog serves the approved value ─────────────────────────────────────────────
		await curator.goto('/content/ability/bite');
		await expect(curator.getByRole('heading', { level: 1, name: proposedName })).toBeVisible({
			timeout: 20_000
		});

		await playerContext.close();
		await curatorContext.close();
	});
});
