<script lang="ts">
	import { Droplets, Footprints, HeartPulse, Shield, Sparkles, Swords } from '@lucide/svelte';
	import { barPercent } from './reducers';
	import { combatUsageReason, powerLabel, powerSummary } from './powers';
	import type { GameSession } from './session.svelte';

	let { session }: { session: GameSession } = $props();

	const powers = $derived(session.powers);
	const context = $derived(session.powerContext);
	const enemyAbilities = $derived(session.combat.enemy?.abilities ?? []);
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
		<div class="flex items-center gap-2">
			<span class="text-xs text-surface-600-400">
				<Droplets size={12} aria-hidden="true" class="inline" />
				{session.stats.currentMana} / {session.stats.maxMana} mana
			</span>
			<span class="chip preset-filled-error-500 text-xs">Turn-based</span>
		</div>
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
			{#if enemyAbilities.length > 0}
				<p class="text-xs text-surface-600-400">
					Can use: {enemyAbilities.join(', ')}
				</p>
			{/if}
		</div>
	{/if}

	<div class="flex flex-wrap gap-2">
		<button
			class="btn preset-filled-error-500"
			type="button"
			onclick={() => session.attack()}
			disabled={!session.combat.active}
			title={session.combat.active ? 'Attack with your equipped weapon.' : 'Not in combat.'}
		>
			<Swords size={15} aria-hidden="true" />
			Attack
		</button>
		<button
			class="btn preset-tonal-surface"
			type="button"
			onclick={() => session.defend()}
			disabled={!session.combat.active}
			title={session.combat.active ? 'Reduce the next hit.' : 'Not in combat.'}
		>
			<Shield size={15} aria-hidden="true" />
			Defend
		</button>
		<button
			class="btn preset-tonal-surface"
			type="button"
			onclick={() => session.flee()}
			disabled={!session.combat.active}
			title={session.combat.active ? 'Try to leave the fight.' : 'Not in combat.'}
		>
			<Footprints size={15} aria-hidden="true" />
			Flee
		</button>
	</div>

	{#if powers.length === 0}
		<p class="text-xs text-surface-600-400">No abilities learned yet.</p>
	{:else}
		<div>
			<h3 class="text-xs font-semibold tracking-wide text-surface-600-400 uppercase">Abilities</h3>
			<ul class="mt-2 space-y-2">
				{#each powers as power (power.slug)}
					{@const reason = combatUsageReason(power, context)}
					{@const summary = powerSummary(power)}
					<li class="flex flex-wrap items-center justify-between gap-2">
						<div>
							<p class="text-sm font-semibold">{powerLabel(power)}</p>
							{#if summary}
								<p class="text-xs text-surface-600-400">{summary}</p>
							{/if}
						</div>
						<button
							class="btn preset-tonal-surface text-xs"
							type="button"
							disabled={reason !== null}
							title={reason ?? `Use ${powerLabel(power)} this turn.`}
							aria-label={`Use ${powerLabel(power)}`}
							onclick={() => session.useCombatAbility(power.slug)}
						>
							<Sparkles size={13} aria-hidden="true" />
							Use
						</button>
					</li>
					{#if reason}
						<p class="text-xs text-warning-500">{reason}</p>
					{/if}
				{/each}
			</ul>
		</div>
	{/if}

	<div class="max-h-56 space-y-1 overflow-y-auto rounded bg-surface-950/60 p-3 text-sm">
		{#each session.combat.log.slice(-12) as line, index (index)}
			<p>{line}</p>
		{:else}
			<p class="text-surface-600-400">No combat yet.</p>
		{/each}
	</div>
</section>
