<script lang="ts">
	import { resolve } from '$app/paths';
	import { authClient } from '$lib/auth-client';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	let email = $state('');
	let resendMessage = $state('');
	let resendError = $state('');
	let sending = $state(false);

	async function resend(event: SubmitEvent) {
		event.preventDefault();
		resendError = '';
		resendMessage = '';
		sending = true;

		try {
			const { error } = await authClient.sendVerificationEmail({
				email,
				callbackURL: `${window.location.origin}/account`
			});

			if (error) {
				resendError = error.message ?? 'Could not send a new verification email.';
				return;
			}

			resendMessage =
				'If an account exists for that address, a new confirmation link has been sent.';
		} finally {
			sending = false;
		}
	}
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
			This link is missing its token. Request a new verification email below.
		</p>
	{:else}
		<h1 class="h2">Verification failed</h1>
		<p class="text-sm text-surface-600-400">
			This verification link is invalid or has expired. Request a new one below.
		</p>
	{/if}

	{#if data.status !== 'verified'}
		{#if resendMessage}
			<p class="card border border-success-500/40 p-3 text-sm text-success-500">{resendMessage}</p>
		{/if}
		{#if resendError}
			<p class="card border border-error-500/40 p-3 text-sm text-error-500">{resendError}</p>
		{/if}

		<form class="space-y-3 card p-6" onsubmit={resend}>
			<label class="label">
				<span>Email</span>
				<input class="input" type="email" bind:value={email} required autocomplete="email" />
			</label>
			<button class="btn w-full preset-filled-primary-500" type="submit" disabled={sending}>
				{sending ? 'Sending…' : 'Send a new verification link'}
			</button>
		</form>
	{/if}

	<div class="flex gap-2">
		<a class="btn preset-filled-primary-500" href={resolve('/login')}>Sign in</a>
		<a class="btn preset-tonal-surface" href={resolve('/account')}>Account</a>
	</div>
</div>
