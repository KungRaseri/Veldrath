<script lang="ts">
	import { Check, DoorOpen, MapPin, Swords, Undo2 } from '@lucide/svelte';
	import { currentRoom, dungeonProgress, dungeonRooms, exitNode, isDungeonZone } from './dungeon';
	import type { GameSession } from './session.svelte';

	let { session }: { session: GameSession } = $props();

	const isDungeon = $derived(isDungeonZone(session.zone?.zoneType));
	const rooms = $derived(
		dungeonRooms(
			session.map,
			session.currentNodeId,
			session.adjacentNodeIds,
			session.clearedLocations
		)
	);
	const progress = $derived(dungeonProgress(rooms));
	const here = $derived(currentRoom(rooms));
	const exit = $derived(exitNode(session.map));
</script>

{#if isDungeon && session.map}
	<section class="game-panel space-y-3 p-4" data-testid="dungeon-panel">
		<div class="flex flex-wrap items-center justify-between gap-2">
			<div>
				<h2 class="game-panel-header">
					<Swords size={18} aria-hidden="true" />
					{session.zone?.name ?? 'Dungeon'}
				</h2>
				<p class="text-sm text-surface-600-400">
					{#if here}
						Standing in {here.label}
					{:else}
						On the floor
					{/if}
					· {progress.cleared}/{progress.total} rooms cleared
					{#if session.instanceId}
						· this attempt
					{/if}
				</p>
			</div>

			{#if exit}
				<button
					class="btn preset-tonal-surface"
					type="button"
					onclick={() => session.travel(exit.id)}
				>
					<DoorOpen size={15} aria-hidden="true" />
					Leave the dungeon
				</button>
			{/if}
		</div>

		<ul class="space-y-1.5">
			{#each rooms as room (room.nodeId)}
				<li
					class="flex items-center justify-between gap-2 rounded-lg border border-surface-200-800 px-3 py-2 text-sm"
					aria-current={room.isCurrent ? 'location' : undefined}
				>
					<span class="flex items-center gap-2">
						{#if room.isCleared}
							<Check size={14} aria-hidden="true" class="text-success-500" />
						{:else}
							<MapPin size={14} aria-hidden="true" class="text-surface-500" />
						{/if}
						<span class:font-semibold={room.isCurrent}>{room.label}</span>
						{#if room.isCurrent}
							<span class="text-xs text-surface-600-400">you are here</span>
						{:else if room.isCleared}
							<span class="text-xs text-surface-600-400">cleared</span>
						{/if}
					</span>

					{#if room.isCurrent && room.slug}
						<!-- The party action: enter the room you are standing at, through the one entry path. -->
						<button
							class="btn preset-filled-primary-500 px-2 py-1 text-xs"
							type="button"
							disabled={!session.zone}
							onclick={() => session.enterLocation(session.zone!.id, room.slug!)}
						>
							<Undo2 size={13} aria-hidden="true" />
							Enter room
						</button>
					{:else if room.isAdjacent}
						<button
							class="btn preset-tonal-surface px-2 py-1 text-xs"
							type="button"
							onclick={() => session.travel(room.nodeId)}
						>
							Walk here
						</button>
					{/if}
				</li>
			{/each}
		</ul>
	</section>
{/if}
