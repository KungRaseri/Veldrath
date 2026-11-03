import { cleanup, fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it } from 'vitest';
import ContentBrowse from './(foundry)/content/[type]/+page.svelte';
import ContentDetail from './(foundry)/content/[type]/[slug]/+page.svelte';
import ContentEditor from './(foundry)/content/[type]/[slug]/edit/+page.svelte';
import Submissions from './(foundry)/submissions/+page.svelte';
import SubmissionDetail from './(foundry)/submissions/[id]/+page.svelte';

/**
 * Branch-coverage companion for the Foundry pages.
 *
 * Svelte compiles every text interpolation `{expr}` to `expr ?? ''`, so each dynamic value has a
 * fallback branch that only runs when the value is nullish. The API responses these pages render
 * are not guaranteed to carry every optional field, so each suite below renders the populated case
 * once (already covered elsewhere) and then a deliberately sparse one: null display names, null
 * counters, null submitters. That is the state a half-populated payload produces, and it is what
 * the nullish branches exist for.
 *
 * `pages-foundry.test.ts` owns the happy paths; this file only adds the sparse variants.
 */

function page(data: Record<string, unknown>, form: Record<string, unknown> | null = null) {
	return { data, form } as never;
}

afterEach(cleanup);

describe('content editor, sparse payload', () => {
	const field = (overrides: Record<string, unknown>) => ({
		default: null,
		min: null,
		max: null,
		enumValues: null,
		hint: null,
		required: false,
		...overrides
	});

	const schema = {
		contentType: 'item',
		displayLabel: 'Item',
		description: 'Everything a player can hold.',
		groups: [
			{
				label: 'Identity',
				fields: [
					field({ name: 'displayName', label: 'Display name', fieldType: 'Text' }),
					field({
						name: 'rarity',
						label: 'Rarity',
						fieldType: 'EnumString',
						enumValues: ['', 'Rare']
					}),
					field({ name: 'weight', label: 'Weight', fieldType: 'Integer', min: 0, max: 9 })
				]
			}
		]
	};

	const summary = {
		contentType: 'item',
		typeKey: 'Weapon',
		slug: 'ember-blade',
		displayName: null,
		rarityWeight: 12,
		isActive: true
	};

	it('falls back to the slug in the title, breadcrumb, and heading', () => {
		render(
			ContentEditor,
			page({
				canEdit: true,
				contentType: 'item',
				slug: 'ember-blade',
				summary,
				schema,
				payload: {}
			})
		);

		expect(screen.getByRole('heading', { level: 1, name: 'Edit ember-blade' })).toBeTruthy();
		expect(screen.getAllByText(/ember-blade/).length).toBeGreaterThan(0);
	});

	it('renders the empty enum option and a bounded integer input', () => {
		render(
			ContentEditor,
			page({
				canEdit: true,
				contentType: 'item',
				slug: 'ember-blade',
				summary,
				schema,
				payload: {}
			})
		);

		expect(screen.getByRole('option', { name: '(empty)' })).toBeTruthy();
		const weight = screen.getByLabelText('Weight') as HTMLInputElement;
		expect(weight.getAttribute('min')).toBe('0');
		expect(weight.getAttribute('max')).toBe('9');
		expect(weight.type).toBe('number');
	});

	it('keeps edits in local state when the operator types', async () => {
		render(
			ContentEditor,
			page({
				canEdit: true,
				contentType: 'item',
				slug: 'ember-blade',
				summary,
				schema,
				payload: {}
			})
		);

		const name = screen.getByLabelText('Display name') as HTMLInputElement;
		await fireEvent.input(name, { target: { value: 'Frost ward' } });
		expect(name.value).toBe('Frost ward');

		const rarity = screen.getByLabelText('Rarity') as HTMLSelectElement;
		await fireEvent.change(rarity, { target: { value: 'Rare' } });
		expect(rarity.value).toBe('Rare');
	});

	it('shows no status banner for a form without saved, action, or message keys', () => {
		render(
			ContentEditor,
			page(
				{ canEdit: true, contentType: 'item', slug: 'ember-blade', summary, schema, payload: {} },
				{ issues: [] }
			)
		);

		expect(screen.queryByText('Saved.')).toBeNull();
		expect(screen.queryByText(/Validation passed/)).toBeNull();
		expect(screen.getByRole('button', { name: 'Save' })).toBeTruthy();
	});

	it('reports an action message and lists fieldless and messageless issues', () => {
		render(
			ContentEditor,
			page(
				{ canEdit: true, contentType: 'item', slug: 'ember-blade', summary, schema, payload: {} },
				{
					message: 'Row version conflict',
					issues: [
						{ rule: 'Slug', message: null, severity: 'Warning', field: null, identifier: null },
						{
							rule: 'Range',
							message: 'Too heavy',
							severity: 'Error',
							field: 'weight',
							identifier: null
						}
					]
				}
			)
		);

		expect(screen.getByText('Row version conflict')).toBeTruthy();
		expect(screen.getByText('Too heavy')).toBeTruthy();
		expect(screen.getByText(/\(weight\)/)).toBeTruthy();
		expect((screen.getByRole('button', { name: 'Save' }) as HTMLButtonElement).disabled).toBe(true);
	});
	it('renders an entity whose display name, slug, and schema labels are all absent', () => {
		render(
			ContentEditor,
			page({
				canEdit: true,
				contentType: null,
				slug: null,
				summary: { ...summary, displayName: null },
				schema: { ...schema, displayLabel: null },
				payload: {}
			})
		);

		expect(screen.getByRole('heading', { level: 1, name: 'Edit' })).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Save' })).toBeTruthy();
	});

	it('renders an enum option the payload left empty', () => {
		render(
			ContentEditor,
			page({
				canEdit: true,
				contentType: 'item',
				slug: 'ember-blade',
				summary,
				schema: {
					...schema,
					groups: [
						{
							label: 'Identity',
							fields: [
								field({
									name: 'rarity',
									label: 'Rarity',
									fieldType: 'EnumString',
									enumValues: [null, 'Rare']
								})
							]
						}
					]
				},
				payload: {}
			})
		);

		expect(screen.getByRole('option', { name: '(empty)' })).toBeTruthy();
	});
});

describe('content browse, sparse totals', () => {
	it('renders a content type page with no total and no entries', () => {
		render(
			ContentBrowse,
			page({
				contentType: null,
				total: null,
				search: '',
				entries: [],
				page: 1,
				pageSize: 20
			})
		);

		expect(screen.getByText('No entries found.')).toBeTruthy();
		expect(screen.getByRole('link', { name: /All content types/ })).toBeTruthy();
	});
});

describe('content detail, sparse summary', () => {
	it('falls back to the slug and marks the entity inactive', () => {
		render(
			ContentDetail,
			page({
				contentType: 'item',
				slug: 'iron-helm',
				summary: {
					contentType: null,
					typeKey: null,
					slug: 'iron-helm',
					displayName: null,
					rarityWeight: null,
					isActive: false
				},
				payload: {},
				canEdit: false
			})
		);

		expect(screen.getByRole('heading', { level: 1, name: 'iron-helm' })).toBeTruthy();
		expect(screen.getByText(/inactive/)).toBeTruthy();
	});

	it('renders the breadcrumb when the content type is missing', () => {
		render(
			ContentDetail,
			page({
				contentType: null,
				slug: 'iron-helm',
				summary: {
					contentType: 'item',
					typeKey: 'Armor',
					slug: 'iron-helm',
					displayName: 'Iron helm',
					rarityWeight: 3,
					isActive: true
				},
				payload: {},
				canEdit: true
			})
		);

		expect(screen.getByRole('heading', { level: 1, name: 'Iron helm' })).toBeTruthy();
		expect(screen.getByText(/All entries/)).toBeTruthy();
	});
});

describe('content browse, sparse entries', () => {
	it('renders a row whose display name and category are missing', () => {
		render(
			ContentBrowse,
			page({
				contentType: 'item',
				total: 1,
				search: '',
				entries: [
					{
						id: 'item-9',
						contentType: 'item',
						slug: 'nameless',
						displayName: null,
						typeKey: null,
						rarityWeight: null,
						updatedAt: '2026-02-01T00:00:00.000Z'
					}
				],
				page: 1,
				pageSize: 20
			})
		);

		expect(screen.getByRole('link', { name: 'nameless' })).toBeTruthy();
		expect(screen.getByText('—')).toBeTruthy();
	});
});

describe('submission queue, sparse filters', () => {
	it('renders the filters with every value omitted', () => {
		render(
			Submissions,
			page({
				total: 0,
				filters: { search: null, status: null, contentType: null },
				submissions: [],
				page: 1,
				pageSize: 20
			})
		);

		expect((screen.getByRole('searchbox') as HTMLInputElement).value).toBe('');
		expect((screen.getByLabelText('Status') as HTMLSelectElement).value).toBe('');
		expect(screen.getByText('No submissions match these filters.')).toBeTruthy();
	});

	it('re-renders a filtered queue without disturbing the selects', async () => {
		const data = {
			total: 1,
			filters: { search: 'ember', status: 'Pending', contentType: 'Item' },
			submissions: [
				{
					id: 's1',
					title: 'Ember blade',
					contentType: 'Item',
					status: 'Pending',
					submitterName: 'kaleth',
					voteScore: 4,
					createdAt: '2026-02-01T00:00:00.000Z'
				}
			],
			page: 1,
			pageSize: 20
		};

		const { rerender } = render(Submissions, page(data));
		// An equal second payload re-runs the compiled `<select>` value effects, which only skip
		// their write when the selection has not moved.
		await rerender(page(data));

		expect((screen.getByLabelText('Status') as HTMLSelectElement).value).toBe('Pending');
		expect((screen.getByLabelText('Content type') as HTMLSelectElement).value).toBe('Item');
	});
});

describe('submission detail, sparse submission', () => {
	const submission = {
		id: 's1',
		title: 'Ember blade',
		contentType: 'Item',
		status: 'Pending',
		submitterName: 'kaleth',
		voteScore: 4,
		description: 'A blade of banked fire.',
		payload: '{"slug":"ember-blade"}',
		reviewerName: null,
		reviewedAt: null,
		reviewNotes: null
	};

	it('falls back to the generic title when the submission is missing', () => {
		render(SubmissionDetail, page({ mode: 'view', submission: null, canReview: false }));

		expect(screen.queryByRole('heading', { level: 1 })).toBeNull();
		expect(screen.getByRole('link', { name: /All submissions/ })).toBeTruthy();
	});

	it('renders the title ternary for a view-mode submission', () => {
		render(SubmissionDetail, page({ mode: 'view', submission, canReview: false }));

		expect(screen.getByRole('heading', { level: 1, name: 'Ember blade' })).toBeTruthy();
	});

	it('falls back to placeholder copy for missing submitter, status, and score', () => {
		render(
			SubmissionDetail,
			page({
				mode: 'view',
				canReview: false,
				submission: { ...submission, submitterName: null, status: null, voteScore: null }
			})
		);

		expect(screen.getByText(/By/)).toBeTruthy();
		expect(screen.getByRole('button', { name: /Upvote/ })).toBeTruthy();
	});

	it('renders a review without a date and without notes', () => {
		render(
			SubmissionDetail,
			page({
				mode: 'view',
				canReview: false,
				submission: {
					...submission,
					status: 'Approved',
					reviewerName: 'sable',
					reviewedAt: null,
					reviewNotes: null
				}
			})
		);

		expect(screen.getByRole('heading', { level: 2, name: 'Review' })).toBeTruthy();
		expect(screen.queryByText(/^on /)).toBeNull();
	});

	it('renders a review whose status the payload omitted', () => {
		render(
			SubmissionDetail,
			page({
				mode: 'view',
				canReview: false,
				submission: {
					...submission,
					status: null,
					reviewerName: 'sable',
					reviewedAt: '2026-02-03T00:00:00.000Z',
					reviewNotes: 'Great flavour'
				}
			})
		);

		expect(document.body.textContent).toContain('by sable');
		expect(screen.getByText('Great flavour')).toBeTruthy();
	});

	it('prefers the action error and falls back to the loaded one', () => {
		const { unmount } = render(
			SubmissionDetail,
			page({ mode: 'view', submission, canReview: false }, { error: 'Review failed' })
		);
		expect(screen.getByText('Review failed')).toBeTruthy();
		unmount();

		render(
			SubmissionDetail,
			page({ mode: 'view', submission: null, canReview: false, error: 'Submission gone' })
		);
		expect(screen.getByText('Submission gone')).toBeTruthy();
	});
});
