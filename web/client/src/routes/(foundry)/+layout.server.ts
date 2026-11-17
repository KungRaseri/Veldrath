import type { LayoutServerLoad } from './$types';
import { hasPermission, Permissions, requireUser, toGameIdentity } from '#lib/server/access';
import { getUnreadNotificationCount } from '#lib/server/foundry-api';

/**
 * Foundry pages require an authenticated account and nothing more.
 *
 * Reading is what makes the portal public: any signed-in account can browse the catalog, the map graphs and
 * the schema registry, and can submit its own work. Authoring needs `content.manage`; a decision on a
 * submission or a map needs `content.review`. The content audit is the one maintainer tool that stays behind
 * `content.view`, so the shell exposes it as a separate capability rather than as "can read".
 *
 * Anonymous visitors are sent to the login page with this URL as the destination, so a shared link still
 * works after signing in.
 */
export const load: LayoutServerLoad = async ({ locals, url, parent }) => {
	const user = requireUser(locals, url.pathname + url.search);
	const { access } = await parent();

	// A failed count must not take the whole group down — a missing badge is a far smaller problem than a
	// portal that does not render.
	const unread = await getUnreadNotificationCount(toGameIdentity(user));

	return {
		user,
		canManageContent: hasPermission(access, Permissions.ManageContent),
		// The decision endpoints require the permission claim rather than the role, so the navigation follows
		// the permission: an Admin or a hand-granted reviewer decides exactly like a Curator does.
		canReview: hasPermission(access, Permissions.ReviewSubmissions),
		canAudit: hasPermission(access, Permissions.ViewContent),
		unreadNotifications: unread.data?.count ?? 0
	};
};
