<script lang="ts">
	import { resolve } from '$app/paths';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();

	const player = $derived(data.player);
</script>

<svelte:head>
	<title>{player.username} — Moderation</title>
</svelte:head>

<div class="mx-auto max-w-4xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={resolve('/mod/users')}>← Player accounts</a>

	{#if form?.error}
		<p class="text-sm text-error-500">{form.error}</p>
	{/if}

	<header class="space-y-1">
		<h1 class="h1">{player.username}</h1>
		<p class="text-surface-600-400">
			{player.email ?? 'No email'} · <span class="font-mono text-xs">{player.id}</span>
		</p>
		<div class="flex flex-wrap gap-2">
			{#if player.isBanned}
				<span class="chip preset-tonal-surface text-xs text-error-500">Banned</span>
			{/if}
			{#if player.isMuted}
				<span class="chip preset-tonal-surface text-xs text-warning-500">Muted</span>
			{/if}
			<span class="chip preset-tonal-surface text-xs">{player.warnCount} warnings</span>
			{#each player.roles as role (role)}
				<span class="chip preset-tonal-surface text-xs">{role}</span>
			{/each}
		</div>
	</header>

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

		<form class="space-y-2 card p-4" method="POST" action="?/unmute">
			<h2 class="h3">Lift mute</h2>
			<p class="text-sm text-surface-600-400">Remove an active chat mute.</p>
			<button class="btn preset-tonal-surface" type="submit">Unmute</button>
		</form>
	</section>
</div>
