<script lang="ts">
	import { resolve } from '$app/paths';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>{data.note.title} — Veldrath</title>
	<meta name="description" content={data.note.summary} />
</svelte:head>

<article class="mx-auto max-w-3xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={resolve('/patch-notes')}>← All patch notes</a>

	<header class="space-y-1">
		<div class="flex flex-wrap items-center gap-2">
			<h1 class="h1">{data.note.title}</h1>
			<span class="chip preset-tonal-surface text-xs">{data.note.version}</span>
		</div>
		<p class="text-surface-600-400">{data.note.summary}</p>
		{#if data.note.publishedAt}
			<p class="text-xs text-surface-600-400">
				Published {new Date(data.note.publishedAt).toLocaleDateString()}
			</p>
		{/if}
	</header>

	<div class="markdown card p-6">
		<!-- eslint-disable-next-line svelte/no-at-html-tags -- sanitized by renderMarkdown() in the server load -->
		{@html data.html}
	</div>
</article>
