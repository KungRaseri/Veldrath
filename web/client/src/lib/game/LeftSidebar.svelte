<script lang="ts">
	import {
		BedDouble,
		Bug,
		DoorOpen,
		LogOut,
		MapPin,
		Route,
		Search,
		Store,
		Swords,
		Zap
	} from '@lucide/svelte';
	import type { GameSession } from './session.svelte';

	let { session }: { session: GameSession } = $props();
</script>

<aside class="flex min-h-0 flex-col gap-3">
	<section class="game-panel space-y-2 p-4">
		<h2 class="game-panel-header">
			<MapPin size={17} aria-hidden="true" />
			Location
		</h2>
		{#if session.location}
			<p class="font-semibold">{session.location.locationDisplayName}</p>
			<p class="text-xs tracking-wide text-surface-600-400 uppercase">{session.location.typeKey}</p>
		{:else if session.currentNode}
			<p class="font-semibold">{session.currentNode.displayName}</p>
			<p class="text-sm text-surface-600-400">{session.currentNode.description}</p>
		{:else}
			<p class="text-sm text-surface-600-400">Loading…</p>
		{/if}
	</section>

	{#if session.enemies.length > 0}
		<section class="game-panel space-y-2 p-4">
			<h2 class="game-panel-header">
				<Bug size={17} aria-hidden="true" />
				Enemies
			</h2>
			<ul class="space-y-2">
				{#each session.enemies as enemy (enemy.id)}
					<li class="flex items-center justify-between gap-2 text-sm">
						<span>
							{enemy.name}
							<span class="text-surface-600-400">· Lv {enemy.level}</span>
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
		</section>
	{/if}

	<section class="game-panel space-y-2 p-4">
		<h2 class="game-panel-header">
			<Route size={17} aria-hidden="true" />
			Routes
		</h2>
		{#if session.adjacentNodeIds.length === 0}
			<p class="text-sm text-surface-600-400">No direct routes from here.</p>
		{:else}
			<ul class="space-y-1">
				{#each session.adjacentNodeIds as nodeId (nodeId)}
					{@const node = session.map?.nodes.find((candidate) => candidate.id === nodeId)}
					{#if node}
						<li class="flex items-center justify-between gap-2 text-sm">
							<span class="inline-flex items-center gap-1 truncate">
								<DoorOpen class="text-secondary-500" size={13} aria-hidden="true" />
								{node.displayName}
							</span>
							<button
								class="btn preset-tonal-surface px-2 py-1 text-xs"
								type="button"
								onclick={() => session.travel(node.id)}
								disabled={session.dead}
							>
								Travel
							</button>
						</li>
					{/if}
				{/each}
			</ul>
		{/if}
	</section>

	<section class="game-panel space-y-2 p-4">
		<h2 class="game-panel-header">
			<Zap size={17} aria-hidden="true" />
			Actions
		</h2>
		<div class="grid gap-2">
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
				Rest at {session.location?.locationDisplayName ?? 'location'}
			</button>
			<button
				class="btn preset-tonal-surface"
				type="button"
				onclick={() => session.visitShop()}
				disabled={!session.canShop}
			>
				<Store size={15} aria-hidden="true" />
				Visit merchant
			</button>
			<button
				class="btn preset-tonal-surface"
				type="button"
				onclick={() => session.leaveZone()}
				disabled={!session.zone}
			>
				<LogOut size={15} aria-hidden="true" />
				Leave zone
			</button>
			{#if session.dead}
				<button
					class="btn preset-filled-primary-500"
					type="button"
					onclick={() => session.respawn()}
				>
					Respawn
				</button>
			{/if}
		</div>
	</section>
</aside>
