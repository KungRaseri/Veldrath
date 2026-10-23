import { Permissions, requirePermission, requireUser } from '$lib/server/access';

/** Moderation routes require the `players.view` permission. */
export const load = async ({ locals, parent }) => {
	requireUser(locals);
	const { access } = await parent();
	requirePermission(
		access,
		Permissions.ViewPlayers,
		'Moderation access requires the players.view permission.'
	);
	return { access };
};
