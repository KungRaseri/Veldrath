import { describe, expect, it } from 'vitest';
import { foundryNavItems, isCurrentNavItem, type FoundryNavFlags } from './nav';

const reader: FoundryNavFlags = {
	canViewContent: true,
	canManageContent: false,
	canReviewSubmissions: false
};

const author: FoundryNavFlags = {
	canViewContent: true,
	canManageContent: true,
	canReviewSubmissions: false
};

const guest: FoundryNavFlags = {
	canViewContent: false,
	canManageContent: false,
	canReviewSubmissions: false
};

describe('Foundry navigation', () => {
	it('never links a /foundry path, because the route group is not a URL segment', () => {
		const hrefs = [
			...foundryNavItems(reader),
			...foundryNavItems(author),
			...foundryNavItems(guest)
		].map((item) => item.href);

		expect(hrefs.some((href) => href.startsWith('/foundry'))).toBe(false);
	});

	it('gives a reader the maps, without the write or review entries', () => {
		expect(foundryNavItems(reader).map((item) => item.href)).toEqual([
			'/content',
			'/maps',
			'/languages',
			'/notifications'
		]);
	});

	it('hides maps from an account that cannot read content', () => {
		expect(foundryNavItems(guest).map((item) => item.href)).toEqual([
			'/content',
			'/languages',
			'/notifications'
		]);
	});

	it('offers the submission queue to an author and to a reviewer', () => {
		expect(foundryNavItems(author).map((item) => item.href)).toContain('/submissions');
		expect(
			foundryNavItems({ ...reader, canReviewSubmissions: true }).map((item) => item.href)
		).toContain('/submissions');
	});

	it('marks the exact section and its children, and nothing that merely shares a prefix', () => {
		expect(isCurrentNavItem('/maps', '/maps')).toBe(true);
		expect(isCurrentNavItem('/maps', '/maps/zone:fenwick')).toBe(true);
		expect(isCurrentNavItem('/maps', '/mapsomething')).toBe(false);
		expect(isCurrentNavItem('/content', '/maps/zone:fenwick')).toBe(false);
	});
});
