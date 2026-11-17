import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { toGameIdentity } from '#lib/server/access';
import { listSubmissions } from '#lib/server/foundry-api';
import { getMap, listMaps, validateMap } from '#lib/server/maps-api';
import { reviewVerdict, type MapReviewVerdict } from '#lib/foundry/map-editor';
import type { GameIdentity } from '#lib/server/game-request';

/**
 * One queue for everything awaiting a decision.
 *
 * H0 ships the destination the sidebar needs; the full queue — filters, a diff before promotion, decision
 * history — lands in H5. Until then this is a true list rather than a placeholder: both kinds are already
 * decided through the pages it links to.
 */
export const load: PageServerLoad = async ({ locals, parent }) => {
	const { canReview } = await parent();

	// The decision endpoints require `content.review`, so an account without the permission would be shown
	// rows it cannot act on. Refusing here is what makes the sidebar filter honest.
	if (!canReview) {
		error(403, 'The review queue needs the content review permission.');
	}

	const identity = toGameIdentity(locals.user!);

	const [pending, maps] = await Promise.all([
		listSubmissions(identity, { status: 'Pending', pageSize: 50 }),
		listMaps(identity)
	]);

	const submitted = (maps.data ?? []).filter(
		(map) => map.submittedAt != null && map.isActive !== true
	);

	return {
		submissions: pending.data?.items ?? [],
		maps: await Promise.all(
			submitted.map(async (map) => ({ ...map, verdict: await verdictFor(identity, map.id) }))
		),
		error: pending.error ?? maps.error ?? null
	};
};

/**
 * The verdict approving a map would reach.
 *
 * Approval is what puts a map live, so the queue answers the question a reviewer is about to ask. The validate
 * endpoint runs the same world rules the decision runs and stores nothing. A failed call is reported as
 * `unknown` rather than as a refusal: an unreachable verdict must not read as "this map is broken".
 *
 * @param identity Who is asking, so the graph and the validate pass are authenticated.
 * @param mapId The map awaiting a decision.
 * @returns The verdict, or `unknown` when either call failed.
 */
async function verdictFor(identity: GameIdentity, mapId: string): Promise<MapReviewVerdict> {
	const graph = await getMap(identity, mapId);
	if (!graph.data) return reviewVerdict(null);

	const validation = await validateMap(identity, mapId, graph.data);
	return reviewVerdict(validation.data?.issues ?? null);
}
