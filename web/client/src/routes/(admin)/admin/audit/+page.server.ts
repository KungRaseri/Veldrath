import type { PageServerLoad } from './$types';
import { Permissions, requirePermission, toGameIdentity } from '#lib/server/access';
import { getAuditLog } from '#lib/server/admin-api';

export const load: PageServerLoad = async ({ parent, locals, url }) => {
	// Await the group layout first: it admits both halves of the staff split, and this page then owns
	// the permission its own API call needs. Without this the page load races the layout and calls
	// the API for a request that is already being refused.
	const { access } = await parent();
	requirePermission(
		access,
		Permissions.ManageUsers,
		'The audit log requires user management permission.'
	);

	const page = Math.max(1, Number(url.searchParams.get('page') ?? 1));
	const targetId = url.searchParams.get('targetId');
	const result = await getAuditLog(toGameIdentity(locals.user!), page, 50, targetId);

	return {
		entries: result.data?.items ?? [],
		total: result.data?.total ?? 0,
		page: result.data?.page ?? page,
		pageSize: result.data?.pageSize ?? 50,
		targetId: targetId ?? '',
		error: result.error
	};
};
