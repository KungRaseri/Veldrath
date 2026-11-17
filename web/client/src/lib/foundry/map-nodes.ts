import { layoutCanvas, nextFreeCell, type CanvasCell } from './map-canvas';
import type { MapGraphDto, MapNodeDto, MapTargets } from './map-editor';

/**
 * Node authoring rules: what a new node looks like, and when one may be removed.
 *
 * The rules are pure so the inspector and the page cannot disagree, and so the refusals are tested
 * without a browser. One refusal is deliberately *not* here: whether another map's exit targets this
 * node is knowledge the client does not have. That mistake is caught by the world validator's
 * `target_node_unknown`, which refuses the save and names the node — a server-side guard the editor
 * reuses rather than reimplements from stale data.
 */

/** Node kinds the inspector offers, in the order the map reads: entrance, places, ways out. */
export const NODE_TYPES = [
	'EntryPoint',
	'Zone',
	'Location',
	'ZoneExit',
	'RegionExit',
	'Portal',
	'Stair',
	'Landmark'
] as const;

/** Unlock mechanisms the validator knows. `skill-check` carries its gate in `discoverThreshold`. */
export const UNLOCK_TYPES = ['quest', 'item', 'achievement', 'skill-check'] as const;

/** Whether the node kind is one the validator requires a destination for. */
export function isExitNodeType(nodeType: string): boolean {
	return ['ZoneExit', 'RegionExit', 'Portal', 'Stair'].includes(nodeType);
}

/**
 * The references a node of this kind may name.
 *
 * @param nodeType The node's kind.
 * @param targets The reference catalog served by the editor API.
 * @returns The options to offer, or an empty list when the kind's `RefId` is free text.
 */
export function refOptionsFor(nodeType: string, targets: MapTargets) {
	switch (nodeType) {
		case 'Zone':
			return targets.zones;
		case 'Location':
			return targets.locations;
		case 'RegionExit':
			return targets.regions;
		default:
			return [];
	}
}

/**
 * Suggests an id for a node of this kind, unique within the map.
 *
 * The id is derived rather than typed: `<mapId>:<kind>` with a counter when that is taken. It stays
 * editable in the inspector, but a suggestion that cannot collide removes the commonest way to make the
 * save fail with `node_duplicate`.
 *
 * @param graph The map the node will join.
 * @param nodeType The node's kind.
 * @returns An id no node on this map uses yet.
 */
export function suggestNodeId(graph: MapGraphDto, nodeType: string): string {
	const base = `${graph.id}:${nodeType.toLowerCase()}`;
	const taken = new Set(graph.nodes.map((node) => node.id.toLowerCase()));
	if (!taken.has(base.toLowerCase())) return base;

	for (let index = 2; index < 1_000; index += 1) {
		const candidate = `${base}-${index}`;
		if (!taken.has(candidate.toLowerCase())) return candidate;
	}

	return `${base}-${Date.now()}`;
}

/** What the inspector asks for when creating a node. */
export interface NewNode {
	/** Node kind, one of {@link NODE_TYPES}. */
	nodeType: string;
	/** Label shown on the card and in the table. */
	displayName: string;
	/** Where to place it; when omitted the first free cell is used. */
	cell?: CanvasCell;
	/**
	 * Catalog slug the node points at. Omitted for nodes that reference nothing, such as an entry point;
	 * a location added from the zone panel arrives with its slug already filled in.
	 */
	refId?: string | null;
}

/**
 * Adds a node with the defaults the plan settled: `sortOrder` after the last node, a free cell near the
 * selection, and the exit flag set from the node kind.
 *
 * @param graph The map to change.
 * @param input What kind of node to add and what to call it.
 * @returns A new graph with the node appended.
 */
export function addNode(graph: MapGraphDto, input: NewNode): MapGraphDto {
	const sortOrder =
		graph.nodes.reduce((highest, node) => Math.max(highest, node.sortOrder), 0) + 10;
	const cell = input.cell ?? nextFreeCell(layoutCanvas(graph));

	const node: MapNodeDto = {
		id: suggestNodeId(graph, input.nodeType),
		mapId: graph.id,
		nodeType: input.nodeType,
		refId: input.refId ?? null,
		displayName: input.displayName.trim() === '' ? 'New node' : input.displayName.trim(),
		description: '',
		iconKey: null,
		layoutX: cell.x,
		layoutY: cell.y,
		isEntryPoint: input.nodeType === 'EntryPoint',
		isExitPoint: isExitNodeType(input.nodeType),
		targetMapId: null,
		targetNodeId: null,
		isHidden: false,
		unlockType: null,
		unlockKey: null,
		discoverThreshold: null,
		sortOrder
	};

	return { ...graph, nodes: [...graph.nodes, node] };
}

/**
 * Why a node may not be deleted, or `null` when it may.
 *
 * @param graph The map the node belongs to.
 * @param nodeId The node to remove.
 * @param isLive Whether the map is already in the game; only a reviewer may edit one.
 * @returns A sentence to show the author, or `null`.
 */
export function deleteBlockedReason(
	graph: MapGraphDto,
	nodeId: string,
	isLive = false
): string | null {
	if (isLive) {
		return 'This map is live. Only a reviewer may change it; ask one to make the edit.';
	}

	if (nodeId === graph.entryNodeId) {
		return 'This is the entry node. Point the map at another entry node before deleting it.';
	}

	const node = graph.nodes.find((candidate) => candidate.id === nodeId);
	if (!node) return 'That node is not on this map.';

	if (node.isEntryPoint) {
		return 'This node is marked as an entry point. Clear the flag before deleting it.';
	}

	return null;
}

/**
 * Removes a node and every edge that touches it.
 *
 * Callers must check {@link deleteBlockedReason} first; this function assumes the deletion is allowed.
 *
 * @param graph The map to change.
 * @param nodeId The node to remove.
 * @returns A new graph without the node or its edges.
 */
export function deleteNode(graph: MapGraphDto, nodeId: string): MapGraphDto {
	return {
		...graph,
		nodes: graph.nodes.filter((node) => node.id !== nodeId),
		edges: graph.edges.filter((edge) => edge.fromNodeId !== nodeId && edge.toNodeId !== nodeId)
	};
}
