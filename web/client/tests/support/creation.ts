import { expect, type Page } from '@playwright/test';

/** The six attributes the point-buy budget is spread across, in the order the wizard lists them. */
export const STAT_NAMES = [
	'Strength',
	'Dexterity',
	'Constitution',
	'Intelligence',
	'Wisdom',
	'Charisma'
] as const;

/**
 * Spends the whole attribute budget on the wizard's Attributes step (step 5 of 7).
 *
 * The step refuses to advance while a point is unspent, and the budget and cost table belong to the
 * server (`POST /api/characters/creation` returns them), so nothing here may assume 27 points or a
 * particular cost curve. It clicks `Increase` on each stat until that button disables itself — which is
 * exactly the greedy path a player takes — and then checks the hint is gone, which is the page saying
 * the budget is spent.
 *
 * @param page The wizard, already on the Attributes step.
 */
export async function spendAttributePoints(page: Page): Promise<void> {
	await expect(page.getByText(/Step 5 of 7/)).toBeVisible({ timeout: 20_000 });

	for (const stat of STAT_NAMES) {
		const increase = page.getByRole('button', { name: `Increase ${stat}` });
		// The cap is 15 in every shipped configuration, so 20 is a bound rather than a guess.
		for (let clicks = 0; clicks < 20 && (await increase.isEnabled()); clicks += 1) {
			await increase.click();
		}
	}

	await expect(page.getByText(/Spend every point to continue/)).toHaveCount(0);
}
