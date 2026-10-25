<script lang="ts">
	import { Coins, Gem, RefreshCw, ScrollText, Settings, Store, X } from '@lucide/svelte';
	import { Dialog } from 'bits-ui';
	import { rarityClass } from './types';
	import type { GameSession } from './session.svelte';

	let { session }: { session: GameSession } = $props();
</script>

<Dialog.Root open={session.showQuests} onOpenChange={(open) => (session.showQuests = open)}>
	<Dialog.Portal>
		<Dialog.Overlay class="game-dialog-backdrop" />
		<Dialog.Content class="game-dialog" aria-label="Quest log">
			<header class="game-dialog-header">
				<Dialog.Title class="game-panel-header">
					<ScrollText size={18} aria-hidden="true" />
					Quest log
				</Dialog.Title>
				<Dialog.Close
					class="btn preset-tonal-surface px-2 py-1 text-xs"
					aria-label="Close quest log"
				>
					<X size={14} aria-hidden="true" />
				</Dialog.Close>
			</header>
			<Dialog.Description class="text-xs text-surface-600-400">
				Active and completed quests tracked by the server.
			</Dialog.Description>

			{#if session.quests.length === 0}
				<p class="mt-3 text-sm text-surface-600-400">No quests yet.</p>
			{:else}
				<ul class="mt-3 space-y-1">
					{#each session.quests as quest (quest.slug)}
						<li
							class="flex items-center justify-between gap-2 rounded border border-surface-800 p-2 text-sm"
						>
							<span>{quest.title}</span>
							<span class="chip preset-tonal-surface text-xs">{quest.status}</span>
						</li>
					{/each}
				</ul>
			{/if}
		</Dialog.Content>
	</Dialog.Portal>
</Dialog.Root>

<Dialog.Root open={session.showShop} onOpenChange={(open) => (session.showShop = open)}>
	<Dialog.Portal>
		<Dialog.Overlay class="game-dialog-backdrop" />
		<Dialog.Content class="game-dialog" aria-label="Shop">
			<header class="game-dialog-header">
				<Dialog.Title class="game-panel-header">
					<Store size={18} aria-hidden="true" />
					Shop{session.shopZone ? ` · ${session.shopZone}` : ''}
				</Dialog.Title>
				<Dialog.Close class="btn preset-tonal-surface px-2 py-1 text-xs" aria-label="Close shop">
					<X size={14} aria-hidden="true" />
				</Dialog.Close>
			</header>
			<Dialog.Description class="text-xs text-surface-600-400">
				<Gem class="text-secondary-500" size={12} aria-hidden="true" />
				{session.stats.gold.toLocaleString()} gold available.
			</Dialog.Description>

			{#if session.shopCatalog.length === 0}
				<p class="mt-3 text-sm text-surface-600-400">No wares available.</p>
			{:else}
				<ul class="mt-3 space-y-1">
					{#each session.shopCatalog as item (item.slug)}
						<li
							class="flex items-center justify-between gap-2 rounded border border-surface-800 p-2 text-sm"
						>
							<span>
								<strong class={rarityClass(item.rarity)}>{item.name}</strong>
								<span class="block text-xs text-surface-600-400">{item.rarity ?? 'Item'}</span>
							</span>
							<span class="flex items-center gap-2">
								<span class="inline-flex items-center gap-1 text-xs">
									<Gem class="text-secondary-500" size={12} aria-hidden="true" />
									{item.price} g
								</span>
								<button
									class="btn preset-filled-primary-500 px-2 py-1 text-xs"
									type="button"
									onclick={() => session.buy(item.slug)}
									disabled={session.stats.gold < item.price}
								>
									<Coins size={13} aria-hidden="true" />
									Buy
								</button>
							</span>
						</li>
					{/each}
				</ul>
			{/if}
		</Dialog.Content>
	</Dialog.Portal>
</Dialog.Root>

<Dialog.Root open={session.showSettings} onOpenChange={(open) => (session.showSettings = open)}>
	<Dialog.Portal>
		<Dialog.Overlay class="game-dialog-backdrop" />
		<Dialog.Content class="game-dialog" aria-label="Settings">
			<header class="game-dialog-header">
				<Dialog.Title class="game-panel-header">
					<Settings size={18} aria-hidden="true" />
					Settings
				</Dialog.Title>
				<Dialog.Close
					class="btn preset-tonal-surface px-2 py-1 text-xs"
					aria-label="Close settings"
				>
					<X size={14} aria-hidden="true" />
				</Dialog.Close>
			</header>
			<Dialog.Description class="text-xs text-surface-600-400">
				Session diagnostics and manual refreshes.
			</Dialog.Description>

			<dl class="mt-3 space-y-2 text-sm">
				<div class="flex justify-between gap-3">
					<dt class="text-surface-600-400">Connection</dt>
					<dd>{session.phase}</dd>
				</div>
				{#if session.serverInfo}
					<div class="flex justify-between gap-3">
						<dt class="text-surface-600-400">Server</dt>
						<dd>{session.serverInfo.serverVersion}</dd>
					</div>
					<div class="flex justify-between gap-3">
						<dt class="text-surface-600-400">Connection ID</dt>
						<dd class="truncate">{session.serverInfo.connectionId}</dd>
					</div>
				{/if}
				{#if session.character}
					<div class="flex justify-between gap-3">
						<dt class="text-surface-600-400">Character ID</dt>
						<dd class="truncate">{session.character.id}</dd>
					</div>
				{/if}
			</dl>

			<div class="mt-4 flex flex-wrap gap-2">
				<button
					class="btn preset-tonal-surface"
					type="button"
					onclick={() => session.requestMapGraph(null)}
				>
					<RefreshCw size={15} aria-hidden="true" />
					Refresh map
				</button>
				<button
					class="btn preset-tonal-surface"
					type="button"
					onclick={() => session.openInventory()}
				>
					<RefreshCw size={15} aria-hidden="true" />
					Refresh inventory
				</button>
			</div>
		</Dialog.Content>
	</Dialog.Portal>
</Dialog.Root>
