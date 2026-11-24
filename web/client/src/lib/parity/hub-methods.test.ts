import { describe, expect, it } from 'vitest';
import { matchAll, readRepoFile, stripComments, walkFiles } from '#lib/testing/source';

/**
 * Hub method parity.
 *
 * [`GameHub.cs`](../../../../../Veldrath.Server/Hubs/GameHub.cs) exposes game operations over
 * SignalR; the browser calls them through `hub.invoke`. Nothing generates one side from the other,
 * so a renamed or deleted hub method only shows up as a runtime `Error: Failed to invoke …`.
 *
 * The allow-list below is the deliberate part: a hub method may exist without a web caller only
 * for a documented reason. Everything else must be invoked, and every invocation must exist.
 */

const hubSource = stripComments(readRepoFile('Veldrath.Server/Hubs/GameHub.cs'));

/** Every public method declared on the hub, ignoring primary constructors of the DTO records. */
function serverHubMethods(): string[] {
	const methods: string[] = [];

	for (const match of hubSource.matchAll(/public\s+([^;{()]*?)\(/g)) {
		const declaration = match[1];

		// `public record Foo(...)` and `public class Bar(...)` are DTO declarations, not hub methods.
		if (/\b(record|class|struct|interface|enum)\b/.test(declaration)) continue;

		const name = /(\w+)\s*$/.exec(declaration)?.[1];
		// The constructor is public too, and it is not callable over the wire.
		if (!name || name === 'GameHub') continue;

		methods.push(name);
	}

	return [...new Set(methods)];
}

/** Every `hub.invoke('Name')` / `connection.invoke<T>('Name')` in shipped client code. */
function clientInvocations(): string[] {
	const invoked = new Set<string>();

	for (const file of walkFiles('web/client/src', ['.ts', '.svelte'])) {
		if (/\.(test|spec)\.ts$/.test(file)) continue;
		if (file.includes('/lib/parity/') || file.includes('/lib/testing/')) continue;

		const source = readRepoFile(file);
		for (const [name] of matchAll(source, /\.invoke(?:<[^>]*>)?\(\s*['"](\w+)['"]/g)) {
			invoked.add(name);
		}
	}

	return [...invoked].sort();
}

/**
 * Hub methods with no web caller, and why.
 *
 * Adding an entry is a decision: it says "this operation is not reachable from the SvelteKit
 * client on purpose". The deferred entries become real work when their features land.
 */
const NO_WEB_CALLER: Record<string, string> = {
	OnConnectedAsync: 'SignalR lifecycle override, called by the framework.',
	OnDisconnectedAsync: 'SignalR lifecycle override, called by the framework.',
	Ping: 'Diagnostic round-trip; the UI has no latency display.',
	GetActiveCharacters:
		'Multi-client diagnostic; the roster is server-rendered, so the UI never needs the live list.',

	// Server-authoritative commands the web UI never calls. They remain hub methods; the client only reads the
	// server's authoritative values, so no UI path needs them.
	AddGold: 'Awarded by loot and quest handlers, never by the browser.',
	AwardSkillXp: 'Awarded by combat and quest handlers, never by the browser.',

	// Deferred features: the hub bridge exists, the UI does not. See the plan file.
	EnterDungeon: 'Deferred: dungeon UI does not exist yet.',

	// Navigation is fully covered by the map graph.
	NavigateToLocation:
		'Zone locations move through the map graph; TravelToNode returns the destination location.',
	UnlockZoneLocation:
		'Locations unlock server-side inside NavigateToLocation and SearchArea, so no client call is needed.'
};

describe('hub method parity', () => {
	const server = serverHubMethods();
	const client = clientInvocations();

	it('finds hub methods and client invocations', () => {
		expect(server.length).toBeGreaterThan(40);
		expect(server).toContain('SelectCharacter');
		expect(client.length).toBeGreaterThan(30);
		expect(client).toContain('SelectCharacter');
	});

	it('invokes only methods the hub actually declares', () => {
		const unknown = client.filter((method) => !server.includes(method));

		expect(unknown, 'client invokes a hub method that GameHub.cs does not declare').toEqual([]);
	});

	it('invokes every hub method unless it is documented as server-only', () => {
		const missing = server.filter(
			(method) => !client.includes(method) && !(method in NO_WEB_CALLER)
		);

		expect(
			missing,
			'add the invocation to the game client, or document why it has no web caller'
		).toEqual([]);
	});

	it('keeps the no-caller allow-list honest', () => {
		const stale = Object.keys(NO_WEB_CALLER).filter((method) => !server.includes(method));
		const nowCalled = Object.keys(NO_WEB_CALLER).filter((method) => client.includes(method));

		expect(stale, 'allow-list names a hub method that no longer exists').toEqual([]);
		expect(nowCalled, 'allow-list names a hub method the client now calls').toEqual([]);
	});

	it('gives a reason for every server-only method', () => {
		const undocumented = Object.entries(NO_WEB_CALLER)
			.filter(([, reason]) => reason.trim().length < 10)
			.map(([method]) => method);

		expect(undocumented).toEqual([]);
	});
});
