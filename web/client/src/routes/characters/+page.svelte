<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	async function enterWorld(characterId: string) {
		await goto(resolve('/play/[id]', { id: characterId }));
	}
</script>

<svelte:head>
	<title>Characters — Veldrath</title>
</svelte:head>

<div class="mx-auto max-w-3xl space-y-6 p-6">
	<header class="space-y-1">
		<h1 class="h2">Choose your character</h1>
		<p class="text-sm text-surface-600-400">Select an existing character to enter Veldrath.</p>
	</header>

	{#if data.loadError}
		<div class="card border border-error-500/40 p-4 text-sm text-error-500">
			{data.loadError}
		</div>
	{/if}

	{#if data.characters.length === 0}
		<div class="space-y-3 card p-6">
			<h2 class="h3">No characters yet</h2>
			<p class="text-sm text-surface-600-400">
				Character creation moves to SvelteKit in a later phase. Use the current client to create a
				character, then return here.
			</p>
			<a class="btn w-fit preset-tonal-surface" href={resolve('/')}>Back to the home page</a>
		</div>
	{:else}
		<ul class="space-y-3">
			{#each data.characters as character (character.id)}
				<li class="flex flex-wrap items-center justify-between gap-4 card p-5">
					<div class="space-y-1">
						<div class="flex items-center gap-2">
							<h2 class="h3">{character.name}</h2>
							{#if character.isOnline}
								<span class="chip preset-filled-success-500 text-xs">Online</span>
							{/if}
							{#if character.isHardcore}
								<span class="chip preset-filled-error-500 text-xs">Hardcore</span>
							{/if}
						</div>
						<p class="text-sm text-surface-600-400">
							Level {character.level}
							{character.className} · Slot {character.slotIndex + 1}
						</p>
						<p class="text-sm text-surface-600-400">
							{character.currentZoneId || 'Region map'} · {character.experience.toLocaleString()}
							XP
						</p>
					</div>

					<button
						class="btn preset-filled-primary-500"
						onclick={() => enterWorld(character.id)}
						disabled={character.isOnline}
					>
						{character.isOnline ? 'Already online' : 'Enter world'}
					</button>
				</li>
			{/each}
		</ul>
	{/if}
</div>
