/**
 * The Foundry navigation, as data.
 *
 * The `(foundry)` route group is a layout grouping, not a URL segment: the pages live at `/content`,
 * `/maps`, `/submissions`, `/languages` and `/notifications`. Linking `/foundry/...` is a 404, which is
 * the bug this module exists to make impossible to repeat — the href is asserted by a unit test rather
 * than typed into five components.
 *
 * The one exception is the portal home: `/` belongs to the public site, so the portal's landing page is
 * `/foundry`, and `FOUNDRY_HOME_HREF` is the only place that string is written.
 */

/** Which Foundry capabilities the signed-in account holds, derived once by the group layout. */
export interface FoundryNavFlags {
	/** `content.view` — reads the catalog and the map graphs, including hidden nodes and unlock keys. */
	canViewContent: boolean;
	/** `content.manage` — authoring. */
	canManageContent: boolean;
	/** The `Curator` role, which is what the decision endpoints actually require. */
	canReview: boolean;
	/** Unread notifications, as a badge count. */
	unreadNotifications: number;
	/** Maps and submissions awaiting a decision; omitted when the caller did not count them. */
	pendingReview?: number;
}

/** The three jobs the portal is organised around. */
export type FoundryNavGroupName = 'Author' | 'Curate' | 'Reference';

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

/** The whole navigation: the home entry stands alone above the groups. */
export interface FoundryNav {
	/** The portal landing page. */
	home: FoundryNavItem;
	/** Populated groups in display order; a group with nothing to show is omitted. */
	groups: FoundryNavGroup[];
}

/** The portal home. `/` is the public site, so this is the only place the route is spelled. */
export const FOUNDRY_HOME_HREF = '/foundry';

/**
 * Builds the navigation for an account.
 *
 * Content is always reachable. Maps need `content.view` because the graphs carry hidden nodes and unlock
 * keys. The review queue needs the Curator role, which is what the decision endpoints check. The
 * submission queue needs either authoring or review, and notifications are open to any signed-in account.
 *
 * Empty groups are dropped rather than rendered with a heading and no links.
 *
 * @param flags Capabilities derived by `(foundry)/+layout.server.ts`.
 * @returns The home entry and the ordered groups.
 */
export function foundryNav(flags: FoundryNavFlags): FoundryNav {
	const author: FoundryNavItem[] = [{ href: '/content', label: 'Content' }];

	if (flags.canViewContent) {
		author.push({ href: '/maps', label: 'Maps' });
	}

	const curate: FoundryNavItem[] = [];

	if (flags.canReview) {
		curate.push(withCount({ href: '/review', label: 'Review' }, flags.pendingReview));
	}

	if (flags.canManageContent || flags.canReview) {
		curate.push({ href: '/submissions', label: 'Submissions' });
	}

	const groups: FoundryNavGroup[] = [
		{ group: 'Author', items: author },
		// Schemas and world health join this group in H6, once those routes exist.
		{
			group: 'Reference',
			items: [
				withCount({ href: '/notifications', label: 'Notifications' }, flags.unreadNotifications)
			]
		}
	];

	if (curate.length > 0) {
		groups.splice(1, 0, { group: 'Curate', items: curate });
	}

	return { home: { href: FOUNDRY_HOME_HREF, label: 'Home' }, groups };
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
