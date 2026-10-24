import type { PageServerLoad } from './$types';
import { listAnnouncements, listLoreArticles, listPatchNotes } from '#lib/server/editorial-api';

export const load: PageServerLoad = async () => {
	const [announcements, lore, notes] = await Promise.all([
		listAnnouncements(),
		listLoreArticles(null, 1, 3),
		listPatchNotes(1, 3)
	]);

	return {
		announcements: announcements.page.items.slice(0, 2),
		lore: lore.page.items,
		patchNotes: notes.page.items
	};
};
