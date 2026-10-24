<script lang="ts">
	import { resolve } from '$app/paths';
	import Pager from '#lib/components/Pager.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>Audit log — Admin</title>
</svelte:head>

<div class="mx-auto max-w-6xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={resolve('/(admin)/admin')}>← Admin</a>

	<header class="space-y-1">
		<h1 class="h1">Audit log</h1>
		<p class="text-surface-600-400">{data.total} recorded staff actions.</p>
	</header>

	{#if data.error}
		<p class="text-sm text-error-500">{data.error}</p>
	{/if}

	<form class="flex flex-wrap items-end gap-3 card p-4" method="GET">
		<label class="label flex-1">
			<span>Target account ID</span>
			<input class="input font-mono text-xs" type="text" name="targetId" value={data.targetId} />
		</label>
		<button class="btn preset-tonal-surface" type="submit">Filter</button>
	</form>

	<div class="overflow-x-auto card">
		<table class="table w-full text-sm">
			<thead>
				<tr>
					<th>When</th>
					<th>Actor</th>
					<th>Action</th>
					<th>Target</th>
					<th>Details</th>
				</tr>
			</thead>
			<tbody>
				{#each data.entries as entry (entry.id)}
					<tr>
						<td>{new Date(entry.occurredAt).toLocaleString()}</td>
						<td>{entry.actorUsername}</td>
						<td><span class="chip preset-tonal-surface text-xs">{entry.action}</span></td>
						<td>{entry.targetUsername ?? '—'}</td>
						<td class="max-w-xs truncate">{entry.details ?? '—'}</td>
					</tr>
				{:else}
					<tr>
						<td class="text-surface-600-400" colspan="5">No audit entries yet.</td>
					</tr>
				{/each}
			</tbody>
		</table>
	</div>

	<Pager
		page={data.page}
		pageSize={data.pageSize}
		total={data.total}
		basePath="/admin/audit"
		query={{ targetId: data.targetId }}
	/>
</div>
