<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { authClient } from '$lib/auth-client';
	import SocialLoginButtons from '$lib/components/auth/SocialLoginButtons.svelte';

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

<div class="mx-auto max-w-md p-6">
	<div class="space-y-4 card p-6">
		<h1 class="h2">Sign in</h1>

		{#if errorMessage}
			<p class="text-sm text-error-500">{errorMessage}</p>
		{/if}

		<SocialLoginButtons />

		<div class="flex items-center gap-3">
			<span class="h-px flex-1 bg-surface-200-800"></span>
			<span class="text-xs tracking-wide text-surface-600-400 uppercase"
				>or continue with email or username</span
			>
			<span class="h-px flex-1 bg-surface-200-800"></span>
		</div>

		<form class="space-y-4" onsubmit={submit}>
			<label class="label">
				<span>Email or username</span>
				<input class="input" type="text" bind:value={identifier} required autocomplete="username" />
			</label>

			<label class="label">
				<span>Password</span>
				<input
					class="input"
					type="password"
					bind:value={password}
					required
					autocomplete="current-password"
				/>
			</label>

			<div class="-mt-2 text-right">
				<a class="anchor text-sm" href={resolve('/forgot-password')}>Forgot password?</a>
			</div>

			<button class="btn w-full preset-filled-primary-500" type="submit" disabled={submitting}>
				{submitting ? 'Signing in…' : 'Sign in'}
			</button>
		</form>

		<p class="text-sm text-surface-600-400">
			No account? <a class="anchor" href={resolve('/register')}>Create one</a>
		</p>
	</div>
</div>
