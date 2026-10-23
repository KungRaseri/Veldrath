<script lang="ts">
	import type { GameSession } from './session.svelte';

	let { session }: { session: GameSession } = $props();

	const phaseClass = $derived(
		session.phase === 'connected'
			? 'bg-success-500'
			: session.phase === 'reconnecting' || session.phase === 'connecting'
				? 'bg-warning-500'
				: 'bg-error-500'
	);
</script>

<footer class="flex flex-wrap items-center gap-4 card px-4 py-2 text-xs text-surface-600-400">
	<span class="inline-flex items-center gap-2">
		<span class="h-2 w-2 rounded-full {phaseClass}"></span>
		{session.phase}
	</span>
	{#if session.serverInfo}
		<span>server {session.serverInfo.serverVersion}</span>
	{/if}
	<span>
		{session.map?.name ?? 'No map'}
		{#if session.currentNode}
			· {session.currentNode.displayName}
		{/if}
	</span>
	<span>{session.occupants.length} player(s) here</span>
	<span class="ml-auto">Veldrath · SvelteKit client</span>
</footer>
