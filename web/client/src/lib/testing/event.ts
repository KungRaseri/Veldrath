import type { RequestEvent } from '@sveltejs/kit';

/**
 * Fake `RequestEvent` builders for SvelteKit load and action tests.
 *
 * Loads are tested by importing `./+page.server` and calling `load(event)` directly, so the
 * event only needs the properties the route actually reads. Everything SvelteKit would
 * otherwise provide (route ids, client address, platform) is filled with inert values rather
 * than left undefined, because a load that reads one of them should fail on the assertion, not
 * on a `TypeError` that hides the real problem.
 */

/** A signed-in Better Auth user; override only the fields an assertion depends on. */
export function fakeUser(overrides: Partial<AuthUser> = {}): AuthUser {
	const timestamp = new Date('2026-01-01T00:00:00.000Z');

	return {
		id: '11111111-1111-4111-8111-111111111111',
		email: 'player@example.com',
		name: 'Player One',
		username: 'player-one',
		emailVerified: true,
		image: null,
		createdAt: timestamp,
		updatedAt: timestamp,
		...overrides
	};
}

/** A Better Auth session row paired with {@link fakeUser}. */
export function fakeSession(
	user: AuthUser = fakeUser(),
	overrides: Partial<AuthSession> = {}
): AuthSession {
	return {
		id: '22222222-2222-4222-8222-222222222222',
		userId: user.id,
		expiresAt: new Date('2026-12-31T00:00:00.000Z'),
		token: 'test-session-token',
		createdAt: new Date('2026-01-01T00:00:00.000Z'),
		updatedAt: new Date('2026-01-01T00:00:00.000Z'),
		ipAddress: '127.0.0.1',
		userAgent: 'vitest',
		...overrides
	};
}

/** Options accepted by {@link makeServerEvent}. */
export interface FakeEventInit {
	/** Absolute URL or a path resolved against `http://localhost:5173`. */
	url?: string;
	method?: string;
	/** JSON body. When present the request becomes a POST with a JSON content type. */
	body?: unknown;
	params?: Record<string, string>;
	/** Signed-in user, or `null`/`undefined` for an anonymous visitor. */
	user?: AuthUser | null;
	session?: AuthSession | null;
	headers?: Record<string, string>;
	/** Extra `locals` entries layered on top of `user` and `session`. */
	locals?: Partial<App.Locals>;
}

/**
 * Builds a `RequestEvent` for a load or form action.
 *
 * The default is an anonymous visitor on `/`, which is what the redirect-guard tests need.
 * Pass `user` to sign in, `params` for dynamic segments, and `body` for a form action.
 */
export function makeServerEvent(init: FakeEventInit = {}): RequestEvent {
	const url = new URL(init.url ?? '/', 'http://localhost:5173');
	const headers = new Headers(init.headers ?? { 'user-agent': 'vitest' });
	const method = init.method ?? (init.body === undefined ? 'GET' : 'POST');
	const canHaveBody = method !== 'GET' && method !== 'HEAD';

	if (init.body !== undefined && canHaveBody && !headers.has('content-type')) {
		headers.set('content-type', 'application/json');
	}

	const request = new Request(url, {
		method,
		headers,
		body: init.body !== undefined && canHaveBody ? JSON.stringify(init.body) : undefined
	});

	const user = init.user ?? null;
	const session = init.session ?? (init.user ? fakeSession(init.user) : null);

	/** Minimal cookie jar: enough for a load that reads or writes a cookie. */
	const jar = new Map<string, string>();
	const cookies = {
		get: (name: string) => jar.get(name),
		getAll: () => [...jar.entries()].map(([name, value]) => ({ name, value })),
		set: (name: string, value: string) => {
			jar.set(name, value);
		},
		delete: (name: string) => {
			jar.delete(name);
		},
		serialize: () => ''
	};

	const event = {
		url,
		params: init.params ?? {},
		request,
		locals: {
			...(user ? { user } : {}),
			...(session ? { session } : {}),
			...init.locals
		} satisfies App.Locals,
		cookies,
		fetch: globalThis.fetch,
		route: { id: null },
		setHeaders: () => {},
		isDataRequest: false,
		isSubRequest: false,
		getClientAddress: () => '127.0.0.1',
		platform: undefined
	};

	return event as unknown as RequestEvent;
}

/**
 * Reads the JSON body of a form action's request as a plain object.
 *
 * SvelteKit form actions usually receive `FormData`; routes in this app accept JSON, so tests
 * build the request with `makeServerEvent({ body })` and the route parses it the same way the
 * browser would.
 */
export async function readJsonBody(request: Request): Promise<Record<string, unknown>> {
	return (await request.json()) as Record<string, unknown>;
}
