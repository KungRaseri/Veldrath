<script lang="ts">
	import { resolve } from '$app/paths';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();

	const article = $derived(data.article);
</script>

<svelte:head>
	<title
		>{data.mode === 'create' ? 'New lore article' : (article?.title ?? 'Lore article')} — Editorial</title
	>
</svelte:head>

<div class="mx-auto max-w-4xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={resolve('/editorial/lore')}>← Lore articles</a>

	{#if form?.error}
		<p class="text-sm text-error-500">{form.error}</p>
	{:else if form?.message}
		<p class="text-sm text-success-500">{form.message}</p>
	{/if}

	<header class="flex flex-wrap items-center justify-between gap-3">
		<h1 class="h1">{data.mode === 'create' ? 'New lore article' : 'Edit lore article'}</h1>
		{#if article}
			<span class="chip preset-tonal-surface text-xs">{article.status}</span>
		{/if}
	</header>

	<form class="space-y-4 card p-6" method="POST" action="?/save">
		<div class="grid gap-3 sm:grid-cols-2">
			{#if data.mode === 'create'}
				<label class="label">
					<span>Slug</span>
					<input
						class="input font-mono"
						type="text"
						name="slug"
						required
						placeholder="founding-of-calen"
					/>
				</label>
			{:else if article}
				<label class="label">
					<span>Slug</span>
					<input class="input font-mono" type="text" value={article.slug} disabled />
				</label>
			{/if}
			<label class="label">
				<span>Category</span>
				<input class="input" type="text" name="category" value={article?.category ?? 'History'} />
			</label>
		</div>

		<label class="label">
			<span>Title</span>
			<input class="input" type="text" name="title" required value={article?.title ?? ''} />
		</label>

		<label class="label">
			<span>Summary</span>
			<textarea class="textarea" name="summary" rows="2" required>{article?.summary ?? ''}</textarea
			>
		</label>

		<label class="label">
			<span>Content (Markdown)</span>
			<textarea class="textarea font-mono text-xs" name="content" rows="16" required
				>{article?.content ?? ''}</textarea
			>
		</label>

		{#if article}
			<label class="label max-w-xs">
				<span>Status</span>
				<select class="select" name="status" value={article.status}>
					<option value="Draft">Draft</option>
					<option value="Published">Published</option>
				</select>
			</label>
		{/if}

		<button class="btn preset-filled-primary-500" type="submit">
			{data.mode === 'create' ? 'Create article' : 'Save changes'}
		</button>
	</form>

	{#if article}
		<div class="flex flex-wrap gap-2">
			<form method="POST" action="?/publish">
				<button class="btn preset-tonal-surface" type="submit">
					{article.status === 'Published' ? 'Unpublish' : 'Publish'}
				</button>
			</form>
			<form method="POST" action="?/delete">
				<button class="btn preset-tonal-surface text-error-500" type="submit">Delete article</button
				>
			</form>
		</div>
	{/if}
</div>
