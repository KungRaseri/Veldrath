<script lang="ts">
	import { ArrowRight, Plus } from '@lucide/svelte';
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
		title="New content"
		description="Pick what you are making. Editors write straight into the catalog; everyone else submits it for a Curator to review."
	>
		<a class="btn preset-filled-primary-500 btn-sm" href="/new">
			<Plus size={15} aria-hidden="true" />
			Start something
		</a>
	</FoundrySection>

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

	{#if data.canReview}
		<FoundrySection
			title="Review"
			count={data.pendingSubmissions + data.mapsInReview}
			description="Everything waiting for a decision, submissions and maps in one queue."
		>
			<a class="btn preset-tonal-surface btn-sm" href="/review">
				Open the queue
				<ArrowRight size={15} aria-hidden="true" />
			</a>
		</FoundrySection>
	{/if}

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
