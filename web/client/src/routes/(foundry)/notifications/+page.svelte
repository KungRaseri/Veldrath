<script lang="ts">
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();
</script>

<svelte:head>
	<title>Notifications — Foundry</title>
</svelte:head>

<div class="mx-auto max-w-3xl space-y-4 p-6">
	<header class="space-y-1">
		<h1 class="h1">Notifications</h1>
		<p class="text-surface-600-400">Review outcomes for your community submissions.</p>
	</header>

	{#if form?.error || data.error}
		<p class="text-sm text-error-500">{form?.error ?? data.error}</p>
	{/if}

	{#if data.notifications.length === 0}
		<p class="card p-4 text-surface-600-400">No notifications yet.</p>
	{:else}
		<ul class="space-y-2">
			{#each data.notifications as notification (notification.id)}
				<li class="flex flex-wrap items-start justify-between gap-3 card p-4">
					<div class="space-y-1">
						<p class="text-sm font-medium">
							<a class="anchor" href={`/submissions/${notification.submissionId}`}>
								{notification.submissionTitle}
							</a>
						</p>
						<p class="text-sm text-surface-600-400">{notification.message}</p>
						<p class="text-xs text-surface-600-400">
							{new Date(notification.createdAt).toLocaleString()}
						</p>
					</div>
					{#if notification.isRead}
						<span class="chip preset-tonal-surface text-xs">Read</span>
					{:else}
						<form method="POST" action="?/read">
							<input type="hidden" name="id" value={notification.id} />
							<button class="btn preset-tonal-surface btn-sm" type="submit">Mark read</button>
						</form>
					{/if}
				</li>
			{/each}
		</ul>
	{/if}
</div>
