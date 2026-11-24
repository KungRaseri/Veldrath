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

	/**
	 * A level-1 character standing on the drove road with a live enemy roster.
	 *
	 * `difficultyMode` is the character's own mode and therefore its zone group: a wilderness scope is
	 * `zone:{zoneId}_{mode}`, so a `hardcore` character joins a roster the `normal` one cannot see. That is how a
	 * spec that kills gets a roster of its own instead of emptying the one `a1-the-droveway.e2e.ts` counts rows in.
	 */
	async function characterOnTheRoad(
		browser: Browser,
		request: APIRequestContext,
		difficultyMode: 'normal' | 'hardcore' = 'normal'
	) {
		const context = await browser.newContext();
		const page = await context.newPage();
		await register(page, makeAccount('e2efight'));

		const token = await gameToken(page);
		const name = `Fight${randomUUID()
			.replace(/[^a-z]/g, '')
			.slice(0, 6)}`;
		const created = await request.post(`${apiBase}/api/characters`, {
			headers: { authorization: `Bearer ${token}` },
			data: { name, className: 'Warrior', difficultyMode }
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

		// Hardcore, so the kill this spec lands sits in `zone:the-droveway_hardcore`. `a1-the-droveway.e2e.ts` runs
		// in the same stack and asserts the drove road spawned at least two rows in the group its character joined — a
		// pool draw here rolls 2–4 rows (`ActorPoolResolver.MinCount`/`MaxCount`), so when it rolls 2 one kill in the
		// shared `_normal` roster would take it below that until the 2s respawn.
		const { context, page } = await characterOnTheRoad(browser, request, 'hardcore');

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

		const combat = page.getByRole('heading', { name: /Combat/ });
		// Two places say it — the shell's banner and the combat log — so match the first.
		const defeat = page.getByText('You have been defeated.').first();
		const defend = page.getByRole('button', { name: /^Defend$/ });
		const engage = page.getByRole('button', { name: 'Engage' });

		/**
		 * Steps off the drove road and back onto it, so the client re-reads the location's roster.
		 *
		 * The Local panel only learns a roster on arrival, so a row that died under this journey stays painted until
		 * the client re-enters. The respawn sweeper revives it on its own clock (2s in the isolated stack), so the
		 * wait is what gives a cleared road its roster back.
		 */
		async function reenterTheRoad() {
			await walkTo(page, 'Arrival');
			await walkTo(page, 'The Drove Road');
			await page.waitForTimeout(1_500);
		}

		await engage.first().click();
		await expect(combat).toBeVisible({ timeout: 20_000 });

		// Defend rather than attack: the point is to lose, and a level-1 warrior cannot outlast a clearing full of
		// level 1–6 actors by trading blows.
		//
		// `normal`, unlike the two specs that kill: hardcore never respawns, and this journey is the respawn test.
		// Sharing the roster costs nothing here because defending removes no row — `a1-the-droveway.e2e.ts` can kill
		// one under this journey, and that is handled below.
		//
		// The fight can also end without the character dying. A wilderness zone has one roster per zone scope, so
		// `a1-the-droveway.e2e.ts` fights these same Drove Road rows in the same run; a kill there releases every
		// combat session pointed at that enemy, this journey's included, and the server says so with `CombatEnded`.
		// That is a clean end, not a lost journey: engage whatever is still standing and keep defending. A shared
		// kill never heals, so the walk to 0 HP stays monotonic.
		for (let round = 0; round < 80; round++) {
			if (await defeat.isVisible().catch(() => false)) break;

			if (await defend.isVisible().catch(() => false)) {
				await defend.click();
				await page.waitForTimeout(300);
				continue;
			}

			if (
				await engage
					.first()
					.isVisible()
					.catch(() => false)
			) {
				await engage.first().click();
				await page.waitForTimeout(1_000);

				// A row can die between the panel painting and this click (another character's kill), and the client
				// only learns that by re-reading the location. That is not a failure — it is the branch below.
				if (!(await combat.isVisible().catch(() => false))) await reenterTheRoad();
				continue;
			}

			// Nothing left to fight and no panel: the road was cleared under this journey.
			await reenterTheRoad();
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

	test('a kill by another character ends the fight here with a clean ending', async ({
		browser,
		request
	}) => {
		test.setTimeout(180_000);

		// The character whose fight somebody else will finish. It stands still: the enemy only counter-attacks on a
		// turn, so an idle fighter takes no damage and its session stays exactly as engaged.
		//
		// Both characters of this spec are `hardcore`, which gives it a roster of its own: a wilderness scope is
		// `zone:{zoneId}_{difficultyMode}`, no other spec in the suite creates a hardcore droveway character, and the
		// pool draw that fills this roster is therefore untouched by anyone else's kill — and touches no one else's.
		// Neither character dies here, so hardcore's permanent death never comes into it.
		const defender = await characterOnTheRoad(browser, request, 'hardcore');
		const defenderCombat = defender.page.getByRole('heading', { name: /Combat/ });
		await defender.page.getByRole('button', { name: 'Engage' }).first().click();
		await expect(defenderCombat).toBeVisible({ timeout: 20_000 });

		// A second character on the same roster — `zone:the-droveway_hardcore` — so its Local panel lists the very
		// row the first character is fighting, in the same server order (spawn instant, then id), which is why the
		// first Engage button on each page is the same enemy.
		const killer = await characterOnTheRoad(browser, request, 'hardcore');

		// Level the killer through the sanctioned dev preparation path so this spec does not depend on a level-1
		// character outlasting a level 1–6 draw: at level 10 a blow lands for 10 and the fight is over in a few turns.
		const killerId = killer.page.url().split('/').pop() ?? '';
		const prepared = await request.post(`${apiBase}/api/dev/experience`, {
			headers: { authorization: `Bearer ${await gameToken(killer.page)}` },
			data: { characterId: killerId, amount: 4500, source: 'encounters shared-kill spec' }
		});
		expect(prepared.status(), await prepared.text()).toBe(200);

		await killer.page.getByRole('button', { name: 'Engage' }).first().click();
		const killerCombat = killer.page.getByRole('heading', { name: /Combat/ });
		await expect(killerCombat).toBeVisible({ timeout: 20_000 });

		const attack = killer.page.getByRole('button', { name: /^Attack$/ });
		for (let round = 0; round < 40; round++) {
			if (!(await killerCombat.isVisible().catch(() => false))) break;
			await attack.click();
			await killer.page.waitForTimeout(400);
		}
		await expect(killerCombat).toBeHidden({ timeout: 20_000 });

		// The killing blow released this character's combat session too. Nothing clicks here — the server has to tell
		// the losing client its fight is over, or the panel stays up for a fight that no longer exists and the next
		// Defend is answered "Not in combat".
		await expect(defenderCombat).toBeHidden({ timeout: 20_000 });
		await expect(defender.page.getByText('Combat ended: shared kill.').first()).toBeVisible({
			timeout: 20_000
		});

		// A shared kill is not a death: the character is still standing and lost nothing.
		await expect(defender.page.getByText('You have been defeated.')).toBeHidden();

		await defender.context.close();
		await killer.context.close();
	});
});
