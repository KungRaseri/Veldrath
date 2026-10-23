<script lang="ts">
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>Base content — Foundry</title>
</svelte:head>

<div class="mx-auto max-w-5xl space-y-4 p-6">
	<header class="space-y-1">
		<h1 class="h1">Base content</h1>
		<p class="text-surface-600-400">
			Browse canonical game content authored in RealmForge and seeded into the database.
		</p>
	</header>

	{#if data.error}
		<p class="text-sm text-error-500">{data.error}</p>
	{/if}

	<div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
		{#each data.types as type (type.contentType)}
			<article class="flex flex-col gap-2 card p-4">
				<h2 class="h3">{type.displayLabel}</h2>
				<p class="flex-1 text-sm text-surface-600-400">{type.description}</p>
				<a class="btn self-start preset-tonal-surface btn-sm" href={`/content/${type.contentType}`}>
					Browse
				</a>
			</article>
		{:else}
			<p class="card p-4 text-surface-600-400">No content schemas are registered.</p>
		{/each}
	</div>
</div>
