import { redirect } from '@sveltejs/kit';
import { env } from '$env/dynamic/private';
import type { PageServerLoad } from './$types';
import { listLinkedProviders, type LinkedProvider } from '$lib/server/account';
import { getAuth } from '$lib/server/auth';

interface AccountSession {
	id: string;
	token: string;
	createdAt: string;
	expiresAt: string;
	ipAddress: string | null;
	userAgent: string | null;
}

interface LinkableProvider {
	id: string;
	label: string;
}

function configuredProviders(): LinkableProvider[] {
	const providers: LinkableProvider[] = [];

	if (env.DISCORD_CLIENT_ID && env.DISCORD_CLIENT_SECRET) {
		providers.push({ id: 'discord', label: 'Discord' });
	}
	if (env.GOOGLE_CLIENT_ID && env.GOOGLE_CLIENT_SECRET) {
		providers.push({ id: 'google', label: 'Google' });
	}
	if (env.MICROSOFT_CLIENT_ID && env.MICROSOFT_CLIENT_SECRET) {
		providers.push({ id: 'microsoft', label: 'Microsoft' });
	}

	return providers;
}

export const load: PageServerLoad = async ({ locals, request, url }) => {
	if (!locals.user) {
		redirect(303, '/login');
	}

	let providers: LinkedProvider[] = [];
	try {
		providers = await listLinkedProviders(locals.user.id);
	} catch (error) {
		console.warn('[account] could not load linked providers.', error);
	}

	let sessions: AccountSession[] = [];
	try {
		const active = await getAuth().api.listSessions({ headers: request.headers });
		sessions = active.map((session) => ({
			id: session.id,
			token: session.token,
			createdAt: new Date(session.createdAt).toISOString(),
			expiresAt: new Date(session.expiresAt).toISOString(),
			ipAddress: session.ipAddress ?? null,
			userAgent: session.userAgent ?? null
		}));
	} catch (error) {
		console.warn('[account] could not load sessions.', error);
	}

	return {
		user: locals.user,
		providers,
		sessions,
		currentSessionId: locals.session?.id ?? null,
		linkableProviders: configuredProviders(),
		oauthError: url.searchParams.get('error')
	};
};
