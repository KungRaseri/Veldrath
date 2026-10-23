import { redirect } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { listLinkedProviders, type LinkedProvider } from '$lib/server/account';

export const load: PageServerLoad = async ({ locals }) => {
	if (!locals.user) {
		redirect(303, '/login');
	}

	let providers: LinkedProvider[] = [];
	try {
		providers = await listLinkedProviders(locals.user.id);
	} catch (error) {
		console.warn('[account] could not load linked providers.', error);
	}

	return {
		user: locals.user,
		providers
	};
};
