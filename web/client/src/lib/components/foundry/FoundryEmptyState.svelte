<script lang="ts">
	import type { Snippet } from 'svelte';
	import { Inbox } from '@lucide/svelte';

	/**
	 * What a section shows when it has nothing to show.
	 *
	 * An empty table and a failed request looked identical before, so a curator could not tell "nobody has
	 * submitted anything" from "the list did not load". Callers render an error message *instead* of this.
	 */
	let {
		message,
		action
	}: {
		/** One sentence, phrased as a fact rather than an apology. */
		message: string;
		/** Optional next step, e.g. a "New" link. */
		action?: Snippet;
	} = $props();
</script>

<div class="game-panel flex flex-col items-center gap-3 p-8 text-center">
	<Inbox class="text-surface-500" size={22} aria-hidden="true" />
	<p class="max-w-prose text-sm text-surface-600-400">{message}</p>
	{#if action}
		{@render action()}
	{/if}
</div>
