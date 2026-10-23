import { redirect } from '@sveltejs/kit';

export const load = ({ locals }) => {
	if (!locals.user) {
		redirect(302, '/login?redirectTo=/game');
	}

	return { user: locals.user };
};
