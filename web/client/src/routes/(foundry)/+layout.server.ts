import type { LayoutServerLoad } from './$types';
import { requireUser } from '#lib/server/access';

/** Foundry pages require an authenticated account; feature permissions are enforced per page. */
export const load: LayoutServerLoad = ({ locals }) => {
	const user = requireUser(locals);
	return { user };
};
