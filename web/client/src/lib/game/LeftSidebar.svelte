<script lang="ts">
	import type { GameSession } from './session.svelte';

	let { session }: { session: GameSession } = $props();
</script>

<aside class="flex min-h-0 flex-col gap-3">
	<section class="space-y-2 card p-4">
		<h2 class="h3">Location</h2>
		{#if session.location}
			<p class="font-semibold">{session.location.name}</p>
			<p class="text-xs text-surface-600-400">{session.location.type}</p>
			{#if session.location.description}
				<p class="text-sm text-surface-600-400">{session.location.description}</p>
			{/if}
		{:else if session.currentNode}
			<p class="font-semibold">{session.currentNode.displayName}</p>
			<p class="text-sm text-surface-600-400">{session.currentNode.description}</p>
		{:else}
			<p class="text-sm text-surface-600-400">Loading…</p>
		{/if}
	</section>

	{#if session.enemies.length > 0}
		<section class="space-y-2 card p-4">
			<h2 class="h3">Enemies</h2>
			<ul class="space-y-2">
				{#each session.enemies as enemy (enemy.id)}
					<li class="flex items-center justify-between gap-2 text-sm">
						<span>
							{enemy.name}
							<span class="text-surface-600-400">· Lv {enemy.level}</span>
						</span>
						<button
							class="btn preset-filled-error-500 px-2 py-1 text-xs"
							onclick={() => session.engageEnemy(enemy.id)}
							disabled={session.dead || session.combat.active}
						>
							Engage
						</button>
					</li>
				{/each}
			</ul>
		</section>
	{/if}

	<section class="space-y-2 card p-4">
		<h2 class="h3">Routes</h2>
		{#if session.adjacentNodeIds.length === 0}
			<p class="text-sm text-surface-600-400">No direct routes from here.</p>
		{:else}
			<ul class="space-y-1">
				{#each session.adjacentNodeIds as nodeId (nodeId)}
					{@const node = session.map?.nodes.find((candidate) => candidate.id === nodeId)}
					{#if node}
						<li class="flex items-center justify-between gap-2 text-sm">
							<span class="truncate">{node.displayName}</span>
							<button
								class="btn preset-tonal-surface px-2 py-1 text-xs"
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

	<section class="space-y-2 card p-4">
		<h2 class="h3">Actions</h2>
		<div class="grid gap-2">
			<button
				class="btn preset-tonal-surface"
				onclick={() => session.searchArea()}
				disabled={!session.zone || session.dead}
			>
				Search area
			</button>
			<button
				class="btn preset-tonal-surface"
				onclick={() => session.rest()}
				disabled={!session.canRest || session.dead}
			>
				Rest at {session.location?.name ?? 'location'}
			</button>
			<button
				class="btn preset-tonal-surface"
				onclick={() => session.visitShop()}
				disabled={!session.canShop}
			>
				Visit merchant
			</button>
			<button
				class="btn preset-tonal-surface"
				onclick={() => session.leaveZone()}
				disabled={!session.zone}
			>
				Leave zone
			</button>
			{#if session.dead}
				<button class="btn preset-filled-primary-500" onclick={() => session.respawn()}>
					Respawn
				</button>
			{/if}
		</div>
	</section>
</aside>
