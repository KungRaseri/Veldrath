import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { toGameIdentity } from '#lib/server/access';
import { listSubmissions } from '#lib/server/foundry-api';
import { listMaps } from '#lib/server/maps-api';

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

	return {
		submissions: pending.data?.items ?? [],
		maps: (maps.data ?? []).filter((map) => map.submittedAt != null && map.isActive !== true),
		error: pending.error ?? maps.error ?? null
	};
};
