<script lang="ts">
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	const sections = $derived([
		{
			title: 'Lore articles',
			href: '/editorial/lore',
			count: data.counts.lore,
			description: 'World history, factions, geography, and Calethic language notes.'
		},
		{
			title: 'Patch notes',
			href: '/editorial/patch-notes',
			count: data.counts.patchNotes,
			description: 'Versioned release notes shown on the public patch-notes page.'
		},
		{
			title: 'Announcements',
			href: '/editorial/announcements',
			count: data.counts.announcements,
			description: 'Short site-wide notices surfaced on the home and community pages.'
		}
	]);
</script>

<svelte:head>
	<title>Editorial — Veldrath</title>
</svelte:head>

<div class="mx-auto max-w-5xl space-y-4 p-6">
	<header class="space-y-1">
		<h1 class="h1">Editorial</h1>
		<p class="text-surface-600-400">
			Manage public lore, patch notes, and announcements. Publishing requires the content.manage
			permission.
		</p>
	</header>

	{#each data.errors as error, index (index)}
		<p class="text-sm text-error-500">{error}</p>
	{/each}

	<div class="grid gap-3 sm:grid-cols-3">
		{#each sections as section (section.href)}
			<a class="flex flex-col gap-2 card p-4" href={section.href}>
				<span class="text-3xl font-semibold text-primary-500">{section.count}</span>
				<h2 class="h3">{section.title}</h2>
				<p class="text-sm text-surface-600-400">{section.description}</p>
			</a>
		{/each}
	</div>
</div>
