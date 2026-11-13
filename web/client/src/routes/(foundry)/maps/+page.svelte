<script lang="ts">
	import { Map as MapIcon } from '@lucide/svelte';
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
				The authored world graph. A save is validated against the whole world before it is stored.
			</p>
		</div>
		<a class="btn preset-tonal-surface" href="/content">Back to content</a>
	</header>

	{#if !data.canEdit}
		<p class="game-panel p-3 text-sm text-warning-500">
			You can read the graphs but not save them: this account lacks the content permission.
		</p>
	{/if}

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
				</tr>
			</thead>
			<tbody>
				{#each data.maps as map (map.id)}
					<tr>
						<td>
							<a class="link" href={`/maps/${encodeURIComponent(map.id)}`}>
								{map.name}
							</a>
							<p class="text-xs text-surface-600-400">{map.id}</p>
						</td>
						<td>{map.mapType}</td>
						<td>{map.ownerId}</td>
						<td>{map.nodeCount}</td>
						<td>{map.edgeCount}</td>
						<td>v{map.version}</td>
					</tr>
				{/each}
			</tbody>
		</table>
	</div>
</section>
