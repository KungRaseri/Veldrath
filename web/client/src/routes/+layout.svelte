<script lang="ts">
	import type { Snippet } from 'svelte';
	import { page } from '$app/state';
	import SiteFooter from '#lib/components/site/SiteFooter.svelte';
	import SiteHeader from '#lib/components/site/SiteHeader.svelte';
	import type { LayoutData } from './$types';
	import './layout.css';

	let { data, children }: { data: LayoutData; children: Snippet } = $props();

	// The desktop SPA and the in-game shell render their own full-screen chrome.
	const isFullScreen = $derived(
		page.url.pathname.startsWith('/desktop') || page.url.pathname.startsWith('/play/')
	);
</script>

{#if !isFullScreen}
	<div class="flex min-h-screen flex-col">
		<SiteHeader user={data?.user ?? null} staff={data?.staff ?? null} />
		<main class="flex-1">{@render children()}</main>
		<SiteFooter user={data?.user ?? null} />
	</div>
{:else}
	{@render children()}
{/if}
