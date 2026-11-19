<script lang="ts">
	import { Map as MapIcon, Plus } from '@lucide/svelte';
	import { mapState } from '#lib/foundry/map-editor';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>Maps — Foundry</title>
</svelte:head>

<section class="space-y-3">
	<header class="flex flex-wrap items-center justify-between gap-3">
		<div>
			<h1 class="game-panel-header">
				<MapIcon size={20} aria-hidden="true" />
				Maps
			</h1>
			<p class="text-sm text-surface-600-400">
				The authored world graph. A save is validated against the whole world before it is stored,
				and a map enters the game only when a reviewer approves it.
			</p>
		</div>
		<div class="flex flex-wrap items-center gap-2">
			<a class="btn preset-filled-primary-500" href="/maps/new">
				<Plus size={15} aria-hidden="true" />
				New map
			</a>
			<a class="btn preset-tonal-surface" href="/content">Back to content</a>
		</div>
	</header>

	<div class="game-panel overflow-x-auto p-0">
		<table class="table w-full text-sm">
			<thead>
				<tr>
					<th scope="col">Map</th>
					<th scope="col">Type</th>
					<th scope="col">Owner</th>
					<th scope="col">Nodes</th>
					<th scope="col">Edges</th>
					<th scope="col">Version</th>
					<th scope="col">State</th>
				</tr>
			</thead>
			<tbody>
				{#each data.maps as map (map.id)}
					{@const state = mapState(map)}
					<tr>
						<td>
							<a class="link" href={`/maps/${encodeURIComponent(map.id)}`}>
								{map.name}
							</a>
							<p class="text-xs text-surface-600-400">
								{map.id}
								{#if map.canEdit === false}
									· read-only
								{/if}
							</p>
						</td>
						<td>{map.mapType}</td>
						<td>{map.ownerId}</td>
						<td>{map.nodeCount}</td>
						<td>{map.edgeCount}</td>
						<td>v{map.version}</td>
						<td>
							<span
								class="badge text-xs"
								class:preset-tonal-primary={state === 'Live'}
								class:preset-tonal-surface={state !== 'Live'}
								class:text-warning-500={state === 'Changed since approval'}
							>
								{state}
							</span>
						</td>
					</tr>
				{:else}
					<tr>
						<td class="text-surface-600-400" colspan="7"> No maps yet. Start one from New map. </td>
					</tr>
				{/each}
			</tbody>
		</table>
	</div>
</section>
