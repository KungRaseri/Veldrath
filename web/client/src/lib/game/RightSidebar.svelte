<script lang="ts">
	import type { ChatChannel } from './session.svelte';
	import type { GameSession } from './session.svelte';

	let { session }: { session: GameSession } = $props();

	let draft = $state('');
	let messagesElement = $state<HTMLDivElement | null>(null);

	const channels: ChatChannel[] = ['Zone', 'Global', 'Whisper'];

	async function submit(event: SubmitEvent) {
		event.preventDefault();
		const text = draft;
		draft = '';
		await session.sendChat(text);
		queueMicrotask(() => {
			if (messagesElement) messagesElement.scrollTop = messagesElement.scrollHeight;
		});
	}
</script>

<aside class="flex min-h-0 flex-col card p-3">
	<div class="mb-2 flex items-center gap-1">
		{#each channels as channel (channel)}
			<button
				class="rounded px-2 py-1 text-xs {session.chatChannel === channel
					? 'bg-primary-500 text-white'
					: 'text-surface-600-400 hover:bg-surface-200-800'}"
				onclick={() => (session.chatChannel = channel)}
			>
				{channel}
			</button>
		{/each}
	</div>

	<div
		bind:this={messagesElement}
		class="min-h-48 flex-1 space-y-1 overflow-y-auto rounded bg-surface-50-950/50 p-2 text-sm"
		aria-live="polite"
	>
		{#each session.messagesForChannel as entry, index (index)}
			<p class="break-words">
				<span
					class="text-xs font-semibold {entry.channel.toLowerCase() === 'system'
						? 'text-warning-500'
						: entry.channel.toLowerCase() === 'whisper'
							? 'text-secondary-500'
							: 'text-primary-500'}"
				>
					[{entry.channel}] {entry.sender}
				</span>
				{entry.message}
			</p>
		{:else}
			<p class="text-xs text-surface-600-400">No messages yet.</p>
		{/each}
	</div>

	{#if session.chatChannel === 'Whisper'}
		<label class="label mt-2">
			<span>Whisper target</span>
			<input class="input" bind:value={session.whisperTarget} placeholder="Character name" />
		</label>
	{/if}

	<form class="mt-2 flex gap-2" onsubmit={submit}>
		<input
			class="input flex-1"
			bind:value={draft}
			placeholder={session.chatChannel === 'Whisper'
				? 'Whisper message…'
				: `Message ${session.chatChannel.toLowerCase()}…`}
			maxlength="500"
			autocomplete="off"
		/>
		<button class="btn preset-filled-primary-500" type="submit">Send</button>
	</form>
</aside>
