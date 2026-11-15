import { describe, expect, it } from 'vitest';
import {
	MAX_CELL,
	clampCell,
	directionBetween,
	layoutCanvas,
	moveNode,
	nextFreeCell,
	nodeAt,
	tidyLayout
} from './map-canvas';
import type { MapGraphDto, MapNodeDto } from './map-editor';

function node(overrides: Partial<MapNodeDto> & Pick<MapNodeDto, 'id'>): MapNodeDto {
	return {
		mapId: 'zone:the-halrow',
		nodeType: 'Location',
		refId: overrides.id,
		displayName: overrides.id,
		description: '',
		iconKey: null,
		layoutX: null,
		layoutY: null,
		isEntryPoint: false,
		isExitPoint: false,
		targetMapId: null,
		targetNodeId: null,
		isHidden: false,
		unlockType: null,
		unlockKey: null,
		discoverThreshold: null,
		sortOrder: 0,
		...overrides
	};
}

function edge(fromNodeId: string, toNodeId: string, direction = 'South') {
	return {
		id: `${fromNodeId}->${toNodeId}`,
		mapId: 'zone:the-halrow',
		fromNodeId,
		toNodeId,
		direction,
		edgeType: 'Walk',
		label: null,
		isTraversable: true,
		isHidden: false
	};
}

/** Entry and hall are authored; the vault is not, and the exit hangs east off the hall. */
function graph(overrides: Partial<MapGraphDto> = {}): MapGraphDto {
	return {
		id: 'zone:the-halrow',
		mapType: 'Zone',
		ownerId: 'the-halrow',
		name: 'The Halrow',
		description: '',
		entryNodeId: 'entry',
		nodes: [
			node({ id: 'entry', nodeType: 'EntryPoint', isEntryPoint: true, layoutX: 2, layoutY: 2 }),
			node({ id: 'hall', displayName: 'The Hall', layoutX: 2, layoutY: 3 }),
			node({ id: 'vault', displayName: 'The Vault' }),
			node({
				id: 'exit',
				nodeType: 'RegionExit',
				isExitPoint: true,
				layoutX: 3,
				layoutY: 3,
				targetMapId: 'region:varenmark',
				targetNodeId: 'region:varenmark:entry'
			})
		],
		edges: [
			edge('entry', 'hall', 'South'),
			edge('hall', 'vault', 'South'),
			edge('hall', 'exit', 'East')
		],
		version: 3,
		...overrides
	};
}

describe('layoutCanvas', () => {
	it('separates the authored nodes from the one waiting for a position', () => {
		const layout = layoutCanvas(graph());

		expect(layout.placed.map((placement) => placement.id)).toEqual(['entry', 'hall', 'exit']);
		expect(layout.unplaced.map((placement) => placement.id)).toEqual(['vault']);
		expect(layout.unplaced[0].isAuthored).toBe(false);
	});

	it('reports the grid extent of the placed nodes only', () => {
		const { bounds } = layoutCanvas(graph());

		expect(bounds).toEqual({ minX: 2, minY: 2, maxX: 3, maxY: 3, width: 2, height: 2 });
	});

	it('reports an empty extent when nothing is placed', () => {
		const empty = graph({
			nodes: [node({ id: 'entry', nodeType: 'EntryPoint', isEntryPoint: true })]
		});

		expect(layoutCanvas(empty).bounds.width).toBe(0);
	});
});

describe('nodeAt', () => {
	it('finds the node occupying a cell, ignoring the one being dragged', () => {
		const { placed } = layoutCanvas(graph());

		expect(nodeAt(placed, { x: 2, y: 3 })?.id).toBe('hall');
		expect(nodeAt(placed, { x: 2, y: 3 }, 'hall')).toBeNull();
		expect(nodeAt(placed, { x: 9, y: 9 })).toBeNull();
	});
});

describe('moveNode', () => {
	it('writes the new coordinates', () => {
		const moved = moveNode(graph(), 'hall', { x: 5, y: 6 });
		const hall = moved.nodes.find((candidate) => candidate.id === 'hall')!;

		expect(hall.layoutX).toBe(5);
		expect(hall.layoutY).toBe(6);
	});

	it('refuses a cell another node already occupies, so nothing stacks', () => {
		const original = graph();

		expect(moveNode(original, 'entry', { x: 3, y: 3 })).toBe(original);
	});

	it('ignores a node that has no authored position yet', () => {
		const original = graph();

		expect(moveNode(original, 'vault', { x: 0, y: 0 })).toBe(original);
	});

	it('clamps a negative or oversized drag into the grid', () => {
		const moved = moveNode(graph(), 'entry', { x: -4, y: MAX_CELL + 40 });
		const entry = moved.nodes.find((candidate) => candidate.id === 'entry')!;

		expect(entry.layoutX).toBe(0);
		expect(entry.layoutY).toBe(MAX_CELL);
	});
});

describe('clampCell', () => {
	it('rounds, bounds and survives a non-finite value', () => {
		expect(clampCell(2.6)).toBe(3);
		expect(clampCell(-1)).toBe(0);
		expect(clampCell(Number.POSITIVE_INFINITY)).toBe(0);
	});
});

describe('directionBetween', () => {
	it('names the four cardinal directions', () => {
		expect(directionBetween({ x: 1, y: 1 }, { x: 1, y: 0 })).toBe('North');
		expect(directionBetween({ x: 1, y: 1 }, { x: 1, y: 2 })).toBe('South');
		expect(directionBetween({ x: 1, y: 1 }, { x: 2, y: 1 })).toBe('East');
		expect(directionBetween({ x: 1, y: 1 }, { x: 0, y: 1 })).toBe('West');
	});

	it('refuses a diagonal or an identical cell rather than guessing', () => {
		expect(directionBetween({ x: 1, y: 1 }, { x: 2, y: 2 })).toBeNull();
		expect(directionBetween({ x: 1, y: 1 }, { x: 1, y: 1 })).toBeNull();
	});
});

describe('nextFreeCell', () => {
	it('skips the cells that are taken', () => {
		const layout = layoutCanvas(graph());

		expect(nextFreeCell(layout, { x: 2, y: 2 })).toEqual({ x: 3, y: 2 });
	});
});

describe('tidyLayout', () => {
	it('walks the edges outward from the entry and authors every position', () => {
		const tidied = tidyLayout(graph());
		const at = (id: string) => {
			const found = tidied.nodes.find((candidate) => candidate.id === id)!;
			return { x: found.layoutX, y: found.layoutY };
		};

		expect(at('entry')).toEqual({ x: 2, y: 2 });
		expect(at('hall')).toEqual({ x: 2, y: 3 });
		expect(at('vault')).toEqual({ x: 2, y: 4 });
		expect(at('exit')).toEqual({ x: 3, y: 3 });
	});

	it('never puts two nodes in one cell', () => {
		const tidied = tidyLayout(graph());
		const cells = tidied.nodes.map((candidate) => `${candidate.layoutX}:${candidate.layoutY}`);

		expect(new Set(cells).size).toBe(cells.length);
	});

	it('still places a node the walk cannot reach, rather than looking like it dropped it', () => {
		const withIsland = tidyLayout(
			graph({
				nodes: [
					node({ id: 'entry', nodeType: 'EntryPoint', isEntryPoint: true }),
					node({ id: 'island' })
				],
				edges: []
			})
		);

		expect(withIsland.nodes.every((candidate) => candidate.layoutX !== null)).toBe(true);
		expect(withIsland.nodes.every((candidate) => candidate.layoutY !== null)).toBe(true);
	});
});
