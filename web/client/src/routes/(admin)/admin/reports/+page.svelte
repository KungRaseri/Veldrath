<script lang="ts">
	import { resolve } from '$app/paths';
	import Pager from '$lib/components/Pager.svelte';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();
</script>

<svelte:head>
	<title>Player reports — Admin</title>
</svelte:head>

<div class="mx-auto max-w-5xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={resolve('/admin')}>← Admin</a>

	<header class="space-y-1">
		<h1 class="h1">Player reports</h1>
		<p class="text-surface-600-400">{data.total} reports.</p>
	</header>

	{#if form?.error || data.error}
		<p class="text-sm text-error-500">{form?.error ?? data.error}</p>
	{/if}

	<form class="flex flex-wrap items-end gap-3 card p-4" method="GET">
		<label class="label">
			<span>Status</span>
			<select class="select" name="resolved" value={data.resolved}>
				<option value="">All</option>
				<option value="false">Open</option>
				<option value="true">Resolved</option>
			</select>
		</label>
		<button class="btn preset-tonal-surface" type="submit">Filter</button>
	</form>

	<div class="overflow-x-auto card">
		<table class="table w-full text-sm">
			<thead>
				<tr>
					<th>Reporter</th>
					<th>Target</th>
					<th>Reason</th>
					<th>Submitted</th>
					<th>Status</th>
					<th></th>
				</tr>
			</thead>
			<tbody>
				{#each data.reports as report (report.id)}
					<tr>
						<td>{report.reporterName}</td>
						<td>{report.targetName}</td>
						<td class="max-w-xs truncate">{report.reason}</td>
						<td>{new Date(report.submittedAt).toLocaleString()}</td>
						<td>
							{#if report.isResolved}
								<span class="chip preset-tonal-surface text-xs">Resolved</span>
							{:else}
								<span class="chip preset-tonal-surface text-xs text-warning-500">Open</span>
							{/if}
						</td>
						<td>
							{#if !report.isResolved}
								<form method="POST" action="?/resolve">
									<input type="hidden" name="id" value={report.id} />
									<button class="btn preset-tonal-surface btn-sm" type="submit">Resolve</button>
								</form>
							{/if}
						</td>
					</tr>
				{:else}
					<tr>
						<td class="text-surface-600-400" colspan="6">No reports found.</td>
					</tr>
				{/each}
			</tbody>
		</table>
	</div>

	<Pager
		page={data.page}
		pageSize={data.pageSize}
		total={data.total}
		basePath="/admin/reports"
		query={{ resolved: data.resolved }}
	/>
</div>
