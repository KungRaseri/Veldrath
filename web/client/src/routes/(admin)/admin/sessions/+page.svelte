<script lang="ts">
	import { resolve } from '$app/paths';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>Active sessions — Admin</title>
</svelte:head>

<div class="mx-auto max-w-5xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={resolve('/(admin)/admin')}>← Admin</a>

	<header class="space-y-1">
		<h1 class="h1">Active sessions</h1>
		<p class="text-surface-600-400">{data.sessions.length} characters currently connected.</p>
	</header>

	{#if data.error}
		<p class="text-sm text-error-500">{data.error}</p>
	{/if}

	<form class="flex flex-wrap items-end gap-3 card p-4" method="GET">
		<label class="label flex-1">
			<span>Region ID</span>
			<input class="input font-mono text-xs" type="text" name="regionId" value={data.regionId} />
		</label>
		<label class="label flex-1">
			<span>Zone ID</span>
			<input class="input font-mono text-xs" type="text" name="zoneId" value={data.zoneId} />
		</label>
		<button class="btn preset-tonal-surface" type="submit">Filter</button>
	</form>

	<div class="overflow-x-auto card">
		<table class="table w-full text-sm">
			<thead>
				<tr>
					<th>Character</th>
					<th>Account</th>
					<th>Region</th>
					<th>Zone</th>
					<th>Entered</th>
				</tr>
			</thead>
			<tbody>
				{#each data.sessions as session (session.characterId)}
					<tr>
						<td>{session.characterName}</td>
						<td>
							<a class="anchor" href={`/admin/users/${session.accountId}`}>
								{session.accountUsername}
							</a>
						</td>
						<td class="font-mono text-xs">{session.regionId}</td>
						<td class="font-mono text-xs">{session.zoneId ?? '—'}</td>
						<td>{new Date(session.enteredAt).toLocaleString()}</td>
					</tr>
				{:else}
					<tr>
						<td class="text-surface-600-400" colspan="5">No active sessions.</td>
					</tr>
				{/each}
			</tbody>
		</table>
	</div>
</div>
