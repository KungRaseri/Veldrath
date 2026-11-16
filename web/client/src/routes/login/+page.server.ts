import type { PageServerLoad } from './$types';
import { redirect } from '@sveltejs/kit';
import { localPathFrom } from '#lib/navigation';

export const load: PageServerLoad = ({ locals, url }) => {
	if (locals.user) {
		redirect(302, localPathFrom(url.searchParams.get('redirectTo')) ?? '/game');
	}
};
