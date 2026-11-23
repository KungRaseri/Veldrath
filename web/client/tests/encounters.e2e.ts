import { randomUUID } from 'node:crypto';
import { expect, test, type APIRequestContext, type Browser, type Page } from '@playwright/test';
import {
	apiBase,
	gameToken,
	makeAccount,
	register,
	stackEnabled,
	stackSkipReason
} from './support/stack';

/**
 * Combat and death, driven through the real client.
 *
 * The world's spawn pools are real content now, but which location has which enemy is a content decision that will
 * keep changing — so these specs use `the-droveway`, the level 1–4 wilderness next to the starter town: its
 * `drove-road` draws its roster from the zone's spawn pool, and its `weathered-waypost` is the zone's `SafeRespawn`
 * point.
 *
 * The dev teleport only moves the character into the zone (the `playground` zone is hidden from the public API, so a
 * client cannot open it). Everything after that is the production path: the roster is spawned by walking onto the
 * location's map node, and the death lands on the zone's safe location.
 */
test.describe.configure({ mode: 'serial' });

test.describe('combat and death', () => {
	test.skip(!stackEnabled, stackSkipReason);

	/**
	 * Walks to a map node, one hop at a time.
	 *
	 * A zone's entry node is linked to its exits, and the exits are linked to the locations — so a location is
	 * usually two hops from where a character arrives. Reaching the node is what makes the server spawn the
	 * location's roster.
	 */
	async function walkTo(page: Page, nodeName: string) {
		for (let hop = 0; hop < 6; hop++) {
			await page.locator('.map-node-cell button').filter({ hasText: nodeName }).first().click();

			const travel = page.getByRole('button', { name: `Travel to ${nodeName}` });
			if (await travel.isVisible().catch(() => false)) {
				await travel.click();
				// The location breadcrumb only changes once the arrival payload has been applied.
				await expect(page.getByText('Routes').first()).toBeVisible({ timeout: 20_000 });
				return;
			}

			// Not adjacent yet: walk to the first route the map offers and try again from there.
			await page
				.getByRole('button', { name: /^(Road|Path|Track|Trail) to / })
				.first()
				.click();
			await page.waitForTimeout(1_500);
		}

		throw new Error(`could not walk to ${nodeName} within six hops`);
	}

	/** A level-1 character standing on the drove road with a live enemy roster. */
	async function characterOnTheRoad(browser: Browser, request: APIRequestContext) {
		const context = await browser.newContext();
		const page = await context.newPage();
		await register(page, makeAccount('e2efight'));

		const token = await gameToken(page);
		const name = `Fight${randomUUID()
			.replace(/[^a-z]/g, '')
			.slice(0, 6)}`;
		const created = await request.post(`${apiBase}/api/characters`, {
			headers: { authorization: `Bearer ${token}` },
			data: { name, className: 'Warrior', difficultyMode: 'normal' }
		});
		expect(created.status(), await created.text()).toBe(201);
		const character = (await created.json()) as { id: string };

		const teleported = await request.post(`${apiBase}/api/dev/teleport`, {
			headers: { authorization: `Bearer ${token}` },
			data: { characterId: character.id, zoneId: 'the-droveway', locationSlug: null }
		});
		expect(teleported.status(), await teleported.text()).toBe(200);

		await page.goto(`/play/${character.id}`);
		// The map renders only once the hub has connected and pushed its first snapshot.
		await expect(page.locator('.map-node-cell button').first()).toBeVisible({ timeout: 30_000 });

		await walkTo(page, 'The Drove Road');

		return { context, page };
	}

	test('walking into a seeded location spawns a roster and the fight can be won', async ({
		browser,
		request
	}) => {
		// Registering, creating a character, loading the world and two map hops already take most of the default
		// budget before any combat starts.
		test.setTimeout(120_000);

		const { context, page } = await characterOnTheRoad(browser, request);

		// The HUD's "Local" panel lists what spawned; the button is the engage action.
		const engageButtons = page.getByRole('button', { name: 'Engage' });
		await expect(engageButtons.first()).toBeVisible({ timeout: 30_000 });
		const hostilesBefore = await engageButtons.count();

		await engageButtons.first().click();

		const combat = page.getByRole('heading', { name: /Combat/ });
		await expect(combat).toBeVisible({ timeout: 20_000 });
		await expect(page.getByText(/Combat started: /)).toBeVisible({ timeout: 20_000 });

		// Attack until the fight ends. A level-1 character carries the engine's attribute blob (Con*10 + level*5 HP),
		// so the starter band is winnable — before the endpoint wrote that blob it had 10 HP and lost on turn one.
		const attack = page.getByRole('button', { name: /^Attack$/ });
		for (let round = 0; round < 40; round++) {
			if (!(await combat.isVisible())) break;
			await attack.click();
			await page.waitForTimeout(400);
		}

		await expect(combat).toBeHidden({ timeout: 20_000 });

		// Either that enemy left the roster or the location is cleared — both mean the kill landed.
		const hostilesAfter = await page.getByRole('button', { name: 'Engage' }).count();
		const cleared = await page
			.getByText('Nothing hostile is lurking here right now.')
			.isVisible()
			.catch(() => false);

		expect(cleared || hostilesAfter < hostilesBefore).toBe(true);

		await context.close();
	});

	test('dying on the road respawns the character at the zone’s safe location', async ({
		browser,
		request
	}) => {
		test.setTimeout(150_000);

		const { context, page } = await characterOnTheRoad(browser, request);

		await page.getByRole('button', { name: 'Engage' }).first().click();
		await expect(page.getByRole('heading', { name: /Combat/ })).toBeVisible({ timeout: 20_000 });

		// Defend rather than attack: the point is to lose, and a level-1 warrior cannot outlast a clearing full of
		// level 1–6 actors by trading blows.
		// Two places say it — the shell's banner and the combat log — so match the first.
		const defeat = page.getByText('You have been defeated.').first();
		const defend = page.getByRole('button', { name: /^Defend$/ });
		for (let round = 0; round < 80; round++) {
			if (await defeat.isVisible().catch(() => false)) break;
			await defend.click();
			await page.waitForTimeout(300);
		}

		await expect(defeat).toBeVisible({ timeout: 30_000 });

		await page.getByRole('button', { name: 'Respawn' }).click();

		// The Respawn button is the honest signal: the combat log keeps its "You have been defeated." line for the
		// rest of the session, so asserting on that text would only ever be satisfied by the banner being replaced.
		await expect(page.getByRole('button', { name: 'Respawn' })).toBeHidden({ timeout: 20_000 });

		// The droveway's SafeRespawn location is the weathered waypost, so a death anywhere in the zone comes back here.
		await expect(page.getByText('The Weathered Waypost').first()).toBeVisible({ timeout: 20_000 });

		await context.close();
	});
});
