<script lang="ts">
	import type { Snippet } from 'svelte';
	import { page } from '$app/state';
	import {
		BookOpenText,
		Inbox,
		Languages as LanguagesIcon,
		Map as MapIcon,
		Send
	} from '@lucide/svelte';
	import { foundryNavItems, isCurrentNavItem } from '#lib/foundry/nav';
	import type { LayoutData } from './$types';

	let { data, children }: { data: LayoutData; children: Snippet } = $props();

	const items = $derived(foundryNavItems(data));

	/** Icons live here rather than in the pure module, so `nav.ts` stays component-free and testable. */
	const icons: Record<string, typeof MapIcon> = {
		'/content': BookOpenText,
		'/maps': MapIcon,
		'/submissions': Send,
		'/languages': LanguagesIcon,
		'/notifications': Inbox
	};
</script>

<div class="mx-auto max-w-5xl px-6 pt-6">
	<nav aria-label="Foundry sections" class="flex flex-wrap items-center gap-2">
		{#each items as item (item.href)}
			{@const Icon = icons[item.href] ?? BookOpenText}
			<a
				class="btn preset-tonal-surface btn-sm"
				href={item.href}
				aria-current={isCurrentNavItem(item.href, page.url.pathname) ? 'page' : undefined}
			>
				<Icon size={15} aria-hidden="true" />
				{item.label}
			</a>
		{/each}
	</nav>
</div>

{@render children()}
