import { json } from '@sveltejs/kit';
import type { RequestHandler } from './$types';
import { syncAccountToGameServer } from '#lib/server/foundry-bridge';

/**
 * Mirrors a freshly registered Better Auth account into ASP.NET Core Identity, including
 * the password hash, so the desktop client's native sign-in (`/api/auth/login`) keeps
 * working while Identity remains the account store.
 *
 * The session cookie authenticates the caller; the plaintext password is only ever used to
 * derive the Identity hash and is never stored by SvelteKit.
 */
export const POST: RequestHandler = async ({ locals, request }) => {
	if (!locals.user) {
		return json({ synced: false, error: 'Not authenticated.' }, { status: 401 });
	}

	const body = (await request.json().catch(() => null)) as { password?: string } | null;
	if (!body?.password) {
		return json({ synced: false, error: 'Password is required.' }, { status: 400 });
	}

	const synced = await syncAccountToGameServer({
		accountId: locals.user.id,
		username: locals.user.username ?? locals.user.name,
		email: locals.user.email,
		emailVerified: locals.user.emailVerified,
		displayName: locals.user.name,
		password: body.password
	});

	return json({ synced });
};
