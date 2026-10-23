<script lang="ts">
	import type { Snippet } from 'svelte';
	import { resolve } from '$app/paths';
	import type { LayoutData } from './$types';
	import './layout.css';

	let { data, children }: { data: LayoutData; children: Snippet } = $props();
</script>

<header class="border-b border-surface-200-800 bg-surface-50-950">
	<div class="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-3 p-4">
		<a class="text-lg font-semibold text-primary-500" href={resolve('/')}>Veldrath</a>

		<nav class="flex flex-wrap items-center gap-1 text-sm">
			<a class="rounded px-2 py-1 hover:bg-surface-200-800" href={resolve('/lore')}>Lore</a>
			<a class="rounded px-2 py-1 hover:bg-surface-200-800" href={resolve('/patch-notes')}>
				Patch notes
			</a>
			<a class="rounded px-2 py-1 hover:bg-surface-200-800" href={resolve('/community')}>
				Community
			</a>
		</nav>

		<nav class="flex items-center gap-2">
			{#if data.user}
				<a class="btn preset-tonal-surface btn-sm" href={resolve('/account')}>
					{data.user.username ?? data.user.name}
				</a>
				<a class="btn preset-filled-primary-500 btn-sm" href={resolve('/characters')}>Play</a>
			{:else}
				<a class="btn preset-tonal-surface btn-sm" href={resolve('/login')}>Sign in</a>
				<a class="btn preset-filled-primary-500 btn-sm" href={resolve('/register')}>
					Create account
				</a>
			{/if}
		</nav>
	</div>
</header>

{@render children()}
