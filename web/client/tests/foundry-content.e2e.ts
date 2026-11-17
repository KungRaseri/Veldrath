import { expect, test } from '@playwright/test';
import {
	makeAccount,
	register,
	signUpWithRoles,
	stackEnabled,
	stackSkipReason
} from './support/stack';

/**
 * The catalog journey, against a real server: what an editor sees, and what a plain player sees of the
 * same catalog.
 *
 * This is the part the unit suites cannot prove. The browse, detail and create pages each call
 * `gameApiFetch` from their `+page.server.ts`, so the API call happens outside the browser and no
 * `page.route()` mock can stand in for it — see `tests/support/stack.ts`.
 *
 * The write half of this journey is deliberately not asserted yet: every content write currently fails
 * server-side with "The request body is not a valid content payload" (`ContentEntityTypes.Deserialize`
 * returns null for the nested payload the schema-driven form builds). The capability checks, the form
 * rendering and the read-only split are still worth pinning, and `plans/web-e2e-workflows.md` records the
 * write defect with the evidence and the follow-up.
 */
test.describe('foundry content journey', () => {
	test.skip(!stackEnabled, stackSkipReason);
	// A workflow drives several server-rendered pages, each of which calls the API; the default 30s is
	// a unit-test budget, not a journey's.
	test.describe.configure({ timeout: 120_000 });

	test('an editor sees the authoring surface, and a player reads the same catalog without it', async ({
		browser
	}) => {
		// ── The editor: portal, catalog, the one create door, the schema-driven form ──────────────
		const editorContext = await browser.newContext();
		const editor = await editorContext.newPage();

		await signUpWithRoles(editor, ['ContentEditor'], 'e2econtent');

		// The portal home renders for any signed-in account; the role is what adds the capability groups.
		await editor.goto('/foundry');
		await expect(editor.getByRole('heading', { level: 1 })).toBeVisible();
		await expect(editor.getByRole('navigation', { name: 'Foundry sections' })).toBeVisible();
		await expect(editor.getByRole('link', { name: 'Review queue' })).toHaveCount(0);

		// /content lists every registered type, with a browse link into each.
		await editor.goto('/content');
		await expect(editor.getByRole('heading', { name: 'Base content' })).toBeVisible();
		await editor
			.getByRole('article')
			.filter({ hasText: 'Abilities' })
			.getByRole('link', { name: 'Browse' })
			.click();
		await expect(editor.getByRole('link', { name: 'New entry' })).toBeVisible();

		// One entry, rendered from the API payload, with the edit link a writer gets and a reader does not.
		await editor.goto('/content/ability/bite');
		await expect(editor.getByRole('heading', { level: 1 })).toBeVisible();
		await editor.getByRole('link', { name: 'Edit this entity' }).click();
		// The edit form is preloaded from the payload, and the identity fields the validator requires
		// (`typeKey`, and `powerType` for a power) are the ones it asks for.
		await expect(editor.locator('#displayName')).toHaveValue('Bite');
		await expect(editor.locator('#typeKey')).toHaveValue('active/offensive');
		await expect(editor.getByRole('button', { name: 'Save', exact: true })).toBeVisible();

		// The picker is the one create door, and the create form is driven by the schema.
		await editor.goto('/new');
		await expect(editor.getByRole('heading', { level: 1, name: 'New content' })).toBeVisible();
		await editor.goto('/content/ability/new');
		await expect(editor.locator('#displayName')).toBeVisible();
		await expect(editor.locator('#slug')).toBeVisible();
		await expect(editor.locator('#typeKey')).toBeVisible();

		// Schemas is the read-only description of the same registry the forms are built from.
		await editor.goto('/schemas');
		await expect(editor.getByRole('heading', { level: 1, name: 'Schemas' })).toBeVisible();

		// World health runs the audit; it needs `content.view`, which this editor holds.
		const audit = await editor.goto('/world-health');
		expect(audit?.status()).toBe(200);
		await expect(editor.getByRole('heading', { level: 1, name: 'World health' })).toBeVisible();

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

		await reader.goto('/content/ability/bite');
		await expect(reader.getByRole('link', { name: 'Edit this entity' })).toHaveCount(0);

		// A reader may open the create form — it explains the permission instead of 403ing.
		await reader.goto('/content/ability/new');
		await expect(reader.getByText(/requires the content-editor permission/)).toBeVisible();

		// The audit is the one catalog surface behind `content.view`, so a reader is refused outright.
		const refused = await reader.goto('/world-health');
		expect(refused?.status()).toBe(403);

		await editorContext.close();
		await readerContext.close();
	});
});
