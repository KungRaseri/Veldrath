<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { authClient } from '$lib/auth-client';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	let currentPassword = $state('');
	let newPassword = $state('');
	let confirmPassword = $state('');
	let passwordMessage = $state('');
	let passwordError = $state('');
	let savingPassword = $state(false);

	async function changePassword(event: SubmitEvent) {
		event.preventDefault();
		passwordError = '';
		passwordMessage = '';

		if (newPassword !== confirmPassword) {
			passwordError = 'The new passwords do not match.';
			return;
		}

		savingPassword = true;
		try {
			const { error } = await authClient.changePassword({
				currentPassword,
				newPassword,
				revokeOtherSessions: false
			});

			if (error) {
				passwordError = error.message ?? 'Could not change the password.';
				return;
			}

			// Keep the temporary Blazor Foundry login in sync with the new password.
			await fetch('/api/foundry-bridge/register', {
				method: 'POST',
				headers: { 'content-type': 'application/json' },
				body: JSON.stringify({ password: newPassword })
			}).catch(() => undefined);

			currentPassword = '';
			newPassword = '';
			confirmPassword = '';
			passwordMessage = 'Password updated.';
		} finally {
			savingPassword = false;
		}
	}

	async function signOut() {
		await authClient.signOut();
		await goto(resolve('/'));
	}
</script>

<svelte:head>
	<title>Account — Veldrath</title>
</svelte:head>

<div class="mx-auto max-w-3xl space-y-4 p-6">
	<header class="space-y-1">
		<h1 class="h1">Account</h1>
		<p class="text-surface-600-400">Manage your Veldrath account.</p>
	</header>

	<section class="space-y-3 card p-6">
		<h2 class="h3">Profile</h2>
		<dl class="grid gap-2 text-sm sm:grid-cols-2">
			<div>
				<dt class="text-surface-600-400">Username</dt>
				<dd>{data.user.username ?? data.user.name}</dd>
			</div>
			<div>
				<dt class="text-surface-600-400">Email</dt>
				<dd>{data.user.email}</dd>
			</div>
			<div>
				<dt class="text-surface-600-400">Email verified</dt>
				<dd>{data.user.emailVerified ? 'Yes' : 'No'}</dd>
			</div>
			<div>
				<dt class="text-surface-600-400">Created</dt>
				<dd>{new Date(data.user.createdAt).toLocaleDateString()}</dd>
			</div>
			<div class="sm:col-span-2">
				<dt class="text-surface-600-400">Account ID</dt>
				<dd class="font-mono text-xs break-all">{data.user.id}</dd>
			</div>
		</dl>
	</section>

	<section class="space-y-3 card p-6">
		<h2 class="h3">Linked sign-in methods</h2>
		{#if data.providers.length === 0}
			<p class="text-sm text-surface-600-400">No linked providers found.</p>
		{:else}
			<ul class="flex flex-wrap gap-2">
				{#each data.providers as provider (provider.providerId)}
					<li class="chip preset-tonal-surface text-xs">
						{provider.providerId === 'credential' ? 'Email & password' : provider.providerId}
					</li>
				{/each}
			</ul>
		{/if}
	</section>

	<section class="space-y-3 card p-6">
		<h2 class="h3">Change password</h2>
		{#if passwordError}
			<p class="text-sm text-error-500">{passwordError}</p>
		{/if}
		{#if passwordMessage}
			<p class="text-sm text-success-500">{passwordMessage}</p>
		{/if}

		<form class="grid gap-3 sm:grid-cols-2" onsubmit={changePassword}>
			<label class="label sm:col-span-2">
				<span>Current password</span>
				<input
					class="input"
					type="password"
					bind:value={currentPassword}
					required
					autocomplete="current-password"
				/>
			</label>
			<label class="label">
				<span>New password</span>
				<input
					class="input"
					type="password"
					bind:value={newPassword}
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
			<div class="flex flex-wrap gap-2 sm:col-span-2">
				<button class="btn preset-filled-primary-500" type="submit" disabled={savingPassword}>
					{savingPassword ? 'Saving…' : 'Update password'}
				</button>
				<a class="btn preset-tonal-surface" href={resolve('/characters')}>Characters</a>
				<button class="btn preset-tonal-surface" type="button" onclick={signOut}>Sign out</button>
			</div>
		</form>
	</section>
</div>
