import type { PageServerLoad } from './$types';
import { toGameIdentity } from '#lib/server/access';
import { listModReports, listModUsers } from '#lib/server/admin-api';

export const load: PageServerLoad = async ({ parent, locals }) => {
	// Await the group layout first: it owns the permission check, and without this the page load
	// races it and calls the API for a request that is already being refused.
	await parent();

	const identity = toGameIdentity(locals.user!);
	const [users, reports] = await Promise.all([
		listModUsers(identity, { page: 1, pageSize: 1 }),
		listModReports(identity, false, 1, 1)
	]);

	return {
		userCount: users.data?.total ?? 0,
		openReports: reports.data?.total ?? 0,
		errors: [users.error, reports.error].filter(Boolean)
	};
};
