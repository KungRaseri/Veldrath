/**
 * Temporary environment overrides for server-side tests.
 *
 * `#lib/server/env` exports `process.env` itself, so mutating `process.env` is visible to every
 * module that already imported it. No module reload is needed, which keeps the tests honest:
 * they exercise the same code path the running server uses.
 */

/** A function that puts the process environment back the way it was. */
export type RestoreEnv = () => void;

/**
 * Sets environment variables for the duration of one test.
 *
 * ```ts
 * let restore: RestoreEnv;
 * afterEach(() => restore?.());
 * restore = withEnv({ INTERNAL_BRIDGE_URL: 'http://bridge' });
 * ```
 */
export function withEnv(variables: Record<string, string | undefined>): RestoreEnv {
	const previous = new Map<string, string | undefined>();

	for (const [key, value] of Object.entries(variables)) {
		previous.set(key, process.env[key]);
		if (value === undefined) {
			delete process.env[key];
		} else {
			process.env[key] = value;
		}
	}

	return () => {
		for (const [key, value] of previous) {
			if (value === undefined) {
				delete process.env[key];
			} else {
				process.env[key] = value;
			}
		}
	};
}
