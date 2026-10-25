<script lang="ts">
	import { Activity, LogOut, MapPin, User } from '@lucide/svelte';
	import type { GameSession } from './session.svelte';

	let { session, onLeave }: { session: GameSession; onLeave: () => void } = $props();

	const statusClass = $derived(
		session.phase === 'connected'
			? 'status-dot-online'
			: session.phase === 'reconnecting' || session.phase === 'connecting'
				? 'status-dot-reconnecting'
				: 'status-dot-offline'
	);
</script>

<header class="game-header flex flex-wrap items-center gap-3 px-4 py-2">
	<div class="flex min-w-0 items-center gap-2">
		<User class="text-secondary-500" size={18} aria-hidden="true" />
		<h1 class="text-title truncate text-base">
			{session.character?.name ?? 'Entering Veldrath…'}
			{#if session.character}
				<span class="text-surface-600-400">
					· {session.character.className} · Lv {session.stats.level}
				</span>
			{/if}
		</h1>
	</div>

	<div class="game-header-divider hidden sm:block" aria-hidden="true"></div>

	<p class="flex min-w-0 items-center gap-1 text-xs text-surface-600-400">
		<MapPin size={12} aria-hidden="true" />
		<span class="truncate">
			{session.zoneLabel}
			{#if session.location}
				· {session.location.locationDisplayName}
			{:else if session.currentNode}
				· {session.currentNode.displayName}
			{/if}
		</span>
	</p>

	<span class="inline-flex items-center gap-2 text-xs text-surface-600-400">
		<span class="status-dot {statusClass}"></span>
		<Activity size={12} aria-hidden="true" />
		{session.phase}
	</span>

	<button class="ml-auto btn preset-tonal-surface" type="button" onclick={onLeave}>
		<LogOut size={15} aria-hidden="true" />
		Characters
	</button>
</header>
