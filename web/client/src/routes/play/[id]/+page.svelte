<script lang="ts">
	import { onMount } from 'svelte';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import type { PageData } from './$types';
	import CardinalMap from '$lib/game/CardinalMap.svelte';
	import CombatPanel from '$lib/game/CombatPanel.svelte';
	import GameFooter from '$lib/game/GameFooter.svelte';
	import GameHeader from '$lib/game/GameHeader.svelte';
	import GameOverlays from '$lib/game/GameOverlays.svelte';
	import LeftSidebar from '$lib/game/LeftSidebar.svelte';
	import RightSidebar from '$lib/game/RightSidebar.svelte';
	import { GameSession } from '$lib/game/session.svelte';

	let { data }: { data: PageData } = $props();

	const session = new GameSession();

	onMount(() => {
		void session.connect(data.characterId);
		return () => {
			void session.disconnect();
		};
	});

	async function returnToCharacters() {
		await session.disconnect();
		await goto(resolve('/characters'));
	}
</script>

<svelte:head>
	<title>{session.character?.name ?? 'Entering Veldrath'} — Veldrath</title>
</svelte:head>

<div class="flex min-h-screen flex-col gap-3 p-3">
	<GameHeader {session} onLeave={returnToCharacters} />

	{#if session.phase === 'reconnecting'}
		<div class="card border border-warning-500/40 p-3 text-sm">
			Connection lost. Reconnecting and restoring your session…
		</div>
	{/if}

	{#if session.dead}
		<div
			class="flex flex-wrap items-center justify-between gap-3 card border border-error-500/40 p-3 text-sm"
		>
			<span>You have been defeated.</span>
			<button class="btn preset-filled-primary-500" onclick={() => session.respawn()}>
				Respawn
			</button>
		</div>
	{/if}

	{#if session.error}
		<div
			class="flex items-center justify-between gap-3 card border border-error-500/40 p-3 text-sm text-error-500"
		>
			<span>{session.error}</span>
			<button
				class="btn preset-tonal-surface px-2 py-1 text-xs"
				onclick={() => (session.error = null)}
			>
				Dismiss
			</button>
		</div>
	{/if}

	<div class="grid min-h-0 flex-1 gap-3 lg:grid-cols-[15rem_minmax(0,1fr)_20rem]">
		<LeftSidebar {session} />

		<main class="min-w-0 space-y-3">
			{#if session.combat.active}
				<CombatPanel {session} />
			{:else}
				<section class="card p-4">
					<div class="mb-3 flex flex-wrap items-center justify-between gap-3">
						<div>
							<h2 class="h3">{session.map?.name ?? 'Map'}</h2>
							<p class="text-sm text-surface-600-400">
								{session.map?.mapType ?? 'Loading'} · {session.map?.nodes.length ?? 0} nodes
							</p>
						</div>
						<div class="flex gap-2">
							<button
								class="btn preset-tonal-surface"
								onclick={() => session.requestMapGraph(null)}
							>
								Refresh
							</button>
							{#if session.zone}
								<button
									class="btn preset-tonal-surface"
									onclick={() => session.visitShop()}
									disabled={!session.canShop}
								>
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

				<section class="space-y-3 card p-4">
					<h2 class="h3">Node details</h2>

					{#if session.selectedNode}
						<div class="flex flex-wrap items-start justify-between gap-3">
							<div class="space-y-1">
								<p class="font-semibold">
									{session.selectedNode.displayName}
									<span class="text-surface-600-400">· {session.selectedNode.nodeType}</span>
								</p>
								<p class="text-sm text-surface-600-400">{session.selectedNode.description}</p>
								{#if session.selectedNode.refId}
									<p class="text-xs text-surface-600-400">
										Reference: {session.selectedNode.refId}
									</p>
								{/if}
							</div>

							<button
								class="btn preset-filled-primary-500"
								disabled={!session.adjacentNodeIds.includes(session.selectedNode.id) ||
									session.dead}
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

					{#if session.zone && session.occupants.length > 0}
						<div class="space-y-1 border-t border-surface-200-800 pt-3">
							<p class="text-xs tracking-wide text-surface-600-400 uppercase">Also here</p>
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

		<RightSidebar {session} />
	</div>

	<GameFooter {session} />
</div>

<GameOverlays {session} />
