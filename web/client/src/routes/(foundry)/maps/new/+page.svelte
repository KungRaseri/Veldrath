<script lang="ts">
	import { untrack } from 'svelte';
	import { ArrowLeft } from '@lucide/svelte';
	import FoundryPageHeader from '#lib/components/foundry/FoundryPageHeader.svelte';
	import { MAP_KIND_HINTS, MAP_KINDS } from '#lib/foundry/map-editor';
	import type { PageData } from './$types';

	let {
		data,
		form
	}: {
		data: PageData;
		form: { message?: string; mapType?: string; ownerId?: string; name?: string } | null;
	} = $props();

	// Seeded once: the select owns the value from there, and a rejected post must not snap it back.
	let kind = $state(untrack(() => form?.mapType ?? 'Zone'));
	const hint = $derived(MAP_KIND_HINTS[kind]);
	const interior = $derived(kind === 'Interior');
</script>

<svelte:head>
	<title>New map — Foundry</title>
</svelte:head>

<div class="space-y-4">
	<FoundryPageHeader
		title="New map"
		description="A map starts as a draft only you can see. A reviewer makes it live, and until then nothing in the game changes."
	/>

	{#if form?.message}
		<p class="game-panel p-3 text-sm text-error-500" role="alert">{form.message}</p>
	{/if}

	{#if data.error}
		<p class="text-sm text-warning-500">
			The location list could not be loaded, so interiors cannot be suggested. {data.error}
		</p>
	{/if}

	<form method="POST" class="game-panel space-y-4 p-4">
		<div>
			<label class="label">
				<span>What are you making?</span>
				<select class="select" name="mapType" bind:value={kind}>
					{#each MAP_KINDS as entry (entry.value)}
						<option value={entry.value}>{entry.label}</option>
					{/each}
				</select>
			</label>
			<p class="mt-1 text-xs text-surface-600-400">{hint}</p>
		</div>

		<div>
			<label class="label">
				<span>
					{interior
						? 'Location slug'
						: `New slug for this ${kind === 'Region' ? 'region' : 'zone'}`}
				</span>
				<input
					class="input"
					name="ownerId"
					list="map-locations"
					required
					value={form?.ownerId ?? ''}
					placeholder={interior ? 'the-halrow-threshold' : 'the-halrow'}
				/>
			</label>
			<p class="mt-1 text-xs text-surface-600-400">
				{interior
					? 'An interior belongs to a location that already exists — pick one below or type its slug.'
					: 'The slug becomes the map id, so it must be unique, lowercase, and hyphenated.'}
			</p>
			<datalist id="map-locations">
				{#each data.locations as location (location.id)}
					<option value={location.id}>{location.label}</option>
				{/each}
			</datalist>
		</div>

		<label class="label">
			<span>Map name</span>
			<input class="input" name="name" required value={form?.name ?? ''} placeholder="The Halrow" />
		</label>

		<div class="flex flex-wrap items-center gap-2">
			<button class="btn preset-filled-primary-500" type="submit">Create draft</button>
			<a class="btn preset-tonal-surface" href="/maps">
				<ArrowLeft size={15} aria-hidden="true" />
				Back to maps
			</a>
		</div>
	</form>
</div>
