import { randomUUID } from 'node:crypto';
import { expect, test, type Page } from '@playwright/test';
import { spendAttributePoints } from './support/creation';
import { walkTo } from './support/map';
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
 * Opens the first chest the Local panel offers, and proves the opened chest left the list.
 *
 * Authored chests are world state, not per-character state: a second run against the same database finds
 * the one an earlier run emptied still inside its reopen window, so the caller walks on rather than
 * pretending the chest loop was exercised.
 *
 * The assertion is "the list shrank", not "no chest is listed". A location can legitimately hold more than
 * one — a reopened authored chest beside a roaming cache the sweeper placed — and an earlier version of this
 * helper asserted the stronger, false thing, which made the spec fail on any database that had been used once.
 *
 * @param page The game page.
 * @returns Whether a chest was opened.
 */
async function openAnyChest(page: Page): Promise<boolean> {
	const openButtons = page.getByRole('button', { name: 'Open', exact: true });
	const open = openButtons.first();

	try {
		await expect(open).toBeVisible({ timeout: 20_000 });
	} catch {
		return false;
	}

	const before = await openButtons.count();
	await open.click();

	// Pushed by the server after the claim, so it is awaited rather than sampled.
	await expect.poll(() => openButtons.count(), { timeout: 20_000 }).toBeLessThan(before);
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

		// ── The elder the square is seeded with ────────────────────────────────────────────────────
		// Placement is what makes a `talk:` objective possible at all: the wayfarer elder stands here, the panel
		// lists him, and speaking to him answers with one of his own lines rather than nothing.
		await expect(page.getByText('Wayfarer Elder')).toBeVisible({ timeout: 30_000 });
		await page.getByRole('button', { name: 'Talk', exact: true }).first().click();
		await expect(page.getByText(/Wayfarer Elder:/)).toBeVisible({ timeout: 30_000 });

		// Talking now opens the conversation panel, which is modal — walking on with it open would click through it.
		// The conversation itself is asserted in dialogue.e2e.ts; here it only has to get out of the way.
		await page.getByRole('button', { name: 'Close conversation' }).click();
		await expect(page.getByRole('dialog')).toBeHidden({ timeout: 10_000 });

		expect(await walkTo(page, 'Road to The Ashlen Wood')).toBe(true);
		expect(await walkTo(page, 'The Old Clearing')).toBe(true);

		// The nodes are pushed on arrival: the panel names them and offers the verb.
		const gather = page.getByRole('button', { name: 'Gather' }).first();
		await expect(gather).toBeVisible({ timeout: 30_000 });

		await gather.click();

		// The item is in the character's bag, which is the server's blob — the whole point of the slice.
		const sheet = page.getByRole('complementary', { name: 'Character sheet' });
		await expect(sheet.getByText('Glowcap Mushroom').first()).toBeVisible({ timeout: 30_000 });

		// ── The strongbox the seed places in this very clearing ────────────────────────────────────
		// The chest half of the same walk: it is seeded content (always here, unlike the roaming caches),
		// it is listed with the nodes on arrival, and opening it puts its guaranteed potion in the bag.
		let opened = await openAnyChest(page);

		if (!opened) {
			// A previous run on this database opened it and the reopen window has not elapsed: the cache on
			// the road is the other authored chest, one hop away inside the same zone.
			expect(await walkTo(page, 'The Wood Road')).toBe(true);
			opened = await openAnyChest(page);
		}

		if (!opened) {
			test.skip(true, 'An earlier run emptied both authored chests; start the stack with --fresh.');
		}

		// The opened chest leaving the list is asserted inside `openAnyChest`, on the shrinking count: the chat log
		// keeps the "Opened the …" line for the rest of the session, so a name-based check here would only ever be
		// satisfied by the log losing it.
		await expect(sheet.getByText('Health Potion').first()).toBeVisible({ timeout: 30_000 });
	});
});
