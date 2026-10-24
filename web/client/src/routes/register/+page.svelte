<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { authClient } from '$lib/auth-client';
	import SocialLoginButtons from '$lib/components/auth/SocialLoginButtons.svelte';

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

			// Temporary Blazor Foundry bridge: mirror the password hash into ASP.NET Identity.
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

<div class="mx-auto max-w-md p-6">
	<div class="space-y-4 card p-6">
		<h1 class="h2">Create account</h1>

		{#if errorMessage}
			<p class="text-sm text-error-500">{errorMessage}</p>
		{/if}

		<SocialLoginButtons />

		<div class="flex items-center gap-3">
			<span class="h-px flex-1 bg-surface-200-800"></span>
			<span class="text-xs tracking-wide text-surface-600-400 uppercase">or sign up with email</span
			>
			<span class="h-px flex-1 bg-surface-200-800"></span>
		</div>

		<form class="space-y-4" onsubmit={submit}>
			<label class="label">
				<span>Username</span>
				<input
					class="input"
					type="text"
					bind:value={username}
					required
					minlength="3"
					maxlength="30"
					autocomplete="username"
				/>
				<span class="text-xs text-surface-600-400">
					3–30 characters: letters, numbers, dots, and underscores.
				</span>
			</label>

			<label class="label">
				<span>Email</span>
				<input class="input" type="email" bind:value={email} required autocomplete="email" />
				<span class="text-xs text-surface-600-400">
					We’ll send a confirmation link so you can verify this address.
				</span>
			</label>

			<label class="label">
				<span>Password</span>
				<input
					class="input"
					type="password"
					bind:value={password}
					required
					autocomplete="new-password"
				/>
			</label>

			<label class="label">
				<span>Confirm password</span>
				<input
					class="input"
					type="password"
					bind:value={confirmPassword}
					required
					autocomplete="new-password"
				/>
			</label>

			<button class="btn w-full preset-filled-primary-500" type="submit" disabled={submitting}>
				{submitting ? 'Creating account…' : 'Create account'}
			</button>
		</form>

		<p class="text-sm text-surface-600-400">
			Already have an account? <a class="anchor" href={resolve('/login')}>Sign in</a>
		</p>
	</div>
</div>
