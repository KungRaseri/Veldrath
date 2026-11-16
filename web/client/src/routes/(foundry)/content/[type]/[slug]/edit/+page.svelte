<script lang="ts">
	import { untrack } from 'svelte';
	import type { PageData, ActionData } from './$types';
	import FoundryBreadcrumbs from '#lib/components/foundry/FoundryBreadcrumbs.svelte';
	import FoundryPageHeader from '#lib/components/foundry/FoundryPageHeader.svelte';
	import {
		allFields,
		anchorId,
		hasBlockingIssues,
		initialValues,
		isDirty,
		issuesByField,
		missingRequired,
		parseMarkdown,
		slugify,
		toInputValue,
		type ContentIssue,
		type MarkdownSpan
	} from '#lib/foundry/content-editor';

	let { data, form }: { data: PageData; form: ActionData } = $props();

	/**
	 * Seeds the editable form state from the loaded entity once. `untrack` is the documented way to read a prop as an
	 * initial value without re-running on every `data` change — action responses reuse the component, so edits survive
	 * a failed save or a validate round-trip.
	 */
	const seed = untrack(() => initialValues(data.payload, data.schema));
	let values = $state<Record<string, string | boolean>>({ ...seed });

	const issues: ContentIssue[] = $derived(
		(form && 'issues' in form && form.issues ? form.issues : []) as ContentIssue[]
	);
	const grouped = $derived(issuesByField(issues));
	const blocking = $derived(hasBlockingIssues(issues));
	const fieldCount = $derived(allFields(data.schema).length);

	/** Required fields the form has left blank — these block the save before it reaches the server. */
	const requiredMissing = $derived(missingRequired(data.schema, values));
	/** Whether the form holds edits that a navigation would discard. */
	const dirty = $derived(isDirty(seed, values));
	const canSave = $derived(!blocking && requiredMissing.length === 0);

	const slugField = $derived(
		allFields(data.schema).find((field) => field.fieldType === 'Slug') ?? null
	);
	const displayNameField = $derived(
		allFields(data.schema).find((field) => field.name === 'displayName') ?? null
	);

	/**
	 * Whether the slug follows the display name.
	 *
	 * Locked by default on an existing entity: the slug is part of the URL other content points at, so an
	 * incidental rename must not silently rewrite it. The author unlocks it deliberately.
	 */
	let slugLocked = $state(true);
	/** Which `LongText` fields are showing their rendered preview. */
	let previews = $state<Record<string, boolean>>({});

	/** Keeps the slug in step with the display name while it is unlocked. */
	function onDisplayName(value: string) {
		if (!displayNameField) return;
		values[displayNameField.name] = value;

		if (!slugLocked && slugField) {
			values[slugField.name] = slugify(value);
		}
	}

	function discard() {
		values = { ...seed };
		slugLocked = true;
		previews = {};
	}

	// A dirty form that would be lost on navigation asks first. This is the only guard the browser offers
	// for a route change; the Discard button covers the deliberate case.
	$effect(() => {
		if (!dirty) return;

		const warn = (event: BeforeUnloadEvent) => {
			event.preventDefault();
			event.returnValue = '';
		};

		window.addEventListener('beforeunload', warn);
		return () => window.removeEventListener('beforeunload', warn);
	});

	/**
	 * The entity's name. Both the display name and the slug can be absent on a sparse record, and a template
	 * literal would then read "Edit null" — so the heading is built from a value that is only used when there
	 * is one.
	 */
	const label = $derived(data.summary.displayName ?? data.slug ?? '');
	const headingTitle = $derived(label ? `Edit ${label}` : 'Edit');

	const status = $derived.by(() => {
		if (!form) return null;
		if ('saved' in form && form.saved) return { kind: 'ok' as const, text: 'Saved.' };
		if ('action' in form && form.action === 'validate') {
			return {
				kind: issues.length === 0 ? ('ok' as const) : ('warn' as const),
				text:
					issues.length === 0
						? 'Validation passed with no issues.'
						: `${issues.length} issue(s) found.`
			};
		}
		if ('message' in form && form.message)
			return { kind: 'error' as const, text: String(form.message) };
		return null;
	});
</script>

{#snippet inline(run: MarkdownSpan)}
	{#if run.href}
		<a class="link" href={run.href} target="_blank" rel="noreferrer noopener">{run.text}</a>
	{:else if run.code}
		<code class="rounded bg-surface-200-800 px-1">{run.text}</code>
	{:else if run.strong}
		<strong>{run.text}</strong>
	{:else if run.em}
		<em>{run.text}</em>
	{:else}
		{run.text}
	{/if}
{/snippet}

<svelte:head>
	<title>{headingTitle} — Foundry</title>
</svelte:head>

<div class="space-y-4">
	<FoundryBreadcrumbs
		items={[
			{ label: 'Foundry', href: '/foundry' },
			{
				label: data.schema.displayLabel ?? data.contentType ?? 'Content',
				href: `/content/${data.contentType}`
			},
			{ label: label || 'Untitled' }
		]}
	/>

	<FoundryPageHeader title={headingTitle} description={data.schema.description} />

	{#if !data.canEdit}
		<p class="card p-4 text-sm text-surface-600-400">
			You can read this entity but not change it: editing requires the content-editor permission.
		</p>
	{:else}
		{#if status}
			<p
				class="card p-3 text-sm"
				class:text-error-500={status.kind === 'error'}
				class:text-warning-500={status.kind === 'warn'}
				class:text-success-500={status.kind === 'ok'}
			>
				{status.text}
			</p>
		{/if}

		{#if issues.length > 0}
			<ul class="space-y-1 card p-4 text-sm">
				{#each issues as issue (issue.rule + issue.message)}
					<li
						class:text-error-500={issue.severity === 'Error'}
						class:text-warning-500={issue.severity === 'Warning'}
					>
						<span class="font-mono text-xs">{issue.rule}</span>
						{#if issue.field}<span class="text-surface-600-400"> ({issue.field})</span>{/if}
						— {issue.message}
					</li>
				{/each}
			</ul>
		{/if}

		{#if data.schema.groups.length > 1}
			<nav aria-label="Form sections" class="flex flex-wrap gap-2">
				{#each data.schema.groups as group (group.label)}
					<a class="btn preset-tonal-surface btn-sm" href={`#group-${anchorId(group.label)}`}>
						{group.label}
					</a>
				{/each}
			</nav>
		{/if}

		<form method="POST" action="?/save" class="space-y-4">
			{#each data.schema.groups as group (group.label)}
				<section id={`group-${anchorId(group.label)}`} class="scroll-mt-6 space-y-3 card p-4">
					<h2 class="h3">{group.label}</h2>

					{#each group.fields as field (field.name)}
						{@const fieldIssues = grouped.get(field.name) ?? []}
						{@const missing = requiredMissing.includes(field.name)}
						<div class="space-y-1">
							<div class="flex flex-wrap items-center justify-between gap-2">
								<label class="label" for={field.name}>
									{field.label}{#if field.required}<span class="text-error-500" aria-hidden="true">
											*</span
										>{/if}
								</label>

								{#if slugField && field.name === slugField.name}
									<label class="flex items-center gap-1 text-xs text-surface-600-400">
										<input
											type="checkbox"
											class="checkbox"
											checked={!slugLocked}
											onchange={(event) => {
												slugLocked = !event.currentTarget.checked;
												// Unlocking adopts the current display name immediately, so the
												// derived value is visible rather than appearing on the next keystroke.
												if (!slugLocked && displayNameField && slugField) {
													values[slugField.name] = slugify(
														String(values[displayNameField.name] ?? '')
													);
												}
											}}
										/>
										Derive from display name
									</label>
								{/if}
							</div>

							{#if field.fieldType === 'Boolean'}
								<input
									id={field.name}
									name={field.name}
									type="checkbox"
									class="checkbox"
									checked={values[field.name] === true}
									onchange={(event) => (values[field.name] = event.currentTarget.checked)}
								/>
							{:else if field.enumValues && field.enumValues.length > 0}
								<select
									id={field.name}
									name={field.name}
									class="select"
									aria-invalid={missing || undefined}
									bind:value={values[field.name]}
								>
									<option value="">—</option>
									{#each field.enumValues as option (option)}
										<option value={option}>{option || '(empty)'}</option>
									{/each}
								</select>
							{:else if field.fieldType === 'LongText'}
								<textarea
									id={field.name}
									name={field.name}
									class="textarea"
									rows="4"
									aria-invalid={missing || undefined}
									bind:value={values[field.name]}></textarea>
								<div class="flex flex-wrap items-center gap-2 text-xs">
									<button
										class="btn preset-tonal-surface btn-sm"
										type="button"
										onclick={() => (previews[field.name] = !previews[field.name])}
									>
										{previews[field.name] ? 'Hide preview' : 'Preview'}
									</button>
									<span class="text-surface-600-400">
										Markdown: **bold**, _italic_, `code`, - list, [link](url)
									</span>
								</div>
								{#if previews[field.name]}
									<!--
										The parser returns spans rather than HTML, so every author character here is
										escaped by the template compiler and `{@html}` is never involved.
									-->
									<div class="space-y-2 card p-3 text-sm">
										{#each parseMarkdown(String(values[field.name] ?? '')) as block, blockIndex (blockIndex)}
											{#if block.kind === 'heading'}
												<p class="font-semibold">
													{#each block.spans as run, runIndex (runIndex)}{@render inline(
															run
														)}{/each}
												</p>
											{:else if block.kind === 'list'}
												<ul class="list-disc pl-5">
													{#each block.items as item, itemIndex (itemIndex)}
														<li>
															{#each item as run, runIndex (runIndex)}{@render inline(run)}{/each}
														</li>
													{/each}
												</ul>
											{:else}
												<p>
													{#each block.lines as line, lineIndex (lineIndex)}
														{#each line as run, runIndex (runIndex)}{@render inline(run)}{/each}
														{#if lineIndex < block.lines.length - 1}<br />{/if}
													{/each}
												</p>
											{/if}
										{/each}
									</div>
								{/if}
							{:else if field.name === displayNameField?.name}
								<input
									id={field.name}
									name={field.name}
									class="input"
									value={toInputValue(values[field.name])}
									aria-invalid={missing || undefined}
									oninput={(event) => onDisplayName(event.currentTarget.value)}
								/>
							{:else}
								<input
									id={field.name}
									name={field.name}
									class="input"
									type={field.fieldType === 'Integer' || field.fieldType === 'Decimal'
										? 'number'
										: 'text'}
									min={field.min ?? undefined}
									max={field.max ?? undefined}
									aria-invalid={missing || undefined}
									value={toInputValue(values[field.name])}
									oninput={(event) => (values[field.name] = event.currentTarget.value)}
								/>
							{/if}

							{#if field.hint}
								<p class="text-xs text-surface-600-400">{field.hint}</p>
							{/if}

							{#if missing}
								<p class="text-xs text-error-500">This field is required.</p>
							{/if}

							{#each fieldIssues as issue (issue.rule + issue.message)}
								<p
									class="text-xs"
									class:text-error-500={issue.severity === 'Error'}
									class:text-warning-500={issue.severity === 'Warning'}
								>
									{issue.message}
								</p>
							{/each}
						</div>
					{/each}
				</section>
			{/each}

			{#if fieldCount === 0}
				<p class="card p-4 text-sm text-surface-600-400">
					This content type exposes no editable fields yet.
				</p>
			{/if}

			<div
				class="sticky bottom-0 z-10 flex flex-wrap items-center gap-2 border-t border-surface-200-800 bg-surface-50-950/95 px-1 py-3 backdrop-blur"
			>
				<button class="btn preset-filled-primary-500" type="submit" disabled={!canSave}>
					Save
				</button>
				<button class="btn preset-tonal-surface" type="submit" formaction="?/validate">
					Validate
				</button>
				<button class="btn preset-tonal-surface" type="button" disabled={!dirty} onclick={discard}>
					Discard
				</button>
				<button class="btn preset-tonal-error" type="submit" formaction="?/deactivate">
					Deactivate
				</button>
				{#if dirty}
					<span class="text-xs text-warning-500">Unsaved changes</span>
				{/if}
			</div>
		</form>
	{/if}
</div>
