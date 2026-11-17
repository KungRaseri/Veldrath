<script lang="ts">
	import FoundryEmptyState from '#lib/components/foundry/FoundryEmptyState.svelte';
	import FoundryPageHeader from '#lib/components/foundry/FoundryPageHeader.svelte';
	import FoundrySection from '#lib/components/foundry/FoundrySection.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>Review — Foundry</title>
</svelte:head>

<FoundryPageHeader
	title="Review"
	description="Submissions and maps waiting for a decision. Each row opens the surface where the decision is recorded."
/>

{#if data.error}
	<p class="game-panel p-3 text-sm text-error-500" role="alert">{data.error}</p>
{/if}

<FoundrySection
	title="Submissions"
	count={data.submissions.length}
	description="Community contributions, newest first."
>
	{#if data.submissions.length === 0}
		<FoundryEmptyState
			message="Nothing is waiting. Approved and rejected submissions stay in the queue history."
		/>
	{:else}
		<ul class="divide-y divide-surface-200-800">
			{#each data.submissions as submission (submission.id)}
				<li class="flex flex-wrap items-center justify-between gap-2 py-2">
					<div>
						<a class="link" href={`/submissions/${submission.id}`}>{submission.title}</a>
						<p class="text-xs text-surface-600-400">
							{submission.contentType} · by {submission.submitterName}
						</p>
					</div>
					<span class="badge preset-tonal-surface text-xs">{submission.status}</span>
				</li>
			{/each}
		</ul>
	{/if}
</FoundrySection>

<FoundrySection
	title="Maps"
	count={data.maps.length}
	description="Submitted graphs that are not live yet."
>
	{#if data.maps.length === 0}
		<FoundryEmptyState
			message="No map is waiting for approval. A draft appears here once its author submits it."
		/>
	{:else}
		<ul class="divide-y divide-surface-200-800">
			{#each data.maps as map (map.id)}
				<li class="flex flex-wrap items-center justify-between gap-2 py-2">
					<div>
						<a class="link" href={`/maps/${encodeURIComponent(map.id)}`}>{map.name}</a>
						<p class="text-xs text-surface-600-400">
							{map.mapType} · {map.ownerId} · v{map.version}
						</p>
					</div>
					<a class="btn preset-tonal-surface btn-sm" href={`/maps/${encodeURIComponent(map.id)}`}>
						Open the graph
					</a>
				</li>
			{/each}
		</ul>
	{/if}
</FoundrySection>
