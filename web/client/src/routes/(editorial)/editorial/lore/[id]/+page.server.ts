import { error, fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { toGameIdentity } from '$lib/server/access';
import {
	adminCreateLoreArticle,
	adminDeleteLoreArticle,
	adminGetLoreArticle,
	adminPublishLoreArticle,
	adminUpdateLoreArticle
} from '$lib/server/editorial-api';
import { stringField } from '$lib/server/form';

export const load: PageServerLoad = async ({ locals, params }) => {
	if (params.id === 'new') {
		return { mode: 'create' as const, article: null };
	}

	const result = await adminGetLoreArticle(toGameIdentity(locals.user!), params.id);
	if (!result.data) {
		error(404, result.error ?? 'Lore article not found.');
	}

	return { mode: 'edit' as const, article: result.data };
};

export const actions: Actions = {
	save: async ({ request, locals, params }) => {
		const identity = toGameIdentity(locals.user!);
		const form = await request.formData();
		const title = stringField(form, 'title');
		const content = stringField(form, 'content');
		const summary = stringField(form, 'summary');
		const category = stringField(form, 'category');

		if (!title || !content || !summary) {
			return fail(400, { error: 'Title, summary, and content are required.' });
		}

		if (params.id === 'new') {
			const slug = stringField(form, 'slug');
			if (!slug) return fail(400, { error: 'Slug is required.' });

			const result = await adminCreateLoreArticle(identity, {
				slug,
				title,
				content,
				summary,
				category
			});
			if (!result.data) {
				return fail(400, { error: result.error ?? 'Could not create the article.' });
			}

			redirect(303, `/editorial/lore/${result.data.id}`);
		}

		const status = stringField(form, 'status', 'Draft');
		const result = await adminUpdateLoreArticle(identity, params.id, {
			title,
			content,
			summary,
			category,
			status
		});

		if (result.error) return fail(400, { error: result.error });
		return { ok: true, message: 'Article saved.' };
	},

	publish: async ({ locals, params }) => {
		const result = await adminPublishLoreArticle(toGameIdentity(locals.user!), params.id);
		if (result.error) return fail(400, { error: result.error });
		redirect(303, `/editorial/lore/${params.id}`);
	},

	delete: async ({ locals, params }) => {
		const result = await adminDeleteLoreArticle(toGameIdentity(locals.user!), params.id);
		if (result.error) return fail(400, { error: result.error });
		redirect(303, '/editorial/lore');
	}
};
