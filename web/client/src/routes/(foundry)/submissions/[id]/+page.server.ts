import { fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { Roles, toGameIdentity } from '$lib/server/access';
import {
	createSubmission,
	getSubmission,
	reviewSubmission,
	voteSubmission
} from '$lib/server/foundry-api';
import { stringField } from '$lib/server/form';

export const load: PageServerLoad = async ({ locals, params, parent }) => {
	const { access } = await parent();
	const canReview = access?.roles.includes(Roles.Curator) ?? false;

	if (params.id === 'new') {
		return { mode: 'create' as const, submission: null, error: null, canReview };
	}

	const result = await getSubmission(toGameIdentity(locals.user!), params.id);
	return {
		mode: 'view' as const,
		submission: result.data,
		error: result.error,
		canReview
	};
};

export const actions: Actions = {
	create: async ({ request, locals }) => {
		const form = await request.formData();
		const contentType = stringField(form, 'contentType');
		const title = stringField(form, 'title');
		const payload = stringField(form, 'payload');

		if (!contentType || !title || !payload) {
			return fail(400, { error: 'Content type, title, and payload are required.' });
		}

		const result = await createSubmission(toGameIdentity(locals.user!), {
			contentType,
			title,
			payload,
			description: stringField(form, 'description') || null
		});

		if (result.error || !result.data) {
			return fail(400, { error: result.error ?? 'Could not create the submission.' });
		}

		redirect(303, `/submissions/${result.data.id}`);
	},

	vote: async ({ request, locals, params }) => {
		const form = await request.formData();
		const value = Number(stringField(form, 'value'));
		if (value !== 1 && value !== -1) {
			return fail(400, { error: 'Vote must be +1 or -1.' });
		}

		const result = await voteSubmission(toGameIdentity(locals.user!), params.id, value);
		if (result.error) {
			return fail(400, { error: result.error });
		}

		redirect(303, `/submissions/${params.id}`);
	},

	review: async ({ request, locals, params }) => {
		const form = await request.formData();
		const approved = stringField(form, 'approved') === 'yes';
		const notes = stringField(form, 'notes') || null;

		const result = await reviewSubmission(toGameIdentity(locals.user!), params.id, approved, notes);
		if (result.error) {
			return fail(400, { error: result.error });
		}

		redirect(303, `/submissions/${params.id}`);
	}
};
