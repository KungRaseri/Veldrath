import { defineConfig } from '@playwright/test';

export default defineConfig({
	webServer: {
		command: 'npm run build && npm run preview',
		port: 4173,
		// Reuse a preview server already listening on 4173 locally (faster local
		// iteration); CI always starts a fresh one.
		//
		// Beware locally: a reused preview is the PREVIOUS build, so a fix you just made is not in the
		// browser and the old failure looks unreproducible. Set CI=true to force a rebuild.
		reuseExistingServer: !process.env.CI,
		// `vite build` (SvelteKit) comfortably exceeds the default 60s — give
		// the build + preview server room to come up before Playwright gives up.
		timeout: 240_000
	},
	// A stack spec drives a whole journey against a real database; a flake here is a signal worth
	// keeping, but a single retry stops one slow API call from turning the job red. The trace is the
	// point: a retry whose first attempt left no trace is a flake nobody can diagnose.
	retries: process.env.CI ? 1 : 0,
	use: {
		trace: 'on-first-retry',
		video: 'retain-on-failure'
	},
	// Server-rendered pages call the API before they paint, so the default 5s expect budget is tight
	// for a cold container.
	expect: { timeout: 10_000 },
	testMatch: '**/*.e2e.{ts,js}'
});
