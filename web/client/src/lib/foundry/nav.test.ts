import { describe, expect, it } from 'vitest';
import {
	FOUNDRY_HOME_HREF,
	FOUNDRY_NEW_HREF,
	foundryNav,
	isCurrentNavItem,
	isFoundryPath,
	type FoundryNavFlags
} from './nav';

/** A player: signed in, reads the portal, authors nothing and audits nothing. */
const reader: FoundryNavFlags = {
	canManageContent: false,
	canReview: false,
	canAudit: false,
	unreadNotifications: 0
};

/** A content editor: writes straight into the catalog and can read the audit. */
const author: FoundryNavFlags = {
	canManageContent: true,
	canReview: false,
	canAudit: true,
	unreadNotifications: 0
};

/** A curator: decides, does not write, and holds the audit through `content.view`. */
const curator: FoundryNavFlags = {
	canManageContent: false,
	canReview: true,
	canAudit: true,
	unreadNotifications: 3,
	pendingReview: 4
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
		expect(FOUNDRY_NEW_HREF).toBe('/new');

		// `/foundry` is the deliberate exception: `/` belongs to the public site, so the portal needs a home
		// of its own. The sections themselves must never be spelled through the group — `/foundry/maps` is
		// a 404, which is the bug this module exists to prevent.
		expect(hrefs(reader).some((href) => href.startsWith('/foundry'))).toBe(false);
	});

	it('gives every signed-in account the reading sections and a way to contribute', () => {
		expect(groupNames(reader)).toEqual(['Explore', 'Contribute']);
		expect(hrefs(reader)).toEqual(['/content', '/maps', '/schemas', '/submissions']);
	});

	it('collapses creation into one primary action, for an author and a player alike', () => {
		// Before this, an editor pressed New inside a content type and everybody else had to know the
		// submission form's URL. One entry, one place to change.
		expect(foundryNav(reader).primary).toEqual({ href: '/new', label: 'New content' });
		expect(foundryNav(author).primary).toEqual({ href: '/new', label: 'New content' });
	});

	it('adds the review queue only for an account that may decide', () => {
		expect(groupNames(author)).toEqual(['Explore', 'Contribute', 'Maintain']);
		expect(hrefs(author)).toContain('/world-health');
		expect(hrefs(author)).not.toContain('/review');
	});

	it('puts review in its own group for a curator, and badges the queue', () => {
		const nav = foundryNav(curator);
		const curate = nav.groups.find((group) => group.group === 'Curate');

		expect(groupNames(curator)).toEqual(['Explore', 'Contribute', 'Curate', 'Maintain']);
		expect(curate?.items).toEqual([{ href: '/review', label: 'Review queue', count: 4 }]);
	});

	it('keeps the inbox out of the capability groups, because it is about the account', () => {
		expect(foundryNav(curator).account).toEqual([
			{ href: '/notifications', label: 'Notifications', count: 3 }
		]);
		expect(hrefs(curator)).not.toContain('/notifications');
	});

	it('renders no badge for a count of zero, so an empty inbox is quiet', () => {
		expect(foundryNav(reader).account[0].count).toBeUndefined();
	});

	it('marks the exact section and its children, and nothing that merely shares a prefix', () => {
		expect(isCurrentNavItem('/maps', '/maps')).toBe(true);
		expect(isCurrentNavItem('/maps', '/maps/zone:fenwick')).toBe(true);
		expect(isCurrentNavItem('/maps', '/mapsomething')).toBe(false);
		expect(isCurrentNavItem('/content', '/maps/zone:fenwick')).toBe(false);
		expect(isCurrentNavItem('/foundry', '/foundry')).toBe(true);
		expect(isCurrentNavItem('/foundry', '/foundryish')).toBe(false);
	});

	it('recognises every portal path from the site header, and nothing outside it', () => {
		// The group is a layout grouping, so the header cannot use a single URL prefix to know it is inside
		// the portal.
		expect(isFoundryPath('/foundry')).toBe(true);
		expect(isFoundryPath('/new')).toBe(true);
		expect(isFoundryPath('/content/item')).toBe(true);
		expect(isFoundryPath('/maps/zone:fenwick')).toBe(true);
		expect(isFoundryPath('/submissions/new')).toBe(true);
		expect(isFoundryPath('/review')).toBe(true);
		expect(isFoundryPath('/schemas')).toBe(true);
		expect(isFoundryPath('/world-health')).toBe(true);
		expect(isFoundryPath('/notifications')).toBe(true);
		expect(isFoundryPath('/lore')).toBe(false);
		expect(isFoundryPath('/contentious')).toBe(false);
	});
});
