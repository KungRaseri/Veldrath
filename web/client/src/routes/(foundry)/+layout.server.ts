import { requireUser } from '$lib/server/access';

/** Foundry pages require an authenticated account; feature permissions are enforced per page. */
export const load = ({ locals }) => {
	const user = requireUser(locals);
	return { user };
};
