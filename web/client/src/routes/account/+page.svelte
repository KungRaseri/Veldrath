<script lang="ts">
	import { goto, invalidateAll } from '$app/navigation';
	import { resolve } from '$app/paths';
	import {
		KeyRound,
		Link2,
		LogOut,
		Mail,
		MonitorSmartphone,
		Pencil,
		Plus,
		RefreshCw,
		Save,
		Send,
		Unlink,
		User,
		Users,
		X
	} from '@lucide/svelte';
	import { authClient } from '#lib/auth-client';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	type ProviderId = 'discord' | 'google' | 'microsoft';

	const providerLabels: Record<string, string> = {
		credential: 'Email & password',
		discord: 'Discord',
		google: 'Google',
		microsoft: 'Microsoft'
	};

	// ── Profile ────────────────────────────────────────────────────────────────
	let displayName = $state('');
	let username = $state('');

	$effect(() => {
		displayName = data.user.name;
		username = data.user.username ?? data.user.name;
	});

	let profileMessage = $state('');
	let profileError = $state('');
	let savingProfile = $state(false);

	async function syncGameIdentity(profile?: {
		username?: string;
		displayName?: string;
	}): Promise<void> {
		await fetch('/api/foundry-bridge/account', {
			method: 'POST',
			headers: { 'content-type': 'application/json' },
			body: JSON.stringify(profile ?? {})
		}).catch(() => undefined);
	}

	async function saveProfile(event: SubmitEvent) {
		event.preventDefault();
		profileError = '';
		profileMessage = '';

		const name = displayName.trim();
		const handle = username.trim();
		if (!name) {
			profileError = 'A display name is required.';
			return;
		}

		savingProfile = true;
		try {
			const { error } = await authClient.updateUser({ name, username: handle });
			if (error) {
				profileError = error.message ?? 'Could not update the profile.';
				return;
			}

			await syncGameIdentity({ username: handle, displayName: name });
			profileMessage = 'Profile updated.';
			await invalidateAll();
		} finally {
			savingProfile = false;
		}
	}

	// ── Email ──────────────────────────────────────────────────────────────────
	let newEmail = $state('');
	let emailMessage = $state('');
	let emailError = $state('');
	let savingEmail = $state(false);
	let verificationMessage = $state('');
	let verificationError = $state('');
	let sendingVerification = $state(false);

	async function requestEmailChange(event: SubmitEvent) {
		event.preventDefault();
		emailError = '';
		emailMessage = '';

		const value = newEmail.trim();
		if (!value) {
			emailError = 'Enter the new email address.';
			return;
		}

		savingEmail = true;
		try {
			const { error } = await authClient.changeEmail({
				newEmail: value,
				callbackURL: `${window.location.origin}/account`
			});

			if (error) {
				emailError = error.message ?? 'Could not start the email change.';
				return;
			}

			newEmail = '';
			emailMessage =
				'Check the new inbox for a confirmation link. Your address changes once you click it.';
		} finally {
			savingEmail = false;
		}
	}

	async function resendVerification() {
		verificationError = '';
		verificationMessage = '';
		sendingVerification = true;

		try {
			const { error } = await authClient.sendVerificationEmail({
				email: data.user.email,
				callbackURL: `${window.location.origin}/account`
			});

			if (error) {
				verificationError = error.message ?? 'Could not send the verification email.';
				return;
			}

			verificationMessage = 'A new confirmation link has been sent to your inbox.';
		} finally {
			sendingVerification = false;
		}
	}

	// ── Linked sign-in methods ─────────────────────────────────────────────────
	let providerMessage = $state('');
	let providerError = $state('');
	let pendingProvider = $state<string | null>(null);

	function oauthErrorMessage(code: string): string {
		switch (code) {
			case 'access_denied':
				return 'The provider sign-in was cancelled.';
			case 'account_not_linked':
				return 'That provider account is not linked to this Veldrath account.';
			default:
				return 'Linking failed. Make sure the provider account uses the same email address as this account.';
		}
	}

	function linkedProvider(providerId: string) {
		return data.providers.find((provider) => provider.providerId === providerId);
	}

	async function linkProvider(provider: ProviderId) {
		providerError = '';
		providerMessage = '';
		pendingProvider = provider;

		try {
			const { data: result, error } = await authClient.linkSocial({
				provider,
				callbackURL: `${window.location.origin}/account`,
				errorCallbackURL: `${window.location.origin}/account`
			});

			if (error) {
				providerError = error.message ?? 'Could not link that provider.';
				return;
			}

			if (result?.url) {
				window.location.href = result.url;
			}
		} finally {
			pendingProvider = null;
		}
	}

	async function unlinkProvider(accountId: string, label: string) {
		if (!window.confirm(`Unlink ${label} from your Veldrath account?`)) {
			return;
		}

		providerError = '';
		providerMessage = '';
		pendingProvider = label;

		try {
			const { error } = await authClient.unlinkAccount({ accountId });
			if (error) {
				providerError = error.message ?? 'Could not unlink that provider.';
				return;
			}

			await syncGameIdentity();
			providerMessage = `${label} unlinked.`;
			await invalidateAll();
		} finally {
			pendingProvider = null;
		}
	}

	// ── Sessions ───────────────────────────────────────────────────────────────
	let sessionMessage = $state('');
	let sessionError = $state('');
	let revokingToken = $state<string | null>(null);
	let revokingOthers = $state(false);

	function describeSession(userAgent: string | null): string {
		if (!userAgent) {
			return 'Unknown device';
		}

		return userAgent.length > 90 ? `${userAgent.slice(0, 90)}…` : userAgent;
	}

	async function revokeSession(token: string) {
		sessionError = '';
		sessionMessage = '';
		revokingToken = token;

		try {
			const { error } = await authClient.revokeSession({ token });
			if (error) {
				sessionError = error.message ?? 'Could not revoke that session.';
				return;
			}

			sessionMessage = 'Session revoked.';
			await invalidateAll();
		} finally {
			revokingToken = null;
		}
	}

	async function revokeOtherSessions() {
		sessionError = '';
		sessionMessage = '';
		revokingOthers = true;

		try {
			const { error } = await authClient.revokeOtherSessions();
			if (error) {
				sessionError = error.message ?? 'Could not revoke the other sessions.';
				return;
			}

			sessionMessage = 'All other sessions have been signed out.';
			await invalidateAll();
		} finally {
			revokingOthers = false;
		}
	}

	// ── Password ───────────────────────────────────────────────────────────────
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

			// Keep the temporary Identity login in sync with the new password.
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
		<h1 class="text-title-large flex items-center gap-3">
			<img
				class="h-11 w-11 object-contain"
				src="/images/veldrath-final-crest-transparent.png"
				alt=""
				aria-hidden="true"
			/>
			Account
		</h1>
		<p class="text-surface-600-400">Manage your Veldrath account.</p>
	</header>

	<section class="space-y-3 card p-6">
		<h2 class="flex items-center gap-2 h3">
			<User class="text-secondary-500" size={18} aria-hidden="true" />
			Profile
		</h2>
		<dl class="grid gap-2 text-sm sm:grid-cols-2">
			<div>
				<dt class="text-surface-600-400">Username</dt>
				<dd>{data.user.username ?? data.user.name}</dd>
			</div>
			<div>
				<dt class="text-surface-600-400">Display name</dt>
				<dd>{data.user.name}</dd>
			</div>
			<div>
				<dt class="text-surface-600-400">Email</dt>
				<dd class="flex flex-wrap items-center gap-2">
					<span>{data.user.email}</span>
					{#if data.user.emailVerified}
						<span class="chip preset-filled-success-500 text-xs">Verified</span>
					{:else}
						<span class="preset-tonal-warning-500 chip text-xs">Unverified</span>
					{/if}
				</dd>
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

		{#if !data.user.emailVerified}
			{#if verificationMessage}
				<p class="text-sm text-success-500">{verificationMessage}</p>
			{/if}
			{#if verificationError}
				<p class="text-sm text-error-500">{verificationError}</p>
			{/if}
			<button
				class="btn preset-tonal-surface"
				type="button"
				onclick={resendVerification}
				disabled={sendingVerification}
			>
				<RefreshCw size={15} aria-hidden="true" />
				{sendingVerification ? 'Sending…' : 'Resend verification email'}
			</button>
		{/if}
	</section>

	<section class="space-y-3 card p-6">
		<h2 class="flex items-center gap-2 h3">
			<Pencil class="text-secondary-500" size={18} aria-hidden="true" />
			Edit profile
		</h2>
		{#if profileMessage}
			<p class="text-sm text-success-500">{profileMessage}</p>
		{/if}
		{#if profileError}
			<p class="text-sm text-error-500">{profileError}</p>
		{/if}

		<form class="grid gap-3 sm:grid-cols-2" onsubmit={saveProfile}>
			<label class="label">
				<span>Display name</span>
				<input
					class="input"
					type="text"
					bind:value={displayName}
					required
					autocomplete="nickname"
				/>
			</label>
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
			</label>
			<div class="sm:col-span-2">
				<button class="btn preset-filled-primary-500" type="submit" disabled={savingProfile}>
					<Save size={15} aria-hidden="true" />
					{savingProfile ? 'Saving…' : 'Save profile'}
				</button>
			</div>
		</form>
	</section>

	<section class="space-y-3 card p-6">
		<h2 class="flex items-center gap-2 h3">
			<Mail class="text-secondary-500" size={18} aria-hidden="true" />
			Email address
		</h2>
		{#if emailMessage}
			<p class="text-sm text-success-500">{emailMessage}</p>
		{/if}
		{#if emailError}
			<p class="text-sm text-error-500">{emailError}</p>
		{/if}

		<form class="grid gap-3 sm:grid-cols-2" onsubmit={requestEmailChange}>
			<label class="label sm:col-span-2">
				<span>New email address</span>
				<input class="input" type="email" bind:value={newEmail} required autocomplete="email" />
			</label>
			<div class="sm:col-span-2">
				<button class="btn preset-filled-primary-500" type="submit" disabled={savingEmail}>
					<Send size={15} aria-hidden="true" />
					{savingEmail ? 'Sending…' : 'Send confirmation link'}
				</button>
			</div>
		</form>
	</section>

	<section class="space-y-3 card p-6">
		<h2 class="flex items-center gap-2 h3">
			<Link2 class="text-secondary-500" size={18} aria-hidden="true" />
			Linked sign-in methods
		</h2>
		{#if data.oauthError}
			<p class="text-sm text-error-500">{oauthErrorMessage(data.oauthError)}</p>
		{/if}
		{#if providerMessage}
			<p class="text-sm text-success-500">{providerMessage}</p>
		{/if}
		{#if providerError}
			<p class="text-sm text-error-500">{providerError}</p>
		{/if}

		{#if data.providers.length === 0}
			<p class="text-sm text-surface-600-400">No linked providers found.</p>
		{:else}
			<ul class="space-y-2">
				{#each data.providers as provider (provider.id)}
					<li class="flex flex-wrap items-center justify-between gap-2">
						<span class="chip preset-tonal-surface text-xs">
							{providerLabels[provider.providerId] ?? provider.providerId}
						</span>
						{#if provider.providerId !== 'credential'}
							<button
								class="preset-tonal-error-500 btn text-xs"
								type="button"
								onclick={() =>
									unlinkProvider(
										provider.id,
										providerLabels[provider.providerId] ?? provider.providerId
									)}
								disabled={pendingProvider !== null}
							>
								<Unlink size={13} aria-hidden="true" />
								Unlink
							</button>
						{/if}
					</li>
				{/each}
			</ul>
		{/if}

		{#if data.linkableProviders.length > 0}
			<div class="flex flex-wrap gap-2 border-t border-surface-200-800 pt-3">
				{#each data.linkableProviders as provider (provider.id)}
					{#if !linkedProvider(provider.id)}
						<button
							class="btn preset-tonal-surface text-sm"
							type="button"
							onclick={() => linkProvider(provider.id as ProviderId)}
							disabled={pendingProvider !== null}
						>
							<Plus size={14} aria-hidden="true" />
							{pendingProvider === provider.id
								? `Connecting ${provider.label}…`
								: `Link ${provider.label}`}
						</button>
					{/if}
				{/each}
			</div>
		{/if}
	</section>

	<section class="space-y-3 card p-6">
		<div class="flex flex-wrap items-center justify-between gap-2">
			<h2 class="flex items-center gap-2 h3">
				<MonitorSmartphone class="text-secondary-500" size={18} aria-hidden="true" />
				Active sessions
			</h2>
			{#if data.sessions.length > 1}
				<button
					class="btn preset-tonal-surface text-sm"
					type="button"
					onclick={revokeOtherSessions}
					disabled={revokingOthers}
				>
					<LogOut size={14} aria-hidden="true" />
					{revokingOthers ? 'Signing out…' : 'Sign out other sessions'}
				</button>
			{/if}
		</div>

		{#if sessionMessage}
			<p class="text-sm text-success-500">{sessionMessage}</p>
		{/if}
		{#if sessionError}
			<p class="text-sm text-error-500">{sessionError}</p>
		{/if}

		{#if data.sessions.length === 0}
			<p class="text-sm text-surface-600-400">No active sessions found.</p>
		{:else}
			<ul class="space-y-2">
				{#each data.sessions as session (session.id)}
					<li class="flex flex-wrap items-center justify-between gap-2 rounded p-2">
						<div class="text-sm">
							<p class="flex items-center gap-2">
								<span>{describeSession(session.userAgent)}</span>
								{#if session.id === data.currentSessionId}
									<span class="chip preset-filled-primary-500 text-xs">This device</span>
								{/if}
							</p>
							<p class="text-xs text-surface-600-400">
								{session.ipAddress ?? 'Unknown IP'} · started
								{new Date(session.createdAt).toLocaleString()} · expires
								{new Date(session.expiresAt).toLocaleDateString()}
							</p>
						</div>
						{#if session.id !== data.currentSessionId}
							<button
								class="preset-tonal-error-500 btn text-xs"
								type="button"
								onclick={() => revokeSession(session.token)}
								disabled={revokingToken !== null}
							>
								<X size={13} aria-hidden="true" />
								{revokingToken === session.token ? 'Revoking…' : 'Revoke'}
							</button>
						{/if}
					</li>
				{/each}
			</ul>
		{/if}
	</section>

	<section class="space-y-3 card p-6">
		<h2 class="flex items-center gap-2 h3">
			<KeyRound class="text-secondary-500" size={18} aria-hidden="true" />
			Change password
		</h2>
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
					<KeyRound size={15} aria-hidden="true" />
					{savingPassword ? 'Saving…' : 'Update password'}
				</button>
				<a class="btn preset-tonal-surface" href={resolve('/characters')}>
					<Users size={15} aria-hidden="true" />
					Characters
				</a>
				<button class="btn preset-tonal-surface" type="button" onclick={signOut}>
					<LogOut size={15} aria-hidden="true" />
					Sign out
				</button>
			</div>
		</form>
	</section>
</div>
