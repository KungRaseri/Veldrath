<script lang="ts">
	/**
	 * The trail back to the portal home.
	 *
	 * Every nested page (`/content/[type]`, `/maps/[mapId]`, …) used to offer a single "back to X" button,
	 * which is a one-step trail with no way to reach anything above it. This renders the whole path, with
	 * the last crumb as plain text because it is the current page.
	 */
	let {
		items
	}: {
		/** Ancestors first; the final entry is the current page. */
		items: { label: string; href?: string }[];
	} = $props();
</script>

<nav aria-label="Breadcrumb">
	<ol class="flex flex-wrap items-center gap-1 text-xs text-surface-600-400">
		<!-- Keyed by position: a trail is a fixed list, and labels may legitimately repeat or be empty. -->
		{#each items as crumb, index (index)}
			<li class="flex items-center gap-1">
				{#if crumb.href && index < items.length - 1}
					<a class="link" href={crumb.href}>{crumb.label}</a>
				{:else}
					<span class="text-surface-800-200" aria-current="page">{crumb.label}</span>
				{/if}
				{#if index < items.length - 1}
					<span aria-hidden="true">/</span>
				{/if}
			</li>
		{/each}
	</ol>
</nav>
