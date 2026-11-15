import type { MapGraphDto, MapNodeDto } from './map-editor';

/**
 * The canvas rules, as pure functions.
 *
 * The component renders and dispatches; everything that decides *where* a node goes lives here so it can
 * be tested without a browser. Two ideas carry the whole module:
 *
 * 1. `layoutX`/`layoutY` are nullable and `null` means "not authored yet". A node without them must never
 *    be silently given a coordinate — it is rendered in an **unplaced lane**, because the difference
 *    between "the author put it there" and "the layout put it there" is the difference between data and a
 *    guess.
 * 2. Cells are integers and one node per cell. A move onto an occupied cell is refused rather than
 *    stacked, so the drawing always shows what the game will do.
 */

/** Pixel size of one grid step. */
export const CELL = 96;

/** Largest grid coordinate the editor will author. Beyond this the map stops being readable. */
export const MAX_CELL = 200;

/** A node's position on the grid. */
export interface CanvasCell {
	/** Column. */
	x: number;
	/** Row. */
	y: number;
}

/** One node as the canvas draws it. */
export interface CanvasPlacement extends CanvasCell {
	/** Node identifier. */
	id: string;
	/** Label shown on the card. */
	label: string;
	/** Node kind, shown as a subtitle. */
	nodeType: string;
	/** Whether the position came from the map, as opposed to the unplaced lane. */
	isAuthored: boolean;
	/** Whether the node is the map's entry point. */
	isEntryPoint: boolean;
	/** Whether the node is an exit. */
	isExitPoint: boolean;
}

/** A graph laid out for drawing. */
export interface CanvasLayout {
	/** Nodes with a position, authored or fallback, drawn on the grid. */
	placed: CanvasPlacement[];
	/** Nodes with no authored position, drawn in their own lane so they are not mistaken for placed. */
	unplaced: CanvasPlacement[];
	/** Grid extent of {@link placed}, in cells. `width`/`height` are 0 when nothing is placed. */
	bounds: { minX: number; minY: number; maxX: number; maxY: number; width: number; height: number };
}

/** Cardinal directions the canvas can author. Vertical and interior directions are not inferred. */
export type Cardinal = 'North' | 'South' | 'East' | 'West';

const isAuthored = (node: MapNodeDto): boolean => node.layoutX !== null && node.layoutY !== null;

/**
 * Splits a graph into the nodes on the grid and the nodes waiting for a position.
 *
 * @param graph The graph to lay out.
 * @returns Placements, the unplaced lane, and the grid bounds.
 */
export function layoutCanvas(graph: MapGraphDto): CanvasLayout {
	const placed: CanvasPlacement[] = [];
	const unplaced: CanvasPlacement[] = [];

	for (const node of orderedNodes(graph)) {
		const placement: CanvasPlacement = {
			id: node.id,
			label: node.displayName || node.id,
			nodeType: node.nodeType,
			x: node.layoutX ?? 0,
			y: node.layoutY ?? 0,
			isAuthored: isAuthored(node),
			isEntryPoint: node.isEntryPoint,
			isExitPoint: node.isExitPoint || node.targetMapId !== null
		};

		if (placement.isAuthored) {
			placed.push(placement);
		} else {
			unplaced.push(placement);
		}
	}

	return { placed, unplaced, bounds: boundsOf(placed) };
}

/**
 * Orders nodes the way the grid should read: by authored row then column, with the unplaced ones last
 * so a fallback never appears to be a real position.
 */
export function orderedNodes(graph: MapGraphDto): MapNodeDto[] {
	return [...graph.nodes].sort((left, right) => {
		const leftAuthored = isAuthored(left);
		const rightAuthored = isAuthored(right);
		if (leftAuthored !== rightAuthored) return leftAuthored ? -1 : 1;

		return (
			(left.layoutY ?? 0) - (right.layoutY ?? 0) ||
			(left.layoutX ?? 0) - (right.layoutX ?? 0) ||
			left.sortOrder - right.sortOrder ||
			left.id.localeCompare(right.id)
		);
	});
}

/** Computes the grid extent of the placements. */
export function boundsOf(placements: readonly CanvasPlacement[]): CanvasLayout['bounds'] {
	if (placements.length === 0) {
		return { minX: 0, minY: 0, maxX: 0, maxY: 0, width: 0, height: 0 };
	}

	const xs = placements.map((placement) => placement.x);
	const ys = placements.map((placement) => placement.y);
	const minX = Math.min(...xs);
	const minY = Math.min(...ys);
	const maxX = Math.max(...xs);
	const maxY = Math.max(...ys);

	return { minX, minY, maxX, maxY, width: maxX - minX + 1, height: maxY - minY + 1 };
}

/**
 * The placement already occupying a cell, if any.
 *
 * @param placements Placements to search.
 * @param cell The target cell.
 * @param exceptId A node allowed to occupy the cell — the one being dragged.
 * @returns The occupying placement, or `null`.
 */
export function nodeAt(
	placements: readonly CanvasPlacement[],
	cell: CanvasCell,
	exceptId?: string
): CanvasPlacement | null {
	return (
		placements.find(
			(placement) => placement.x === cell.x && placement.y === cell.y && placement.id !== exceptId
		) ?? null
	);
}

/** Clamps a coordinate into the authored range. */
export function clampCell(value: number): number {
	if (!Number.isFinite(value)) return 0;
	return Math.min(MAX_CELL, Math.max(0, Math.round(value)));
}

/**
 * Moves one node, returning a new graph.
 *
 * The move is refused — the graph comes back unchanged — when the target cell is taken by another node.
 * Refusing rather than stacking is what keeps the drawing honest.
 *
 * @param graph The graph to change.
 * @param nodeId The node to move.
 * @param cell The target cell.
 * @returns The updated graph, or the original when the move is not allowed.
 */
export function moveNode(graph: MapGraphDto, nodeId: string, cell: CanvasCell): MapGraphDto {
	const target = { x: clampCell(cell.x), y: clampCell(cell.y) };
	const layout = layoutCanvas(graph);

	if (!layout.placed.some((placement) => placement.id === nodeId)) return graph;
	if (nodeAt(layout.placed, target, nodeId)) return graph;

	return {
		...graph,
		nodes: graph.nodes.map((node) =>
			node.id === nodeId ? { ...node, layoutX: target.x, layoutY: target.y } : node
		)
	};
}

/** The first free cell at or after `from`, scanning rows then columns. */
export function nextFreeCell(layout: CanvasLayout, from: CanvasCell = { x: 0, y: 0 }): CanvasCell {
	for (let y = from.y; y <= MAX_CELL; y += 1) {
		for (let x = y === from.y ? from.x : 0; x <= MAX_CELL; x += 1) {
			if (!nodeAt(layout.placed, { x, y })) return { x, y };
		}
	}

	return { x: 0, y: 0 };
}

const DELTAS: Record<Cardinal, CanvasCell> = {
	North: { x: 0, y: -1 },
	South: { x: 0, y: 1 },
	East: { x: 1, y: 0 },
	West: { x: -1, y: 0 }
};

/**
 * The cardinal direction from one cell to another, or `null` when they are not cardinally aligned.
 *
 * Used to infer an edge's direction from a drag in G4, and by {@link tidyLayout} to walk the graph.
 *
 * @param from Origin cell.
 * @param to Destination cell.
 * @returns The direction, or `null` for a diagonal or an identical cell.
 */
export function directionBetween(from: CanvasCell, to: CanvasCell): Cardinal | null {
	const dx = Math.sign(to.x - from.x);
	const dy = Math.sign(to.y - from.y);

	if (dx === 0 && dy === 0) return null;
	if (dx === 1 && dy === 0) return 'East';
	if (dx === -1 && dy === 0) return 'West';
	if (dx === 0 && dy === 1) return 'South';
	if (dx === 0 && dy === -1) return 'North';
	return null;
}

/**
 * Writes authored coordinates for every node, following the edges outward from the entry point.
 *
 * This is the "tidy" action: it derives positions from the map's own edge directions rather than
 * compacting the grid, so the result still reads like the author's intent. Nodes the walk cannot reach
 * are still placed — the validator reports them as unreachable, but tidy must not leave a node without a
 * position, or the next save would look like data loss.
 *
 * @param graph The graph to lay out.
 * @returns A new graph whose every node has `layoutX`/`layoutY`.
 */
export function tidyLayout(graph: MapGraphDto): MapGraphDto {
	const ordered = orderedNodes(graph);
	const authoredEntry = layoutCanvas(graph).placed.find(
		(placement) => placement.id === graph.entryNodeId
	);
	const start =
		ordered.find((node) => node.id === graph.entryNodeId) ??
		ordered.find((node) => node.isEntryPoint) ??
		ordered[0];

	if (!start) return graph;

	/** Cells already handed out by this pass, in the shape `nodeAt`/`nextFreeCell` expect. */
	const occupied: CanvasPlacement[] = [];
	const positions = new Map<string, CanvasCell>();

	const take = (id: string, preferred: CanvasCell): void => {
		const wanted = { x: clampCell(preferred.x), y: clampCell(preferred.y) };
		const cell = nodeAt(occupied, wanted)
			? nextFreeCell({ placed: occupied, unplaced: [], bounds: boundsOf(occupied) }, wanted)
			: wanted;

		positions.set(id, cell);
		occupied.push({
			id,
			label: id,
			nodeType: '',
			x: cell.x,
			y: cell.y,
			isAuthored: true,
			isEntryPoint: false,
			isExitPoint: false
		});
	};

	take(start.id, authoredEntry ? { x: authoredEntry.x, y: authoredEntry.y } : { x: 0, y: 0 });

	const queue = [start.id];
	const seen = new Set([start.id]);

	while (queue.length > 0) {
		const currentId = queue.shift()!;
		const current = positions.get(currentId)!;

		for (const edge of graph.edges.filter((candidate) => candidate.fromNodeId === currentId)) {
			if (seen.has(edge.toNodeId)) continue;
			if (!graph.nodes.some((node) => node.id === edge.toNodeId)) continue;

			seen.add(edge.toNodeId);

			const delta = DELTAS[edge.direction as Cardinal];
			take(edge.toNodeId, delta ? { x: current.x + delta.x, y: current.y + delta.y } : current);
			queue.push(edge.toNodeId);
		}
	}

	// Unreachable nodes still get a cell: tidy must never look like it deleted authoring.
	for (const node of ordered) {
		if (!positions.has(node.id)) take(node.id, { x: 0, y: 0 });
	}

	return {
		...graph,
		nodes: graph.nodes.map((node) => ({
			...node,
			layoutX: positions.get(node.id)?.x ?? node.layoutX,
			layoutY: positions.get(node.id)?.y ?? node.layoutY
		}))
	};
}
