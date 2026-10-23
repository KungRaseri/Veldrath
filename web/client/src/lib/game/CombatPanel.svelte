<script lang="ts">
	import { barPercent } from './reducers';
	import type { GameSession } from './session.svelte';

	let { session }: { session: GameSession } = $props();

	const abilities = $derived((session.character?.learnedAbilities ?? []).slice(0, 6));
</script>

<section class="space-y-4 card p-4">
	<header class="flex flex-wrap items-center justify-between gap-3">
		<div>
			<h2 class="h2">
				Combat{#if session.combat.enemy}
					· {session.combat.enemy.name}
				{/if}
			</h2>
			{#if session.combat.enemy}
				<p class="text-sm text-surface-600-400">Level {session.combat.enemy.level}</p>
			{/if}
		</div>
		<span class="chip preset-filled-error-500 text-xs">Turn-based</span>
	</header>

	{#if session.combat.enemy}
		<div class="space-y-1">
			<div class="flex justify-between text-xs text-surface-600-400">
				<span>Enemy health</span>
				<span>
					{session.combat.enemy.currentHealth} / {session.combat.enemy.maxHealth}
				</span>
			</div>
			<div class="h-3 overflow-hidden rounded bg-surface-200-800">
				<div
					class="h-full bg-error-500 transition-all"
					style:width={`${barPercent(
						session.combat.enemy.currentHealth,
						session.combat.enemy.maxHealth
					)}%`}
				></div>
			</div>
		</div>
	{/if}

	<div class="flex flex-wrap gap-2">
		<button
			class="btn preset-filled-error-500"
			onclick={() => session.attack()}
			disabled={!session.combat.active}
		>
			Attack
		</button>
		<button
			class="btn preset-tonal-surface"
			onclick={() => session.defend()}
			disabled={!session.combat.active}
		>
			Defend
		</button>
		<button
			class="btn preset-tonal-surface"
			onclick={() => session.flee()}
			disabled={!session.combat.active}
		>
			Flee
		</button>
		{#each abilities as ability (ability)}
			<button
				class="btn preset-tonal-surface text-xs"
				onclick={() => session.useCombatAbility(ability)}
				disabled={!session.combat.active}
			>
				{ability}
			</button>
		{/each}
	</div>

	<div class="max-h-56 space-y-1 overflow-y-auto rounded bg-surface-50-950/50 p-3 text-sm">
		{#each session.combat.log.slice(-12) as line, index (index)}
			<p>{line}</p>
		{:else}
			<p class="text-surface-600-400">No combat yet.</p>
		{/each}
	</div>
</section>
