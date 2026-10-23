<script lang="ts">
	import { resolve } from '$app/paths';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>Verify email — Veldrath</title>
</svelte:head>

<div class="mx-auto max-w-md space-y-4 p-6">
	{#if data.status === 'verified'}
		<h1 class="h2">Email verified</h1>
		<p class="text-sm text-surface-600-400">
			Your email address is confirmed. You can now sign in and enter Veldrath.
		</p>
	{:else if data.status === 'missing'}
		<h1 class="h2">Verification link incomplete</h1>
		<p class="text-sm text-surface-600-400">
			This link is missing its token. Request a new verification email from your account page.
		</p>
	{:else}
		<h1 class="h2">Verification failed</h1>
		<p class="text-sm text-surface-600-400">
			This verification link is invalid or has expired. Request a new one from your account page.
		</p>
	{/if}

	<div class="flex gap-2">
		<a class="btn preset-filled-primary-500" href={resolve('/login')}>Sign in</a>
		<a class="btn preset-tonal-surface" href={resolve('/account')}>Account</a>
	</div>
</div>
