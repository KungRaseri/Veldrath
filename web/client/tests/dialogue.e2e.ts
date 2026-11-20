import { randomUUID } from 'node:crypto';
import { expect, test } from '@playwright/test';
import { spendAttributePoints } from './support/creation';
import { makeAccount, register, stackEnabled, stackSkipReason } from './support/stack';

/**
 * A conversation, from the road a player takes: the wizard, the square, the person standing on it.
 *
 * What this proves is the whole transport — the People entry, the hub round trip, the panel, and the greeting still
 * reaching the log (which is what advances `talk:` objectives). The seeded townspeople carry lines only, so what is
 * asserted about the panel is that it says exactly that rather than showing an empty list; the choose path is
 * covered by the handler tests, and this spec grows a choice assertion as soon as the content team authors the
 * elder's options in Foundry.
 */

/** A name the wizard accepts: letters and spaces, 2–20 characters, unique per run. */
const heroName = () =>
	`Talk${randomUUID()
		.replace(/[^a-z]/g, '')
		.slice(0, 6)}`.slice(0, 20);

test.describe('conversations', () => {
	test.skip(!stackEnabled, stackSkipReason);
	test.describe.configure({ timeout: 240_000 });

	test('talking to the elder opens their panel and still greets in the log', async ({ page }) => {
		await register(page, makeAccount('e2etalk'));

		// ── The wizard, because it is what sets the starting location ─────────────────────────────
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

		// ── The square, and the elder the seed places on it ───────────────────────────────────────
		await expect(page.getByText('Wayfarer Elder')).toBeVisible({ timeout: 30_000 });
		await page.getByRole('button', { name: 'Talk', exact: true }).first().click();

		const panel = page.getByRole('dialog');
		await expect(panel).toBeVisible({ timeout: 20_000 });

		// The panel is titled with the speaker, so "who am I talking to" is answered without reading the prose.
		await expect(panel.getByRole('heading')).toContainText('Wayfarer Elder', { timeout: 20_000 });

		// No conversation has been authored for the seed yet. The honest answer is "nothing more to say", not an
		// empty list that reads like a failed load — and it is what an unauthored speaker shows a real player.
		await expect(panel.getByText('There is nothing more to say just now.')).toBeVisible({
			timeout: 20_000
		});

		// Closing leaves the conversation without choosing anything.
		await panel.getByRole('button', { name: 'Close conversation' }).click();
		await expect(page.getByRole('dialog')).toBeHidden({ timeout: 10_000 });

		// The greeting still reached the log: that is the `talk:` objective path, and it must survive the panel.
		await expect(page.getByText(/Wayfarer Elder: “/)).toBeVisible({ timeout: 20_000 });
	});
});
