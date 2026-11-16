import { fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { Roles, toGameIdentity } from '#lib/server/access';
import {
	createSubmission,
	getSubmission,
	reviewSubmission,
	voteSubmission
} from '#lib/server/foundry-api';
import { getContentDetail, getContentTypeSchema, listContentTypes } from '#lib/server/content-api';
import { stringField } from '#lib/server/form';
import {
	allFields,
	buildPayload,
	isScalarKind,
	missingRequired
} from '#lib/foundry/content-editor';

/** Reads the stored payload as an object, keeping the raw text when it is not valid JSON. */
function parsePayload(raw: string | undefined): Record<string, unknown> {
	if (!raw) return {};

	try {
		const parsed: unknown = JSON.parse(raw);
		return parsed !== null && typeof parsed === 'object' && !Array.isArray(parsed)
			? (parsed as Record<string, unknown>)
			: {};
	} catch {
		return {};
	}
}

export const load: PageServerLoad = async ({ locals, params, parent, url }) => {
	const { access } = await parent();
	const canReview = access?.roles.includes(Roles.Curator) ?? false;

	if (params.id === 'new') {
		// The form renders the chosen type's schema, so the type picker is a GET: changing it reloads the
		// page with different fields instead of juggling every schema in one document.
		const types = await listContentTypes();
		const available = types?.data ?? [];
		const chosen = url.searchParams.get('type') ?? available[0]?.contentType ?? '';
		const schema = chosen ? await getContentTypeSchema(chosen) : null;

		return {
			mode: 'create' as const,
			submission: null,
			error: types.error ?? schema?.error ?? null,
			canReview,
			types: available,
			typeKey: chosen,
			schema: schema?.data ?? null,
			payload: {}
		};
	}

	const result = await getSubmission(toGameIdentity(locals.user!), params.id);
	const payload = parsePayload(result.data?.payload);
	const schema = result.data ? await getContentTypeSchema(result.data.contentType) : null;

	// What the catalog holds at the slug this submission claims, so a reviewer sees the change rather than
	// just the proposal. Only fetched while the decision is still open: after a decision the diff is history.
	const proposedSlug =
		typeof payload.slug === 'string' && payload.slug.trim() !== '' ? payload.slug : null;
	const existing =
		result.data?.status === 'Pending' && proposedSlug
			? await getContentDetail(result.data.contentType.toLowerCase(), proposedSlug)
			: null;

	return {
		mode: 'view' as const,
		submission: result.data,
		error: result.error,
		canReview,
		schema: schema?.data ?? null,
		payload,
		rawPayload: result.data?.payload ?? '',
		proposedSlug,
		existing: existing?.data?.payload ?? null
	};
};

export const actions: Actions = {
	create: async ({ request, locals }) => {
		const form = await request.formData();
		const contentType = stringField(form, 'contentType');
		const title = stringField(form, 'title');

		if (!contentType || !title) {
			return fail(400, { error: 'Content type and title are required.' });
		}

		const schema = await getContentTypeSchema(contentType);
		if (!schema.data) {
			return fail(400, { error: `No schema is registered for '${contentType}'.` });
		}

		// The payload is built from the same schema the form was rendered from, so a submission and a saved
		// entity are the same shape — which is what lets a curator promote one without a per-type mapper.
		const values: Record<string, string> = {};
		for (const field of allFields(schema.data)) {
			if (!isScalarKind(field)) continue;
			const raw = form.get(field.name);
			if (raw !== null) values[field.name] = String(raw);
		}

		const missing = missingRequired(schema.data, values);
		if (missing.length > 0) {
			return fail(400, { error: `Fill in: ${missing.join(', ')}.` });
		}

		const result = await createSubmission(toGameIdentity(locals.user!), {
			contentType,
			title,
			payload: JSON.stringify(buildPayload({}, schema.data, values)),
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
