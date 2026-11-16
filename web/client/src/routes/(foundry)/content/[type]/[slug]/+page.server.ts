import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { hasPermission, Permissions, requirePermission } from '#lib/server/access';
import { getContentDetail } from '#lib/server/content-api';

export const load: PageServerLoad = async ({ parent, params }) => {
	// The capability snapshot is already loaded by the root layout — reading it again per page was a second
	// round trip to the same endpoint for the same answer.
	const { access } = await parent();
	requirePermission(
		access,
		Permissions.ViewContent,
		'Foundry needs the content permission. Ask a Curator for access.'
	);

	const result = await getContentDetail(params.type, params.slug);
	if (!result.data) {
		error(404, result.error ?? 'Content entry not found.');
	}

	return {
		contentType: params.type,
		slug: params.slug,
		canEdit: hasPermission(access, Permissions.ManageContent),
		summary: result.data.summary,
		payload: result.data.payload
	};
};
