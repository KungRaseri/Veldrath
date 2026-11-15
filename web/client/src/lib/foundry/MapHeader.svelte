<script lang="ts">
	import type { MapGraphDto } from '#lib/foundry/map-editor';

	/**
	 * The map's own metadata.
	 *
	 * Name, description and the entry node are editable; the id, kind and owner are not. Moving a map to a
	 * different owner changes what the game resolves when a character enters that zone, so it is a separate
	 * decision rather than a field. The review state lands here in G6, once `IsActive` is on the contract.
	 */
	let {
		graph,
		canEdit,
		status = null,
		onPatch
	}: {
		graph: MapGraphDto;
		canEdit: boolean;
		status?: string | null;
		onPatch: (patch: Partial<MapGraphDto>) => void;
	} = $props();
</script>

<section class="game-panel grid gap-3 p-4 sm:grid-cols-2" aria-label="Map details">
	<label class="space-y-1 text-sm">
		<span class="text-surface-600-400">Name</span>
		<input
			class="input w-full"
			value={graph.name}
			disabled={!canEdit}
			oninput={(event) => onPatch({ name: event.currentTarget.value })}
		/>
	</label>

	<label class="space-y-1 text-sm">
		<span class="text-surface-600-400">Entry node</span>
		<select
			class="select w-full"
			value={graph.entryNodeId ?? ''}
			disabled={!canEdit}
			onchange={(event) =>
				onPatch({
					entryNodeId: event.currentTarget.value === '' ? null : event.currentTarget.value
				})}
		>
			<option value="">— none —</option>
			{#each graph.nodes as node (node.id)}
				<option value={node.id}>{node.displayName || node.id} · {node.id}</option>
			{/each}
		</select>
	</label>

	<label class="space-y-1 text-sm sm:col-span-2">
		<span class="text-surface-600-400">Description</span>
		<textarea
			class="textarea w-full"
			rows="2"
			value={graph.description}
			disabled={!canEdit}
			oninput={(event) => onPatch({ description: event.currentTarget.value })}></textarea>
	</label>

	<div class="space-y-1 text-sm">
		<span class="text-surface-600-400">Kind</span>
		<p class="font-mono text-xs">{graph.mapType}</p>
	</div>

	<div class="space-y-1 text-sm">
		<span class="text-surface-600-400">Owner</span>
		<p class="font-mono text-xs">{graph.ownerId}</p>
	</div>

	{#if status}
		<div class="space-y-1 text-sm">
			<span class="text-surface-600-400">State</span>
			<p class="text-xs">{status}</p>
		</div>
	{/if}
</section>
