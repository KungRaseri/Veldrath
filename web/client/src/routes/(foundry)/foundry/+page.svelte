<script lang="ts">
	import { ArrowRight } from '@lucide/svelte';
	import FoundryEmptyState from '#lib/components/foundry/FoundryEmptyState.svelte';
	import FoundryPageHeader from '#lib/components/foundry/FoundryPageHeader.svelte';
	import FoundrySection from '#lib/components/foundry/FoundrySection.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>Foundry — Veldrath</title>
</svelte:head>

<FoundryPageHeader
	title="Foundry"
	description="Author the world, curate what other people author, and read the reference material behind both."
/>

{#if data.error}
	<p class="game-panel p-3 text-sm text-error-500" role="alert">{data.error}</p>
{/if}

<div class="grid gap-4 md:grid-cols-2">
	<FoundrySection
		title="Content"
		count={data.contentTypes}
		description="Every registered content type: items, quests, actors, dialogue, languages."
	>
		<a class="btn preset-tonal-surface btn-sm" href="/content">
			Browse content
			<ArrowRight size={15} aria-hidden="true" />
		</a>
	</FoundrySection>

	<FoundrySection
		title="Maps"
		count={data.maps}
		description="The authored world graph. A map enters the game only when a Curator approves it."
	>
		<p class="text-sm text-surface-600-400">{data.mapsInReview} awaiting a decision.</p>
		<a class="btn preset-tonal-surface btn-sm" href="/maps">
			Open the map editor
			<ArrowRight size={15} aria-hidden="true" />
		</a>
	</FoundrySection>

	<FoundrySection
		title="Review"
		count={data.canReview ? data.pendingSubmissions + data.mapsInReview : undefined}
	>
		{#if data.canReview}
			<p class="text-sm text-surface-600-400">
				{data.pendingSubmissions} submissions and {data.mapsInReview} maps are waiting for a decision.
			</p>
			<a class="btn preset-tonal-surface btn-sm" href="/review">
				Open the queue
				<ArrowRight size={15} aria-hidden="true" />
			</a>
		{:else}
			<FoundryEmptyState
				message="This account can author content but not review it — a Curator makes the decision."
			/>
		{/if}
	</FoundrySection>

	<FoundrySection
		title="Notifications"
		count={data.unreadNotifications}
		description="Review outcomes for your own submissions and maps."
	>
		<a class="btn preset-tonal-surface btn-sm" href="/notifications">
			Open the inbox
			<ArrowRight size={15} aria-hidden="true" />
		</a>
	</FoundrySection>
</div>
