<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { authClient } from '$lib/auth-client';

	let email = $state('');
	let password = $state('');
	let errorMessage = $state('');
	let submitting = $state(false);

	async function submit(event: SubmitEvent) {
		event.preventDefault();
		submitting = true;
		errorMessage = '';

		try {
			const { error } = await authClient.signIn.email({ email, password });
			if (error) {
				errorMessage = error.message ?? 'Sign-in failed.';
				return;
			}

			await goto(resolve('/game'));
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

		<form class="space-y-4" onsubmit={submit}>
			<label class="label">
				<span>Email</span>
				<input class="input" type="email" bind:value={email} required autocomplete="email" />
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

			<button class="btn w-full preset-filled-primary-500" type="submit" disabled={submitting}>
				{submitting ? 'Signing in…' : 'Sign in'}
			</button>
		</form>

		<p class="text-sm text-surface-600-400">
			No account? <a class="anchor" href={resolve('/register')}>Create one</a>
		</p>
	</div>
</div>
