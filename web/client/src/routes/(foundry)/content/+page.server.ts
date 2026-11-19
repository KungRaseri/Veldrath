import type { PageServerLoad } from './$types';
import { listContentTypes } from '#lib/server/content-api';

/**
 * The catalog index.
 *
 * Reading is open to every signed-in account: the catalog is the community's, and the portal exists to show
 * people what can be written. Authoring is what `content.manage` gates, and that is decided on the type and
 * entry pages where the write controls live.
 */
export const load: PageServerLoad = async () => {
	const result = await listContentTypes();
	return { types: result.data ?? [], error: result.error };
};
