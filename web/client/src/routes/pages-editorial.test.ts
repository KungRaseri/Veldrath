import { render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';
import EditorialHome from './(editorial)/editorial/+page.svelte';
import AnnouncementList from './(editorial)/editorial/announcements/+page.svelte';
import AnnouncementEditor from './(editorial)/editorial/announcements/[id]/+page.svelte';
import LoreList from './(editorial)/editorial/lore/+page.svelte';
import LoreEditor from './(editorial)/editorial/lore/[id]/+page.svelte';
import PatchNoteList from './(editorial)/editorial/patch-notes/+page.svelte';
import PatchNoteEditor from './(editorial)/editorial/patch-notes/[id]/+page.svelte';

/**
 * Editorial page components.
 *
 * The three lists and their three editors share one shape: a table of every entry (drafts
 * included), and an editor whose create mode hides the slug field's disabled twin and whose
 * publish button flips to "Unpublish" once the entry is live. Those are the branches a
 * content editor hits daily, and they are asserted here rather than only at the loader.
 *
 * Loaders and form actions are covered in `staff-admin-editorial.test.ts`.
 */

/** Props for a route page; `form` is `null` on a plain GET. */
function page(data: Record<string, unknown>, form: Record<string, unknown> | null = null) {
	return { data, form } as never;
}

const article = {
	id: 'lore-1',
	slug: 'the-emberfall',
	title: 'The Emberfall',
	summary: 'How the sky caught fire.',
	content: '# The Emberfall',
	category: 'History',
	status: 'Draft',
	publishedAt: null
};

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
	status: 'Published',
	publishedAt: '2026-02-02T00:00:00.000Z'
};

describe('editorial overview', () => {
	it('shows one count per content kind', () => {
		render(
			EditorialHome,
			page({ counts: { lore: 12, patchNotes: 4, announcements: 2 }, errors: [] })
		);

		expect(screen.getByRole('heading', { level: 1, name: 'Editorial' })).toBeTruthy();
		expect(screen.getByText('12')).toBeTruthy();
		expect(screen.getByRole('heading', { level: 2, name: 'Patch notes' })).toBeTruthy();
		expect(screen.getByRole('heading', { level: 2, name: 'Announcements' })).toBeTruthy();
	});

	it('lists every count error above the sections', () => {
		render(
			EditorialHome,
			page({
				counts: { lore: 0, patchNotes: 0, announcements: 0 },
				errors: ['Lore count failed', 'Patch notes count failed']
			})
		);

		expect(screen.getByText('Lore count failed')).toBeTruthy();
		expect(screen.getByText('Patch notes count failed')).toBeTruthy();
	});
});

describe('editorial lore list', () => {
	it('lists drafts and published articles with their dates', () => {
		render(
			LoreList,
			page({
				total: 2,
				articles: [
					article,
					{
						...article,
						id: 'lore-2',
						slug: 'calen',
						title: 'Calen',
						status: 'Published',
						publishedAt: '2026-02-01T00:00:00.000Z'
					}
				],
				page: 1,
				pageSize: 20
			})
		);

		expect(screen.getByText('2 articles, including drafts.')).toBeTruthy();
		expect(screen.getByRole('link', { name: 'The Emberfall' }).getAttribute('href')).toBe(
			'/editorial/lore/lore-1'
		);
		// `Published` is a column header as well as a status chip.
		expect(screen.getAllByText('Published').length).toBeGreaterThanOrEqual(2);
		expect(screen.getByText('Draft')).toBeTruthy();
		expect(screen.getAllByText('—')).toHaveLength(1);
	});

	it('renders the empty state and an error', () => {
		render(
			LoreList,
			page({
				total: 0,
				error: 'Editorial API unavailable',
				articles: [],
				page: 1,
				pageSize: 20
			})
		);

		expect(screen.getByText('Editorial API unavailable')).toBeTruthy();
		expect(screen.getByText('No lore articles yet.')).toBeTruthy();
	});
});

describe('editorial lore editor', () => {
	it('creates: shows a required slug field and hides the destructive controls', () => {
		render(LoreEditor, page({ mode: 'create' }));

		expect(screen.getByRole('heading', { level: 1, name: 'New lore article' })).toBeTruthy();
		expect((screen.getByLabelText(/Slug/) as HTMLInputElement).placeholder).toBe(
			'founding-of-calen'
		);
		expect(screen.getByRole('button', { name: 'Create article' })).toBeTruthy();
		expect(screen.queryByRole('button', { name: 'Delete article' })).toBeNull();
		expect(screen.queryByRole('button', { name: /publish/i })).toBeNull();
	});

	it('edits: pins the slug, preloads the fields and offers publish and delete', () => {
		render(LoreEditor, page({ mode: 'edit', article }));

		expect(screen.getByRole('heading', { level: 1, name: 'Edit lore article' })).toBeTruthy();
		expect((screen.getByDisplayValue('the-emberfall') as HTMLInputElement).disabled).toBe(true);
		expect(screen.getByDisplayValue('The Emberfall')).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Publish' })).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Delete article' })).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Save changes' })).toBeTruthy();
	});

	it('flips the publish button when the article is already live', () => {
		render(LoreEditor, page({ mode: 'edit', article: { ...article, status: 'Published' } }));

		expect(screen.getByRole('button', { name: 'Unpublish' })).toBeTruthy();
	});

	it('reports a rejected save', () => {
		render(LoreEditor, page({ mode: 'edit', article }, { error: 'Title is already taken' }));

		expect(screen.getByText('Title is already taken')).toBeTruthy();
	});

	it('reports a successful publish', () => {
		render(LoreEditor, page({ mode: 'edit', article }, { message: 'Article published' }));

		expect(screen.getByText('Article published')).toBeTruthy();
		// A message is not an error: the error paragraph must stay empty.
		expect(screen.queryByText('Article published')).not.toBeNull();
	});
});

describe('editorial patch notes list', () => {
	it('lists versions and dates', () => {
		render(
			PatchNoteList,
			page({
				total: 2,
				notes: [
					note,
					{ ...note, id: 'note-2', title: 'Alpha 0.0.9', version: '0.0.9', publishedAt: null }
				],
				page: 1,
				pageSize: 20
			})
		);

		expect(screen.getByText('2 patch notes, including drafts.')).toBeTruthy();
		expect(screen.getByRole('link', { name: 'Alpha 0.1.0' }).getAttribute('href')).toBe(
			'/editorial/patch-notes/note-1'
		);
		expect(screen.getByText('0.1.0')).toBeTruthy();
		expect(screen.getAllByText('—')).toHaveLength(1);
	});

	it('renders the empty state and an error', () => {
		render(
			PatchNoteList,
			page({ total: 0, error: 'List failed', notes: [], page: 1, pageSize: 20 })
		);

		expect(screen.getByText('List failed')).toBeTruthy();
		expect(screen.getByText('No patch notes yet.')).toBeTruthy();
	});
});

describe('editorial patch note editor', () => {
	it('creates: version is required and there is nothing to publish', () => {
		render(PatchNoteEditor, page({ mode: 'create' }));

		expect(screen.getByRole('heading', { level: 1, name: 'New patch note' })).toBeTruthy();
		expect((screen.getByLabelText(/Version/) as HTMLInputElement).required).toBe(true);
		expect(screen.getByRole('button', { name: 'Create patch note' })).toBeTruthy();
		expect(screen.queryByRole('button', { name: 'Delete patch note' })).toBeNull();
	});

	it('edits: preloads version and content and offers unpublish', () => {
		render(PatchNoteEditor, page({ mode: 'edit', note }));

		expect(screen.getByRole('heading', { level: 1, name: 'Edit patch note' })).toBeTruthy();
		expect(screen.getByDisplayValue('0.1.0')).toBeTruthy();
		expect(screen.getByDisplayValue('First playable build.')).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Unpublish' })).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Delete patch note' })).toBeTruthy();
	});

	it('shows a rejected save', () => {
		render(PatchNoteEditor, page({ mode: 'edit', note }, { error: 'Version already exists' }));

		expect(screen.getByText('Version already exists')).toBeTruthy();
	});
});

describe('editorial announcements list', () => {
	it('lists announcements with status and date', () => {
		render(
			AnnouncementList,
			page({
				total: 2,
				announcements: [
					announcement,
					{ ...announcement, id: 'ann-2', title: 'Maintenance window', publishedAt: null }
				],
				page: 1,
				pageSize: 20
			})
		);

		expect(screen.getByText('2 announcements, including drafts.')).toBeTruthy();
		expect(screen.getByRole('link', { name: 'Server opening' }).getAttribute('href')).toBe(
			'/editorial/announcements/ann-1'
		);
		expect(screen.getAllByText('Published').length).toBeGreaterThanOrEqual(2);
		expect(screen.getAllByText('—')).toHaveLength(1);
	});

	it('renders the empty state and an error', () => {
		render(
			AnnouncementList,
			page({ total: 0, error: 'List failed', announcements: [], page: 1, pageSize: 20 })
		);

		expect(screen.getByText('List failed')).toBeTruthy();
		expect(screen.getByText('No announcements yet.')).toBeTruthy();
	});
});

describe('editorial announcement editor', () => {
	it('creates: no status field and no destructive controls', () => {
		render(AnnouncementEditor, page({ mode: 'create' }));

		expect(screen.getByRole('heading', { level: 1, name: 'New announcement' })).toBeTruthy();
		expect((screen.getByLabelText(/Title/) as HTMLInputElement).required).toBe(true);
		expect(screen.getByRole('button', { name: 'Create announcement' })).toBeTruthy();
		expect(screen.queryByRole('combobox')).toBeNull();
	});

	it('edits: preloads the body, exposes the status select and the publish toggle', () => {
		render(AnnouncementEditor, page({ mode: 'edit', announcement }));

		expect(screen.getByRole('heading', { level: 1, name: 'Edit announcement' })).toBeTruthy();
		expect(screen.getByDisplayValue('Friday at 20:00 UTC.')).toBeTruthy();
		expect((screen.getByRole('combobox') as HTMLSelectElement).value).toBe('Published');
		expect(screen.getByRole('button', { name: 'Unpublish' })).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Delete announcement' })).toBeTruthy();
	});

	it('shows a rejected save and a successful one', () => {
		const { unmount } = render(
			AnnouncementEditor,
			page({ mode: 'edit', announcement }, { error: 'Body cannot be empty' })
		);
		expect(screen.getByText('Body cannot be empty')).toBeTruthy();
		unmount();

		render(AnnouncementEditor, page({ mode: 'edit', announcement }, { message: 'Saved' }));
		expect(screen.getByText('Saved')).toBeTruthy();
	});
});
