<script lang="ts">
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
</script>

<div class="space-y-3">
	<div class="flex flex-wrap items-center gap-3 text-xs text-surface-600-400">
		<span class="inline-flex items-center gap-1">
			<span class="h-2 w-2 rounded-full bg-primary-500"></span> Current
		</span>
		<span class="inline-flex items-center gap-1">
			<span class="h-2 w-2 rounded-full bg-success-500"></span> Reachable
		</span>
		<span class="inline-flex items-center gap-1">
			<span class="h-2 w-2 rounded-full bg-surface-400-600"></span> Known
		</span>
		<span class="ml-auto">{map.mapType} · {map.nodes.length} nodes · {map.edges.length} routes</span
		>
	</div>

	<div class="grid gap-3 overflow-x-auto p-1" style={gridStyle}>
		{#each placements as placement (placement.nodeId)}
			{@const node = nodeById.get(placement.nodeId)}
			{#if node}
				{@const isCurrent = node.id === currentNodeId}
				{@const isSelected = node.id === selectedNodeId}
				{@const isReachable = adjacentNodeIds.includes(node.id)}
				<div style:grid-column={placement.x + 1} style:grid-row={placement.y + 1} class="min-w-0">
					<button
						class="flex h-full w-full flex-col gap-1 rounded border p-2 text-left transition {isSelected
							? 'border-primary-500 ring-2 ring-primary-500/40'
							: isCurrent
								? 'border-primary-500 bg-primary-500/10'
								: isReachable
									? 'border-success-500/60'
									: 'border-surface-200-800 opacity-70'}"
						onclick={() => onSelect(node.id)}
						aria-current={isCurrent ? 'location' : undefined}
						title={node.description}
					>
						<span class="flex items-center gap-2">
							{#if isCurrent}
								<span class="h-2 w-2 shrink-0 rounded-full bg-primary-500"></span>
							{:else if isReachable}
								<span class="h-2 w-2 shrink-0 rounded-full bg-success-500"></span>
							{:else}
								<span class="h-2 w-2 shrink-0 rounded-full bg-surface-400-600"></span>
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
									<span class="chip preset-tonal-surface text-[10px]">Entry</span>
								{/if}
								{#if node.isExitPoint}
									<span class="chip preset-tonal-surface text-[10px]">Exit</span>
								{/if}
							</span>
						{/if}
					</button>
				</div>
			{/if}
		{/each}
	</div>
</div>
