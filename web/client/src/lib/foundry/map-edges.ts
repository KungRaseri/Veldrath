import { directionBetween, layoutCanvas, type Cardinal } from './map-canvas';
import type { MapEdgeDto, MapGraphDto } from './map-editor';

/**
 * Edge authoring rules.
 *
 * The validator rejects four edge mistakes — a self-reference, two edges leaving a node in the same
 * direction, an origin or destination that is not on the map, and a duplicate edge id. Every one of them
 * is cheaper to prevent in the gesture that creates the edge than to report after a save, so they live
 * here and the connect flow asks first.
 */

/** Every direction an edge may carry. The vertical and interior ones cannot be inferred from a drag. */
export const DIRECTIONS = ['North', 'South', 'East', 'West', 'Up', 'Down', 'In', 'Out'] as const;

/** Every traversal type an edge may carry. */
export const EDGE_TYPES = ['Walk', 'ExitToZone', 'ExitToRegion', 'Portal', 'Door'] as const;

const OPPOSITE: Record<Cardinal, Cardinal> = {
	North: 'South',
	South: 'North',
	East: 'West',
	West: 'East'
};

/** What the connect gesture asks for. */
export interface ConnectOptions {
	/** An explicit direction; when omitted the layout decides. */
	direction?: string;
	/** Traversal type, `Walk` when omitted. */
	edgeType?: string;
	/** Whether to create the return edge as well — on by default for Walk. */
	reciprocal?: boolean;
}

/** The outcome of a connect gesture. */
export type ConnectResult =
	{ ok: true; graph: MapGraphDto; edges: MapEdgeDto[] } | { ok: false; reason: string };

/**
 * The direction from one placed node to another, or `null` when they are not cardinally aligned.
 *
 * @param graph The map holding both nodes.
 * @param fromId Origin node.
 * @param toId Destination node.
 * @returns A cardinal direction, or `null` when either node is unplaced or the pair is diagonal.
 */
export function edgeDirection(graph: MapGraphDto, fromId: string, toId: string): string | null {
	const layout = layoutCanvas(graph);
	const from = layout.placed.find((placement) => placement.id === fromId);
	const to = layout.placed.find((placement) => placement.id === toId);
	if (!from || !to) return null;

	return directionBetween(from, to);
}

/** The directions already leaving a node. */
export function outgoingDirections(graph: MapGraphDto, fromId: string): string[] {
	return graph.edges.filter((edge) => edge.fromNodeId === fromId).map((edge) => edge.direction);
}

/** Whether an edge already runs from one node to another. */
export function hasEdge(graph: MapGraphDto, fromId: string, toId: string): boolean {
	return graph.edges.some((edge) => edge.fromNodeId === fromId && edge.toNodeId === toId);
}

function makeEdge(
	graph: MapGraphDto,
	fromNodeId: string,
	toNodeId: string,
	direction: string,
	edgeType: string
): MapEdgeDto {
	return {
		// Minted once and kept: a re-minted id would make the save delete and re-add the row.
		id: globalThis.crypto.randomUUID(),
		mapId: graph.id,
		fromNodeId,
		toNodeId,
		direction,
		edgeType,
		label: null,
		isTraversable: true,
		isHidden: false
	};
}

/**
 * Connects two nodes, optionally in both directions.
 *
 * @param graph The map to change.
 * @param fromId Origin node.
 * @param toId Destination node.
 * @param options Direction, traversal type and whether to create the return edge.
 * @returns The new graph and the edges added, or the reason the gesture was refused.
 */
export function connectNodes(
	graph: MapGraphDto,
	fromId: string,
	toId: string,
	options: ConnectOptions = {}
): ConnectResult {
	if (fromId === toId) {
		return { ok: false, reason: 'A node cannot connect to itself.' };
	}

	if (
		!graph.nodes.some((node) => node.id === fromId) ||
		!graph.nodes.some((node) => node.id === toId)
	) {
		return { ok: false, reason: 'Both nodes have to be on this map.' };
	}

	const direction = options.direction ?? edgeDirection(graph, fromId, toId);
	if (!direction) {
		return {
			ok: false,
			reason:
				'Those nodes are not cardinally aligned. Move one of them, or pick a direction by hand.'
		};
	}

	if (outgoingDirections(graph, fromId).includes(direction)) {
		return { ok: false, reason: `That node already has an edge going ${direction}.` };
	}

	const edgeType = options.edgeType ?? 'Walk';
	const edges = [makeEdge(graph, fromId, toId, direction, edgeType)];

	if (options.reciprocal) {
		const opposite = OPPOSITE[direction as Cardinal];
		if (!opposite) {
			return { ok: false, reason: `A ${direction} edge has no cardinal opposite to return by.` };
		}

		if (hasEdge(graph, toId, fromId)) {
			return { ok: false, reason: 'The return edge already exists.' };
		}

		if (outgoingDirections(graph, toId).includes(opposite)) {
			return { ok: false, reason: `The other node already has an edge going ${opposite}.` };
		}

		edges.push(makeEdge(graph, toId, fromId, opposite, edgeType));
	}

	return { ok: true, graph: { ...graph, edges: [...graph.edges, ...edges] }, edges };
}

/** Changes one edge, leaving every other edge and the nodes untouched. */
export function patchEdge(
	graph: MapGraphDto,
	edgeId: string,
	patch: Partial<MapEdgeDto>
): MapGraphDto {
	return {
		...graph,
		edges: graph.edges.map((edge) => (edge.id === edgeId ? { ...edge, ...patch } : edge))
	};
}

/**
 * Removes one edge.
 *
 * @param graph The map to change.
 * @param edgeId The edge to remove.
 * @param withReciprocal Also remove the edge that runs the other way, when one exists.
 * @returns A new graph.
 */
export function deleteEdge(
	graph: MapGraphDto,
	edgeId: string,
	withReciprocal = false
): MapGraphDto {
	const edge = graph.edges.find((candidate) => candidate.id === edgeId);
	if (!edge) return graph;

	const doomed = new Set([edgeId]);
	if (withReciprocal) {
		for (const candidate of graph.edges) {
			if (candidate.fromNodeId === edge.toNodeId && candidate.toNodeId === edge.fromNodeId) {
				doomed.add(candidate.id);
			}
		}
	}

	return { ...graph, edges: graph.edges.filter((candidate) => !doomed.has(candidate.id)) };
}

/** The edge that runs the other way, when one exists. */
export function reciprocalOf(graph: MapGraphDto, edge: MapEdgeDto): MapEdgeDto | null {
	return (
		graph.edges.find(
			(candidate) =>
				candidate.fromNodeId === edge.toNodeId && candidate.toNodeId === edge.fromNodeId
		) ?? null
	);
}
