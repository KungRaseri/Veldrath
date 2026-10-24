<script lang="ts">
	import { Footprints, HeartPulse, Shield, Sparkles, Swords } from '@lucide/svelte';
	import { barPercent } from './reducers';
	import type { GameSession } from './session.svelte';

	let { session }: { session: GameSession } = $props();

	const abilities = $derived((session.character?.learnedAbilities ?? []).slice(0, 6));
</script>

<section class="game-panel space-y-4 p-4">
	<header class="flex flex-wrap items-center justify-between gap-3">
		<div>
			<h2 class="game-panel-header">
				<Swords size={19} aria-hidden="true" />
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
			<div class="flex items-center justify-between text-xs text-surface-600-400">
				<span class="inline-flex items-center gap-1">
					<HeartPulse size={12} aria-hidden="true" />
					Enemy health
				</span>
				<span>
					{session.combat.enemy.currentHealth} / {session.combat.enemy.maxHealth}
				</span>
			</div>
			<div class="h-3 overflow-hidden rounded bg-surface-800">
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
			type="button"
			onclick={() => session.attack()}
			disabled={!session.combat.active}
		>
			<Swords size={15} aria-hidden="true" />
			Attack
		</button>
		<button
			class="btn preset-tonal-surface"
			type="button"
			onclick={() => session.defend()}
			disabled={!session.combat.active}
		>
			<Shield size={15} aria-hidden="true" />
			Defend
		</button>
		<button
			class="btn preset-tonal-surface"
			type="button"
			onclick={() => session.flee()}
			disabled={!session.combat.active}
		>
			<Footprints size={15} aria-hidden="true" />
			Flee
		</button>
		{#each abilities as ability (ability)}
			<button
				class="btn preset-tonal-surface text-xs"
				type="button"
				onclick={() => session.useCombatAbility(ability)}
				disabled={!session.combat.active}
			>
				<Sparkles size={13} aria-hidden="true" />
				{ability}
			</button>
		{/each}
	</div>

	<div class="max-h-56 space-y-1 overflow-y-auto rounded bg-surface-950/60 p-3 text-sm">
		{#each session.combat.log.slice(-12) as line, index (index)}
			<p>{line}</p>
		{:else}
			<p class="text-surface-600-400">No combat yet.</p>
		{/each}
	</div>
</section>
