<script lang="ts">
	import { ArrowRight, Send } from '@lucide/svelte';
	import FoundryEmptyState from '#lib/components/foundry/FoundryEmptyState.svelte';
	import FoundryPageHeader from '#lib/components/foundry/FoundryPageHeader.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>New content — Veldrath</title>
</svelte:head>

<FoundryPageHeader
	title="New content"
	description="Pick what you are making. Editors write straight into the catalog; everyone else submits their work for a Curator to review."
/>

{#if data.error}
	<p class="game-panel p-3 text-sm text-error-500" role="alert">{data.error}</p>
{/if}

{#if data.types.length === 0}
	<FoundryEmptyState
		message="No content types are registered yet, so there is nothing to create."
	/>
{:else}
	<ul class="grid gap-3 md:grid-cols-2">
		{#each data.types as type (type.contentType)}
			<li class="game-panel flex flex-col p-4">
				<h2 class="h4">{type.displayLabel}</h2>
				<p class="mt-1 flex-1 text-sm text-surface-600-400">{type.description}</p>
				<div class="mt-3 flex flex-wrap gap-2">
					{#if data.canManageContent}
						<a class="btn preset-filled-primary-500 btn-sm" href="/content/{type.contentType}/new">
							Create
							<ArrowRight size={15} aria-hidden="true" />
						</a>
					{/if}
					<a
						class="btn preset-tonal-surface btn-sm"
						href="/submissions/new?type={type.contentType}"
					>
						<Send size={15} aria-hidden="true" />
						Submit for review
					</a>
				</div>
			</li>
		{/each}
	</ul>
{/if}
