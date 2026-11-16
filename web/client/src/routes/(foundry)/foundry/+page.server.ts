import type { PageServerLoad } from './$types';
import { Permissions, requirePermission, toGameIdentity } from '#lib/server/access';
import { listContentTypes } from '#lib/server/content-api';
import { listSubmissions } from '#lib/server/foundry-api';
import { listMaps } from '#lib/server/maps-api';

/**
 * The portal home.
 *
 * It exists because the portal had no landing page at all: a signed-in editor arriving from the site header
 * was dropped straight into the submission queue, and every other section was reachable only by knowing its
 * URL. Every count here comes from the list call the destination page already makes, so the home cannot
 * report a number the page it links to disagrees with.
 *
 * The portal home requires `content.view`. A player's entry point is `/submissions/new` — the header renders
 * that instead, so nobody lands here by accident.
 */
export const load: PageServerLoad = async ({ locals, parent }) => {
	const { access, canReview, unreadNotifications } = await parent();

	requirePermission(
		access,
		Permissions.ViewContent,
		'Foundry needs the content permission. Ask a Curator for access.'
	);

	const identity = toGameIdentity(locals.user!);

	const [types, maps, pending] = await Promise.all([
		listContentTypes(),
		listMaps(identity),
		// The pending total is a count, not a queue — the review page loads its own rows.
		canReview
			? listSubmissions(identity, { status: 'Pending', pageSize: 1 })
			: Promise.resolve(null)
	]);

	const mapRows = maps.data ?? [];
	const mapsInReview = mapRows.filter(
		(map) => map.submittedAt != null && map.isActive !== true
	).length;

	return {
		canReview,
		unreadNotifications,
		contentTypes: types.data?.length ?? 0,
		maps: mapRows.length,
		mapsInReview,
		pendingSubmissions: pending?.data?.totalCount ?? 0,
		// The first failure wins: it is the one the reader can act on, and three stacked warnings say less
		// than one.
		error: types.error ?? maps.error ?? pending?.error ?? null
	};
};
