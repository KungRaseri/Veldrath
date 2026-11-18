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
	await expect(node).toBeVisible({ timeout: 30_000 });
	await node.click();

	const travel = page.getByRole('button', { name: /^Travel to / });
	if (await travel.isEnabled().catch(() => false)) {
		await travel.click();
		return true;
	}

	// No travel action and the current-location chip showing means the character is already here.
	return page
		.getByText('Current location')
		.isVisible()
		.catch(() => false);
}

test.describe('gathering', () => {
	test.skip(!stackEnabled, stackSkipReason);
	// The forest is behind the zone-progress gate: a new character can walk Crestfall but cannot cross into the
	// Ashlen Wood yet, so the nodes are unreachable from a fresh run. The spec is written and runnable
	// (`npm run test:e2e:harvest`); it stays out of the parallel suite until zone unlocking lands.
	test.skip(
		process.env.E2E_HARVEST !== '1',
		'the Ashlen Wood is not reachable from a new character yet (zone progression) — run with E2E_HARVEST=1'
	);
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

		// ── Crestfall → the Ashlen Wood → the Wood Road ────────────────────────────────────────────
		// The character may start on the zone's entry node rather than in the square itself.
		await walkTo(page, 'The Crestfall Square');

		// A new character cannot always cross into the wood: zone progression gates the exit. Skipping says so
		// rather than pretending the forest was walked to — and once unlocking lands, this stops skipping.
		test.skip(
			!(await walkTo(page, 'Road to The Ashlen Wood')),
			'the Ashlen Wood is not reachable from a new character yet (zone progression)'
		);
		await expect(page.locator('.map-node-cell button').first()).toBeVisible({ timeout: 30_000 });
		test.skip(
			!(await walkTo(page, 'The Wood Road')),
			'the Wood Road is not reachable inside the Ashlen Wood'
		);

		// The nodes are pushed on arrival: the panel names them and offers the verb.
		const gather = page.getByRole('button', { name: 'Gather' }).first();
		await expect(gather).toBeVisible({ timeout: 30_000 });
		const healthBefore = await page
			.getByText(/· \d+% left/)
			.first()
			.innerText();

		await gather.click();

		// The item is in the character's bag, which is the server's blob — the whole point of the slice.
		const sheet = page.getByRole('complementary', { name: 'Character sheet' });
		await expect(sheet.getByText('Glowcap Mushroom').first()).toBeVisible({ timeout: 30_000 });

		// And the node remembers being picked: its health moved, which is what the next gather draws.
		await expect(page.getByText(/· \d+% left/).first()).not.toHaveText(healthBefore, {
			timeout: 20_000
		});
	});
});
