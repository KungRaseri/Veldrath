<script lang="ts">
	import { onDestroy } from 'svelte';
	import type { HubConnection } from '@microsoft/signalr';
	import { goto } from '$app/navigation';
	import type { PageData } from './$types';
	import { authClient } from '$lib/auth-client';
	import { createGameHub } from '$lib/hub';

	let { data }: { data: PageData } = $props();
	let tokenPreview = $state('');
	let tokenError = $state('');
	let loadingToken = $state(false);
	let hubState = $state<'idle' | 'connecting' | 'connected' | 'failed'>('idle');
	let hubMessage = $state('');
	let connection: HubConnection | undefined;

	async function fetchGameToken() {
		loadingToken = true;
		tokenError = '';
		tokenPreview = '';

		try {
			const response = await fetch('/api/game-token');
			if (!response.ok) {
				tokenError = `Token request failed (${response.status}).`;
				return;
			}

			const payload = (await response.json()) as { token: string; expiresIn: number };
			tokenPreview = `${payload.token.slice(0, 32)}… (${payload.expiresIn}s)`;
		} catch (error) {
			tokenError = error instanceof Error ? error.message : 'Token request failed.';
		} finally {
			loadingToken = false;
		}
	}

	async function connectHub() {
		hubState = 'connecting';
		hubMessage = '';

		try {
			if (connection) {
				await connection.stop();
				connection = undefined;
			}

			connection = createGameHub();
			connection.on('ServerInfo', (payload: { connectionId: string; serverVersion: string }) => {
				hubMessage = `Connected as ${payload.connectionId} (server ${payload.serverVersion}).`;
				hubState = 'connected';
			});

			await connection.start();
			hubState = 'connected';
		} catch (error) {
			hubState = 'failed';
			hubMessage = error instanceof Error ? error.message : 'SignalR connection failed.';
		}
	}

	async function disconnectHub() {
		if (connection) {
			await connection.stop();
			connection = undefined;
		}
		hubState = 'idle';
		hubMessage = '';
	}

	async function signOut() {
		await disconnectHub();
		await authClient.signOut();
		await goto('/');
	}

	onDestroy(() => {
		void connection?.stop();
	});
</script>

<svelte:head>
	<title>Game — Veldrath</title>
</svelte:head>

<div class="mx-auto max-w-3xl space-y-6 p-6">
	<div class="card space-y-3 p-6">
		<h1 class="h2">Welcome, {data.user.username ?? data.user.name}</h1>
		<p class="text-sm text-surface-600-400">Account ID: {data.user.id}</p>
		<p class="text-sm text-surface-600-400">Email: {data.user.email}</p>

		<div class="flex flex-wrap gap-2">
			<button class="btn preset-filled-primary-500" onclick={fetchGameToken} disabled={loadingToken}>
				{loadingToken ? 'Minting…' : 'Mint game token'}
			</button>
			<button
				class="btn preset-tonal-surface"
				onclick={connectHub}
				disabled={hubState === 'connecting' || hubState === 'connected'}
			>
				{hubState === 'connecting' ? 'Connecting…' : 'Connect to SignalR'}
			</button>
			<button class="btn preset-tonal-surface" onclick={disconnectHub} disabled={hubState === 'idle'}>
				Disconnect
			</button>
			<button class="btn preset-tonal-surface" onclick={signOut}>Sign out</button>
		</div>

		{#if tokenPreview}
			<p class="text-sm text-success-500">Token: {tokenPreview}</p>
		{/if}
		{#if tokenError}
			<p class="text-sm text-error-500">{tokenError}</p>
		{/if}
		{#if hubMessage}
			<p class={hubState === 'failed' ? 'text-sm text-error-500' : 'text-sm text-success-500'}>
				{hubMessage}
			</p>
		{/if}
	</div>

	<div class="card p-6">
		<h2 class="h3">Phase 0 checkpoint</h2>
		<p class="mt-2 text-sm text-surface-600-400">
			Authentication is working. Mint a token and connect to SignalR to validate the
			server-to-browser path. Character select and the map are the next slice.
		</p>
	</div>
</div>
