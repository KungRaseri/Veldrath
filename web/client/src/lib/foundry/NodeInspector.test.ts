import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';
import NodeInspector from './NodeInspector.svelte';
import type { MapNodeDto, MapTargets } from './map-editor';

const targets: MapTargets = {
	maps: [{ id: 'region:varenmark', label: 'Varenmark' }],
	zones: [{ id: 'the-halrow', label: 'The Halrow' }],
	locations: [{ id: 'halrow-threshold', label: 'The Threshold' }],
	regions: [{ id: 'varenmark', label: 'Varenmark' }],
	nodes: [
		{
			id: 'region:varenmark:entry',
			mapId: 'region:varenmark',
			label: 'Arrival',
			isEntryPoint: true
		},
		{
			id: 'region:varenmark:fenwick',
			mapId: 'region:varenmark',
			label: 'Fenwick',
			isEntryPoint: false
		},
		{ id: 'zone:the-halrow:entry', mapId: 'zone:the-halrow', label: 'Arrival', isEntryPoint: true }
	]
};

function node(overrides: Partial<MapNodeDto> = {}): MapNodeDto {
	return {
		id: 'halrow-threshold',
		mapId: 'zone:the-halrow',
		nodeType: 'Location',
		refId: 'halrow-threshold',
		displayName: 'The Threshold',
		description: '',
		iconKey: null,
		layoutX: 1,
		layoutY: 1,
		isEntryPoint: false,
		isExitPoint: false,
		targetMapId: null,
		targetNodeId: null,
		isHidden: false,
		unlockType: null,
		unlockKey: null,
		discoverThreshold: null,
		sortOrder: 10,
		...overrides
	};
}

const noop = () => {};

describe('NodeInspector', () => {
	it('offers the reference options that match the node kind', () => {
		render(NodeInspector, { node: node(), targets, canEdit: true, onPatch: noop, onDelete: noop });

		expect(screen.getByRole('option', { name: /The Threshold/ })).toBeTruthy();
		expect(screen.queryByRole('option', { name: /Varenmark/ })).toBeNull();
	});

	it('reports a label edit without owning the draft', async () => {
		const onPatch = vi.fn();
		render(NodeInspector, { node: node(), targets, canEdit: true, onPatch, onDelete: noop });

		await fireEvent.input(screen.getByLabelText('Label'), { target: { value: 'The Deep Vault' } });

		expect(onPatch).toHaveBeenCalledWith({ displayName: 'The Deep Vault' });
	});

	it('asks for the key of a content-gated unlock and for a threshold of a skill check', () => {
		const { unmount } = render(NodeInspector, {
			node: node({ unlockType: 'quest' }),
			targets,
			canEdit: true,
			onPatch: noop,
			onDelete: noop
		});
		expect(screen.getByLabelText('Unlock key')).toBeTruthy();
		unmount();

		render(NodeInspector, {
			node: node({ unlockType: 'skill-check' }),
			targets,
			canEdit: true,
			onPatch: noop,
			onDelete: noop
		});
		expect(screen.getByLabelText('Discover threshold')).toBeTruthy();
	});

	it('shows a destination only for an exit kind', () => {
		const { unmount } = render(NodeInspector, {
			node: node(),
			targets,
			canEdit: true,
			onPatch: noop,
			onDelete: noop
		});
		expect(screen.queryByLabelText('Target map')).toBeNull();
		unmount();

		render(NodeInspector, {
			node: node({ nodeType: 'RegionExit', isExitPoint: true }),
			targets,
			canEdit: true,
			onPatch: noop,
			onDelete: noop
		});
		expect(screen.getByLabelText('Target map')).toBeTruthy();
	});

	it('chooses the target map entry node when the destination map changes', async () => {
		const onPatch = vi.fn();
		render(NodeInspector, {
			node: node({ nodeType: 'RegionExit', isExitPoint: true }),
			targets,
			canEdit: true,
			onPatch,
			onDelete: noop
		});

		await fireEvent.change(screen.getByLabelText('Target map'), {
			target: { value: 'region:varenmark' }
		});

		// Leaving the old node id behind would point the exit at a node that map does not have.
		expect(onPatch).toHaveBeenCalledWith({
			targetMapId: 'region:varenmark',
			targetNodeId: 'region:varenmark:entry'
		});
	});

	it('disables delete and shows why when the node may not go', () => {
		render(NodeInspector, {
			node: node(),
			targets,
			canEdit: true,
			blockedReason: 'This is the entry node.',
			onPatch: noop,
			onDelete: noop
		});

		const remove = screen.getByRole('button', { name: /Delete node/ }) as HTMLButtonElement;
		expect(remove.disabled).toBe(true);
		expect(screen.getByText('This is the entry node.')).toBeTruthy();
	});
});
