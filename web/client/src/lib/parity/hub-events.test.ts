import { describe, expect, it } from 'vitest';
import { matchAll, readRepoFile, stripComments, walkFiles } from '#lib/testing/source';

/**
 * Hub event parity.
 *
 * The hub broadcasts `SendAsync("<Event>", …)`; [`GameSession`](../game/session.svelte.ts) reduces
 * those events into runes. An unsubscribed event is invisible: the server keeps sending it and the
 * player sees nothing, which is exactly how the moderation and staff broadcasts went missing.
 *
 * Every event must be either subscribed or listed below with a reason. The reason is the whole
 * point — it turns "we forgot" into "we decided".
 */

const hubSource = stripComments(readRepoFile('Veldrath.Server/Hubs/GameHub.cs'));
const sessionSource = readRepoFile('web/client/src/lib/game/session.svelte.ts');

/** Every event name the hub broadcasts. */
function broadcastEvents(): string[] {
	return [...new Set(matchAll(hubSource, /SendAsync\(\s*"(\w+)"/g).map(([name]) => name))].sort();
}

/** Every event the game shell subscribes to, across all client modules. */
function subscriptions(): string[] {
	const subscribed = new Set<string>();

	for (const file of walkFiles('web/client/src', ['.ts', '.svelte'])) {
		if (/\.(test|spec)\.ts$/.test(file)) continue;
		if (file.includes('/lib/parity/') || file.includes('/lib/testing/')) continue;

		const source = readRepoFile(file);
		for (const [name] of matchAll(source, /\.on\(\s*['"](\w+)['"]/g)) {
			subscribed.add(name);
		}
	}

	return [...subscribed].sort();
}

/**
 * Broadcasts with no subscriber, and why.
 *
 * The deferred entries belong to the features listed as deferred in
 * `plans/web-feature-parity-and-coverage.md`; the other three are deliberate client design.
 */
const NO_SUBSCRIBER: Record<string, string> = {
	// Deferred features: the server broadcasts, the UI that would consume it does not exist.
	DungeonEntered: 'Deferred: dungeon UI does not exist yet.',

	// Deliberate: nothing for this client to render.
	EnemyEngaged:
		'Third-party combat: the client only renders its own fight, so another player engaging is noise.',
	CharacterStatusChanged:
		'The roster is server-rendered per request; multi-tab character status is not shown.',
	ZoneLocationUnlocked:
		'Discoveries arrive on the NodeTraveled and AreaSearched payloads, which the session already applies.',
	OnWhisper:
		'Legacy slash-command whisper path; SendWhisper echoes through ReceiveChatMessage instead.'
};

describe('hub event parity', () => {
	const events = broadcastEvents();
	const subscribed = subscriptions();

	it('finds broadcasts and subscriptions', () => {
		expect(events.length).toBeGreaterThan(40);
		expect(events).toContain('InventoryLoaded');
		expect(subscribed.length).toBeGreaterThan(30);
		expect(subscribed).toContain('InventoryLoaded');
	});

	it('subscribes only to events the hub actually broadcasts', () => {
		const unknown = subscribed.filter((event) => !events.includes(event));

		expect(unknown, 'session subscribes to an event GameHub.cs never sends').toEqual([]);
	});

	it('subscribes to every broadcast unless it is documented as intentionally ignored', () => {
		const missing = events.filter(
			(event) => !subscribed.includes(event) && !(event in NO_SUBSCRIBER)
		);

		expect(missing, 'subscribe in session.svelte.ts, or document why the event is ignored').toEqual(
			[]
		);
	});

	it('keeps the ignore list honest', () => {
		const stale = Object.keys(NO_SUBSCRIBER).filter((event) => !events.includes(event));
		const nowHandled = Object.keys(NO_SUBSCRIBER).filter((event) => subscribed.includes(event));

		expect(stale, 'ignore list names an event the hub no longer broadcasts').toEqual([]);
		expect(nowHandled, 'ignore list names an event the session now subscribes to').toEqual([]);
	});

	it('gives a reason for every ignored event', () => {
		const undocumented = Object.entries(NO_SUBSCRIBER)
			.filter(([, reason]) => reason.trim().length < 10)
			.map(([event]) => event);

		expect(undocumented).toEqual([]);
	});

	it('routes chat through SendChatMessage so slash commands work', () => {
		expect(sessionSource).toContain("invoke('SendChatMessage'");
		expect(sessionSource).toContain("invoke<ChatCommandInfo[]>('GetChatCommands'");
	});
});
