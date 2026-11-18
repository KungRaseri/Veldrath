import { randomUUID } from 'node:crypto';
import { expect, test } from '@playwright/test';
import { spendAttributePoints } from './support/creation';
import { expectNoHorizontalOverflow } from './support/layout';
import { makeAccount, register, stackEnabled, stackSkipReason } from './support/stack';

/**
 * Nothing may paint outside the box that is meant to hold it.
 *
 * This exists because the creation wizard's species and background steps grew past their card: the
 * options are buttons carrying a whole sentence, Skeleton's `.btn` refuses to wrap, and a grid item
 * that cannot shrink is as wide as the sentence — so the row widened the document and every page
 * gained a horizontal scrollbar. A unit test cannot see that (jsdom has no layout), and a screenshot
 * is nobody's regression gate, so the assertion is geometric: measure the document and every known
 * container at three widths and fail on the pixels.
 *
 * The three viewports are the ones the wizard is actually used at — a desktop window, a tablet held
 * sideways, and a phone — because the two-column class grid is the narrowest place a label is squeezed.
 */
const VIEWPORTS = [
	{ label: 'desktop', width: 1280, height: 800 },
	{ label: 'tablet', width: 768, height: 1024 },
	{ label: 'phone', width: 390, height: 844 }
] as const;

/** A name the wizard accepts: letters and spaces, 2–20 characters, unique per run. */
const heroName = () =>
	`Hero${randomUUID()
		.replace(/[^a-z]/g, '')
		.slice(0, 6)}`.slice(0, 20);

test.describe('layout', () => {
	test.skip(!stackEnabled, stackSkipReason);
	test.describe.configure({ timeout: 240_000 });

	for (const viewport of VIEWPORTS) {
		test(`the creation wizard keeps its content inside the card at ${viewport.label}`, async ({
			page
		}) => {
			await page.setViewportSize({ width: viewport.width, height: viewport.height });
			await register(page, makeAccount('e2elayout'));
			await page.goto('/characters/new');
			await expect(page.getByText(/Step 1 of 7/)).toBeVisible({ timeout: 20_000 });

			// Step 1 — the name field and its availability line.
			await expectNoHorizontalOverflow(page);
			await page.getByLabel('Character name').fill(heroName());
			await expect(page.getByText('That name is available.')).toBeVisible({ timeout: 20_000 });

			// Steps 2–4 — the choice rows, the ones that used to bleed past the card.
			for (const label of ['Class', 'Species', 'Background']) {
				await page.getByRole('button', { name: 'Continue' }).click();
				await expect(page.getByText(new RegExp(`· ${label}$`))).toBeVisible({ timeout: 20_000 });
				await expectNoHorizontalOverflow(page);
			}

			// Step 5 — the stat rows, before and after the budget is spent (the hint goes, the rows stay).
			await page.getByRole('button', { name: 'Continue' }).click();
			await expect(page.getByText(/Step 5 of 7/)).toBeVisible({ timeout: 20_000 });
			await expectNoHorizontalOverflow(page);
			await spendAttributePoints(page);
			await expectNoHorizontalOverflow(page);

			// Steps 6–7 — selects, checkboxes, the preview definition list and the difficulty fieldset.
			for (const label of ['Equipment', 'Review']) {
				await page.getByRole('button', { name: 'Continue' }).click();
				await expect(page.getByText(new RegExp(`· ${label}$`))).toBeVisible({ timeout: 20_000 });
				await expectNoHorizontalOverflow(page);
			}
			await expect(page.getByRole('group', { name: 'Difficulty' })).toBeVisible();
			await expectNoHorizontalOverflow(page);
		});
	}

	test('the public pages and the roster keep their content inside their cards', async ({
		page
	}) => {
		// The phone is the width where a public page breaks first, and it is also where the site
		// header wraps; the roster is included because it renders a grid of cards from real data.
		await page.setViewportSize({ width: 390, height: 844 });

		for (const route of ['/', '/lore', '/patch-notes', '/community', '/register']) {
			await page.goto(route);
			await expect(page.getByRole('heading', { level: 1 })).toBeVisible({ timeout: 20_000 });
			await expectNoHorizontalOverflow(page);
		}

		await register(page, makeAccount('e2elayoutpublic'));

		// The roster and the account page are the two signed-in surfaces that render variable content —
		// character cards, linked-provider rows, active sessions — so they are the other place a wide row
		// can appear without anyone typing a sentence into a component.
		for (const route of ['/characters', '/account']) {
			await page.goto(route);
			await expect(page.getByRole('heading', { level: 1 })).toBeVisible({ timeout: 20_000 });
			await expectNoHorizontalOverflow(page);
		}
	});
});
