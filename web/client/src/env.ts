import { defineEnvVars } from '@sveltejs/kit/env';

/**
 * Explicit environment variables (SvelteKit 3).
 *
 * Declaring a variable here makes it importable from `$app/env/public` or
 * `$app/env/private`. Public values are inlined for the browser; keep secrets private.
 * Server-only secrets are read through `#lib/server/env.ts`, which reads `process.env`
 * directly (see that file for why `$env/dynamic/private` is avoided).
 */
export const variables = defineEnvVars({
	PUBLIC_API_URL: { public: true },
	PUBLIC_HUB_URL: { public: true }
});
