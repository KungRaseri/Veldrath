import { fail } from '@sveltejs/kit';
import type { Actions } from './$types';
import { toGameIdentity } from '#lib/server/access';
import { broadcastAnnouncement } from '#lib/server/admin-api';
import { stringField } from '#lib/server/form';

export const actions: Actions = {
	broadcast: async ({ request, locals }) => {
		const form = await request.formData();
		const message = stringField(form, 'message');
		const severity = stringField(form, 'severity', 'info');

		if (!message) {
			return fail(400, { error: 'An announcement message is required.' });
		}

		const result = await broadcastAnnouncement(toGameIdentity(locals.user!), message, severity);
		if (result.error) return fail(400, { error: result.error });
		return { ok: true, message: 'Announcement broadcast to all connected clients.' };
	}
};
