import { render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';
import ContentDiff from './ContentDiff.svelte';
import type { FieldChange } from '#lib/foundry/content-editor';

const changes: FieldChange[] = [
	{
		field: 'displayName',
		label: 'Display name',
		before: 'Old blade',
		after: 'Ember blade',
		kind: 'changed'
	}
];

describe('ContentDiff', () => {
	it('shows the stored value beside the proposed one', () => {
		render(ContentDiff, { changes, slug: 'ember-blade' } as never);

		expect(screen.getByRole('heading', { level: 2, name: 'What approval writes' })).toBeTruthy();
		expect(screen.getByText('Old blade')).toBeTruthy();
		expect(screen.getByText('Ember blade')).toBeTruthy();
		expect(screen.getByText(/would replace the stored entry/)).toBeTruthy();
	});

	it('says when the proposal would create a new entry', () => {
		render(ContentDiff, {
			changes: [{ ...changes[0], before: '—', kind: 'added' }],
			slug: 'ember-blade'
		} as never);

		expect(screen.getByText(/would create a new entry/)).toBeTruthy();
	});

	it('warns that a payload without a slug cannot be published', () => {
		render(ContentDiff, { changes: [], slug: null } as never);

		expect(screen.getByText(/approval would be refused/)).toBeTruthy();
	});

	it('says when nothing differs', () => {
		render(ContentDiff, { changes: [], slug: 'ember-blade' } as never);

		expect(screen.getByText('Nothing in the payload differs from the catalog.')).toBeTruthy();
	});
});
