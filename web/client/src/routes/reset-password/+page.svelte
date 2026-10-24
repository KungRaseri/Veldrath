<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { KeyRound, Lock, ShieldCheck } from '@lucide/svelte';
	import { authClient } from '#lib/auth-client';

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

<div class="mx-auto max-w-md px-5 py-12">
	<div class="auth-card p-8">
		<img
			class="auth-crest"
			src="/images/veldrath-final-crest-transparent.png"
			alt=""
			aria-hidden="true"
		/>
		<p class="hero-kicker text-center">A new seal</p>
		<h1 class="mt-2 text-center h2">Choose a new password</h1>

		{#if message}
			<p class="mt-4 border border-success-500/40 bg-success-950/40 p-3 text-sm text-success-500">
				{message}
			</p>
		{/if}
		{#if errorMessage}
			<p class="mt-4 border border-error-500/40 bg-error-950/40 p-3 text-sm text-error-500">
				{errorMessage}
			</p>
		{/if}

		{#if token}
			<form class="mt-6 space-y-4" onsubmit={submit}>
				<label class="label">
					<span>New password</span>
					<div class="input-icon">
						<Lock size={16} aria-hidden="true" />
						<input
							class="input"
							type="password"
							bind:value={password}
							required
							autocomplete="new-password"
						/>
					</div>
				</label>
				<label class="label">
					<span>Confirm new password</span>
					<div class="input-icon">
						<ShieldCheck size={16} aria-hidden="true" />
						<input
							class="input"
							type="password"
							bind:value={confirmPassword}
							required
							autocomplete="new-password"
						/>
					</div>
				</label>
				<button class="btn w-full preset-filled-primary-500" type="submit" disabled={submitting}>
					<KeyRound size={16} aria-hidden="true" />
					{submitting ? 'Saving…' : 'Set new password'}
				</button>
			</form>
		{:else}
			<p class="mt-4 text-center text-sm text-surface-600-400">
				Request a new link from the <a href={resolve('/forgot-password')}>forgot password</a> page.
			</p>
		{/if}
	</div>
</div>
