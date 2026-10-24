<script lang="ts">
	import { resolve } from '$app/paths';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>Server status — Admin</title>
</svelte:head>

<div class="mx-auto max-w-5xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={resolve('/(admin)/admin')}>← Admin</a>

	<header class="space-y-1">
		<h1 class="h1">Server status</h1>
		{#if data.status}
			<p class="text-surface-600-400">
				Started {new Date(data.status.serverStartedAt).toLocaleString()}
			</p>
		{/if}
	</header>

	{#if data.error}
		<p class="text-sm text-error-500">{data.error}</p>
	{/if}

	{#if data.status}
		<div class="grid gap-3 sm:grid-cols-3">
			<div class="card p-4">
				<p class="text-xs tracking-wide text-surface-600-400 uppercase">Connected</p>
				<p class="text-3xl font-semibold">{data.status.connectedPlayers}</p>
			</div>
			<div class="card p-4">
				<p class="text-xs tracking-wide text-surface-600-400 uppercase">Accounts</p>
				<p class="text-3xl font-semibold">{data.status.registeredAccounts}</p>
			</div>
			<div class="card p-4">
				<p class="text-xs tracking-wide text-surface-600-400 uppercase">Characters</p>
				<p class="text-3xl font-semibold">{data.status.totalCharacters}</p>
			</div>
		</div>

		<section class="overflow-x-auto card p-4">
			<h2 class="mb-2 h3">Zone populations</h2>
			<table class="table w-full text-sm">
				<thead>
					<tr><th>Zone</th><th>ID</th><th class="text-right">Players</th></tr>
				</thead>
				<tbody>
					{#each data.status.zonePopulations as zone (zone.zoneId)}
						<tr>
							<td>{zone.zoneName}</td>
							<td class="font-mono text-xs">{zone.zoneId}</td>
							<td class="text-right">{zone.playerCount}</td>
						</tr>
					{:else}
						<tr><td class="text-surface-600-400" colspan="3">No populated zones.</td></tr>
					{/each}
				</tbody>
			</table>
		</section>

		<section class="space-y-2 card p-4">
			<h2 class="h3">Health checks</h2>
			<ul class="space-y-1 text-sm">
				{#each data.status.healthChecks ?? [] as check (check.name)}
					<li class="flex flex-wrap items-center gap-2">
						<span class="chip preset-tonal-surface text-xs">{check.status}</span>
						<span>{check.name}</span>
						{#if check.description}
							<span class="text-surface-600-400">— {check.description}</span>
						{/if}
					</li>
				{:else}
					<li class="text-surface-600-400">No health checks reported.</li>
				{/each}
			</ul>
		</section>
	{/if}
</div>
