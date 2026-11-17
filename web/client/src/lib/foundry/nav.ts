/**
 * The Foundry navigation, as data.
 *
 * The `(foundry)` route group is a layout grouping, not a URL segment: the pages live at `/content`,
 * `/maps`, `/submissions`, `/new` and `/notifications`. Linking `/foundry/...` is a 404, which is the bug
 * this module exists to make impossible to repeat — the href is asserted by a unit test rather than typed
 * into five components.
 *
 * The one exception is the portal home: `/` belongs to the public site, so the portal's landing page is
 * `/foundry`, and `FOUNDRY_HOME_HREF` is the only place that string is written.
 */

/**
 * Which Foundry capabilities the signed-in account holds, derived once by the group layout.
 *
 * The portal is readable by any signed-in account, so there is no "can read" flag: what is left is what an
 * account may *do*, and the one maintainer surface that stays closed.
 */
export interface FoundryNavFlags {
	/** `content.manage` — authoring directly in the catalog. */
	canManageContent: boolean;
	/** `content.review` — decides submissions and map approvals, the same permission the API checks. */
	canReview: boolean;
	/** `content.view` — the content audit, the only maintainer surface left behind a permission. */
	canAudit: boolean;
	/** Unread notifications, as a badge count. */
	unreadNotifications: number;
	/** Submissions awaiting a decision; omitted when the caller did not count them. */
	pendingReview?: number;
}

/** The jobs the portal is organised around. */
export type FoundryNavGroupName = 'Explore' | 'Contribute' | 'Curate' | 'Maintain';

/** One entry in the Foundry navigation. */
export interface FoundryNavItem {
	/** Route the SvelteKit group resolves to — never a `/foundry/...` path. */
	href: string;
	/** Human-readable section name. */
	label: string;
	/** Badge count, only present when there is something to count. */
	count?: number;
}

/** A named cluster of related entries. */
export interface FoundryNavGroup {
	/** Section heading, rendered as a small caption rather than a link. */
	group: FoundryNavGroupName;
	/** Entries in display order; never empty. */
	items: FoundryNavItem[];
}

/** The whole navigation: one primary action, the home entry, the groups, and the personal entries. */
export interface FoundryNav {
	/** The portal landing page. */
	home: FoundryNavItem;
	/** The single way into creating something. */
	primary: FoundryNavItem;
	/** Populated groups in display order; a group with nothing to show is omitted. */
	groups: FoundryNavGroup[];
	/** Personal entries, rendered at the bottom rather than inside a capability group. */
	account: FoundryNavItem[];
}

/** The portal home. `/` is the public site, so this is the only place the route is spelled. */
export const FOUNDRY_HOME_HREF = '/foundry';

/** The type picker that every create action goes through. */
export const FOUNDRY_NEW_HREF = '/new';

/**
 * Every top-level path the `(foundry)` group answers on.
 *
 * The site header needs this to mark its own "Foundry" entry while the visitor is anywhere in the portal —
 * the group is a layout grouping, so those pages do not share a URL prefix.
 */
export const FOUNDRY_PATH_ROOTS: readonly string[] = [
	FOUNDRY_HOME_HREF,
	FOUNDRY_NEW_HREF,
	'/content',
	'/maps',
	'/submissions',
	'/review',
	'/schemas',
	'/world-health',
	'/notifications'
];

/**
 * Whether a path belongs to the portal.
 *
 * @param pathname Current `page.url.pathname`.
 * @returns `true` for the portal home, the type picker, or any section inside the group.
 */
export function isFoundryPath(pathname: string): boolean {
	return FOUNDRY_PATH_ROOTS.some((root) => isCurrentNavItem(root, pathname));
}

/**
 * Builds the navigation for an account.
 *
 * Reading the catalog, the maps and the schema registry is open to every signed-in account, so those entries
 * are unconditional. Creating collapses into one primary action: a picker decides between writing straight
 * into the catalog (an editor) and submitting for review (everybody), which is what keeps the portal from
 * having two doors that do almost the same thing under contradictory names.
 *
 * The review queue needs `content.review`, and World health needs `content.view`; both are omitted rather
 * than shown disabled, because a nav entry that answers 403 is worse than an entry that is not there.
 *
 * @param flags Capabilities derived by `(foundry)/+layout.server.ts`.
 * @returns The home entry, the primary action, the ordered groups and the personal entries.
 */
export function foundryNav(flags: FoundryNavFlags): FoundryNav {
	const groups: FoundryNavGroup[] = [
		{
			group: 'Explore',
			items: [
				{ href: '/content', label: 'Content' },
				{ href: '/maps', label: 'Maps' },
				{ href: '/schemas', label: 'Schemas' }
			]
		},
		{
			group: 'Contribute',
			items: [{ href: '/submissions', label: 'My submissions' }]
		}
	];

	if (flags.canReview) {
		groups.push({
			group: 'Curate',
			items: [withCount({ href: '/review', label: 'Review queue' }, flags.pendingReview)]
		});
	}

	if (flags.canAudit) {
		groups.push({
			group: 'Maintain',
			items: [{ href: '/world-health', label: 'World health' }]
		});
	}

	return {
		home: { href: FOUNDRY_HOME_HREF, label: 'Home' },
		primary: { href: FOUNDRY_NEW_HREF, label: 'New content' },
		groups,
		account: [
			withCount({ href: '/notifications', label: 'Notifications' }, flags.unreadNotifications)
		]
	};
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

/**
 * Attaches a badge, but only when there is something to report — a `0` next to Notifications is noise,
 * and an absent `count` is what tells the component not to render the badge at all.
 */
function withCount(item: FoundryNavItem, count: number | undefined): FoundryNavItem {
	return count !== undefined && count > 0 ? { ...item, count } : item;
}
