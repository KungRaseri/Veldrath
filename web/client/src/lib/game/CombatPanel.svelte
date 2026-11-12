<script lang="ts">
	import {
		Check,
		Droplets,
		Footprints,
		HeartPulse,
		Shield,
		Sparkles,
		Swords,
		Users
	} from '@lucide/svelte';
	import { barPercent } from './reducers';
	import { combatUsageReason, powerLabel, powerSummary } from './powers';
	import type { GameSession } from './session.svelte';

	let { session }: { session: GameSession } = $props();

	/** True while a turn is in flight, so the panel cannot submit two in a row. */
	let waiting = $state(false);

	const powers = $derived(session.powers);
	const context = $derived(session.powerContext);
	const enemy = $derived(session.combat.enemy);
	const enemyAbilities = $derived(enemy?.abilities ?? []);
	const participants = $derived(session.combat.participants ?? []);
	const outcome = $derived(session.combat.outcome ?? null);
	const myCharacterId = $derived(session.character?.id ?? null);

	async function act(run: () => Promise<void>): Promise<void> {
		if (waiting || !session.combat.active) return;
		waiting = true;
		try {
			await run();
		} finally {
			waiting = false;
		}
	}

	function participantName(name: string | null | undefined, characterId: string): string {
		const trimmed = name?.trim();
		if (trimmed && trimmed.length > 0) return trimmed;
		return characterId === myCharacterId ? 'You' : 'Unknown fighter';
	}

	function outcomeTitle(kind: 'victory' | 'defeat' | 'fled'): string {
		if (kind === 'victory') return 'Victory';
		if (kind === 'fled') return 'You escaped';
		return 'Defeated';
	}
</script>

<section class="game-panel space-y-4 p-4">
	<header class="flex flex-wrap items-center justify-between gap-3">
		<div>
			<h2 class="game-panel-header">
				<Swords size={19} aria-hidden="true" />
				Combat{#if enemy}
					· {enemy.name}
				{/if}
			</h2>
			{#if enemy}
				<p class="text-sm text-surface-600-400">Level {enemy.level}</p>
			{/if}
		</div>
		<div class="flex items-center gap-2">
			<span class="text-xs text-surface-600-400">
				<Droplets size={12} aria-hidden="true" class="inline" />
				{session.stats.currentMana} / {session.stats.maxMana} mana
			</span>
			{#if waiting}
				<span class="chip preset-filled-secondary-500 text-xs" role="status"
					>Waiting for the server…</span
				>
			{:else}
				<span class="chip preset-filled-error-500 text-xs">Your turn</span>
			{/if}
		</div>
	</header>

	{#if enemy}
		<div class="space-y-1">
			<div class="flex items-center justify-between text-xs text-surface-600-400">
				<span class="inline-flex items-center gap-1">
					<HeartPulse size={12} aria-hidden="true" />
					Enemy health
				</span>
				<span>{enemy.currentHealth} / {enemy.maxHealth}</span>
			</div>
			<div class="h-3 overflow-hidden rounded bg-surface-800">
				<div
					class="h-full bg-error-500 transition-all"
					style:width={`${barPercent(enemy.currentHealth, enemy.maxHealth)}%`}
				></div>
			</div>
			{#if enemyAbilities.length > 0}
				<p class="text-xs text-surface-600-400">Can use: {enemyAbilities.join(', ')}</p>
			{/if}
		</div>
	{/if}

	{#if participants.length > 0}
		<div>
			<h3 class="text-xs font-semibold tracking-wide text-surface-600-400 uppercase">
				<Users size={12} aria-hidden="true" class="inline" />
				Fighting
			</h3>
			<ul class="mt-2 space-y-1.5">
				{#each participants as participant (participant.characterId)}
					<li class="rounded border border-surface-800 p-2 text-sm">
						<div class="flex items-center justify-between gap-2">
							<span class="font-semibold">
								{participantName(participant.name, participant.characterId)}
								{#if participant.characterId === myCharacterId}
									<span class="text-xs text-surface-600-400">(you)</span>
								{/if}
							</span>
							<span class="text-xs text-surface-600-400">
								{participant.currentHealth} / {participant.maxHealth} HP · {participant.contributionPercent}%
							</span>
						</div>
						<div class="mt-1 h-1.5 overflow-hidden rounded bg-surface-800">
							<div
								class="h-full bg-success-500 transition-all"
								style:width={`${barPercent(participant.currentHealth, participant.maxHealth)}%`}
							></div>
						</div>
					</li>
				{/each}
			</ul>
		</div>
	{/if}

	<div class="flex flex-wrap gap-2">
		<button
			class="btn preset-filled-error-500"
			type="button"
			onclick={() => act(() => session.attack())}
			disabled={!session.combat.active || waiting}
			title={session.combat.active ? 'Attack with your equipped weapon.' : 'Not in combat.'}
		>
			<Swords size={15} aria-hidden="true" />
			Attack
		</button>
		<button
			class="btn preset-tonal-surface"
			type="button"
			onclick={() => act(() => session.defend())}
			disabled={!session.combat.active || waiting}
			title={session.combat.active ? 'Reduce the next hit.' : 'Not in combat.'}
		>
			<Shield size={15} aria-hidden="true" />
			Defend
		</button>
		<button
			class="btn preset-tonal-surface"
			type="button"
			onclick={() => act(() => session.flee())}
			disabled={!session.combat.active || waiting}
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
							disabled={reason !== null || waiting}
							title={reason ?? `Use ${powerLabel(power)} this turn.`}
							aria-label={`Use ${powerLabel(power)}`}
							onclick={() => act(() => session.useCombatAbility(power.slug))}
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

	{#if outcome}
		<div class="rounded border border-surface-800 p-3" role="status">
			<p class="text-sm font-semibold">{outcomeTitle(outcome.kind)}</p>
			{#if outcome.kind === 'victory'}
				<p class="text-xs text-surface-600-400">
					+{outcome.xpEarned} XP · +{outcome.goldEarned} gold
				</p>
			{:else if outcome.kind === 'defeat'}
				<p class="text-xs text-surface-600-400">
					-{outcome.xpLost} XP · -{outcome.goldLost} gold
					{#if outcome.hardcore}
						· this character did not survive
					{/if}
				</p>
			{/if}
		</div>
	{/if}

	<div class="max-h-64 space-y-1 overflow-y-auto rounded bg-surface-950/60 p-3 text-sm">
		{#each session.combat.log as line, index (index)}
			<p>{line}</p>
		{:else}
			<p class="text-surface-600-400">No combat yet.</p>
		{/each}
	</div>

	{#if session.combat.log.length > 0}
		<p class="text-xs text-surface-600-400">
			<Check size={12} aria-hidden="true" class="inline" />
			Every exchange of this fight is listed above.
		</p>
	{/if}
</section>
