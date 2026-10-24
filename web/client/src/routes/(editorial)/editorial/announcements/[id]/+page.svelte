<script lang="ts">
	import { resolve } from '$app/paths';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();

	const announcement = $derived(data.announcement);
</script>

<svelte:head>
	<title
		>{data.mode === 'create' ? 'New announcement' : (announcement?.title ?? 'Announcement')} — Editorial</title
	>
</svelte:head>

<div class="mx-auto max-w-3xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={resolve('/(editorial)/editorial/announcements')}>
		← Announcements
	</a>

	{#if form?.error}
		<p class="text-sm text-error-500">{form.error}</p>
	{:else if form?.message}
		<p class="text-sm text-success-500">{form.message}</p>
	{/if}

	<header class="flex flex-wrap items-center justify-between gap-3">
		<h1 class="h1">{data.mode === 'create' ? 'New announcement' : 'Edit announcement'}</h1>
		{#if announcement}
			<span class="chip preset-tonal-surface text-xs">{announcement.status}</span>
		{/if}
	</header>

	<form class="space-y-4 card p-6" method="POST" action="?/save">
		<label class="label">
			<span>Title</span>
			<input class="input" type="text" name="title" required value={announcement?.title ?? ''} />
		</label>

		<label class="label">
			<span>Body</span>
			<textarea class="textarea" name="body" rows="6" required>{announcement?.body ?? ''}</textarea>
		</label>

		{#if announcement}
			<label class="label max-w-xs">
				<span>Status</span>
				<select class="select" name="status" value={announcement.status}>
					<option value="Draft">Draft</option>
					<option value="Published">Published</option>
				</select>
			</label>
		{/if}

		<button class="btn preset-filled-primary-500" type="submit">
			{data.mode === 'create' ? 'Create announcement' : 'Save changes'}
		</button>
	</form>

	{#if announcement}
		<div class="flex flex-wrap gap-2">
			<form method="POST" action="?/publish">
				<button class="btn preset-tonal-surface" type="submit">
					{announcement.status === 'Published' ? 'Unpublish' : 'Publish'}
				</button>
			</form>
			<form method="POST" action="?/delete">
				<button class="btn preset-tonal-surface text-error-500" type="submit">
					Delete announcement
				</button>
			</form>
		</div>
	{/if}
</div>
