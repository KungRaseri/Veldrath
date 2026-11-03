import { describe, expect, it } from 'vitest';
import { areCardinal, computeCardinalLayout, type CardinalNodePlacement } from './cardinal-layout';
import type { MapEdge, MapNode } from './types';

/**
 * Branch-focused companion to `cardinal-layout.test.ts`: these cases drive the direction
 * switches, the authored-layout alignment arms, and the cell-search fallbacks that the
 * original suite never reaches.
 */
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
		id: `${from}->${to}->${direction}`,
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

describe('computeCardinalLayout direction handling', () => {
	it('keeps a dense authored grid whose edges use every cardinal direction', () => {
		const placements = computeCardinalLayout(
			[
				node('a', { layoutX: 0, layoutY: 0, sortOrder: 0 }),
				node('b', { layoutX: 1, layoutY: 0, sortOrder: 1 }),
				node('c', { layoutX: 0, layoutY: 1, sortOrder: 2 })
			],
			[
				// Two edges share a source, so the comparator falls through to the target
				// name and then the direction.
				edge('a', 'b', 'East'),
				edge('a', 'b', 'Elsewhere'),
				edge('a', 'c', 'South'),
				edge('b', 'a', 'West'),
				edge('c', 'b', 'South'),
				edge('c', 'a', 'North')
			]
		);

		expect(placements.every((placement) => placement.isAuthored)).toBe(true);
		expect([byId(placements, 'a').x, byId(placements, 'a').y]).toEqual([0, 0]);
		expect([byId(placements, 'b').x, byId(placements, 'b').y]).toEqual([1, 0]);
		expect([byId(placements, 'c').x, byId(placements, 'c').y]).toEqual([0, 1]);
	});

	it('falls back to a north vector for an unknown direction', () => {
		const placements = computeCardinalLayout(
			[node('a', { layoutX: 4, layoutY: 4, sortOrder: 0 }), node('b', { sortOrder: 1 })],
			[edge('a', 'b', 'Sideways')]
		);

		const a = byId(placements, 'a');
		const b = byId(placements, 'b');
		expect(b.x).toBe(a.x);
		expect(b.y).toBe(a.y - 1);
		expect(b.isAuthored).toBe(false);
	});
});

describe('computeCardinalLayout alignment', () => {
	it('realigns two computed neighbours that ended up diagonal', () => {
		const placements = computeCardinalLayout(
			[node('r', { sortOrder: 0 }), node('x', { sortOrder: 1 }), node('y', { sortOrder: 2 })],
			[edge('r', 'x', 'East'), edge('r', 'y', 'South'), edge('y', 'x', 'East')]
		);

		const x = byId(placements, 'x');
		const y = byId(placements, 'y');
		expect(areCardinal(x, y)).toBe(true);
		expect(x.y).toBe(y.y);
	});

	it('moves a computed node onto its authored neighbour', () => {
		const placements = computeCardinalLayout(
			[
				node('a', { layoutX: 0, layoutY: 0, sortOrder: 0 }),
				node('b', { layoutX: 1, layoutY: 0, sortOrder: 1 }),
				node('c', { layoutX: 1, layoutY: 1, sortOrder: 2 }),
				node('q', { sortOrder: 3 })
			],
			[edge('a', 'b', 'East'), edge('q', 'a', 'South'), edge('q', 'c', 'East')]
		);

		const q = byId(placements, 'q');
		const a = byId(placements, 'a');
		expect(q.isAuthored).toBe(false);
		expect(areCardinal(q, a)).toBe(true);
	});

	it('moves a computed node onto its authored neighbour down a column', () => {
		const placements = computeCardinalLayout(
			[
				node('a', { layoutX: 0, layoutY: 0, sortOrder: 0 }),
				node('b', { layoutX: 1, layoutY: 0, sortOrder: 1 }),
				node('c', { layoutX: 1, layoutY: 1, sortOrder: 2 }),
				node('q', { sortOrder: 3 })
			],
			[edge('a', 'b', 'East'), edge('q', 'a', 'South'), edge('q', 'c', 'North')]
		);

		const q = byId(placements, 'q');
		const c = byId(placements, 'c');
		expect(q.isAuthored).toBe(false);
		expect(areCardinal(q, c)).toBe(true);
	});

	it('walks past occupied cells on both sides of a direction vector', () => {
		const placements = computeCardinalLayout(
			['r', 'a', 'b', 'c', 'd', 'e', 'f'].map((id, index) => node(id, { sortOrder: index })),
			[
				edge('r', 'a', 'East'),
				edge('r', 'b', 'South'),
				edge('r', 'c', 'North'),
				edge('r', 'd', 'East'),
				edge('r', 'e', 'East'),
				edge('r', 'f', 'East')
			]
		);

		const cells = new Set(placements.map((placement) => `${placement.x},${placement.y}`));
		expect(cells.size).toBe(7);
	});
});

describe('computeCardinalLayout cell search', () => {
	it('places eight disconnected nodes on unique cells using both spiral halves', () => {
		const placements = computeCardinalLayout(
			['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h'].map((id, index) => node(id, { sortOrder: index }))
		);

		const cells = new Set(placements.map((placement) => `${placement.x},${placement.y}`));
		expect(placements).toHaveLength(8);
		expect(cells.size).toBe(8);
	});
});
