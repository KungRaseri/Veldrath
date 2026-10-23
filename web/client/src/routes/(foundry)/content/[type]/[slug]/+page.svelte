<script lang="ts">
	import { resolve } from '$app/paths';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	const prettyPayload = $derived(JSON.stringify(data.payload, null, 2));
</script>

<svelte:head>
	<title>{data.summary.displayName ?? data.slug} — Foundry content</title>
</svelte:head>

<div class="mx-auto max-w-4xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={`/content/${data.contentType}`}>
		← All {data.contentType} entries
	</a>

	<header class="space-y-1">
		<p class="text-xs tracking-wide text-surface-600-400 uppercase">
			{data.summary.contentType} · {data.summary.typeKey}
		</p>
		<h1 class="h1">{data.summary.displayName ?? data.slug}</h1>
		<p class="text-surface-600-400">
			<span class="font-mono text-xs">{data.summary.slug}</span> · rarity
			{data.summary.rarityWeight} · {data.summary.isActive ? 'active' : 'inactive'}
		</p>
	</header>

	<section class="space-y-2 card p-4">
		<h2 class="h3">Entity payload</h2>
		<pre class="overflow-auto text-xs">{prettyPayload}</pre>
	</section>

	<a class="text-sm text-primary-500" href={resolve('/content')}>← Back to content types</a>
</div>
