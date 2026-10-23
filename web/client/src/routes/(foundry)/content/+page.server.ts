import type { PageServerLoad } from './$types';
import { listContentTypes } from '$lib/server/content-api';

export const load: PageServerLoad = async () => {
	const result = await listContentTypes();
	return { types: result.data ?? [], error: result.error };
};
