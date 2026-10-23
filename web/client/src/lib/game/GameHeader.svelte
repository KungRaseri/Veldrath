<script lang="ts">
	import { barPercent } from './reducers';
	import type { GameSession } from './session.svelte';

	let { session, onLeave }: { session: GameSession; onLeave: () => void } = $props();
</script>

<header class="flex flex-wrap items-center gap-4 card p-4">
	<div class="min-w-48 space-y-1">
		<h1 class="h2">
			{#if session.character}
				{session.character.name}
				<span class="text-surface-600-400">
					· Lv {session.stats.level}
					{session.character.className}
				</span>
			{:else}
				Entering Veldrath…
			{/if}
		</h1>
		<p class="text-sm text-surface-600-400">
			{session.zoneLabel}
			{#if session.location}
				· {session.location.name}
			{/if}
		</p>
	</div>

	<div class="grid min-w-56 flex-1 gap-2 sm:grid-cols-2">
		<div class="space-y-1">
			<div class="flex justify-between text-xs text-surface-600-400">
				<span>Health</span>
				<span>{session.stats.currentHealth} / {session.stats.maxHealth}</span>
			</div>
			<div class="h-2 overflow-hidden rounded bg-surface-200-800">
				<div
					class="h-full bg-error-500 transition-all"
					style:width={`${barPercent(session.stats.currentHealth, session.stats.maxHealth)}%`}
				></div>
			</div>
		</div>

		<div class="space-y-1">
			<div class="flex justify-between text-xs text-surface-600-400">
				<span>Mana</span>
				<span>{session.stats.currentMana} / {session.stats.maxMana}</span>
			</div>
			<div class="h-2 overflow-hidden rounded bg-surface-200-800">
				<div
					class="h-full bg-primary-500 transition-all"
					style:width={`${barPercent(session.stats.currentMana, session.stats.maxMana)}%`}
				></div>
			</div>
		</div>
	</div>

	<div class="space-y-1 text-sm">
		<p><span class="text-surface-600-400">Gold</span> {session.stats.gold.toLocaleString()}</p>
		<p><span class="text-surface-600-400">XP</span> {session.stats.experience.toLocaleString()}</p>
	</div>

	<div class="flex flex-wrap gap-2">
		<button class="btn preset-filled-primary-500" onclick={() => session.openInventory()}>
			Inventory
		</button>
		<button class="btn preset-tonal-surface" onclick={() => session.loadQuests()}>Quests</button>
		{#if session.canShop}
			<button class="btn preset-tonal-surface" onclick={() => session.visitShop()}>Shop</button>
		{/if}
		<button class="btn preset-tonal-surface" onclick={() => (session.showSettings = true)}>
			Settings
		</button>
		<button class="btn preset-tonal-surface" onclick={onLeave}>Characters</button>
	</div>
</header>
