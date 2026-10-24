import { redirect } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { requireUser } from '$lib/server/access';
import { fetchCharacterSlots, fetchCreationCatalog } from '$lib/server/character-api';

export const load: PageServerLoad = async ({ locals }) => {
	const user = requireUser(locals);

	const [catalog, slots] = await Promise.all([
		fetchCreationCatalog(user),
		fetchCharacterSlots(user)
	]);

	if (slots.data && slots.data.remaining <= 0) {
		redirect(303, '/characters');
	}

	return {
		catalog: catalog.catalog,
		catalogError: catalog.error,
		slots: slots.data,
		slotsError: slots.error
	};
};
