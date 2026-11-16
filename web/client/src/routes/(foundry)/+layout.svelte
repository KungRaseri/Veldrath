<script lang="ts">
	import type { Snippet } from 'svelte';
	import FoundrySidebar from '#lib/components/foundry/FoundrySidebar.svelte';
	import { foundryNav } from '#lib/foundry/nav';
	import type { LayoutData } from './$types';

	/**
	 * The portal shell.
	 *
	 * This file owns the frame: one content width (`max-w-6xl`), one padding scale, and the navigation. It
	 * used to be a bare horizontal nav across the top while each page chose its own width — `3xl`, `4xl`,
	 * `5xl` and `6xl` all appeared somewhere, so the portal jittered as you moved between pages.
	 */
	let { data, children }: { data: LayoutData; children: Snippet } = $props();

	const nav = $derived(
		foundryNav({
			canViewContent: data.canViewContent,
			canManageContent: data.canManageContent,
			canReview: data.canReview,
			unreadNotifications: data.unreadNotifications
		})
	);
</script>

<div class="mx-auto grid max-w-6xl gap-6 px-6 py-6 lg:grid-cols-[13rem_minmax(0,1fr)]">
	<FoundrySidebar {nav} />
	<main class="min-w-0 space-y-6">
		{@render children()}
	</main>
</div>
