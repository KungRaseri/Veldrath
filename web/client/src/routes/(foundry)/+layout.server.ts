import type { LayoutServerLoad } from './$types';
import { hasPermission, Permissions, requireUser } from '#lib/server/access';

/**
 * Foundry pages require an authenticated account; feature permissions are enforced per page.
 *
 * The sub-navigation has to know what the account can reach, so the three content capabilities are
 * derived here from the root layout's RBAC snapshot rather than loading the profile again per page.
 */
export const load: LayoutServerLoad = async ({ locals, parent }) => {
	const user = requireUser(locals);
	const { access } = await parent();

	return {
		user,
		canViewContent: hasPermission(access, Permissions.ViewContent),
		canManageContent: hasPermission(access, Permissions.ManageContent),
		canReviewSubmissions: hasPermission(access, Permissions.ReviewSubmissions)
	};
};
