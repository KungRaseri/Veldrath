import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { requireUser, toGameIdentity } from '#lib/server/access';
import { listMaps } from '#lib/server/maps-api';

/**
 * Lists the maps the caller may see.
 *
 * Reading needs a signed-in account and nothing more. The list is already scoped by the server: live maps plus
 * the drafts this account created, or everything for an editor or a reviewer. Creating a map is its own page
 * (`/maps/new`) — the list used to carry the form, which made it the second door into the same job.
 */
export const load: PageServerLoad = async ({ locals }) => {
	const user = requireUser(locals);

	const result = await listMaps(toGameIdentity(user));
	if (!result.data) {
		error(result.status || 502, result.error ?? 'Could not load the map list.');
	}

	return { maps: result.data };
};
