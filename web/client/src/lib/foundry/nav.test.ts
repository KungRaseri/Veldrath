import { describe, expect, it } from 'vitest';
import { FOUNDRY_HOME_HREF, foundryNav, isCurrentNavItem, type FoundryNavFlags } from './nav';

const reader: FoundryNavFlags = {
	canViewContent: true,
	canManageContent: false,
	canReview: false,
	unreadNotifications: 0
};

const author: FoundryNavFlags = {
	canViewContent: true,
	canManageContent: true,
	canReview: false,
	unreadNotifications: 0
};

const curator: FoundryNavFlags = {
	canViewContent: true,
	canManageContent: false,
	canReview: true,
	unreadNotifications: 3,
	pendingReview: 4
};

const guest: FoundryNavFlags = {
	canViewContent: false,
	canManageContent: false,
	canReview: false,
	unreadNotifications: 0
};

/** Every section href, flattened, so a test can talk about the whole navigation at once. */
function hrefs(flags: FoundryNavFlags): string[] {
	return foundryNav(flags).groups.flatMap((group) => group.items.map((item) => item.href));
}

function groupNames(flags: FoundryNavFlags): string[] {
	return foundryNav(flags).groups.map((group) => group.group);
}

describe('Foundry navigation', () => {
	it('links the portal home at /foundry, and no section through the route group', () => {
		expect(FOUNDRY_HOME_HREF).toBe('/foundry');

		// `/foundry` is the deliberate exception: `/` belongs to the public site, so the portal needs a home
		// of its own. The sections themselves must never be spelled through the group — `/foundry/maps` is
		// a 404, which is the bug this module exists to prevent.
		expect(hrefs(reader).some((href) => href.startsWith('/foundry'))).toBe(false);
	});

	it('groups the reader under Author and Reference, with no Curate group at all', () => {
		const nav = foundryNav(reader);

		expect(nav.home).toEqual({ href: '/foundry', label: 'Home' });
		expect(groupNames(reader)).toEqual(['Author', 'Reference']);
		expect(hrefs(reader)).toEqual([
			'/content',
			'/maps',
			'/schemas',
			'/world-health',
			'/notifications'
		]);
	});

	it('hides maps and the catalog references from an account that cannot read content', () => {
		expect(hrefs(guest)).toEqual(['/content', '/notifications']);
	});

	it('offers an author the submission queue but not the review queue', () => {
		const nav = foundryNav(author);

		expect(groupNames(author)).toEqual(['Author', 'Curate', 'Reference']);
		expect(hrefs(author)).toEqual([
			'/content',
			'/maps',
			'/submissions',
			'/schemas',
			'/world-health',
			'/notifications'
		]);
		expect(nav.groups[1].items.map((item) => item.href)).not.toContain('/review');
	});

	it('puts review first for a curator, and badges the two queues that carry a count', () => {
		const nav = foundryNav(curator);
		const curate = nav.groups.find((group) => group.group === 'Curate');
		const reference = nav.groups.find((group) => group.group === 'Reference');

		expect(curate?.items.map((item) => item.href)).toEqual(['/review', '/submissions']);
		expect(curate?.items[0].count).toBe(4);
		// Reference now leads with the two catalog pages, so find the inbox rather than assume a position.
		expect(reference?.items.find((item) => item.href === '/notifications')).toMatchObject({
			count: 3
		});
	});

	it('renders no badge for a count of zero, so an empty inbox is quiet', () => {
		const reference = foundryNav(reader).groups.find((group) => group.group === 'Reference');

		expect(reference?.items[0].count).toBeUndefined();
	});

	it('marks the exact section and its children, and nothing that merely shares a prefix', () => {
		expect(isCurrentNavItem('/maps', '/maps')).toBe(true);
		expect(isCurrentNavItem('/maps', '/maps/zone:fenwick')).toBe(true);
		expect(isCurrentNavItem('/maps', '/mapsomething')).toBe(false);
		expect(isCurrentNavItem('/content', '/maps/zone:fenwick')).toBe(false);
		expect(isCurrentNavItem('/foundry', '/foundry')).toBe(true);
		expect(isCurrentNavItem('/foundry', '/foundryish')).toBe(false);
	});
});
