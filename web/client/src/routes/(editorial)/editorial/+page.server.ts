import type { PageServerLoad } from './$types';
import { toGameIdentity } from '$lib/server/access';
import {
	adminListAnnouncements,
	adminListLoreArticles,
	adminListPatchNotes
} from '$lib/server/editorial-api';

export const load: PageServerLoad = async ({ locals }) => {
	const identity = toGameIdentity(locals.user!);
	const [lore, patchNotes, announcements] = await Promise.all([
		adminListLoreArticles(identity, 1, 1),
		adminListPatchNotes(identity, 1, 1),
		adminListAnnouncements(identity, 1, 1)
	]);

	return {
		counts: {
			lore: lore.data?.totalCount ?? 0,
			patchNotes: patchNotes.data?.totalCount ?? 0,
			announcements: announcements.data?.totalCount ?? 0
		},
		errors: [lore.error, patchNotes.error, announcements.error].filter(Boolean)
	};
};
