import type { LayoutServerLoad } from './$types';
import { hasPermission, loadAccountAccess, Permissions } from '#lib/server/access';

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
			admin: hasPermission(access, Permissions.ManageUsers),
			mod: hasPermission(access, Permissions.ViewPlayers),
			editorial: hasPermission(access, Permissions.ManageContent)
		}
	};
};
