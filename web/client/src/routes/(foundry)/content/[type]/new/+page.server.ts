import { error, fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { hasPermission, Permissions, requireUser, toGameIdentity } from '#lib/server/access';
import { createContent, getContentTypeSchema } from '#lib/server/content-api';
import { referenceOptions } from '#lib/server/content-refs';
import {
	allFields,
	buildPayload,
	isScalarKind,
	missingRequired
} from '#lib/foundry/content-editor';

/**
 * Creates one entity of any content type.
 *
 * The form renders the type's own schema, so a new type needs no new page. Collections are not authored
 * here: an entity is created with its scalars and the author continues in the full editor, which is one link
 * away and remembers the entity. That keeps this route a plain form — no client-side state, no hidden
 * payload — while still being a create path for every registered type.
 *
 * The page loads for any signed-in account and tells a reader that authoring needs the editor permission;
 * the write itself is refused by the API, which is where the decision belongs.
 */
export const load: PageServerLoad = async ({ parent, params }) => {
	const { access } = await parent();

	const schema = await getContentTypeSchema(params.type);
	if (!schema.data) {
		error(404, schema.error ?? `No schema is registered for '${params.type}'.`);
	}

	return {
		contentType: params.type,
		schema: schema.data,
		canEdit: hasPermission(access, Permissions.ManageContent),
		refOptions: await referenceOptions(allFields(schema.data))
	};
};

export const actions: Actions = {
	default: async ({ locals, params, request }) => {
		const user = requireUser(locals);
		const identity = toGameIdentity(user);

		const schema = await getContentTypeSchema(params.type);
		if (!schema.data) {
			return fail(404, { message: `No schema is registered for '${params.type}'.` });
		}

		const form = await request.formData();
		const values: Record<string, string> = {};
		for (const field of allFields(schema.data)) {
			if (!isScalarKind(field)) continue;
			const raw = form.get(field.name);
			if (raw !== null) values[field.name] = String(raw);
		}

		// Only the fields this form renders are required here: a collection starts empty and is filled in the
		// editor, so demanding one now would make every collection-bearing type uncreatable.
		const scalarOnly = {
			...schema.data,
			groups: [{ label: 'Required', fields: allFields(schema.data).filter(isScalarKind) }]
		};
		const missing = missingRequired(scalarOnly, values);
		if (missing.length > 0) {
			return fail(400, { message: `Fill in: ${missing.join(', ')}.` });
		}

		const result = await createContent(
			identity,
			params.type,
			buildPayload({}, schema.data, values)
		);
		if (result.error || !result.data) {
			return fail(result.status || 502, {
				message: result.error ?? 'Could not create the entry.'
			});
		}

		redirect(303, `/content/${params.type}`);
	}
};
