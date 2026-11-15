import type { MapGraph, MapNode } from './types';

/** One room of a dungeon, as the map graph describes it and the run has left it. */
export interface DungeonRoom {
	/** Map node id, which is what travelling takes. */
	nodeId: string;
	/** Zone-location slug the node resolves to, or `null` when the node carries no reference. */
	slug: string | null;
	label: string;
	isCurrent: boolean;
	isCleared: boolean;
	isAdjacent: boolean;
}

/** A dungeon is a zone whose type says so; the floor map alone cannot tell you that. */
export function isDungeonZone(zoneType: string | null | undefined): boolean {
	return zoneType?.toLowerCase() === 'dungeon';
}

/**
 * The rooms of the current floor: every visible location node, with the current one and the ones
 * this run has already cleared marked. Hidden nodes are left out — the server filtered them already,
 * but a stale map can still carry one.
 */
export function dungeonRooms(
	map: MapGraph | null,
	currentNodeId: string | null,
	adjacentNodeIds: readonly string[],
	clearedSlugs: readonly string[]
): DungeonRoom[] {
	if (!map) return [];

	return map.nodes
		.filter((node) => node.nodeType === 'Location' && !node.isHidden)
		.sort((left, right) => left.sortOrder - right.sortOrder)
		.map((node) => ({
			nodeId: node.id,
			slug: node.refId ?? null,
			label: node.displayName,
			isCurrent: node.id === currentNodeId,
			isCleared: node.refId ? clearedSlugs.includes(node.refId) : false,
			isAdjacent: adjacentNodeIds.includes(node.id)
		}));
}

/** How much of the floor is done, for the header line. */
export function dungeonProgress(rooms: readonly DungeonRoom[]): { cleared: number; total: number } {
	return { cleared: rooms.filter((room) => room.isCleared).length, total: rooms.length };
}

/** The room the character is standing in, or `null` when they are elsewhere on the floor. */
export function currentRoom(rooms: readonly DungeonRoom[]): DungeonRoom | null {
	return rooms.find((room) => room.isCurrent) ?? null;
}

/**
 * The node that leaves this floor. A dungeon always has one — the seeded world guarantees it, and the
 * map validator now fails the build when it does not — so the control can be permanently available.
 */
export function exitNode(map: MapGraph | null): MapNode | null {
	if (!map) return null;

	return (
		map.nodes.find((node) => node.nodeType === 'RegionExit') ??
		map.nodes.find((node) => node.nodeType === 'ZoneExit') ??
		null
	);
}
