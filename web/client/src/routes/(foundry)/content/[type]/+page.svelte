<script lang="ts">
	import { resolve } from '$app/paths';
	import Pager from '#lib/components/Pager.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>{data.contentType} content — Foundry</title>
</svelte:head>

<div class="mx-auto max-w-5xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={resolve('/(foundry)/content')}>← All content types</a>

	<header class="space-y-1">
		<h1 class="h1">{data.contentType}</h1>
		<p class="text-surface-600-400">{data.total} published entries.</p>
	</header>

	{#if data.error}
		<p class="text-sm text-error-500">{data.error}</p>
	{/if}

	<form class="flex flex-wrap items-end gap-3 card p-4" method="GET">
		<label class="label flex-1">
			<span>Search</span>
			<input class="input" type="search" name="search" value={data.search} />
		</label>
		<button class="btn preset-tonal-surface" type="submit">Search</button>
	</form>

	<div class="overflow-x-auto card">
		<table class="table w-full text-sm">
			<thead>
				<tr>
					<th>Slug</th>
					<th>Name</th>
					<th>Category</th>
					<th class="text-right">Rarity</th>
					<th>Updated</th>
				</tr>
			</thead>
			<tbody>
				{#each data.entries as entry (entry.id)}
					<tr>
						<td>
							<a class="anchor" href={`/content/${entry.contentType}/${entry.slug}`}>
								{entry.slug}
							</a>
						</td>
						<td>{entry.displayName ?? '—'}</td>
						<td>{entry.typeKey}</td>
						<td class="text-right">{entry.rarityWeight}</td>
						<td>{new Date(entry.updatedAt).toLocaleDateString()}</td>
					</tr>
				{:else}
					<tr>
						<td class="text-surface-600-400" colspan="5">No entries found.</td>
					</tr>
				{/each}
			</tbody>
		</table>
	</div>

	<Pager
		page={data.page}
		pageSize={data.pageSize}
		total={data.total}
		basePath={`/content/${data.contentType}`}
		query={{ search: data.search }}
	/>
</div>
