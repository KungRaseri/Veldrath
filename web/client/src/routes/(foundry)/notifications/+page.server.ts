import { fail } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { toGameIdentity } from '#lib/server/access';
import { listNotifications, markNotificationRead } from '#lib/server/foundry-api';
import { stringField } from '#lib/server/form';

export const load: PageServerLoad = async ({ locals }) => {
	const result = await listNotifications(toGameIdentity(locals.user!));
	return { notifications: result.data ?? [], error: result.error };
};

export const actions: Actions = {
	read: async ({ request, locals }) => {
		const form = await request.formData();
		const id = stringField(form, 'id');
		if (!id) return fail(400, { error: 'Notification id is required.' });

		const result = await markNotificationRead(toGameIdentity(locals.user!), id);
		if (result.error) return fail(400, { error: result.error });
		return { ok: true };
	}
};
