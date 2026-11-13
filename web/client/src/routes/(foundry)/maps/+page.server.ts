import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import {
	hasPermission,
	loadAccountAccess,
	Permissions,
	requireUser,
	toGameIdentity
} from '#lib/server/access';
import { listMaps } from '#lib/server/maps-api';

/** Lists the authored map graphs for the editor. */
export const load: PageServerLoad = async ({ locals }) => {
	const user = requireUser(locals);
	const access = await loadAccountAccess(user);
	const canEdit = hasPermission(access, Permissions.ManageContent);

	const result = await listMaps(toGameIdentity(user));
	if (!result.data) {
		error(result.status || 502, result.error ?? 'Could not load the map list.');
	}

	return { canEdit, maps: result.data };
};
