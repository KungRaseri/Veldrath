import { describe, expect, it } from 'vitest';
import { currentRoom, dungeonProgress, dungeonRooms, exitNode, isDungeonZone } from './dungeon';
import type { MapGraph, MapNode } from './types';

function node(overrides: Partial<MapNode> & Pick<MapNode, 'id'>): MapNode {
	return {
		mapId: 'zone:the-halrow',
		nodeType: 'Location',
		refId: overrides.id,
		displayName: overrides.id,
		description: '',
		isEntryPoint: false,
		isExitPoint: false,
		isHidden: false,
		sortOrder: 0,
		...overrides
	};
}

function map(nodes: MapNode[]): MapGraph {
	return {
		id: 'zone:the-halrow',
		mapType: 'Zone',
		ownerId: 'the-halrow',
		name: 'The Halrow',
		description: '',
		entryNodeId: 'entry',
		nodes: [{ ...node({ id: 'entry', nodeType: 'EntryPoint', refId: null }) }, ...nodes],
		edges: []
	};
}

describe('dungeon rules', () => {
	it('recognises a dungeon by the zone type, not by the floor', () => {
		expect(isDungeonZone('Dungeon')).toBe(true);
		expect(isDungeonZone('dungeon')).toBe(true);
		expect(isDungeonZone('Town')).toBe(false);
		expect(isDungeonZone(null)).toBe(false);
	});

	it('lists the visible rooms in authored order, marking the current and cleared ones', () => {
		const rooms = dungeonRooms(
			map([
				node({ id: 'vault', displayName: 'The Vault', sortOrder: 20 }),
				node({ id: 'threshold', displayName: 'The Threshold', sortOrder: 10 }),
				node({ id: 'secret', displayName: 'Secret', sortOrder: 30, isHidden: true })
			]),
			'threshold',
			['vault'],
			['threshold']
		);

		expect(rooms.map((room) => room.nodeId)).toEqual(['threshold', 'vault']);
		expect(rooms[0]).toMatchObject({ isCurrent: true, isCleared: true, isAdjacent: false });
		expect(rooms[1]).toMatchObject({ isCurrent: false, isCleared: false, isAdjacent: true });
	});

	it('reports progress across the rooms it can see', () => {
		const rooms = dungeonRooms(
			map([
				node({ id: 'a', sortOrder: 1 }),
				node({ id: 'b', sortOrder: 2 }),
				node({ id: 'c', sortOrder: 3 })
			]),
			null,
			[],
			['a', 'c']
		);

		expect(dungeonProgress(rooms)).toEqual({ cleared: 2, total: 3 });
	});

	it('has no current room while standing on the entry node', () => {
		const rooms = dungeonRooms(map([node({ id: 'a' })]), 'entry', [], []);

		expect(currentRoom(rooms)).toBeNull();
	});

	it('prefers the region exit, then the zone exit, and tolerates a map with neither', () => {
		expect(
			exitNode(
				map([
					node({ id: 'zone-exit', nodeType: 'ZoneExit', isExitPoint: true }),
					node({ id: 'region-exit', nodeType: 'RegionExit', isExitPoint: true })
				])
			)?.id
		).toBe('region-exit');

		expect(exitNode(map([node({ id: 'zone-exit', nodeType: 'ZoneExit' })]))?.id).toBe('zone-exit');
		expect(exitNode(map([node({ id: 'room' })]))).toBeNull();
		expect(exitNode(null)).toBeNull();
	});

	it('returns no rooms without a map', () => {
		expect(dungeonRooms(null, null, [], [])).toEqual([]);
	});
});
