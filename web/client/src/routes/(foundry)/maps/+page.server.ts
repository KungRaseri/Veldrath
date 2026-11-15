import { error, fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { requireUser, toGameIdentity } from '#lib/server/access';
import { createMap, listMaps } from '#lib/server/maps-api';

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

export const actions: Actions = {
	/**
	 * Creates an inactive draft and opens it.
	 *
	 * The draft is invisible to the game until a reviewer approves it, so creating one cannot put content in
	 * front of a player.
	 */
	create: async ({ locals, request }) => {
		const user = requireUser(locals);
		const form = await request.formData();

		const result = await createMap(toGameIdentity(user), {
			mapType: String(form.get('mapType') ?? 'Zone'),
			ownerId: String(form.get('ownerId') ?? '').trim(),
			name: String(form.get('name') ?? '').trim()
		});

		if (!result.data) {
			return fail(result.status || 502, {
				message: result.error ?? 'The map could not be created.'
			});
		}

		redirect(303, `/maps/${encodeURIComponent(result.data.id)}`);
	}
};
