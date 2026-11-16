<script lang="ts">
	import { page } from '$app/state';
	import { Compass, TriangleAlert } from '@lucide/svelte';
	import FoundryPageHeader from '#lib/components/foundry/FoundryPageHeader.svelte';
	import { FOUNDRY_HOME_HREF } from '#lib/foundry/nav';

	/**
	 * The portal's own error boundary.
	 *
	 * Without it a 403 inside Foundry rendered the public error page, which dropped the reader out of the
	 * portal entirely and said nothing about *why* — the usual cause being a missing capability, which the
	 * error message already names. The layout still renders around this, so the navigation survives and the
	 * reader can move sideways instead of starting over.
	 */
	const forbidden = $derived(page.status === 403);
	const notFound = $derived(page.status === 404);

	const title = $derived(
		forbidden ? 'Not yours to open' : notFound ? 'No such page' : 'The portal could not finish that'
	);

	const detail = $derived(
		page.error?.message ??
			(forbidden
				? 'This account lacks the capability the page needs.'
				: notFound
					? 'That page is not part of Foundry.'
					: 'The server could not finish the request. Try again shortly.')
	);
</script>

<svelte:head>
	<title>{forbidden ? 'Forbidden' : notFound ? 'Not found' : 'Error'} — Foundry</title>
	<meta name="robots" content="noindex" />
</svelte:head>

<FoundryPageHeader {title} description={detail} />

<p class="text-xs tracking-[0.2em] text-surface-600-400 uppercase">Error {page.status}</p>

<div class="game-panel flex flex-wrap items-center gap-3 p-5">
	{#if forbidden}
		<TriangleAlert class="text-warning-500" size={22} aria-hidden="true" />
	{:else}
		<Compass class="text-secondary-500" size={22} aria-hidden="true" />
	{/if}
	<a class="btn preset-tonal-surface btn-sm" href={FOUNDRY_HOME_HREF}>Back to Foundry</a>
	{#if !notFound && !forbidden}
		<a class="btn preset-tonal-surface btn-sm" href={page.url.pathname} data-sveltekit-reload>
			Try again
		</a>
	{/if}
</div>
