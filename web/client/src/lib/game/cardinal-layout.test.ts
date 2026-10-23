import { describe, expect, it } from 'vitest';
import { areCardinal, computeCardinalLayout, type CardinalNodePlacement } from './cardinal-layout';
import type { MapEdge, MapNode } from './types';

function node(
	id: string,
	options: { layoutX?: number; layoutY?: number; sortOrder?: number; isEntry?: boolean } = {}
): MapNode {
	return {
		id,
		mapId: 'zone:test',
		nodeType: 'Location',
		refId: id,
		displayName: id,
		description: `${id} description`,
		iconKey: 'place',
		layoutX: options.layoutX ?? null,
		layoutY: options.layoutY ?? null,
		isEntryPoint: options.isEntry ?? false,
		isExitPoint: false,
		targetMapId: null,
		targetNodeId: null,
		isHidden: false,
		unlockType: null,
		unlockKey: null,
		discoverThreshold: null,
		sortOrder: options.sortOrder ?? 0
	};
}

function edge(from: string, to: string, direction: string): MapEdge {
	return {
		id: crypto.randomUUID(),
		mapId: 'zone:test',
		fromNodeId: from,
		toNodeId: to,
		direction,
		edgeType: 'Walk',
		label: null,
		isTraversable: true,
		isHidden: false
	};
}

const byId = (placements: CardinalNodePlacement[], id: string) => {
	const placement = placements.find((candidate) => candidate.nodeId === id);
	if (!placement) throw new Error(`Missing placement for ${id}`);
	return placement;
};

describe('computeCardinalLayout', () => {
	it('compacts authored tile coordinates', () => {
		const placements = computeCardinalLayout([
			node('a', { layoutX: 4, layoutY: 7, sortOrder: 1 }),
			node('b', { layoutX: 5, layoutY: 7, sortOrder: 2 }),
			node('c', { layoutX: 5, layoutY: 19, sortOrder: 3 })
		]);

		const a = byId(placements, 'a');
		const b = byId(placements, 'b');
		const c = byId(placements, 'c');

		expect([a.x, a.y]).toEqual([0, 0]);
		expect([b.x, b.y]).toEqual([1, 0]);
		expect([c.x, c.y]).toEqual([1, 1]);
		expect(a.isAuthored).toBe(true);
		expect(b.isAuthored).toBe(true);
		expect(c.isAuthored).toBe(true);
	});

	it('places a conflicting north neighbour in the north half-plane', () => {
		const placements = computeCardinalLayout(
			[
				node('entry', { sortOrder: 0, isEntry: true }),
				node('square', { sortOrder: 1 }),
				node('north-exit', { sortOrder: 2 })
			],
			[edge('entry', 'square', 'South'), edge('square', 'north-exit', 'North')]
		);

		expect(byId(placements, 'north-exit').y).toBeLessThan(byId(placements, 'square').y);
	});

	it('rejects authored layouts that contradict directions', () => {
		const placements = computeCardinalLayout(
			[
				node('a', { layoutX: 0, layoutY: 0, sortOrder: 0, isEntry: true }),
				node('b', { layoutX: 1, layoutY: 0, sortOrder: 1 })
			],
			[edge('a', 'b', 'North')]
		);

		const a = byId(placements, 'a');
		const b = byId(placements, 'b');
		expect(placements.every((placement) => !placement.isAuthored)).toBe(true);
		expect(b.y).toBeLessThan(a.y);
		expect(b.x).toBe(a.x);
	});

	it('keeps a sparse zone graph compact', () => {
		const placements = computeCardinalLayout(
			[
				node('entry', { layoutX: 30, layoutY: 18, sortOrder: 0, isEntry: true }),
				node('aurelian', { layoutX: 42, layoutY: 24, sortOrder: 20 }),
				node('square', { layoutX: 30, layoutY: 22, sortOrder: 30 }),
				node('hearthbound', { layoutX: 17, layoutY: 28, sortOrder: 40 }),
				node('low-quarter', { layoutX: 30, layoutY: 40, sortOrder: 50 }),
				node('varenmark', { layoutX: 30, layoutY: 0, sortOrder: 60 }),
				node('ashlen', { layoutX: 0, layoutY: 22, sortOrder: 70 }),
				node('droveway', { layoutX: 63, layoutY: 22, sortOrder: 80 })
			],
			[
				edge('entry', 'square', 'South'),
				edge('square', 'ashlen', 'West'),
				edge('varenmark', 'square', 'South'),
				edge('low-quarter', 'square', 'North'),
				edge('hearthbound', 'aurelian', 'East'),
				edge('square', 'droveway', 'East'),
				edge('square', 'low-quarter', 'South'),
				edge('ashlen', 'square', 'East'),
				edge('square', 'varenmark', 'North'),
				edge('droveway', 'square', 'West'),
				edge('aurelian', 'square', 'West')
			]
		);

		const xs = placements.map((placement) => placement.x);
		const ys = placements.map((placement) => placement.y);

		expect(Math.max(...xs) - Math.min(...xs)).toBeLessThanOrEqual(3);
		expect(Math.max(...ys) - Math.min(...ys)).toBeLessThanOrEqual(3);
		expect(placements.every((placement) => !placement.isAuthored)).toBe(true);
	});

	it('ignores sparse tile layouts', () => {
		const placements = computeCardinalLayout(
			[
				node('a', { layoutX: 0, layoutY: 0, sortOrder: 1 }),
				node('b', { layoutX: 10, layoutY: 5, sortOrder: 2 }),
				node('c', { layoutX: 40, layoutY: 20, sortOrder: 3 }),
				node('d', { layoutX: 60, layoutY: 40, sortOrder: 4 })
			],
			[edge('a', 'b', 'East'), edge('b', 'c', 'East'), edge('c', 'd', 'East')]
		);

		expect([
			byId(placements, 'a').x,
			byId(placements, 'b').x,
			byId(placements, 'c').x,
			byId(placements, 'd').x
		]).toEqual([0, 1, 2, 3]);
		expect(placements.every((placement) => placement.y === 0)).toBe(true);
		expect(placements.every((placement) => !placement.isAuthored)).toBe(true);
	});

	it('resolves overlapping authored positions', () => {
		const placements = computeCardinalLayout([
			node('a', { layoutX: 0, layoutY: 0, sortOrder: 1 }),
			node('b', { layoutX: 0, layoutY: 0, sortOrder: 2 })
		]);

		const cells = new Set(placements.map((placement) => `${placement.x},${placement.y}`));
		expect(cells.size).toBe(2);
	});

	it('places an outgoing neighbour cardinally', () => {
		const placements = computeCardinalLayout(
			[node('a', { layoutX: 0, layoutY: 0, sortOrder: 1 }), node('b', { sortOrder: 2 })],
			[edge('a', 'b', 'North')]
		);

		const a = byId(placements, 'a');
		const b = byId(placements, 'b');
		expect(b.x).toBe(a.x);
		expect(b.y).toBe(a.y - 1);
		expect(b.isAuthored).toBe(false);
	});

	it('places an incoming neighbour cardinally', () => {
		const placements = computeCardinalLayout(
			[node('a', { sortOrder: 1 }), node('b', { layoutX: 5, layoutY: 5, sortOrder: 2 })],
			[edge('a', 'b', 'East')]
		);

		const a = byId(placements, 'a');
		const b = byId(placements, 'b');
		expect(a.y).toBe(b.y);
		expect(a.x).toBe(b.x - 1);
		expect(areCardinal(a, b)).toBe(true);
	});

	it('places disconnected nodes uniquely', () => {
		const placements = computeCardinalLayout([
			node('a', { sortOrder: 1 }),
			node('b', { sortOrder: 2 }),
			node('c', { sortOrder: 3 })
		]);

		const cells = new Set(placements.map((placement) => `${placement.x},${placement.y}`));
		expect(placements).toHaveLength(3);
		expect(cells.size).toBe(3);
	});

	it('is deterministic', () => {
		const nodes = [
			node('a', { layoutX: 2, layoutY: 2, sortOrder: 1 }),
			node('b', { sortOrder: 2 }),
			node('c', { sortOrder: 3 }),
			node('d', { layoutX: 2, layoutY: 2, sortOrder: 4 })
		];
		const edges = [edge('a', 'b', 'East'), edge('b', 'c', 'South'), edge('d', 'c', 'West')];

		const first = computeCardinalLayout(nodes, edges);
		const second = computeCardinalLayout(nodes, edges);

		expect(first).toEqual(second);
	});

	it('aligns a computed node to an authored target', () => {
		const placements = computeCardinalLayout(
			[node('a', { sortOrder: 1 }), node('b', { layoutX: 10, layoutY: 10, sortOrder: 2 })],
			[edge('a', 'b', 'East')]
		);

		expect(areCardinal(byId(placements, 'a'), byId(placements, 'b'))).toBe(true);
	});

	it('returns empty for no nodes', () => {
		expect(computeCardinalLayout([], [])).toEqual([]);
	});
});

describe('areCardinal', () => {
	it('detects row, column, and diagonal placements', () => {
		const origin: CardinalNodePlacement = { nodeId: 'a', x: 0, y: 0, isAuthored: false };

		expect(areCardinal(origin, { nodeId: 'b', x: 0, y: 3, isAuthored: false })).toBe(true);
		expect(areCardinal(origin, { nodeId: 'c', x: -2, y: 0, isAuthored: false })).toBe(true);
		expect(areCardinal(origin, { nodeId: 'd', x: 1, y: 1, isAuthored: false })).toBe(false);
	});
});
