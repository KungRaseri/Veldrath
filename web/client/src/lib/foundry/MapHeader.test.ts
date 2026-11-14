import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';
import MapHeader from './MapHeader.svelte';
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
		description: 'Root cellars under Fenwick.',
		entryNodeId: 'entry',
		nodes: [
			node({ id: 'entry', displayName: 'Arrival', isEntryPoint: true }),
			node({ id: 'hall', displayName: 'The Hall' })
		],
		edges: [],
		version: 3
	};
}

describe('MapHeader', () => {
	it('shows the map, its kind and its owner', () => {
		render(MapHeader, { graph: graph(), canEdit: true, onPatch: () => {} });

		expect(screen.getByLabelText('Name')).toHaveProperty('value', 'The Halrow');
		expect(screen.getByText('Zone')).toBeTruthy();
		expect(screen.getByText('the-halrow')).toBeTruthy();
	});

	it('reports a name or description change', async () => {
		const onPatch = vi.fn();
		render(MapHeader, { graph: graph(), canEdit: true, onPatch });

		await fireEvent.input(screen.getByLabelText('Name'), { target: { value: 'The Halrow Deeps' } });
		await fireEvent.input(screen.getByLabelText('Description'), { target: { value: 'Deeper.' } });

		expect(onPatch).toHaveBeenCalledWith({ name: 'The Halrow Deeps' });
		expect(onPatch).toHaveBeenCalledWith({ description: 'Deeper.' });
	});

	it('reports a new entry node, and can clear it', async () => {
		const onPatch = vi.fn();
		render(MapHeader, { graph: graph(), canEdit: true, onPatch });

		await fireEvent.change(screen.getByLabelText('Entry node'), { target: { value: 'hall' } });
		expect(onPatch).toHaveBeenCalledWith({ entryNodeId: 'hall' });

		await fireEvent.change(screen.getByLabelText('Entry node'), { target: { value: '' } });
		expect(onPatch).toHaveBeenCalledWith({ entryNodeId: null });
	});

	it('locks every field for a reader', () => {
		render(MapHeader, { graph: graph(), canEdit: false, onPatch: () => {} });

		expect((screen.getByLabelText('Name') as HTMLInputElement).disabled).toBe(true);
		expect((screen.getByLabelText('Entry node') as HTMLSelectElement).disabled).toBe(true);
	});
});
