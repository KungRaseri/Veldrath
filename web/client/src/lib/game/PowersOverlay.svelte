<script lang="ts">
	import { Sparkles, X } from '@lucide/svelte';
	import { Dialog } from 'bits-ui';
	import { outOfCombatUsageReason, powerFacts, powerLabel, powerSummary } from './powers';
	import type { GameSession } from './session.svelte';

	let { session }: { session: GameSession } = $props();

	const powers = $derived(session.powers);
	const context = $derived(session.powerContext);
</script>

<Dialog.Root open={session.showAbilities} onOpenChange={(open) => (session.showAbilities = open)}>
	<Dialog.Portal>
		<Dialog.Overlay class="game-dialog-backdrop" />
		<Dialog.Content class="game-dialog" aria-label="Abilities">
			<header class="game-dialog-header">
				<Dialog.Title class="game-panel-header">
					<Sparkles size={18} aria-hidden="true" />
					Abilities
				</Dialog.Title>
				<Dialog.Close
					class="btn preset-tonal-surface px-2 py-1 text-xs"
					aria-label="Close abilities"
				>
					<X size={14} aria-hidden="true" />
				</Dialog.Close>
			</header>
			<Dialog.Description class="text-xs text-surface-600-400">
				Learned abilities, with what each one costs. Using one outside combat spends mana.
			</Dialog.Description>

			{#if powers.length === 0}
				<p class="mt-3 text-sm text-surface-600-400">No abilities learned yet.</p>
			{:else}
				<ul class="mt-3 space-y-2">
					{#each powers as power (power.slug)}
						{@const reason = outOfCombatUsageReason(power, context)}
						{@const facts = powerFacts(power)}
						<li class="rounded border border-surface-800 p-2 text-sm">
							<div class="flex items-center justify-between gap-2">
								<span class="font-semibold">{powerLabel(power)}</span>
								<button
									class="btn preset-tonal-surface px-2 py-1 text-xs"
									type="button"
									disabled={reason !== null}
									title={reason ?? powerSummary(power)}
									aria-label={`Use ${powerLabel(power)}`}
									onclick={() => session.useAbility(power.slug)}
								>
									<Sparkles size={12} aria-hidden="true" />
									Use
								</button>
							</div>

							{#if power.description}
								<p class="mt-1 text-xs text-surface-600-400">{power.description}</p>
							{/if}

							{#if facts.length > 0}
								<dl class="mt-1.5 flex flex-wrap gap-x-3 gap-y-1 text-xs">
									{#each facts as fact (fact.label)}
										<div class="inline-flex gap-1">
											<dt class="text-surface-600-400">{fact.label}</dt>
											<dd>{fact.value}</dd>
										</div>
									{/each}
								</dl>
							{/if}

							{#if reason}
								<p class="mt-1 text-xs text-warning-500">{reason}</p>
							{/if}
						</li>
					{/each}
				</ul>
			{/if}
		</Dialog.Content>
	</Dialog.Portal>
</Dialog.Root>
