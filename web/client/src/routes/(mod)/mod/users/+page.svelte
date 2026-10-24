<script lang="ts">
	import { resolve } from '$app/paths';
	import Pager from '#lib/components/Pager.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>Player accounts — Moderation</title>
</svelte:head>

<div class="mx-auto max-w-5xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={resolve('/(mod)/mod')}>← Moderation</a>

	<header class="space-y-1">
		<h1 class="h1">Player accounts</h1>
		<p class="text-surface-600-400">{data.total} accounts.</p>
	</header>

	{#if data.error}
		<p class="text-sm text-error-500">{data.error}</p>
	{/if}

	<form class="grid gap-3 card p-4 sm:grid-cols-4" method="GET">
		<label class="label">
			<span>Search</span>
			<input class="input" type="search" name="search" value={data.filters.search ?? ''} />
		</label>
		<label class="label">
			<span>Status</span>
			<select class="select" name="status" value={data.filters.status ?? ''}>
				<option value="">Any</option>
				<option value="active">Active</option>
				<option value="banned">Banned</option>
				<option value="muted">Muted</option>
			</select>
		</label>
		<label class="label">
			<span>Sort</span>
			<select class="select" name="sort" value={data.filters.sort ?? ''}>
				<option value="">Username</option>
				<option value="lastseen">Last seen</option>
				<option value="warncount">Warnings</option>
			</select>
		</label>
		<div class="flex items-end">
			<button class="btn w-full preset-tonal-surface" type="submit">Apply</button>
		</div>
	</form>

	<div class="overflow-x-auto card">
		<table class="table w-full text-sm">
			<thead>
				<tr>
					<th>Username</th>
					<th>Roles</th>
					<th>Status</th>
					<th class="text-right">Warnings</th>
					<th>Last seen</th>
				</tr>
			</thead>
			<tbody>
				{#each data.users as user (user.id)}
					<tr>
						<td><a class="anchor" href={`/mod/users/${user.id}`}>{user.username}</a></td>
						<td>
							{#each user.roles as role (role)}
								<span class="chip preset-tonal-surface text-xs">{role}</span>
							{:else}
								<span class="text-surface-600-400">—</span>
							{/each}
						</td>
						<td>
							{#if user.isBanned}
								<span class="text-error-500">Banned</span>
							{:else if user.isMuted}
								<span class="text-warning-500">Muted</span>
							{:else}
								Active
							{/if}
						</td>
						<td class="text-right">{user.warnCount}</td>
						<td>
							{user.lastSeenAt ? new Date(user.lastSeenAt).toLocaleString() : '—'}
						</td>
					</tr>
				{:else}
					<tr>
						<td class="text-surface-600-400" colspan="5">No accounts match these filters.</td>
					</tr>
				{/each}
			</tbody>
		</table>
	</div>

	<Pager
		page={data.page}
		pageSize={data.pageSize}
		total={data.total}
		basePath="/mod/users"
		query={{
			search: data.filters.search,
			status: data.filters.status,
			sort: data.filters.sort
		}}
	/>
</div>
