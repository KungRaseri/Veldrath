<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { Gamepad2, MailWarning, Plus, RotateCcw, Shield, Trash2, Wifi } from '@lucide/svelte';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();

	const lastSessionCharacter = $derived(
		data.lastSession
			? (data.characters.find((character) => character.id === data.lastSession?.characterId) ??
					null)
			: null
	);

	const canCreate = $derived(!data.slots || data.slots.remaining > 0);

	async function enterWorld(characterId: string) {
		await goto(resolve('/play/[id]', { id: characterId }));
	}

	function confirmDelete(event: SubmitEvent, name: string) {
		if (!window.confirm(`Delete ${name}? This cannot be undone.`)) {
			event.preventDefault();
		}
	}
</script>

<svelte:head>
	<title>Characters — Veldrath</title>
</svelte:head>

<div class="mx-auto max-w-3xl space-y-6 p-6">
	<header class="flex flex-wrap items-end justify-between gap-3">
		<div class="flex items-center gap-3">
			<img
				class="h-12 w-12 object-contain"
				src="/images/veldrath-final-crest-transparent.png"
				alt=""
				aria-hidden="true"
			/>
			<div class="space-y-1">
				<h1 class="h2">Choose your character</h1>
				<p class="text-sm text-surface-600-400">
					Select an existing character to enter Veldrath, or create a new one.
				</p>
				{#if data.slots}
					<p class="text-sm text-surface-600-400">
						{data.slots.used} of {data.slots.max} character slots used
					</p>
				{/if}
			</div>
		</div>

		{#if canCreate}
			<a class="btn preset-filled-primary-500" href={resolve('/characters/new')}>
				<Plus size={16} aria-hidden="true" />
				Create character
			</a>
		{:else}
			<span class="text-sm text-warning-500">
				All character slots are in use. Delete a character to create another.
			</span>
		{/if}
	</header>

	{#if !data.emailVerified}
		<div class="flex items-center gap-2 card border border-warning-500/40 p-4 text-sm">
			<MailWarning class="shrink-0 text-warning-500" size={18} aria-hidden="true" />
			<p>
				Your email address is not verified.
				<a class="anchor" href={resolve('/account')}>Resend the confirmation link</a>
				from your account page to keep your account recoverable.
			</p>
		</div>
	{/if}

	{#if data.loadError}
		<div class="card border border-error-500/40 p-4 text-sm text-error-500">
			{data.loadError}
		</div>
	{/if}
	{#if data.slotsError}
		<div class="card border border-error-500/40 p-4 text-sm text-error-500">
			{data.slotsError}
		</div>
	{/if}
	{#if form?.error}
		<div class="card border border-error-500/40 p-4 text-sm text-error-500">{form.error}</div>
	{/if}
	{#if data.lastSessionError}
		<div class="card border border-warning-500/40 p-4 text-sm text-warning-500">
			Could not load your last session: {data.lastSessionError}
		</div>
	{/if}

	{#if data.lastSession && lastSessionCharacter && !lastSessionCharacter.isOnline}
		<div
			class="flex flex-wrap items-center justify-between gap-3 card border border-primary-500/40 p-5"
		>
			<div class="space-y-1">
				<h2 class="h3">Resume your last session</h2>
				<p class="text-sm text-surface-600-400">
					Continue as {lastSessionCharacter.name}
					{#if lastSessionCharacter.currentZoneLocationSlug}
						at {lastSessionCharacter.currentZoneLocationSlug}
					{:else if lastSessionCharacter.currentZoneId}
						in {lastSessionCharacter.currentZoneId}
					{/if}
				</p>
			</div>
			<button
				class="btn preset-filled-primary-500"
				type="button"
				onclick={() => enterWorld(lastSessionCharacter.id)}
			>
				<RotateCcw size={16} aria-hidden="true" />
				Resume
			</button>
		</div>
	{/if}

	{#if data.characters.length === 0}
		<div class="space-y-3 card p-6">
			<h2 class="h3">No characters yet</h2>
			<p class="text-sm text-surface-600-400">Create your first character to enter Veldrath.</p>
			{#if canCreate}
				<a class="btn w-fit preset-filled-primary-500" href={resolve('/characters/new')}>
					<Plus size={16} aria-hidden="true" />
					Create character
				</a>
			{:else}
				<p class="text-sm text-warning-500">
					Your character slots are full. Delete a character before creating another.
				</p>
			{/if}
		</div>
	{:else}
		<ul class="space-y-3">
			{#each data.characters as character (character.id)}
				<li class="flex flex-wrap items-center justify-between gap-4 card p-5">
					<div class="space-y-1">
						<div class="flex items-center gap-2">
							<h2 class="h3">{character.name}</h2>
							{#if character.isOnline}
								<span class="chip preset-filled-success-500 text-xs">
									<Wifi size={11} aria-hidden="true" />
									Online
								</span>
							{/if}
							{#if character.isHardcore}
								<span class="chip preset-filled-error-500 text-xs">
									<Shield size={11} aria-hidden="true" />
									Hardcore
								</span>
							{/if}
							{#if data.lastSession?.characterId === character.id}
								<span class="preset-tonal-primary-500 chip text-xs">Last played</span>
							{/if}
						</div>
						<p class="text-sm text-surface-600-400">
							Level {character.level}
							{character.className} · Slot {character.slotIndex + 1}
						</p>
						<p class="text-sm text-surface-600-400">
							{character.currentZoneId || 'Region map'} · {character.experience.toLocaleString()}
							XP
						</p>
					</div>

					<div class="flex flex-wrap items-center gap-2">
						<button
							class="btn preset-filled-primary-500"
							type="button"
							onclick={() => enterWorld(character.id)}
							disabled={character.isOnline}
						>
							<Gamepad2 size={16} aria-hidden="true" />
							{character.isOnline ? 'Already online' : 'Enter world'}
						</button>

						<form
							method="POST"
							action="?/delete"
							onsubmit={(event) => confirmDelete(event, character.name)}
						>
							<input type="hidden" name="id" value={character.id} />
							<button
								class="preset-tonal-error-500 btn"
								type="submit"
								disabled={character.isOnline}
								title={character.isOnline
									? 'Sign out the character before deleting it.'
									: undefined}
							>
								<Trash2 size={15} aria-hidden="true" />
								Delete
							</button>
						</form>
					</div>
				</li>
			{/each}
		</ul>
	{/if}
</div>
