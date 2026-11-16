<script lang="ts">
	import { page } from '$app/state';
	import { BookOpenText, ClipboardCheck, House, Inbox, Map as MapIcon, Send } from '@lucide/svelte';
	import { isCurrentNavItem, type FoundryNav, type FoundryNavItem } from '#lib/foundry/nav';

	/**
	 * The portal's navigation.
	 *
	 * Vertical and sticky on wide screens, a wrapping row of chips below `lg` — the same entries either way,
	 * so there is one list to keep correct rather than a mobile copy and a desktop copy. Icons live here
	 * rather than in `nav.ts`, which stays component-free and unit tested.
	 */
	let {
		nav
	}: {
		/** The grouped navigation, already filtered for this account. */
		nav: FoundryNav;
	} = $props();

	const icons: Record<string, typeof House> = {
		'/foundry': House,
		'/content': BookOpenText,
		'/maps': MapIcon,
		'/review': ClipboardCheck,
		'/submissions': Send,
		'/notifications': Inbox
	};

	function current(href: string): 'page' | undefined {
		return isCurrentNavItem(href, page.url.pathname) ? 'page' : undefined;
	}
</script>

{#snippet entry(item: FoundryNavItem)}
	{@const Icon = icons[item.href] ?? BookOpenText}
	{@const active = current(item.href) === 'page'}
	<a
		class="btn justify-start btn-sm"
		class:preset-filled-primary-500={active}
		class:preset-tonal-surface={!active}
		href={item.href}
		aria-current={active ? 'page' : undefined}
	>
		<Icon size={15} aria-hidden="true" />
		<span class="flex-1 text-left">{item.label}</span>
		{#if item.count !== undefined}
			<span class="badge text-xs" class:preset-tonal-surface={!active}>
				{item.count}
			</span>
		{/if}
	</a>
{/snippet}

<nav
	aria-label="Foundry sections"
	class="flex flex-wrap items-center gap-1 lg:sticky lg:top-6 lg:block lg:self-start"
>
	{@render entry(nav.home)}

	{#each nav.groups as group (group.group)}
		<div class="lg:mt-5">
			<p class="hidden px-2 text-xs tracking-[0.15em] text-surface-600-400 uppercase lg:block">
				{group.group}
			</p>
			<ul class="flex flex-wrap gap-1 lg:mt-1 lg:block lg:space-y-0.5">
				{#each group.items as item (item.href)}
					<li>{@render entry(item)}</li>
				{/each}
			</ul>
		</div>
	{/each}
</nav>
