import { error, fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { toGameIdentity } from '$lib/server/access';
import {
	getModUser,
	kickPlayer,
	mutePlayer,
	unmutePlayer,
	warnPlayer
} from '$lib/server/admin-api';
import { numberField, stringField } from '$lib/server/form';

export const load: PageServerLoad = async ({ locals, params }) => {
	const result = await getModUser(toGameIdentity(locals.user!), params.id);
	if (!result.data) {
		error(404, result.error ?? 'Account not found.');
	}

	return { player: result.data };
};

function back(params: { id: string }) {
	redirect(303, `/mod/users/${params.id}`);
}

export const actions: Actions = {
	kick: async ({ request, locals, params }) => {
		const reason = stringField(await request.formData(), 'reason');
		const result = await kickPlayer(toGameIdentity(locals.user!), params.id, reason || null);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	},

	warn: async ({ request, locals, params }) => {
		const reason = stringField(await request.formData(), 'reason');
		if (!reason) return fail(400, { error: 'A reason is required.' });
		const result = await warnPlayer(toGameIdentity(locals.user!), params.id, reason);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	},

	mute: async ({ request, locals, params }) => {
		const form = await request.formData();
		const result = await mutePlayer(
			toGameIdentity(locals.user!),
			params.id,
			stringField(form, 'reason') || null,
			numberField(form, 'durationMinutes')
		);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	},

	unmute: async ({ locals, params }) => {
		const result = await unmutePlayer(toGameIdentity(locals.user!), params.id);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	}
};
