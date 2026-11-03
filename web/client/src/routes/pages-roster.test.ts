import { render, screen } from '@testing-library/svelte';
import { beforeEach, describe, expect, it } from 'vitest';
import { resetNavigation } from '#lib/testing/app-navigation';
import { setPage } from '#lib/testing/app-state';
import CharactersPage from './characters/+page.svelte';

/**
 * Character roster: the partial-data cases.
 *
 * `pages-site.test.ts` covers the roster's happy path, its empty state and its error banners. This
 * file covers the defensive half — a last-session marker left behind by a deleted character, a
 * character whose cached detail fields are missing, and a slot record that arrived without figures.
 * Those are the shapes the .NET endpoints produce on a 204 or a partially populated row.
 */

function page(data: Record<string, unknown>) {
	return { data, form: null } as never;
}

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

function roster(overrides: Record<string, unknown> = {}) {
	return {
		characters: [character],
		slots: { used: 1, max: 5, remaining: 4 },
		emailVerified: true,
		lastSession: null,
		memorial: [],
		loadError: null,
		slotsError: null,
		lastSessionError: null,
		...overrides
	};
}

beforeEach(() => {
	resetNavigation();
	setPage();
});

describe('character roster fallbacks', () => {
	it('ignores a last-session marker for a character that is no longer listed', () => {
		render(CharactersPage, page(roster({ lastSession: { characterId: 'char-404' } })));

		expect(screen.queryByRole('heading', { name: 'Resume your last session' })).toBeNull();
		expect(screen.queryByText('Last played')).toBeNull();
		expect(screen.getByText('Kaleth')).toBeTruthy();
	});

	it('resumes a character that has no name and no known zone', () => {
		render(
			CharactersPage,
			page(
				roster({
					characters: [
						{ ...character, name: undefined, currentZoneLocationSlug: null, currentZoneId: null }
					],
					lastSession: { characterId: 'char-1' }
				})
			)
		);

		expect(
			screen.getByRole('heading', { level: 2, name: 'Resume your last session' })
		).toBeTruthy();
		expect(screen.getByText(/Continue as/)).toBeTruthy();
		expect(screen.queryByText(/in emberfall/)).toBeNull();
	});

	it('falls back to the region map for a character with missing detail fields', () => {
		render(
			CharactersPage,
			page(
				roster({
					characters: [
						{
							...character,
							className: undefined,
							level: undefined,
							currentZoneId: null,
							experience: { toLocaleString: () => undefined },
							isHardcore: false
						}
					]
				})
			)
		);

		expect(screen.getByText(/Region map/)).toBeTruthy();
		expect(screen.queryByText('Hardcore')).toBeNull();
		expect(screen.getByRole('button', { name: /Enter world/ })).toBeTruthy();
	});

	it('renders the slot header from a record that only carries the remainder', () => {
		render(CharactersPage, page(roster({ slots: { remaining: 4 } })));

		expect(screen.getByText(/character slots used/)).toBeTruthy();
	});

	it('renders a memorial entry that has no class or level on record', () => {
		render(
			CharactersPage,
			page(
				roster({
					characters: [],
					memorial: [
						{
							characterId: 'char-9',
							name: 'Brenn',
							diedAt: '2026-01-15T00:00:00.000Z'
						}
					]
				})
			)
		);

		expect(screen.getByText('Brenn')).toBeTruthy();
		expect(screen.getByText(/Fell/)).toBeTruthy();
	});
});
