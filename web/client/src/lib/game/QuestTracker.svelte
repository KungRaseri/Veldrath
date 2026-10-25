<script lang="ts">
	import { ScrollText } from '@lucide/svelte';
	import type { GameSession } from './session.svelte';

	let { session }: { session: GameSession } = $props();
</script>

{#if session.trackedQuests.length > 0}
	<section class="game-panel space-y-2 p-3" aria-label="Quest tracker">
		<h2 class="game-panel-header">
			<ScrollText size={15} aria-hidden="true" />
			Quests
		</h2>

		<ul class="space-y-1.5">
			{#each session.trackedQuests as quest (quest.slug)}
				<li>
					<p class="text-sm font-semibold">{quest.title}</p>
					{#if quest.objective}
						<p class="text-xs text-surface-600-400">
							{quest.objective.key} · {quest.objective.current}/{quest.objective.required}
						</p>
					{:else}
						<p class="text-xs text-secondary-500">Ready to turn in</p>
					{/if}
				</li>
			{/each}
		</ul>
	</section>
{/if}
