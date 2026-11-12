<script lang="ts">
	import { Check, Hammer, X } from '@lucide/svelte';
	import { Dialog } from 'bits-ui';
	import {
		craftBlockedReason,
		materialLine,
		materialSatisfied,
		partitionRecipes,
		recipeFacts,
		recipeLabel
	} from './crafting';
	import type { GameSession } from './session.svelte';

	let { session }: { session: GameSession } = $props();

	/** The recipe slug whose craft is in flight, so one recipe cannot be submitted twice. */
	let busy = $state<string | null>(null);

	const groups = $derived(partitionRecipes(session.craftableRecipes));

	async function craft(slug: string): Promise<void> {
		if (busy) return;
		busy = slug;
		try {
			await session.craft(slug);
		} finally {
			busy = null;
		}
	}
</script>

<Dialog.Root open={session.showCrafting} onOpenChange={(open) => (session.showCrafting = open)}>
	<Dialog.Portal>
		<Dialog.Overlay class="game-dialog-backdrop" />
		<Dialog.Content class="game-dialog" aria-label="Crafting">
			<header class="game-dialog-header">
				<Dialog.Title class="game-panel-header">
					<Hammer size={18} aria-hidden="true" />
					Crafting
				</Dialog.Title>
				<Dialog.Close
					class="btn preset-tonal-surface px-2 py-1 text-xs"
					aria-label="Close crafting"
				>
					<X size={14} aria-hidden="true" />
				</Dialog.Close>
			</header>
			<Dialog.Description class="text-xs text-surface-600-400">
				Crafting consumes the materials listed and adds the result to your bag.
			</Dialog.Description>

			{#if session.craftableRecipes.length === 0}
				<p class="mt-3 text-sm text-surface-600-400">No recipes are known to you yet.</p>
			{:else}
				<div class="mt-3 space-y-3">
					{#each [{ title: 'Ready to craft', recipes: groups.craftable }, { title: 'Not yet', recipes: groups.locked }] as section (section.title)}
						{#if section.recipes.length > 0}
							<section>
								<h3 class="text-xs font-semibold tracking-wide text-surface-600-400 uppercase">
									{section.title}
								</h3>
								<ul class="mt-1.5 space-y-2">
									{#each section.recipes as recipe (recipe.slug)}
										{@const blocked = craftBlockedReason(recipe)}
										<li class="rounded border border-surface-800 p-2 text-sm">
											<div class="flex items-center justify-between gap-2">
												<span class="font-semibold">{recipeLabel(recipe)}</span>
												<button
													class="btn preset-tonal-surface px-2 py-1 text-xs"
													type="button"
													disabled={blocked !== null || busy !== null}
													title={blocked ?? `Craft ${recipeLabel(recipe)}.`}
													aria-label={`Craft ${recipeLabel(recipe)}`}
													onclick={() => craft(recipe.slug)}
												>
													<Hammer size={12} aria-hidden="true" />
													Craft
												</button>
											</div>

											{#if recipe.description}
												<p class="mt-1 text-xs text-surface-600-400">{recipe.description}</p>
											{/if}

											<dl class="mt-1.5 flex flex-wrap gap-x-3 gap-y-1 text-xs">
												{#each recipeFacts(recipe) as fact (fact.label)}
													<div class="inline-flex gap-1">
														<dt class="text-surface-600-400">{fact.label}</dt>
														<dd>{fact.value}</dd>
													</div>
												{/each}
											</dl>

											{#if recipe.materials.length > 0}
												<ul class="mt-1.5 space-y-0.5 text-xs">
													{#each recipe.materials as material (material.itemRef)}
														<li class="flex items-center justify-between gap-2">
															<span class="text-surface-600-400">{material.itemRef}</span>
															<span
																class={materialSatisfied(material)
																	? 'text-success-500'
																	: 'text-warning-500'}
															>
																{#if materialSatisfied(material)}
																	<Check size={11} aria-hidden="true" class="inline" />
																{/if}
																{materialLine(material)}
															</span>
														</li>
													{/each}
												</ul>
											{/if}

											{#if blocked}
												<p class="mt-1 text-xs text-warning-500">{blocked}</p>
											{/if}
										</li>
									{/each}
								</ul>
							</section>
						{/if}
					{/each}
				</div>
			{/if}
		</Dialog.Content>
	</Dialog.Portal>
</Dialog.Root>
