import { fireEvent, render, screen } from '@testing-library/svelte';
import { createRawSnippet } from 'svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { goto, resetNavigation } from '#lib/testing/app-navigation';
import { fakeUser } from '#lib/testing/event';
import { setPage } from '#lib/testing/app-state';
import RootLayout from './+layout.svelte';
import RootError from './+error.svelte';
import CharactersPage from './characters/+page.svelte';
import CommunityPage from './community/+page.svelte';
import Pager from '#lib/components/Pager.svelte';
import SiteFooter from '#lib/components/site/SiteFooter.svelte';
import SiteHeader from '#lib/components/site/SiteHeader.svelte';
import PatchNoteArticle from './patch-notes/[slug]/+page.svelte';

/**
 * Site chrome and the signed-in shell pages.
 *
 * The header and footer decide what a signed-in player can reach, so the staff links are asserted
 * per permission rather than as one "staff" flag. The roster and community pages are the two
 * places a player arrives from the hub, and both are asserted on their error paths as well as
 * their happy path.
 */

/** Props for a route page; `form` is `null` on a plain GET. */
function page(data: Record<string, unknown>, form: Record<string, unknown> | null = null) {
	return { data, form } as never;
}

beforeEach(() => {
	resetNavigation();
	setPage();
});

describe('root layout', () => {
	const children = createRawSnippet(() => ({ render: () => '<p>page body</p>' }));

	it('wraps a normal page in the site header and footer', () => {
		setPage({ url: new URL('http://localhost/lore') });
		render(RootLayout, {
			data: { user: null, staff: null },
			children
		} as never);

		expect(screen.getByRole('banner')).toBeTruthy();
		expect(screen.getByText('page body')).toBeTruthy();
		expect(screen.getByRole('contentinfo')).toBeTruthy();
	});

	it('renders the game shell full screen, without site chrome', () => {
		setPage({ url: new URL('http://localhost/play/char-1') });
		render(RootLayout, {
			data: { user: null, staff: null },
			children
		} as never);

		expect(screen.getByText('page body')).toBeTruthy();
		expect(screen.queryByRole('banner')).toBeNull();
		expect(screen.queryByRole('contentinfo')).toBeNull();
	});
});

describe('site header', () => {
	it('shows the three public links and the sign-in pair for a visitor', () => {
		render(SiteHeader, { user: null, staff: null });

		expect(screen.getByRole('link', { name: 'Lore' }).getAttribute('href')).toBe('/lore');
		expect(screen.getByRole('link', { name: 'Patch notes' })).toBeTruthy();
		expect(screen.getByRole('link', { name: 'Community' })).toBeTruthy();
		expect(screen.getByRole('link', { name: 'Sign in' })).toBeTruthy();
		expect(screen.getByRole('link', { name: 'Create account' })).toBeTruthy();
		expect(screen.queryByRole('link', { name: 'Admin' })).toBeNull();
	});

	it('shows the account name and Play for a signed-in player', () => {
		render(SiteHeader, { user: fakeUser({ username: 'kaleth' }), staff: null });

		expect(screen.getByRole('link', { name: 'kaleth' }).getAttribute('href')).toBe('/account');
		expect(screen.getByRole('link', { name: 'Play' }).getAttribute('href')).toBe('/characters');
		expect(screen.queryByRole('link', { name: 'Sign in' })).toBeNull();
	});

	it('falls back to the display name when the username is unset', () => {
		render(SiteHeader, { user: fakeUser({ username: null }), staff: null });

		expect(screen.getByRole('link', { name: 'Player One' })).toBeTruthy();
	});

	it('adds one link per staff capability held', () => {
		render(SiteHeader, {
			user: fakeUser({ username: 'kaleth' }),
			staff: { foundry: true, editorial: true, mod: true, admin: true }
		});

		expect(screen.getByRole('link', { name: 'Foundry' }).getAttribute('href')).toBe('/foundry');
		expect(screen.getByRole('link', { name: 'Editorial' }).getAttribute('href')).toBe('/editorial');
		expect(screen.getByRole('link', { name: 'Mod' }).getAttribute('href')).toBe('/mod');
		expect(screen.getByRole('link', { name: 'Admin' }).getAttribute('href')).toBe('/admin');
	});

	it('sends a signed-in player to the contribution form, never to the portal', () => {
		render(SiteHeader, {
			user: fakeUser({ username: 'malfeasor' }),
			staff: { foundry: false, submit: true }
		});

		expect(screen.getByRole('link', { name: 'Submit content' }).getAttribute('href')).toBe(
			'/submissions/new'
		);
		// The portal home answers 403 to an account without `content.view`, so it must not be offered.
		expect(screen.queryByRole('link', { name: 'Foundry' })).toBeNull();
	});

	it('shows an editor the portal instead of the contribution shortcut', () => {
		render(SiteHeader, {
			user: fakeUser({ username: 'kaleth' }),
			staff: { foundry: true, submit: true }
		});

		expect(screen.queryByRole('link', { name: 'Submit content' })).toBeNull();
	});

	it('marks the current section as the active page, including nested routes', () => {
		setPage({ url: new URL('http://localhost/lore/the-emberfall') });
		render(SiteHeader, { user: null, staff: null });

		expect(screen.getByRole('link', { name: 'Lore' }).getAttribute('aria-current')).toBe('page');
		expect(screen.getByRole('link', { name: 'Patch notes' }).getAttribute('aria-current')).toBe(
			null
		);
	});

	it('does not mark a prefix-matching sibling as active', () => {
		setPage({ url: new URL('http://localhost/patch-notes') });
		render(SiteHeader, { user: null, staff: null });

		expect(screen.getByRole('link', { name: 'Patch notes' }).getAttribute('aria-current')).toBe(
			'page'
		);
		expect(screen.getByRole('link', { name: 'Lore' }).getAttribute('aria-current')).toBe(null);
	});
});

describe('site footer', () => {
	it('offers sign-in links to a visitor', () => {
		render(SiteFooter, { user: null });

		expect(screen.getByRole('link', { name: 'Sign In' })).toBeTruthy();
		expect(screen.getByRole('link', { name: 'World & Lore' })).toBeTruthy();
		expect(screen.getByRole('link', { name: 'Source' })).toBeTruthy();
		expect(screen.queryByRole('link', { name: 'Account' })).toBeNull();
	});

	it('offers Play and Account to a signed-in player', () => {
		render(SiteFooter, { user: fakeUser({ username: 'kaleth' }) });

		expect(screen.getByRole('link', { name: 'Play' }).getAttribute('href')).toBe('/characters');
		expect(screen.getByRole('link', { name: 'Account' }).getAttribute('href')).toBe('/account');
		expect(screen.queryByRole('link', { name: 'Sign In' })).toBeNull();
	});
});

describe('pager', () => {
	it('renders nothing when everything fits on one page', () => {
		const { container } = render(Pager, {
			page: 1,
			pageSize: 20,
			total: 5,
			basePath: '/mod/users'
		});

		expect(container.querySelector('nav')).toBeNull();
	});

	it('keeps the active filters in both directions and disables the edges', () => {
		render(Pager, {
			page: 1,
			pageSize: 20,
			total: 45,
			basePath: '/mod/users',
			query: { search: 'br', status: '', sort: 'warncount' }
		});

		expect(screen.getByText('Page 1 of 3')).toBeTruthy();
		// Empty filter values are dropped, non-empty ones survive.
		expect(screen.getByRole('link', { name: /Next/ }).getAttribute('href')).toBe(
			'/mod/users?search=br&sort=warncount&page=2'
		);
		expect(screen.getByRole('link', { name: /Prev/ }).getAttribute('aria-disabled')).toBe('true');
	});

	it('clamps the next link on the last page', () => {
		render(Pager, { page: 3, pageSize: 20, total: 45, basePath: '/admin/audit' });

		expect(screen.getByRole('link', { name: /Next/ }).getAttribute('aria-disabled')).toBe('true');
		expect(screen.getByRole('link', { name: /Prev/ }).getAttribute('href')).toBe(
			'/admin/audit?page=2'
		);
	});

	it('survives a zero page size without dividing by zero', () => {
		render(Pager, { page: 1, pageSize: 0, total: 45, basePath: '/x' });

		// The guard clamps the divisor to 1, so every row gets its own page instead of Infinity.
		expect(screen.getByText('Page 1 of 45')).toBeTruthy();
	});
});

describe('root error page', () => {
	it('gives a 404 its own copy and hides the retry button', () => {
		setPage({ status: 404, error: null, url: new URL('http://localhost/nope') });
		render(RootError, {} as never);

		expect(screen.getByRole('heading', { level: 1, name: 'Lost in the mists' })).toBeTruthy();
		expect(screen.getByText(/the road you followed has faded/)).toBeTruthy();
		expect(screen.queryByRole('link', { name: /Try again/ })).toBeNull();
	});

	it('shows the thrown message for a server error and offers a retry', () => {
		setPage({
			status: 500,
			error: { message: 'The roster service is down' },
			url: new URL('http://localhost/characters')
		});
		render(RootError, {} as never);

		expect(screen.getByRole('heading', { level: 1, name: 'Something went wrong' })).toBeTruthy();
		expect(screen.getByText('The roster service is down')).toBeTruthy();
		expect(screen.getByRole('link', { name: /Try again/ }).getAttribute('href')).toBe(
			'/characters'
		);
	});

	it('falls back to generic copy when the error carries no message', () => {
		setPage({ status: 503, error: null, url: new URL('http://localhost/') });
		render(RootError, {} as never);

		expect(screen.getByText(/The server could not finish that request/)).toBeTruthy();
	});
});

describe('community page', () => {
	const announcements = [{ id: 'a1', title: 'Server opening', body: 'Friday, 20:00 UTC.' }];

	it('reports the server as online and lists announcements', () => {
		render(CommunityPage, page({ online: true, announcements, error: null }));

		expect(screen.getByText('Online and accepting connections.')).toBeTruthy();
		expect(screen.getByText('Online')).toBeTruthy();
		expect(screen.getByText('Server opening')).toBeTruthy();
	});

	it('reports the server as offline with the announcement error and empty state', () => {
		render(CommunityPage, page({ online: false, announcements: [], error: 'Feed unavailable' }));

		expect(screen.getByText('Status unavailable right now.')).toBeTruthy();
		expect(screen.getByText('Offline')).toBeTruthy();
		expect(screen.getByText('Feed unavailable')).toBeTruthy();
		expect(screen.getByText('No announcements right now.')).toBeTruthy();
	});

	it('refuses to look up a value that is not an account GUID', async () => {
		render(CommunityPage, page({ online: true, announcements: [], error: null }));

		await fireEvent.input(screen.getByLabelText('Account ID'), { target: { value: 'kaleth' } });
		await fireEvent.click(screen.getByRole('button', { name: /View profile/ }));

		expect(screen.getByText('Enter a valid account GUID.')).toBeTruthy();
		expect(goto).not.toHaveBeenCalled();
	});

	it('navigates to the profile for a valid GUID', async () => {
		render(CommunityPage, page({ online: true, announcements: [], error: null }));

		await fireEvent.input(screen.getByLabelText('Account ID'), {
			target: { value: '11111111-1111-4111-8111-111111111111' }
		});
		await fireEvent.click(screen.getByRole('button', { name: /View profile/ }));

		expect(goto).toHaveBeenCalledWith('/players/11111111-1111-4111-8111-111111111111');
		expect(screen.queryByText('Enter a valid account GUID.')).toBeNull();
	});
});

describe('character roster', () => {
	const character = {
		id: 'char-1',
		name: 'Kaleth',
		slotIndex: 0,
		level: 7,
		className: 'Ranger',
		speciesSlug: 'human',
		experience: 12345,
		currentZoneId: 'emberfall',
		currentZoneLocationSlug: 'emberfall-market',
		isOnline: false,
		isHardcore: true
	};

	const roster = (overrides: Record<string, unknown> = {}) => ({
		characters: [character],
		slots: { used: 1, max: 5, remaining: 4 },
		emailVerified: true,
		lastSession: null,
		memorial: [],
		loadError: null,
		slotsError: null,
		lastSessionError: null,
		...overrides
	});

	beforeEach(() => {
		vi.spyOn(window, 'confirm').mockReturnValue(true);
	});

	afterEach(() => {
		vi.restoreAllMocks();
	});

	it('lists characters with their badges, slot and XP', () => {
		render(CharactersPage, page(roster()));

		expect(screen.getByRole('heading', { level: 1, name: 'Choose your character' })).toBeTruthy();
		expect(screen.getByText('Kaleth')).toBeTruthy();
		expect(screen.getByText('Hardcore')).toBeTruthy();
		expect(screen.getByText('1 of 5 character slots used')).toBeTruthy();
		// The XP figure shares its line with the zone label.
		expect(document.body.textContent).toContain('12,345');
	});

	it('enters the world when Enter world is pressed', async () => {
		render(CharactersPage, page(roster()));

		await fireEvent.click(screen.getByRole('button', { name: /Enter world/ }));

		expect(goto).toHaveBeenCalledWith('/play/char-1');
	});

	it('offers Resume for an offline last session and jumps straight in', async () => {
		render(CharactersPage, page(roster({ lastSession: { characterId: 'char-1' } })));

		expect(
			screen.getByRole('heading', { level: 2, name: 'Resume your last session' })
		).toBeTruthy();
		expect(screen.getByText(/at emberfall-market/)).toBeTruthy();
		expect(screen.getAllByText('Last played').length).toBeGreaterThanOrEqual(1);

		await fireEvent.click(screen.getByRole('button', { name: /Resume/ }));
		expect(goto).toHaveBeenCalledWith('/play/char-1');
	});

	it('falls back to the zone id when the location is unknown, and hides Resume while online', () => {
		const { unmount } = render(
			CharactersPage,
			page(
				roster({
					characters: [{ ...character, currentZoneLocationSlug: null }],
					lastSession: { characterId: 'char-1' }
				})
			)
		);

		expect(screen.getByText(/in emberfall/)).toBeTruthy();
		unmount();

		render(
			CharactersPage,
			page(
				roster({
					characters: [{ ...character, isOnline: true }],
					lastSession: { characterId: 'char-1' }
				})
			)
		);

		expect(screen.queryByRole('heading', { name: 'Resume your last session' })).toBeNull();
		expect(screen.getByText('Already online')).toBeTruthy();
	});

	it('shows the empty state when the player has no characters', () => {
		render(CharactersPage, page(roster({ characters: [] })));

		expect(screen.getByRole('heading', { level: 2, name: 'No characters yet' })).toBeTruthy();
		expect(screen.getByText('Create your first character to enter Veldrath.')).toBeTruthy();
	});

	it('explains a full slot list instead of offering a Create button', () => {
		const { unmount } = render(
			CharactersPage,
			page(roster({ characters: [], slots: { used: 5, max: 5, remaining: 0 } }))
		);

		expect(screen.getByText(/All character slots are in use/)).toBeTruthy();
		expect(screen.getByText(/Your character slots are full/)).toBeTruthy();
		unmount();

		render(CharactersPage, page(roster({ slots: null })));

		// No slot information at all still means "you may create one".
		expect(screen.getByRole('link', { name: /Create character/ })).toBeTruthy();
	});

	it('collects the unverified-email warning and every load error', () => {
		render(
			CharactersPage,
			page(
				roster({
					emailVerified: false,
					loadError: 'Roster unavailable',
					slotsError: 'Slots unavailable',
					lastSessionError: 'timeout'
				}),
				{ error: 'Delete failed' }
			)
		);

		expect(screen.getByText(/Your email address is not verified/)).toBeTruthy();
		expect(screen.getByText('Roster unavailable')).toBeTruthy();
		expect(screen.getByText('Slots unavailable')).toBeTruthy();
		expect(screen.getByText('Delete failed')).toBeTruthy();
		expect(screen.getByText(/Could not load your last session: timeout/)).toBeTruthy();
	});

	it('renders the memorial for fallen hardcore characters', () => {
		render(
			CharactersPage,
			page(
				roster({
					characters: [],
					memorial: [
						{
							characterId: 'char-9',
							name: 'Brenn',
							className: 'Warrior',
							level: 12,
							diedAt: '2026-01-15T00:00:00.000Z'
						}
					]
				})
			)
		);

		expect(screen.getByText('Memorial')).toBeTruthy();
		expect(screen.getByText('Brenn')).toBeTruthy();
		expect(screen.getByText(/Warrior · Level 12/)).toBeTruthy();
	});

	it('asks for confirmation before deleting and submits when confirmed', async () => {
		render(CharactersPage, page(roster()));

		const form = screen.getByRole('button', { name: /Delete/ }).closest('form') as HTMLFormElement;
		const prevented = vi.fn();
		form.addEventListener('submit', (event) => {
			// jsdom would try to navigate; the assertion is about the handler's decision.
			event.preventDefault();
			prevented();
		});
		await fireEvent.click(screen.getByRole('button', { name: /Delete/ }));

		expect(window.confirm).toHaveBeenCalledWith('Delete Kaleth? This cannot be undone.');
		expect(prevented).toHaveBeenCalled();
	});

	it('cancels the delete when the confirmation is declined', async () => {
		vi.mocked(window.confirm).mockReturnValue(false);
		render(CharactersPage, page(roster()));

		const form = screen.getByRole('button', { name: /Delete/ }).closest('form') as HTMLFormElement;
		const submit = new Event('submit', { cancelable: true, bubbles: true });
		form.dispatchEvent(submit);

		expect(submit.defaultPrevented).toBe(true);
	});
});

describe('patch note article', () => {
	it('renders the version, summary, date and sanitized body', () => {
		render(
			PatchNoteArticle,
			page({
				note: {
					id: 'n1',
					slug: 'alpha-0-1-0',
					title: 'Alpha 0.1.0',
					version: '0.1.0',
					summary: 'First playable build.',
					publishedAt: '2026-02-01T00:00:00.000Z'
				},
				html: '<p>Added the character creation wizard.</p>'
			})
		);

		expect(screen.getByRole('heading', { level: 1, name: 'Alpha 0.1.0' })).toBeTruthy();
		expect(screen.getByText('0.1.0')).toBeTruthy();
		expect(screen.getByText('First playable build.')).toBeTruthy();
		expect(screen.getByText('Added the character creation wizard.')).toBeTruthy();
		expect(screen.getByText(/Published/)).toBeTruthy();
	});

	it('omits the published date for a draft', () => {
		render(
			PatchNoteArticle,
			page({
				note: {
					id: 'n2',
					slug: 'draft',
					title: 'Unreleased',
					version: '0.2.0',
					summary: 'Still cooking.',
					publishedAt: null
				},
				html: ''
			})
		);

		expect(screen.queryByText(/Published/)).toBeNull();
	});
});
