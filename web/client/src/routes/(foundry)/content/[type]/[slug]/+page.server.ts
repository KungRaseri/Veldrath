import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { loadAccountAccess, hasPermission, Permissions, requireUser } from '#lib/server/access';
import { getContentDetail } from '#lib/server/content-api';

export const load: PageServerLoad = async ({ locals, params }) => {
	const user = requireUser(locals);
	const access = await loadAccountAccess(user);

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
