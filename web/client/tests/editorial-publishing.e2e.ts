import { expect, test } from '@playwright/test';
import { signUpWithRoles, stackEnabled, stackSkipReason } from './support/stack';

/**
 * Publishing, against a real server: an announcement and a patch note are written in Editorial, published,
 * and then read on the public site — which is the only thing that proves the two halves agree.
 *
 * The create form lives inside `[id]` with `id === 'new'` (the same deferral as `/submissions/new`), so
 * `/editorial/announcements/new` is a real page even though no `new/` directory exists.
 */
test.describe('editorial publishing journey', () => {
	test.skip(!stackEnabled, stackSkipReason);
	test.describe.configure({ timeout: 180_000 });

	test('an editor writes and publishes an announcement and a patch note, and the public site serves them', async ({
		page
	}) => {
		const stamp = `${Date.now().toString(36)}`;
		const announcementTitle = `E2E Notice ${stamp}`;
		const patchTitle = `E2E Update ${stamp}`;
		const patchSlug = `e2e-update-${stamp}`;

		await signUpWithRoles(page, ['Admin'], 'e2eeditor');

		// ── An announcement: create → publish → visible on the community page ───────────────────
		await page.goto('/editorial/announcements/new');
		await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
		await page.locator('input[name="title"]').fill(announcementTitle);
		await page.locator('textarea[name="body"]').fill('Server maintenance on the eighth.');
		await page.getByRole('button', { name: 'Create announcement' }).click();

		// Creating redirects into the edit view of the new row.
		await expect(page.getByRole('button', { name: 'Publish' })).toBeVisible({ timeout: 20_000 });
		await page.getByRole('button', { name: 'Publish' }).click();
		// The publish action flips the same control, so the state is readable from the page.
		await expect(page.getByRole('button', { name: 'Unpublish' })).toBeVisible({ timeout: 20_000 });

		await page.goto('/community');
		await expect(page.getByText(announcementTitle)).toBeVisible({ timeout: 20_000 });

		// ── A patch note: create → publish → readable at its public URL ─────────────────────────
		await page.goto('/editorial/patch-notes/new');
		await page.locator('input[name="slug"]').fill(patchSlug);
		await page.locator('input[name="version"]').fill(`0.0.${stamp.slice(-3)}`);
		await page.locator('input[name="title"]').fill(patchTitle);
		await page.locator('textarea[name="summary"]').fill('A small update for the archive.');
		await page.locator('textarea[name="content"]').fill('## Changes\n\n- Nothing broke.');
		await page.getByRole('button', { name: 'Create patch note' }).click();

		await expect(page.getByRole('button', { name: 'Publish' })).toBeVisible({ timeout: 20_000 });
		await page.getByRole('button', { name: 'Publish' }).click();
		await expect(page.getByRole('button', { name: 'Unpublish' })).toBeVisible({ timeout: 20_000 });

		// The public article is what a player sees; this is the assertion that publishing crossed over.
		await page.goto(`/patch-notes/${patchSlug}`);
		await expect(page.getByRole('heading', { level: 1, name: patchTitle })).toBeVisible({
			timeout: 20_000
		});

		const index = await page.goto('/patch-notes');
		expect(index?.status()).toBe(200);
		await expect(page.getByRole('link', { name: new RegExp(patchTitle) })).toBeVisible({
			timeout: 20_000
		});

		// ── A lore article: create → publish → readable at its public URL ───────────────────────
		const loreTitle = `E2E Chronicle ${stamp}`;
		const loreSlug = `e2e-chronicle-${stamp}`;
		await page.goto('/editorial/lore/new');
		await page.locator('input[name="slug"]').fill(loreSlug);
		await page.locator('input[name="category"]').fill('History');
		await page.locator('input[name="title"]').fill(loreTitle);
		await page.locator('textarea[name="summary"]').fill('A footnote for the archive.');
		await page.locator('textarea[name="content"]').fill('## The quiet years\n\nNothing happened.');
		await page.getByRole('button', { name: 'Create article' }).click();

		await expect(page.getByRole('button', { name: 'Publish' })).toBeVisible({ timeout: 20_000 });
		await page.getByRole('button', { name: 'Publish' }).click();
		await expect(page.getByRole('button', { name: 'Unpublish' })).toBeVisible({ timeout: 20_000 });

		await page.goto(`/lore/${loreSlug}`);
		await expect(page.getByRole('heading', { level: 1, name: loreTitle })).toBeVisible({
			timeout: 20_000
		});

		// ── A failure path: a required field left empty is refused on the page ──────────────────
		await page.goto('/editorial/announcements/new');
		await page.locator('input[name="title"]').fill('Missing body');
		// `required` blocks the submit in the browser; drop the attribute so the server sees the request.
		await page
			.locator('textarea[name="body"]')
			.evaluate((element) => element.removeAttribute('required'));
		await page.getByRole('button', { name: 'Create announcement' }).click();
		await expect(page.getByText(/title and body are required/i)).toBeVisible({ timeout: 20_000 });
	});
});
