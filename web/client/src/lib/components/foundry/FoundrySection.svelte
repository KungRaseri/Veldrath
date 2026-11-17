<script lang="ts">
	import type { Snippet } from 'svelte';

	/**
	 * A titled card. Pages compose a handful of these instead of inventing their own panels, which is what
	 * makes the whole portal share one spacing scale.
	 */
	let {
		title,
		count,
		description = '',
		actions,
		children
	}: {
		/** Section heading. */
		title: string;
		/** Optional badge — a queue length, a row count. Rendered only when supplied. */
		count?: number;
		/** Optional sentence under the heading. */
		description?: string;
		/** Optional controls aligned to the end of the heading row. */
		actions?: Snippet;
		/** Section body. */
		children: Snippet;
	} = $props();
</script>

<section class="game-panel space-y-3 p-4">
	<header class="flex flex-wrap items-center justify-between gap-2">
		<h2
			class="flex items-center gap-2 text-sm font-semibold tracking-wide text-surface-600-400 uppercase"
		>
			{title}
			{#if count !== undefined}
				<span class="badge preset-tonal-surface text-xs">{count}</span>
			{/if}
		</h2>
		{#if actions}
			{@render actions()}
		{/if}
	</header>
	{#if description}
		<p class="text-sm text-surface-600-400">{description}</p>
	{/if}
	{@render children()}
</section>
