<script lang="ts">
	import { resolve } from '$app/paths';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>{data.article.title} — Veldrath</title>
	<meta name="description" content={data.article.summary} />
</svelte:head>

<article class="mx-auto max-w-3xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={resolve('/lore')}>← All lore</a>

	<header class="space-y-1">
		<p class="text-xs tracking-wide text-surface-600-400 uppercase">{data.article.category}</p>
		<h1 class="h1">{data.article.title}</h1>
		<p class="text-surface-600-400">{data.article.summary}</p>
		{#if data.article.publishedAt}
			<p class="text-xs text-surface-600-400">
				Published {new Date(data.article.publishedAt).toLocaleDateString()}
			</p>
		{/if}
	</header>

	<div class="markdown text-lore card p-6">
		<!-- eslint-disable-next-line svelte/no-at-html-tags -- sanitized by renderMarkdown() in the server load -->
		{@html data.html}
	</div>
</article>
