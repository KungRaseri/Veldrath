import { randomUUID } from 'node:crypto';
import { expect, test } from '@playwright/test';
import { spendAttributePoints } from './support/creation';
import { makeAccount, register, stackEnabled, stackSkipReason } from './support/stack';

/**
 * The game as a player builds and uses it, against the real server and a live hub.
 *
 * The existing `game-loop` spec gets into the world by seeding a character through the API. This one takes
 * the road a person takes: the seven-step creation wizard, the roster, then the world — and then checks that
 * the HUD it lands in is actually usable (a bag with rows, and an action on every row) rather than merely
 * rendered.
 *
 * Combat and death are not asserted yet: they depend on which enemy spawns at a seeded node, and a spec that
 * silently skips when the fight does not appear would be worse than one that says so. They are the next slice
 * in `plans/web-e2e-workflows.md`.
 */
test.describe('game journeys', () => {
	test.skip(!stackEnabled, stackSkipReason);
	test.describe.configure({ timeout: 240_000 });

	test('a player builds a character in the wizard, enters the world, and the sheet is usable', async ({
		page
	}) => {
		await register(page, makeAccount('e2ehero'));

		// The wizard accepts letters and spaces only ("2–20 characters"), so a stamp of digits would be
		// rejected before the availability check even runs.
		const name = `Hero${randomUUID()
			.replace(/[^a-z]/g, '')
			.slice(0, 6)}`.slice(0, 20);

		// ── The wizard: seven steps, each advancing through the real creation session ───────────
		await page.goto('/characters/new');
		await expect(page.getByText(/Step 1 of 7/)).toBeVisible();

		await page.getByLabel('Character name').fill(name);
		// The name is checked against the real roster before the step will advance.
		await expect(page.getByText('That name is available.')).toBeVisible({ timeout: 20_000 });
		// The advancing button reads "Continue" until the last step, where it becomes "Create character".
		await page.getByRole('button', { name: 'Continue' }).click();

		// Class, species, background: pick the first option offered, then advance.
		for (const step of ['2 of 7', '3 of 7', '4 of 7']) {
			await expect(page.getByText(new RegExp(`Step ${step}`))).toBeVisible({ timeout: 20_000 });
			await page.locator('button[aria-pressed]').first().click();
			await page.getByRole('button', { name: 'Continue' }).click();
		}

		// Attributes must be spent in full — the step and the engine both refuse a partial budget now —
		// then equipment keeps the class kit, and the last hop is the review.
		await spendAttributePoints(page);
		await page.getByRole('button', { name: 'Continue' }).click();
		await expect(page.getByText(/Step 6 of 7/)).toBeVisible({ timeout: 20_000 });
		await page.getByRole('button', { name: 'Continue' }).click();
		await expect(page.getByText(/Step 7 of 7/)).toBeVisible({ timeout: 20_000 });

		// Creating drops the player straight into the world, so the assertion is the world, not the roster.
		await page.getByRole('button', { name: 'Create character' }).click();
		await expect(page).toHaveURL(/\/play\/[0-9a-f-]{36}$/, { timeout: 30_000 });
		await expect(page.locator('.map-node-cell button').first()).toBeVisible({ timeout: 30_000 });

		// ── The sheet is usable, not just visible ──────────────────────────────────────────────
		const sheet = page.getByRole('complementary', { name: 'Character sheet' });
		await expect(sheet.getByRole('heading', { name: /Inventory/ })).toBeVisible();
		await expect(sheet.getByRole('heading', { name: /Equipment/ })).toBeVisible();

		// A starting kit fills the bag, and every row offers at least one action — that is the invariant
		// that catches a dead bag (an item with no verbs) without pinning which items the class starts with.
		const bagRows = sheet.locator('.bag-row');
		await expect(bagRows.first()).toBeVisible({ timeout: 20_000 });
		const rowCount = await bagRows.count();
		expect(rowCount).toBeGreaterThan(0);
		for (let index = 0; index < rowCount; index += 1) {
			const actions = bagRows.nth(index).locator('button');
			expect(
				await actions.count(),
				`bag row ${index} offers no action (equip, use, drop or sell)`
			).toBeGreaterThan(0);
		}

		// ── And the map is walkable: a neighbouring node offers a travel action ────────────────
		const nodes = page.locator('.map-node-cell button');
		const current = page.locator('button[aria-current="location"]');
		await expect(current).toHaveCount(1);
		const currentNodeLabel = (await current.innerText()).trim();

		let travelled = false;
		const nodeCount = await nodes.count();
		for (let index = 0; index < nodeCount; index += 1) {
			const node = nodes.nth(index);
			if ((await node.innerText()).trim() === currentNodeLabel) continue;

			await node.click();
			const travel = page.getByRole('button', { name: /^Travel to / });
			if (await travel.isVisible().catch(() => false)) {
				await travel.click();
				travelled = true;
				break;
			}
		}
		expect(travelled, 'no neighbouring node was reachable from the starting node').toBe(true);

		// Combat is deliberately not asserted yet: the panel mounts only once a fight is active, and starting
		// one needs a known enemy at a known node — E13c in the plan, not something to guess at here.

		// A new character is on the roster, which is where a returning player starts.
		await page.goto('/characters');
		await expect(page.getByRole('heading', { name })).toBeVisible({ timeout: 20_000 });
	});
});
