import type { LayoutServerLoad } from './$types';
import {
	hasAnyPermission,
	hasPermission,
	loadAccountAccess,
	Permissions
} from '#lib/server/access';

export const load: LayoutServerLoad = async ({ locals }) => {
	const user = locals.user ?? null;
	const access = user ? await loadAccountAccess(user) : null;

	return {
		user,
		access,
		staff: {
			// The Foundry is readable by every signed-in account, so the header entry is about being signed
			// in rather than about holding a content permission.
			portal: Boolean(user),
			// The admin group admits account administration or authority management, so either one has to
			// show the header entry; the dashboard itself then offers the pages that account can open.
			admin: hasAnyPermission(access, [Permissions.ManageUsers, Permissions.ManageRoles]),
			mod: hasPermission(access, Permissions.ViewPlayers),
			editorial: hasPermission(access, Permissions.ManageContent)
		}
	};
};
