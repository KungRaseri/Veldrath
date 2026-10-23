<script lang="ts">
	import { onMount } from 'svelte';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import type { PageData } from './$types';
	import CardinalMap from '$lib/game/CardinalMap.svelte';
	import { GameSession } from '$lib/game/session.svelte';
	import { itemLabel, itemSubtitle } from '$lib/game/types';

	let { data }: { data: PageData } = $props();

	const session = new GameSession();

	onMount(() => {
		void session.connect(data.characterId);
		return () => {
			void session.disconnect();
		};
	});

	function barPercent(value: number | undefined, max: number | undefined): number {
		if (!value || !max || max <= 0) return 0;
		return Math.min(100, Math.round((value / max) * 100));
	}

	async function returnToCharacters() {
		await session.disconnect();
		await goto(resolve('/characters'));
	}
</script>

<svelte:head>
	<title>{session.character?.name ?? 'Entering Veldrath'} — Veldrath</title>
</svelte:head>

<div class="mx-auto max-w-6xl space-y-4 p-4 sm:p-6">
	<header class="flex flex-wrap items-center justify-between gap-4 card p-4">
		<div class="space-y-1">
			<h1 class="h2">
				{#if session.character}
					{session.character.name}
					<span class="text-surface-600-400">
						· Level {session.character.level}
						{session.character.className}
					</span>
				{:else}
					Entering Veldrath…
				{/if}
			</h1>
			<p class="text-sm text-surface-600-400">
				{session.zoneLabel}
				{#if session.serverInfo}
					· server {session.serverInfo.serverVersion}
				{/if}
				· hub {session.phase}
			</p>
		</div>

		<div class="flex flex-wrap gap-2">
			<button class="btn preset-tonal-surface" onclick={() => session.requestMapGraph(null)}>
				Refresh map
			</button>
			<button class="btn preset-filled-primary-500" onclick={() => session.openInventory()}>
				Inventory
			</button>
			<button class="btn preset-tonal-surface" onclick={returnToCharacters}> Characters </button>
		</div>
	</header>

	{#if session.character}
		<section class="grid gap-3 sm:grid-cols-3">
			<div class="space-y-2 card p-4">
				<p class="text-xs tracking-wide text-surface-600-400 uppercase">Health</p>
				<div class="h-2 overflow-hidden rounded bg-surface-200-800">
					<div
						class="h-full bg-error-500"
						style:width={`${barPercent(session.character.currentHealth, session.character.maxHealth)}%`}
					></div>
				</div>
				<p class="text-sm">
					{session.character.currentHealth} / {session.character.maxHealth}
				</p>
			</div>

			<div class="space-y-2 card p-4">
				<p class="text-xs tracking-wide text-surface-600-400 uppercase">Mana</p>
				<div class="h-2 overflow-hidden rounded bg-surface-200-800">
					<div
						class="h-full bg-primary-500"
						style:width={`${barPercent(session.character.currentMana, session.character.maxMana)}%`}
					></div>
				</div>
				<p class="text-sm">{session.character.currentMana} / {session.character.maxMana}</p>
			</div>

			<div class="space-y-2 card p-4">
				<p class="text-xs tracking-wide text-surface-600-400 uppercase">Gold</p>
				<p class="h3">{session.character.gold.toLocaleString()}</p>
				<p class="text-sm text-surface-600-400">
					{session.character.experience.toLocaleString()} XP
				</p>
			</div>
		</section>
	{/if}

	{#if session.error}
		<div class="card border border-error-500/40 p-4 text-sm text-error-500">
			{session.error}
		</div>
	{/if}

	{#if session.message}
		<div class="card border border-primary-500/40 p-3 text-sm">{session.message}</div>
	{/if}

	<div class="grid gap-4 lg:grid-cols-[minmax(0,1fr)_20rem]">
		<section class="card p-4">
			<div class="mb-3 flex items-center justify-between gap-3">
				<div>
					<h2 class="h3">{session.map?.name ?? 'Map'}</h2>
					<p class="text-sm text-surface-600-400">
						{session.map?.mapType ?? 'Loading'} · {session.map?.nodes.length ?? 0} nodes
					</p>
				</div>
				{#if session.zone}
					<span class="chip preset-tonal-surface text-xs">{session.zone.zoneType}</span>
				{/if}
			</div>

			{#if session.loadingMap}
				<p class="text-sm text-surface-600-400">Loading map…</p>
			{:else if !session.map}
				<p class="text-sm text-surface-600-400">Select a character to load the map.</p>
			{:else}
				<CardinalMap
					map={session.map}
					currentNodeId={session.currentNodeId}
					selectedNodeId={session.selectedNodeId}
					adjacentNodeIds={session.adjacentNodeIds}
					onSelect={(nodeId) => session.selectNode(nodeId)}
				/>
			{/if}
		</section>

		<aside class="space-y-3 card p-4">
			<h2 class="h3">Node details</h2>

			{#if session.selectedNode}
				<div class="space-y-2">
					<p class="font-semibold">{session.selectedNode.displayName}</p>
					<p class="text-sm text-surface-600-400">{session.selectedNode.description}</p>

					{#if session.selectedNode.refId}
						<p class="text-xs text-surface-600-400">
							Reference: {session.selectedNode.refId}
						</p>
					{/if}

					{#if session.selectedNode.isHidden}
						<p class="text-xs text-warning-500">Undiscovered</p>
					{/if}

					<button
						class="btn w-full preset-filled-primary-500"
						disabled={!session.adjacentNodeIds.includes(session.selectedNode.id)}
						onclick={() => session.travel(session.selectedNode!.id)}
					>
						{session.adjacentNodeIds.includes(session.selectedNode.id)
							? `Travel to ${session.selectedNode.displayName}`
							: 'Not directly reachable'}
					</button>
				</div>
			{:else}
				<p class="text-sm text-surface-600-400">Select a node on the map.</p>
			{/if}

			{#if session.zone && session.zone.occupants.length > 0}
				<div class="space-y-1 border-t border-surface-200-800 pt-3">
					<p class="text-xs tracking-wide text-surface-600-400 uppercase">Also here</p>
					<ul class="space-y-1 text-sm">
						{#each session.zone.occupants as occupant (occupant.characterId)}
							<li>{occupant.characterName}</li>
						{/each}
					</ul>
				</div>
			{/if}
		</aside>
	</div>
</div>

{#if session.showInventory}
	<section
		class="fixed inset-y-0 right-0 z-50 w-full max-w-md overflow-y-auto border-l border-surface-200-800 bg-surface-100-900 p-5 shadow-2xl"
		aria-label="Inventory"
	>
		<div class="mb-4 flex items-center justify-between">
			<h2 class="h3">Inventory</h2>
			<button class="btn preset-tonal-surface" onclick={() => (session.showInventory = false)}>
				Close
			</button>
		</div>

		{#if session.equippedEntries.length > 0}
			<div class="mb-4 space-y-2">
				<h3 class="text-xs tracking-wide text-surface-600-400 uppercase">Equipped</h3>
				<ul class="space-y-1">
					{#each session.equippedEntries as [slot, item] (slot)}
						<li class="rounded border border-surface-200-800 p-2 text-sm">
							<strong>{slot}</strong>
							· {itemLabel(item)}
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
						<li class="rounded border border-surface-200-800 p-2 text-sm">
							<strong>{itemLabel(item)}</strong>
							{#if (item.quantity ?? 1) > 1}
								<span class="text-surface-600-400">× {item.quantity}</span>
							{/if}
							<span class="block text-xs text-surface-600-400">{itemSubtitle(item)}</span>
						</li>
					{/each}
				</ul>
			{/if}
		</div>
	</section>
{/if}
