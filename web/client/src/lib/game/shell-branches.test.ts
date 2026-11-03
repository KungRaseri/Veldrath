import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';
import CardinalMap from './CardinalMap.svelte';
import RightSidebar from './RightSidebar.svelte';
import { GameSession } from './session.svelte';
import type { ChatMessage, MapEdge, MapGraph, MapNode } from './types';

// See widgets.test.ts: the hub module needs a SvelteKit dev global that Vitest does not provide.
vi.mock('#lib/hub', () => ({ createGameHub: () => null }));

/**
 * Branch arms the existing shell suite leaves dark, in the social sidebar and the text map.
 *
 * The sidebar and the map are both branch-dense template files, so each case drives one
 * interpolated value to `null`/`undefined`, takes a `switch` default arm, or toggles party
 * membership, rather than repeating a render the happy-path suite already covers.
 */

function playing(): GameSession {
	const game = new GameSession();
	game.phase = 'connected';
	return game;
}

function message(overrides: Partial<ChatMessage> = {}): ChatMessage {
	return {
		channel: 'Zone',
		sender: 'Aria',
		message: 'hello',
		timestamp: '2026-01-01T00:00:00Z',
		...overrides
	};
}

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

function graph(overrides: Partial<MapGraph> = {}): MapGraph {
	return {
		id: 'map-1',
		name: 'Aldenmere Roads',
		mapType: 'Overworld',
		nodes: [node('market', { displayName: 'Market' }), node('woods', { displayName: 'Woods' })],
		edges: [edge('market', 'woods')],
		entryNodeId: 'market',
		...overrides
	} as MapGraph;
}

describe('right sidebar branches', () => {
	it('adds the party tab once the character is grouped and can switch to it', async () => {
		const session = playing();
		session.party = { partyId: 'party-1', leaderCharacterId: 'c1', members: [] } as never;

		render(RightSidebar, { props: { session } });

		const partyTab = screen.getByRole('button', { name: 'Party' });
		expect(partyTab).toBeTruthy();

		await fireEvent.click(partyTab);
		expect(session.chatChannel).toBe('Party');
	});

	it('styles a global message and renders an unknown sender', () => {
		const session = playing();
		session.chatChannel = 'Global';
		session.chat = [message({ channel: 'Global', sender: null as never, message: 'hear ye' })];

		render(RightSidebar, { props: { session } });

		const line = screen.getByText('hear ye').closest('p');
		// The `global` switch arm supplies the modifier class; the null sender falls back to blank.
		expect(line?.className).toContain('game-chat-message-global');
		expect(line?.textContent).toContain('[Global]');
	});

	it('lists a slash command whose description is unknown', () => {
		const session = playing();
		session.chatCommands = [
			{ command: 'who', usage: '/who', description: null as never, requiredPermission: null }
		];

		render(RightSidebar, { props: { session } });

		expect(screen.getByText('Slash commands (1)')).toBeTruthy();
		expect(screen.getByText('/who')).toBeTruthy();
	});

	it('skips the chat auto-scroll when the log is already unmounted', async () => {
		const session = playing();
		const sendChat = vi.spyOn(session, 'sendChat').mockResolvedValue(undefined);
		const { unmount } = render(RightSidebar, { props: { session } });

		const input = screen.getByPlaceholderText(/Message zone/);
		await fireEvent.input(input, { target: { value: 'bye' } });
		const form = input.closest('form') as HTMLFormElement;

		// The submit handler awaits `sendChat` before scrolling; unmounting first clears the bound
		// log element, so the scroll guard takes its false arm instead of scrolling.
		void fireEvent.submit(form);
		unmount();

		await vi.waitFor(() => expect(sendChat).toHaveBeenCalledWith('bye'));
	});
});

describe('cardinal map branches', () => {
	it('falls back to the hidden connector class for an unknown direction', () => {
		const { container } = render(CardinalMap, {
			props: {
				map: graph({ edges: [edge('market', 'woods', { direction: 'up' })] }),
				currentNodeId: 'market',
				selectedNodeId: null,
				onSelect: vi.fn()
			}
		});

		// `up` is not one of the four cardinal cases, so the switch default paints the connector.
		expect(container.querySelectorAll('.map-connector-line.map-connector-hidden')).toHaveLength(1);
	});

	it('renders a map with no type and a node with no type', () => {
		render(CardinalMap, {
			props: {
				map: graph({
					mapType: null as never,
					nodes: [node('market', { displayName: 'Market', nodeType: null as never })]
				}),
				currentNodeId: 'market',
				selectedNodeId: 'market',
				onSelect: vi.fn()
			}
		});

		// Both the map subtitle and the node subtitle drop their missing parts.
		expect(screen.getByText(/1 nodes/)).toBeTruthy();
		expect(screen.getByRole('button', { name: /Market/ }).textContent).not.toContain('null');
	});
});
