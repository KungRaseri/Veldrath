<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { onMount } from 'svelte';

	// The server load redirects to /characters; this fallback covers client-side navigation.
	onMount(() => {
		void goto(resolve('/characters'), { replaceState: true });
	});
</script>

<svelte:head>
	<title>Entering Veldrath…</title>
</svelte:head>

<p class="p-6 text-sm text-surface-600-400">Redirecting to character selection…</p>
