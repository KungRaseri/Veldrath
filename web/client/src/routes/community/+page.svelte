<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { Megaphone, Search, Server, UserSearch, Users } from '@lucide/svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	let playerId = $state('');
	let lookupError = $state('');

	const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

	async function lookupPlayer(event: SubmitEvent) {
		event.preventDefault();
		lookupError = '';

		if (!guidPattern.test(playerId.trim())) {
			lookupError = 'Enter a valid account GUID.';
			return;
		}

		await goto(resolve('/players/[id]', { id: playerId.trim() }));
	}
</script>

<svelte:head>
	<title>Community — Veldrath</title>
	<meta name="description" content="Veldrath community, announcements, and player profiles." />
</svelte:head>

<div class="mx-auto max-w-4xl space-y-6 p-6">
	<header class="space-y-2">
		<h1 class="text-title-large flex items-center gap-3">
			<Users class="text-secondary-500" size={28} aria-hidden="true" />
			Community
		</h1>
		<p class="text-surface-600-400">Server status, announcements, and public player profiles.</p>
	</header>

	<section class="flex flex-wrap items-center justify-between gap-3 card p-4">
		<div>
			<h2 class="flex items-center gap-2 h3">
				<Server class="text-secondary-500" size={18} aria-hidden="true" />
				Game server
			</h2>
			<p class="text-sm text-surface-600-400">
				{data.online ? 'Online and accepting connections.' : 'Status unavailable right now.'}
			</p>
		</div>
		<span class="chip {data.online ? 'preset-filled-success-500' : 'preset-filled-error-500'}">
			{data.online ? 'Online' : 'Offline'}
		</span>
	</section>

	<section class="space-y-3 card p-4">
		<h2 class="flex items-center gap-2 h3">
			<Megaphone class="text-secondary-500" size={18} aria-hidden="true" />
			Announcements
		</h2>
		{#if data.error}
			<p class="text-sm text-error-500">{data.error}</p>
		{/if}
		{#if data.announcements.length === 0}
			<p class="text-sm text-surface-600-400">No announcements right now.</p>
		{:else}
			<ul class="space-y-2">
				{#each data.announcements as announcement (announcement.id)}
					<li class="rounded border border-surface-200-800 p-3">
						<p class="font-semibold">{announcement.title}</p>
						<p class="text-sm text-surface-600-400">{announcement.body}</p>
					</li>
				{/each}
			</ul>
		{/if}
	</section>

	<section class="space-y-3 card p-4">
		<h2 class="flex items-center gap-2 h3">
			<UserSearch class="text-secondary-500" size={18} aria-hidden="true" />
			Find a player
		</h2>
		<form class="flex flex-wrap items-end gap-2" onsubmit={lookupPlayer}>
			<label class="label min-w-64 flex-1">
				<span>Account ID</span>
				<input
					class="input"
					bind:value={playerId}
					placeholder="00000000-0000-0000-0000-000000000000"
				/>
			</label>
			<button class="btn preset-filled-primary-500" type="submit">
				<Search size={15} aria-hidden="true" />
				View profile
			</button>
		</form>
		{#if lookupError}
			<p class="text-sm text-error-500">{lookupError}</p>
		{/if}
	</section>
</div>
