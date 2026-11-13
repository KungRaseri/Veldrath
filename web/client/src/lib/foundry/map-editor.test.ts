import { describe, expect, it } from 'vitest';
import {
	applyNodeEdits,
	issueLabel,
	nodeRows,
	readNodeEdit,
	type MapGraphDto,
	type MapNodeDto
} from './map-editor';

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
			node({ id: 'vault', displayName: 'The Vault', sortOrder: 20 }),
			node({ id: 'entry', nodeType: 'EntryPoint', isEntryPoint: true, sortOrder: 0 }),
			node({ id: 'threshold', displayName: 'The Threshold', sortOrder: 10 })
		],
		edges: [
			{
				id: '11111111-1111-1111-1111-111111111111',
				mapId: 'zone:the-halrow',
				fromNodeId: 'entry',
				toNodeId: 'threshold',
				direction: 'North',
				edgeType: 'Walk',
				label: null,
				isTraversable: true,
				isHidden: false
			}
		],
		version: 4
	};
}

describe('foundry map editor rules', () => {
	it('orders nodes the way the map shows them and counts their edges', () => {
		const rows = nodeRows(graph());

		expect(rows.map((row) => row.id)).toEqual(['entry', 'threshold', 'vault']);
		expect(rows[0]).toMatchObject({ outgoing: 1, incoming: 0 });
		expect(rows[1]).toMatchObject({ outgoing: 0, incoming: 1 });
	});

	it('reads the inspector values for one node out of the form', () => {
		const form = new FormData();
		form.set('node:threshold:displayName', 'The Threshold of Roots');
		form.set('node:threshold:isHidden', 'true');
		form.set('node:threshold:unlockKey', 'relic-chamber');
		form.set('node:threshold:targetMapId', 'region:varenmark');

		const edit = readNodeEdit(node({ id: 'threshold', displayName: 'The Threshold' }), form);

		expect(edit).toEqual({
			displayName: 'The Threshold of Roots',
			isHidden: true,
			unlockKey: 'relic-chamber',
			targetMapId: 'region:varenmark'
		});
	});

	it('falls back to the stored values when the form omits a field', () => {
		const edit = readNodeEdit(
			node({ id: 'threshold', displayName: 'The Threshold', unlockKey: 'keep-me' }),
			new FormData()
		);

		expect(edit.displayName).toBe('The Threshold');
		expect(edit.isHidden).toBe(false);
		expect(edit.unlockKey).toBe('keep-me');
	});

	it('applies only the edited fields and leaves edges and the version alone', () => {
		const original = graph();

		const edited = applyNodeEdits(original, {
			threshold: {
				displayName: 'The Threshold of Roots',
				isHidden: true,
				unlockKey: '  relic-chamber  ',
				targetMapId: ''
			}
		});

		const threshold = edited.nodes.find((candidate) => candidate.id === 'threshold');
		expect(threshold).toMatchObject({
			displayName: 'The Threshold of Roots',
			isHidden: true,
			unlockKey: 'relic-chamber',
			targetMapId: null
		});

		// The untouched node and every edge survive intact, and the version is the server's business.
		expect(edited.nodes.find((candidate) => candidate.id === 'vault')?.displayName).toBe(
			'The Vault'
		);
		expect(edited.edges).toEqual(original.edges);
		expect(edited.version).toBe(original.version);
	});

	it('treats a blank unlock key or target as the absence of one', () => {
		const edited = applyNodeEdits(graph(), {
			threshold: { displayName: 'x', isHidden: false, unlockKey: '   ', targetMapId: '  ' }
		});

		const threshold = edited.nodes.find((candidate) => candidate.id === 'threshold');
		expect(threshold?.unlockKey).toBeNull();
		expect(threshold?.targetMapId).toBeNull();
	});

	it('labels an issue with the node it belongs to', () => {
		expect(
			issueLabel({
				code: 'node_unreachable',
				message: 'Node is not reachable.',
				severity: 'Error',
				mapId: 'zone:the-halrow',
				nodeId: 'vault'
			})
		).toBe('node_unreachable: Node is not reachable. (vault)');

		expect(
			issueLabel({
				code: 'map_id_missing',
				message: 'Map id is required.',
				severity: 'Error',
				mapId: null,
				nodeId: null
			})
		).toBe('map_id_missing: Map id is required.');
	});
});
