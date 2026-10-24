import { fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { fetchCharactersForUser } from '#lib/server/game-api';
import { deleteCharacter, fetchCharacterSlots, fetchLastSession } from '#lib/server/character-api';
import { stringField } from '#lib/server/form';

export const load: PageServerLoad = async ({ locals }) => {
	if (!locals.user) {
		redirect(303, '/login');
	}

	const [characters, slots, lastSession] = await Promise.all([
		fetchCharactersForUser(locals.user),
		fetchCharacterSlots(locals.user),
		fetchLastSession(locals.user)
	]);

	return {
		characters: characters.characters,
		loadError: characters.error,
		slots: slots.data,
		slotsError: slots.error,
		lastSession: lastSession.data,
		lastSessionError: lastSession.error,
		emailVerified: locals.user.emailVerified
	};
};

export const actions: Actions = {
	delete: async ({ locals, request }) => {
		if (!locals.user) {
			redirect(303, '/login');
		}

		const id = stringField(await request.formData(), 'id');
		if (!id) {
			return fail(400, { error: 'Character ID is required.' });
		}

		const result = await deleteCharacter(locals.user, id);
		if (result.error) {
			const status = result.status >= 400 && result.status < 500 ? result.status : 400;
			return fail(status, { error: result.error });
		}

		redirect(303, '/characters');
	}
};
