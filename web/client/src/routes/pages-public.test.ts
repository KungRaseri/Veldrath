import { render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';
import HomePage from './+page.svelte';
import HardcoreBoard from './leaderboard/hardcore/+page.svelte';
import LoreList from './lore/+page.svelte';
import LoreArticle from './lore/[slug]/+page.svelte';
import PatchNoteList from './patch-notes/+page.svelte';
import PlayerProfile from './players/[id]/+page.svelte';

/**
 * Public page components.
 *
 * The loaders are tested separately; these cases cover what the reader actually sees — including
 * the empty states, which is where a page most often ships broken because nobody visits it with an
 * empty database.
 *
 * The `data` fixtures are cast because a page's `PageData` is generated per route and importing six
 * of them here would be noise; the loaders are the place where those shapes are enforced.
 */

const withData = (data: Record<string, unknown>) => ({ data: data as never });

describe('home page', () => {
	it('always shows the hero and the three feature cards', async () => {
		render(HomePage, {
			props: withData({ announcements: [], lore: [], patchNotes: [] })
		});

		expect(await screen.findByRole('heading', { name: 'Veldrath' })).toBeTruthy();
		expect(screen.getByText('Deep character building')).toBeTruthy();
		expect(screen.getByText('A living world')).toBeTruthy();
		expect(screen.getByText('Crafted by the community')).toBeTruthy();
	});

	it('hides the announcement section and shows both empty states', async () => {
		render(HomePage, {
			props: withData({ announcements: [], lore: [], patchNotes: [] })
		});

		expect(await screen.findByText('Lore articles will appear here.')).toBeTruthy();
		expect(screen.getByText('Patch notes will appear here.')).toBeTruthy();
		expect(screen.queryByRole('heading', { name: 'Announcements' })).toBeNull();
	});

	it('lists announcements, lore, and patch notes when the API returned them', async () => {
		render(HomePage, {
			props: withData({
				announcements: [{ id: 'a1', title: 'Server opening', body: 'Friday, 20:00 UTC.' }],
				lore: [{ id: 'l1', slug: 'emberfall', title: 'The Emberfall', summary: 'How it began.' }],
				patchNotes: [
					{ id: 'p1', slug: 'v1-2', title: 'v1.2', version: '1.2.0', summary: 'Combat tuning.' }
				]
			})
		});

		expect(await screen.findByRole('heading', { name: 'Announcements' })).toBeTruthy();
		expect(screen.getByText('Server opening')).toBeTruthy();
		expect(screen.getByText('The Emberfall')).toBeTruthy();
		expect(screen.getByText('Combat tuning.')).toBeTruthy();
		expect(screen.getByText('1.2.0')).toBeTruthy();
	});
});

describe('lore list', () => {
	it('renders the category chips and the article cards', async () => {
		render(LoreList, {
			props: withData({
				category: 'History',
				categories: ['Factions', 'History'],
				articles: [
					{
						id: 'l1',
						slug: 'emberfall',
						title: 'The Emberfall',
						summary: 'How it began.',
						category: 'History',
						publishedAt: '2026-01-02T00:00:00.000Z'
					}
				],
				total: 1,
				error: null
			})
		});

		expect(await screen.findByRole('link', { name: 'All' })).toBeTruthy();
		expect(screen.getAllByRole('link', { name: 'History' }).length).toBeGreaterThan(0);
		expect(screen.getByText('The Emberfall')).toBeTruthy();
		expect(screen.getByText('How it began.')).toBeTruthy();
	});

	it('shows the error and the empty state', async () => {
		render(LoreList, {
			props: withData({
				category: null,
				categories: [],
				articles: [],
				total: 0,
				error: 'Request failed (503).'
			})
		});

		expect(await screen.findByText('Request failed (503).')).toBeTruthy();
		expect(screen.getByText('No lore articles have been published yet.')).toBeTruthy();
	});
});

describe('lore article', () => {
	it('renders the header, the published date, and the sanitized body', async () => {
		const { container } = render(LoreArticle, {
			props: withData({
				article: {
					id: 'l1',
					slug: 'emberfall',
					title: 'The Emberfall',
					summary: 'How it began.',
					category: 'History',
					status: 'Published',
					publishedAt: '2026-01-02T00:00:00.000Z',
					content: '# The Fall'
				},
				html: '<h1>The Fall</h1><p>It began with fire.</p>'
			})
		});

		expect(await screen.findByRole('heading', { name: 'The Emberfall' })).toBeTruthy();
		expect(screen.getByText('History')).toBeTruthy();
		expect(screen.getByText(/Published/)).toBeTruthy();
		expect(container.querySelector('.markdown h1')?.textContent).toBe('The Fall');
	});

	it('omits the published date for a draft', async () => {
		render(LoreArticle, {
			props: withData({
				article: {
					id: 'l1',
					slug: 'draft',
					title: 'Unfinished',
					summary: 'Later.',
					category: 'History',
					status: 'Draft',
					publishedAt: null,
					content: ''
				},
				html: ''
			})
		});

		expect(await screen.findByRole('heading', { name: 'Unfinished' })).toBeTruthy();
		expect(screen.queryByText(/Published/)).toBeNull();
	});
});

describe('patch notes list', () => {
	it('renders entries with their version and date', async () => {
		render(PatchNoteList, {
			props: withData({
				notes: [
					{
						id: 'p1',
						slug: 'v1-2',
						title: 'v1.2',
						version: '1.2.0',
						summary: 'Combat tuning.',
						status: 'Published',
						publishedAt: '2026-02-03T00:00:00.000Z'
					}
				],
				total: 1,
				error: null
			})
		});

		expect(await screen.findByRole('heading', { name: 'v1.2' })).toBeTruthy();
		expect(screen.getByText('1.2.0')).toBeTruthy();
		expect(screen.getByText('Combat tuning.')).toBeTruthy();
	});

	it('shows the empty state', async () => {
		render(PatchNoteList, {
			props: withData({ notes: [], total: 0, error: null })
		});

		expect(await screen.findByText('No patch notes have been published yet.')).toBeTruthy();
	});
});

describe('player profile', () => {
	it('renders the character summary and the account id', async () => {
		render(PlayerProfile, {
			props: withData({
				profile: {
					accountId: 'acc-1',
					username: 'aria',
					level: 12,
					characterClass: 'Ranger',
					species: 'Human',
					currentZone: 'Aldenmere',
					registeredAt: '2026-01-02T00:00:00.000Z'
				}
			})
		});

		expect(await screen.findByRole('heading', { name: 'aria' })).toBeTruthy();
		expect(screen.getByText(/Level 12/)).toBeTruthy();
		expect(screen.getByText(/Ranger/)).toBeTruthy();
		expect(screen.getByText('Aldenmere')).toBeTruthy();
		expect(screen.getByText('acc-1')).toBeTruthy();
	});

	it('falls back to Unknown for a player who is not in a zone', async () => {
		render(PlayerProfile, {
			props: withData({
				profile: {
					accountId: 'acc-2',
					username: 'wanderer',
					level: 1,
					characterClass: null,
					species: null,
					currentZone: null,
					registeredAt: '2026-01-02T00:00:00.000Z'
				}
			})
		});

		expect(await screen.findByText('Unknown')).toBeTruthy();
	});
});

describe('hardcore board', () => {
	const board = (overrides: Record<string, unknown> = {}) => ({
		items: [
			{
				characterId: 'c1',
				name: 'Thornwarden',
				className: 'Warrior',
				level: 9,
				experience: 4200,
				isOwn: true,
				diedAt: '2026-03-04T00:00:00.000Z'
			},
			{
				characterId: 'c2',
				name: 'Aria',
				className: 'Ranger',
				level: 7,
				experience: 2100,
				isOwn: false,
				diedAt: null
			}
		],
		total: 12,
		page: 1,
		pageSize: 10,
		...overrides
	});

	it('lists the living and the fallen with ranks and dates', async () => {
		render(HardcoreBoard, { props: withData({ board: board(), loadError: null }) });

		expect(await screen.findByText('Thornwarden')).toBeTruthy();
		expect(screen.getByText('You')).toBeTruthy();
		expect(screen.getByLabelText('Fallen')).toBeTruthy();
		expect(screen.getByText(/Fell /)).toBeTruthy();
		expect(screen.getByText(/Level 9/)).toBeTruthy();
		expect(screen.getByRole('link', { name: 'Next' })).toBeTruthy();
		expect(screen.queryByRole('link', { name: 'Previous' })).toBeNull();
	});

	it('shows the load error instead of the board', async () => {
		render(HardcoreBoard, {
			props: withData({ board: null, loadError: 'Character list request failed (403).' })
		});

		expect(await screen.findByText('Character list request failed (403).')).toBeTruthy();
	});

	it('shows an honest empty state', async () => {
		render(HardcoreBoard, {
			props: withData({ board: board({ items: [], total: 0 }), loadError: null })
		});

		expect(await screen.findByText(/No hardcore characters yet/)).toBeTruthy();
	});

	it('shows Previous on later pages', async () => {
		render(HardcoreBoard, {
			props: withData({ board: board({ page: 2 }), loadError: null })
		});

		expect(await screen.findByRole('link', { name: 'Previous' })).toBeTruthy();
		expect(screen.queryByRole('link', { name: 'Next' })).toBeNull();
	});
});
