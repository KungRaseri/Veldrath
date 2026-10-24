<script lang="ts">
	import { authClient } from '$lib/auth-client';

	type Provider = 'discord' | 'google' | 'microsoft';

	const providers: { id: Provider; label: string }[] = [
		{ id: 'discord', label: 'Discord' },
		{ id: 'google', label: 'Google' },
		{ id: 'microsoft', label: 'Microsoft' }
	];

	let pending = $state<Provider | null>(null);
	let errorMessage = $state('');

	async function signIn(provider: Provider) {
		pending = provider;
		errorMessage = '';

		try {
			const { data, error } = await authClient.signIn.social({
				provider,
				callbackURL: `${window.location.origin}/characters`,
				errorCallbackURL: `${window.location.origin}/login`
			});

			if (error) {
				errorMessage = error.message ?? `${provider} sign-in failed.`;
				return;
			}

			if (data?.url) {
				window.location.href = data.url;
			}
		} catch (error) {
			errorMessage = error instanceof Error ? error.message : `${provider} sign-in failed.`;
		} finally {
			pending = null;
		}
	}
</script>

<div class="space-y-2">
	{#each providers as provider (provider.id)}
		<button
			class="btn w-full preset-tonal-surface"
			type="button"
			onclick={() => signIn(provider.id)}
			disabled={pending !== null}
		>
			{pending === provider.id
				? `Connecting to ${provider.label}…`
				: `Continue with ${provider.label}`}
		</button>
	{/each}

	{#if errorMessage}
		<p class="text-sm text-error-500">{errorMessage}</p>
	{/if}
</div>
