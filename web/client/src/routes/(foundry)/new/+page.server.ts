import type { PageServerLoad } from './$types';
import { listContentTypes } from '#lib/server/content-api';

/**
 * The one way into creating something.
 *
 * The portal used to have two doors: a New action buried inside each content type, and a submission form
 * reachable only by knowing the URL. Both still exist, but the choice between them is now made here, once,
 * from the type's own label and description — and the account's permission decides which action a type
 * offers.
 */
export const load: PageServerLoad = async ({ parent }) => {
	const { canManageContent } = await parent();
	const result = await listContentTypes();

	return {
		canManageContent,
		types: result.data ?? [],
		error: result.error
	};
};
