import { describe, expect, it } from 'vitest';
import {
	issueLabel,
	issuesByNode,
	mapState,
	nodeRows,
	normalizeDraft,
	parseDraftGraph,
	type MapGraphDto,
	type MapGraphIssue,
	type MapNodeDto
} from './map-editor';

describe('issuesByNode', () => {
	const issue = (nodeId: string | null, code: string): MapGraphIssue => ({
		code,
		message: code,
		severity: 'Error',
		mapId: 'zone:the-halrow',
		nodeId
	});

	it('files a node issue beside that node', () => {
		const grouped = issuesByNode([issue('vault', 'unlock_key_missing')]);

		expect(grouped.nodes.vault).toHaveLength(1);
		expect(grouped.map).toEqual([]);
	});

	it('keeps a map-level issue in the summary instead of dropping it', () => {
		// Dropping it would hide the reason a save is refused, since no row can render it.
		const grouped = issuesByNode([issue(null, 'map_without_exit')]);

		expect(grouped.map.map((entry) => entry.code)).toEqual(['map_without_exit']);
		expect(grouped.nodes).toEqual({});
	});

	it('collects several issues on one node and leaves the rest untouched', () => {
		const grouped = issuesByNode([
			issue('vault', 'unlock_key_missing'),
			issue('vault', 'node_unreachable'),
			issue('threshold', 'node_name_missing')
		]);

		expect(grouped.nodes.vault).toHaveLength(2);
		expect(grouped.nodes.threshold).toHaveLength(1);
	});
});

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

	it('reads the draft a page posted', () => {
		const original = graph();
		const posted = {
			...original,
			nodes: original.nodes.map((candidate) =>
				candidate.id === 'threshold' ? { ...candidate, layoutX: 4, layoutY: 5 } : candidate
			)
		};

		const parsed = parseDraftGraph(JSON.stringify(posted), original, original.id);

		expect(parsed.nodes.find((candidate) => candidate.id === 'threshold')).toMatchObject({
			layoutX: 4,
			layoutY: 5
		});
	});

	it('ignores a draft for a different map', () => {
		const original = graph();

		expect(
			parseDraftGraph(JSON.stringify({ ...original, id: 'zone:crestfall' }), original, original.id)
		).toBe(original);
	});

	it('ignores a malformed draft rather than writing something nobody authored', () => {
		const original = graph();

		expect(parseDraftGraph('not json', original, original.id)).toBe(original);
		expect(parseDraftGraph(JSON.stringify({ id: original.id }), original, original.id)).toBe(
			original
		);
		expect(parseDraftGraph(null, original, original.id)).toBe(original);
	});

	it('normalises a blank unlock key or exit target to null when the draft is posted', () => {
		const normalized = normalizeDraft({
			...graph(),
			nodes: [
				node({ id: 'threshold', unlockKey: '  relic-chamber  ', targetMapId: '   ' }),
				node({ id: 'vault', unlockKey: null })
			]
		});

		expect(normalized.nodes.find((candidate) => candidate.id === 'threshold')).toMatchObject({
			unlockKey: 'relic-chamber',
			targetMapId: null
		});
		expect(normalized.nodes.find((candidate) => candidate.id === 'vault')?.unlockKey).toBeNull();
	});

	it('names the lifecycle state from the two numbers that define it', () => {
		expect(mapState({ isActive: false, version: 3, submittedAt: null })).toBe('Draft');
		expect(mapState({ isActive: false, version: 3, submittedAt: '2026-01-02T00:00:00Z' })).toBe(
			'In review'
		);
		expect(mapState({ isActive: true, version: 3, approvedVersion: 3 })).toBe('Live');
		expect(mapState({ isActive: true, version: 4, approvedVersion: 3 })).toBe(
			'Changed since approval'
		);
		// A payload from before the review gate carries no state, and the contract's default is live.
		expect(mapState({ version: 1 })).toBe('Changed since approval');
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
