<script lang="ts">
	import { resolve } from '$app/paths';
	import Pager from '#lib/components/Pager.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>Languages — Foundry</title>
</svelte:head>

<div class="mx-auto max-w-5xl space-y-4 p-6">
	<header class="flex flex-wrap items-end justify-between gap-3">
		<div>
			<h1 class="h1">Languages</h1>
			<p class="text-surface-600-400">
				Constructed and natural languages of Veldrath — phonology, morphology, roots, and registers.
			</p>
		</div>
		<a class="btn preset-filled-primary-500" href={resolve('/(foundry)/languages/new')}
			>Build a language</a
		>
	</header>

	{#if data.error}
		<p class="text-sm text-error-500">{data.error}</p>
	{/if}

	<form class="flex flex-wrap items-end gap-3 card p-4" method="GET">
		<label class="label flex-1">
			<span>Search</span>
			<input class="input" type="search" name="search" value={data.search} />
		</label>
		<button class="btn preset-tonal-surface" type="submit">Search</button>
	</form>

	<div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
		{#each data.languages as language (language.id)}
			<a class="space-y-1 card p-4" href={`/content/language/${language.slug}`}>
				<h2 class="h3">{language.displayName ?? language.slug}</h2>
				<p class="text-xs tracking-wide text-surface-600-400 uppercase">{language.typeKey}</p>
				<p class="text-sm text-surface-600-400">{language.slug}</p>
			</a>
		{:else}
			<p class="card p-4 text-surface-600-400">
				No languages have been submitted yet. Be the first to build one.
			</p>
		{/each}
	</div>

	<Pager
		page={data.page}
		pageSize={data.pageSize}
		total={data.total}
		basePath="/languages"
		query={{ search: data.search }}
	/>
</div>
