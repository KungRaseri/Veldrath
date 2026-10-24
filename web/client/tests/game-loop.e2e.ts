import { expect, test } from '@playwright/test';

/**
 * Authenticated game loop against a real Veldrath.Server.
 *
 * The roster is rendered by SvelteKit's SSR (the server calls `/api/characters`), and the game
 * UI needs a live SignalR hub, so neither can be stubbed from the browser. This spec therefore
 * only runs when a real server is reachable: set `E2E_GAME=1` and point
 * `PUBLIC_API_URL`/`PUBLIC_HUB_URL`/`INTERNAL_BRIDGE_URL` at it, and set
 * `ORIGIN`/`BETTER_AUTH_URL` to the preview origin (`http://localhost:4173`) so Better Auth
 * trusts it. See W7 in `plans/blazor-decommission-and-cleanup.md`.
 *
 * Plain `npm run test` skips it, so local and CI runs stay server-free unless the job opts in.
 */
const gameEnabled = process.env.E2E_GAME === '1';

const stamp = `${Date.now().toString(36)}${Math.floor(Math.random() * 1e4).toString(36)}`;
const account = {
	username: `e2e${stamp}`.slice(0, 30),
	email: `e2e+${stamp}@example.test`,
	password: 'E2e-password-123'
};
const characterName = `E2e${stamp}`.slice(0, 20);
const apiBase = (process.env.E2E_API_URL ?? 'http://localhost:9000').replace(/\/+$/, '');

test.describe('authenticated game loop', () => {
	test.skip(
		!gameEnabled,
		'needs a running Veldrath.Server — set E2E_GAME=1 (see plans/blazor-decommission-and-cleanup.md W7)'
	);

	test('register → roster → play → travel → inventory', async ({ page, request }) => {
		// 1. Register through the real form so Better Auth owns the session.
		await page.goto('/register');
		await page.locator('input[autocomplete="username"]').fill(account.username);
		await page.locator('input[autocomplete="email"]').fill(account.email);
		await page.locator('input[autocomplete="new-password"]').nth(0).fill(account.password);
		await page.locator('input[autocomplete="new-password"]').nth(1).fill(account.password);
		await page.getByRole('button', { name: /create account/i }).click();
		await page.waitForURL(/\/(game|characters)/, { timeout: 20_000 });

		// 2. Seed a character through the game API with this session's minted token. Creating a
		//    character exercises the same account provisioning path as the wizard without
		//    depending on seven wizard screens' markup. The token is fetched from inside the
		//    page so the Better Auth session cookie is guaranteed to be sent.
		const token = await page.evaluate(async () => {
			const response = await fetch('/api/game-token', { cache: 'no-store' });
			if (!response.ok) {
				throw new Error(`game-token request failed (${response.status}).`);
			}
			const payload = (await response.json()) as { token: string };
			return payload.token;
		});

		const created = await request.post(`${apiBase}/api/characters`, {
			headers: { authorization: `Bearer ${token}` },
			data: { name: characterName, className: 'Warrior', difficultyMode: 'normal' }
		});
		expect(created.status(), await created.text()).toBe(201);
		const character = (await created.json()) as { id: string; name: string };

		// 3. The roster is server-rendered through the real API. The name also appears in the
		//    header and the "continue as" banner, so target the character card heading.
		await page.goto('/characters');
		await expect(page.getByRole('heading', { name: character.name })).toBeVisible({
			timeout: 20_000
		});

		// 4. Enter the world: the hub connects, the character is selected, and the map renders.
		await page.goto(`/play/${character.id}`);
		const nodes = page.locator('.map-node-cell button');
		await expect(nodes.first()).toBeVisible({ timeout: 30_000 });

		const current = page.locator('button[aria-current="location"]');
		await expect(current).toHaveCount(1);
		const currentNodeLabel = (await current.innerText()).trim();

		// 5. Travel to a neighbouring node. Selecting a node reveals a "Travel to …" action only
		//    when the edge is traversable, so probe the nodes until one offers it.
		let targetLabel: string | null = null;
		const nodeCount = await nodes.count();
		for (let index = 0; index < nodeCount; index += 1) {
			const node = nodes.nth(index);
			const label = (await node.innerText()).trim();
			if (label === currentNodeLabel) continue;

			await node.click();
			const travel = page.getByRole('button', { name: /^Travel to / });

			if (await travel.isVisible().catch(() => false)) {
				targetLabel = label;
				await travel.click();
				break;
			}
		}
		expect(targetLabel, 'no reachable neighbouring node in the map').not.toBeNull();

		// The current-node marker moves to the node we travelled to.
		await expect(page.locator('button[aria-current="location"]')).toContainText(
			targetLabel!.split('\n')[0]
		);

		// 6. Inventory opens and loads through the hub.
		await page.getByRole('button', { name: /^inventory$/i }).click();
		await expect(page.getByRole('region', { name: 'Inventory' })).toBeVisible();
	});
});
