import type { PageServerLoad } from './$types';
import { Permissions, requirePermission } from '#lib/server/access';
import { listContentTypes } from '#lib/server/content-api';

export const load: PageServerLoad = async ({ parent }) => {
	const { access } = await parent();

	// The catalog is `content.view`, the same gate as the portal home and the map graphs. A signed-in
	// player has no catalog to read; their entry point is the contribution form.
	requirePermission(
		access,
		Permissions.ViewContent,
		'Foundry needs the content permission. Ask a Curator for access.'
	);

	const result = await listContentTypes();
	return { types: result.data ?? [], error: result.error };
};
