import { building } from '$app/env';
import type { Handle, HandleServerError } from '@sveltejs/kit/hooks';
import { svelteKitHandler } from 'better-auth/svelte-kit';
import { getAuth } from '#lib/server/auth';

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
