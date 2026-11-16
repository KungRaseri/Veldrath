import type { LayoutServerLoad } from './$types';
import { hasPermission, Permissions, requireUser, Roles, toGameIdentity } from '#lib/server/access';
import { getUnreadNotificationCount } from '#lib/server/foundry-api';

/**
 * Foundry pages require an authenticated account; feature permissions are enforced per page.
 *
 * The shell has to know what the account can reach, so every capability is derived here from the root
 * layout's RBAC snapshot rather than loading the profile again per page.
 */
export const load: LayoutServerLoad = async ({ locals, parent }) => {
	const user = requireUser(locals);
	const { access } = await parent();

	// A failed count must not take the whole group down — a missing badge is a far smaller problem than a
	// portal that does not render.
	const unread = await getUnreadNotificationCount(toGameIdentity(user));

	return {
		user,
		canViewContent: hasPermission(access, Permissions.ViewContent),
		canManageContent: hasPermission(access, Permissions.ManageContent),
		// The decision endpoints are role-gated (`RequireAuthorization("Curator")`), so the navigation has to
		// be too: a hand-granted `content.review` permission without the role would render a Review entry
		// that answers 403 on every click. Reconciling the role-only endpoints with the permission model is
		// milestone H5 work, not something the sidebar should paper over.
		canReview: access?.roles.includes(Roles.Curator) ?? false,
		unreadNotifications: unread.data?.count ?? 0
	};
};
