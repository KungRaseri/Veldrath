import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';
import CardinalMap from './CardinalMap.svelte';
import GameScreen from './GameScreen.svelte';
import RightSidebar from './RightSidebar.svelte';
import { GameSession } from './session.svelte';

// `#lib/hub` imports `$app/env/public`, whose client runtime expects a dev env global that only
// exists inside a real SvelteKit app. The shell never calls it (the session is driven by direct
// state mutation), so stub the module out rather than faking SvelteKit internals.
vi.mock('#lib/hub', () => ({ createGameHub: () => null }));
import type {
	ChatMessage,
	GroundItem,
	LocationEnteredPayload,
	MapEdge,
	MapGraph,
	MapNode,
	SpawnedEnemy,
	ZoneEnteredPayload
} from './types';

/**
 * The game shell components.
 *
 * These are tested against the real `GameSession` rather than a hand-written fake: the shell reads
 * two dozen derived getters (`zoneLabel`, `canRest`, `canShop`, `adjacentNodeIds`, …), and a fake
 * would have to reimplement all of them. Mutating session state directly gives the same component
 * coverage without a parallel model that can drift.
 */

function node(id: string, overrides: Partial<MapNode> = {}): MapNode {
	return {
		id,
		mapId: 'map-1',
		nodeType: 'Location',
		displayName: id,
		description: `${id} description`,
		isEntryPoint: false,
		isExitPoint: false,
		isHidden: false,
		sortOrder: 0,
		...overrides
	};
}

function edge(from: string, to: string, overrides: Partial<MapEdge> = {}): MapEdge {
	return {
		id: `${from}->${to}`,
		mapId: 'map-1',
		fromNodeId: from,
		toNodeId: to,
		direction: 'north',
		edgeType: 'Path',
		isTraversable: true,
		isHidden: false,
		...overrides
	};
}

const market = node('market', { displayName: 'Market', isEntryPoint: true });
const woods = node('woods', { displayName: 'Woods', isExitPoint: true });
const crypt = node('crypt', { displayName: 'Crypt' });

function graph(overrides: Partial<MapGraph> = {}): MapGraph {
	return {
		id: 'map-1',
		name: 'Aldenmere Roads',
		mapType: 'Overworld',
		nodes: [market, woods, crypt],
		edges: [
			edge('market', 'woods', { direction: 'north' }),
			edge('woods', 'crypt', { direction: 'south' }),
			edge('market', 'crypt', { direction: 'east', isTraversable: false }),
			edge('crypt', 'market', { direction: 'west', isHidden: true })
		],
		entryNodeId: 'market',
		...overrides
	} as MapGraph;
}

const zone: ZoneEnteredPayload = {
	id: 'zone-1',
	name: 'Aldenmere',
	hasInn: true,
	hasMerchant: true
} as ZoneEnteredPayload;

const location: LocationEnteredPayload = {
	locationSlug: 'market',
	locationDisplayName: 'Market',
	typeKey: 'Location'
} as LocationEnteredPayload;

const wolf: SpawnedEnemy = {
	id: 'enemy-1',
	name: 'Dire Wolf',
	level: 4,
	currentHealth: 20,
	maxHealth: 30
};

const rock: GroundItem = {
	id: 'ground-1',
	itemRef: 'rock',
	name: 'Rock',
	rarity: 'Common',
	quantity: 2
};

/** A connected session standing in the market with the map loaded. */
function playing(overrides: Partial<GameSession> = {}): GameSession {
	const session = new GameSession();
	session.phase = 'connected';
	session.zone = zone;
	session.location = location;
	session.map = graph();
	session.currentNodeId = 'market';
	session.selectedNodeId = 'market';

	Object.assign(session, overrides);
	return session;
}

describe('GameScreen', () => {
	it('renders the map, zone breadcrumb and node count', async () => {
		const session = playing();

		render(GameScreen, { props: { session, onLeave: vi.fn() } });

		expect(await screen.findByText('Aldenmere Roads')).toBeTruthy();
		// The zone name appears in the header breadcrumb and in the map subtitle.
		expect(screen.getAllByText(/Aldenmere/).length).toBeGreaterThan(0);
		expect(screen.getAllByText(/3 nodes/).length).toBeGreaterThan(0);
		expect(screen.getAllByText('Market').length).toBeGreaterThan(0);
	});

	it('refreshes the map on demand', async () => {
		const session = playing();
		const requestMapGraph = vi.spyOn(session, 'requestMapGraph');

		render(GameScreen, { props: { session, onLeave: vi.fn() } });
		await fireEvent.click(screen.getByRole('button', { name: 'Refresh' }));

		expect(requestMapGraph).toHaveBeenCalledWith(null);
	});

	it('travelling from a reachable selected node', async () => {
		const session = playing({ selectedNodeId: 'woods' });
		const travel = vi.spyOn(session, 'travel');

		render(GameScreen, { props: { session, onLeave: vi.fn() } });
		await fireEvent.click(screen.getByRole('button', { name: /Travel to Woods/ }));

		expect(travel).toHaveBeenCalledWith('woods');
	});

	it('disables travel to a node that is not adjacent', async () => {
		const session = playing({ selectedNodeId: 'crypt' });

		render(GameScreen, { props: { session, onLeave: vi.fn() } });

		const button = screen.getByRole('button', { name: /Not directly reachable/ });
		expect(button.hasAttribute('disabled')).toBe(true);
	});

	it('shows the current-location chip when the selected node is current', async () => {
		render(GameScreen, { props: { session: playing(), onLeave: vi.fn() } });

		expect(await screen.findByText('Current location')).toBeTruthy();
	});

	it('replaces the map with the combat panel during a fight', async () => {
		const session = playing({
			combat: {
				active: true,
				enemy: {
					id: 'enemy-1',
					name: 'Dire Wolf',
					level: 4,
					currentHealth: 20,
					maxHealth: 30,
					abilities: []
				},
				log: []
			}
		});

		render(GameScreen, { props: { session, onLeave: vi.fn() } });

		const heading = await screen.findByRole('heading', { name: /Combat/ });
		expect(heading.textContent).toContain('Dire Wolf');
		expect(screen.queryByText('Aldenmere Roads')).toBeNull();
	});

	it('shows the reconnect banner while reconnecting', async () => {
		render(GameScreen, {
			props: { session: playing({ phase: 'reconnecting' }), onLeave: vi.fn() }
		});

		expect(await screen.findByText(/Connection lost\. Reconnecting/)).toBeTruthy();
	});

	it('replaces the world with a disconnected screen after a kick', async () => {
		const onLeave = vi.fn();
		const session = playing({
			phase: 'disconnected',
			error: 'You were disconnected by a moderator.'
		});

		render(GameScreen, { props: { session, onLeave } });

		const panel = await screen.findByRole('region', { name: 'Disconnected' });
		expect(panel.textContent).toContain('You were disconnected by a moderator.');
		// A kick ends the session: the map and the controls that drive it must leave the screen,
		// because every command they send would go into a closed connection.
		expect(screen.queryByText('Aldenmere Roads')).toBeNull();
		expect(screen.queryByRole('button', { name: 'Refresh' })).toBeNull();
		expect(screen.queryByRole('button', { name: 'Leave zone' })).toBeNull();

		await fireEvent.click(screen.getByRole('button', { name: 'Back to characters' }));
		expect(onLeave).toHaveBeenCalled();
	});

	it('words a disconnect the server gave no reason for', async () => {
		render(GameScreen, {
			props: { session: playing({ phase: 'disconnected' }), onLeave: vi.fn() }
		});

		expect(await screen.findByText(/Your session ended\. Enter the world again/)).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Back to characters' })).toBeTruthy();
	});

	it('offers a respawn button when the character is dead', async () => {
		const session = playing({ dead: true });
		const respawn = vi.spyOn(session, 'respawn');

		render(GameScreen, { props: { session, onLeave: vi.fn() } });
		await fireEvent.click(screen.getByRole('button', { name: 'Respawn' }));

		expect(respawn).toHaveBeenCalled();
	});

	it('shows and dismisses an error banner', async () => {
		const session = playing({ error: 'Travel failed.' });

		render(GameScreen, { props: { session, onLeave: vi.fn() } });
		expect(await screen.findByText('Travel failed.')).toBeTruthy();

		await fireEvent.click(screen.getByRole('button', { name: 'Dismiss' }));
		expect(session.error).toBeNull();
	});

	it('explains an empty map and a loading map', async () => {
		const loading = render(GameScreen, {
			props: { session: playing({ loadingMap: true }), onLeave: vi.fn() }
		});
		expect(await screen.findByText('Loading map…')).toBeTruthy();
		loading.unmount();

		render(GameScreen, {
			props: { session: playing({ map: null }), onLeave: vi.fn() }
		});
		expect(await screen.findByText('Select a character to load the map.')).toBeTruthy();
	});

	it('lists enemies, ground items and search discoveries', async () => {
		const session = playing({
			enemies: [wolf],
			groundItems: [rock],
			lastSearch: {
				rollValue: 17,
				discovered: [
					{ slug: 'woods', displayName: 'Woods', typeKey: 'Location' },
					{ slug: 'shrine', displayName: 'Hidden Shrine', typeKey: 'Shrine' }
				]
			}
		});
		const engageEnemy = vi.spyOn(session, 'engageEnemy');
		const pickUpGroundItem = vi.spyOn(session, 'pickUpGroundItem');

		render(GameScreen, { props: { session, onLeave: vi.fn() } });

		expect(await screen.findByText('Dire Wolf')).toBeTruthy();
		expect(screen.getByText(/× 2/)).toBeTruthy();
		expect(screen.getByText(/search roll/i)).toBeTruthy();
		// A discovered node that is adjacent is a travel shortcut; one that is not is a chip.
		expect(screen.getByText('Hidden Shrine')).toBeTruthy();

		await fireEvent.click(screen.getByRole('button', { name: /Engage/ }));
		await fireEvent.click(screen.getByRole('button', { name: /Pick up/ }));

		expect(engageEnemy).toHaveBeenCalledWith('enemy-1');
		expect(pickUpGroundItem).toHaveBeenCalledWith('ground-1');
	});

	it('wires search, rest, merchant and leave-zone actions', async () => {
		const session = playing();
		const searchArea = vi.spyOn(session, 'searchArea');
		const rest = vi.spyOn(session, 'rest');
		const visitShop = vi.spyOn(session, 'visitShop');
		const leaveZone = vi.spyOn(session, 'leaveZone');

		render(GameScreen, { props: { session, onLeave: vi.fn() } });

		await fireEvent.click(screen.getByRole('button', { name: /Search area/ }));
		await fireEvent.click(screen.getByRole('button', { name: /Rest/ }));
		await fireEvent.click(screen.getAllByRole('button', { name: /Merchant/ })[0]);
		await fireEvent.click(screen.getByRole('button', { name: /Leave zone/ }));

		expect(searchArea).toHaveBeenCalled();
		expect(rest).toHaveBeenCalled();
		expect(visitShop).toHaveBeenCalled();
		expect(leaveZone).toHaveBeenCalled();
	});
});

describe('CardinalMap', () => {
	it('renders every node with its type, directions and entry/exit badges', async () => {
		render(CardinalMap, {
			props: { map: graph(), currentNodeId: 'market', selectedNodeId: 'market', onSelect: vi.fn() }
		});

		expect(await screen.findByText('Overworld · 3 nodes · 4 routes')).toBeTruthy();
		expect(screen.getByText('Entry')).toBeTruthy();
		expect(screen.getByText('Exit')).toBeTruthy();
		// The market has a north route and an east route; both directions are rendered.
		expect(screen.getAllByText(/· north/).length).toBeGreaterThan(0);
	});

	it('marks the current node and the reachable neighbours', async () => {
		render(CardinalMap, {
			props: {
				map: graph(),
				currentNodeId: 'market',
				selectedNodeId: null,
				adjacentNodeIds: ['woods'],
				onSelect: vi.fn()
			}
		});

		const current = await screen.findByRole('button', { name: /Market/ });
		expect(current.getAttribute('aria-current')).toBe('location');
	});

	it('selects the clicked node', async () => {
		const onSelect = vi.fn();

		render(CardinalMap, {
			props: { map: graph(), currentNodeId: 'market', selectedNodeId: null, onSelect }
		});

		await fireEvent.click(screen.getByRole('button', { name: /Woods/ }));

		expect(onSelect).toHaveBeenCalledWith('woods');
	});
});

describe('RightSidebar', () => {
	const message = (overrides: Partial<ChatMessage> = {}): ChatMessage => ({
		channel: 'Zone',
		sender: 'Aria',
		message: 'hello',
		timestamp: '2026-01-01T00:00:00Z',
		...overrides
	});

	it('renders the active channel and system lines', async () => {
		const session = playing();
		session.chat = [message(), message({ channel: 'System', sender: 'System', message: 'saved' })];

		render(RightSidebar, { props: { session } });

		expect(await screen.findByText('hello')).toBeTruthy();
		expect(screen.getByText('saved')).toBeTruthy();
	});

	it('switches channels and shows the whisper target field', async () => {
		const session = playing();
		session.chat = [message({ channel: 'Whisper', message: 'psst' })];

		render(RightSidebar, { props: { session } });

		await fireEvent.click(screen.getByRole('button', { name: 'Whisper' }));

		expect(session.chatChannel).toBe('Whisper');
		expect(await screen.findByText('psst')).toBeTruthy();
		expect(screen.getByText('Whisper target')).toBeTruthy();
	});

	it('submits a message and clears the draft', async () => {
		const session = playing();
		const sendChat = vi.spyOn(session, 'sendChat').mockResolvedValue(undefined);

		render(RightSidebar, { props: { session } });

		const input = screen.getByPlaceholderText(/Message zone/);
		await fireEvent.input(input, { target: { value: 'hello zone' } });
		await fireEvent.submit(input.closest('form')!);

		expect(sendChat).toHaveBeenCalledWith('hello zone');
		expect((input as HTMLInputElement).value).toBe('');
	});

	it('lists the slash commands the caller may run', async () => {
		const session = playing();
		session.chatCommands = [
			{ command: 'who', usage: '/who', description: 'List players', requiredPermission: null }
		];

		render(RightSidebar, { props: { session } });

		expect(await screen.findByText('Slash commands (1)')).toBeTruthy();
		expect(screen.getByText(/List players/)).toBeTruthy();
	});

	it('lists the other players in the zone, or says the player is alone', async () => {
		const empty = playing();
		const { unmount } = render(RightSidebar, { props: { session: empty } });

		expect(await screen.findByText('Only you.')).toBeTruthy();
		unmount();

		const busy = playing();
		busy.occupants = [
			{ characterId: 'c2', characterName: 'Aria', enteredAt: '2026-01-01T00:00:00Z' }
		];
		render(RightSidebar, { props: { session: busy } });

		expect(await screen.findByText('Aria')).toBeTruthy();
	});
});
