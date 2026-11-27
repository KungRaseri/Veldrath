import { fail } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import {
	Permissions,
	requireActionPermission,
	requirePermission,
	toGameIdentity
} from '#lib/server/access';
import { broadcastAnnouncement } from '#lib/server/admin-api';
import { stringField } from '#lib/server/form';

export const load: PageServerLoad = async ({ parent }) => {
	// Await the group layout first: it admits both halves of the staff split, and this page then owns
	// the permission its own form needs. The page makes no API call of its own, so this is the only
	// guard between an operator and a form whose submit would be refused.
	const { access } = await parent();
	requirePermission(
		access,
		Permissions.SendAnnouncements,
		'Broadcasting an announcement requires the admin.announce permission.'
	);

	return {};
};

export const actions: Actions = {
	broadcast: async ({ request, locals }) => {
		await requireActionPermission(
			locals,
			Permissions.SendAnnouncements,
			'Broadcasting an announcement requires the admin.announce permission.'
		);

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
