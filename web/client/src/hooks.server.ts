import { building } from '$app/environment';
import type { Handle } from '@sveltejs/kit';
import { svelteKitHandler } from 'better-auth/svelte-kit';
import { getAuth } from '$lib/server/auth';
import { loadExternalLogins, syncAccountToGameServer } from '$lib/server/foundry-bridge';

// The temporary Identity bridge only needs to run once per account per server process.
// Password sync is handled separately by /api/foundry-bridge/register.
const bridgedUserIds = new Set<string>();

async function bridgeAccountOnce(user: AuthUser): Promise<void> {
	if (bridgedUserIds.has(user.id)) {
		return;
	}

	bridgedUserIds.add(user.id);

	try {
		const externalLogins = await loadExternalLogins(user.id);
		const synced = await syncAccountToGameServer({
			accountId: user.id,
			username: user.username ?? user.name,
			email: user.email,
			emailVerified: user.emailVerified,
			externalLogins
		});

		if (!synced) {
			bridgedUserIds.delete(user.id);
		}
	} catch (error) {
		// Allow a retry on the next request; never block page rendering on the bridge.
		bridgedUserIds.delete(user.id);
		console.warn('[foundry-bridge] background account sync failed.', error);
	}
}

export const handle: Handle = async ({ event, resolve }) => {
	const auth = getAuth();

	if (!building) {
		const session = await auth.api.getSession({ headers: event.request.headers });

		if (session) {
			event.locals.session = session.session;
			event.locals.user = session.user;

			// Fire-and-forget: provisions the Identity row and links OAuth providers so
			// Blazor Foundry and the desktop client can still authenticate this account.
			void bridgeAccountOnce(session.user);
		}
	}

	return svelteKitHandler({ event, resolve, auth, building });
};
