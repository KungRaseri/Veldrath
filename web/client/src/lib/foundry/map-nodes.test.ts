import { describe, expect, it } from 'vitest';
import {
	addNode,
	deleteBlockedReason,
	deleteNode,
	refOptionsFor,
	suggestNodeId
} from './map-nodes';
import type { MapGraphDto, MapNodeDto, MapTargets } from './map-editor';

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

function graph(): MapGraphDto {
	return {
		id: 'zone:the-halrow',
		mapType: 'Zone',
		ownerId: 'the-halrow',
		name: 'The Halrow',
		description: '',
		entryNodeId: 'entry',
		nodes: [
			node({
				id: 'entry',
				nodeType: 'EntryPoint',
				isEntryPoint: true,
				layoutX: 0,
				layoutY: 0,
				sortOrder: 0
			}),
			node({ id: 'hall', displayName: 'The Hall', layoutX: 0, layoutY: 1, sortOrder: 10 })
		],
		edges: [
			{
				id: 'entry->hall',
				mapId: 'zone:the-halrow',
				fromNodeId: 'entry',
				toNodeId: 'hall',
				direction: 'South',
				edgeType: 'Walk',
				label: null,
				isTraversable: true,
				isHidden: false
			},
			{
				id: 'hall->entry',
				mapId: 'zone:the-halrow',
				fromNodeId: 'hall',
				toNodeId: 'entry',
				direction: 'North',
				edgeType: 'Walk',
				label: null,
				isTraversable: true,
				isHidden: false
			}
		],
		version: 3
	};
}

const targets: MapTargets = {
	maps: [
		{ id: 'region:varenmark', label: 'Varenmark' },
		{ id: 'zone:the-halrow', label: 'The Halrow' }
	],
	zones: [{ id: 'the-halrow', label: 'The Halrow' }],
	locations: [{ id: 'halrow-threshold', label: 'The Threshold' }],
	regions: [{ id: 'varenmark', label: 'Varenmark' }],
	nodes: [
		{ id: 'zone:the-halrow:entry', mapId: 'zone:the-halrow', label: 'Arrival', isEntryPoint: true }
	]
};

describe('refOptionsFor', () => {
	it('offers the catalog that matches the node kind', () => {
		expect(refOptionsFor('Zone', targets)).toEqual(targets.zones);
		expect(refOptionsFor('Location', targets)).toEqual(targets.locations);
		expect(refOptionsFor('RegionExit', targets)).toEqual(targets.regions);
	});

	it('offers nothing for a kind whose reference is free text', () => {
		expect(refOptionsFor('Landmark', targets)).toEqual([]);
	});
});

describe('suggestNodeId', () => {
	it('derives an id from the map and the kind', () => {
		expect(suggestNodeId(graph(), 'Location')).toBe('zone:the-halrow:location');
	});

	it('counts up when the derived id is taken', () => {
		const occupied = graph();
		occupied.nodes.push(node({ id: 'zone:the-halrow:location' }));

		expect(suggestNodeId(occupied, 'Location')).toBe('zone:the-halrow:location-2');
	});
});

describe('addNode', () => {
	it('appends a node after the last sort order, placed in a free cell', () => {
		const next = addNode(graph(), { nodeType: 'Location', displayName: '  The Vault  ' });
		const added = next.nodes[next.nodes.length - 1];

		expect(added.id).toBe('zone:the-halrow:location');
		expect(added.displayName).toBe('The Vault');
		expect(added.sortOrder).toBe(20);
		expect(added.mapId).toBe('zone:the-halrow');
		expect(added.layoutX).not.toBeNull();
		expect(added.layoutY).not.toBeNull();
		expect(added.refId).toBeNull();
	});

	it('carries the catalog slug when the caller already knows it', () => {
		// The zone panel adds a location node whose reference is already correct, so a slug cannot be mistyped.
		const next = addNode(graph(), {
			nodeType: 'Location',
			displayName: 'The Market',
			refId: 'halrow-market'
		});
		const added = next.nodes[next.nodes.length - 1];

		expect(added.refId).toBe('halrow-market');
		expect(added.displayName).toBe('The Market');
	});

	it('marks an exit kind as an exit and an entry kind as the entry', () => {
		const withExit = addNode(graph(), { nodeType: 'ZoneExit', displayName: 'North road' });
		expect(withExit.nodes[withExit.nodes.length - 1].isExitPoint).toBe(true);

		const withEntry = addNode(graph(), { nodeType: 'EntryPoint', displayName: 'Arrival' });
		expect(withEntry.nodes[withEntry.nodes.length - 1].isEntryPoint).toBe(true);
	});

	it('names an unnamed node rather than leaving a blank label the validator rejects', () => {
		const next = addNode(graph(), { nodeType: 'Location', displayName: '   ' });

		expect(next.nodes[next.nodes.length - 1].displayName).toBe('New node');
	});

	it('honours an explicit cell', () => {
		const next = addNode(graph(), {
			nodeType: 'Location',
			displayName: 'Vault',
			cell: { x: 9, y: 9 }
		});
		const added = next.nodes[next.nodes.length - 1];

		expect({ x: added.layoutX, y: added.layoutY }).toEqual({ x: 9, y: 9 });
	});
});

describe('deleteBlockedReason', () => {
	it('refuses the entry node first', () => {
		expect(deleteBlockedReason(graph(), 'entry')).toMatch(/entry node/i);
	});

	it('refuses any node still flagged as an entry point', () => {
		const flagged = graph();
		flagged.nodes.push(node({ id: 'old-entry', isEntryPoint: true }));

		expect(deleteBlockedReason(flagged, 'old-entry')).toMatch(/entry point/i);
	});

	it('refuses everything on a live map, because only a reviewer may change one', () => {
		expect(deleteBlockedReason(graph(), 'hall', true)).toMatch(/live/i);
	});

	it('allows an ordinary node', () => {
		expect(deleteBlockedReason(graph(), 'hall')).toBeNull();
	});
});

describe('deleteNode', () => {
	it('removes the node and every edge that touched it, in both directions', () => {
		const next = deleteNode(graph(), 'hall');

		expect(next.nodes.map((candidate) => candidate.id)).toEqual(['entry']);
		expect(next.edges).toEqual([]);
	});
});
