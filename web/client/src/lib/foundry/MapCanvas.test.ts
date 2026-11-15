import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';
import MapCanvas from './MapCanvas.svelte';
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

function graph(): MapGraphDto {
	return {
		id: 'zone:the-halrow',
		mapType: 'Zone',
		ownerId: 'the-halrow',
		name: 'The Halrow',
		description: '',
		entryNodeId: 'entry',
		nodes: [
			node({ id: 'entry', nodeType: 'EntryPoint', isEntryPoint: true, layoutX: 0, layoutY: 0 }),
			node({ id: 'hall', displayName: 'The Hall', layoutX: 0, layoutY: 1 }),
			node({ id: 'vault', displayName: 'The Vault' })
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
			}
		],
		version: 3
	};
}

describe('MapCanvas', () => {
	it('draws the positioned nodes and lists the unpositioned ones separately', () => {
		render(MapCanvas, { graph: graph(), canEdit: true });

		expect(screen.getByText('The Hall')).toBeTruthy();
		expect(screen.getByText('not positioned')).toBeTruthy();
		expect(screen.getByText(/2 positioned · 1 not positioned/)).toBeTruthy();
	});

	it('nudges the focused node with the arrow keys', async () => {
		const onMove = vi.fn();
		render(MapCanvas, { graph: graph(), canEdit: true, onMove });

		const hall = screen.getByRole('button', { name: /The Hall/ });
		await fireEvent.keyDown(hall, { key: 'ArrowEast' });
		expect(onMove).not.toHaveBeenCalled();

		await fireEvent.keyDown(hall, { key: 'ArrowRight' });
		expect(onMove).toHaveBeenCalledWith('hall', { x: 1, y: 1 });
	});

	it('converts a pointer drag into whole cells', async () => {
		const onMove = vi.fn();
		render(MapCanvas, { graph: graph(), canEdit: true, onMove });

		const hall = screen.getByRole('button', { name: /The Hall/ });
		await fireEvent.pointerDown(hall, { clientX: 0, clientY: 0 });
		await fireEvent.pointerUp(hall, { clientX: 96 * 2, clientY: 96 });

		expect(onMove).toHaveBeenCalledWith('hall', { x: 2, y: 2 });
	});

	it('ignores a drag too small to be a cell, and any drag when read-only', async () => {
		const onMove = vi.fn();
		render(MapCanvas, { graph: graph(), canEdit: true, onMove });

		const hall = screen.getByRole('button', { name: /The Hall/ });
		await fireEvent.pointerDown(hall, { clientX: 0, clientY: 0 });
		await fireEvent.pointerUp(hall, { clientX: 12, clientY: 4 });
		expect(onMove).not.toHaveBeenCalled();
	});

	it('reports the selection', async () => {
		const onSelect = vi.fn();
		render(MapCanvas, { graph: graph(), canEdit: true, onSelect });

		await fireEvent.click(screen.getByRole('button', { name: /The Vault/ }));

		expect(onSelect).toHaveBeenCalledWith('vault');
	});

	it('marks an exit node and lists its unplaced neighbours for a reader', () => {
		const map = graph();
		map.nodes[1] = {
			...map.nodes[1],
			isExitPoint: true,
			targetMapId: 'region:varenmark',
			targetNodeId: 'region:varenmark:entry'
		};

		render(MapCanvas, { graph: map, canEdit: false });

		expect(screen.getByText('exit')).toBeTruthy();
		expect(screen.getByText('The Vault')).toBeTruthy();
		expect(screen.queryByRole('button', { name: /Tidy layout/ })).toBeNull();
	});

	it('offers tidy only to an editor, and ignores arrows without the permission', async () => {
		const onMove = vi.fn();
		render(MapCanvas, { graph: graph(), canEdit: false, onMove, onTidy: () => {} });

		expect(screen.queryByRole('button', { name: /Tidy layout/ })).toBeNull();

		await fireEvent.keyDown(screen.getByRole('button', { name: /The Hall/ }), {
			key: 'ArrowRight'
		});
		expect(onMove).not.toHaveBeenCalled();
	});
});
