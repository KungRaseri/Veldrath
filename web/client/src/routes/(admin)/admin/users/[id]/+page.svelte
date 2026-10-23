<script lang="ts">
	import { resolve } from '$app/paths';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();

	const player = $derived(data.player);
</script>

<svelte:head>
	<title>{player.username} — Admin</title>
</svelte:head>

<div class="mx-auto max-w-5xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={resolve('/admin/users')}>← User management</a>

	{#if form?.error}
		<p class="text-sm text-error-500">{form.error}</p>
	{/if}

	<header class="flex flex-wrap items-center justify-between gap-3">
		<div>
			<h1 class="h1">{player.username}</h1>
			<p class="text-surface-600-400">
				{player.email ?? 'No email'} · account <span class="font-mono text-xs">{player.id}</span>
			</p>
		</div>
		<div class="flex flex-wrap gap-2">
			{#if player.isBanned}
				<span class="chip preset-tonal-surface text-xs text-error-500">Banned</span>
			{/if}
			{#if player.isMuted}
				<span class="chip preset-tonal-surface text-xs text-warning-500">Muted</span>
			{/if}
			<span class="chip preset-tonal-surface text-xs">{player.warnCount} warnings</span>
		</div>
	</header>

	<section class="space-y-3 card p-4">
		<h2 class="h3">Roles</h2>
		<ul class="flex flex-wrap gap-2">
			{#each player.roles as role (role)}
				<li class="chip flex items-center gap-2 preset-tonal-surface text-xs">
					{role}
					<form method="POST" action="?/revokeRole">
						<input type="hidden" name="role" value={role} />
						<button class="text-error-500" type="submit" aria-label={`Revoke ${role}`}>✕</button>
					</form>
				</li>
			{:else}
				<li class="text-sm text-surface-600-400">No roles assigned.</li>
			{/each}
		</ul>
		<form class="flex flex-wrap items-end gap-2" method="POST" action="?/assignRole">
			<label class="label">
				<span>Assign role</span>
				<select class="select" name="role">
					{#each data.allRoles as role (role)}
						<option value={role}>{role}</option>
					{/each}
				</select>
			</label>
			<button class="btn preset-tonal-surface" type="submit">Assign</button>
		</form>
	</section>

	<section class="space-y-3 card p-4">
		<h2 class="h3">Effective permissions</h2>
		<ul class="flex flex-wrap gap-2">
			{#each player.effectivePermissions as permission (permission)}
				<li class="chip preset-tonal-surface text-xs">{permission}</li>
			{:else}
				<li class="text-sm text-surface-600-400">No permissions.</li>
			{/each}
		</ul>

		{#if player.userPermissions.length > 0}
			<h3 class="h4">Direct grants</h3>
			<ul class="flex flex-wrap gap-2">
				{#each player.userPermissions as permission (permission)}
					<li class="chip flex items-center gap-2 preset-tonal-surface text-xs">
						{permission}
						<form method="POST" action="?/revokePermission">
							<input type="hidden" name="permission" value={permission} />
							<button class="text-error-500" type="submit" aria-label={`Revoke ${permission}`}
								>✕</button
							>
						</form>
					</li>
				{/each}
			</ul>
		{/if}

		<form class="flex flex-wrap items-end gap-2" method="POST" action="?/grantPermission">
			<label class="label">
				<span>Grant permission</span>
				<select class="select" name="permission">
					{#each data.allPermissions as permission (permission)}
						<option value={permission}>{permission}</option>
					{/each}
				</select>
			</label>
			<button class="btn preset-tonal-surface" type="submit">Grant</button>
		</form>
	</section>

	<section class="grid gap-3 sm:grid-cols-2">
		<form class="space-y-2 card p-4" method="POST" action="?/kick">
			<h2 class="h3">Kick</h2>
			<label class="label">
				<span>Reason</span>
				<input class="input" type="text" name="reason" />
			</label>
			<button class="btn preset-tonal-surface" type="submit">Kick sessions</button>
		</form>

		<form class="space-y-2 card p-4" method="POST" action="?/warn">
			<h2 class="h3">Warn</h2>
			<label class="label">
				<span>Reason</span>
				<input class="input" type="text" name="reason" required />
			</label>
			<button class="btn preset-tonal-surface" type="submit">Issue warning</button>
		</form>

		<form class="space-y-2 card p-4" method="POST" action="?/mute">
			<h2 class="h3">Mute</h2>
			<label class="label">
				<span>Reason</span>
				<input class="input" type="text" name="reason" />
			</label>
			<label class="label">
				<span>Duration (minutes, blank = permanent)</span>
				<input class="input" type="number" name="durationMinutes" min="1" />
			</label>
			<button class="btn preset-tonal-surface" type="submit">Mute</button>
		</form>

		<form class="space-y-2 card p-4" method="POST" action="?/ban">
			<h2 class="h3">Ban</h2>
			<label class="label">
				<span>Reason</span>
				<input class="input" type="text" name="reason" required />
			</label>
			<label class="label">
				<span>Duration (minutes, blank = permanent)</span>
				<input class="input" type="number" name="durationMinutes" min="1" />
			</label>
			<button class="btn preset-tonal-surface text-error-500" type="submit">Ban account</button>
		</form>
	</section>

	<div class="flex flex-wrap gap-2">
		<form method="POST" action="?/unmute">
			<button class="btn preset-tonal-surface" type="submit">Lift mute</button>
		</form>
		<form method="POST" action="?/unban">
			<button class="btn preset-tonal-surface" type="submit">Lift ban</button>
		</form>
	</div>
</div>
