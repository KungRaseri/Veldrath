import type { PageServerLoad } from './$types';
import { Permissions, hasPermission, toGameIdentity } from '#lib/server/access';
import { getServerStatus } from '#lib/server/admin-api';

export const load: PageServerLoad = async ({ parent, locals }) => {
	// The group layout admits either half of the staff split. The counters come from an `admin.users`
	// endpoint, so a role manager gets the link grid and no API call — a page this account may open
	// must not make a call it would be refused.
	const { access } = await parent();

	const can = {
		manageUsers: hasPermission(access, Permissions.ManageUsers),
		manageRoles: hasPermission(access, Permissions.ManageRoles),
		viewPlayers: hasPermission(access, Permissions.ViewPlayers),
		sendAnnouncements: hasPermission(access, Permissions.SendAnnouncements)
	};

	if (!can.manageUsers) {
		return { status: null, error: null, can };
	}

	const result = await getServerStatus(toGameIdentity(locals.user!));
	return { status: result.data, error: result.error, can };
};
