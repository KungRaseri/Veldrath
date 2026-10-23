import type { PageServerLoad } from './$types';
import { listPatchNotes } from '$lib/server/editorial-api';

export const load: PageServerLoad = async () => {
	const { page, error } = await listPatchNotes();
	return { notes: page.items, total: page.totalCount, error };
};
