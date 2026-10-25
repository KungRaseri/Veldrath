<script lang="ts">
	import {
		Backpack,
		Gem,
		HeartPulse,
		LogOut,
		ScrollText,
		Settings,
		Sparkles,
		Store,
		User
	} from '@lucide/svelte';
	import { barPercent } from './reducers';
	import type { GameSession } from './session.svelte';

	let { session, onLeave }: { session: GameSession; onLeave: () => void } = $props();
</script>

<header class="game-header flex flex-wrap items-center gap-4 p-3">
	<div class="min-w-48 space-y-1">
		<h1 class="text-title flex items-center gap-2">
			<User class="text-secondary-500" size={20} aria-hidden="true" />
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
				· {session.location.locationDisplayName}
			{/if}
		</p>
	</div>

	<div class="game-header-divider hidden sm:block" aria-hidden="true"></div>

	<div class="grid min-w-56 flex-1 gap-2 sm:grid-cols-2">
		<div class="space-y-1">
			<div class="flex items-center justify-between text-xs text-surface-600-400">
				<span class="inline-flex items-center gap-1">
					<HeartPulse size={12} aria-hidden="true" />
					Health
				</span>
				<span>{session.stats.currentHealth} / {session.stats.maxHealth}</span>
			</div>
			<div class="h-2 overflow-hidden rounded bg-surface-800">
				<div
					class="h-full bg-error-500 transition-all"
					style:width={`${barPercent(session.stats.currentHealth, session.stats.maxHealth)}%`}
				></div>
			</div>
		</div>

		<div class="space-y-1">
			<div class="flex items-center justify-between text-xs text-surface-600-400">
				<span class="inline-flex items-center gap-1">
					<Sparkles size={12} aria-hidden="true" />
					Mana
				</span>
				<span>{session.stats.currentMana} / {session.stats.maxMana}</span>
			</div>
			<div class="h-2 overflow-hidden rounded bg-surface-800">
				<div
					class="h-full bg-primary-500 transition-all"
					style:width={`${barPercent(session.stats.currentMana, session.stats.maxMana)}%`}
				></div>
			</div>
		</div>
	</div>

	<div class="space-y-1 text-sm">
		<p class="inline-flex items-center gap-1">
			<Gem class="text-secondary-500" size={14} aria-hidden="true" />
			<span class="text-surface-600-400">Gold</span>
			{session.stats.gold.toLocaleString()}
		</p>
		<p class="inline-flex items-center gap-1">
			<Sparkles class="text-primary-400" size={14} aria-hidden="true" />
			<span class="text-surface-600-400">XP</span>
			{session.stats.experience.toLocaleString()}
		</p>
	</div>

	<div class="ml-auto flex flex-wrap gap-2">
		<button
			class="btn preset-filled-primary-500"
			type="button"
			onclick={() => session.openInventory()}
		>
			<Backpack size={15} aria-hidden="true" />
			Inventory
		</button>
		<button class="btn preset-tonal-surface" type="button" onclick={() => session.loadQuests()}>
			<ScrollText size={15} aria-hidden="true" />
			Quests
		</button>
		{#if session.canShop}
			<button class="btn preset-tonal-surface" type="button" onclick={() => session.visitShop()}>
				<Store size={15} aria-hidden="true" />
				Shop
			</button>
		{/if}
		<button
			class="btn preset-tonal-surface"
			type="button"
			onclick={() => (session.showSettings = true)}
		>
			<Settings size={15} aria-hidden="true" />
			Settings
		</button>
		<button class="btn preset-tonal-surface" type="button" onclick={onLeave}>
			<LogOut size={15} aria-hidden="true" />
			Characters
		</button>
	</div>
</header>
