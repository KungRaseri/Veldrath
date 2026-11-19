import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { hasPermission, Permissions } from '#lib/server/access';
import { getContentDetail, getContentTypeSchema } from '#lib/server/content-api';

/** Renders one entry. Reading is public to signed-in accounts; `canEdit` decides on the edit link. */
export const load: PageServerLoad = async ({ parent, params }) => {
	// The capability snapshot is already loaded by the root layout — reading it again per page was a second
	// round trip to the same endpoint for the same answer.
	const { access } = await parent();

	const [result, schema] = await Promise.all([
		getContentDetail(params.type, params.slug),
		// The read view renders fields, not JSON, so it needs the same schema the editor uses. A missing
		// schema is not fatal: the renderer falls back to listing every payload key as an "other" field.
		getContentTypeSchema(params.type)
	]);

	if (!result.data) {
		error(404, result.error ?? 'Content entry not found.');
	}

	return {
		contentType: params.type,
		slug: params.slug,
		canEdit: hasPermission(access, Permissions.ManageContent),
		summary: result.data.summary,
		payload: result.data.payload,
		schema: schema.data ?? null
	};
};
