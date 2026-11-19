<script lang="ts">
	import ContentRenderer from '#lib/components/foundry/ContentRenderer.svelte';
	import FoundryBreadcrumbs from '#lib/components/foundry/FoundryBreadcrumbs.svelte';
	import FoundryPageHeader from '#lib/components/foundry/FoundryPageHeader.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	/** The entity's name; both parts can be absent on a sparse record, and a template literal would say "null". */
	const label = $derived(data.summary.displayName ?? data.slug ?? 'Untitled');
	const description = $derived(
		`${data.summary.contentType} · ${data.summary.typeKey ?? 'untyped'} · ${data.summary.isActive ? 'active' : 'inactive'}`
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
