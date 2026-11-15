<script lang="ts">
	import { GraduationCap, X } from '@lucide/svelte';
	import { Dialog } from 'bits-ui';
	import { groupSkillsByCategory, skillLabel, skillProgressPercent } from './skills';
	import type { GameSession } from './session.svelte';

	let { session }: { session: GameSession } = $props();

	const groups = $derived(groupSkillsByCategory(session.skills));
</script>

<Dialog.Root open={session.showSkills} onOpenChange={(open) => (session.showSkills = open)}>
	<Dialog.Portal>
		<Dialog.Overlay class="game-dialog-backdrop" />
		<Dialog.Content class="game-dialog" aria-label="Skills">
			<header class="game-dialog-header">
				<Dialog.Title class="game-panel-header">
					<GraduationCap size={18} aria-hidden="true" />
					Skills
				</Dialog.Title>
				<Dialog.Close class="btn preset-tonal-surface px-2 py-1 text-xs" aria-label="Close skills">
					<X size={14} aria-hidden="true" />
				</Dialog.Close>
			</header>
			<Dialog.Description class="text-xs text-surface-600-400">
				Skills improve as you use them. Each rank costs the same experience.
			</Dialog.Description>

			{#if groups.length === 0}
				<p class="mt-3 text-sm text-surface-600-400">No skills practised yet.</p>
			{:else}
				<div class="mt-3 space-y-3">
					{#each groups as group (group.category)}
						<section>
							<h3 class="text-xs font-semibold tracking-wide text-surface-600-400 uppercase">
								{group.label}
							</h3>
							<ul class="mt-1.5 space-y-2">
								{#each group.skills as skill (skill.skillId)}
									<li class="rounded border border-surface-800 p-2 text-sm">
										<div class="flex items-center justify-between gap-2">
											<span class="font-semibold">{skillLabel(skill)}</span>
											<span class="text-xs text-surface-600-400">
												Rank {skill.rank}{skill.maxRank > 0 ? ` / ${skill.maxRank}` : ''}
											</span>
										</div>

										{#if skill.resolved}
											<div class="mt-1.5 h-1.5 overflow-hidden rounded bg-surface-800">
												<div
													class="h-full bg-primary-500 transition-all"
													style:width={`${skillProgressPercent(skill)}%`}
												></div>
											</div>
											<p class="mt-1 text-xs text-surface-600-400">
												{skill.xpIntoRank} / {skill.xpToNextRank} XP to the next rank · {skill.totalXp}
												total
											</p>
											{#if skill.governingAttribute}
												<p class="text-xs text-surface-600-400">
													Governed by {skill.governingAttribute}
												</p>
											{/if}
										{:else}
											<p class="mt-1 text-xs text-warning-500">
												The catalog has no entry for this skill.
											</p>
											<p class="text-xs text-surface-600-400">
												{skill.totalXp} XP recorded
											</p>
										{/if}
									</li>
								{/each}
							</ul>
						</section>
					{/each}
				</div>
			{/if}
		</Dialog.Content>
	</Dialog.Portal>
</Dialog.Root>
