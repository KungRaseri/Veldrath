<script lang="ts">
	import { resolve } from '$app/paths';
	import Pager from '$lib/components/Pager.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>Community submissions — Foundry</title>
</svelte:head>

<div class="mx-auto max-w-5xl space-y-4 p-6">
	<header class="flex flex-wrap items-end justify-between gap-3">
		<div>
			<h1 class="h1">Community submissions</h1>
			<p class="text-surface-600-400">
				Review, vote on, and curate community-created items, powers, quests, and languages.
			</p>
		</div>
		<a class="btn preset-filled-primary-500" href={resolve('/submissions/new')}>New submission</a>
	</header>

	{#if data.error}
		<p class="text-sm text-error-500">{data.error}</p>
	{/if}

	<form class="grid gap-3 card p-4 sm:grid-cols-4" method="GET">
		<label class="label">
			<span>Search</span>
			<input class="input" type="search" name="search" value={data.filters.search ?? ''} />
		</label>
		<label class="label">
			<span>Status</span>
			<select class="select" name="status" value={data.filters.status ?? ''}>
				<option value="">Any</option>
				<option value="Pending">Pending</option>
				<option value="Approved">Approved</option>
				<option value="Rejected">Rejected</option>
			</select>
		</label>
		<label class="label">
			<span>Content type</span>
			<select class="select" name="contentType" value={data.filters.contentType ?? ''}>
				<option value="">Any</option>
				<option value="Item">Item</option>
				<option value="Spell">Spell</option>
				<option value="Ability">Ability</option>
				<option value="Npc">Npc</option>
				<option value="Quest">Quest</option>
				<option value="Recipe">Recipe</option>
				<option value="LootTable">LootTable</option>
				<option value="language">Language</option>
			</select>
		</label>
		<div class="flex items-end">
			<button class="btn w-full preset-tonal-surface" type="submit">Apply filters</button>
		</div>
	</form>

	<div class="overflow-x-auto card">
		<table class="table w-full text-sm">
			<thead>
				<tr>
					<th>Title</th>
					<th>Type</th>
					<th>Status</th>
					<th>Submitter</th>
					<th class="text-right">Votes</th>
					<th>Created</th>
				</tr>
			</thead>
			<tbody>
				{#each data.submissions as submission (submission.id)}
					<tr>
						<td>
							<a class="anchor" href={`/submissions/${submission.id}`}>{submission.title}</a>
						</td>
						<td>{submission.contentType}</td>
						<td><span class="chip preset-tonal-surface text-xs">{submission.status}</span></td>
						<td>{submission.submitterName}</td>
						<td class="text-right">{submission.voteScore}</td>
						<td>{new Date(submission.createdAt).toLocaleDateString()}</td>
					</tr>
				{:else}
					<tr>
						<td class="text-surface-600-400" colspan="6">No submissions match these filters.</td>
					</tr>
				{/each}
			</tbody>
		</table>
	</div>

	<Pager
		page={data.page}
		pageSize={data.pageSize}
		total={data.total}
		basePath="/submissions"
		query={{
			search: data.filters.search,
			status: data.filters.status,
			contentType: data.filters.contentType
		}}
	/>
</div>
