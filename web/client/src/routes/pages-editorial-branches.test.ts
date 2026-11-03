import { cleanup, render, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it } from 'vitest';
import AnnouncementEditor from './(editorial)/editorial/announcements/[id]/+page.svelte';
import LoreEditor from './(editorial)/editorial/lore/[id]/+page.svelte';
import PatchNoteEditor from './(editorial)/editorial/patch-notes/[id]/+page.svelte';

/**
 * Branch-coverage companion for the editorial record editors.
 *
 * The existing suite covers create mode and a loaded record. What is left is the third state the
 * editors are written for but never rendered: edit mode where the record itself is absent (a
 * deleted article opened from a stale tab, or a load that returned nothing). The title ternary,
 * the `?.` lookups, and every `{#if record}` guard then take their fallback side.
 *
 * The `<select value={...}>` branches are not coverable: Svelte compiles them to
 * `if (select_value !== (select_value = ...))`, and neither the unchanged side nor the inner
 * `?? ''` can run during a static render.
 */

function page(data: Record<string, unknown>, form: Record<string, unknown> | null = null) {
	return { data, form } as never;
}

afterEach(cleanup);

describe('patch note editor without a record', () => {
	it('falls back to the generic title and hides the destructive controls', () => {
		render(PatchNoteEditor, page({ mode: 'edit', note: null }));

		expect(screen.getByRole('heading', { level: 1, name: 'Edit patch note' })).toBeTruthy();
		expect((screen.getByLabelText(/Version/) as HTMLInputElement).value).toBe('');
		expect(screen.queryByRole('button', { name: /Publish|Unpublish/ })).toBeNull();
		expect(screen.queryByRole('button', { name: 'Delete patch note' })).toBeNull();
	});

	it('reports a successful action and a missing one', () => {
		const { unmount } = render(
			PatchNoteEditor,
			page({ mode: 'edit', note: null }, { message: 'Saved.' })
		);
		expect(screen.getByText('Saved.')).toBeTruthy();
		unmount();

		render(PatchNoteEditor, page({ mode: 'edit', note: null }, {}));
		expect(screen.queryByText('Saved.')).toBeNull();
	});
});

describe('announcement editor without a record', () => {
	it('falls back to the generic title and hides the status select', () => {
		render(AnnouncementEditor, page({ mode: 'edit', announcement: null }));

		expect(screen.getByRole('heading', { level: 1, name: 'Edit announcement' })).toBeTruthy();
		expect((screen.getByLabelText(/Title/) as HTMLInputElement).value).toBe('');
		expect(screen.queryByRole('combobox')).toBeNull();
		expect(screen.queryByRole('button', { name: /Publish|Unpublish/ })).toBeNull();
	});
});

describe('lore editor without a record', () => {
	it('falls back to the generic title and the default category', () => {
		render(LoreEditor, page({ mode: 'edit', article: null }));

		expect(screen.getByRole('heading', { level: 1, name: 'Edit lore article' })).toBeTruthy();
		// An absent article still offers the category field, seeded with its default.
		expect((screen.getByLabelText(/Category/) as HTMLInputElement).value).toBe('History');
		expect(screen.queryByRole('button', { name: 'Delete article' })).toBeNull();
	});

	it('offers Publish for a draft article and Unpublish for a live one', () => {
		const { unmount } = render(
			LoreEditor,
			page({
				mode: 'edit',
				article: {
					id: 'lore-1',
					slug: 'the-emberfall',
					title: 'The Emberfall',
					summary: 'How the sky caught fire.',
					content: '# The Emberfall',
					category: 'History',
					status: 'Draft',
					publishedAt: null
				}
			})
		);
		expect(screen.getByRole('button', { name: 'Publish' })).toBeTruthy();
		unmount();

		render(
			LoreEditor,
			page({
				mode: 'edit',
				article: {
					id: 'lore-2',
					slug: 'calen',
					title: 'Calen',
					summary: 'The first city.',
					content: '# Calen',
					category: null,
					status: 'Published',
					publishedAt: '2026-02-01T00:00:00.000Z'
				}
			})
		);
		expect(screen.getByRole('button', { name: 'Unpublish' })).toBeTruthy();
	});
});

describe('editorial editors, re-render', () => {
	const note = {
		id: 'note-1',
		slug: 'alpha-0-1-0',
		title: 'Alpha 0.1.0',
		version: '0.1.0',
		summary: 'First playable build.',
		content: '## Added',
		status: 'Published',
		publishedAt: '2026-02-01T00:00:00.000Z'
	};
	const announcement = {
		id: 'ann-1',
		title: 'Server opening',
		body: 'Friday at 20:00 UTC.',
		status: 'Draft',
		publishedAt: null
	};
	const article = {
		id: 'lore-1',
		slug: 'the-emberfall',
		title: 'The Emberfall',
		summary: 'How the sky caught fire.',
		content: '# The Emberfall',
		category: 'History',
		status: 'Published',
		publishedAt: '2026-02-01T00:00:00.000Z'
	};

	/**
	 * An equal second payload re-runs the compiled `<select>` value effect, whose only suppressed
	 * side is the one taken when the selection has not moved.
	 */
	async function assertStableSelect(
		component: never,
		data: Record<string, unknown>,
		expected: string
	) {
		const { rerender } = render(component, page({ mode: 'edit', ...data }));
		await rerender(page({ mode: 'edit', ...data }));

		expect((screen.getByRole('combobox') as HTMLSelectElement).value).toBe(expected);
	}

	it('keeps a published patch note on its status', async () => {
		await assertStableSelect(PatchNoteEditor as never, { note }, 'Published');
	});

	it('keeps a draft announcement on its status', async () => {
		await assertStableSelect(AnnouncementEditor as never, { announcement }, 'Draft');
	});

	it('keeps a published lore article on its status', async () => {
		await assertStableSelect(LoreEditor as never, { article }, 'Published');
	});
});
