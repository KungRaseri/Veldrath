import type { PageServerLoad } from './$types';
import { toGameIdentity } from '$lib/server/access';
import { listModReports, listModUsers } from '$lib/server/admin-api';

export const load: PageServerLoad = async ({ locals }) => {
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
