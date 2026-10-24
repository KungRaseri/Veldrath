<script lang="ts">
	import { brandDiscordPath, brandGooglePath } from '$lib/brand-icons';
	import { authClient } from '$lib/auth-client';

	type Provider = 'discord' | 'google' | 'microsoft';

	const providers: {
		id: Provider;
		label: string;
		className: string;
		path?: string;
	}[] = [
		{ id: 'discord', label: 'Discord', className: 'oauth-discord', path: brandDiscordPath },
		{ id: 'google', label: 'Google', className: 'oauth-google', path: brandGooglePath },
		{ id: 'microsoft', label: 'Microsoft', className: 'oauth-microsoft' }
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
			class="oauth-button {provider.className}"
			type="button"
			onclick={() => signIn(provider.id)}
			disabled={pending !== null}
		>
			{#if provider.id === 'microsoft'}
				<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
					<path d="M3 3h8v8H3zM13 3h8v8h-8zM3 13h8v8H3zM13 13h8v8h-8z" />
				</svg>
			{:else if provider.path}
				<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
					<path d={provider.path} />
				</svg>
			{/if}
			<span>
				{pending === provider.id
					? `Connecting to ${provider.label}…`
					: `Continue with ${provider.label}`}
			</span>
		</button>
	{/each}

	{#if errorMessage}
		<p class="text-sm text-error-500">{errorMessage}</p>
	{/if}
</div>
