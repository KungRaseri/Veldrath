<script lang="ts">
	import type { Snippet } from 'svelte';

	/**
	 * The one page header every Foundry page renders.
	 *
	 * Pages used to hand-roll a `<header>` with their own heading level, their own description paragraph
	 * and their own back-link placement, which is why the portal had four different header layouts. The
	 * layout owns the frame; this owns the title block.
	 */
	let {
		title,
		description = '',
		actions
	}: {
		/** Page title, rendered as the single `<h1>` of the page. */
		title: string;
		/** One sentence explaining what the page is for. */
		description?: string;
		/** Buttons aligned to the end of the title row. */
		actions?: Snippet;
	} = $props();
</script>

<header class="flex flex-wrap items-start justify-between gap-3">
	<div class="space-y-1">
		<h1 class="text-title-large">{title}</h1>
		{#if description}
			<p class="max-w-prose text-sm text-surface-600-400">{description}</p>
		{/if}
	</div>
	{#if actions}
		<div class="flex flex-wrap items-center gap-2">
			{@render actions()}
		</div>
	{/if}
</header>
