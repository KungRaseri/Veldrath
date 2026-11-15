<script lang="ts">
	import { Map as MapIcon, TriangleAlert } from '@lucide/svelte';
	import { mapState } from '#lib/foundry/map-editor';
	import type { PageData } from './$types';

	let { data, form }: { data: PageData; form: { message?: string } | null } = $props();
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
		<a class="btn preset-tonal-surface" href="/content">Back to content</a>
	</header>

	{#if !data.canEdit}
		<p class="game-panel p-3 text-sm text-warning-500">
			You can read the graphs but not save them: this account lacks the content permission.
		</p>
	{/if}

	{#if form?.message}
		<p class="game-panel flex items-center gap-2 p-3 text-sm text-error-500" role="alert">
			<TriangleAlert size={15} aria-hidden="true" />
			{form.message}
		</p>
	{/if}

	{#if data.canEdit}
		<form
			method="POST"
			action="?/create"
			class="game-panel flex flex-wrap items-end gap-2 p-3"
			aria-label="Create a map"
		>
			<label class="space-y-1 text-sm">
				<span class="text-surface-600-400">Kind</span>
				<select class="select" name="mapType">
					<option value="Zone">Zone</option>
					<option value="Region">Region</option>
					<option value="Interior">Interior</option>
				</select>
			</label>
			<label class="space-y-1 text-sm">
				<span class="text-surface-600-400">Owner slug</span>
				<input class="input" name="ownerId" placeholder="the-halrow" required />
			</label>
			<label class="space-y-1 text-sm">
				<span class="text-surface-600-400">Name</span>
				<input class="input" name="name" placeholder="The Halrow" required />
			</label>
			<button class="btn preset-filled-primary-500 btn-sm" type="submit">Create draft</button>
		</form>
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
							<p class="text-xs text-surface-600-400">{map.id}</p>
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
				{/each}
			</tbody>
		</table>
	</div>
</section>
