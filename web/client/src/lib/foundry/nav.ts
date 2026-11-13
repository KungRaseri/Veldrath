/**
 * The Foundry sub-navigation, as data.
 *
 * The `(foundry)` route group is a layout grouping, not a URL segment: the pages live at `/content`,
 * `/maps`, `/submissions`, `/languages` and `/notifications`. Linking `/foundry/...` is a 404, which is
 * the bug this module exists to make impossible to repeat — the href is asserted by a unit test rather
 * than typed into five components.
 */

/** Which Foundry capabilities the signed-in account holds, derived once by the group layout. */
export interface FoundryNavFlags {
	canViewContent: boolean;
	canManageContent: boolean;
	canReviewSubmissions: boolean;
}

/** One entry in the Foundry sub-navigation. */
export interface FoundryNavItem {
	/** Route the SvelteKit group resolves to — never a `/foundry/...` path. */
	href: string;
	/** Human-readable section name. */
	label: string;
}

/**
 * Builds the navigation for an account.
 *
 * Content is always reachable; the map graphs need `content.view` because they carry hidden nodes and
 * unlock keys; the submission queue needs either authoring or review; languages and notifications are
 * open to any signed-in Foundry user.
 *
 * @param flags Capabilities derived by `(foundry)/+layout.server.ts`.
 * @returns The ordered navigation entries.
 */
export function foundryNavItems(flags: FoundryNavFlags): FoundryNavItem[] {
	const items: FoundryNavItem[] = [{ href: '/content', label: 'Content' }];

	if (flags.canViewContent) {
		items.push({ href: '/maps', label: 'Maps' });
	}

	if (flags.canManageContent || flags.canReviewSubmissions) {
		items.push({ href: '/submissions', label: 'Submissions' });
	}

	items.push(
		{ href: '/languages', label: 'Languages' },
		{ href: '/notifications', label: 'Notifications' }
	);

	return items;
}

/**
 * Whether `href` is the section the current path belongs to, so the nav can mark it.
 *
 * `startsWith` on the href plus a separator, so `/maps` marks itself on `/maps/zone:fenwick` without
 * `/content` also matching `/contentious`.
 *
 * @param href Navigation entry href.
 * @param pathname Current `page.url.pathname`.
 * @returns `true` when the entry is the current section.
 */
export function isCurrentNavItem(href: string, pathname: string): boolean {
	return pathname === href || pathname.startsWith(`${href}/`);
}
