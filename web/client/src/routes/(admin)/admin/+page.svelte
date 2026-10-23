<script lang="ts">
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	const links = [
		{
			href: '/admin/users',
			title: 'Users',
			description: 'Search accounts, assign roles and permissions.'
		},
		{
			href: '/admin/roles',
			title: 'Roles',
			description: 'Review seeded roles and their default permissions.'
		},
		{
			href: '/admin/audit',
			title: 'Audit log',
			description: 'Every staff action recorded by the server.'
		},
		{
			href: '/admin/sessions',
			title: 'Active sessions',
			description: 'Characters currently connected.'
		},
		{
			href: '/admin/reports',
			title: 'Reports',
			description: 'Player-submitted behaviour reports.'
		},
		{
			href: '/admin/status',
			title: 'Server status',
			description: 'Population, health checks, and zones.'
		},
		{
			href: '/admin/server',
			title: 'Server commands',
			description: 'Broadcast an in-game announcement.'
		}
	];
</script>

<svelte:head>
	<title>Admin — Veldrath</title>
</svelte:head>

<div class="mx-auto max-w-5xl space-y-4 p-6">
	<header class="space-y-1">
		<h1 class="h1">Admin</h1>
		<p class="text-surface-600-400">User management, moderation, and live server operations.</p>
	</header>

	{#if data.error}
		<p class="text-sm text-error-500">{data.error}</p>
	{/if}

	{#if data.status}
		<div class="grid gap-3 sm:grid-cols-3">
			<div class="card p-4">
				<p class="text-xs tracking-wide text-surface-600-400 uppercase">Connected players</p>
				<p class="text-3xl font-semibold">{data.status.connectedPlayers}</p>
			</div>
			<div class="card p-4">
				<p class="text-xs tracking-wide text-surface-600-400 uppercase">Registered accounts</p>
				<p class="text-3xl font-semibold">{data.status.registeredAccounts}</p>
			</div>
			<div class="card p-4">
				<p class="text-xs tracking-wide text-surface-600-400 uppercase">Characters</p>
				<p class="text-3xl font-semibold">{data.status.totalCharacters}</p>
			</div>
		</div>
	{/if}

	<div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
		{#each links as link (link.href)}
			<a class="flex flex-col gap-1 card p-4" href={link.href}>
				<h2 class="h3">{link.title}</h2>
				<p class="text-sm text-surface-600-400">{link.description}</p>
			</a>
		{/each}
	</div>
</div>
