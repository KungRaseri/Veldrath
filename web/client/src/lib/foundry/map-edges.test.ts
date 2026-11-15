import { describe, expect, it } from 'vitest';
import { connectNodes, deleteEdge, edgeDirection, hasEdge, reciprocalOf } from './map-edges';
import type { MapEdgeDto, MapGraphDto, MapNodeDto } from './map-editor';

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

function edge(
	fromNodeId: string,
	toNodeId: string,
	direction = 'South',
	id = `${fromNodeId}->${toNodeId}`
): MapEdgeDto {
	return {
		id,
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

/** entry at (0,0), hall south of it, vault east of the hall, and a diagonal node for the refusal case. */
function graph(edges: MapEdgeDto[] = [edge('entry', 'hall')]): MapGraphDto {
	return {
		id: 'zone:the-halrow',
		mapType: 'Zone',
		ownerId: 'the-halrow',
		name: 'The Halrow',
		description: '',
		entryNodeId: 'entry',
		nodes: [
			node({ id: 'entry', nodeType: 'EntryPoint', isEntryPoint: true, layoutX: 0, layoutY: 0 }),
			node({ id: 'hall', layoutX: 0, layoutY: 1 }),
			node({ id: 'vault', layoutX: 1, layoutY: 1 }),
			node({ id: 'diagonal', layoutX: 3, layoutY: 3 })
		],
		edges,
		version: 3
	};
}

describe('edgeDirection', () => {
	it('reads the direction off the layout', () => {
		expect(edgeDirection(graph(), 'hall', 'vault')).toBe('East');
		expect(edgeDirection(graph(), 'vault', 'hall')).toBe('West');
	});

	it('refuses a diagonal pair', () => {
		expect(edgeDirection(graph(), 'hall', 'diagonal')).toBeNull();
	});

	it('refuses when a node has no authored position', () => {
		const unplaced = graph();
		unplaced.nodes[1].layoutX = null;

		expect(edgeDirection(unplaced, 'hall', 'vault')).toBeNull();
	});
});

describe('connectNodes', () => {
	it('adds the edge with a minted id and the inferred direction', () => {
		const result = connectNodes(graph(), 'hall', 'vault');

		expect(result.ok).toBe(true);
		if (!result.ok) return;

		expect(result.edges).toHaveLength(1);
		expect(result.edges[0]).toMatchObject({
			fromNodeId: 'hall',
			toNodeId: 'vault',
			direction: 'East',
			edgeType: 'Walk',
			isTraversable: true
		});
		expect(result.edges[0].id).toMatch(/^[0-9a-f-]{36}$/);
	});

	it('refuses a self-reference', () => {
		const result = connectNodes(graph(), 'hall', 'hall');

		expect(result).toEqual({ ok: false, reason: expect.stringMatching(/itself/) });
	});

	it('refuses a direction that node already leaves by', () => {
		// The inferred direction is East, and hall already leaves East to the diagonal node.
		const result = connectNodes(graph([edge('hall', 'diagonal', 'East')]), 'hall', 'vault');

		expect(result.ok).toBe(false);
		if (!result.ok) expect(result.reason).toMatch(/already has an edge going East/);
	});

	it('refuses a diagonal pair instead of guessing a direction', () => {
		const result = connectNodes(graph(), 'hall', 'diagonal');

		expect(result.ok).toBe(false);
		if (!result.ok) expect(result.reason).toMatch(/not cardinally aligned/);
	});

	it('creates the return edge when asked, with the opposite direction', () => {
		const result = connectNodes(graph(), 'hall', 'vault', { reciprocal: true });

		expect(result.ok).toBe(true);
		if (!result.ok) return;

		expect(result.edges.map((candidate) => candidate.direction)).toEqual(['East', 'West']);
		expect(result.graph.edges).toHaveLength(3);
	});

	it('refuses a return edge that already exists', () => {
		const result = connectNodes(graph([edge('vault', 'hall', 'West')]), 'hall', 'vault', {
			reciprocal: true
		});

		expect(result.ok).toBe(false);
		if (!result.ok) expect(result.reason).toMatch(/return edge already exists/);
	});

	it('refuses a reciprocal when the return direction is already taken', () => {
		// vault already leaves West, so the return edge would duplicate its direction.
		const result = connectNodes(graph([edge('vault', 'entry', 'West')]), 'hall', 'vault', {
			reciprocal: true
		});

		expect(result.ok).toBe(false);
		if (!result.ok) expect(result.reason).toMatch(/already has an edge going West/);
	});

	it('refuses a reciprocal for a direction with no cardinal opposite', () => {
		const result = connectNodes(graph(), 'hall', 'vault', {
			direction: 'In',
			reciprocal: true
		});

		expect(result.ok).toBe(false);
		if (!result.ok) expect(result.reason).toMatch(/no cardinal opposite/);
	});

	it('honours an explicit direction and type', () => {
		const result = connectNodes(graph(), 'hall', 'vault', {
			direction: 'In',
			edgeType: 'Portal'
		});

		expect(result.ok).toBe(true);
		if (!result.ok) return;

		expect(result.edges[0]).toMatchObject({ direction: 'In', edgeType: 'Portal' });
	});
});

describe('deleteEdge', () => {
	it('removes only the edge it was given', () => {
		const twoWay = graph([edge('hall', 'vault', 'East', 'a'), edge('vault', 'hall', 'West', 'b')]);

		const next = deleteEdge(twoWay, 'a');

		expect(next.edges.map((candidate) => candidate.id)).toEqual(['b']);
	});

	it('removes the return edge too when asked', () => {
		const twoWay = graph([edge('hall', 'vault', 'East', 'a'), edge('vault', 'hall', 'West', 'b')]);

		const next = deleteEdge(twoWay, 'a', true);

		expect(next.edges).toEqual([]);
	});

	it('keeps a one-way edge when no return edge exists to remove', () => {
		const oneWay = graph([edge('hall', 'vault', 'East', 'a')]);

		const next = deleteEdge(oneWay, 'a', true);

		expect(next.edges).toEqual([]);
	});

	it('leaves the graph alone for an id it does not know', () => {
		const original = graph();

		expect(deleteEdge(original, 'nope')).toBe(original);
	});
});

describe('hasEdge and reciprocalOf', () => {
	it('answer for the pair, in the right direction', () => {
		const map = graph([edge('hall', 'vault', 'East', 'a')]);

		expect(hasEdge(map, 'hall', 'vault')).toBe(true);
		expect(hasEdge(map, 'vault', 'hall')).toBe(false);
		expect(reciprocalOf(map, map.edges[0])).toBeNull();
	});
});
