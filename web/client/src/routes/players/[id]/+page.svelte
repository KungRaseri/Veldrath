<script lang="ts">
	import { resolve } from '$app/paths';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>{data.profile.username} — Veldrath</title>
</svelte:head>

<div class="mx-auto max-w-3xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={resolve('/community')}>← Community</a>

	<section class="space-y-3 card p-6">
		<header class="space-y-1">
			<h1 class="h1">{data.profile.username}</h1>
			<p class="text-surface-600-400">
				Level {data.profile.level}
				{#if data.profile.characterClass}· {data.profile.characterClass}{/if}
				{#if data.profile.species}· {data.profile.species}{/if}
			</p>
		</header>

		<dl class="grid gap-2 text-sm sm:grid-cols-2">
			<div>
				<dt class="text-surface-600-400">Current zone</dt>
				<dd>{data.profile.currentZone ?? 'Unknown'}</dd>
			</div>
			<div>
				<dt class="text-surface-600-400">Registered</dt>
				<dd>{new Date(data.profile.registeredAt).toLocaleDateString()}</dd>
			</div>
			<div class="sm:col-span-2">
				<dt class="text-surface-600-400">Account ID</dt>
				<dd class="font-mono text-xs break-all">{data.profile.accountId}</dd>
			</div>
		</dl>
	</section>
</div>
