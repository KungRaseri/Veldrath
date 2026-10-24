<script lang="ts">
	import { resolve } from '$app/paths';
	import type { ActionData } from './$types';

	let { form }: { form: ActionData } = $props();
</script>

<svelte:head>
	<title>Server commands — Admin</title>
</svelte:head>

<div class="mx-auto max-w-3xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={resolve('/(admin)/admin')}>← Admin</a>

	<header class="space-y-1">
		<h1 class="h1">Server commands</h1>
		<p class="text-surface-600-400">
			Broadcast an announcement to every connected game client. Requires the admin.announce
			permission.
		</p>
	</header>

	{#if form?.error}
		<p class="text-sm text-error-500">{form.error}</p>
	{:else if form?.message}
		<p class="text-sm text-success-500">{form.message}</p>
	{/if}

	<form class="space-y-4 card p-6" method="POST" action="?/broadcast">
		<label class="label">
			<span>Message</span>
			<textarea class="textarea" name="message" rows="4" required></textarea>
		</label>
		<label class="label max-w-xs">
			<span>Severity</span>
			<select class="select" name="severity">
				<option value="info">Info</option>
				<option value="warning">Warning</option>
				<option value="critical">Critical</option>
			</select>
		</label>
		<button class="btn preset-filled-primary-500" type="submit">Broadcast announcement</button>
	</form>

	<a class="text-sm text-primary-500" href={resolve('/(admin)/admin/status')}>← Server status</a>
</div>
