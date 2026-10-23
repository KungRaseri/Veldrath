import { defineConfig } from '@playwright/test';

export default defineConfig({
	webServer: {
		command: 'npm run build && npm run preview',
		port: 4173,
		// Reuse a preview server already listening on 4173 locally (faster local
		// iteration); CI always starts a fresh one.
		reuseExistingServer: !process.env.CI,
		// `vite build` (SvelteKit) comfortably exceeds the default 60s — give
		// the build + preview server room to come up before Playwright gives up.
		timeout: 240_000
	},
	testMatch: '**/*.e2e.{ts,js}'
});
