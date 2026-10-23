<script lang="ts">
	import { resolve } from '$app/paths';
	import { authClient } from '$lib/auth-client';

	let email = $state('');
	let message = $state('');
	let errorMessage = $state('');
	let submitting = $state(false);

	async function submit(event: SubmitEvent) {
		event.preventDefault();
		errorMessage = '';
		message = '';
		submitting = true;

		try {
			const { error } = await authClient.requestPasswordReset({
				email,
				redirectTo: `${window.location.origin}/reset-password`
			});

			if (error) {
				errorMessage = error.message ?? 'Could not request a password reset.';
				return;
			}

			message =
				'If an account exists for that address, a reset link has been sent. Check your email.';
		} finally {
			submitting = false;
		}
	}
</script>

<svelte:head>
	<title>Forgot password — Veldrath</title>
</svelte:head>

<div class="mx-auto max-w-md space-y-4 p-6">
	<h1 class="h2">Forgot your password?</h1>
	<p class="text-sm text-surface-600-400">
		Enter the email address on your account and we’ll send a reset link.
	</p>

	{#if message}
		<p class="card border border-success-500/40 p-3 text-sm text-success-500">{message}</p>
	{/if}
	{#if errorMessage}
		<p class="card border border-error-500/40 p-3 text-sm text-error-500">{errorMessage}</p>
	{/if}

	<form class="space-y-3 card p-6" onsubmit={submit}>
		<label class="label">
			<span>Email</span>
			<input class="input" type="email" bind:value={email} required autocomplete="email" />
		</label>
		<button class="btn w-full preset-filled-primary-500" type="submit" disabled={submitting}>
			{submitting ? 'Sending…' : 'Send reset link'}
		</button>
	</form>

	<p class="text-sm text-surface-600-400">
		<a class="anchor" href={resolve('/login')}>Back to sign in</a>
	</p>
</div>
