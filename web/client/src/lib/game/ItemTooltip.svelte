<script lang="ts">
	import {
		armorLabel,
		damageLabel,
		effectLabel,
		isUsable,
		itemLabel,
		rarityClass,
		type CharacterAttributes,
		type InventoryItem
	} from './types';

	let {
		item,
		level = 0,
		attributes,
		anchor = null
	}: {
		item: InventoryItem;
		level?: number;
		attributes?: CharacterAttributes;
		anchor?: DOMRect | null;
	} = $props();

	const WIDTH = 304;
	const MARGIN = 12;

	/** Places the card beside the anchor, flipping sides when it would overflow the viewport. */
	const position = $derived.by(() => {
		if (!anchor) return { left: MARGIN, top: MARGIN };

		const viewportWidth = typeof window === 'undefined' ? 1280 : window.innerWidth;
		const viewportHeight = typeof window === 'undefined' ? 800 : window.innerHeight;

		let left = anchor.right + MARGIN;
		if (left + WIDTH > viewportWidth - MARGIN) left = anchor.left - WIDTH - MARGIN;
		if (left < MARGIN) left = MARGIN;

		let top = anchor.top - 4;
		if (top < MARGIN) top = MARGIN;
		if (top > viewportHeight - 240) top = Math.max(MARGIN, viewportHeight - 240);

		return { left, top };
	});

	const damage = $derived(damageLabel(item));
	const armour = $derived(armorLabel(item));

	const requirements = $derived.by(() => {
		const rows: { label: string; met: boolean }[] = [];

		if (item.requirementsLevel > 1) {
			rows.push({ label: `Level ${item.requirementsLevel}`, met: level >= item.requirementsLevel });
		}

		for (const [attribute, value] of Object.entries(item.requirements ?? {})) {
			const current = attributes?.[attribute.toLowerCase() as keyof CharacterAttributes] ?? 0;
			const label = `${attribute.charAt(0).toUpperCase()}${attribute.slice(1)} ${value}`;
			rows.push({ label, met: current >= value });
		}

		return rows;
	});

	const bonuses = $derived(
		Object.entries(item.bonuses ?? {}).map(
			([attribute, value]) =>
				`${attribute.charAt(0).toUpperCase()}${attribute.slice(1)} ${value > 0 ? '+' : ''}${value}`
		)
	);
</script>

<div
	class="item-tooltip"
	style:left={`${position.left}px`}
	style:top={`${position.top}px`}
	role="tooltip"
	aria-label={`${itemLabel(item)} details`}
>
	<header class="item-tooltip-header">
		<p class="item-tooltip-name {rarityClass(item.rarity)}">{itemLabel(item)}</p>
		<p class="item-tooltip-type">
			{item.rarity}
			{#if item.type}· {item.type}{/if}
			{#if item.slot}· {item.slot}{/if}
		</p>
	</header>

	{#if item.description}
		<p class="item-tooltip-desc">{item.description}</p>
	{/if}

	<div class="item-tooltip-stats">
		{#if damage}
			<div class="item-tooltip-row">
				<span class="item-tooltip-label">Damage</span>
				<span>{damage}</span>
			</div>
		{/if}
		{#if armour}
			<div class="item-tooltip-row">
				<span class="item-tooltip-label">Armour</span>
				<span>{armour.replace(' armour', '')}</span>
			</div>
		{/if}
		{#if item.durability != null}
			<div class="item-tooltip-row">
				<span class="item-tooltip-label">Durability</span>
				<span>{item.durability}%</span>
			</div>
		{/if}
		{#if isUsable(item)}
			<div class="item-tooltip-row">
				<span class="item-tooltip-label">Effect</span>
				<span>{effectLabel(item)}</span>
			</div>
		{/if}
		{#if item.isEquipped}
			<div class="item-tooltip-row">
				<span class="item-tooltip-label">State</span>
				<span class="item-tooltip-equipped">Equipped</span>
			</div>
		{/if}
	</div>

	{#if bonuses.length > 0}
		<div class="item-tooltip-divider"></div>
		<ul class="item-tooltip-list">
			{#each bonuses as bonus (bonus)}
				<li class="item-tooltip-bonus">{bonus}</li>
			{/each}
		</ul>
	{/if}

	{#if requirements.length > 0}
		<div class="item-tooltip-divider"></div>
		<p class="item-tooltip-label">Requires</p>
		<ul class="item-tooltip-list">
			{#each requirements as requirement (requirement.label)}
				<li class={requirement.met ? 'item-tooltip-met' : 'item-tooltip-unmet'}>
					{requirement.label}
				</li>
			{/each}
		</ul>
	{/if}

	<div class="item-tooltip-divider"></div>
	<footer class="item-tooltip-footer">
		<span>{item.price} g</span>
		{#if (item.quantity ?? 1) > 1}
			<span>{item.quantity} / {item.stackSize}</span>
		{/if}
		{#if item.typeKey}
			<span class="item-tooltip-type">{item.typeKey}</span>
		{/if}
	</footer>
</div>
