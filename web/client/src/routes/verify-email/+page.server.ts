import type { PageServerLoad } from './$types';
import { getAuth } from '$lib/server/auth';

export const load: PageServerLoad = async ({ url }) => {
	const token = url.searchParams.get('token');
	if (!token) {
		return { status: 'missing' as const };
	}

	try {
		await getAuth().api.verifyEmail({ query: { token } });
		return { status: 'verified' as const };
	} catch (error) {
		console.warn('[verify-email] verification failed.', error);
		return { status: 'invalid' as const };
	}
};
