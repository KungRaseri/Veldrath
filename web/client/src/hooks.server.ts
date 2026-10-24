import { building } from '$app/env';
import type { Handle, HandleServerError } from '@sveltejs/kit/hooks';
import { svelteKitHandler } from 'better-auth/svelte-kit';
import { getAuth } from '#lib/server/auth';
import { loadExternalLogins, syncAccountToGameServer } from '#lib/server/foundry-bridge';

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
	// During prerendering there is no request session and no auth database, and the
	// build must not require BETTER_AUTH_SECRET. Render routes directly.
	if (building) {
		return resolve(event);
	}

	const auth = getAuth();
	const session = await auth.api.getSession({ headers: event.request.headers });

	if (session) {
		event.locals.session = session.session;
		event.locals.user = session.user;

		// Fire-and-forget: provisions the Identity row and links OAuth providers so
		// Blazor Foundry and the desktop client can still authenticate this account.
		void bridgeAccountOnce(session.user);
	}

	return svelteKitHandler({ event, resolve, auth, building });
};

/**
 * SvelteKit hides server error details from clients in production, so log the full error
 * (kind + stack) where the container logs can see it. Without this a failing OAuth callback
 * or load function only surfaces as an opaque "500 Internal Error" page.
 *
 * Kit 3 passes a discriminated union: `kind` is `app` / `framework` / `validation` /
 * `unknown` and `error` is typed accordingly; returning nothing keeps SvelteKit's defaults.
 */
export const handleError: HandleServerError = ({ kind, error, event }) => {
	const detail = error instanceof Error ? (error.stack ?? error.message) : error;
	console.error(`[handleError] ${kind} ${event.request.method} ${event.url.pathname}:`, detail);
};
