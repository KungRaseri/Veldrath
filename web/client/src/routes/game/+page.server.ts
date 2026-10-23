import { redirect } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';

/** The Phase 0 smoke page moved to the real character-select / play flow. */
export const load: PageServerLoad = () => {
	redirect(307, '/characters');
};
