<script lang="ts">
	import { resolve } from '$app/paths';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	function loreUrl(category: string | null): string {
		return category ? `/lore?category=${encodeURIComponent(category)}` : '/lore';
	}
</script>

<svelte:head>
	<title>Lore — Veldrath</title>
	<meta name="description" content="Histories, factions, and legends of Veldrath." />
</svelte:head>

<div class="mx-auto max-w-4xl space-y-6 p-6">
	<header class="space-y-2">
		<h1 class="h1">Lore</h1>
		<p class="text-surface-600-400">Histories, factions, and legends of the world of Veldrath.</p>
	</header>

	{#if data.categories.length > 0}
		<nav class="flex flex-wrap gap-2 text-sm">
			<a
				class="chip {data.category ? 'preset-tonal-surface' : 'preset-filled-primary-500'}"
				href={loreUrl(null)}
			>
				All
			</a>
			{#each data.categories as category (category)}
				<a
					class="chip {data.category === category
						? 'preset-filled-primary-500'
						: 'preset-tonal-surface'}"
					href={loreUrl(category)}
				>
					{category}
				</a>
			{/each}
		</nav>
	{/if}

	{#if data.error}
		<p class="card border border-error-500/40 p-3 text-sm text-error-500">{data.error}</p>
	{/if}

	{#if data.articles.length === 0}
		<div class="card p-6">
			<p class="text-sm text-surface-600-400">No lore articles have been published yet.</p>
		</div>
	{:else}
		<ul class="space-y-3">
			{#each data.articles as article (article.id)}
				<li class="card p-5">
					<a class="space-y-1" href={resolve('/lore/[slug]', { slug: article.slug })}>
						<h2 class="h3">{article.title}</h2>
						<p class="text-sm text-surface-600-400">{article.summary}</p>
						<p class="text-xs text-surface-600-400">
							{article.category}
							{#if article.publishedAt}
								· {new Date(article.publishedAt).toLocaleDateString()}
							{/if}
						</p>
					</a>
				</li>
			{/each}
		</ul>
	{/if}
</div>
