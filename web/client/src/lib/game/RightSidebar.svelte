<script lang="ts">
	import { MessageSquare, Send } from '@lucide/svelte';
	import type { ChatChannel } from './session.svelte';
	import type { GameSession } from './session.svelte';

	let { session }: { session: GameSession } = $props();

	let draft = $state('');
	let messagesElement = $state<HTMLDivElement | null>(null);

	const channels: ChatChannel[] = ['Zone', 'Global', 'Whisper'];

	function messageClass(channel: string): string {
		switch (channel.toLowerCase()) {
			case 'system':
				return 'game-chat-message-system';
			case 'whisper':
				return 'game-chat-message-whisper';
			case 'global':
				return 'game-chat-message-global';
			default:
				return '';
		}
	}

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

<aside class="game-panel flex min-h-0 flex-col p-3">
	<h2 class="game-panel-header mb-2">
		<MessageSquare size={17} aria-hidden="true" />
		Chat
	</h2>

	<div class="mb-2 flex items-center gap-1">
		{#each channels as channel (channel)}
			<button
				class="rounded px-2 py-1 text-xs transition {session.chatChannel === channel
					? 'bg-primary-500 text-primary-50'
					: 'text-surface-600-400 hover:bg-surface-800 hover:text-surface-100'}"
				type="button"
				onclick={() => (session.chatChannel = channel)}
			>
				{channel}
			</button>
		{/each}
	</div>

	<div
		bind:this={messagesElement}
		class="game-chat-log min-h-48 flex-1 space-y-1 overflow-y-auto rounded bg-surface-950/60 p-2 text-sm"
		aria-live="polite"
	>
		{#each session.messagesForChannel as entry, index (index)}
			<p class="game-chat-message {messageClass(entry.channel)} break-words">
				<span class="game-chat-channel">[{entry.channel}]</span>
				<span class="game-chat-sender">{entry.sender}:</span>
				<span class="game-chat-text">{entry.message}</span>
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
		<button class="btn preset-filled-primary-500" type="submit">
			<Send size={15} aria-hidden="true" />
			Send
		</button>
	</form>
</aside>
