<script lang="ts">
	import { Activity, MapPin, Users } from '@lucide/svelte';
	import type { GameSession } from './session.svelte';

	let { session }: { session: GameSession } = $props();

	const statusClass = $derived(
		session.phase === 'connected'
			? 'status-dot-online'
			: session.phase === 'reconnecting' || session.phase === 'connecting'
				? 'status-dot-reconnecting'
				: 'status-dot-offline'
	);
</script>

<footer
	class="game-footer flex flex-wrap items-center gap-4 px-4 py-2 text-xs text-surface-600-400"
>
	<span class="inline-flex items-center gap-2">
		<span class="status-dot {statusClass}"></span>
		<Activity size={12} aria-hidden="true" />
		{session.phase}
	</span>
	{#if session.serverInfo}
		<span>server {session.serverInfo.serverVersion}</span>
	{/if}
	<span class="inline-flex items-center gap-1">
		<MapPin size={12} aria-hidden="true" />
		{session.map?.name ?? 'No map'}
		{#if session.currentNode}
			· {session.currentNode.displayName}
		{/if}
	</span>
	<span class="inline-flex items-center gap-1">
		<Users size={12} aria-hidden="true" />
		{session.occupants.length} player(s) here
	</span>
	<span class="ml-auto text-surface-600-400">Veldrath · SvelteKit client</span>
</footer>
