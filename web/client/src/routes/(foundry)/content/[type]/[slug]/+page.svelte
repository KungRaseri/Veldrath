<script lang="ts">
	import ContentRenderer from '#lib/components/foundry/ContentRenderer.svelte';
	import FoundryBreadcrumbs from '#lib/components/foundry/FoundryBreadcrumbs.svelte';
	import FoundryPageHeader from '#lib/components/foundry/FoundryPageHeader.svelte';
	import ArchetypePortrait from '#lib/game/ArchetypePortrait.svelte';
	import { archetypeTierFromTraits, silhouetteForTypeKey } from '#lib/game/archetype-portraits';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	/** The entity's name; both parts can be absent on a sparse record, and a template literal would say "null". */
	const label = $derived(data.summary.displayName ?? data.slug ?? 'Untitled');
	const description = $derived(
		`${data.summary.contentType} · ${data.summary.typeKey ?? 'untyped'} · ${data.summary.isActive ? 'active' : 'inactive'}`
	);

	/**
	 * Archetypes are the one content type that owns art, and their detail view is where it is read.
	 * The portrait is addressed by slug convention — see `archetype-portraits.ts` — so no row carries
	 * an image path and a missing file simply draws the class glyph.
	 */
	const portrait = $derived(
		data.contentType === 'archetype'
			? {
					tier: archetypeTierFromTraits(data.payload.traits),
					silhouette: silhouetteForTypeKey(
						typeof data.payload.typeKey === 'string' ? data.payload.typeKey : null
					)
				}
			: null
	);
</script>

<svelte:head>
	<title>{label} — Foundry content</title>
</svelte:head>

<div class="space-y-4">
	<FoundryBreadcrumbs
		items={[
			{ label: 'Foundry', href: '/foundry' },
			{
				label: data.schema?.displayLabel ?? data.contentType,
				href: `/content/${data.contentType}`
			},
			{ label }
		]}
	/>

	<FoundryPageHeader title={label} {description} />

	{#if portrait}
		<section class="game-panel flex items-center gap-4 p-4" aria-label="Archetype portrait">
			<ArchetypePortrait
				slug={data.slug}
				size={256}
				tier={portrait.tier}
				silhouette={portrait.silhouette}
			/>
			<div class="space-y-1">
				<p class="text-sm font-semibold">Portrait</p>
				<p class="text-xs text-surface-600-400">
					Looked up by slug at <code>images/archetypes/{data.slug}-256.webp</code>. No file means
					the silhouette glyph — content and art land independently.
				</p>
			</div>
		</section>
	{/if}

	{#if data.canEdit}
		<a
			class="btn self-start preset-filled-primary-500 btn-sm"
			href={`/content/${data.contentType}/${data.slug}/edit`}
		>
			Edit this entity
		</a>
	{/if}

	<ContentRenderer schema={data.schema} payload={data.payload} />
</div>
