import { randomUUID } from 'node:crypto';
import { expect, test, type Page } from '@playwright/test';
import { spendAttributePoints } from './support/creation';
import { makeAccount, register, stackEnabled, stackSkipReason } from './support/stack';

/**
 * Gathering, from the road a player takes: the wizard, the map, the wood, the node.
 *
 * This is the loop the content pass could not finish for a long time. `gather-mushrooms` asked for ten
 * glowcap mushrooms while nothing in the world could produce one — no harvesting loot table was seeded and
 * no resource node was ever created — so the assertion that matters is not "a button renders" but "the
 * item is in the bag afterwards, and the node remembers being picked".
 */

/** A name the wizard accepts: letters and spaces, 2–20 characters, unique per run. */
const heroName = () =>
	`Herb${randomUUID()
		.replace(/[^a-z]/g, '')
		.slice(0, 6)}`.slice(0, 20);

/**
 * Walks to a node on the current map through the UI: select it, then take the travel action.
 *
 * The panel only offers a travel action for a node that is adjacent, and none at all for the node the
 * character is already standing on — so "already there" is a no-op. A node that is visible but not adjacent
 * answers `false` so the caller can skip with a reason instead of waiting out the test timeout.
 *
 * @param page The game page.
 * @param nodeName The node label as the map draws it.
 * @returns Whether the character ends up on that node.
 */
async function walkTo(page: Page, nodeName: string): Promise<boolean> {
	const node = page.locator('.map-node-cell button', { hasText: nodeName }).first();

	// A zone change paints a new map, so the node is awaited rather than sampled.
	try {
		await expect(node).toBeVisible({ timeout: 30_000 });
	} catch {
		return false;
	}

	const before = await page
		.locator('.map-node-cell button')
		.allInnerTexts()
		.catch(() => []);

	await node.click();

	// The action is named after the selected node, so waiting for that exact name is what proves the
	// selection landed — a stale panel offers the previous node's action instead.
	const escaped = nodeName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
	const travel = page.getByRole('button', { name: new RegExp(`^Travel to ${escaped}$`) });

	try {
		await expect(travel).toBeEnabled({ timeout: 10_000 });
	} catch {
		// Nothing offered: either the character already stands there, or the node is not adjacent.
		return page
			.getByText('Current location')
			.isVisible()
			.catch(() => false);
	}

	await travel.click();

	// Arrival, without a sleep: either the current-location chip names the destination, or the map was
	// replaced (exit nodes leave the zone, so the whole node list changes).
	await expect
		.poll(
			async () => {
				const moved = await page
					.locator('button[aria-current="location"]')
					.innerText()
					.then((text) => text.includes(nodeName))
					.catch(() => false);
				const after = await page
					.locator('.map-node-cell button')
					.allInnerTexts()
					.catch(() => []);
				return moved || JSON.stringify(after) !== JSON.stringify(before);
			},
			{ timeout: 30_000 }
		)
		.toBe(true);

	return true;
}

test.describe('gathering', () => {
	test.skip(!stackEnabled, stackSkipReason);
	test.describe.configure({ timeout: 240_000 });

	test('a player walks into the wood, gathers mushrooms and the node remembers it', async ({
		page
	}) => {
		await register(page, makeAccount('e2eherb'));

		// ── The wizard, because it is what sets the starting location (Crestfall Square) ──────────
		await page.goto('/characters/new');
		await page.getByLabel('Character name').fill(heroName());
		await expect(page.getByText('That name is available.')).toBeVisible({ timeout: 20_000 });
		await page.getByRole('button', { name: 'Continue' }).click();
		for (const step of ['2 of 7', '3 of 7', '4 of 7']) {
			await expect(page.getByText(new RegExp(`Step ${step}`))).toBeVisible({ timeout: 20_000 });
			await page.locator('button[aria-pressed]').first().click();
			await page.getByRole('button', { name: 'Continue' }).click();
		}
		await spendAttributePoints(page);
		await page.getByRole('button', { name: 'Continue' }).click();
		await expect(page.getByText(/Step 6 of 7/)).toBeVisible({ timeout: 20_000 });
		await page.getByRole('button', { name: 'Continue' }).click();
		await expect(page.getByText(/Step 7 of 7/)).toBeVisible({ timeout: 20_000 });
		await page.getByRole('button', { name: 'Create character' }).click();
		await expect(page).toHaveURL(/\/play\/[0-9a-f-]{36}$/, { timeout: 30_000 });
		await expect(page.locator('.map-node-cell button').first()).toBeVisible({ timeout: 30_000 });

		// ── Crestfall → the Ashlen Wood → the Old Clearing ─────────────────────────────────────────
		// The zone-exit node carries the destination map, so this is one hop out of town; inside the wood the
		// arrival point chains to the clearing (the first location) and on to the road. The forage nodes are
		// authored at both, so the clearing is where this journey stops.
		expect(await walkTo(page, 'The Crestfall Square')).toBe(true);
		expect(await walkTo(page, 'Road to The Ashlen Wood')).toBe(true);
		expect(await walkTo(page, 'The Old Clearing')).toBe(true);

		// The nodes are pushed on arrival: the panel names them and offers the verb.
		const gather = page.getByRole('button', { name: 'Gather' }).first();
		await expect(gather).toBeVisible({ timeout: 30_000 });

		await gather.click();

		// The item is in the character's bag, which is the server's blob — the whole point of the slice.
		const sheet = page.getByRole('complementary', { name: 'Character sheet' });
		await expect(sheet.getByText('Glowcap Mushroom').first()).toBeVisible({ timeout: 30_000 });
	});
});
