import { cleanup, render, screen } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { setPage } from '#lib/testing/app-state';
import RootError from './+error.svelte';
import LoreList from './lore/+page.svelte';
import PatchNoteList from './patch-notes/+page.svelte';

/**
 * Branch-coverage companion for the public lore/patch-note lists and the root error boundary.
 *
 * The lists are already tested with entries and empty; what is missing is partially-filled data —
 * a note with no publish date, an article whose category is absent, a request that failed while
 * entries still rendered — and the error boundary's copy when SvelteKit hands it no status.
 *
 * `data` is cast because each route's `PageData` is generated per route; the loaders own the shape.
 */
const withData = (data: Record<string, unknown>) => ({ data: data as never });

beforeEach(setPage);
afterEach(cleanup);

describe('patch note list, partial payloads', () => {
	it('renders an unpublished note and a load error alongside the entries', () => {
		render(
			PatchNoteList,
			withData({
				notes: [
					{
						id: 'p1',
						slug: 'v1-2',
						title: 'v1.2',
						version: '1.2.0',
						summary: 'Combat tuning.',
						status: 'Draft',
						publishedAt: null
					}
				],
				total: 1,
				error: 'Patch notes feed is stale.'
			})
		);

		expect(screen.getByText('Patch notes feed is stale.')).toBeTruthy();
		expect(screen.getByRole('heading', { name: 'v1.2' })).toBeTruthy();
		// A draft has no date line, so the version chip stands alone.
		expect(screen.getByText('1.2.0')).toBeTruthy();
	});
});

describe('lore list, partial payloads', () => {
	it('renders an article with no category and no publish date', () => {
		render(
			LoreList,
			withData({
				category: null,
				categories: [],
				articles: [
					{
						id: 'l1',
						slug: 'untitled',
						title: 'Untitled',
						summary: 'No tags yet.',
						category: null,
						publishedAt: null
					}
				],
				total: 1,
				error: null
			})
		);

		expect(screen.getByText('Untitled')).toBeTruthy();
		expect(screen.getByText('No tags yet.')).toBeTruthy();
	});

	it('marks the selected category chip and links the others', () => {
		render(
			LoreList,
			withData({
				category: 'History',
				categories: ['Factions', 'History'],
				articles: [],
				total: 0,
				error: null
			})
		);

		const all = screen.getByRole('link', { name: 'All' });
		expect(all.className).toContain('preset-tonal-surface');
		expect(screen.getByRole('link', { name: 'Factions' }).getAttribute('href')).toBe(
			'/lore?category=Factions'
		);
		expect(screen.getByRole('link', { name: 'History' }).getAttribute('href')).toBe(
			'/lore?category=History'
		);
	});
});

describe('root error page, missing status', () => {
	it('renders without a status when SvelteKit supplies none', () => {
		setPage({ status: null as never, error: null, url: new URL('http://localhost/nope') });

		render(RootError, {} as never);

		expect(screen.getByText(/Error/)).toBeTruthy();
		expect(screen.getByRole('heading', { level: 1, name: 'Something went wrong' })).toBeTruthy();
	});
});
