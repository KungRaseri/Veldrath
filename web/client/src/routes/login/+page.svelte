<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { Lock, LogIn, Mail } from '@lucide/svelte';
	import SocialLoginButtons from '#lib/components/auth/SocialLoginButtons.svelte';
	import { authClient } from '#lib/auth-client';

	let identifier = $state('');
	let password = $state('');
	let errorMessage = $state(oauthErrorMessage(page.url.searchParams.get('error')));
	let submitting = $state(false);

	function oauthErrorMessage(code: string | null): string {
		switch (code) {
			case null:
			case '':
				return '';
			case 'access_denied':
				return 'Sign-in was cancelled.';
			case 'account_not_linked':
				return 'That provider is not linked to this account. Sign in with your password, then link it from the account page.';
			default:
				return 'Social sign-in failed. Try again or continue with email and password.';
		}
	}

	async function submit(event: SubmitEvent) {
		event.preventDefault();
		submitting = true;
		errorMessage = '';

		const value = identifier.trim();

		try {
			const result = value.includes('@')
				? await authClient.signIn.email({ email: value, password })
				: await authClient.signIn.username({ username: value, password });

			if (result.error) {
				errorMessage = result.error.message ?? 'Sign-in failed.';
				return;
			}

			await goto(resolve('/characters'));
		} finally {
			submitting = false;
		}
	}
</script>

<svelte:head>
	<title>Sign in — Veldrath</title>
</svelte:head>

<div class="mx-auto max-w-md px-5 py-12">
	<div class="auth-card p-8">
		<img
			class="auth-crest"
			src="/images/veldrath-final-crest-transparent.png"
			alt=""
			aria-hidden="true"
		/>
		<p class="hero-kicker text-center">Return to the world</p>
		<h1 class="mt-2 text-center h2">Sign in</h1>
		<p class="mt-2 text-center text-sm text-surface-600-400">
			Sign in with your Veldrath account or a connected provider.
		</p>

		{#if errorMessage}
			<p class="mt-4 border border-error-500/40 bg-error-950/40 p-3 text-sm text-error-500">
				{errorMessage}
			</p>
		{/if}

		<form class="mt-6 space-y-4" onsubmit={submit}>
			<label class="label">
				<span>Email or username</span>
				<div class="input-icon">
					<Mail size={16} aria-hidden="true" />
					<input
						class="input"
						type="text"
						bind:value={identifier}
						required
						autocomplete="username"
					/>
				</div>
			</label>

			<label class="label">
				<span>Password</span>
				<div class="input-icon">
					<Lock size={16} aria-hidden="true" />
					<input
						class="input"
						type="password"
						bind:value={password}
						required
						autocomplete="current-password"
					/>
				</div>
			</label>

			<div class="-mt-1 text-right">
				<a class="text-sm" href={resolve('/forgot-password')}>Forgot password?</a>
			</div>

			<button class="btn w-full preset-filled-primary-500" type="submit" disabled={submitting}>
				<LogIn size={17} aria-hidden="true" />
				{submitting ? 'Signing in…' : 'Sign in'}
			</button>
		</form>

		<div class="oauth-divider my-5">or continue with</div>
		<SocialLoginButtons />

		<p class="mt-6 text-center text-sm text-surface-600-400">
			No account? <a href={resolve('/register')}>Create one</a>
		</p>
	</div>
</div>
