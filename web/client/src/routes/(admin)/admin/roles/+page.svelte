<script lang="ts">
	import { resolve } from '$app/paths';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();
</script>

<svelte:head>
	<title>Role management — Admin</title>
</svelte:head>

<div class="mx-auto max-w-5xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={resolve('/(admin)/admin')}>← Admin</a>

	<header class="space-y-1">
		<h1 class="h1">Role management</h1>
		<p class="text-surface-600-400">
			Roles are seeded at server startup. Assign them, or grant a permission to one account, from
			that account's page.
			{#if data.canManageUsers}
				<a class="anchor" href={resolve('/(admin)/admin/users')}>User management</a> finds an account
				by search.
			{/if}
		</p>
	</header>

	{#if data.canManageRoles}
		<section class="space-y-3 card p-4">
			<h2 class="h3">Manage a player's roles</h2>
			<p class="text-sm text-surface-600-400">
				Enter the account's username to open its roles and per-user permissions.
			</p>
			<form class="flex flex-wrap items-end gap-2" method="POST" action="?/open">
				<label class="label">
					<span>Account username</span>
					<input class="input" type="text" name="username" autocomplete="off" required />
				</label>
				<button class="btn preset-tonal-surface" type="submit">Open account</button>
			</form>
			{#if form?.error}
				<p class="text-sm text-error-500">{form.error}</p>
			{/if}
		</section>
	{/if}

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
