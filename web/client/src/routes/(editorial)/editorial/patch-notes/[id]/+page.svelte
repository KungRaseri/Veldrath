<script lang="ts">
	import { resolve } from '$app/paths';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();

	const note = $derived(data.note);
</script>

<svelte:head>
	<title
		>{data.mode === 'create' ? 'New patch note' : (note?.title ?? 'Patch note')} — Editorial</title
	>
</svelte:head>

<div class="mx-auto max-w-4xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={resolve('/(editorial)/editorial/patch-notes')}
		>← Patch notes</a
	>

	{#if form?.error}
		<p class="text-sm text-error-500">{form.error}</p>
	{:else if form?.message}
		<p class="text-sm text-success-500">{form.message}</p>
	{/if}

	<header class="flex flex-wrap items-center justify-between gap-3">
		<h1 class="h1">{data.mode === 'create' ? 'New patch note' : 'Edit patch note'}</h1>
		{#if note}
			<span class="chip preset-tonal-surface text-xs">{note.status}</span>
		{/if}
	</header>

	<form class="space-y-4 card p-6" method="POST" action="?/save">
		<div class="grid gap-3 sm:grid-cols-3">
			{#if data.mode === 'create'}
				<label class="label">
					<span>Slug</span>
					<input
						class="input font-mono"
						type="text"
						name="slug"
						required
						placeholder="alpha-0-1-0"
					/>
				</label>
			{:else if note}
				<label class="label">
					<span>Slug</span>
					<input class="input font-mono" type="text" value={note.slug} disabled />
				</label>
			{/if}
			<label class="label">
				<span>Version</span>
				<input class="input" type="text" name="version" required value={note?.version ?? ''} />
			</label>
			{#if note}
				<label class="label">
					<span>Status</span>
					<select class="select" name="status" value={note.status}>
						<option value="Draft">Draft</option>
						<option value="Published">Published</option>
					</select>
				</label>
			{/if}
		</div>

		<label class="label">
			<span>Title</span>
			<input class="input" type="text" name="title" required value={note?.title ?? ''} />
		</label>

		<label class="label">
			<span>Summary</span>
			<textarea class="textarea" name="summary" rows="2" required>{note?.summary ?? ''}</textarea>
		</label>

		<label class="label">
			<span>Content (Markdown)</span>
			<textarea class="textarea font-mono text-xs" name="content" rows="16" required
				>{note?.content ?? ''}</textarea
			>
		</label>

		<button class="btn preset-filled-primary-500" type="submit">
			{data.mode === 'create' ? 'Create patch note' : 'Save changes'}
		</button>
	</form>

	{#if note}
		<div class="flex flex-wrap gap-2">
			<form method="POST" action="?/publish">
				<button class="btn preset-tonal-surface" type="submit">
					{note.status === 'Published' ? 'Unpublish' : 'Publish'}
				</button>
			</form>
			<form method="POST" action="?/delete">
				<button class="btn preset-tonal-surface text-error-500" type="submit"
					>Delete patch note</button
				>
			</form>
		</div>
	{/if}
</div>
