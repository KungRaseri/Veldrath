import { mdsvex } from 'mdsvex';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'vitest/config';
import nodeAdapter from '@sveltejs/adapter-node';
import { sveltekit } from '@sveltejs/kit/vite';

// Veldrath is served by adapter-node only: the SvelteKit app is the single client,
// so no adapter-static SPA build is needed.
export default defineConfig(() => {
	return {
		plugins: [
			tailwindcss(),
			sveltekit({
				compilerOptions: {
					// Force runes mode for the project, except for libraries. Can be removed in svelte 6.
					runes: ({ filename }) =>
						filename.split(/[/\\]/).includes('node_modules') ? undefined : true
				},
				adapter: nodeAdapter(),
				preprocess: [mdsvex({ extensions: ['.svx', '.md'] })],
				extensions: ['.svelte', '.svx', '.md']
			})
		],
		test: {
			expect: { requireAssertions: true },
			environment: 'jsdom',
			passWithNoTests: true,
			include: ['src/**/*.{test,spec}.{js,ts}'],
			setupFiles: ['./src/test-setup.ts'],
			/**
			 * Coverage gate: 90% global, with per-glob floors.
			 *
			 * Files matched by a glob threshold still count toward the global aggregate, so the
			 * floors raise the bar on the layers that matter (server clients, game logic, route
			 * handlers) instead of letting a rush of page-markup tests mask an untested API client.
			 *
			 * Measured baseline before the gate existed (2026-11-02): 27.12% lines, 24.28%
			 * statements, 23.35% branches, 28.10% functions. See
			 * `plans/web-feature-parity-and-coverage.md` for the per-directory breakdown. The gate is
			 * expected to fail until that pass lands; it is deliberately not softened to match the
			 * current number.
			 */
			coverage: {
				provider: 'v8' as const,
				reporter: ['text', 'text-summary', 'lcov', 'json-summary'],
				include: ['src/**/*.{ts,svelte}'],
				exclude: [
					'src/**/*.{test,spec}.{js,ts}',
					'src/**/*.d.ts',
					'src/test-setup.ts',
					// Test-only helpers: fake events, fetch stubs, environment overrides, and the
					// C# source readers the parity tests use. They never ship in the app bundle, so
					// measuring them would dilute the gate with code the product does not run.
					'src/lib/testing/**'
				],
				thresholds: {
					lines: 90,
					functions: 90,
					branches: 90,
					statements: 90,
					// Server-side API clients, auth, mail, markdown, and the game token. Cheapest
					// lines to cover and the highest cost when they drift.
					'src/lib/server/**': {
						lines: 92,
						functions: 92,
						branches: 92,
						statements: 92
					},
					// The runes store, reducers, and pure game helpers. TypeScript only: the shell
					// components beside them are markup, and v8's branch bookkeeping for compiled
					// Svelte templates measures `{#if}` chains rather than logic. They are held to
					// the `.svelte` floor below instead of being swept into a logic floor they can
					// never meet.
					'src/lib/game/**/*.ts': {
						lines: 92,
						functions: 92,
						branches: 92,
						statements: 92
					},
					// Schema-driven Foundry form logic.
					'src/lib/foundry/**': {
						lines: 92,
						functions: 92,
						branches: 92,
						statements: 92
					},
					// Route loads, form actions, and +server.ts endpoints.
					'src/routes/**/*.ts': {
						lines: 90,
						functions: 90,
						branches: 90,
						statements: 90
					},
					// Page and component markup. Lower because template branches dominate the
					// branch count, but high enough that a page cannot ship without a render test.
					'src/**/*.svelte': {
						lines: 85,
						functions: 85,
						branches: 80,
						statements: 85
					}
				}
			}
		}
	};
});
