<script lang="ts">
	import { Trash2 } from '@lucide/svelte';
	import { DIRECTIONS, EDGE_TYPES, reciprocalOf } from '#lib/foundry/map-edges';
	import type { MapEdgeDto, MapGraphDto } from '#lib/foundry/map-editor';

	/**
	 * The edges of a map, and the fields that are not worth a gesture.
	 *
	 * The direction is a select rather than a drag because `MapDirection` includes `Up`, `Down`, `In` and
	 * `Out`, which a two-dimensional drag cannot express. Deleting a connection that runs both ways
	 * removes both directions — a stranded one-way edge is how a room becomes a trap.
	 */
	let {
		graph,
		canEdit,
		onPatch,
		onDelete
	}: {
		graph: MapGraphDto;
		canEdit: boolean;
		onPatch: (edgeId: string, patch: Partial<MapEdgeDto>) => void;
		onDelete: (edgeId: string) => void;
	} = $props();
</script>

<section class="game-panel overflow-x-auto p-0" aria-label="Edges">
	<table class="table w-full text-sm">
		<thead>
			<tr>
				<th scope="col">From</th>
				<th scope="col">To</th>
				<th scope="col">Direction</th>
				<th scope="col">Type</th>
				<th scope="col">Label</th>
				<th scope="col">Hidden</th>
				<th scope="col">Walkable</th>
				<th scope="col"></th>
			</tr>
		</thead>
		<tbody>
			{#each graph.edges as edge (edge.id)}
				{@const twoWay = reciprocalOf(graph, edge) !== null}
				<tr>
					<td class="font-mono text-xs">{edge.fromNodeId}</td>
					<td class="font-mono text-xs">
						{edge.toNodeId}
						{#if twoWay}
							<span class="badge preset-tonal-surface text-[10px]" title="A return edge exists">
								↔
							</span>
						{/if}
					</td>
					<td>
						<select
							class="select w-28"
							value={edge.direction}
							disabled={!canEdit}
							onchange={(event) => onPatch(edge.id, { direction: event.currentTarget.value })}
						>
							{#each DIRECTIONS as direction (direction)}
								<option value={direction}>{direction}</option>
							{/each}
						</select>
					</td>
					<td>
						<select
							class="select w-36"
							value={edge.edgeType}
							disabled={!canEdit}
							onchange={(event) => onPatch(edge.id, { edgeType: event.currentTarget.value })}
						>
							{#each EDGE_TYPES as edgeType (edgeType)}
								<option value={edgeType}>{edgeType}</option>
							{/each}
						</select>
					</td>
					<td>
						<input
							class="input w-40"
							value={edge.label ?? ''}
							placeholder="—"
							disabled={!canEdit}
							oninput={(event) =>
								onPatch(edge.id, {
									label: event.currentTarget.value === '' ? null : event.currentTarget.value
								})}
						/>
					</td>
					<td>
						<select
							class="select w-24"
							value={String(edge.isHidden)}
							disabled={!canEdit}
							onchange={(event) =>
								onPatch(edge.id, { isHidden: event.currentTarget.value === 'true' })}
						>
							<option value="false">visible</option>
							<option value="true">hidden</option>
						</select>
					</td>
					<td>
						<select
							class="select w-24"
							value={String(edge.isTraversable)}
							disabled={!canEdit}
							onchange={(event) =>
								onPatch(edge.id, { isTraversable: event.currentTarget.value === 'true' })}
						>
							<option value="true">walkable</option>
							<option value="false">blocked</option>
						</select>
					</td>
					<td>
						{#if canEdit}
							<button
								class="btn preset-tonal-surface btn-sm"
								type="button"
								aria-label={`Delete the edge from ${edge.fromNodeId} to ${edge.toNodeId}`}
								onclick={() => onDelete(edge.id)}
							>
								<Trash2 size={14} aria-hidden="true" />
							</button>
						{/if}
					</td>
				</tr>
			{:else}
				<tr>
					<td colspan="8" class="p-3 text-sm text-surface-600-400">
						No edges yet. Select a node, press “Connect”, then pick the destination.
					</td>
				</tr>
			{/each}
		</tbody>
	</table>
</section>
