<script lang="ts">
	import { resolve } from '$app/paths';
	import { Mail, Send } from '@lucide/svelte';
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

<div class="mx-auto max-w-md px-5 py-12">
	<div class="auth-card p-8">
		<img
			class="auth-crest"
			src="/images/veldrath-final-crest-transparent.png"
			alt=""
			aria-hidden="true"
		/>
		<p class="hero-kicker text-center">Recover your account</p>
		<h1 class="mt-2 text-center h2">Forgot your password?</h1>
		<p class="mt-2 text-center text-sm text-surface-600-400">
			Enter the email address on your account and we’ll send a reset link.
		</p>

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

		<form class="mt-6 space-y-4" onsubmit={submit}>
			<label class="label">
				<span>Email</span>
				<div class="input-icon">
					<Mail size={16} aria-hidden="true" />
					<input class="input" type="email" bind:value={email} required autocomplete="email" />
				</div>
			</label>
			<button class="btn w-full preset-filled-primary-500" type="submit" disabled={submitting}>
				<Send size={16} aria-hidden="true" />
				{submitting ? 'Sending…' : 'Send reset link'}
			</button>
		</form>

		<p class="mt-6 text-center text-sm text-surface-600-400">
			<a href={resolve('/login')}>Back to sign in</a>
		</p>
	</div>
</div>
