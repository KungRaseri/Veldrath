<script lang="ts">
	import { CircleDot, DoorOpen, Flag, Footprints, Map as MapIcon, MapPin } from '@lucide/svelte';
	import { computeCardinalLayout } from './cardinal-layout';
	import type { MapGraph } from './types';

	let {
		map,
		currentNodeId,
		selectedNodeId,
		adjacentNodeIds = [],
		onSelect
	}: {
		map: MapGraph;
		currentNodeId: string | null;
		selectedNodeId: string | null;
		adjacentNodeIds?: string[];
		onSelect: (nodeId: string) => void;
	} = $props();

	const placements = $derived(computeCardinalLayout(map.nodes, map.edges));
	const columns = $derived(Math.max(0, ...placements.map((placement) => placement.x)) + 1);
	const rows = $derived(Math.max(0, ...placements.map((placement) => placement.y)) + 1);
	const nodeById = $derived(new Map(map.nodes.map((node) => [node.id, node])));

	const gridStyle = $derived(
		`grid-template-columns: repeat(${columns}, minmax(9rem, 1fr)); grid-template-rows: repeat(${rows}, minmax(4.5rem, auto));`
	);

	function directionsFor(nodeId: string): string {
		const labels = map.edges
			.filter((edge) => edge.fromNodeId === nodeId && edge.isTraversable && !edge.isHidden)
			.map((edge) => edge.direction);
		return [...new Set(labels)].join(' · ');
	}

	function connectorClass(direction: string): string {
		switch (direction.toLowerCase()) {
			case 'north':
				return 'map-connector-north';
			case 'south':
				return 'map-connector-south';
			case 'east':
				return 'map-connector-east';
			case 'west':
				return 'map-connector-west';
			default:
				return 'map-connector-hidden';
		}
	}
</script>

<div class="space-y-3">
	<div class="flex flex-wrap items-center gap-3 text-xs text-surface-600-400">
		<span class="inline-flex items-center gap-1">
			<CircleDot class="text-secondary-500" size={13} aria-hidden="true" />
			Current
		</span>
		<span class="inline-flex items-center gap-1">
			<Footprints class="text-success-500" size={13} aria-hidden="true" />
			Reachable
		</span>
		<span class="inline-flex items-center gap-1">
			<MapPin size={13} aria-hidden="true" />
			Known
		</span>
		<span class="ml-auto inline-flex items-center gap-1">
			<MapIcon size={13} aria-hidden="true" />
			{map.mapType} · {map.nodes.length} nodes · {map.edges.length} routes
		</span>
	</div>

	<div class="grid gap-3 overflow-x-auto p-1" style={gridStyle}>
		{#each placements as placement (placement.nodeId)}
			{@const node = nodeById.get(placement.nodeId)}
			{#if node}
				{@const isCurrent = node.id === currentNodeId}
				{@const isSelected = node.id === selectedNodeId}
				{@const isReachable = adjacentNodeIds.includes(node.id)}
				<div
					style:grid-column={placement.x + 1}
					style:grid-row={placement.y + 1}
					class="map-node-cell min-w-0"
				>
					{#each map.edges.filter((edge) => edge.fromNodeId === node.id) as edge (edge.id)}
						<span
							class="map-connector-line {connectorClass(edge.direction)} {edge.isTraversable
								? ''
								: 'map-connector-blocked'} {edge.isHidden ? 'map-connector-hidden' : ''}"
							aria-hidden="true"
						></span>
					{/each}
					<button
						class="flex h-full w-full flex-col gap-1 rounded border p-2 text-left text-surface-50 transition {isSelected
							? 'border-secondary-500 bg-secondary-500/10 ring-2 ring-secondary-500/30'
							: isCurrent
								? 'border-primary-500 bg-primary-500/10'
								: isReachable
									? 'border-success-500/60'
									: 'border-surface-800 opacity-75 hover:opacity-100'}"
						type="button"
						onclick={() => onSelect(node.id)}
						aria-current={isCurrent ? 'location' : undefined}
						title={node.description}
					>
						<span class="flex items-center gap-2">
							{#if isCurrent}
								<CircleDot class="shrink-0 text-secondary-500" size={14} aria-hidden="true" />
							{:else if isReachable}
								<Footprints class="shrink-0 text-success-500" size={14} aria-hidden="true" />
							{:else}
								<MapPin class="shrink-0 text-surface-600-400" size={14} aria-hidden="true" />
							{/if}
							<span class="truncate text-sm font-semibold">{node.displayName}</span>
						</span>

						<span class="truncate text-xs text-surface-600-400">
							{node.nodeType}
							{#if directionsFor(node.id)}
								· {directionsFor(node.id)}
							{/if}
						</span>

						{#if node.isEntryPoint || node.isExitPoint}
							<span class="flex flex-wrap gap-1">
								{#if node.isEntryPoint}
									<span class="text-info chip preset-tonal-surface text-[10px]">
										<DoorOpen size={10} aria-hidden="true" />
										Entry
									</span>
								{/if}
								{#if node.isExitPoint}
									<span class="chip preset-tonal-surface text-[10px] text-warning-500">
										<Flag size={10} aria-hidden="true" />
										Exit
									</span>
								{/if}
							</span>
						{/if}
					</button>
				</div>
			{/if}
		{/each}
	</div>
</div>
