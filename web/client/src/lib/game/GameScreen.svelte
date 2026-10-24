<script lang="ts">
	import {
		Compass,
		Footprints,
		Map as MapIcon,
		MapPin,
		RefreshCw,
		Store,
		Users
	} from '@lucide/svelte';
	import CardinalMap from '$lib/game/CardinalMap.svelte';
	import CombatPanel from '$lib/game/CombatPanel.svelte';
	import GameFooter from '$lib/game/GameFooter.svelte';
	import GameHeader from '$lib/game/GameHeader.svelte';
	import GameOverlays from '$lib/game/GameOverlays.svelte';
	import LeftSidebar from '$lib/game/LeftSidebar.svelte';
	import RightSidebar from '$lib/game/RightSidebar.svelte';
	import type { GameSession } from '$lib/game/session.svelte';

	let { session, onLeave }: { session: GameSession; onLeave: () => void } = $props();
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
								{session.map?.mapType ?? 'Loading'} · {session.map?.nodes.length ?? 0} nodes
							</p>
						</div>
						<div class="flex gap-2">
							<button
								class="btn preset-tonal-surface"
								type="button"
								onclick={() => session.requestMapGraph(null)}
							>
								<RefreshCw size={15} aria-hidden="true" />
								Refresh
							</button>
							{#if session.zone}
								<button
									class="btn preset-tonal-surface"
									type="button"
									onclick={() => session.visitShop()}
									disabled={!session.canShop}
								>
									<Store size={15} aria-hidden="true" />
									Merchant
								</button>
							{/if}
						</div>
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
						Node details
					</h2>

					{#if session.selectedNode}
						<div class="flex flex-wrap items-start justify-between gap-3">
							<div class="space-y-1">
								<p class="flex items-center gap-2 font-semibold">
									<MapPin class="text-secondary-500" size={15} aria-hidden="true" />
									{session.selectedNode.displayName}
									<span class="text-surface-600-400">· {session.selectedNode.nodeType}</span>
								</p>
								<p class="text-lore text-sm text-surface-600-400">
									{session.selectedNode.description}
								</p>
								{#if session.selectedNode.refId}
									<p class="text-mono text-xs">Reference: {session.selectedNode.refId}</p>
								{/if}
							</div>

							<button
								class="btn preset-filled-primary-500"
								type="button"
								disabled={!session.adjacentNodeIds.includes(session.selectedNode.id) ||
									session.dead}
								onclick={() => session.travel(session.selectedNode!.id)}
							>
								<Footprints size={15} aria-hidden="true" />
								{session.adjacentNodeIds.includes(session.selectedNode.id)
									? `Travel to ${session.selectedNode.displayName}`
									: 'Not directly reachable'}
							</button>
						</div>
					{:else}
						<p class="text-sm text-surface-600-400">Select a node on the map.</p>
					{/if}

					{#if session.zone && session.occupants.length > 0}
						<div class="space-y-1 border-t border-surface-800 pt-3">
							<p
								class="flex items-center gap-1 text-xs tracking-wide text-surface-600-400 uppercase"
							>
								<Users size={12} aria-hidden="true" />
								Also here
							</p>
							<ul class="flex flex-wrap gap-2 text-sm">
								{#each session.occupants as occupant (occupant.characterId)}
									<li class="chip preset-tonal-surface text-xs">{occupant.characterName}</li>
								{/each}
							</ul>
						</div>
					{/if}
				</section>
			{/if}
		</main>

		<div class="game-column game-column-right">
			<RightSidebar {session} />
		</div>
	</div>

	<GameFooter {session} />
</div>

<GameOverlays {session} />
