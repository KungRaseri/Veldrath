<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { authClient } from '$lib/auth-client';

	const token = page.url.searchParams.get('token') ?? '';

	let password = $state('');
	let confirmPassword = $state('');
	let message = $state('');
	let errorMessage = $state('');
	let submitting = $state(false);

	async function submit(event: SubmitEvent) {
		event.preventDefault();
		errorMessage = '';
		message = '';

		if (!token) {
			errorMessage = 'This reset link is missing its token. Request a new one.';
			return;
		}

		if (password !== confirmPassword) {
			errorMessage = 'The passwords do not match.';
			return;
		}

		submitting = true;
		try {
			const { error } = await authClient.resetPassword({ newPassword: password, token });
			if (error) {
				errorMessage = error.message ?? 'Could not reset the password.';
				return;
			}

			message = 'Password updated. You can sign in with your new password.';
			setTimeout(() => void goto(resolve('/login')), 1500);
		} finally {
			submitting = false;
		}
	}
</script>

<svelte:head>
	<title>Reset password — Veldrath</title>
</svelte:head>

<div class="mx-auto max-w-md space-y-4 p-6">
	<h1 class="h2">Choose a new password</h1>

	{#if message}
		<p class="card border border-success-500/40 p-3 text-sm text-success-500">{message}</p>
	{/if}
	{#if errorMessage}
		<p class="card border border-error-500/40 p-3 text-sm text-error-500">{errorMessage}</p>
	{/if}

	{#if token}
		<form class="space-y-3 card p-6" onsubmit={submit}>
			<label class="label">
				<span>New password</span>
				<input
					class="input"
					type="password"
					bind:value={password}
					required
					autocomplete="new-password"
				/>
			</label>
			<label class="label">
				<span>Confirm new password</span>
				<input
					class="input"
					type="password"
					bind:value={confirmPassword}
					required
					autocomplete="new-password"
				/>
			</label>
			<button class="btn w-full preset-filled-primary-500" type="submit" disabled={submitting}>
				{submitting ? 'Saving…' : 'Set new password'}
			</button>
		</form>
	{:else}
		<p class="text-sm text-surface-600-400">
			Request a new link from the <a class="anchor" href={resolve('/forgot-password')}
				>forgot password</a
			> page.
		</p>
	{/if}
</div>
