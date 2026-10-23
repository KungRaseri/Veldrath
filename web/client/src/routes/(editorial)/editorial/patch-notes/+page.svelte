<script lang="ts">
	import { resolve } from '$app/paths';
	import Pager from '$lib/components/Pager.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>Patch notes — Editorial</title>
</svelte:head>

<div class="mx-auto max-w-5xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={resolve('/editorial')}>← Editorial</a>

	<header class="flex flex-wrap items-end justify-between gap-3">
		<div>
			<h1 class="h1">Patch notes</h1>
			<p class="text-surface-600-400">{data.total} patch notes, including drafts.</p>
		</div>
		<a class="btn preset-filled-primary-500" href={resolve('/editorial/patch-notes/new')}>
			New patch note
		</a>
	</header>

	{#if data.error}
		<p class="text-sm text-error-500">{data.error}</p>
	{/if}

	<div class="overflow-x-auto card">
		<table class="table w-full text-sm">
			<thead>
				<tr>
					<th>Title</th>
					<th>Slug</th>
					<th>Version</th>
					<th>Status</th>
					<th>Published</th>
				</tr>
			</thead>
			<tbody>
				{#each data.notes as note (note.id)}
					<tr>
						<td><a class="anchor" href={`/editorial/patch-notes/${note.id}`}>{note.title}</a></td>
						<td class="font-mono text-xs">{note.slug}</td>
						<td>{note.version}</td>
						<td><span class="chip preset-tonal-surface text-xs">{note.status}</span></td>
						<td>{note.publishedAt ? new Date(note.publishedAt).toLocaleDateString() : '—'}</td>
					</tr>
				{:else}
					<tr>
						<td class="text-surface-600-400" colspan="5">No patch notes yet.</td>
					</tr>
				{/each}
			</tbody>
		</table>
	</div>

	<Pager
		page={data.page}
		pageSize={data.pageSize}
		total={data.total}
		basePath="/editorial/patch-notes"
	/>
</div>
