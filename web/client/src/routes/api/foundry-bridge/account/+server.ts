import { json } from '@sveltejs/kit';
import type { RequestHandler } from './$types';
import { loadExternalLogins, syncAccountToGameServer } from '#lib/server/foundry-bridge';

/**
 * Re-mirrors the signed-in account's identity into ASP.NET Core Identity after a profile,
 * email, or linked-provider change. The session cookie authenticates the caller.
 */
export const POST: RequestHandler = async ({ locals, request }) => {
	if (!locals.user) {
		return json({ synced: false, error: 'Not authenticated.' }, { status: 401 });
	}

	const body = (await request.json().catch(() => null)) as {
		username?: string;
		displayName?: string;
	} | null;
	const username = body?.username?.trim() || locals.user.username || locals.user.name;
	const displayName = body?.displayName?.trim() || locals.user.name;
	const externalLogins = await loadExternalLogins(locals.user.id);

	const synced = await syncAccountToGameServer({
		accountId: locals.user.id,
		username,
		email: locals.user.email,
		emailVerified: locals.user.emailVerified,
		displayName,
		externalLogins
	});

	return json({ synced });
};
