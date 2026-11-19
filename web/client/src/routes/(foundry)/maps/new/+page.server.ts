import { fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { requireUser, toGameIdentity } from '#lib/server/access';
import { MAP_KINDS } from '#lib/foundry/map-editor';
import { createMap, getMapTargets } from '#lib/server/maps-api';

/** Lowercase slugs only: an owner slug becomes the map id, and a slug with capitals is a 404 waiting to happen. */
const SLUG = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

/**
 * The one way to create a map.
 *
 * The list used to carry an inline form that asked for a raw "owner slug" and appeared only for
 * `content.manage`. Any signed-in account may now author a map, so creation gets its own page that explains
 * the three kinds and offers the locations that already exist instead of expecting the author to know one.
 */
export const load: PageServerLoad = async ({ locals }) => {
	const user = requireUser(locals);
	const targets = await getMapTargets(toGameIdentity(user));

	// A missing target list only costs the Interior suggestions; Region and Zone maps still work.
	return {
		locations: targets.data?.locations ?? [],
		error: targets.error
	};
};

export const actions: Actions = {
	default: async ({ locals, request }) => {
		const user = requireUser(locals);
		const form = await request.formData();

		const mapType = String(form.get('mapType') ?? 'Zone');
		const ownerId = String(form.get('ownerId') ?? '')
			.trim()
			.toLowerCase();
		const name = String(form.get('name') ?? '').trim();
		const values = { mapType, ownerId, name };

		if (!MAP_KINDS.some((kind) => kind.value === mapType)) {
			return fail(400, { message: 'Choose what kind of map you are making.', ...values });
		}

		if (!SLUG.test(ownerId)) {
			return fail(400, {
				message:
					'The owner slug must be lowercase letters, digits and single hyphens — like "the-halrow".',
				...values
			});
		}

		if (name.length === 0) {
			return fail(400, { message: 'Give the map a name.', ...values });
		}

		const result = await createMap(toGameIdentity(user), { mapType, ownerId, name });
		if (!result.data) {
			return fail(result.status || 502, {
				message: result.error ?? 'The map could not be created.',
				...values
			});
		}

		redirect(303, `/maps/${encodeURIComponent(result.data.id)}`);
	}
};
