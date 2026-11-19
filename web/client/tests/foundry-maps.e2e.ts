import { expect, test } from '@playwright/test';
import { signUpWithRoles, stackEnabled, stackSkipReason } from './support/stack';

/**
 * The map authoring lifecycle, against a real server and real accounts.
 *
 * The unit and component suites carry the detail — canvas rules, edge rules, node rules, the
 * inspector, the lifecycle verbs and the review gate are covered there and in `Veldrath.Server.Tests`.
 * What this spec adds is proof that the real pages, the real session and the real API agree on one
 * journey: a community author drafts a map, the submit gate lets a warning through, the review queue
 * says approval would refuse it, the approval is refused, and a rejection reaches the author's inbox.
 *
 * It needs writing (`content.manage`) and deciding (`content.review`) rights, which are granted by
 * writing the Identity join table — see `tests/support/stack.ts`. Without a stack it is skipped.
 */
test.describe('foundry map lifecycle', () => {
	test.skip(!stackEnabled, stackSkipReason);
	// A whole lifecycle across two accounts; the default 30s budget is a unit-test budget.
	test.describe.configure({ timeout: 180_000 });

	test('a draft without an exit is refused at approval and the rejection reaches the author', async ({
		browser
	}) => {
		const stamp = `${Date.now().toString(36)}`;
		const zoneId = `e2e-zone-${stamp}`;
		const mapName = `E2E Zone ${stamp}`;
		const notes = 'Add a landmark before the exit.';

		const authorContext = await browser.newContext();
		const author = await authorContext.newPage();
		await signUpWithRoles(author, ['ContentEditor'], 'e2emapauthor');

		// ── The wizard is the one create door: kind, owner, name ─────────────────────────────────
		await author.goto('/maps/new');
		await expect(author.getByRole('heading', { level: 1, name: 'New map' })).toBeVisible();
		await author.locator('select[name="mapType"]').selectOption('Zone');
		await author.locator('input[name="ownerId"]').fill(zoneId);
		await author.locator('input[name="name"]').fill(mapName);
		await author.getByRole('button', { name: 'Create draft' }).click();

		// Landing in the editor is the whole point of the wizard: the canvas, the inspector and the
		// metadata form all read the one draft it created.
		await expect(author.getByRole('region', { name: 'Map details' })).toBeVisible({
			timeout: 20_000
		});
		await expect(author.getByTestId('map-canvas')).toBeVisible();
		// These two panels are labelled divs rather than landmarks, so they are found by label.
		await expect(author.getByLabel('Draft checklist')).toBeVisible();
		// A zone map pairs itself with the locations of its own zone; a fresh zone has none yet.
		await expect(author.getByLabel('Locations in this zone')).toBeVisible();

		// Validating runs the world rules without writing.
		await author.getByRole('button', { name: 'Validate', exact: true }).click();
		await expect(
			author.getByText(/No errors — a save would accept this graph|This graph would be refused/)
		).toBeVisible({ timeout: 20_000 });

		// ── Submitting lets a warning through: a half-made map is how a zone gets built ──────────
		await author.getByRole('button', { name: 'Submit for review' }).click();
		// The state appears in the lifecycle bar; the same words also label the map in its details.
		await expect(author.getByText('In review', { exact: true }).first()).toBeVisible({
			timeout: 20_000
		});
		await expect(author.getByRole('button', { name: 'Submit for review' })).toHaveCount(0);

		// ── The reviewer sees the verdict before opening the graph ───────────────────────────────
		const reviewerContext = await browser.newContext();
		const reviewer = await reviewerContext.newPage();
		await signUpWithRoles(reviewer, ['Curator'], 'e2emapreviewer');

		await reviewer.goto('/review');
		await expect(reviewer.getByRole('heading', { level: 1, name: 'Review' })).toBeVisible();
		const row = reviewer.getByRole('listitem').filter({ hasText: mapName });
		await expect(row).toBeVisible({ timeout: 20_000 });
		// The queue ran the validator: a blocked verdict with no errors is the missing exit.
		await expect(row.getByText('Approval would be refused · the map has no exit')).toBeVisible();

		// ── Approving is refused, and the refusal says why ──────────────────────────────────────
		await row.getByRole('link', { name: mapName }).click();
		await expect(reviewer.getByRole('region', { name: 'Map details' })).toBeVisible({
			timeout: 20_000
		});
		await reviewer.getByRole('button', { name: 'Approve', exact: true }).click();
		await expect(reviewer.getByText(/only leave it by dying/i)).toBeVisible({ timeout: 20_000 });
		// Still in review: a refusal must not move the map's state.
		await expect(reviewer.getByText('In review', { exact: true }).first()).toBeVisible();

		// ── Rejecting is accepted, and the note travels to the author's inbox ───────────────────
		await reviewer.locator('input[name="notes"]').fill(notes);
		await reviewer.getByRole('button', { name: 'Reject', exact: true }).click();
		await expect(reviewer.getByText('Draft', { exact: true }).first()).toBeVisible({
			timeout: 20_000
		});

		await author.goto('/notifications');
		const notification = author.getByRole('listitem').filter({ hasText: mapName });
		await expect(notification).toBeVisible({ timeout: 20_000 });
		await expect(notification.getByText(notes)).toBeVisible();
		// The row links to the map itself, not to a submission: a map decision is where the fix is made.
		await expect(notification.getByRole('link', { name: mapName })).toHaveAttribute(
			'href',
			`/maps/${encodeURIComponent(`zone:${zoneId}`)}`
		);

		await authorContext.close();
		await reviewerContext.close();
	});
});
