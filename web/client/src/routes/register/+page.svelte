<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { Lock, Mail, ShieldCheck, User, UserPlus } from '@lucide/svelte';
	import SocialLoginButtons from '$lib/components/auth/SocialLoginButtons.svelte';
	import { authClient } from '$lib/auth-client';

	let username = $state('');
	let email = $state('');
	let password = $state('');
	let confirmPassword = $state('');
	let errorMessage = $state('');
	let submitting = $state(false);

	async function submit(event: SubmitEvent) {
		event.preventDefault();
		errorMessage = '';

		if (password !== confirmPassword) {
			errorMessage = 'Passwords do not match.';
			return;
		}

		submitting = true;

		try {
			const { error } = await authClient.signUp.email({
				email,
				password,
				name: username,
				username
			});

			if (error) {
				errorMessage = error.message ?? 'Registration failed.';
				return;
			}

			// Temporary desktop bridge: mirror the password hash into ASP.NET Identity.
			// A failure here must not block registration; the next authenticated request
			// performs an account-only sync through hooks.server.ts.
			await fetch('/api/foundry-bridge/register', {
				method: 'POST',
				headers: { 'content-type': 'application/json' },
				body: JSON.stringify({ password })
			}).catch(() => undefined);

			await goto(resolve('/game'));
		} finally {
			submitting = false;
		}
	}
</script>

<svelte:head>
	<title>Create account — Veldrath</title>
</svelte:head>

<div class="mx-auto max-w-md px-5 py-12">
	<div class="auth-card p-8">
		<img
			class="auth-crest"
			src="/images/veldrath-final-crest-transparent.png"
			alt=""
			aria-hidden="true"
		/>
		<p class="hero-kicker text-center">Begin your legend</p>
		<h1 class="mt-2 text-center h2">Create account</h1>
		<p class="mt-2 text-center text-sm text-surface-600-400">
			Claim your place in Veldrath. Your first character is only a few steps away.
		</p>

		{#if errorMessage}
			<p class="mt-4 border border-error-500/40 bg-error-950/40 p-3 text-sm text-error-500">
				{errorMessage}
			</p>
		{/if}

		<form class="mt-6 space-y-4" onsubmit={submit}>
			<label class="label">
				<span>Username</span>
				<div class="input-icon">
					<User size={16} aria-hidden="true" />
					<input
						class="input"
						type="text"
						bind:value={username}
						required
						minlength="3"
						maxlength="30"
						autocomplete="username"
					/>
				</div>
				<span class="text-xs text-surface-600-400">
					3–30 characters: letters, numbers, dots, and underscores.
				</span>
			</label>

			<label class="label">
				<span>Email</span>
				<div class="input-icon">
					<Mail size={16} aria-hidden="true" />
					<input class="input" type="email" bind:value={email} required autocomplete="email" />
				</div>
				<span class="text-xs text-surface-600-400">
					We’ll send a confirmation link so you can verify this address.
				</span>
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
						autocomplete="new-password"
					/>
				</div>
			</label>

			<label class="label">
				<span>Confirm password</span>
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
				<UserPlus size={17} aria-hidden="true" />
				{submitting ? 'Creating account…' : 'Create account'}
			</button>
		</form>

		<div class="oauth-divider my-5">or sign up with</div>
		<SocialLoginButtons />

		<p class="mt-6 text-center text-sm text-surface-600-400">
			Already have an account? <a href={resolve('/login')}>Sign in</a>
		</p>
	</div>
</div>
