import { defineConfig } from '@playwright/test';

/**
 * The preview origin the specs talk to.
 *
 * The stack the workflow specs need is deliberately isolated from every dev server — Postgres on 55432,
 * the API on 9100 and this preview on 4273 — so the port comes from the environment (`npm run
 * test:e2e:stack` sets it). Better Auth validates the origin, so the preview is *started* with a
 * matching ORIGIN/BETTER_AUTH_URL here instead of leaving it to whoever runs the suite to remember.
 */
const webPort = Number(process.env.E2E_WEB_PORT ?? 4173);
const origin = `http://localhost:${webPort}`;

// The SvelteKit server (not the browser) calls the API, so the preview has to be pointed at the same
// isolated API port the stack script started — otherwise it would read `.env` and talk to the dev API.
const apiPort = Number(process.env.E2E_API_PORT ?? 9100);
const apiOrigin = `http://127.0.0.1:${apiPort}`;

// Better Auth signs sessions from the SvelteKit server, so the preview needs the e2e database too —
// `.env` points at the dev one on 5433, which is exactly the cross-wiring this isolation prevents. The
// auth role owns the `auth` schema and is the only role Better Auth can use.
const dbPort = Number(process.env.E2E_DB_PORT ?? 55432);
const authDatabaseUrl =
	process.env.E2E_DB_URL ??
	`postgres://veldrath_auth:veldrath_auth_dev@localhost:${dbPort}/veldrath`;

/**
 * Where this invocation writes its reports.
 *
 * `tests/run-e2e.mjs` sets these per run name, because one CI job runs Playwright more than once
 * (the stack-free run and then the isolated stack run), and a fixed output file would be
 * truncated by the second invocation — the first run's failures would vanish from the JSON that
 * the summary and the integrity check read. Before this config existed there was no `reporter`
 * at all, so every workflow uploaded an empty `playwright-report/` and the pull request saw
 * nothing but console output.
 */
const runName = process.env.PW_RUN_NAME ?? 'local';
const jsonReport = process.env.PW_JSON_REPORT ?? `test-results/e2e-${runName}.json`;
const junitReport = process.env.PW_JUNIT_REPORT ?? `test-results/e2e-${runName}.xml`;
const htmlReport = process.env.PW_HTML_REPORT ?? `playwright-report/${runName}`;

export default defineConfig({
	// `json` is what scripts/e2e-summary.mjs and scripts/check-e2e-manifest.mjs read, `junit` is
	// the format the .NET jobs already upload, and `html` is the openable report with traces
	// attached. `open: 'never'` matters: without it a CI runner tries to start a browser.
	reporter: [
		['list'],
		['html', { outputFolder: htmlReport, open: 'never' }],
		['json', { outputFile: jsonReport }],
		['junit', { outputFile: junitReport }]
	],
	// Traces and video land here; per-run so the second invocation keeps the first one's evidence.
	outputDir: process.env.PW_OUTPUT_DIR ?? `test-results/${runName}`,
	webServer: {
		// `--port` and `--strictPort`: the preview must land on the isolated port, or fail rather than
		// drift to the next one and leave a stale server answering where the specs look.
		command: `npm run build && npm run preview -- --port ${webPort} --strictPort`,
		port: webPort,
		env: {
			ORIGIN: origin,
			BETTER_AUTH_URL: origin,
			// Both are inlined into the browser bundle by `vite build`. Setting only PUBLIC_API_URL
			// leaves the SignalR connection pointed at `.env`'s dev hub on :9000, so the map never
			// renders in a spec and the failure looks like a client bug rather than a build input.
			PUBLIC_API_URL: apiOrigin,
			PUBLIC_HUB_URL: `${apiOrigin}/hubs/game`,
			INTERNAL_BRIDGE_URL: apiOrigin,
			DATABASE_URL: authDatabaseUrl,
			// The specs register dozens of accounts in a minute from one machine. Better Auth buckets
			// its limits per client address and shares one bucket per path when it cannot resolve one,
			// so leaving the limits on turns the parallel run into 429s on `/sign-up/email` — which is
			// what made a full fresh-stack run fail in twelve places while every spec passed on its
			// own. The API gets the same treatment from `scripts/e2e-stack.sh`.
			AUTH_RATE_LIMIT_DISABLED: 'true'
		},
		// A reused preview is the PREVIOUS build, so a fix you just made is not in the browser and the old
		// failure looks unreproducible. Stack runs and CI always rebuild.
		// `vite build` (SvelteKit) comfortably exceeds the default 60s — give the build and the server room.
		reuseExistingServer: !process.env.CI && !process.env.E2E_STACK,
		timeout: 240_000
	},
	// A stack spec drives a whole journey against a real database; a flake here is a signal worth
	// keeping, but a single retry stops one slow API call from turning the job red. The trace is the
	// point: a retry whose first attempt left no trace is a flake nobody can diagnose.
	retries: process.env.CI ? 1 : 0,
	use: {
		baseURL: origin,
		trace: 'on-first-retry',
		video: 'retain-on-failure'
	},
	// Server-rendered pages call the API before they paint, so the default 5s expect budget is tight
	// for a cold container.
	expect: { timeout: 10_000 },
	testMatch: '**/*.e2e.{ts,js}'
});
