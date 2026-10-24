<script lang="ts">
	import { resolve } from '$app/paths';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();

	const prettyPayload = $derived.by(() => {
		if (data.mode !== 'view' || !data.submission) return '';
		try {
			return JSON.stringify(JSON.parse(data.submission.payload), null, 2);
		} catch {
			return data.submission.payload;
		}
	});
</script>

<svelte:head>
	<title
		>{data.mode === 'create' ? 'New submission' : (data.submission?.title ?? 'Submission')} — Foundry</title
	>
</svelte:head>

<div class="mx-auto max-w-4xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={resolve('/(foundry)/submissions')}>← All submissions</a>

	{#if form?.error || data.error}
		<p class="text-sm text-error-500">{form?.error ?? data.error}</p>
	{/if}

	{#if data.mode === 'create'}
		<header class="space-y-1">
			<h1 class="h1">New submission</h1>
			<p class="text-surface-600-400">
				Submit content for curator review. The payload must be valid JSON matching the chosen type.
			</p>
		</header>

		<form class="space-y-4 card p-6" method="POST" action="?/create">
			<div class="grid gap-3 sm:grid-cols-2">
				<label class="label">
					<span>Content type</span>
					<select class="select" name="contentType" required>
						<option value="Item">Item</option>
						<option value="Spell">Spell</option>
						<option value="Ability">Ability</option>
						<option value="Npc">Npc</option>
						<option value="Quest">Quest</option>
						<option value="Recipe">Recipe</option>
						<option value="LootTable">LootTable</option>
						<option value="language">Language</option>
					</select>
				</label>
				<label class="label">
					<span>Title</span>
					<input class="input" type="text" name="title" required />
				</label>
			</div>

			<label class="label">
				<span>Description</span>
				<textarea class="textarea" name="description" rows="3"></textarea>
			</label>

			<label class="label">
				<span>Payload (JSON)</span>
				<textarea class="textarea font-mono text-xs" name="payload" rows="12" required
					>{'{\n  "slug": "",\n  "displayName": "",\n  "description": ""\n}'}</textarea
				>
			</label>

			<button class="btn preset-filled-primary-500" type="submit">Submit for review</button>
		</form>
	{:else if data.submission}
		<header class="flex flex-wrap items-start justify-between gap-3">
			<div class="space-y-1">
				<p class="text-xs tracking-wide text-surface-600-400 uppercase">
					{data.submission.contentType}
				</p>
				<h1 class="h1">{data.submission.title}</h1>
				<p class="text-surface-600-400">
					By {data.submission.submitterName} · {data.submission.status} · score
					{data.submission.voteScore}
				</p>
			</div>
			<form class="flex gap-2" method="POST" action="?/vote">
				<button class="btn preset-tonal-surface btn-sm" name="value" value="1" type="submit">
					▲ Upvote
				</button>
				<button class="btn preset-tonal-surface btn-sm" name="value" value="-1" type="submit">
					▼ Downvote
				</button>
			</form>
		</header>

		{#if data.submission.description}
			<p class="card p-4">{data.submission.description}</p>
		{/if}

		<section class="space-y-2 card p-4">
			<h2 class="h3">Payload</h2>
			<pre class="overflow-auto text-xs">{prettyPayload}</pre>
		</section>

		{#if data.submission.reviewerName}
			<section class="space-y-1 card p-4">
				<h2 class="h3">Review</h2>
				<p class="text-sm">
					{data.submission.status} by {data.submission.reviewerName}
					{#if data.submission.reviewedAt}
						on {new Date(data.submission.reviewedAt).toLocaleString()}
					{/if}
				</p>
				{#if data.submission.reviewNotes}
					<p class="text-sm text-surface-600-400">{data.submission.reviewNotes}</p>
				{/if}
			</section>
		{/if}

		{#if data.canReview && data.submission.status === 'Pending'}
			<form class="space-y-3 card p-4" method="POST" action="?/review">
				<h2 class="h3">Curator decision</h2>
				<label class="label">
					<span>Notes</span>
					<textarea class="textarea" name="notes" rows="3"></textarea>
				</label>
				<div class="flex gap-2">
					<button class="btn preset-filled-primary-500" name="approved" value="yes" type="submit">
						Approve
					</button>
					<button class="btn preset-tonal-surface" name="approved" value="no" type="submit">
						Reject
					</button>
				</div>
			</form>
		{/if}
	{/if}
</div>
