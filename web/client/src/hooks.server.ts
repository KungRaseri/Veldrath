import { building } from '$app/environment';
import type { Handle } from '@sveltejs/kit';
import { svelteKitHandler } from 'better-auth/svelte-kit';
import { getAuth } from '$lib/server/auth';
import { loadExternalLogins, syncAccountToGameServer } from '$lib/server/foundry-bridge';

// The temporary Identity bridge runs when an account's identity snapshot changes so username,
// email, and verification updates reach Identity. Password sync is handled separately by
// /api/foundry-bridge/register.
const bridgedSnapshots = new Map<string, string>();

function identitySnapshot(user: AuthUser): string {
	return `${user.username ?? user.name}\u0000${user.email}\u0000${user.emailVerified}`;
}

async function bridgeAccountOnce(user: AuthUser): Promise<void> {
	const snapshot = identitySnapshot(user);
	if (bridgedSnapshots.get(user.id) === snapshot) {
		return;
	}

	bridgedSnapshots.set(user.id, snapshot);

	try {
		const externalLogins = await loadExternalLogins(user.id);
		const synced = await syncAccountToGameServer({
			accountId: user.id,
			username: user.username ?? user.name,
			email: user.email,
			emailVerified: user.emailVerified,
			displayName: user.name,
			externalLogins
		});

		if (!synced) {
			bridgedSnapshots.delete(user.id);
		}
	} catch (error) {
		// Allow a retry on the next request; never block page rendering on the bridge.
		bridgedSnapshots.delete(user.id);
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
