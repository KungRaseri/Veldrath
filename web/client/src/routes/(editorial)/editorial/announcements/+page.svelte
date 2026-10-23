<script lang="ts">
	import { resolve } from '$app/paths';
	import Pager from '$lib/components/Pager.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>Announcements — Editorial</title>
</svelte:head>

<div class="mx-auto max-w-5xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={resolve('/editorial')}>← Editorial</a>

	<header class="flex flex-wrap items-end justify-between gap-3">
		<div>
			<h1 class="h1">Announcements</h1>
			<p class="text-surface-600-400">{data.total} announcements, including drafts.</p>
		</div>
		<a class="btn preset-filled-primary-500" href={resolve('/editorial/announcements/new')}>
			New announcement
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
					<th>Status</th>
					<th>Published</th>
				</tr>
			</thead>
			<tbody>
				{#each data.announcements as announcement (announcement.id)}
					<tr>
						<td>
							<a class="anchor" href={`/editorial/announcements/${announcement.id}`}>
								{announcement.title}
							</a>
						</td>
						<td>
							<span class="chip preset-tonal-surface text-xs">{announcement.status}</span>
						</td>
						<td>
							{announcement.publishedAt
								? new Date(announcement.publishedAt).toLocaleDateString()
								: '—'}
						</td>
					</tr>
				{:else}
					<tr>
						<td class="text-surface-600-400" colspan="3">No announcements yet.</td>
					</tr>
				{/each}
			</tbody>
		</table>
	</div>

	<Pager
		page={data.page}
		pageSize={data.pageSize}
		total={data.total}
		basePath="/editorial/announcements"
	/>
</div>
