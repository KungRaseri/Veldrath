import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';
import EdgeList from './EdgeList.svelte';
import type { MapEdgeDto, MapGraphDto } from './map-editor';

function edge(overrides: Partial<MapEdgeDto> = {}): MapEdgeDto {
	return {
		id: 'a',
		mapId: 'zone:the-halrow',
		fromNodeId: 'entry',
		toNodeId: 'hall',
		direction: 'South',
		edgeType: 'Walk',
		label: null,
		isTraversable: true,
		isHidden: false,
		...overrides
	};
}

function graph(edges: MapEdgeDto[]): MapGraphDto {
	return {
		id: 'zone:the-halrow',
		mapType: 'Zone',
		ownerId: 'the-halrow',
		name: 'The Halrow',
		description: '',
		entryNodeId: 'entry',
		nodes: [],
		edges,
		version: 3
	};
}

const noop = () => {};

describe('EdgeList', () => {
	it('lists each edge and marks a two-way connection', () => {
		render(EdgeList, {
			graph: graph([
				edge(),
				edge({ id: 'b', fromNodeId: 'hall', toNodeId: 'entry', direction: 'North' })
			]),
			canEdit: true,
			onPatch: noop,
			onDelete: noop
		});

		expect(screen.getAllByRole('row')).toHaveLength(3);
		expect(screen.getAllByText('↔')).toHaveLength(2);
	});

	it('reports a direction change', async () => {
		const onPatch = vi.fn();
		render(EdgeList, { graph: graph([edge()]), canEdit: true, onPatch, onDelete: noop });

		await fireEvent.change(screen.getAllByRole('combobox')[0], { target: { value: 'East' } });

		expect(onPatch).toHaveBeenCalledWith('a', { direction: 'East' });
	});

	it('reports a label change and clears it when emptied', async () => {
		const onPatch = vi.fn();
		render(EdgeList, {
			graph: graph([edge({ label: 'Road' })]),
			canEdit: true,
			onPatch,
			onDelete: noop
		});

		await fireEvent.input(screen.getByDisplayValue('Road'), { target: { value: '' } });

		expect(onPatch).toHaveBeenCalledWith('a', { label: null });
	});

	it('reports a type, hidden and walkable change', async () => {
		const onPatch = vi.fn();
		render(EdgeList, { graph: graph([edge()]), canEdit: true, onPatch, onDelete: noop });

		const [direction, edgeType, hidden, walkable] = screen.getAllByRole('combobox');
		expect(direction).toBeTruthy();

		await fireEvent.change(edgeType, { target: { value: 'Portal' } });
		await fireEvent.change(hidden, { target: { value: 'true' } });
		await fireEvent.change(walkable, { target: { value: 'false' } });

		expect(onPatch).toHaveBeenCalledWith('a', { edgeType: 'Portal' });
		expect(onPatch).toHaveBeenCalledWith('a', { isHidden: true });
		expect(onPatch).toHaveBeenCalledWith('a', { isTraversable: false });
	});

	it('deletes through the row button', async () => {
		const onDelete = vi.fn();
		render(EdgeList, { graph: graph([edge()]), canEdit: true, onPatch: noop, onDelete });

		await fireEvent.click(
			screen.getByRole('button', { name: /Delete the edge from entry to hall/ })
		);

		expect(onDelete).toHaveBeenCalledWith('a');
	});

	it('explains the connect gesture when there are no edges, and hides the delete control from a reader', () => {
		render(EdgeList, { graph: graph([]), canEdit: false, onPatch: noop, onDelete: noop });

		expect(screen.getByText(/press “Connect”/)).toBeTruthy();
		expect(screen.queryByRole('button', { name: /Delete the edge/ })).toBeNull();
	});
});
