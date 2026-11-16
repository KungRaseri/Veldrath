<script lang="ts">
	import Pager from '#lib/components/Pager.svelte';
	import FoundryPageHeader from '#lib/components/foundry/FoundryPageHeader.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	const showingAll = $derived(data.scope === 'all');
</script>

<svelte:head>
	<title>{showingAll ? 'Community submissions' : 'My submissions'} — Foundry</title>
</svelte:head>

<div class="space-y-4">
	<div class="flex flex-wrap items-end justify-between gap-3">
		<FoundryPageHeader
			title={showingAll ? 'Community submissions' : 'My submissions'}
			description={showingAll
				? 'Every submission in the portal, at every status.'
				: 'What you have written, its status, and what a Curator said about it.'}
		/>
		<a class="btn preset-filled-primary-500" href="/new">New content</a>
	</div>

	{#if data.canReview}
		<nav class="flex gap-2" aria-label="Submission scope">
			<a
				class="btn btn-sm"
				class:preset-filled-primary-500={!showingAll}
				class:preset-tonal-surface={showingAll}
				aria-current={!showingAll ? 'page' : undefined}
				href="/submissions">Mine</a
			>
			<a
				class="btn btn-sm"
				class:preset-filled-primary-500={showingAll}
				class:preset-tonal-surface={!showingAll}
				aria-current={showingAll ? 'page' : undefined}
				href="/submissions?scope=all">Everyone</a
			>
		</nav>
	{/if}

	{#if data.error}
		<p class="text-sm text-error-500">{data.error}</p>
	{/if}

	<form class="grid gap-3 card p-4 sm:grid-cols-4" method="GET">
		{#if showingAll}
			<input type="hidden" name="scope" value="all" />
		{/if}
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
						<td class="text-surface-600-400" colspan="6">
							{showingAll
								? 'No submissions match these filters.'
								: 'You have not submitted anything yet. Start one from New content.'}
						</td>
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
			contentType: data.filters.contentType,
			scope: showingAll ? 'all' : undefined
		}}
	/>
</div>
