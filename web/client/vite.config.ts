import { fileURLToPath } from 'node:url';
import { mdsvex } from 'mdsvex';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'vitest/config';
import nodeAdapter from '@sveltejs/adapter-node';
import { sveltekit } from '@sveltejs/kit/vite';

/**
 * SvelteKit's `$app/*` modules are virtual: their runtime reads `globalThis.__sveltekit_dev`, which
 * only exists inside a running SvelteKit server or browser session. Under Vitest that global is
 * undefined, so importing any component that touches `page` or `goto` throws before the test can
 * render anything. Aliasing the two modules a rendered page actually uses to plain test doubles
 * (in `src/lib/testing/`) keeps those components testable without stubbing each one per suite.
 */
const testAppModules = {
	'$app/state': fileURLToPath(new URL('./src/lib/testing/app-state.ts', import.meta.url)),
	'$app/navigation': fileURLToPath(new URL('./src/lib/testing/app-navigation.ts', import.meta.url))
};

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
			alias: testAppModules,
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
				// cobertura feeds the repository's coverage reporter (scripts/coverage-report.sh),
				// which is what renders the HTML report and the PR comment; lcov stays for
				// external viewers and json-summary for tooling that wants per-file numbers.
				reporter: ['text', 'text-summary', 'lcov', 'json-summary', 'cobertura'],
				// A failing threshold is exactly when the numbers matter. With this off, a red gate
				// wrote no report at all, so the PR comment kept whatever the previous head said —
				// stale percentages beside a red check. The comment now quotes the gate's own
				// totals (scripts/web-coverage-gate.mjs); it has to exist on a failed run to do that.
				reportOnFailure: true,
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
					// Schema-driven Foundry form, canvas and edge logic. TypeScript only, for the same
					// reason as `src/lib/game`: v8's branch bookkeeping for compiled Svelte templates
					// measures `{#if}` chains rather than logic, so the components beside these modules
					// are held to the `.svelte` floor below instead of a bar they can never meet.
					'src/lib/foundry/**/*.ts': {
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
