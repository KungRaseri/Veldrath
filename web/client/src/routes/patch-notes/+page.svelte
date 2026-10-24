<script lang="ts">
	import { resolve } from '$app/paths';
	import { ScrollText } from '@lucide/svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>Patch notes — Veldrath</title>
	<meta name="description" content="Release notes and updates for Veldrath." />
</svelte:head>

<div class="mx-auto max-w-4xl space-y-6 p-6">
	<header class="space-y-2">
		<h1 class="text-title-large flex items-center gap-3">
			<ScrollText class="text-secondary-500" size={28} aria-hidden="true" />
			Patch notes
		</h1>
		<p class="text-surface-600-400">Release notes and updates for Veldrath.</p>
	</header>

	{#if data.error}
		<p class="card border border-error-500/40 p-3 text-sm text-error-500">{data.error}</p>
	{/if}

	{#if data.notes.length === 0}
		<div class="card p-6">
			<p class="text-sm text-surface-600-400">No patch notes have been published yet.</p>
		</div>
	{:else}
		<ul class="space-y-3">
			{#each data.notes as note (note.id)}
				<li class="card p-5">
					<a class="space-y-1" href={resolve('/patch-notes/[slug]', { slug: note.slug })}>
						<div class="flex flex-wrap items-center gap-2">
							<h2 class="h3">{note.title}</h2>
							<span class="chip preset-tonal-surface text-xs">{note.version}</span>
						</div>
						<p class="text-sm text-surface-600-400">{note.summary}</p>
						{#if note.publishedAt}
							<p class="text-xs text-surface-600-400">
								{new Date(note.publishedAt).toLocaleDateString()}
							</p>
						{/if}
					</a>
				</li>
			{/each}
		</ul>
	{/if}
</div>
