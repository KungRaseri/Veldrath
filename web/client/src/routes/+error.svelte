<script lang="ts">
	import { page } from '$app/state';
	import { ArrowLeft, Compass, TriangleAlert } from '@lucide/svelte';

	/**
	 * Root error boundary.
	 *
	 * Without this file SvelteKit renders its own unstyled error page, which ignores the
	 * Veldrath design system and leaks the request path in a way that looks broken rather than
	 * deliberate. A 404 is a normal navigation outcome and gets friendlier copy than a 5xx.
	 */
	const isNotFound = $derived(page.status === 404);

	const heading = $derived(isNotFound ? 'Lost in the mists' : 'Something went wrong');
	const detail = $derived(
		isNotFound
			? 'That page is not part of Veldrath — the road you followed has faded.'
			: (page.error?.message ?? 'The server could not finish that request. Try again shortly.')
	);
</script>

<svelte:head>
	<title>{isNotFound ? 'Not found' : 'Error'} — Veldrath</title>
	<meta name="robots" content="noindex" />
</svelte:head>

<div class="mx-auto max-w-3xl space-y-6 p-6">
	<header class="space-y-2">
		<p class="text-xs tracking-[0.2em] text-surface-600-400 uppercase">
			Error {page.status}
		</p>
		<h1 class="text-title-large flex items-center gap-3">
			{#if isNotFound}
				<Compass class="text-secondary-500" size={28} aria-hidden="true" />
			{:else}
				<TriangleAlert class="text-error-500" size={28} aria-hidden="true" />
			{/if}
			{heading}
		</h1>
		<p class="text-surface-600-400">{detail}</p>
	</header>

	<div class="flex flex-wrap items-center gap-3 card p-5">
		<a class="btn preset-filled-primary-500" href="/">
			<ArrowLeft size={16} aria-hidden="true" />
			Back to the gate
		</a>
		<a class="btn preset-tonal-surface" href="/characters">Your characters</a>
		{#if !isNotFound}
			<a class="btn preset-tonal-surface" href={page.url.pathname} data-sveltekit-reload>
				Try again
			</a>
		{/if}
	</div>
</div>
