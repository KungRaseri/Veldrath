<script lang="ts">
	import {
		ArrowUpFromLine,
		BedDouble,
		Bug,
		Compass,
		Footprints,
		LogOut,
		Map as MapIcon,
		MapPin,
		RefreshCw,
		Search,
		Store,
		Swords
	} from '@lucide/svelte';
	import CardinalMap from '#lib/game/CardinalMap.svelte';
	import CombatPanel from '#lib/game/CombatPanel.svelte';
	import GameHeader from '#lib/game/GameHeader.svelte';
	import GameOverlays from '#lib/game/GameOverlays.svelte';
	import LeftSidebar from '#lib/game/LeftSidebar.svelte';
	import RightSidebar from '#lib/game/RightSidebar.svelte';
	import type { GameSession } from '#lib/game/session.svelte';
	import { rarityClass } from '#lib/game/types';

	let { session, onLeave }: { session: GameSession; onLeave: () => void } = $props();

	const focusNode = $derived(session.selectedNode ?? session.currentNode);
	const isSelectedNodeReachable = $derived(
		Boolean(session.selectedNode && session.adjacentNodeIds.includes(session.selectedNode.id))
	);
</script>

<svelte:head>
	<title>{session.character?.name ?? 'Entering Veldrath'} — Veldrath</title>
</svelte:head>

<div class="flex min-h-screen flex-col gap-3 p-3">
	<GameHeader {session} {onLeave} />

	{#if session.phase === 'reconnecting'}
		<div class="game-panel border border-warning-500/40 p-3 text-sm">
			Connection lost. Reconnecting and restoring your session…
		</div>
	{/if}

	{#if session.dead}
		<div
			class="game-panel flex flex-wrap items-center justify-between gap-3 border border-error-500/40 p-3 text-sm"
		>
			<span>You have been defeated.</span>
			<button class="btn preset-filled-primary-500" type="button" onclick={() => session.respawn()}>
				Respawn
			</button>
		</div>
	{/if}

	{#if session.error}
		<div
			class="game-panel flex items-center justify-between gap-3 border border-error-500/40 p-3 text-sm text-error-500"
		>
			<span>{session.error}</span>
			<button
				class="btn preset-tonal-surface px-2 py-1 text-xs"
				type="button"
				onclick={() => (session.error = null)}
			>
				Dismiss
			</button>
		</div>
	{/if}

	<div class="game-shell min-h-0 flex-1">
		<div class="game-column game-column-left">
			<LeftSidebar {session} />
		</div>

		<main class="game-column game-column-main min-w-0 space-y-3">
			{#if session.combat.active}
				<CombatPanel {session} />
			{:else}
				<section class="game-panel p-4">
					<div class="mb-3 flex flex-wrap items-center justify-between gap-3">
						<div>
							<h2 class="game-panel-header">
								<MapIcon size={19} aria-hidden="true" />
								{session.map?.name ?? 'Map'}
							</h2>
							<p class="text-sm text-surface-600-400">
								{session.zoneLabel}
								{#if session.location}
									· {session.location.locationDisplayName}
								{/if}
								{#if session.map}
									· {session.map.nodes.length} nodes
								{/if}
							</p>
						</div>
						<button
							class="btn preset-tonal-surface"
							type="button"
							onclick={() => session.requestMapGraph(null)}
						>
							<RefreshCw size={15} aria-hidden="true" />
							Refresh
						</button>
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

				<section class="game-panel space-y-3 p-4">
					<h2 class="game-panel-header">
						<Compass size={18} aria-hidden="true" />
						{session.selectedNode ? 'Selected node' : 'You are here'}
					</h2>

					{#if focusNode}
						<div class="flex flex-wrap items-start justify-between gap-3">
							<div class="space-y-1">
								<p class="flex items-center gap-2 font-semibold">
									<MapPin class="text-secondary-500" size={15} aria-hidden="true" />
									{focusNode.displayName}
									<span class="text-surface-600-400">· {focusNode.nodeType}</span>
								</p>
								<p class="text-lore text-sm text-surface-600-400">
									{focusNode.description}
								</p>
								{#if focusNode.refId}
									<p class="text-mono text-xs">Reference: {focusNode.refId}</p>
								{/if}
							</div>

							{#if session.selectedNode && session.selectedNode.id !== session.currentNodeId}
								<button
									class="btn preset-filled-primary-500"
									type="button"
									disabled={!isSelectedNodeReachable || session.dead}
									onclick={() => session.travel(session.selectedNode!.id)}
								>
									<Footprints size={15} aria-hidden="true" />
									{isSelectedNodeReachable
										? `Travel to ${session.selectedNode.displayName}`
										: 'Not directly reachable'}
								</button>
							{:else}
								<span class="chip preset-tonal-surface text-xs">Current location</span>
							{/if}
						</div>

						{#if session.adjacentNodeIds.length > 0}
							<div class="flex flex-wrap items-center gap-2 border-t border-surface-800 pt-3">
								<span class="text-xs tracking-wide text-surface-600-400 uppercase">Routes</span>
								{#each session.adjacentNodeIds as nodeId (nodeId)}
									{@const node = session.map?.nodes.find((candidate) => candidate.id === nodeId)}
									{#if node}
										<button
											class="btn preset-tonal-surface px-2 py-1 text-xs"
											type="button"
											disabled={session.dead}
											onclick={() => session.travel(node.id)}
										>
											<Footprints size={12} aria-hidden="true" />
											{node.displayName}
										</button>
									{/if}
								{/each}
							</div>
						{/if}
					{:else}
						<p class="text-sm text-surface-600-400">
							{session.map ? 'Select a node on the map.' : 'Waiting for the map…'}
						</p>
					{/if}
				</section>

				<section class="game-panel space-y-3 p-4">
					<h2 class="game-panel-header">
						<Bug size={18} aria-hidden="true" />
						Local
					</h2>

					{#if session.enemies.length === 0}
						<p class="text-sm text-surface-600-400">
							{session.location
								? 'Nothing hostile is lurking here right now.'
								: 'Move to a location to see what is here.'}
						</p>
					{:else}
						<ul class="space-y-2">
							{#each session.enemies as enemy (enemy.id)}
								<li class="flex items-center justify-between gap-2 text-sm">
									<span>
										{enemy.name}
										<span class="text-surface-600-400">
											· Lv {enemy.level} · {enemy.currentHealth}/{enemy.maxHealth}
										</span>
									</span>
									<button
										class="btn preset-filled-error-500 px-2 py-1 text-xs"
										type="button"
										onclick={() => session.engageEnemy(enemy.id)}
										disabled={session.dead || session.combat.active}
									>
										<Swords size={13} aria-hidden="true" />
										Engage
									</button>
								</li>
							{/each}
						</ul>
					{/if}

					{#if session.groundItems.length > 0}
						<div class="space-y-1 border-t border-surface-800 pt-3">
							<p class="text-xs tracking-wide text-surface-600-400 uppercase">On the ground</p>
							<ul class="space-y-1">
								{#each session.groundItems as ground (ground.id)}
									<li class="flex items-center justify-between gap-2 text-sm">
										<span class={rarityClass(ground.rarity)}>
											{ground.name}
											{#if ground.quantity > 1}
												<span class="text-surface-600-400">× {ground.quantity}</span>
											{/if}
										</span>
										<button
											class="btn preset-tonal-surface px-2 py-1 text-xs"
											type="button"
											onclick={() => session.pickUpGroundItem(ground.id)}
										>
											<ArrowUpFromLine size={12} aria-hidden="true" />
											Pick up
										</button>
									</li>
								{/each}
							</ul>
						</div>
					{/if}

					{#if session.lastSearch && session.lastSearch.discovered.length > 0}
						<div class="space-y-1 border-t border-surface-800 pt-3">
							<p class="text-xs tracking-wide text-surface-600-400 uppercase">
								Discovered · search roll {session.lastSearch.rollValue}
							</p>
							<ul class="flex flex-wrap gap-2">
								{#each session.lastSearch.discovered as finding (finding.slug)}
									{@const node = session.map?.nodes.find(
										(candidate) => candidate.refId === finding.slug || candidate.id === finding.slug
									)}
									<li>
										{#if node && session.adjacentNodeIds.includes(node.id) && !session.dead}
											<button
												class="btn preset-tonal-surface px-2 py-1 text-xs"
												type="button"
												title={`Travel to ${finding.displayName}`}
												onclick={() => session.travel(node.id)}
											>
												<Footprints size={12} aria-hidden="true" />
												{finding.displayName}
											</button>
										{:else}
											<span class="chip preset-tonal-surface text-xs">
												{finding.displayName}
												<span class="text-surface-600-400">· {finding.typeKey}</span>
											</span>
										{/if}
									</li>
								{/each}
							</ul>
						</div>
					{/if}

					<div class="flex flex-wrap gap-2 border-t border-surface-800 pt-3">
						<button
							class="btn preset-tonal-surface"
							type="button"
							onclick={() => session.searchArea()}
							disabled={!session.zone || session.dead}
						>
							<Search size={15} aria-hidden="true" />
							Search area
						</button>
						<button
							class="btn preset-tonal-surface"
							type="button"
							onclick={() => session.rest()}
							disabled={!session.canRest || session.dead}
						>
							<BedDouble size={15} aria-hidden="true" />
							{session.canRest ? `Rest (${session.location?.locationDisplayName})` : 'Rest'}
						</button>
						{#if session.canShop}
							<button
								class="btn preset-tonal-surface"
								type="button"
								onclick={() => session.visitShop()}
							>
								<Store size={15} aria-hidden="true" />
								Merchant
							</button>
						{/if}
						<button
							class="ml-auto btn preset-tonal-surface"
							type="button"
							onclick={() => session.leaveZone()}
							disabled={!session.zone}
						>
							<LogOut size={15} aria-hidden="true" />
							Leave zone
						</button>
					</div>
				</section>
			{/if}
		</main>

		<div class="game-column game-column-right">
			<RightSidebar {session} />
		</div>
	</div>
</div>

<GameOverlays {session} />
