<script lang="ts">
	import { resolve } from '$app/paths';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>Moderation — Veldrath</title>
</svelte:head>

<div class="mx-auto max-w-4xl space-y-4 p-6">
	<header class="space-y-1">
		<h1 class="h1">Moderation</h1>
		<p class="text-surface-600-400">
			Search player accounts and handle open reports. Requires the players.view permission.
		</p>
	</header>

	{#each data.errors as error, index (index)}
		<p class="text-sm text-error-500">{error}</p>
	{/each}

	<div class="grid gap-3 sm:grid-cols-2">
		<a class="space-y-1 card p-4" href={resolve('/(mod)/mod/users')}>
			<span class="text-3xl font-semibold text-primary-500">{data.userCount}</span>
			<h2 class="h3">Player accounts</h2>
			<p class="text-sm text-surface-600-400">Search accounts, view status, warn, mute, or kick.</p>
		</a>
		<a class="space-y-1 card p-4" href={resolve('/(mod)/mod/reports')}>
			<span class="text-3xl font-semibold text-primary-500">{data.openReports}</span>
			<h2 class="h3">Open reports</h2>
			<p class="text-sm text-surface-600-400">Review player-submitted behaviour reports.</p>
		</a>
	</div>
</div>
