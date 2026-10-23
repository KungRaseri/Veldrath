<script lang="ts">
	import type { Snippet } from 'svelte';
	import { resolve } from '$app/paths';
	import type { LayoutData } from './$types';
	import './layout.css';

	let { data, children }: { data: LayoutData; children: Snippet } = $props();
</script>

<header class="border-b border-surface-200-800 bg-surface-50-950">
	<div class="mx-auto flex max-w-6xl items-center justify-between gap-4 p-4">
		<a class="text-lg font-semibold text-primary-500" href={resolve('/')}>Veldrath</a>
		<nav class="flex items-center gap-2">
			{#if data.user}
				<span class="text-sm text-surface-600-400">
					{data.user.username ?? data.user.name}
				</span>
				<a class="btn preset-filled-primary-500 btn-sm" href={resolve('/game')}>Play</a>
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
