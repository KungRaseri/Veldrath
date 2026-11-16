import { fireEvent, render, screen, within } from '@testing-library/svelte';
import { createRawSnippet } from 'svelte';
import { describe, expect, it } from 'vitest';
import { setPage } from '#lib/testing/app-state';
import FoundryBreadcrumbs from '#lib/components/foundry/FoundryBreadcrumbs.svelte';
import FoundryEmptyState from '#lib/components/foundry/FoundryEmptyState.svelte';
import FoundryPageHeader from '#lib/components/foundry/FoundryPageHeader.svelte';
import FoundrySection from '#lib/components/foundry/FoundrySection.svelte';
import FoundrySidebar from '#lib/components/foundry/FoundrySidebar.svelte';
import { foundryNav } from '#lib/foundry/nav';
import FoundryError from './(foundry)/+error.svelte';
import ContentIndex from './(foundry)/content/+page.svelte';
import ContentBrowse from './(foundry)/content/[type]/+page.svelte';
import ContentDetail from './(foundry)/content/[type]/[slug]/+page.svelte';
import ContentEditor from './(foundry)/content/[type]/[slug]/edit/+page.svelte';
import NewContentEntry from './(foundry)/content/[type]/new/+page.svelte';
import FoundryHome from './(foundry)/foundry/+page.svelte';
import NewLanguage from './(foundry)/languages/new/+page.svelte';
import MapDetail from './(foundry)/maps/[mapId]/+page.svelte';
import Maps from './(foundry)/maps/+page.svelte';
import Notifications from './(foundry)/notifications/+page.svelte';
import ReviewQueue from './(foundry)/review/+page.svelte';
import Schemas from './(foundry)/schemas/+page.svelte';
import WorldHealth from './(foundry)/world-health/+page.svelte';
import Submissions from './(foundry)/submissions/+page.svelte';
import SubmissionDetail from './(foundry)/submissions/[id]/+page.svelte';

/**
 * Foundry page components.
 *
 * The content editor is the one page here with real logic in the markup: it renders an input per
 * field type, blocks Save when the server returned a blocking issue, and distinguishes a saved
 * action from a validate action in its status banner. The rest are browse surfaces, asserted on
 * rows and on their empty states.
 *
 * Loaders and form actions are covered in `foundry-loaders.test.ts`.
 */

/** Props for a route page; `form` is `null` on a plain GET. */
function page(data: Record<string, unknown>, form: Record<string, unknown> | null = null) {
	return { data, form } as never;
}

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
				field({
					name: 'displayName',
					label: 'Display name',
					fieldType: 'Text',
					required: true,
					hint: 'Shown in game'
				}),
				field({ name: 'slug', label: 'Slug', fieldType: 'Slug', required: true }),
				field({ name: 'isActive', label: 'Active', fieldType: 'Boolean' }),
				field({
					name: 'rarity',
					label: 'Rarity',
					fieldType: 'EnumString',
					enumValues: ['Common', 'Rare']
				}),
				field({ name: 'description', label: 'Description', fieldType: 'LongText' }),
				field({ name: 'weight', label: 'Weight', fieldType: 'Decimal', min: 0, max: 100 })
			]
		}
	]
};

const payload = {
	displayName: 'Ember blade',
	slug: 'ember-blade',
	isActive: true,
	rarity: 'Rare',
	description: 'A blade of banked fire.',
	weight: 1.5
};

const summary = {
	contentType: 'item',
	typeKey: 'Weapon',
	slug: 'ember-blade',
	displayName: 'Ember blade',
	rarityWeight: 12,
	isActive: true
};

const editorData = {
	canEdit: true,
	contentType: 'item',
	slug: 'ember-blade',
	summary,
	schema,
	payload
};

const mapSummary = {
	id: 'zone:the-halrow',
	mapType: 'Zone',
	ownerId: 'the-halrow',
	name: 'The Halrow',
	description: '',
	entryNodeId: 'zone:the-halrow:entry',
	nodeCount: 2,
	edgeCount: 1,
	version: 3
};

const mapNode = {
	id: 'zone:the-halrow:entry',
	mapId: 'zone:the-halrow',
	nodeType: 'EntryPoint',
	refId: null,
	displayName: 'Arrival',
	description: '',
	iconKey: null,
	layoutX: 1,
	layoutY: 1,
	isEntryPoint: true,
	isExitPoint: false,
	targetMapId: null,
	targetNodeId: null,
	isHidden: false,
	unlockType: null,
	unlockKey: null,
	discoverThreshold: null,
	sortOrder: 0
};

const mapGraph = {
	id: 'zone:the-halrow',
	mapType: 'Zone',
	ownerId: 'the-halrow',
	name: 'The Halrow',
	description: '',
	entryNodeId: 'zone:the-halrow:entry',
	nodes: [mapNode],
	edges: [],
	version: 3
};

const mapTargets = {
	maps: [{ id: 'region:varenmark', label: 'Varenmark' }],
	zones: [{ id: 'the-halrow', label: 'The Halrow' }],
	locations: [{ id: 'halrow-threshold', label: 'The Threshold' }],
	regions: [{ id: 'varenmark', label: 'Varenmark' }],
	nodes: [
		{
			id: 'region:varenmark:entry',
			mapId: 'region:varenmark',
			label: 'Arrival',
			isEntryPoint: true
		}
	]
};

describe('map editor pages', () => {
	it('links a graph at the route group path, not /foundry', () => {
		render(Maps, page({ canEdit: true, maps: [mapSummary] }));

		expect(screen.getByRole('heading', { level: 1, name: 'Maps' })).toBeTruthy();
		expect(screen.getByRole('link', { name: 'The Halrow' }).getAttribute('href')).toBe(
			'/maps/zone%3Athe-halrow'
		);
		expect(screen.queryByText(/lacks the content permission/)).toBeNull();
	});

	it('tells a reader that the write controls are off', () => {
		render(Maps, page({ canEdit: false, maps: [mapSummary] }));

		expect(screen.getByText(/lacks the content permission/)).toBeTruthy();
	});

	it('shows the validate result with a badge on the node it names', () => {
		render(
			MapDetail,
			page(
				{ canEdit: true, graph: mapGraph },
				{
					validated: true,
					isValid: false,
					issues: [
						{
							code: 'node_name_missing',
							message: 'Needs a name',
							severity: 'Error',
							mapId: 'zone:the-halrow',
							nodeId: 'zone:the-halrow:entry'
						}
					]
				}
			)
		);

		expect(screen.getByRole('button', { name: 'Validate' })).toBeTruthy();
		expect(screen.getByText(/would be refused/)).toBeTruthy();
		expect(screen.getByText('1 issue')).toBeTruthy();
	});

	it('offers only the draft verbs while a map is a draft', () => {
		render(
			MapDetail,
			page({
				canEdit: true,
				canReview: true,
				graph: { ...mapGraph, isActive: false, approvedVersion: 0, submittedAt: null },
				targets: mapTargets
			})
		);

		expect(screen.getByRole('button', { name: 'Submit for review' })).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Delete draft' })).toBeTruthy();
		expect(screen.queryByRole('button', { name: 'Approve' })).toBeNull();
		expect(screen.queryByRole('button', { name: 'Deactivate' })).toBeNull();
	});

	it('offers approve and reject while a map is in review', () => {
		render(
			MapDetail,
			page({
				canEdit: true,
				canReview: true,
				graph: { ...mapGraph, isActive: false, submittedAt: '2026-02-01T00:00:00.000Z' },
				targets: mapTargets
			})
		);

		expect(screen.getByRole('button', { name: 'Approve' })).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Reject' })).toBeTruthy();
		expect(screen.queryByRole('button', { name: 'Submit for review' })).toBeNull();
	});

	it('offers deactivate, not delete, for a live map', () => {
		render(
			MapDetail,
			page({
				canEdit: true,
				canReview: true,
				graph: { ...mapGraph, isActive: true, approvedVersion: 3 },
				targets: mapTargets
			})
		);

		expect(screen.getByRole('button', { name: 'Deactivate' })).toBeTruthy();
		expect(screen.queryByRole('button', { name: 'Delete draft' })).toBeNull();
	});

	it('shows no lifecycle verb to a reader, and no create form on the list', () => {
		render(
			MapDetail,
			page({ canEdit: false, canReview: false, graph: mapGraph, targets: mapTargets })
		);

		expect(screen.queryByRole('button', { name: 'Submit for review' })).toBeNull();
		expect(screen.queryByRole('button', { name: 'Deactivate' })).toBeNull();

		render(
			Maps,
			page({ canEdit: false, maps: [{ ...mapSummary, isActive: false, approvedVersion: 0 }] })
		);

		expect(screen.queryByRole('button', { name: 'Create draft' })).toBeNull();
		expect(screen.getByText('Draft')).toBeTruthy();
	});

	it('opens the inspector for the node selected on the canvas', async () => {
		render(MapDetail, page({ canEdit: true, graph: mapGraph, targets: mapTargets }));

		await fireEvent.click(screen.getByRole('button', { name: /Arrival/ }));

		expect(screen.getByRole('region', { name: 'Node inspector' })).toBeTruthy();
		expect(screen.getByRole('button', { name: /Delete node/ })).toBeTruthy();
	});

	it('summarises a map-level issue that no node row can carry', () => {
		render(
			MapDetail,
			page(
				{ canEdit: true, graph: mapGraph },
				{
					validated: true,
					isValid: false,
					issues: [
						{
							code: 'map_without_exit',
							message: 'Zone map has no exit',
							severity: 'Error',
							mapId: 'zone:the-halrow',
							nodeId: null
						}
					]
				}
			)
		);

		expect(screen.getByText(/map_without_exit/)).toBeTruthy();
	});
});

describe('content type index', () => {
	it('lists each registered schema with a browse link', () => {
		render(
			ContentIndex,
			page({
				types: [
					{ contentType: 'item', displayLabel: 'Items', description: 'Weapons and armour' },
					{ contentType: 'npc', displayLabel: 'NPCs', description: 'People and creatures' }
				]
			})
		);

		expect(screen.getByRole('heading', { level: 1, name: 'Base content' })).toBeTruthy();
		expect(screen.getByRole('heading', { level: 2, name: 'Items' })).toBeTruthy();
		expect(screen.getAllByRole('link', { name: 'Browse' })[0].getAttribute('href')).toBe(
			'/content/item'
		);
	});

	it('renders the empty state and an error', () => {
		render(ContentIndex, page({ types: [], error: 'Schema registry unreachable' }));

		expect(screen.getByText('Schema registry unreachable')).toBeTruthy();
		expect(screen.getByText('No content schemas are registered.')).toBeTruthy();
	});
});

describe('content browse', () => {
	const entries = [
		{
			id: 'item-1',
			contentType: 'item',
			slug: 'ember-blade',
			displayName: 'Ember blade',
			typeKey: 'Weapon',
			rarityWeight: 12,
			updatedAt: '2026-02-01T00:00:00.000Z'
		},
		{
			id: 'item-2',
			contentType: 'item',
			slug: 'iron-helm',
			displayName: null,
			typeKey: 'Armor',
			rarityWeight: 3,
			updatedAt: '2026-02-02T00:00:00.000Z'
		}
	];

	it('lists entries, mapping a missing display name to a dash', () => {
		render(
			ContentBrowse,
			page({
				contentType: 'item',
				total: 2,
				search: 'ember',
				entries,
				page: 1,
				pageSize: 20
			})
		);

		expect(screen.getByText('2 published entries.')).toBeTruthy();
		expect(screen.getByRole('link', { name: 'ember-blade' }).getAttribute('href')).toBe(
			'/content/item/ember-blade'
		);
		expect(screen.getByText('Ember blade')).toBeTruthy();
		expect(screen.getByText('—')).toBeTruthy();
		expect((screen.getByRole('searchbox') as HTMLInputElement).value).toBe('ember');
	});

	it('renders the empty state and an error', () => {
		render(
			ContentBrowse,
			page({
				contentType: 'item',
				total: 0,
				error: 'Content API unavailable',
				search: '',
				entries: [],
				page: 1,
				pageSize: 20
			})
		);

		expect(screen.getByText('Content API unavailable')).toBeTruthy();
		expect(screen.getByText('No entries found.')).toBeTruthy();
	});
});

describe('content create page', () => {
	const createSchema = {
		contentType: 'quest',
		displayLabel: 'Quests',
		description: 'A quest with objectives.',
		groups: [
			{
				label: 'Identity',
				fields: [
					field({
						name: 'displayName',
						label: 'Display name',
						fieldType: 'Text',
						required: true
					}),
					field({ name: 'minLevel', label: 'Min level', fieldType: 'Integer', min: 1, max: 100 })
				]
			},
			{
				label: 'Objectives',
				fields: [field({ name: 'objectives', label: 'Objectives', fieldType: 'ObjectList' })]
			}
		]
	};

	it('renders the schema fields and names the collections it cannot author', () => {
		render(
			NewContentEntry,
			page({ contentType: 'quest', canEdit: true, refOptions: {}, schema: createSchema })
		);

		expect(screen.getByRole('heading', { level: 1, name: 'New Quests' })).toBeTruthy();
		expect(screen.getByLabelText(/^Display name/)).toBeTruthy();
		expect((screen.getByLabelText('Min level') as HTMLInputElement).getAttribute('max')).toBe(
			'100'
		);
		expect(screen.getByText(/Objectives is filled in the editor/)).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Create draft' })).toBeTruthy();
	});

	it('renders every control kind, a Reference datalist, and a plural collection note', () => {
		render(
			NewContentEntry,
			page({
				contentType: 'recipe',
				canEdit: true,
				refOptions: { item: [{ value: 'iron-sword', label: 'Iron Sword' }] },
				schema: {
					contentType: 'recipe',
					displayLabel: 'Recipes',
					description: '',
					groups: [
						{
							label: 'All',
							fields: [
								field({ name: 'isActive', label: 'Active', fieldType: 'Boolean' }),
								field({
									name: 'rarity',
									label: 'Rarity',
									fieldType: 'EnumString',
									enumValues: ['', 'Rare']
								}),
								field({
									name: 'notes',
									label: 'Notes',
									fieldType: 'LongText',
									hint: 'Markdown is fine.'
								}),
								{
									...field({
										name: 'outputItemSlug',
										label: 'Output item',
										fieldType: 'Reference'
									}),
									refType: 'item'
								},
								field({ name: 'tags', label: 'Tags', fieldType: 'StringList' }),
								field({ name: 'stats', label: 'Stats', fieldType: 'KeyValue' })
							]
						}
					]
				}
			})
		);

		expect((screen.getByLabelText('Active') as HTMLInputElement).type).toBe('checkbox');
		expect(screen.getByRole('option', { name: '(empty)' })).toBeTruthy();
		expect((screen.getByLabelText('Notes') as HTMLTextAreaElement).tagName).toBe('TEXTAREA');
		expect(screen.getByText('Markdown is fine.')).toBeTruthy();
		expect(screen.getByText('Iron Sword')).toBeTruthy();
		expect(screen.getByText(/Tags, Stats are filled in the editor/)).toBeTruthy();
	});

	it('falls back to a plain input for an empty enum and an unloaded reference list', () => {
		render(
			NewContentEntry,
			page({
				contentType: 'item',
				canEdit: true,
				refOptions: {},
				schema: {
					contentType: 'item',
					displayLabel: 'Items',
					description: '',
					groups: [
						{
							label: 'All',
							fields: [
								field({
									name: 'rarity',
									label: 'Rarity',
									fieldType: 'EnumString',
									enumValues: [],
									required: true
								}),
								{
									...field({
										name: 'questSlug',
										label: 'Quest',
										fieldType: 'Reference'
									}),
									refType: 'quest'
								}
							]
						}
					]
				}
			})
		);

		// An empty enum is a text field, not an empty select, and a reference with nothing loaded has no list.
		expect((screen.getByLabelText(/^Rarity/) as HTMLInputElement).type).toBe('text');
		expect(screen.queryAllByRole('option')).toEqual([]);
	});

	it('explains that creating needs the editor permission', () => {
		render(
			NewContentEntry,
			page({ contentType: 'quest', canEdit: false, refOptions: {}, schema: createSchema })
		);

		expect(screen.getByText(/requires the content-editor permission/)).toBeTruthy();
		expect(screen.queryByRole('button', { name: 'Create draft' })).toBeNull();
	});

	it('shows a rejected create', () => {
		render(
			NewContentEntry,
			page(
				{ contentType: 'quest', canEdit: true, refOptions: {}, schema: createSchema },
				{ message: 'Slug already exists.' }
			)
		);

		expect(screen.getByRole('alert').textContent).toContain('Slug already exists.');
	});

	it('offers an author a New action on the browse page', () => {
		render(
			ContentBrowse,
			page({
				contentType: 'item',
				canEdit: true,
				total: 0,
				search: '',
				entries: [],
				page: 1,
				pageSize: 24,
				error: null
			})
		);

		expect(screen.getByRole('link', { name: 'New entry' }).getAttribute('href')).toBe(
			'/content/item/new'
		);
	});
});

describe('content detail', () => {
	it('renders the summary, the pretty-printed payload and the edit link for an editor', () => {
		render(
			ContentDetail,
			page({
				contentType: 'item',
				slug: 'ember-blade',
				summary,
				payload,
				canEdit: true
			})
		);

		expect(screen.getByRole('heading', { level: 1, name: 'Ember blade' })).toBeTruthy();
		expect(screen.getByText(/item · Weapon/)).toBeTruthy();
		expect(screen.getByText(/active/)).toBeTruthy();
		expect(screen.getByRole('link', { name: 'Edit this entity' }).getAttribute('href')).toBe(
			'/content/item/ember-blade/edit'
		);
		// The payload is no longer the main view: it sits behind a disclosure.
		expect(screen.getByText(/Advanced — raw JSON/)).toBeTruthy();
		expect(screen.getByText(/"displayName": "Ember blade"/)).toBeTruthy();
	});

	it('hides the edit link and marks an inactive entity', () => {
		render(
			ContentDetail,
			page({
				contentType: 'item',
				slug: 'ember-blade',
				summary: { ...summary, displayName: null, isActive: false },
				payload,
				canEdit: false
			})
		);

		expect(screen.getByRole('heading', { level: 1, name: 'ember-blade' })).toBeTruthy();
		expect(screen.queryByRole('link', { name: 'Edit this entity' })).toBeNull();
		expect(screen.getByText(/inactive/)).toBeTruthy();
	});
});

describe('content editor', () => {
	it('renders one control per field type with the payload preloaded', () => {
		render(ContentEditor, page(editorData));

		expect(screen.getByRole('heading', { level: 1, name: 'Edit Ember blade' })).toBeTruthy();
		expect(screen.getByRole('heading', { level: 2, name: 'Identity' })).toBeTruthy();
		expect((screen.getByLabelText(/Display name/) as HTMLInputElement).value).toBe('Ember blade');
		expect((screen.getByLabelText('Active') as HTMLInputElement).checked).toBe(true);
		expect((screen.getByLabelText('Rarity') as HTMLSelectElement).value).toBe('Rare');
		expect((screen.getByLabelText('Description') as HTMLTextAreaElement).value).toBe(
			'A blade of banked fire.'
		);
		expect((screen.getByLabelText('Weight') as HTMLInputElement).value).toBe('1.5');
		expect(screen.getByText('Shown in game')).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Save' })).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Validate' })).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Deactivate' })).toBeTruthy();
	});

	it('explains that a reader cannot edit instead of rendering the form', () => {
		render(ContentEditor, page({ ...editorData, canEdit: false }));

		expect(screen.getByText(/editing requires the content-editor permission/)).toBeTruthy();
		expect(screen.queryByRole('button', { name: 'Save' })).toBeNull();
	});

	it('reports a saved action', () => {
		render(ContentEditor, page(editorData, { action: 'save', saved: true }));

		expect(screen.getByText('Saved.')).toBeTruthy();
	});

	it('reports a clean validate and lists the issues of a failing one', () => {
		const { unmount } = render(ContentEditor, page(editorData, { action: 'validate', issues: [] }));
		expect(screen.getByText('Validation passed with no issues.')).toBeTruthy();
		unmount();

		render(
			ContentEditor,
			page(editorData, {
				action: 'validate',
				issues: [
					{
						rule: 'Required',
						message: 'Display name is required',
						severity: 'Error',
						field: 'displayName',
						identifier: null
					},
					{
						rule: 'Range',
						message: 'Weight is unusually high',
						severity: 'Warning',
						field: 'weight',
						identifier: null
					},
					{
						rule: 'Slug',
						message: 'Slug must be lowercase',
						severity: 'Warning',
						field: null,
						identifier: null
					}
				]
			})
		);

		expect(screen.getByText('3 issue(s) found.')).toBeTruthy();
		expect(screen.getByText('Display name is required')).toBeTruthy();
		expect(screen.getByText('Weight is unusually high')).toBeTruthy();
		// An issue with no field renders rule and message inside one list item.
		expect(screen.getByText(/Slug must be lowercase/)).toBeTruthy();
		expect((screen.getByRole('button', { name: 'Save' }) as HTMLButtonElement).disabled).toBe(true);
	});

	it('shows an action message and keeps Save enabled for warnings alone', () => {
		render(
			ContentEditor,
			page(editorData, {
				message: 'Row version conflict — reload and retry',
				issues: [
					{
						rule: 'Range',
						message: 'Weight is unusually high',
						severity: 'Warning',
						field: 'weight',
						identifier: null
					}
				]
			})
		);

		expect(screen.getByText('Row version conflict — reload and retry')).toBeTruthy();
		expect((screen.getByRole('button', { name: 'Save' }) as HTMLButtonElement).disabled).toBe(
			false
		);
	});

	it('says so when a content type exposes no fields', () => {
		render(
			ContentEditor,
			page({
				...editorData,
				schema: { ...schema, groups: [] }
			})
		);

		expect(screen.getByText('This content type exposes no editable fields yet.')).toBeTruthy();
	});
});

/** A snippet that renders one line of markup, for the components whose body is the caller's. */
const body = createRawSnippet(() => ({ render: () => '<span>section body</span>' }));

describe('content editor form behaviour', () => {
	it('leaves the slug alone while it is still locked', async () => {
		render(ContentEditor, page(editorData));

		// A required field's label carries the asterisk, so match on the prefix.
		const slug = screen.getByLabelText(/^Slug/) as HTMLInputElement;
		await fireEvent.input(screen.getByLabelText(/Display name/), {
			target: { value: 'Something Else' }
		});

		expect(slug.value).toBe('ember-blade');
	});

	it('derives the slug from the display name once the lock is released', async () => {
		render(ContentEditor, page(editorData));

		await fireEvent.click(screen.getByLabelText('Derive from display name'));
		await fireEvent.input(screen.getByLabelText(/Display name/), {
			target: { value: 'Café Noir' }
		});

		expect((screen.getByLabelText(/^Slug/) as HTMLInputElement).value).toBe('cafe-noir');
	});

	it('renders the markdown preview of a long-text field on request', async () => {
		render(ContentEditor, page(editorData));

		expect(screen.queryByText('A blade of banked fire.')).toBeNull();

		await fireEvent.click(screen.getByRole('button', { name: 'Preview' }));

		expect(screen.getByText('A blade of banked fire.')).toBeTruthy();
	});

	it('renders every preview block and run it parses', async () => {
		render(ContentEditor, page(editorData));

		await fireEvent.input(screen.getByLabelText('Description'), {
			target: {
				value: `# Heading\n\n- one\n- two\n\nplain **bold** _em_ \`code\` [link](/lore)`
			}
		});
		await fireEvent.click(screen.getByRole('button', { name: 'Preview' }));

		expect(screen.getByText('Heading')).toBeTruthy();
		expect(screen.getByText('one')).toBeTruthy();
		expect(screen.getByText('bold')).toBeTruthy();
		expect(screen.getByText('em')).toBeTruthy();
		expect(screen.getByText('code')).toBeTruthy();
		expect(screen.getByRole('link', { name: 'link' }).getAttribute('href')).toBe('/lore');
	});

	it('leaves the slug alone when the schema names no display name field', async () => {
		render(
			ContentEditor,
			page({
				...editorData,
				schema: {
					...schema,
					groups: [
						{
							label: 'Identity',
							fields: [field({ name: 'slug', label: 'Slug', fieldType: 'Slug', required: true })]
						}
					]
				},
				payload: { slug: 'ember-blade' }
			})
		);

		await fireEvent.click(screen.getByLabelText('Derive from display name'));

		expect((screen.getByLabelText(/^Slug/) as HTMLInputElement).value).toBe('ember-blade');
	});

	it('renders the collection editors a schema declares', () => {
		render(
			ContentEditor,
			page({
				...editorData,
				schema: {
					...schema,
					groups: [
						{
							label: 'Collections',
							fields: [
								field({ name: 'tags', label: 'Tags', fieldType: 'StringList' }),
								field({ name: 'stats', label: 'Stats', fieldType: 'KeyValue' }),
								{
									...field({ name: 'objectives', label: 'Objectives', fieldType: 'ObjectList' }),
									items: [field({ name: 'type', label: 'Type', fieldType: 'Text' })]
								},
								{
									...field({ name: 'skill', label: 'Skill', fieldType: 'Reference' }),
									refType: 'skill'
								}
							]
						}
					]
				},
				payload: {
					tags: ['fire'],
					stats: { fire: '10' },
					objectives: [{ type: 'kill' }],
					skill: 'smithing'
				},
				refOptions: { skill: [{ value: 'smithing', label: 'Smithing' }] }
			})
		);

		expect((screen.getByLabelText('tags 1') as HTMLInputElement).value).toBe('fire');
		expect((screen.getByLabelText(/^Key/) as HTMLInputElement).value).toBe('fire');
		expect((screen.getByLabelText(/^Type/) as HTMLInputElement).value).toBe('kill');
		expect(screen.getByText('Smithing')).toBeTruthy();
	});

	it('marks a blank required field and blocks the save before it reaches the server', async () => {
		render(ContentEditor, page(editorData));

		await fireEvent.input(screen.getByLabelText(/Display name/), { target: { value: '' } });

		expect(screen.getByText('This field is required.')).toBeTruthy();
		expect((screen.getByRole('button', { name: 'Save' }) as HTMLButtonElement).disabled).toBe(true);
	});

	it('reports unsaved changes and discards them', async () => {
		render(ContentEditor, page(editorData));

		const displayName = screen.getByLabelText(/Display name/) as HTMLInputElement;
		expect(screen.queryByText('Unsaved changes')).toBeNull();
		expect((screen.getByRole('button', { name: 'Discard' }) as HTMLButtonElement).disabled).toBe(
			true
		);

		await fireEvent.input(displayName, { target: { value: 'Ember blade II' } });
		expect(screen.getByText('Unsaved changes')).toBeTruthy();

		await fireEvent.click(screen.getByRole('button', { name: 'Discard' }));

		expect(displayName.value).toBe('Ember blade');
		expect(screen.queryByText('Unsaved changes')).toBeNull();
	});
});

describe('foundry home', () => {
	it('counts every section and links each destination', () => {
		render(
			FoundryHome,
			page({
				canReview: true,
				unreadNotifications: 2,
				contentTypes: 20,
				maps: 5,
				mapsInReview: 2,
				pendingSubmissions: 3,
				error: null
			})
		);

		expect(screen.getByRole('heading', { level: 1, name: 'Foundry' })).toBeTruthy();
		expect(screen.getByText('3 submissions and 2 maps are waiting for a decision.')).toBeTruthy();
		expect(screen.getByRole('link', { name: /Browse content/ }).getAttribute('href')).toBe(
			'/content'
		);
		expect(screen.getByRole('link', { name: /Open the map editor/ }).getAttribute('href')).toBe(
			'/maps'
		);
		expect(screen.getByRole('link', { name: /Open the queue/ }).getAttribute('href')).toBe(
			'/review'
		);
		expect(screen.getByRole('link', { name: /Open the inbox/ }).getAttribute('href')).toBe(
			'/notifications'
		);
	});

	it('explains itself instead of offering a queue the account cannot use', () => {
		render(
			FoundryHome,
			page({
				canReview: false,
				unreadNotifications: 0,
				contentTypes: 1,
				maps: 0,
				mapsInReview: 0,
				pendingSubmissions: 0,
				error: null
			})
		);

		expect(screen.getByText(/can author content but not review it/)).toBeTruthy();
		expect(screen.queryByRole('link', { name: /Open the queue/ })).toBeNull();
	});

	it('reports the first failed call rather than claiming the section is empty', () => {
		render(
			FoundryHome,
			page({
				canReview: true,
				unreadNotifications: 0,
				contentTypes: 0,
				maps: 0,
				mapsInReview: 0,
				pendingSubmissions: 0,
				error: 'Map list unavailable'
			})
		);

		expect(screen.getByRole('alert').textContent).toContain('Map list unavailable');
	});
});

describe('catalog reference pages', () => {
	it('lists audit findings with their severity and entity', () => {
		render(
			WorldHealth,
			page({
				issues: [
					{
						rule: 'missing-quest-target',
						message: 'Quest x names wolf, which does not exist.',
						severity: 'Error',
						field: 'objectives',
						identifier: 'the-ashen-road'
					}
				],
				errors: 1,
				warnings: 0,
				error: null
			})
		);

		expect(screen.getByRole('heading', { level: 1, name: 'World health' })).toBeTruthy();
		expect(screen.getByText('missing-quest-target')).toBeTruthy();
		expect(screen.getByText('the-ashen-road')).toBeTruthy();
		expect(screen.getByText(/does not exist/)).toBeTruthy();
		expect(screen.getByText(/1 error\(s\) and 0 warning\(s\)/)).toBeTruthy();
	});

	it('shows an audit failure, and dashes for an issue with no entity or field', () => {
		render(
			WorldHealth,
			page({
				issues: [{ rule: 'r', message: 'm', severity: 'Warning', field: null, identifier: null }],
				errors: 0,
				warnings: 1,
				error: 'audit is down'
			})
		);

		expect(screen.getByRole('alert').textContent).toContain('audit is down');
		expect(screen.getAllByText('—')).toHaveLength(2);
	});

	it('celebrates a consistent catalog', () => {
		render(WorldHealth, page({ issues: [], errors: 0, warnings: 0, error: null }));

		expect(screen.getByText(/The catalog is consistent/)).toBeTruthy();
	});

	it('renders each schema with its groups, kinds and rules', () => {
		render(
			Schemas,
			page({
				error: 'schemas are down',
				schemas: [
					{
						contentType: 'quest',
						displayLabel: 'Quests',
						description: 'A quest.',
						groups: [
							{
								label: 'Objectives',
								fields: [
									{
										...field({
											name: 'minLevel',
											label: 'Min level',
											fieldType: 'Integer',
											required: true,
											min: 1,
											max: 100
										}),
										enumValues: null,
										items: null,
										refType: null
									},
									{
										...field({ name: 'objectives', label: 'Objectives', fieldType: 'ObjectList' }),
										items: [field({ name: 'type', label: 'Type', fieldType: 'Text' })]
									},
									{
										...field({ name: 'rarity', label: 'Rarity', fieldType: 'EnumString' }),
										enumValues: ['Common', 'Rare']
									},
									{
										...field({ name: 'itemSlug', label: 'Item', fieldType: 'Reference' }),
										refType: 'item',
										hint: 'Pick an item slug.'
									},
									{
										...field({ name: 'level', label: 'Level', fieldType: 'Integer' }),
										max: 50
									}
								]
							}
						]
					}
				]
			})
		);

		expect(screen.getByRole('heading', { level: 2, name: /Quests/ })).toBeTruthy();
		expect(screen.getByRole('heading', { level: 3, name: 'Objectives' })).toBeTruthy();
		expect(screen.getByText('Min level')).toBeTruthy();
		expect(screen.getAllByText('Integer').length).toBeGreaterThan(0);
		expect(screen.getByText(/required · 1–100/)).toBeTruthy();
		// A collection says what its rows contain, an enum lists its options, a reference names its target.
		expect(screen.getByText(/rows of Type/)).toBeTruthy();
		expect(screen.getByText(/one of Common, Rare/)).toBeTruthy();
		expect(screen.getByText(/references item/)).toBeTruthy();
		expect(screen.getByText('Pick an item slug.')).toBeTruthy();
		// A field with only an upper bound still reads sensibly.
		expect(screen.getByText(/optional · −∞–50/)).toBeTruthy();
		// The schema list still renders when the type list failed.
		expect(screen.getByRole('alert').textContent).toContain('schemas are down');
	});

	it('renders the empty state when no schema is registered', () => {
		render(Schemas, page({ schemas: [], error: null }));

		expect(screen.getByText(/No content schemas are registered/)).toBeTruthy();
	});
});

describe('foundry error boundary', () => {
	it('names the missing capability on a 403 and offers the way back', () => {
		setPage({ status: 403, error: { message: 'The review queue needs the Curator role.' } });
		render(FoundryError);

		expect(screen.getByRole('heading', { level: 1, name: 'Not yours to open' })).toBeTruthy();
		expect(screen.getByText('The review queue needs the Curator role.')).toBeTruthy();
		expect(screen.getByRole('link', { name: 'Back to Foundry' }).getAttribute('href')).toBe(
			'/foundry'
		);
	});

	it('offers a retry for a server error, and not for a 404', () => {
		setPage({ status: 500 });
		const { unmount } = render(FoundryError);
		expect(
			screen.getByRole('heading', { level: 1, name: 'The portal could not finish that' })
		).toBeTruthy();
		expect(screen.getByRole('link', { name: 'Try again' })).toBeTruthy();
		unmount();

		setPage({ status: 404 });
		render(FoundryError);
		expect(screen.getByRole('heading', { level: 1, name: 'No such page' })).toBeTruthy();
		expect(screen.getByText('That page is not part of Foundry.')).toBeTruthy();
		expect(screen.queryByRole('link', { name: 'Try again' })).toBeNull();
	});

	it('falls back to copy when the error carries no message', () => {
		setPage({ status: 403 });
		render(FoundryError);

		expect(screen.getByText('This account lacks the capability the page needs.')).toBeTruthy();
	});
});

describe('review queue', () => {
	it('lists both kinds and links each to its own decision surface', () => {
		render(
			ReviewQueue,
			page({
				submissions: [
					{
						id: 'sub-1',
						contentType: 'Quest',
						title: 'The Ashen Road',
						status: 'Pending',
						submitterName: 'malfeasor',
						voteScore: 2,
						createdAt: '2026-01-01T00:00:00.000Z'
					}
				],
				// A map id carries a colon, so the link has to be encoded.
				maps: [
					{ id: 'zone:fenwick', name: 'Fenwick', mapType: 'Zone', ownerId: 'fenwick', version: 3 }
				],
				error: null
			})
		);

		expect(screen.getByRole('link', { name: 'The Ashen Road' }).getAttribute('href')).toBe(
			'/submissions/sub-1'
		);
		expect(screen.getByRole('link', { name: 'Fenwick' }).getAttribute('href')).toBe(
			'/maps/zone%3Afenwick'
		);
	});

	it('renders an empty state for each kind', () => {
		render(ReviewQueue, page({ submissions: [], maps: [], error: null }));

		expect(screen.getByText(/Nothing is waiting/)).toBeTruthy();
		expect(screen.getByText(/No map is waiting for approval/)).toBeTruthy();
	});

	it('reports a failed load', () => {
		render(ReviewQueue, page({ submissions: [], maps: [], error: 'Submission queue unavailable' }));

		expect(screen.getByRole('alert').textContent).toContain('Submission queue unavailable');
	});
});

describe('foundry shell components', () => {
	it('renders the page header and its description', () => {
		render(FoundryPageHeader, { title: 'Maps', description: 'The world graph.' } as never);

		expect(screen.getByRole('heading', { level: 1, name: 'Maps' })).toBeTruthy();
		expect(screen.getByText('The world graph.')).toBeTruthy();
	});

	it('links every breadcrumb but the current page', () => {
		render(FoundryBreadcrumbs, {
			items: [{ label: 'Foundry', href: '/foundry' }, { label: 'Maps' }]
		} as never);

		expect(screen.getByRole('link', { name: 'Foundry' }).getAttribute('href')).toBe('/foundry');
		expect(screen.queryByRole('link', { name: 'Maps' })).toBeNull();
		expect(screen.getByText('Maps').getAttribute('aria-current')).toBe('page');
	});

	it('renders an empty-state sentence', () => {
		render(FoundryEmptyState, { message: 'Nothing is waiting.' } as never);

		expect(screen.getByText('Nothing is waiting.')).toBeTruthy();
	});

	it('badges a section only when it is given a count', () => {
		render(FoundrySection, { title: 'Content', count: 7, children: body } as never);

		expect(screen.getByRole('heading', { level: 2 }).textContent).toContain('Content');
		expect(screen.getByText('7')).toBeTruthy();
		expect(screen.getByText('section body')).toBeTruthy();
	});

	it('renders a header without a description or actions', () => {
		render(FoundryPageHeader, { title: 'Bare' } as never);

		const heading = screen.getByRole('heading', { level: 1, name: 'Bare' });
		expect(heading.parentElement?.querySelector('p')).toBeNull();
	});

	it('renders the sidebar groups, the home entry and the queue badges', () => {
		render(FoundrySidebar, {
			nav: foundryNav({
				canViewContent: true,
				canManageContent: false,
				canReview: true,
				unreadNotifications: 2,
				pendingReview: 5
			})
		} as never);

		expect(screen.getByRole('link', { name: /Home/ }).getAttribute('href')).toBe('/foundry');
		expect(screen.getByText('Curate')).toBeTruthy();
		expect(screen.getByRole('link', { name: /Review/ }).textContent).toContain('5');
		expect(screen.getByRole('link', { name: /Notifications/ }).textContent).toContain('2');
	});

	it('omits the Curate group for a reader and still marks the inbox', () => {
		render(FoundrySidebar, {
			nav: foundryNav({
				canViewContent: false,
				canManageContent: false,
				canReview: false,
				unreadNotifications: 1
			})
		} as never);

		expect(screen.queryByText('Curate')).toBeNull();
		expect(screen.getByRole('link', { name: /Notifications/ }).textContent).toContain('1');
	});
});

describe('new language form', () => {
	it('renders every field and a submit button', () => {
		render(NewLanguage, page({}));

		expect(screen.getByRole('heading', { level: 1, name: 'Build a language' })).toBeTruthy();
		expect((screen.getByLabelText(/Display name/) as HTMLInputElement).required).toBe(true);
		expect(screen.getByLabelText(/Sample text/)).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Submit for review' })).toBeTruthy();
	});

	it('renders a rejected submission', () => {
		render(NewLanguage, page({}, { error: 'That slug is already taken' }));

		expect(screen.getByText('That slug is already taken')).toBeTruthy();
	});
});

describe('foundry notifications', () => {
	const notifications = [
		{
			id: 'n1',
			kind: 'Submission',
			submissionId: 's1',
			mapId: null,
			title: 'Ember blade',
			message: 'Approved by the curators',
			createdAt: '2026-02-01T00:00:00.000Z',
			isRead: false
		},
		{
			id: 'n2',
			kind: 'Submission',
			submissionId: 's2',
			mapId: null,
			title: 'Frost ward',
			message: 'Rejected: balance',
			createdAt: '2026-02-02T00:00:00.000Z',
			isRead: true
		}
	];

	it('offers Mark read only on unread notifications', () => {
		render(Notifications, page({ notifications }));

		expect(screen.getByRole('link', { name: 'Ember blade' }).getAttribute('href')).toBe(
			'/submissions/s1'
		);
		expect(screen.getAllByRole('button', { name: 'Mark read' })).toHaveLength(1);
		expect(screen.getByText('Read')).toBeTruthy();
	});

	it('links a map notification to the map, not to a submission', () => {
		render(
			Notifications,
			page({
				notifications: [
					{
						id: 'n3',
						kind: 'MapReview',
						submissionId: null,
						mapId: 'zone:the-halrow',
						title: 'The Halrow',
						message: 'Your map "The Halrow" was approved and is now live.',
						createdAt: '2026-02-03T00:00:00.000Z',
						isRead: true
					}
				]
			})
		);

		expect(screen.getByRole('link', { name: 'The Halrow' }).getAttribute('href')).toBe(
			'/maps/zone%3Athe-halrow'
		);
	});

	it('renders the empty state and prefers the action error', () => {
		render(
			Notifications,
			page({ notifications: [], error: 'Inbox unavailable' }, { error: 'Notification not found' })
		);

		expect(screen.getByText('Notification not found')).toBeTruthy();
		expect(screen.queryByText('Inbox unavailable')).toBeNull();
		expect(screen.getByText('No notifications yet.')).toBeTruthy();
	});
});

describe('submission queue', () => {
	const submissions = [
		{
			id: 's1',
			title: 'Ember blade',
			contentType: 'Item',
			status: 'Pending',
			submitterName: 'kaleth',
			voteScore: 4,
			createdAt: '2026-02-01T00:00:00.000Z'
		},
		{
			id: 's2',
			title: 'Frost ward',
			contentType: 'Spell',
			status: 'Rejected',
			submitterName: 'sable',
			voteScore: -1,
			createdAt: '2026-02-02T00:00:00.000Z'
		}
	];

	it('lists submissions with status, submitter and score', () => {
		render(
			Submissions,
			page({
				total: 2,
				filters: { search: 'ember', status: 'Pending', contentType: 'Item' },
				submissions,
				page: 1,
				pageSize: 20
			})
		);

		expect(screen.getByRole('heading', { level: 1, name: 'Community submissions' })).toBeTruthy();
		expect(screen.getByRole('link', { name: 'Ember blade' }).getAttribute('href')).toBe(
			'/submissions/s1'
		);
		expect(screen.getByText('kaleth')).toBeTruthy();
		expect(screen.getAllByText('Pending')).toHaveLength(2);
		expect(screen.getByText('-1')).toBeTruthy();
	});

	it('renders the empty state and an error', () => {
		render(
			Submissions,
			page({
				total: 0,
				error: 'Submission queue unavailable',
				filters: {},
				submissions: [],
				page: 1,
				pageSize: 20
			})
		);

		expect(screen.getByText('Submission queue unavailable')).toBeTruthy();
		expect(screen.getByText('No submissions match these filters.')).toBeTruthy();
	});
});

describe('submission detail', () => {
	const submission = {
		id: 's1',
		title: 'Ember blade',
		contentType: 'Item',
		status: 'Pending',
		submitterName: 'kaleth',
		voteScore: 4,
		description: 'A blade of banked fire.',
		payload: '{"slug":"ember-blade","damage":12}',
		reviewerName: null,
		reviewedAt: null,
		reviewNotes: null
	};

	it('renders the type picker and the chosen schema instead of a JSON box', () => {
		render(
			SubmissionDetail,
			page({
				mode: 'create',
				submission: null,
				error: null,
				canReview: false,
				types: [{ contentType: 'item', displayLabel: 'Items', description: '' }],
				typeKey: 'item',
				payload: {},
				schema: {
					contentType: 'item',
					displayLabel: 'Items',
					description: '',
					groups: [
						{
							label: 'Identity',
							fields: [
								field({
									name: 'displayName',
									label: 'Display name',
									fieldType: 'Text',
									required: true
								})
							]
						}
					]
				}
			})
		);

		expect(screen.getByRole('heading', { level: 1, name: 'New submission' })).toBeTruthy();
		expect(screen.getByRole('combobox', { name: /Content type/ })).toBeTruthy();
		expect(screen.getByLabelText(/^Display name/)).toBeTruthy();
		// The JSON skeleton is gone: the payload is built from the schema.
		expect(screen.queryByDisplayValue(/"slug": ""/)).toBeNull();
		expect(screen.getByRole('button', { name: 'Submit for review' })).toBeTruthy();
	});

	it('says which fields this form cannot author', () => {
		render(
			SubmissionDetail,
			page({
				mode: 'create',
				submission: null,
				error: null,
				canReview: false,
				types: [],
				typeKey: 'quest',
				payload: {},
				schema: {
					contentType: 'quest',
					displayLabel: 'Quests',
					description: '',
					groups: [
						{
							label: 'Objectives',
							fields: [field({ name: 'objectives', label: 'Objectives', fieldType: 'ObjectList' })]
						}
					]
				}
			})
		);

		expect(screen.getByText(/This type also has Objectives/)).toBeTruthy();
	});

	it('renders the payload through the schema and the vote controls', () => {
		render(
			SubmissionDetail,
			page({
				mode: 'view',
				submission,
				canReview: false,
				schema: null,
				payload: { slug: 'ember-blade', damage: 12 },
				rawPayload: submission.payload
			})
		);

		expect(screen.getByRole('heading', { level: 1, name: 'Ember blade' })).toBeTruthy();
		expect(screen.getByText('A blade of banked fire.')).toBeTruthy();
		// No schema is known here, so every key is listed rather than dropped.
		expect(screen.getByRole('heading', { level: 2, name: 'Other fields' })).toBeTruthy();
		expect(screen.getByText('damage')).toBeTruthy();
		expect(screen.getByText(/"damage": 12/)).toBeTruthy();
		expect(screen.getByRole('button', { name: /Upvote/ })).toBeTruthy();
		expect(screen.queryByRole('button', { name: 'Approve' })).toBeNull();
	});

	it('renders a boolean, an enum, a long text and a bounded number in the create form', () => {
		render(
			SubmissionDetail,
			page({
				mode: 'create',
				submission: null,
				error: null,
				canReview: false,
				types: [{ contentType: 'item', displayLabel: 'Items', description: '' }],
				typeKey: 'item',
				payload: {},
				schema: {
					contentType: 'item',
					displayLabel: 'Items',
					description: '',
					groups: [
						{
							label: 'All',
							fields: [
								field({ name: 'isActive', label: 'Active', fieldType: 'Boolean' }),
								field({
									name: 'rarity',
									label: 'Rarity',
									fieldType: 'EnumString',
									enumValues: ['', 'Rare']
								}),
								field({ name: 'body', label: 'Body', fieldType: 'LongText' }),
								field({ name: 'weight', label: 'Weight', fieldType: 'Decimal', min: 0, max: 9 })
							]
						}
					]
				}
			})
		);

		expect((screen.getByLabelText('Active') as HTMLInputElement).type).toBe('checkbox');
		expect(screen.getByRole('option', { name: '(empty)' })).toBeTruthy();
		expect((screen.getByLabelText('Body') as HTMLTextAreaElement).tagName).toBe('TEXTAREA');
		expect((screen.getByLabelText('Weight') as HTMLInputElement).getAttribute('max')).toBe('9');
	});

	it('shows what approval would write, side by side with the catalog', () => {
		render(
			SubmissionDetail,
			page({
				mode: 'view',
				canReview: true,
				submission,
				schema: {
					contentType: 'item',
					displayLabel: 'Items',
					description: '',
					groups: [
						{
							label: 'Identity',
							fields: [field({ name: 'displayName', label: 'Display name', fieldType: 'Text' })]
						}
					]
				},
				payload: { displayName: 'Ember blade v2' },
				rawPayload: '{}',
				proposedSlug: 'ember-blade',
				existing: { displayName: 'Old blade' }
			})
		);

		expect(screen.getByRole('heading', { level: 2, name: 'What approval writes' })).toBeTruthy();

		// The same value also appears in the payload view, so scope the comparison to the diff table.
		const diff = within(screen.getByRole('table'));
		expect(diff.getByText('Old blade')).toBeTruthy();
		expect(diff.getByText('Ember blade v2')).toBeTruthy();
	});

	it('links the published entity on an approved submission', () => {
		render(
			SubmissionDetail,
			page({
				mode: 'view',
				canReview: false,
				schema: null,
				payload: {},
				rawPayload: '{}',
				submission: { ...submission, status: 'Approved', promotedSlug: 'ember-blade' }
			})
		);

		expect(screen.getByRole('link', { name: 'Item/ember-blade' }).getAttribute('href')).toBe(
			'/content/item/ember-blade'
		);
	});

	it('shows a curator decision only for a pending submission the viewer can review', () => {
		render(SubmissionDetail, page({ mode: 'view', submission, canReview: true }));

		expect(screen.getByRole('heading', { level: 2, name: 'Curator decision' })).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Approve' })).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Reject' })).toBeTruthy();
	});

	it('renders the review outcome and keeps an unparseable payload verbatim', () => {
		render(
			SubmissionDetail,
			page({
				mode: 'view',
				canReview: true,
				schema: null,
				payload: {},
				rawPayload: 'not json',
				submission: {
					...submission,
					status: 'Approved',
					payload: 'not json',
					reviewerName: 'sable',
					reviewedAt: '2026-02-03T00:00:00.000Z',
					reviewNotes: 'Great flavour'
				}
			})
		);

		expect(screen.getByRole('heading', { level: 2, name: 'Review' })).toBeTruthy();
		expect(screen.getByText('Great flavour')).toBeTruthy();
		expect(screen.getByText('not json')).toBeTruthy();
		// A decided submission no longer offers the decision form.
		expect(screen.queryByRole('heading', { name: 'Curator decision' })).toBeNull();
	});

	it('renders the entry error and skips the optional sections', () => {
		render(
			SubmissionDetail,
			page(
				{
					mode: 'view',
					canReview: false,
					submission: { ...submission, description: null }
				},
				{ error: 'Submission not found' }
			)
		);

		expect(screen.getByText('Submission not found')).toBeTruthy();
		expect(screen.queryByRole('heading', { name: 'Review' })).toBeNull();
	});
});
