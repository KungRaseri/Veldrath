<script lang="ts">
	import { onMount } from 'svelte';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import GameScreen from '#lib/game/GameScreen.svelte';
	import { GameSession } from '#lib/game/session.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	const session = new GameSession();

	onMount(() => {
		void session.connect(data.characterId);
		return () => {
			void session.disconnect();
		};
	});

	async function returnToCharacters() {
		await session.disconnect();
		await goto(resolve('/characters'));
	}
</script>

<GameScreen {session} onLeave={returnToCharacters} />
