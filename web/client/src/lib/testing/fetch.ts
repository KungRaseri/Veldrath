import { vi } from 'vitest';

/**
 * Response factories and a recording `fetch` stub for server-side client tests.
 *
 * The server clients in `#lib/server` all funnel through `gameApiFetch`, which reads
 * `content-type`, parses JSON, and maps status codes. Tests therefore need real `Response`
 * objects, not plain objects, or the parsing path is never exercised.
 */

/** A JSON response the way `Veldrath.Server` sends one. */
export function jsonResponse(body: unknown, init: ResponseInit = {}): Response {
	return new Response(JSON.stringify(body), {
		...init,
		headers: { 'content-type': 'application/json', ...toHeaderRecord(init.headers) }
	});
}

/** A plain-text response, used for the invalid-JSON and raw-body error paths. */
export function textResponse(body: string, init: ResponseInit = {}): Response {
	return new Response(body, {
		...init,
		headers: { 'content-type': 'text/plain', ...toHeaderRecord(init.headers) }
	});
}

/** A body-less response; `204` is what the write endpoints return after a deactivate. */
export function emptyResponse(status = 204): Response {
	return new Response(null, { status });
}

/** A response carrying only the `WWW-Authenticate` header, like an ASP.NET Core 401. */
export function unauthorizedResponse(description?: string, status = 401): Response {
	const header = description
		? `Bearer error="invalid_token", error_description="${description}"`
		: 'Bearer';
	return new Response(null, { status, headers: { 'www-authenticate': header } });
}

function toHeaderRecord(headers: HeadersInit | undefined): Record<string, string> {
	if (!headers) return {};
	return Object.fromEntries(new Headers(headers).entries());
}

/** One recorded `fetch` invocation. */
export interface FetchCall {
	url: string;
	method: string;
	body: string | null;
	headers: Record<string, string>;
}

/** A handler that maps a recorded call to a response. */
export type FetchHandler = (call: FetchCall) => Response | Promise<Response>;

/** Handle returned by {@link installFetch}. */
export interface FetchStub {
	/** Every recorded call, in order. */
	calls: FetchCall[];
	/** Restores the previous global `fetch`. Always call this in `afterEach`. */
	restore(): void;
}

/**
 * Replaces the global `fetch` with a recorder and delegates to `handler`.
 *
 * Call `restore()` in `afterEach`; the stub does not auto-restore so a failing assertion
 * leaves the recorded calls inspectable.
 */
export function installFetch(handler: FetchHandler): FetchStub {
	const original = globalThis.fetch;
	const calls: FetchCall[] = [];

	const stub = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
		const url =
			typeof input === 'string' ? input : input instanceof URL ? input.toString() : input.url;
		const call: FetchCall = {
			url,
			method: (init?.method ?? 'GET').toUpperCase(),
			body: typeof init?.body === 'string' ? init.body : null,
			headers: toHeaderRecord(init?.headers)
		};

		calls.push(call);
		return handler(call);
	});

	vi.stubGlobal('fetch', stub);

	return {
		calls,
		restore: () => {
			vi.stubGlobal('fetch', original);
		}
	};
}

/** Convenience: answer every call with the same JSON payload. */
export function installJsonFetch(body: unknown, status = 200): FetchStub {
	return installFetch(() => jsonResponse(body, { status }));
}
