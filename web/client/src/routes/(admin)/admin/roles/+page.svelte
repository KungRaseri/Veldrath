<script lang="ts">
	import { resolve } from '$app/paths';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>Role management — Admin</title>
</svelte:head>

<div class="mx-auto max-w-5xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={resolve('/(admin)/admin')}>← Admin</a>

	<header class="space-y-1">
		<h1 class="h1">Role management</h1>
		<p class="text-surface-600-400">
			Roles are seeded at server startup. Adjust a player's roles or per-user permissions from
			<a class="anchor" href={resolve('/(admin)/admin/users')}>user management</a>.
		</p>
	</header>

	<div class="grid gap-3 sm:grid-cols-2">
		{#each data.roles as role (role.name)}
			<article class="space-y-2 card p-4">
				<h2 class="h3">{role.name}</h2>
				<p class="text-sm text-surface-600-400">{role.description}</p>
				<ul class="flex flex-wrap gap-1">
					{#each role.permissions as permission (permission)}
						<li class="chip preset-tonal-surface text-xs">{permission}</li>
					{/each}
				</ul>
			</article>
		{/each}
	</div>
</div>
