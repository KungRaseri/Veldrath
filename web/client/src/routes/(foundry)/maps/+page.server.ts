import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { requireUser, toGameIdentity } from '#lib/server/access';
import { listMaps } from '#lib/server/maps-api';

/**
 * Lists the authored map graphs.
 *
 * Reads need `content.view` (the graph carries hidden nodes and unlock keys), so a Reviewer can browse
 * and a player cannot; `canEdit` decides whether the write controls render, not whether the page loads.
 */
export const load: PageServerLoad = async ({ locals, parent }) => {
	const user = requireUser(locals);
	const { canViewContent, canManageContent } = await parent();

	if (!canViewContent) {
		error(403, 'Reading the map graphs requires content read access.');
	}

	const result = await listMaps(toGameIdentity(user));
	if (!result.data) {
		error(result.status || 502, result.error ?? 'Could not load the map list.');
	}

	return { canEdit: canManageContent, maps: result.data };
};
