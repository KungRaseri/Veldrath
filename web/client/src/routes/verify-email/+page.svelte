<script lang="ts">
	import { resolve } from '$app/paths';
	import { CircleAlert, CircleCheck, Mail, Send } from '@lucide/svelte';
	import { authClient } from '#lib/auth-client';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	let email = $state('');
	let resendMessage = $state('');
	let resendError = $state('');
	let sending = $state(false);

	async function resend(event: SubmitEvent) {
		event.preventDefault();
		resendError = '';
		resendMessage = '';
		sending = true;

		try {
			const { error } = await authClient.sendVerificationEmail({
				email,
				callbackURL: `${window.location.origin}/account`
			});

			if (error) {
				resendError = error.message ?? 'Could not send a new verification email.';
				return;
			}

			resendMessage =
				'If an account exists for that address, a new confirmation link has been sent.';
		} finally {
			sending = false;
		}
	}
</script>

<svelte:head>
	<title>Verify email — Veldrath</title>
</svelte:head>

<div class="mx-auto max-w-md px-5 py-12">
	<div class="auth-card p-8">
		<img
			class="auth-crest"
			src="/images/veldrath-final-crest-transparent.png"
			alt=""
			aria-hidden="true"
		/>

		{#if data.status === 'verified'}
			<CircleCheck class="mx-auto text-success-500" size={38} aria-hidden="true" />
			<h1 class="mt-3 text-center h2">Email verified</h1>
			<p class="mt-2 text-center text-sm text-surface-600-400">
				Your email address is confirmed. You can now sign in and enter Veldrath.
			</p>
		{:else if data.status === 'missing'}
			<CircleAlert class="mx-auto text-warning-500" size={38} aria-hidden="true" />
			<h1 class="mt-3 text-center h2">Verification link incomplete</h1>
			<p class="mt-2 text-center text-sm text-surface-600-400">
				This link is missing its token. Request a new verification email below.
			</p>
		{:else}
			<CircleAlert class="mx-auto text-error-500" size={38} aria-hidden="true" />
			<h1 class="mt-3 text-center h2">Verification failed</h1>
			<p class="mt-2 text-center text-sm text-surface-600-400">
				This verification link is invalid or has expired. Request a new one below.
			</p>
		{/if}

		{#if data.status !== 'verified'}
			{#if resendMessage}
				<p class="mt-4 border border-success-500/40 bg-success-950/40 p-3 text-sm text-success-500">
					{resendMessage}
				</p>
			{/if}
			{#if resendError}
				<p class="mt-4 border border-error-500/40 bg-error-950/40 p-3 text-sm text-error-500">
					{resendError}
				</p>
			{/if}

			<form class="mt-6 space-y-4" onsubmit={resend}>
				<label class="label">
					<span>Email</span>
					<div class="input-icon">
						<Mail size={16} aria-hidden="true" />
						<input class="input" type="email" bind:value={email} required autocomplete="email" />
					</div>
				</label>
				<button class="btn w-full preset-filled-primary-500" type="submit" disabled={sending}>
					<Send size={16} aria-hidden="true" />
					{sending ? 'Sending…' : 'Send a new verification link'}
				</button>
			</form>
		{/if}

		<div class="mt-6 flex justify-center gap-2">
			<a class="btn preset-filled-primary-500" href={resolve('/login')}>Sign in</a>
			<a class="btn preset-tonal-surface" href={resolve('/account')}>Account</a>
		</div>
	</div>
</div>
