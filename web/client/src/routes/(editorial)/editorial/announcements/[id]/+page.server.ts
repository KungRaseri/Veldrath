import { error, fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { toGameIdentity } from '#lib/server/access';
import {
	adminCreateAnnouncement,
	adminDeleteAnnouncement,
	adminGetAnnouncement,
	adminPublishAnnouncement,
	adminUpdateAnnouncement
} from '#lib/server/editorial-api';
import { stringField } from '#lib/server/form';

export const load: PageServerLoad = async ({ locals, params }) => {
	if (params.id === 'new') {
		return { mode: 'create' as const, announcement: null };
	}

	const result = await adminGetAnnouncement(toGameIdentity(locals.user!), params.id);
	if (!result.data) {
		error(404, result.error ?? 'Announcement not found.');
	}

	return { mode: 'edit' as const, announcement: result.data };
};

export const actions: Actions = {
	save: async ({ request, locals, params }) => {
		const identity = toGameIdentity(locals.user!);
		const form = await request.formData();
		const title = stringField(form, 'title');
		const body = stringField(form, 'body');

		if (!title || !body) {
			return fail(400, { error: 'Title and body are required.' });
		}

		if (params.id === 'new') {
			const result = await adminCreateAnnouncement(identity, { title, body });
			if (!result.data) {
				return fail(400, { error: result.error ?? 'Could not create the announcement.' });
			}

			redirect(303, `/editorial/announcements/${result.data.id}`);
		}

		const status = stringField(form, 'status', 'Draft');
		const result = await adminUpdateAnnouncement(identity, params.id, { title, body, status });
		if (result.error) return fail(400, { error: result.error });
		return { ok: true, message: 'Announcement saved.' };
	},

	publish: async ({ locals, params }) => {
		const result = await adminPublishAnnouncement(toGameIdentity(locals.user!), params.id);
		if (result.error) return fail(400, { error: result.error });
		redirect(303, `/editorial/announcements/${params.id}`);
	},

	delete: async ({ locals, params }) => {
		const result = await adminDeleteAnnouncement(toGameIdentity(locals.user!), params.id);
		if (result.error) return fail(400, { error: result.error });
		redirect(303, '/editorial/announcements');
	}
};
