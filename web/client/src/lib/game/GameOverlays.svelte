<script lang="ts">
	import {
		Coins,
		FlaskConical,
		Gem,
		Minus,
		Plus,
		RefreshCw,
		ScrollText,
		Settings,
		Sparkles,
		Store,
		Swords,
		Undo2,
		X
	} from '@lucide/svelte';
	import { Dialog } from 'bits-ui';
	import { itemIcon, slotLabel } from './item-icons';
	import {
		ATTRIBUTE_ORDER,
		armorLabel,
		damageLabel,
		effectLabel,
		isEquippable,
		isUsable,
		itemLabel,
		rarityClass,
		type CharacterAttributes,
		type InventoryItem
	} from './types';
	import type { GameSession } from './session.svelte';

	let { session }: { session: GameSession } = $props();

	let pendingPoints = $state<Record<string, number>>({});

	const spentPoints = $derived(Object.values(pendingPoints).reduce((sum, value) => sum + value, 0));
	const remainingPoints = $derived(session.stats.unspentAttributePoints - spentPoints);

	const ownedItem = $derived.by((): InventoryItem | null => {
		const inspected = session.inspectedItem;
		if (!inspected) return null;
		return session.bagItems.find((item) => item.itemRef === inspected.itemRef) ?? inspected;
	});

	const damagedLabel = $derived(ownedItem ? damageLabel(ownedItem) : null);
	const armourLabel = $derived(ownedItem ? armorLabel(ownedItem) : null);
	const requiresLabel = $derived.by(() => {
		if (!ownedItem) return [];
		const parts: string[] = [];
		if (ownedItem.requirementsLevel > 1) parts.push(`Level ${ownedItem.requirementsLevel}`);
		for (const [attribute, value] of Object.entries(ownedItem.requirements ?? {})) {
			parts.push(`${attribute} ${value}`);
		}
		return parts;
	});
	const bonusLabel = $derived(
		Object.entries(ownedItem?.bonuses ?? {}).map(([attribute, value]) => `${attribute} +${value}`)
	);

	function addPoint(key: keyof CharacterAttributes) {
		if (remainingPoints <= 0) return;
		pendingPoints = { ...pendingPoints, [key]: (pendingPoints[key] ?? 0) + 1 };
	}

	function removePoint(key: keyof CharacterAttributes) {
		const current = pendingPoints[key] ?? 0;
		if (current <= 0) return;
		const next = { ...pendingPoints };
		if (current === 1) delete next[key];
		else next[key] = current - 1;
		pendingPoints = next;
	}

	async function confirmAllocation() {
		const allocations: Record<string, number> = {};
		for (const [key, value] of Object.entries(pendingPoints)) {
			if (value > 0) allocations[key.charAt(0).toUpperCase() + key.slice(1)] = value;
		}
		if (Object.keys(allocations).length === 0) return;
		pendingPoints = {};
		session.showAttributes = false;
		await session.allocateAttributePoints(allocations);
	}

	function closeAttributes() {
		pendingPoints = {};
		session.showAttributes = false;
	}
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
				Turn a completed quest in for its reward, or take on something new.
			</Dialog.Description>

			{#if session.quests.length === 0}
				<p class="mt-3 text-sm text-surface-600-400">No quests yet.</p>
			{:else}
				<ul class="mt-3 space-y-2">
					{#each session.quests as quest (quest.slug)}
						<li class="space-y-1 rounded border border-surface-800 p-2 text-sm">
							<div class="flex items-center justify-between gap-2">
								<span class="font-semibold">{quest.title}</span>
								<span class="chip preset-tonal-surface text-xs">{quest.status}</span>
							</div>
							{#if quest.description}
								<p class="text-xs text-surface-600-400">{quest.description}</p>
							{/if}
							<ul class="space-y-0.5">
								{#each quest.objectives as objective (objective.key)}
									<li class="flex items-center justify-between gap-2 text-xs">
										<span class={objective.complete ? 'text-success-500' : 'text-surface-600-400'}>
											{objective.key.replaceAll(':', ' · ').replaceAll('-', ' ')}
										</span>
										<span class="text-mono">{objective.current}/{objective.required}</span>
									</li>
								{/each}
							</ul>
							<div class="flex flex-wrap items-center justify-between gap-2 pt-1">
								<span class="text-xs text-surface-600-400">
									Reward: {quest.goldReward} g · {quest.xpReward} XP
								</span>
								<span class="flex gap-1">
									{#if quest.status === 'Active'}
										<button
											class="btn preset-tonal-surface px-2 py-1 text-xs"
											type="button"
											onclick={() => session.abandonQuest(quest.slug)}
										>
											Abandon
										</button>
										<button
											class="btn preset-filled-primary-500 px-2 py-1 text-xs"
											type="button"
											disabled={!quest.canTurnIn}
											onclick={() => session.turnInQuest(quest.slug)}
										>
											Turn in
										</button>
									{/if}
								</span>
							</div>
						</li>
					{/each}
				</ul>
			{/if}

			{#if session.availableQuests.length > 0}
				<div class="mt-4 space-y-2 border-t border-surface-800 pt-3">
					<h3 class="text-xs tracking-wide text-surface-600-400 uppercase">Available</h3>
					<ul class="space-y-1">
						{#each session.availableQuests as quest (quest.slug)}
							<li
								class="flex items-center justify-between gap-2 rounded border border-surface-800 p-2 text-sm"
							>
								<span class="min-w-0">
									<span class="block truncate font-semibold">{quest.title}</span>
									<span class="block text-xs text-surface-600-400">
										{quest.goldReward} g · {quest.xpReward} XP
									</span>
								</span>
								<button
									class="btn preset-tonal-surface px-2 py-1 text-xs"
									type="button"
									onclick={() => session.acceptQuest(quest.slug)}
								>
									Accept
								</button>
							</li>
						{/each}
					</ul>
				</div>
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

<Dialog.Root open={session.showAbilities} onOpenChange={(open) => (session.showAbilities = open)}>
	<Dialog.Portal>
		<Dialog.Overlay class="game-dialog-backdrop" />
		<Dialog.Content class="game-dialog" aria-label="Abilities">
			<header class="game-dialog-header">
				<Dialog.Title class="game-panel-header">
					<Sparkles size={18} aria-hidden="true" />
					Abilities
				</Dialog.Title>
				<Dialog.Close
					class="btn preset-tonal-surface px-2 py-1 text-xs"
					aria-label="Close abilities"
				>
					<X size={14} aria-hidden="true" />
				</Dialog.Close>
			</header>
			<Dialog.Description class="text-xs text-surface-600-400">
				Learned abilities. Using one outside combat spends mana.
			</Dialog.Description>

			{#if (session.character?.learnedAbilities ?? []).length === 0}
				<p class="mt-3 text-sm text-surface-600-400">No abilities learned yet.</p>
			{:else}
				<ul class="mt-3 space-y-1">
					{#each session.character?.learnedAbilities ?? [] as ability (ability)}
						<li
							class="flex items-center justify-between gap-2 rounded border border-surface-800 p-2 text-sm"
						>
							<span class="capitalize">{ability.replaceAll('_', ' ')}</span>
							<button
								class="btn preset-tonal-surface px-2 py-1 text-xs"
								type="button"
								disabled={session.combat.active || session.dead}
								onclick={() => session.useAbility(ability)}
							>
								<Sparkles size={12} aria-hidden="true" />
								Use
							</button>
						</li>
					{/each}
				</ul>
			{/if}
		</Dialog.Content>
	</Dialog.Portal>
</Dialog.Root>

<Dialog.Root open={session.showAttributes} onOpenChange={(open) => (session.showAttributes = open)}>
	<Dialog.Portal>
		<Dialog.Overlay class="game-dialog-backdrop" />
		<Dialog.Content class="game-dialog" aria-label="Attributes">
			<header class="game-dialog-header">
				<Dialog.Title class="game-panel-header">
					<Plus size={18} aria-hidden="true" />
					Attributes
				</Dialog.Title>
				<button
					class="btn preset-tonal-surface px-2 py-1 text-xs"
					type="button"
					aria-label="Close attributes"
					onclick={closeAttributes}
				>
					<X size={14} aria-hidden="true" />
				</button>
			</header>
			<Dialog.Description class="text-xs text-surface-600-400">
				{remainingPoints} of {session.stats.unspentAttributePoints} unspent points left.
			</Dialog.Description>

			<ul class="mt-3 space-y-1">
				{#each ATTRIBUTE_ORDER as attribute (attribute.key)}
					<li
						class="flex items-center justify-between gap-2 rounded border border-surface-800 p-2 text-sm"
					>
						<span>{attribute.label}</span>
						<span class="flex items-center gap-2">
							<span class="text-mono">{session.attributes[attribute.key]}</span>
							{#if (pendingPoints[attribute.key] ?? 0) > 0}
								<span class="chip preset-filled-secondary-500 text-xs">
									+{pendingPoints[attribute.key]}
								</span>
							{/if}
							<span class="flex gap-1">
								<button
									class="btn preset-tonal-surface px-1.5 py-0.5 text-xs"
									type="button"
									aria-label={`Decrease ${attribute.label}`}
									disabled={(pendingPoints[attribute.key] ?? 0) === 0}
									onclick={() => removePoint(attribute.key)}
								>
									<Minus size={12} aria-hidden="true" />
								</button>
								<button
									class="btn preset-tonal-surface px-1.5 py-0.5 text-xs"
									type="button"
									aria-label={`Increase ${attribute.label}`}
									disabled={remainingPoints <= 0}
									onclick={() => addPoint(attribute.key)}
								>
									<Plus size={12} aria-hidden="true" />
								</button>
							</span>
						</span>
					</li>
				{/each}
			</ul>

			<div class="mt-4 flex justify-end gap-2">
				<button class="btn preset-tonal-surface" type="button" onclick={closeAttributes}>
					Cancel
				</button>
				<button
					class="btn preset-filled-primary-500"
					type="button"
					disabled={spentPoints === 0}
					onclick={confirmAllocation}
				>
					Spend {spentPoints} point{spentPoints === 1 ? '' : 's'}
				</button>
			</div>
		</Dialog.Content>
	</Dialog.Portal>
</Dialog.Root>

<Dialog.Root
	open={Boolean(ownedItem)}
	onOpenChange={(open) => {
		if (!open) session.closeInspect();
	}}
>
	<Dialog.Portal>
		<Dialog.Overlay class="game-dialog-backdrop" />
		<Dialog.Content class="game-dialog" aria-label="Item details">
			{#if ownedItem}
				{@const DetailIcon = itemIcon(ownedItem)}
				<header class="game-dialog-header">
					<Dialog.Title class="game-panel-header {rarityClass(ownedItem.rarity)}">
						<DetailIcon size={18} aria-hidden="true" />
						{itemLabel(ownedItem)}
					</Dialog.Title>
					<Dialog.Close
						class="btn preset-tonal-surface px-2 py-1 text-xs"
						aria-label="Close item details"
					>
						<X size={14} aria-hidden="true" />
					</Dialog.Close>
				</header>

				<Dialog.Description class="text-xs text-surface-600-400">
					{ownedItem.rarity} · {ownedItem.type}{ownedItem.slot
						? ` · ${slotLabel(ownedItem.slot)}`
						: ''}{ownedItem.typeKey ? ` · ${ownedItem.typeKey}` : ''}
				</Dialog.Description>

				{#if ownedItem.description}
					<p class="text-lore mt-2 text-sm text-surface-600-400">{ownedItem.description}</p>
				{/if}

				<dl class="mt-3 space-y-1 text-sm">
					{#if damagedLabel}
						<div class="flex justify-between gap-3">
							<dt class="text-surface-600-400">Damage</dt>
							<dd>{damagedLabel}</dd>
						</div>
					{/if}
					{#if armourLabel}
						<div class="flex justify-between gap-3">
							<dt class="text-surface-600-400">Armour</dt>
							<dd>{armourLabel}</dd>
						</div>
					{/if}
					{#if ownedItem.durability != null}
						<div class="flex justify-between gap-3">
							<dt class="text-surface-600-400">Durability</dt>
							<dd>{ownedItem.durability}%</dd>
						</div>
					{/if}
					{#if isUsable(ownedItem)}
						<div class="flex justify-between gap-3">
							<dt class="text-surface-600-400">Effect</dt>
							<dd>{effectLabel(ownedItem)}</dd>
						</div>
					{/if}
					{#if requiresLabel.length > 0}
						<div class="flex justify-between gap-3">
							<dt class="text-surface-600-400">Requires</dt>
							<dd>{requiresLabel.join(' · ')}</dd>
						</div>
					{/if}
					{#if bonusLabel.length > 0}
						<div class="flex justify-between gap-3">
							<dt class="text-surface-600-400">Bonuses</dt>
							<dd>{bonusLabel.join(' · ')}</dd>
						</div>
					{/if}
					<div class="flex justify-between gap-3">
						<dt class="text-surface-600-400">Value</dt>
						<dd>{ownedItem.price} g</dd>
					</div>
					{#if (ownedItem.quantity ?? 1) > 1}
						<div class="flex justify-between gap-3">
							<dt class="text-surface-600-400">Stack</dt>
							<dd>{ownedItem.quantity} / {ownedItem.stackSize}</dd>
						</div>
					{/if}
				</dl>

				<div class="mt-4 flex flex-wrap justify-end gap-2">
					{#if isUsable(ownedItem)}
						<button
							class="btn preset-filled-primary-500"
							type="button"
							disabled={session.combat.active}
							onclick={() => session.useItem(ownedItem!.itemRef)}
						>
							<FlaskConical size={14} aria-hidden="true" />
							Use
						</button>
					{/if}
					{#if ownedItem.isEquipped}
						<button
							class="btn preset-tonal-surface"
							type="button"
							onclick={() => session.unequip(ownedItem!.slot ?? '')}
						>
							<Undo2 size={14} aria-hidden="true" />
							Unequip
						</button>
					{:else if isEquippable(ownedItem)}
						<button
							class="btn preset-filled-primary-500"
							type="button"
							onclick={() => session.equipItem(ownedItem!.itemRef)}
						>
							<Swords size={14} aria-hidden="true" />
							Equip
						</button>
					{/if}
					{#if session.canShop && !ownedItem.isEquipped}
						<button
							class="btn preset-tonal-surface"
							type="button"
							onclick={() => session.sell(ownedItem!.itemRef)}
						>
							<Coins size={14} aria-hidden="true" />
							Sell
						</button>
					{/if}
					<button
						class="btn preset-tonal-surface"
						type="button"
						onclick={() => session.closeInspect()}
					>
						Close
					</button>
				</div>
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
