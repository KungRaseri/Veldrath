import { expect, test } from '@playwright/test';
import {
	makeAccount,
	register,
	signUpWithRoles,
	stackEnabled,
	stackSkipReason
} from './support/stack';

/**
 * The catalog journey, against a real server: an editor creates an entry, edits it, saves it, retires it,
 * and a plain player reads the same catalog with no write surface at all.
 *
 * This is the part the unit suites cannot prove. The browse, detail, create and edit pages each call
 * `gameApiFetch` from their `+page.server.ts`, so the API call happens outside the browser and no
 * `page.route()` mock can stand in for it — see `tests/support/stack.ts`.
 */
test.describe('foundry content journey', () => {
	test.skip(!stackEnabled, stackSkipReason);
	// A workflow drives several server-rendered pages, each of which calls the API; the default 30s is
	// a unit-test budget, not a journey's.
	test.describe.configure({ timeout: 180_000 });

	test('an editor creates, edits, saves, refuses a duplicate and retires an entry; a player only reads', async ({
		browser
	}) => {
		const stamp = `${Date.now().toString(36)}`;
		const newName = `E2E Ward ${stamp}`;
		const renamed = `${newName} II`;
		const newSlug = `e2e-ward-${stamp}`;

		// ── The editor: portal, catalog, the one create door, the schema-driven form ──────────────
		const editorContext = await browser.newContext();
		const editor = await editorContext.newPage();

		await signUpWithRoles(editor, ['ContentEditor'], 'e2econtent');

		// The portal home renders for any signed-in account; the role is what adds the capability groups.
		await editor.goto('/foundry');
		await expect(editor.getByRole('heading', { level: 1 })).toBeVisible();
		await expect(editor.getByRole('navigation', { name: 'Foundry sections' })).toBeVisible();

		// /content lists every registered type, with a browse link into each.
		await editor.goto('/content');
		await expect(editor.getByRole('heading', { name: 'Base content' })).toBeVisible();
		await editor
			.getByRole('article')
			.filter({ hasText: 'Abilities' })
			.getByRole('link', { name: 'Browse' })
			.click();
		await expect(editor.getByRole('link', { name: 'New entry' })).toBeVisible();

		// ── Create: the four required identity/discriminator fields, then the write lands ────────
		await editor.goto('/new');
		await expect(editor.getByRole('heading', { level: 1, name: 'New content' })).toBeVisible();
		await editor.goto('/content/ability/new');
		await expect(editor.locator('#displayName')).toBeVisible();

		await editor.locator('#displayName').fill(newName);
		await editor.locator('#slug').fill(newSlug);
		await editor.locator('#typeKey').fill('active/offensive');
		await editor.locator('#powerType').selectOption('active');
		await editor.getByRole('button', { name: 'Create draft' }).click();
		// A refused write reports on the same page; assert none so a failure names its reason here.
		await expect(editor.getByRole('alert')).toHaveCount(0);
		await editor.goto(`/content/ability/${newSlug}`);
		await expect(editor.getByRole('heading', { level: 1, name: newName })).toBeVisible({
			timeout: 20_000
		});

		// ── Edit and save: the form is preloaded from the payload and writes back ────────────────
		await editor.getByRole('link', { name: 'Edit this entity' }).click();
		await expect(editor.locator('#displayName')).toHaveValue(newName);
		await expect(editor.locator('#typeKey')).toHaveValue('active/offensive');
		await editor.locator('#displayName').fill(renamed);
		await expect(editor.getByRole('button', { name: 'Save', exact: true })).toBeEnabled();
		await editor.getByRole('button', { name: 'Save', exact: true }).click();
		await expect(editor.getByText('Saved.')).toBeVisible({ timeout: 20_000 });

		// The detail page reads the new value back from the API, so the save really landed.
		await editor.goto(`/content/ability/${newSlug}`);
		await expect(editor.getByRole('heading', { level: 1, name: renamed })).toBeVisible({
			timeout: 20_000
		});

		// ── A failure path a person will hit: reusing a slug is refused, and says so ─────────────
		await editor.goto('/content/ability/new');
		await editor.locator('#displayName').fill(`${newName} duplicate`);
		await editor.locator('#slug').fill(newSlug);
		await editor.locator('#typeKey').fill('active/offensive');
		await editor.locator('#powerType').selectOption('active');
		await editor.getByRole('button', { name: 'Create draft' }).click();
		await expect(editor.getByRole('alert')).toBeVisible({ timeout: 20_000 });

		// ── The player: the same catalog, no authoring surface ───────────────────────────────────
		const readerContext = await browser.newContext();
		const reader = await readerContext.newPage();
		await register(reader, makeAccount('e2ereader'));

		await reader.goto('/content');
		await expect(reader.getByRole('heading', { name: 'Base content' })).toBeVisible();

		await reader.goto('/content/ability');
		await expect(reader.getByRole('heading', { level: 1, name: 'ability' })).toBeVisible();
		// `canEdit` comes from the account's permissions, not from the page re-deriving a role.
		await expect(reader.getByRole('link', { name: 'New entry' })).toHaveCount(0);

		await reader.goto(`/content/ability/${newSlug}`);
		await expect(reader.getByRole('heading', { level: 1, name: renamed })).toBeVisible();
		await expect(reader.getByRole('link', { name: 'Edit this entity' })).toHaveCount(0);

		// A reader may open the create form — it explains the permission instead of 403ing.
		await reader.goto('/content/ability/new');
		await expect(reader.getByText(/requires the content-editor permission/)).toBeVisible();

		// The audit is the one catalog surface behind `content.view`, so a reader is refused outright.
		const refused = await reader.goto('/world-health');
		expect(refused?.status()).toBe(403);

		// ── Retire it: the editor deactivates instead of deleting, and the catalog stops serving it ──
		await editor.goto(`/content/ability/${newSlug}/edit`);
		await editor.getByRole('button', { name: 'Deactivate' }).click();
		await expect(editor).toHaveURL(/\/content\/ability$/, { timeout: 20_000 });

		// Retired means gone from the catalog: the public read filters inactive entries out, so the entry
		// that was readable a moment ago now 404s. That is the whole difference between retiring and
		// deleting, and it is worth asserting rather than trusting.
		const retired = await editor.goto(`/content/ability/${newSlug}`);
		expect(retired?.status()).toBe(404);

		// Schemas and world health are the two read-only catalog surfaces; both render for an editor.
		await editor.goto('/schemas');
		await expect(editor.getByRole('heading', { level: 1, name: 'Schemas' })).toBeVisible();
		const audit = await editor.goto('/world-health');
		expect(audit?.status()).toBe(200);

		await editorContext.close();
		await readerContext.close();
	});
});
