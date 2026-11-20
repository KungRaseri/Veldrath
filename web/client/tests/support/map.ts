import { expect, type Page } from '@playwright/test';

/**
 * Walks to a node on the current map through the UI: select it, then take the travel action.
 *
 * Shared because two specs now need it for the same reason: **a character does not arrive at a location, it arrives
 * at the zone's entry node**, and every zone map chains `entry → first location → second …`. A spec that expects to
 * meet somebody the moment it lands is wrong about the world, not about the client — `harvest.e2e.ts` learned that
 * for the Ashlen Wood and `dialogue.e2e.ts` for the square.
 *
 * The panel only offers a travel action for a node that is adjacent, and none at all for the node the character is
 * already standing on — so "already there" is a no-op. A node that is visible but not adjacent answers `false` so the
 * caller can skip with a reason instead of waiting out the test timeout.
 *
 * @param page The game page.
 * @param nodeName The node label as the map draws it.
 * @returns Whether the character ends up on that node.
 */
export async function walkTo(page: Page, nodeName: string): Promise<boolean> {
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
