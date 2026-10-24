import { error, fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { toGameIdentity } from '#lib/server/access';
import {
	adminCreatePatchNote,
	adminDeletePatchNote,
	adminGetPatchNote,
	adminPublishPatchNote,
	adminUpdatePatchNote
} from '#lib/server/editorial-api';
import { stringField } from '#lib/server/form';

export const load: PageServerLoad = async ({ locals, params }) => {
	if (params.id === 'new') {
		return { mode: 'create' as const, note: null };
	}

	const result = await adminGetPatchNote(toGameIdentity(locals.user!), params.id);
	if (!result.data) {
		error(404, result.error ?? 'Patch note not found.');
	}

	return { mode: 'edit' as const, note: result.data };
};

export const actions: Actions = {
	save: async ({ request, locals, params }) => {
		const identity = toGameIdentity(locals.user!);
		const form = await request.formData();
		const title = stringField(form, 'title');
		const content = stringField(form, 'content');
		const summary = stringField(form, 'summary');
		const version = stringField(form, 'version');

		if (!title || !content || !summary || !version) {
			return fail(400, { error: 'Title, summary, version, and content are required.' });
		}

		if (params.id === 'new') {
			const slug = stringField(form, 'slug');
			if (!slug) return fail(400, { error: 'Slug is required.' });

			const result = await adminCreatePatchNote(identity, {
				slug,
				title,
				content,
				summary,
				version
			});
			if (!result.data) {
				return fail(400, { error: result.error ?? 'Could not create the patch note.' });
			}

			redirect(303, `/editorial/patch-notes/${result.data.id}`);
		}

		const status = stringField(form, 'status', 'Draft');
		const result = await adminUpdatePatchNote(identity, params.id, {
			title,
			content,
			summary,
			version,
			status
		});

		if (result.error) return fail(400, { error: result.error });
		return { ok: true, message: 'Patch note saved.' };
	},

	publish: async ({ locals, params }) => {
		const result = await adminPublishPatchNote(toGameIdentity(locals.user!), params.id);
		if (result.error) return fail(400, { error: result.error });
		redirect(303, `/editorial/patch-notes/${params.id}`);
	},

	delete: async ({ locals, params }) => {
		const result = await adminDeletePatchNote(toGameIdentity(locals.user!), params.id);
		if (result.error) return fail(400, { error: result.error });
		redirect(303, '/editorial/patch-notes');
	}
};
