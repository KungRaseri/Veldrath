import type { MapEdge, MapNode } from './types';

/** A node position produced by {@link computeCardinalLayout}. */
export interface CardinalNodePlacement {
	nodeId: string;
	x: number;
	y: number;
	isAuthored: boolean;
}

interface Point {
	x: number;
	y: number;
}

const key = (value: string) => value.toLowerCase();

function compareIgnoreCase(a: string, b: string): number {
	const left = a.toLowerCase();
	const right = b.toLowerCase();
	return left < right ? -1 : left > right ? 1 : 0;
}

/**
 * Deterministic grid layout for text map graphs.
 *
 * Authored `layoutX`/`layoutY` positions are preferred when the authored grid is dense and
 * consistent with edge directions; remaining nodes are placed cardinally from already-placed
 * neighbours. Every grid cell is unique, and computed neighbours share a row or column.
 *
 * Ported from `Veldrath.GameClient.Components/Layout/CardinalGraphLayout.cs`.
 */
export function computeCardinalLayout(
	nodes: MapNode[],
	edges: MapEdge[] = []
): CardinalNodePlacement[] {
	const orderedNodes = nodes
		.filter((node) => Boolean(node.id?.trim()))
		.slice()
		.sort((a, b) => a.sortOrder - b.sortOrder || compareIgnoreCase(a.id, b.id));

	if (orderedNodes.length === 0) return [];

	const nodeIds = new Set(orderedNodes.map((node) => key(node.id)));

	const orderedEdges = edges
		.filter(
			(edge) =>
				edge.isTraversable &&
				nodeIds.has(key(edge.fromNodeId)) &&
				nodeIds.has(key(edge.toNodeId)) &&
				key(edge.fromNodeId) !== key(edge.toNodeId)
		)
		.slice()
		.sort(
			(a, b) =>
				compareIgnoreCase(a.fromNodeId, b.fromNodeId) ||
				compareIgnoreCase(a.toNodeId, b.toNodeId) ||
				compareIgnoreCase(a.direction, b.direction)
		);

	const positions = new Map<string, Point>();
	const occupied = new Set<string>();
	const authored = new Set<string>();

	const authoredNodes = orderedNodes.filter((node) => node.layoutX != null && node.layoutY != null);

	if (
		authoredNodes.length > 0 &&
		isDenseAuthoredLayout(authoredNodes) &&
		isCardinallyConsistent(authoredNodes, orderedEdges)
	) {
		const xIndex = buildCompactIndex(authoredNodes.map((node) => node.layoutX as number));
		const yIndex = buildCompactIndex(authoredNodes.map((node) => node.layoutY as number));

		for (const node of authoredNodes) {
			const position = reserve(
				xIndex.get(node.layoutX as number) as number,
				yIndex.get(node.layoutY as number) as number,
				occupied
			);
			positions.set(key(node.id), position);
			authored.add(key(node.id));
		}
	} else {
		const root = orderedNodes.find((node) => node.isEntryPoint) ?? orderedNodes[0];
		positions.set(key(root.id), reserve(0, 0, occupied));
	}

	const outgoing = new Map<string, MapEdge[]>();
	const incoming = new Map<string, MapEdge[]>();

	for (const edge of orderedEdges) {
		const fromKey = key(edge.fromNodeId);
		const toKey = key(edge.toNodeId);
		if (!outgoing.has(fromKey)) outgoing.set(fromKey, []);
		outgoing.get(fromKey)!.push(edge);
		if (!incoming.has(toKey)) incoming.set(toKey, []);
		incoming.get(toKey)!.push(edge);
	}

	const queue = [...positions.keys()].sort(compareIgnoreCase);
	const processed = new Set<string>();

	for (let index = 0; index < queue.length; index++) {
		const nodeId = queue[index];
		if (processed.has(nodeId)) continue;
		processed.add(nodeId);

		for (const edge of outgoing.get(nodeId) ?? []) {
			const targetKey = key(edge.toNodeId);
			if (positions.has(targetKey)) continue;
			const [dx, dy] = directionVector(edge.direction);
			positions.set(
				targetKey,
				reserveDirectional(positions.get(nodeId) as Point, dx, dy, occupied)
			);
			queue.push(targetKey);
		}

		for (const edge of incoming.get(nodeId) ?? []) {
			const sourceKey = key(edge.fromNodeId);
			if (positions.has(sourceKey)) continue;
			const [dx, dy] = directionVector(edge.direction);
			positions.set(
				sourceKey,
				reserveDirectional(positions.get(nodeId) as Point, -dx, -dy, occupied)
			);
			queue.push(sourceKey);
		}
	}

	for (const node of orderedNodes) {
		const nodeKey = key(node.id);
		if (!positions.has(nodeKey)) positions.set(nodeKey, reserve(0, 0, occupied));
	}

	alignComputedEdges(orderedEdges, positions, occupied, authored);
	compactOccupiedGrid(positions);

	return orderedNodes.map((node) => {
		const position = positions.get(key(node.id)) as Point;
		return {
			nodeId: node.id,
			x: position.x,
			y: position.y,
			isAuthored: authored.has(key(node.id))
		};
	});
}

/** Returns whether two placements share a row or column. */
export function areCardinal(first: CardinalNodePlacement, second: CardinalNodePlacement): boolean {
	return first.x === second.x || first.y === second.y;
}

function compactOccupiedGrid(positions: Map<string, Point>): void {
	if (positions.size === 0) return;

	const xValues = [...new Set([...positions.values()].map((position) => position.x))].sort(
		(a, b) => a - b
	);
	const yValues = [...new Set([...positions.values()].map((position) => position.y))].sort(
		(a, b) => a - b
	);
	const xIndex = new Map(xValues.map((value, index) => [value, index]));
	const yIndex = new Map(yValues.map((value, index) => [value, index]));

	for (const [nodeId, position] of positions) {
		positions.set(nodeId, {
			x: xIndex.get(position.x) as number,
			y: yIndex.get(position.y) as number
		});
	}
}

function isDenseAuthoredLayout(authoredNodes: MapNode[]): boolean {
	const columns = new Set(authoredNodes.map((node) => node.layoutX as number)).size;
	const rows = new Set(authoredNodes.map((node) => node.layoutY as number)).size;
	const cells = columns * rows;
	const allowedCells = authoredNodes.length + Math.max(2, Math.floor(authoredNodes.length / 4));
	return cells <= allowedCells;
}

function isCardinallyConsistent(authoredNodes: MapNode[], edges: MapEdge[]): boolean {
	if (edges.length === 0) return true;

	const authoredPositions = new Map<string, Point>();
	for (const node of authoredNodes) {
		if (node.layoutX != null && node.layoutY != null) {
			authoredPositions.set(key(node.id), { x: node.layoutX, y: node.layoutY });
		}
	}

	let checkedEdges = 0;
	let consistentEdges = 0;

	for (const edge of edges) {
		const from = authoredPositions.get(key(edge.fromNodeId));
		const to = authoredPositions.get(key(edge.toNodeId));
		if (!from || !to) continue;

		checkedEdges++;
		if (matchesDirectionHalfPlane(from, to, edge.direction)) consistentEdges++;
	}

	return checkedEdges === 0 || consistentEdges >= Math.ceil(checkedEdges * 0.8);
}

function matchesDirectionHalfPlane(from: Point, to: Point, direction: string): boolean {
	const dx = to.x - from.x;
	const dy = to.y - from.y;

	switch (direction.toLowerCase()) {
		case 'north':
		case 'up':
		case 'in':
			return dy < 0;
		case 'south':
		case 'down':
		case 'out':
			return dy > 0;
		case 'east':
			return dx > 0;
		case 'west':
			return dx < 0;
		default:
			return true;
	}
}

function buildCompactIndex(values: number[]): Map<number, number> {
	const distinct = [...new Set(values)].sort((a, b) => a - b);
	return new Map(distinct.map((value, index) => [value, index]));
}

function alignComputedEdges(
	edges: MapEdge[],
	positions: Map<string, Point>,
	occupied: Set<string>,
	authored: Set<string>
): void {
	for (const edge of edges) {
		const from = positions.get(key(edge.fromNodeId)) as Point;
		const to = positions.get(key(edge.toNodeId)) as Point;

		if (from.x === to.x || from.y === to.y) continue;

		const vertical = isVertical(edge.direction);
		const toKey = key(edge.toNodeId);
		const fromKey = key(edge.fromNodeId);

		if (!authored.has(toKey)) {
			const target = vertical ? { x: from.x, y: to.y } : { x: to.x, y: from.y };
			moveNode(toKey, target, positions, occupied);
		} else if (!authored.has(fromKey)) {
			const target = vertical ? { x: to.x, y: from.y } : { x: from.x, y: to.y };
			moveNode(fromKey, target, positions, occupied);
		}
	}
}

function moveNode(
	nodeId: string,
	target: Point,
	positions: Map<string, Point>,
	occupied: Set<string>
): void {
	const cell = `${target.x},${target.y}`;
	if (occupied.has(cell)) return;

	occupied.add(cell);
	const previous = positions.get(nodeId) as Point;
	occupied.delete(`${previous.x},${previous.y}`);
	positions.set(nodeId, target);
}

function isVertical(direction: string): boolean {
	return ['north', 'south', 'up', 'down', 'in', 'out'].includes(direction.toLowerCase());
}

function directionVector(direction: string): [number, number] {
	switch (direction.toLowerCase()) {
		case 'north':
		case 'up':
		case 'in':
			return [0, -1];
		case 'south':
		case 'down':
		case 'out':
			return [0, 1];
		case 'east':
			return [1, 0];
		case 'west':
			return [-1, 0];
		default:
			return [0, -1];
	}
}

function reserveDirectional(origin: Point, dx: number, dy: number, occupied: Set<string>): Point {
	const perpendicularX = dy;
	const perpendicularY = dx;

	for (let step = 1; step <= 128; step++) {
		const baseX = origin.x + dx * step;
		const baseY = origin.y + dy * step;

		if (tryOccupy(baseX, baseY, occupied)) return { x: baseX, y: baseY };

		for (let offset = 1; offset <= 8; offset++) {
			const leftX = baseX + perpendicularX * offset;
			const leftY = baseY + perpendicularY * offset;
			if (tryOccupy(leftX, leftY, occupied)) return { x: leftX, y: leftY };

			const rightX = baseX - perpendicularX * offset;
			const rightY = baseY - perpendicularY * offset;
			if (tryOccupy(rightX, rightY, occupied)) return { x: rightX, y: rightY };
		}
	}

	return reserve(origin.x + dx, origin.y + dy, occupied);
}

function reserve(preferredX: number, preferredY: number, occupied: Set<string>): Point {
	if (tryOccupy(preferredX, preferredY, occupied)) {
		return { x: preferredX, y: preferredY };
	}

	for (let radius = 1; radius <= 1024; radius++) {
		for (let dx = -radius; dx <= radius; dx++) {
			const dy = radius - Math.abs(dx);

			if (tryOccupy(preferredX + dx, preferredY + dy, occupied)) {
				return { x: preferredX + dx, y: preferredY + dy };
			}

			if (dy > 0 && tryOccupy(preferredX + dx, preferredY - dy, occupied)) {
				return { x: preferredX + dx, y: preferredY - dy };
			}
		}
	}

	throw new Error('Unable to find a free layout cell.');
}

function tryOccupy(x: number, y: number, occupied: Set<string>): boolean {
	const cell = `${x},${y}`;
	if (occupied.has(cell)) return false;
	occupied.add(cell);
	return true;
}
