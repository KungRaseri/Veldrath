import { error, fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import {
	loadAccountAccess,
	hasPermission,
	Permissions,
	requireUser,
	toGameIdentity
} from '#lib/server/access';
import {
	deactivateContent,
	getContentDetail,
	getContentTypeSchema,
	updateContent,
	validateContentPayload
} from '#lib/server/content-api';
import { buildPayload, type ContentIssue } from '#lib/foundry/content-editor';

/** Loads the entity, its schema, and whether this account may write. */
export const load: PageServerLoad = async ({ locals, params }) => {
	const user = requireUser(locals);
	const access = await loadAccountAccess(user);
	const canEdit = hasPermission(access, Permissions.ManageContent);

	const [detail, schema] = await Promise.all([
		getContentDetail(params.type, params.slug),
		getContentTypeSchema(params.type)
	]);

	if (!detail.data) {
		error(404, detail.error ?? 'Content entry not found.');
	}

	if (!schema.data) {
		error(404, schema.error ?? `No schema is registered for content type '${params.type}'.`);
	}

	return {
		contentType: params.type,
		slug: params.slug,
		canEdit,
		schema: schema.data,
		summary: detail.data.summary,
		payload: detail.data.payload
	};
};

/** Reads the form's field values out of the submitted data. */
function readFieldValues(request: Request, fieldNames: string[]) {
	return request.formData().then((form) => {
		const values: Record<string, string | boolean> = {};
		for (const name of fieldNames) {
			const raw = form.get(name);
			if (raw === null) continue;
			values[name] = raw === 'true' ? true : raw === 'false' ? false : String(raw);
		}
		return values;
	});
}

export const actions: Actions = {
	/** Dry run over the *unsaved* form values, so the author sees issues before committing. */
	validate: async ({ locals, params, request }) => {
		const user = requireUser(locals);
		const identity = toGameIdentity(user);

		const [detail, schema] = await Promise.all([
			getContentDetail(params.type, params.slug),
			getContentTypeSchema(params.type)
		]);

		if (!detail.data || !schema.data) {
			return fail(404, { message: detail.error ?? schema.error ?? 'Content entry not found.' });
		}

		const fieldNames = schema.data.groups.flatMap((group) =>
			group.fields.map((field) => field.name)
		);
		const values = await readFieldValues(request, fieldNames);
		const payload = buildPayload(detail.data.payload, schema.data, values);

		const result = await validateContentPayload(identity, params.type, params.slug, payload);
		if (!result.data) {
			return fail(result.status || 502, { message: result.error ?? 'Validation failed.' });
		}

		return {
			action: 'validate' as const,
			issues: [...result.data.errors, ...result.data.warnings] satisfies ContentIssue[]
		};
	},

	/** Saves the form. Error-level issues block the write and come back field by field. */
	save: async ({ locals, params, request }) => {
		const user = requireUser(locals);
		const identity = toGameIdentity(user);

		const [detail, schema] = await Promise.all([
			getContentDetail(params.type, params.slug),
			getContentTypeSchema(params.type)
		]);

		if (!detail.data || !schema.data) {
			return fail(404, { message: detail.error ?? schema.error ?? 'Content entry not found.' });
		}

		const fieldNames = schema.data.groups.flatMap((group) =>
			group.fields.map((field) => field.name)
		);
		const values = await readFieldValues(request, fieldNames);
		const payload = buildPayload(detail.data.payload, schema.data, values);

		const result = await updateContent(
			identity,
			params.type,
			params.slug,
			payload,
			detail.data.summary.updatedAt
		);

		if (!result.data) {
			return fail(result.status || 502, { message: result.error ?? 'The save failed.' });
		}

		if (!result.data.isValid) {
			return fail(400, {
				action: 'save' as const,
				message: 'This entity has errors and was not saved.',
				issues: [...result.data.errors, ...result.data.warnings] satisfies ContentIssue[]
			});
		}

		return {
			action: 'save' as const,
			saved: true,
			issues: result.data.warnings satisfies ContentIssue[]
		};
	},

	/** Deactivates the entity. There is no hard delete: live game rows reference content by slug. */
	deactivate: async ({ locals, params }) => {
		const user = requireUser(locals);
		const identity = toGameIdentity(user);

		const result = await deactivateContent(identity, params.type, params.slug);
		if (!result.data) {
			return fail(result.status || 502, { message: result.error ?? 'The deactivation failed.' });
		}

		redirect(303, `/content/${params.type}`);
	}
};
