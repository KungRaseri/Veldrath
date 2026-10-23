import { hasPermission, loadAccountAccess, Permissions } from '$lib/server/access';

export const load = async ({ locals }) => {
	const user = locals.user ?? null;
	const access = user ? await loadAccountAccess(user) : null;

	return {
		user,
		access,
		staff: {
			foundry: Boolean(user),
			admin: hasPermission(access, Permissions.ManageUsers),
			mod: hasPermission(access, Permissions.ViewPlayers),
			editorial: hasPermission(access, Permissions.ManageContent)
		}
	};
};
