import { fireEvent, render, screen, within } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import GameScreen from './GameScreen.svelte';
import { GameSession } from './session.svelte';
import type {
	GroundItem,
	LocationEnteredPayload,
	MapEdge,
	MapGraph,
	MapNode,
	SearchFinding,
	SpawnedEnemy,
	ZoneEnteredPayload
} from './types';

// Every session action guards on `this.hub`, and `#lib/hub` pulls in `$app/env/public`, which
// needs a SvelteKit dev global Vitest does not install. A null factory keeps the shell mounted.
vi.mock('#lib/hub', () => ({ createGameHub: () => null }));

/**
 * The game shell's conditional arms.
 *
 * Every case renders against the real `GameSession` and mutates it directly, because the shell
 * reads derived values (`zoneLabel`, `adjacentNodeIds`, `focusNode`, `canRest`) that a fake would
 * have to reimplement. Cases are grouped by session state rather than by component region, since
 * one render of the shell exercises most of the panels at once.
 */

function mapNode(id: string, overrides: Partial<MapNode> = {}): MapNode {
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

function mapEdge(from: string, to: string, overrides: Partial<MapEdge> = {}): MapEdge {
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

const market = mapNode('market', { displayName: 'Market', isEntryPoint: true });
const woods = mapNode('woods', { displayName: 'Woods', isExitPoint: true });
const crypt = mapNode('crypt', { displayName: 'Crypt' });

function mapGraph(overrides: Partial<MapGraph> = {}): MapGraph {
	return {
		id: 'map-1',
		mapType: 'Overworld',
		ownerId: 'region-1',
		name: 'Aldenmere Roads',
		description: 'Roads between the market and the woods.',
		nodes: [market, woods, crypt],
		edges: [mapEdge('market', 'woods'), mapEdge('woods', 'crypt')],
		entryNodeId: 'market',
		...overrides
	} as MapGraph;
}

const zone: ZoneEnteredPayload = {
	id: 'zone-1',
	name: 'Aldenmere',
	description: 'A market town.',
	zoneType: 'Town',
	occupants: [],
	hasInn: true,
	hasMerchant: true
};

const location: LocationEnteredPayload = {
	locationSlug: 'market',
	locationDisplayName: 'Market',
	typeKey: 'Location'
} as LocationEnteredPayload;

/** A connected session standing in the market with the map loaded. */
function playing(): GameSession {
	const session = new GameSession();
	session.phase = 'connected';
	session.zone = { ...zone };
	session.location = { ...location };
	session.map = mapGraph();
	session.currentNodeId = 'market';
	session.selectedNodeId = 'market';
	return session;
}

/** The first `<section>` of the main column, which holds the map. */
function mapPanel(container: HTMLElement): HTMLElement {
	const panel = container.querySelector<HTMLElement>('main section');
	if (!panel) throw new Error('no map panel');
	return panel;
}

/** The `<section>` that lists the focus node, its routes and the local actions. */
function localPanel(container: HTMLElement): HTMLElement {
	const panel = [...container.querySelectorAll<HTMLElement>('main section')][2];
	if (!panel) throw new Error('no local panel');
	return panel;
}

beforeEach(() => {
	document.body.innerHTML = '';
	document.title = '';
});

describe('cold start', () => {
	it('renders the placeholders before a character, map or zone exists', () => {
		const session = new GameSession();
		render(GameScreen, { props: { session, onLeave: vi.fn() } });

		expect(document.title).toContain('Entering Veldrath');
		expect(screen.getByRole('heading', { name: 'Map' })).toBeTruthy();
		expect(screen.getAllByText('Region map').length).toBeGreaterThan(0);
		expect(screen.getByText('Waiting for the map…')).toBeTruthy();
		expect(screen.getByText('Move to a location to see what is here.')).toBeTruthy();
	});

	it('disables the local actions while no zone is loaded', () => {
		const session = new GameSession();
		render(GameScreen, { props: { session, onLeave: vi.fn() } });

		// `canRest` needs both an inn and a location, so the button keeps its short label.
		expect(screen.getByRole('button', { name: 'Rest' })).toBeTruthy();
		expect(
			(screen.getByRole('button', { name: /Search area/ }) as HTMLButtonElement).disabled
		).toBe(true);
		expect((screen.getByRole('button', { name: /Leave zone/ }) as HTMLButtonElement).disabled).toBe(
			true
		);
		expect(screen.queryByRole('button', { name: /Merchant/ })).toBeNull();
	});

	it('names the head title after the selected character', () => {
		const session = playing();
		session.character = { id: 'char-1', name: 'Kaleth', className: 'Warden' } as never;
		render(GameScreen, { props: { session, onLeave: vi.fn() } });

		expect(document.title).toContain('Kaleth');
	});
});

describe('breadcrumb', () => {
	it('falls back to the zone-map name when the map is not an overworld', () => {
		const session = new GameSession();
		session.map = mapGraph({
			mapType: 'Zone',
			name: 'Aldenmere Underworks',
			nodes: [market, crypt],
			edges: []
		});
		session.currentNodeId = 'market';
		session.selectedNodeId = 'market';
		render(GameScreen, { props: { session, onLeave: vi.fn() } });

		expect(screen.getByText('Aldenmere Underworks · 2 nodes')).toBeTruthy();
		expect(screen.getByText('Current location')).toBeTruthy();
		// An isolated node has no routes, so the whole Routes strip is skipped.
		expect(screen.queryByText('Routes')).toBeNull();
	});

	it('lists the entered location beside the zone name', () => {
		render(GameScreen, { props: { session: playing(), onLeave: vi.fn() } });

		expect(screen.getByText('Aldenmere · Market · 3 nodes')).toBeTruthy();
	});
});

describe('focus node and routes', () => {
	it('shows the reference of the selected node and offers the travel button', async () => {
		const session = playing();
		session.currentNodeId = 'woods';
		session.selectedNodeId = 'market';
		session.map = mapGraph({
			nodes: [mapNode('market', { displayName: 'Market', refId: 'loc-market' }), woods, crypt],
			edges: [mapEdge('woods', 'market'), mapEdge('woods', 'ghost')]
		});
		const travel = vi.spyOn(session, 'travel');
		const { container } = render(GameScreen, { props: { session, onLeave: vi.fn() } });

		expect(screen.getByRole('heading', { name: 'Selected node' })).toBeTruthy();
		expect(screen.getByText('Reference: loc-market')).toBeTruthy();
		// The route list holds one chip per adjacent node that resolves on the map; the edge to a
		// node that is not in the graph renders nothing.
		const focusPanel = [...container.querySelectorAll<HTMLElement>('main section')][1];
		const routes = within(focusPanel).getByText('Routes').parentElement!;
		expect(within(routes).getAllByRole('button')).toHaveLength(1);

		await fireEvent.click(within(routes).getByRole('button', { name: /Market/ }));
		await fireEvent.click(screen.getByRole('button', { name: 'Travel to Market' }));

		expect(travel).toHaveBeenCalledWith('market');
	});

	it('selects a node straight from the map', async () => {
		const session = playing();
		const selectNode = vi.spyOn(session, 'selectNode');
		const { container } = render(GameScreen, { props: { session, onLeave: vi.fn() } });

		await fireEvent.click(within(mapPanel(container)).getByRole('button', { name: /Crypt/ }));

		expect(selectNode).toHaveBeenCalledWith('crypt');
	});
});

describe('local panel', () => {
	it('offers a shortcut for a discovery that is adjacent and a chip for one that is not', async () => {
		const session = playing();
		session.map = mapGraph({ edges: [mapEdge('market', 'woods')] });
		session.lastSearch = {
			rollValue: 12,
			discovered: [
				{ slug: 'woods', displayName: 'Woods', typeKey: 'Location' },
				{ slug: 'shrine', displayName: 'Hidden Shrine', typeKey: 'Shrine' }
			]
		};
		const travel = vi.spyOn(session, 'travel');
		render(GameScreen, { props: { session, onLeave: vi.fn() } });

		expect(screen.getByText('Hidden Shrine')).toBeTruthy();
		const shortcut = screen.getByTitle('Travel to Woods');
		await fireEvent.click(shortcut);

		expect(travel).toHaveBeenCalledWith('woods');
	});

	it('wires the merchant entry from the action row', async () => {
		const session = playing();
		const visitShop = vi.spyOn(session, 'visitShop').mockResolvedValue(undefined);
		const { container } = render(GameScreen, { props: { session, onLeave: vi.fn() } });

		const merchant = within(localPanel(container)).getByRole('button', { name: /Merchant/ });
		await fireEvent.click(merchant);

		expect(visitShop).toHaveBeenCalled();
	});

	it('names the rest button after the current location', () => {
		render(GameScreen, { props: { session: playing(), onLeave: vi.fn() } });

		expect(screen.getByRole('button', { name: 'Rest (Market)' })).toBeTruthy();
	});

	it('hears no merchant when the zone has no shop', () => {
		const session = playing();
		session.zone = { ...zone, hasMerchant: false };
		const { container } = render(GameScreen, { props: { session, onLeave: vi.fn() } });

		expect(within(localPanel(container)).queryByRole('button', { name: /Merchant/ })).toBeNull();
		expect(within(localPanel(container)).getByRole('button', { name: /Search area/ })).toBeTruthy();
	});
});

describe('partial payloads', () => {
	it('renders empty placeholders instead of the word undefined', async () => {
		const session = playing();
		// A payload from an older server can omit names, levels and rolls.
		session.map = mapGraph({
			nodes: [mapNode('market', { displayName: undefined }), woods, crypt]
		});
		session.enemies = [
			{
				id: 'enemy-1',
				name: undefined,
				level: undefined,
				currentHealth: undefined,
				maxHealth: undefined
			} as unknown as SpawnedEnemy
		];
		session.groundItems = [
			{
				id: 'ground-1',
				itemRef: 'rock',
				name: undefined,
				rarity: 'Common',
				quantity: 1
			} as unknown as GroundItem
		];
		session.lastSearch = {
			rollValue: undefined as unknown as number,
			discovered: [{ slug: 'shrine', displayName: undefined, typeKey: undefined } as never]
		};
		const engage = vi.spyOn(session, 'engageEnemy');
		const { container } = render(GameScreen, { props: { session, onLeave: vi.fn() } });

		expect(container.querySelector('main')?.textContent).not.toContain('undefined');
		expect(screen.getByText('Discovered · search roll')).toBeTruthy();
		// A ground item with a single unit shows no stack badge.
		expect(screen.queryByText(/^×/)).toBeNull();

		await fireEvent.click(screen.getByRole('button', { name: /Engage/ }));
		expect(engage).toHaveBeenCalledWith('enemy-1');
	});

	it('falls back to the plain header when a node has no name or type', () => {
		const session = playing();
		session.selectedNodeId = null;
		session.map = mapGraph({
			nodes: [mapNode('market', { displayName: undefined, nodeType: undefined }), woods],
			edges: []
		});
		const { container } = render(GameScreen, { props: { session, onLeave: vi.fn() } });

		expect(container.textContent).not.toContain('undefined');
		expect(screen.getByRole('heading', { name: 'You are here' })).toBeTruthy();
	});

	it('prompts for a map selection when the loaded map has no focus node', () => {
		const session = new GameSession();
		session.zone = { ...zone };
		session.location = { locationSlug: 'market', typeKey: 'Location' } as LocationEnteredPayload;
		session.map = mapGraph();
		render(GameScreen, { props: { session, onLeave: vi.fn() } });

		expect(screen.getByText('Select a node on the map.')).toBeTruthy();
		// The breadcrumb drops the missing location name rather than printing it verbatim.
		const breadcrumb = screen.getByText(/Aldenmere ·/, { selector: 'p' });
		expect(breadcrumb.textContent).toContain('Aldenmere ·');
		expect(breadcrumb.textContent).not.toContain('undefined');
	});

	it('keeps the route and discovery labels readable when a name is missing', () => {
		const session = playing();
		session.selectedNodeId = null;
		session.map = mapGraph({
			nodes: [market, mapNode('woods', { displayName: undefined })],
			edges: [mapEdge('market', 'woods')]
		});
		session.lastSearch = {
			rollValue: 3,
			discovered: [
				{ slug: 'woods', displayName: undefined, typeKey: 'Location' } as unknown as SearchFinding
			]
		};
		const { container } = render(GameScreen, { props: { session, onLeave: vi.fn() } });

		const focusPanel = [...container.querySelectorAll<HTMLElement>('main section')][1];
		const routes = within(focusPanel).getByText('Routes').parentElement!;
		expect(within(routes).getAllByRole('button')).toHaveLength(1);

		const discovered = screen.getByText(/Discovered · search roll/).parentElement!;
		expect(within(discovered).getAllByRole('button')).toHaveLength(1);
	});
});
