<script lang="ts">
	import {
		Backpack,
		Coins,
		Gem,
		RefreshCw,
		ScrollText,
		Settings,
		Store,
		Trash2,
		Undo2,
		X
	} from '@lucide/svelte';
	import { itemLabel, itemSubtitle, type InventoryItem } from './types';
	import type { GameSession } from './session.svelte';

	let { session }: { session: GameSession } = $props();

	function itemRef(item: InventoryItem): string {
		return item.itemRef ?? item.slug ?? '';
	}

	function rarityClass(rarity: string | undefined): string {
		return rarity ? `rarity-${rarity.toLowerCase()}` : 'text-surface-600-400';
	}
</script>

{#if session.showInventory}
	<section
		class="fixed inset-y-0 right-0 z-50 w-full max-w-md overflow-y-auto border-l border-surface-800 bg-surface-900 p-5 shadow-2xl"
		aria-label="Inventory"
	>
		<div class="mb-4 flex items-center justify-between">
			<h2 class="game-panel-header">
				<Backpack size={18} aria-hidden="true" />
				Inventory
			</h2>
			<button
				class="btn preset-tonal-surface"
				type="button"
				onclick={() => (session.showInventory = false)}
			>
				<X size={15} aria-hidden="true" />
				Close
			</button>
		</div>

		{#if session.equippedEntries.length > 0}
			<div class="mb-4 space-y-2">
				<h3 class="text-xs tracking-wide text-surface-600-400 uppercase">Equipped</h3>
				<ul class="space-y-1">
					{#each session.equippedEntries as [slot, item] (slot)}
						<li
							class="flex items-center justify-between gap-2 rounded border border-surface-800 p-2 text-sm"
						>
							<span>
								<strong>{slot}</strong>
								· {itemLabel(item)}
							</span>
							<button
								class="btn preset-tonal-surface px-2 py-1 text-xs"
								type="button"
								onclick={() => session.equip(slot, null)}
							>
								<Undo2 size={13} aria-hidden="true" />
								Unequip
							</button>
						</li>
					{/each}
				</ul>
			</div>
		{/if}

		<div class="space-y-2">
			<h3 class="text-xs tracking-wide text-surface-600-400 uppercase">Bag</h3>
			{#if session.inventory.length === 0}
				<p class="text-sm text-surface-600-400">Your bag is empty.</p>
			{:else}
				<ul class="space-y-1">
					{#each session.inventory as item, index (item.id ?? `${itemLabel(item)}-${index}`)}
						<li class="space-y-1 rounded border border-surface-800 p-2 text-sm">
							<div class="flex items-center justify-between gap-2">
								<span>
									<strong class={rarityClass(item.rarity)}>{itemLabel(item)}</strong>
									{#if (item.quantity ?? 1) > 1}
										<span class="text-surface-600-400">× {item.quantity}</span>
									{/if}
								</span>
								<div class="flex gap-1">
									{#if session.canShop}
										<button
											class="btn preset-tonal-surface px-2 py-1 text-xs"
											type="button"
											onclick={() => session.sell(itemRef(item))}
										>
											<Coins size={13} aria-hidden="true" />
											Sell
										</button>
									{/if}
									<button
										class="btn preset-tonal-surface px-2 py-1 text-xs"
										type="button"
										onclick={() => session.drop(itemRef(item))}
									>
										<Trash2 size={13} aria-hidden="true" />
										Drop
									</button>
								</div>
							</div>
							<span class="block text-xs text-surface-600-400">{itemSubtitle(item)}</span>
						</li>
					{/each}
				</ul>
			{/if}
		</div>
	</section>
{/if}

{#if session.showShop}
	<section
		class="fixed inset-y-0 right-0 z-50 w-full max-w-md overflow-y-auto border-l border-surface-800 bg-surface-900 p-5 shadow-2xl"
		aria-label="Shop"
	>
		<div class="mb-4 flex items-center justify-between">
			<h2 class="game-panel-header">
				<Store size={18} aria-hidden="true" />
				Shop{session.shopZone ? ` · ${session.shopZone}` : ''}
			</h2>
			<button
				class="btn preset-tonal-surface"
				type="button"
				onclick={() => (session.showShop = false)}
			>
				<X size={15} aria-hidden="true" />
				Close
			</button>
		</div>

		{#if session.shopCatalog.length === 0}
			<p class="text-sm text-surface-600-400">No wares available.</p>
		{:else}
			<ul class="space-y-1">
				{#each session.shopCatalog as item (item.slug)}
					<li
						class="flex items-center justify-between gap-2 rounded border border-surface-800 p-2 text-sm"
					>
						<span>
							<strong>{item.name}</strong>
							<span class="block text-xs {rarityClass(item.rarity)}">{item.rarity ?? 'Item'}</span>
						</span>
						<span class="flex items-center gap-2">
							<span class="inline-flex items-center gap-1">
								<Gem class="text-secondary-500" size={13} aria-hidden="true" />
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
	</section>
{/if}

{#if session.showQuests}
	<section
		class="fixed inset-y-0 right-0 z-50 w-full max-w-md overflow-y-auto border-l border-surface-800 bg-surface-900 p-5 shadow-2xl"
		aria-label="Quest log"
	>
		<div class="mb-4 flex items-center justify-between">
			<h2 class="game-panel-header">
				<ScrollText size={18} aria-hidden="true" />
				Quest log
			</h2>
			<button
				class="btn preset-tonal-surface"
				type="button"
				onclick={() => (session.showQuests = false)}
			>
				<X size={15} aria-hidden="true" />
				Close
			</button>
		</div>

		{#if session.quests.length === 0}
			<p class="text-sm text-surface-600-400">No quests yet.</p>
		{:else}
			<ul class="space-y-1">
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
	</section>
{/if}

{#if session.showSettings}
	<section
		class="fixed inset-y-0 right-0 z-50 w-full max-w-md overflow-y-auto border-l border-surface-800 bg-surface-900 p-5 shadow-2xl"
		aria-label="Settings"
	>
		<div class="mb-4 flex items-center justify-between">
			<h2 class="game-panel-header">
				<Settings size={18} aria-hidden="true" />
				Settings
			</h2>
			<button
				class="btn preset-tonal-surface"
				type="button"
				onclick={() => (session.showSettings = false)}
			>
				<X size={15} aria-hidden="true" />
				Close
			</button>
		</div>

		<dl class="space-y-2 text-sm">
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

		<div class="mt-4 grid gap-2">
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
				<Backpack size={15} aria-hidden="true" />
				Refresh inventory
			</button>
		</div>
	</section>
{/if}
