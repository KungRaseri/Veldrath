<script lang="ts">
	import { resolve } from '$app/paths';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>Veldrath — a multiplayer role-playing world</title>
	<meta
		name="description"
		content="Veldrath is a persistent multiplayer RPG of warring regions, ancient lore, and dangerous frontiers."
	/>
</svelte:head>

<div class="mx-auto max-w-6xl space-y-8 p-6">
	<section class="space-y-4 py-10 text-center">
		<h1 class="text-4xl font-bold text-primary-500 sm:text-5xl">Veldrath</h1>
		<p class="mx-auto max-w-2xl text-lg text-surface-600-400">
			A persistent multiplayer role-playing world of warring regions, ancient lore, and dangerous
			frontiers.
		</p>
		<div class="flex flex-wrap justify-center gap-3">
			<a class="btn preset-filled-primary-500" href={resolve('/characters')}>Enter the world</a>
			<a class="btn preset-tonal-surface" href={resolve('/register')}>Create account</a>
			<a class="btn preset-tonal-surface" href={resolve('/lore')}>Explore the lore</a>
		</div>
	</section>

	{#if data.announcements.length > 0}
		<section class="space-y-2">
			<h2 class="h2">Announcements</h2>
			<div class="grid gap-3 sm:grid-cols-2">
				{#each data.announcements as announcement (announcement.id)}
					<article class="card border border-primary-500/30 p-4">
						<h3 class="h3">{announcement.title}</h3>
						<p class="text-sm text-surface-600-400">{announcement.body}</p>
					</article>
				{/each}
			</div>
		</section>
	{/if}

	<div class="grid gap-6 lg:grid-cols-2">
		<section class="space-y-3">
			<div class="flex items-center justify-between">
				<h2 class="h2">Latest lore</h2>
				<a class="text-sm text-primary-500" href={resolve('/lore')}>All lore</a>
			</div>
			{#if data.lore.length === 0}
				<p class="card p-4 text-sm text-surface-600-400">Lore articles will appear here.</p>
			{:else}
				<ul class="space-y-2">
					{#each data.lore as article (article.id)}
						<li class="card p-4">
							<a href={resolve('/lore/[slug]', { slug: article.slug })}>
								<p class="font-semibold">{article.title}</p>
								<p class="text-sm text-surface-600-400">{article.summary}</p>
							</a>
						</li>
					{/each}
				</ul>
			{/if}
		</section>

		<section class="space-y-3">
			<div class="flex items-center justify-between">
				<h2 class="h2">Latest patch notes</h2>
				<a class="text-sm text-primary-500" href={resolve('/patch-notes')}>All patch notes</a>
			</div>
			{#if data.patchNotes.length === 0}
				<p class="card p-4 text-sm text-surface-600-400">Patch notes will appear here.</p>
			{:else}
				<ul class="space-y-2">
					{#each data.patchNotes as note (note.id)}
						<li class="card p-4">
							<a href={resolve('/patch-notes/[slug]', { slug: note.slug })}>
								<p class="font-semibold">
									{note.title}
									<span class="ml-2 chip preset-tonal-surface text-xs">{note.version}</span>
								</p>
								<p class="text-sm text-surface-600-400">{note.summary}</p>
							</a>
						</li>
					{/each}
				</ul>
			{/if}
		</section>
	</div>
</div>
