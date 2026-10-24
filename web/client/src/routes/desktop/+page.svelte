<script lang="ts">
	import { onMount } from 'svelte';
	import GameScreen from '#lib/game/GameScreen.svelte';
	import { GameSession } from '#lib/game/session.svelte';
	import { loadDesktopCharacters } from '#lib/desktop/api';
	import type { CharacterSummary } from '#lib/game/types';

	let characters = $state<CharacterSummary[]>([]);
	let authenticated = $state(true);
	let loading = $state(true);
	let error = $state<string | null>(null);
	let session = $state<GameSession | null>(null);

	async function refresh() {
		loading = true;
		const result = await loadDesktopCharacters();
		authenticated = result.authenticated;
		characters = result.characters;
		error = result.error;
		loading = false;
	}

	onMount(() => {
		void refresh();
	});

	async function play(characterId: string) {
		// The desktop proxy forwards /hubs to the game server and injects the JWT,
		// so the hub needs no access-token factory here.
		const next = new GameSession({ hubUrl: '/hubs/game' });
		session = next;
		await next.connect(characterId);
	}

	async function leave() {
		const current = session;
		session = null;
		await current?.disconnect();
		await refresh();
	}
</script>

<svelte:head>
	<title>Veldrath</title>
</svelte:head>

{#if session}
	<GameScreen {session} onLeave={leave} />
{:else}
	<div class="mx-auto max-w-3xl space-y-4 p-6">
		<header class="space-y-1">
			<h1 class="h1">Veldrath</h1>
			<p class="text-surface-600-400">Signed in through the desktop client.</p>
		</header>

		{#if loading}
			<p class="text-sm text-surface-600-400">Loading characters…</p>
		{:else if !authenticated}
			<p class="card p-4 text-sm text-surface-600-400">
				Sign in through the Veldrath desktop client to load your characters.
			</p>
		{:else if error}
			<p class="text-sm text-error-500">{error}</p>
		{:else if characters.length === 0}
			<p class="card p-4 text-sm text-surface-600-400">
				No characters found. Create one in the desktop client first.
			</p>
		{:else}
			<div class="grid gap-3 sm:grid-cols-2">
				{#each characters as character (character.id)}
					<article class="space-y-2 card p-4">
						<div class="flex items-start justify-between gap-3">
							<div>
								<h2 class="h3">{character.name}</h2>
								<p class="text-sm text-surface-600-400">
									Level {character.level}
									{character.className}
								</p>
							</div>
							{#if character.isOnline}
								<span class="chip preset-tonal-surface text-xs text-success-500">Online</span>
							{/if}
						</div>
						<button class="btn preset-filled-primary-500" onclick={() => play(character.id)}>
							Enter Veldrath
						</button>
					</article>
				{/each}
			</div>
		{/if}
	</div>
{/if}
