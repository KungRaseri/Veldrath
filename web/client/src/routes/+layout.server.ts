import type { LayoutServerLoad } from './$types';
import { hasPermission, loadAccountAccess, Permissions } from '#lib/server/access';

export const load: LayoutServerLoad = async ({ locals }) => {
	const user = locals.user ?? null;
	const access = user ? await loadAccountAccess(user) : null;

	return {
		user,
		access,
		staff: {
			// The portal home and the content pages require `content.view`, so the header entry that leads
			// there does too. Everyone else who is signed in gets the contribution route instead.
			foundry: hasPermission(access, Permissions.ViewContent),
			submit: Boolean(user),
			admin: hasPermission(access, Permissions.ManageUsers),
			mod: hasPermission(access, Permissions.ViewPlayers),
			editorial: hasPermission(access, Permissions.ManageContent)
		}
	};
};
