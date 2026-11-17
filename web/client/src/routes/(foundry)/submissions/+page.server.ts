import type { PageServerLoad } from './$types';
import { toGameIdentity } from '#lib/server/access';
import { listSubmissions } from '#lib/server/foundry-api';

/**
 * A submission feed, scoped to what the caller is allowed to read.
 *
 * The default is the caller's own work, because that is the question a contributor actually has: what
 * happened to the thing I wrote, and what did the reviewer say. A reviewer can ask for everything, which is
 * the queue's long history rather than its pending rows.
 */
export const load: PageServerLoad = async ({ locals, parent, url }) => {
	const { canReview } = await parent();
	// `scope=all` is only honoured for a reviewer: for anyone else the server narrows the feed anyway, and
	// asking for everything would just be a confusing synonym for the approved catalog.
	const scope = canReview && url.searchParams.get('scope') === 'all' ? 'all' : 'mine';

	const filters = {
		status: url.searchParams.get('status'),
		contentType: url.searchParams.get('contentType'),
		search: url.searchParams.get('search'),
		page: Math.max(1, Number(url.searchParams.get('page') ?? 1))
	};

	const result = await listSubmissions(toGameIdentity(locals.user!), {
		...filters,
		mine: scope === 'mine',
		pageSize: 20
	});

	return {
		canReview,
		scope,
		submissions: result.data?.items ?? [],
		total: result.data?.totalCount ?? 0,
		page: result.data?.page ?? filters.page,
		pageSize: result.data?.pageSize ?? 20,
		filters,
		error: result.error
	};
};
