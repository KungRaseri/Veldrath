<script lang="ts">
	import { resolve } from '$app/paths';
	import ContentDiff from '#lib/components/foundry/ContentDiff.svelte';
	import ContentRenderer from '#lib/components/foundry/ContentRenderer.svelte';
	import FoundryBreadcrumbs from '#lib/components/foundry/FoundryBreadcrumbs.svelte';
	import FoundryPageHeader from '#lib/components/foundry/FoundryPageHeader.svelte';
	import { allFields, diffFields, toInputValue } from '#lib/foundry/content-editor';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();

	/** Fields the create form can author; a collection needs the full editor, so it says so instead. */
	const authorable = $derived(
		allFields(data.schema).filter(
			(field) =>
				field.fieldType !== 'StringList' &&
				field.fieldType !== 'KeyValue' &&
				field.fieldType !== 'ObjectList'
		)
	);
	const unauthorable = $derived(
		allFields(data.schema).filter(
			(field) =>
				field.fieldType === 'StringList' ||
				field.fieldType === 'KeyValue' ||
				field.fieldType === 'ObjectList'
		)
	);

	/** The stored payload when it could not be parsed, so a reviewer still sees what was submitted. */
	const unreadable = $derived(
		data.mode === 'view' && Object.keys(data.payload ?? {}).length === 0 && Boolean(data.rawPayload)
	);

	/** What approving this submission would change, when the decision is still open. */
	const changes = $derived(
		data.mode === 'view' ? diffFields(data.schema, data.existing, data.payload) : []
	);
</script>

<svelte:head>
	<title
		>{data.mode === 'create' ? 'New submission' : (data.submission?.title ?? 'Submission')} — Foundry</title
	>
</svelte:head>

<div class="space-y-4">
	<FoundryBreadcrumbs
		items={[
			{ label: 'Foundry', href: '/foundry' },
			{ label: 'Submissions', href: resolve('/(foundry)/submissions') },
			{ label: data.mode === 'create' ? 'New' : (data.submission?.title ?? 'Submission') }
		]}
	/>

	{#if form?.error || data.error}
		<p class="card p-3 text-sm text-error-500" role="alert">{form?.error ?? data.error}</p>
	{/if}

	{#if data.mode === 'create'}
		<FoundryPageHeader
			title="New submission"
			description="Submit content for curator review. The fields come from the chosen type's schema, the same one the editor uses."
		/>

		<!--
			The type picker is a GET: changing the type reloads the page with that type's fields rather than
			carrying every schema in one document.
		-->
		<form class="flex flex-wrap items-end gap-2 card p-3" method="GET" aria-label="Choose a type">
			<label class="space-y-1 text-sm">
				<span class="text-surface-600-400">Content type</span>
				<select class="select" name="type">
					{#each data.types as type (type.contentType)}
						<option value={type.contentType} selected={type.contentType === data.typeKey}>
							{type.displayLabel}
						</option>
					{/each}
				</select>
			</label>
			<button class="btn preset-tonal-surface btn-sm" type="submit">Show fields</button>
		</form>

		<form class="space-y-3 card p-4" method="POST" action="?/create">
			<input type="hidden" name="contentType" value={data.typeKey} />

			<label class="label">
				<span>Title</span>
				<input class="input" type="text" name="title" required />
			</label>

			<label class="label">
				<span>Description</span>
				<textarea class="textarea" name="description" rows="3"></textarea>
			</label>

			{#each authorable as field (field.name)}
				<div class="space-y-1">
					<label class="label" for={`submit-${field.name}`}>
						{field.label}{#if field.required}<span class="text-error-500" aria-hidden="true">
								*</span
							>{/if}
					</label>

					{#if field.fieldType === 'Boolean'}
						<input
							id={`submit-${field.name}`}
							name={field.name}
							type="checkbox"
							class="checkbox"
							value="true"
						/>
					{:else if field.enumValues && field.enumValues.length > 0}
						<select id={`submit-${field.name}`} name={field.name} class="select">
							<option value="">—</option>
							{#each field.enumValues as option (option)}
								<option value={option}>{option || '(empty)'}</option>
							{/each}
						</select>
					{:else if field.fieldType === 'LongText'}
						<textarea
							id={`submit-${field.name}`}
							name={field.name}
							class="textarea"
							rows="4"
							value={toInputValue(field.default)}></textarea>
					{:else}
						<input
							id={`submit-${field.name}`}
							name={field.name}
							class="input"
							type={field.fieldType === 'Integer' || field.fieldType === 'Decimal'
								? 'number'
								: 'text'}
							min={field.min ?? undefined}
							max={field.max ?? undefined}
							value={toInputValue(field.default)}
						/>
					{/if}

					{#if field.hint}
						<p class="text-xs text-surface-600-400">{field.hint}</p>
					{/if}
				</div>
			{/each}

			{#if unauthorable.length > 0}
				<p class="text-xs text-warning-500">
					This type also has {unauthorable.map((field) => field.label).join(', ')}, which is
					authored through the content editor rather than this form.
				</p>
			{/if}

			<button class="btn preset-filled-primary-500" type="submit">Submit for review</button>
		</form>
	{:else if data.submission}
		<FoundryPageHeader
			title={data.submission.title}
			description={`${data.submission.contentType} · By ${data.submission.submitterName} · ${data.submission.status} · score ${data.submission.voteScore}`}
		/>

		{#if data.submission.promotedSlug}
			<p class="card p-3 text-sm">
				Published as
				<a
					class="link"
					href={`/content/${data.submission.contentType.toLowerCase()}/${data.submission.promotedSlug}`}
				>
					{data.submission.contentType}/{data.submission.promotedSlug}
				</a>
			</p>
		{/if}

		<form class="flex gap-2" method="POST" action="?/vote">
			<button class="btn preset-tonal-surface btn-sm" name="value" value="1" type="submit">
				▲ Upvote
			</button>
			<button class="btn preset-tonal-surface btn-sm" name="value" value="-1" type="submit">
				▼ Downvote
			</button>
		</form>

		{#if data.submission.description}
			<p class="card p-4">{data.submission.description}</p>
		{/if}

		{#if unreadable}
			<section class="space-y-2 card p-4">
				<h2 class="h3">Payload</h2>
				<p class="text-sm text-warning-500">
					This submission's payload is not valid JSON, so it is shown as submitted.
				</p>
				<div class="overflow-auto font-mono text-xs">{data.rawPayload}</div>
			</section>
		{:else}
			<ContentRenderer schema={data.schema} payload={data.payload} />
		{/if}

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

		{#if data.submission.status === 'Pending'}
			<ContentDiff {changes} slug={data.proposedSlug ?? null} />
		{/if}

		{#if data.canReview && data.submission.status === 'Pending'}
			<form class="space-y-3 card p-4" method="POST" action="?/review">
				<h2 class="h3">Curator decision</h2>
				<p class="text-sm text-surface-600-400">
					Approving writes this payload to the catalog; rejecting keeps it out and sends the notes
					back.
				</p>
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
