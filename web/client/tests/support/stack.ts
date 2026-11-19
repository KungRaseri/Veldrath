import { randomUUID } from 'node:crypto';
import { expect, type Page } from '@playwright/test';
import postgres from 'postgres';

/**
 * The shared harness for the full-flow specs: a real account, a real session, a real role.
 *
 * These specs exist because a browser-level mock cannot stand in for the API. Every Foundry page is
 * server-rendered by a `+page.server.ts` load that calls `gameApiFetch`, so the request leaves the
 * browser entirely — `page.route()` would never see it. The only honest way to test a user workflow
 * is to run the app against Veldrath.Server.
 *
 * Start the stack with `scripts/e2e-stack.sh start` — it is an isolated compose project (Postgres on
 * 55432, the API on 9100, the preview on 4273), so it never shares a port or a volume with the dev
 * environment. Then `npm run test:e2e:stack` from `web/`, which sets the matching E2E_* environment.
 * CI does the same in the "Playwright — authenticated" job of `ci-web.yml`.
 */
export const stackEnabled = process.env.E2E_STACK === '1';

/** Why a stack spec skipped, worded so the fix is obvious in the report. */
export const stackSkipReason =
	'needs a real Veldrath.Server and Postgres — run `scripts/e2e-stack.sh start` then `npm run test:e2e:stack`';

/**
 * Where the Identity tables live.
 *
 * Defaults to the isolated e2e database on :55432. `DATABASE_URL` in CI is the database owner, which is
 * also what the API uses; the Better Auth role cannot write `public`, so this must not default to the
 * auth connection string.
 */
const dbPort = process.env.E2E_DB_PORT ?? '55432';
const databaseUrl =
	process.env.E2E_DB_URL ??
	process.env.DATABASE_URL ??
	`postgres://veldrath:veldrath_dev@localhost:${dbPort}/veldrath`;

const PASSWORD = 'E2e-password-123';

/** The .NET API the SvelteKit server calls. Specs use it only for setup the UI has no path for. */
export const apiBase = (
	process.env.E2E_API_URL ?? `http://127.0.0.1:${process.env.E2E_API_PORT ?? '9100'}`
).replace(/\/+$/, '');

/**
 * Reads the short-lived game token the page's session can mint, so a spec can act as that account
 * against the API directly (the hub and some player actions have no `+server.ts` proxy).
 *
 * @param page A signed-in page.
 * @returns The bearer token `Veldrath.Server` accepts for this session.
 */
export async function gameToken(page: Page): Promise<string> {
	const token = await page.evaluate(async () => {
		const response = await fetch('/api/game-token', { cache: 'no-store' });
		if (!response.ok) throw new Error(`game-token request failed (${response.status}).`);
		return ((await response.json()) as { token: string }).token;
	});

	return token;
}

/** A throwaway account, unique per run so parallel workers never collide. */
export interface TestAccount {
	username: string;
	email: string;
	password: string;
}

/**
 * Builds an account whose username and email are unique.
 *
 * @param prefix Short label that identifies the spec in the account list (`e2econtent`, `e2emap`…).
 * @returns The credentials to register with.
 */
export function makeAccount(prefix: string): TestAccount {
	// `randomUUID` rather than `Math.random`: the suffix becomes an identifier that must not repeat
	// across parallel runs (and a predictable suffix would be a weak password-adjacent value).
	const stamp = `${Date.now().toString(36)}${randomUUID().replaceAll('-', '').slice(0, 6)}`;
	return {
		username: `${prefix}${stamp}`.slice(0, 30),
		email: `${prefix}+${stamp}@example.test`,
		password: PASSWORD
	};
}

/**
 * Registers through the real form, so Better Auth owns the session exactly as it does for a player.
 *
 * @param page Any page in a fresh browser context.
 * @param account The credentials to create.
 */
export async function register(page: Page, account: TestAccount): Promise<void> {
	await page.goto('/register');
	await page.locator('input[autocomplete="username"]').fill(account.username);
	await page.locator('input[autocomplete="email"]').fill(account.email);
	await page.locator('input[autocomplete="new-password"]').nth(0).fill(account.password);
	await page.locator('input[autocomplete="new-password"]').nth(1).fill(account.password);
	await page.getByRole('button', { name: /create account/i }).click();
	await page.waitForURL(/\/(game|characters)/, { timeout: 30_000 });
}

/**
 * Signs an existing account in through the login form.
 *
 * @param page A page in a context that is not signed in.
 * @param account The credentials to use.
 */
export async function signIn(page: Page, account: TestAccount): Promise<void> {
	await page.goto('/login');
	await page.locator('input[autocomplete="username"]').fill(account.username);
	await page.locator('input[autocomplete="current-password"]').fill(account.password);
	await page.getByRole('button', { name: 'Sign in' }).click();
	await page.waitForURL((url) => !url.pathname.startsWith('/login'), { timeout: 30_000 });
}

/**
 * Grants roles by writing the Identity join table.
 *
 * There is no bootstrap admin: `POST /api/admin/users/{id}/roles` needs an account that already holds
 * `admin.manage_users`, which is a chicken-and-egg problem for a fresh database. The role itself is
 * seeded with its permission claims at startup ([`Roles.DefaultPermissionsFor`]), so mapping the
 * account to the role row is all that is missing — and a reload then sees the new claims, because
 * `JwtClaimsEnrichment` resolves them per request.
 *
 * @param username The account's username, after the API has provisioned it.
 * @param roles Role names from `Veldrath.Server/Features/Auth/Roles.cs`.
 */
export async function grantRoles(username: string, roles: readonly string[]): Promise<void> {
	if (roles.length === 0) return;

	const sql = postgres(databaseUrl, { max: 1, onnotice: () => {} });

	try {
		// The Identity account is created just in time, on the first call that mints a game token, so
		// the row may not exist yet; retry until it does rather than racing the provisioning.
		for (let attempt = 0; attempt < 40; attempt += 1) {
			const [{ count }] = await sql<{ count: number }[]>`
				SELECT count(*)::int AS count
				FROM "AspNetUserRoles" ur
				JOIN "AspNetUsers" u ON u."Id" = ur."UserId"
				JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
				WHERE lower(u."UserName") = lower(${username}) AND r."Name" = ANY(${roles})
			`;

			if (count >= roles.length) return;

			await sql`
				INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
				SELECT u."Id", r."Id"
				FROM "AspNetUsers" u
				CROSS JOIN "AspNetRoles" r
				WHERE lower(u."UserName") = lower(${username}) AND r."Name" = ANY(${roles})
				ON CONFLICT DO NOTHING
			`;

			await new Promise((resolve) => setTimeout(resolve, 500));
		}

		throw new Error(`could not grant [${roles.join(', ')}] to '${username}'`);
	} finally {
		await sql.end({ timeout: 5 });
	}
}

/**
 * Registers an account, lets the API provision it, grants the roles and reloads so the claims apply.
 *
 * @param page The page to sign the new account in on.
 * @param roles Roles to grant (`ContentEditor` authors, `Curator` decides, `Admin` sees every portal).
 * @param prefix Username prefix, so the account is identifiable in the database.
 * @returns The signed-in account.
 */
export async function signUpWithRoles(
	page: Page,
	roles: readonly string[],
	prefix: string
): Promise<TestAccount> {
	const account = makeAccount(prefix);
	await register(page, account);

	// Visiting a Foundry page is what makes the server provision the Identity account: it mints a game
	// token for the session and the bridge creates the account just in time.
	await page.goto('/foundry');
	await grantRoles(account.username, roles);
	await page.reload();

	return account;
}

/**
 * Asserts the page is the portal home, which is where every journey starts.
 *
 * @param page The signed-in page.
 */
export async function expectPortalHome(page: Page): Promise<void> {
	await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
	await expect(page.getByRole('navigation', { name: /foundry/i })).toBeVisible();
}
