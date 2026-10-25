<script lang="ts">
	import {
		ArrowDownToLine,
		Backpack,
		Coins,
		FlaskConical,
		HeartPulse,
		RefreshCw,
		ScrollText,
		Settings,
		Sparkles,
		Store,
		Swords,
		Trash2,
		Undo2,
		Zap
	} from '@lucide/svelte';
	import ItemTooltip from './ItemTooltip.svelte';
	import { itemIcon, slotIcon, slotLabel } from './item-icons';
	import { barPercent } from './reducers';
	import {
		ATTRIBUTE_ORDER,
		EQUIPMENT_SLOTS,
		itemLabel,
		isEquippable,
		isUsable,
		rarityClass,
		type InventoryItem
	} from './types';
	import type { GameSession } from './session.svelte';

	let { session }: { session: GameSession } = $props();

	let pendingDiscard = $state<string | null>(null);

	/** Item currently hovered/focused, with the anchor rect used to place its tooltip. */
	let hovered = $state<{ item: InventoryItem; anchor: DOMRect } | null>(null);

	function showTooltip(item: InventoryItem, element: EventTarget | null) {
		if (!(element instanceof HTMLElement)) return;
		hovered = { item, anchor: element.getBoundingClientRect() };
	}

	function hideTooltip() {
		hovered = null;
	}

	function confirmDiscard(itemRef: string) {
		pendingDiscard = itemRef;
	}

	async function discard(itemRef: string) {
		pendingDiscard = null;
		await session.discard(itemRef);
	}

	function equippedFor(slot: string): InventoryItem | null {
		return session.equipped[slot] ?? null;
	}
</script>

<aside class="character-sheet" aria-label="Character sheet">
	<section class="game-panel space-y-3 p-3">
		<header class="space-y-1">
			<p class="text-title text-base font-semibold">{session.character?.name ?? 'Entering…'}</p>
			<p class="text-xs tracking-wide text-surface-600-400 uppercase">
				{#if session.character}
					{session.character.className} · Level {session.stats.level}
				{:else}
					Loading
				{/if}
			</p>
		</header>

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

		<div class="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs">
			<span class="inline-flex items-center gap-1">
				<Coins class="text-secondary-500" size={13} aria-hidden="true" />
				<span class="text-surface-600-400">Gold</span>
				<span class="text-mono">{session.stats.gold.toLocaleString()}</span>
			</span>
			<span class="inline-flex items-center gap-1">
				<Zap class="text-primary-400" size={13} aria-hidden="true" />
				<span class="text-surface-600-400">XP</span>
				<span class="text-mono">{session.stats.experience.toLocaleString()}</span>
			</span>
		</div>

		<button
			class="sheet-attributes"
			type="button"
			title="Open attributes"
			onclick={() => (session.showAttributes = true)}
		>
			{#each ATTRIBUTE_ORDER as attribute (attribute.key)}
				<span class="sheet-attribute">
					<span class="text-surface-600-400">{attribute.label.slice(0, 3)}</span>
					<span class="text-mono">{session.attributes[attribute.key]}</span>
				</span>
			{/each}
			{#if session.stats.unspentAttributePoints > 0}
				<span class="chip preset-filled-secondary-500 text-xs">
					+{session.stats.unspentAttributePoints}
				</span>
			{/if}
		</button>
	</section>

	<section class="game-panel space-y-2 p-3">
		<h2 class="game-panel-header">
			<Zap size={15} aria-hidden="true" />
			Equipment
		</h2>

		<ul class="equip-grid">
			{#each EQUIPMENT_SLOTS as slot (slot.id)}
				{@const item = equippedFor(slot.id)}
				{@const SlotIcon = slotIcon(slot.id)}
				<li
					class="equip-slot"
					class:equip-slot-empty={!item}
					onmouseenter={(event) => item && showTooltip(item, event.currentTarget)}
					onmouseleave={hideTooltip}
					onfocusin={(event) => item && showTooltip(item, event.currentTarget)}
					onfocusout={hideTooltip}
				>
					<span class="equip-slot-head">
						<SlotIcon size={13} aria-hidden="true" />
						<span class="text-xs tracking-wide text-surface-600-400 uppercase">
							{slotLabel(slot.id)}
						</span>
					</span>
					{#if item}
						<button
							class="equip-slot-name {rarityClass(item.rarity)}"
							type="button"
							title={`Inspect ${itemLabel(item)}`}
							onclick={() => session.inspectItem(item)}
						>
							{itemLabel(item)}
						</button>
						<button
							class="btn preset-tonal-surface px-1 py-0.5 text-xs"
							type="button"
							title={`Unequip ${itemLabel(item)}`}
							aria-label={`Unequip ${itemLabel(item)}`}
							onclick={() => session.unequip(slot.id)}
						>
							<Undo2 size={12} aria-hidden="true" />
							<span class="sr-only">Unequip</span>
						</button>
					{:else}
						<span class="text-xs text-surface-600-400">Empty</span>
					{/if}
				</li>
			{/each}
		</ul>
	</section>

	<section class="game-panel space-y-2 p-3">
		<div class="flex items-center justify-between gap-2">
			<h2 class="game-panel-header">
				<Backpack size={15} aria-hidden="true" />
				Inventory
				<span class="text-surface-600-400">({session.carriedItemCount})</span>
			</h2>
			<button
				class="btn preset-tonal-surface px-1.5 py-0.5 text-xs"
				type="button"
				title="Refresh inventory"
				aria-label="Refresh inventory"
				onclick={() => session.openInventory()}
			>
				<RefreshCw size={12} aria-hidden="true" />
			</button>
		</div>

		{#if session.inventory.length === 0}
			<p class="text-xs text-surface-600-400">Your bag is empty.</p>
		{:else}
			<ul class="bag-list">
				{#each session.bagItems as item (item.itemRef)}
					{@const ItemIcon = itemIcon(item)}
					<li
						class="bag-row"
						class:bag-row-equipped={item.isEquipped}
						onmouseenter={(event) => showTooltip(item, event.currentTarget)}
						onmouseleave={hideTooltip}
						onfocusin={(event) => showTooltip(item, event.currentTarget)}
						onfocusout={hideTooltip}
					>
						<div class="bag-row-head">
							<span class="bag-icon">
								<ItemIcon size={12} aria-hidden="true" />
							</span>
							<button
								class="bag-row-name {rarityClass(item.rarity)}"
								type="button"
								title={`Inspect ${itemLabel(item)}`}
								onclick={() => session.inspectItem(item)}
							>
								{itemLabel(item)}
							</button>
							{#if (item.quantity ?? 1) > 1}
								<span class="bag-row-qty">×{item.quantity}</span>
							{/if}
						</div>

						<p class="bag-row-status">
							{#if item.isEquipped}
								<span class="bag-row-badge">Equipped</span>
							{/if}
							<span>{item.rarity}</span>
							<span aria-hidden="true">·</span>
							<span>{item.type}</span>
							{#if item.slot}
								<span aria-hidden="true">·</span>
								<span>{slotLabel(item.slot)}</span>
							{/if}
							{#if item.durability != null}
								<span aria-hidden="true">·</span>
								<span>{item.durability}%</span>
							{/if}
						</p>

						<div class="bag-row-actions">
							{#if pendingDiscard === item.itemRef}
								<button
									class="btn preset-filled-error-500 px-1.5 py-0.5 text-xs"
									type="button"
									onclick={() => discard(item.itemRef)}
								>
									Discard
								</button>
								<button
									class="btn preset-tonal-surface px-1.5 py-0.5 text-xs"
									type="button"
									onclick={() => (pendingDiscard = null)}
								>
									Keep
								</button>
							{:else}
								{#if isUsable(item) && !session.combat.active}
									<button
										class="btn preset-tonal-surface px-1 py-0.5"
										type="button"
										title={`Use ${itemLabel(item)}`}
										aria-label={`Use ${itemLabel(item)}`}
										onclick={() => session.useItem(item.itemRef)}
									>
										<FlaskConical size={12} aria-hidden="true" />
									</button>
								{/if}

								{#if item.isEquipped}
									<button
										class="btn preset-tonal-surface px-1 py-0.5"
										type="button"
										title={`Unequip ${itemLabel(item)}`}
										aria-label={`Unequip ${itemLabel(item)}`}
										onclick={() => session.unequip(item.slot ?? '')}
									>
										<Undo2 size={12} aria-hidden="true" />
									</button>
								{:else if isEquippable(item) && session.character}
									<button
										class="btn preset-tonal-surface px-1 py-0.5"
										type="button"
										title={`Equip ${itemLabel(item)}`}
										aria-label={`Equip ${itemLabel(item)}`}
										onclick={() => session.equipItem(item.itemRef)}
									>
										<Swords size={12} aria-hidden="true" />
									</button>
								{/if}

								<button
									class="btn preset-tonal-surface px-1 py-0.5"
									type="button"
									title={`Drop ${itemLabel(item)} on the ground`}
									aria-label={`Drop ${itemLabel(item)}`}
									disabled={!session.location}
									onclick={() => session.dropToGround(item.itemRef)}
								>
									<ArrowDownToLine size={12} aria-hidden="true" />
								</button>

								{#if session.canShop}
									<button
										class="btn preset-tonal-surface px-1 py-0.5"
										type="button"
										title={`Sell ${itemLabel(item)}`}
										aria-label={`Sell ${itemLabel(item)}`}
										onclick={() => session.sell(item.itemRef)}
									>
										<Coins size={12} aria-hidden="true" />
									</button>
								{/if}

								<button
									class="btn preset-tonal-surface px-1 py-0.5"
									type="button"
									title={`Discard ${itemLabel(item)}`}
									aria-label={`Discard ${itemLabel(item)}`}
									onclick={() => confirmDiscard(item.itemRef)}
								>
									<Trash2 size={12} aria-hidden="true" />
								</button>
							{/if}
						</div>
					</li>
				{/each}
			</ul>
		{/if}
	</section>

	<nav class="game-panel sheet-menu p-2" aria-label="Character menu">
		<button class="sheet-menu-item" type="button" onclick={() => session.loadQuests()}>
			<ScrollText size={14} aria-hidden="true" />
			Quest log
		</button>
		<button class="sheet-menu-item" type="button" onclick={() => (session.showAbilities = true)}>
			<Sparkles size={14} aria-hidden="true" />
			Abilities
		</button>
		{#if session.canShop}
			<button class="sheet-menu-item" type="button" onclick={() => session.visitShop()}>
				<Store size={14} aria-hidden="true" />
				Merchant
			</button>
		{/if}
		<button class="sheet-menu-item" type="button" onclick={() => (session.showSettings = true)}>
			<Settings size={14} aria-hidden="true" />
			Settings
		</button>
	</nav>
</aside>

{#if hovered}
	<ItemTooltip
		item={hovered.item}
		anchor={hovered.anchor}
		level={session.stats.level}
		attributes={session.attributes}
	/>
{/if}
