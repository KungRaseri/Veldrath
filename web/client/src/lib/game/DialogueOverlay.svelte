<script lang="ts">
	import { Dialog } from 'bits-ui';
	import { MessageCircle, X } from '@lucide/svelte';
	import {
		blockedLine,
		consequenceLine,
		emptyConversationLine,
		optionClickable,
		speakerSubtitle
	} from './dialogue';
	import type { GameSession } from './session.svelte';

	let { session }: { session: GameSession } = $props();

	const dialogue = $derived(session.dialogue);
	const subtitle = $derived(dialogue ? speakerSubtitle(dialogue) : '');
	const emptyLine = $derived(dialogue ? emptyConversationLine(dialogue) : '');

	function setOpen(open: boolean): void {
		if (!open) session.closeDialogue();
	}
</script>

<Dialog.Root open={dialogue !== null} onOpenChange={setOpen}>
	<Dialog.Portal>
		<Dialog.Overlay class="game-dialog-backdrop" />
		<Dialog.Content class="game-dialog" aria-label="Conversation">
			{#if dialogue}
				<header class="game-dialog-header">
					<Dialog.Title class="game-panel-header">
						<MessageCircle size={18} aria-hidden="true" />
						{dialogue.displayName}
					</Dialog.Title>
					<button
						class="btn preset-tonal-surface px-2 py-1 text-xs"
						type="button"
						aria-label="Close conversation"
						onclick={() => session.closeDialogue()}
					>
						<X size={14} aria-hidden="true" />
					</button>
				</header>

				{#if subtitle}
					<Dialog.Description class="text-xs text-surface-600-400">
						{subtitle}
					</Dialog.Description>
				{:else}
					<Dialog.Description class="sr-only">
						A conversation with {dialogue.displayName}.
					</Dialog.Description>
				{/if}

				<p class="mt-3 border-l-2 border-surface-800 pl-3 text-sm italic">
					“{dialogue.prompt}”
				</p>

				{#if emptyLine}
					<p class="mt-3 text-sm text-surface-600-400">{emptyLine}</p>
				{:else}
					<ul class="mt-3 space-y-2">
						{#each dialogue.options as option (option.key)}
							{@const blocked = blockedLine(option)}
							{@const consequences = consequenceLine(option)}
							<li class="rounded border border-surface-800 p-2 text-sm">
								<div class="flex items-start justify-between gap-3">
									<div class="min-w-0">
										<p>{option.label}</p>
										{#if consequences}
											<p class="text-xs text-surface-600-400">{consequences}</p>
										{/if}
										{#if blocked}
											<p class="text-xs text-error-500">{blocked}</p>
										{/if}
									</div>
									<button
										class="btn preset-tonal-surface px-2 py-1 text-xs"
										type="button"
										disabled={!optionClickable(option, session.dialogueBusy)}
										title={blocked ?? `Say: ${option.label}`}
										aria-label={`Say ${option.label}`}
										onclick={() => session.chooseNpcOption(option.key)}
									>
										{blocked ? 'Unavailable' : 'Say it'}
									</button>
								</div>
							</li>
						{/each}
					</ul>
				{/if}
			{/if}
		</Dialog.Content>
	</Dialog.Portal>
</Dialog.Root>
