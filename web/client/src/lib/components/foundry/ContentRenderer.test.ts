import { render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';
import ContentRenderer from './ContentRenderer.svelte';
import type { ContentField } from '#lib/foundry/content-editor';

/** A field descriptor, defaulted the way the server sends it. */
const field = (name: string, fieldType: string, overrides: Record<string, unknown> = {}) =>
	({
		name,
		label: name,
		fieldType,
		required: false,
		default: null,
		min: null,
		max: null,
		enumValues: null,
		hint: null,
		...overrides
	}) as ContentField;

const schema = {
	contentType: 'recipe',
	displayLabel: 'Recipes',
	description: '',
	groups: [
		{
			label: 'Identity',
			fields: [
				field('displayName', 'Text', { label: 'Display name' }),
				field('isActive', 'Boolean', { label: 'Active' }),
				field('notes', 'LongText', { label: 'Notes' }),
				field('missingValue', 'Text', { label: 'Missing value' })
			]
		},
		{
			label: 'Collections',
			fields: [
				field('tags', 'StringList', { label: 'Tags' }),
				field('stats', 'KeyValue', { label: 'Stats' }),
				field('ingredients', 'ObjectList', {
					label: 'Ingredients',
					items: [
						field('itemSlug', 'Text', { label: 'Item' }),
						field('quantity', 'Integer', { label: 'Qty' })
					]
				}),
				field('skill', 'Reference', { label: 'Skill', refType: 'skill' })
			]
		}
	]
} as never;

const payload = {
	displayName: 'Iron sword',
	isActive: true,
	notes: 'A dependable blade.',
	tags: ['metal', 'starter'],
	stats: { damage: 12 },
	ingredients: [{ itemSlug: 'iron-ingot', quantity: 3 }],
	skill: 'blacksmithing',
	undocumented: 'kept'
};

describe('ContentRenderer', () => {
	it('renders each group with the schema label and a readable value', () => {
		render(ContentRenderer, { schema, payload } as never);

		expect(screen.getByRole('heading', { level: 2, name: 'Identity' })).toBeTruthy();
		expect(screen.getByText('Display name')).toBeTruthy();
		expect(screen.getByText('Iron sword')).toBeTruthy();
		// A boolean reads as a word, and an absent value as a dash rather than "undefined".
		expect(screen.getByText('Yes')).toBeTruthy();
		expect(screen.getByText('—')).toBeTruthy();
		expect(screen.getByText('A dependable blade.')).toBeTruthy();
	});

	it('renders collections as chips, a table and rows', () => {
		render(ContentRenderer, { schema, payload } as never);

		expect(screen.getByText('metal')).toBeTruthy();
		expect(screen.getByText('damage')).toBeTruthy();
		expect(screen.getByText('12')).toBeTruthy();

		expect(screen.getByRole('columnheader', { name: 'Item' })).toBeTruthy();
		expect(screen.getByText('iron-ingot')).toBeTruthy();
		expect(screen.getByText('3')).toBeTruthy();
	});

	it('links a reference to the content it names', () => {
		render(ContentRenderer, { schema, payload } as never);

		expect(screen.getByRole('link', { name: 'blacksmithing' }).getAttribute('href')).toBe(
			'/content/skill/blacksmithing'
		);
	});

	it('lists keys the schema does not describe instead of dropping them', () => {
		render(ContentRenderer, { schema, payload } as never);

		expect(screen.getByRole('heading', { level: 2, name: 'Other fields' })).toBeTruthy();
		expect(screen.getByText('undocumented')).toBeTruthy();
		expect(screen.getByText('kept')).toBeTruthy();
	});

	it('keeps the raw JSON behind a disclosure', () => {
		render(ContentRenderer, { schema, payload } as never);

		expect(screen.getByText('Advanced — raw JSON')).toBeTruthy();
		expect(screen.getByText(/"displayName": "Iron sword"/)).toBeTruthy();
	});

	it('renders every key as an other field when no schema is known', () => {
		render(ContentRenderer, { schema: null, payload: { slug: 'ember-blade' } } as never);

		expect(screen.queryByRole('heading', { level: 2, name: 'Identity' })).toBeNull();
		expect(screen.getByRole('heading', { level: 2, name: 'Other fields' })).toBeTruthy();
		expect(screen.getByText('ember-blade')).toBeTruthy();
	});

	it('survives an absent payload', () => {
		render(ContentRenderer, { schema, payload: undefined } as never);

		expect(screen.getByRole('heading', { level: 2, name: 'Identity' })).toBeTruthy();
	});
});
