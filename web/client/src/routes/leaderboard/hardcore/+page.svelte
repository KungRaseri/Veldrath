<script lang="ts">
	import { resolve } from '$app/paths';
	import { Skull, Trophy } from '@lucide/svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	const page = $derived(data.board?.page ?? 1);
	const hasNext = $derived(
		data.board ? data.board.page * data.board.pageSize < data.board.total : false
	);
</script>

<svelte:head>
	<title>Hardcore board — Veldrath</title>
</svelte:head>

<div class="mx-auto max-w-3xl space-y-6 p-6">
	<header class="space-y-1">
		<h1 class="text-title flex items-center gap-2 text-2xl font-semibold">
			<Trophy size={22} aria-hidden="true" />
			Hardcore board
		</h1>
		<p class="text-sm text-surface-600-400">
			One life. Fallen characters stay on the board with the date they fell.
		</p>
	</header>

	{#if data.loadError}
		<p class="text-sm text-error-500">{data.loadError}</p>
	{:else if !data.board || data.board.items.length === 0}
		<p class="text-sm text-surface-600-400">
			No hardcore characters yet. <a class="underline" href={resolve('/characters')}>Roll one?</a>
		</p>
	{:else}
		<ol class="space-y-2">
			{#each data.board.items as entry, index (entry.characterId)}
				<li class="game-panel flex items-center justify-between gap-3 p-3 text-sm">
					<span class="flex items-center gap-3">
						<span class="text-mono w-8 text-surface-600-400">
							{index + 1 + (page - 1) * data.board.pageSize}
						</span>
						<span>
							<span class="flex items-center gap-2 font-semibold">
								{entry.name}
								{#if entry.isOwn}
									<span class="chip preset-tonal-surface text-xs">You</span>
								{/if}
								{#if entry.diedAt}
									<Skull class="text-error-500" size={13} aria-label="Fallen" role="img" />
								{/if}
							</span>
							<span class="block text-xs text-surface-600-400">
								{entry.className} · Level {entry.level} · {entry.experience.toLocaleString()} XP
							</span>
						</span>
					</span>
					{#if entry.diedAt}
						<span class="text-xs text-surface-600-400">
							Fell {new Date(entry.diedAt).toLocaleDateString()}
						</span>
					{/if}
				</li>
			{/each}
		</ol>

		<div class="flex items-center justify-between gap-3">
			{#if page > 1}
				<a
					class="btn preset-tonal-surface"
					href={`${resolve('/leaderboard/hardcore')}?page=${page - 1}`}
				>
					Previous
				</a>
			{:else}
				<span></span>
			{/if}
			{#if hasNext}
				<a
					class="btn preset-tonal-surface"
					href={`${resolve('/leaderboard/hardcore')}?page=${page + 1}`}
				>
					Next
				</a>
			{/if}
		</div>
	{/if}
</div>
