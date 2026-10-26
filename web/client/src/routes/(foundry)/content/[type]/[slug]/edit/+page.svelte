<script lang="ts">
	import { untrack } from 'svelte';
	import type { PageData, ActionData } from './$types';
	import {
		allFields,
		initialValues,
		issuesByField,
		toInputValue,
		hasBlockingIssues,
		type ContentIssue
	} from '#lib/foundry/content-editor';

	let { data, form }: { data: PageData; form: ActionData } = $props();

	const issues: ContentIssue[] = $derived(
		(form && 'issues' in form && form.issues ? form.issues : []) as ContentIssue[]
	);
	const grouped = $derived(issuesByField(issues));
	const blocking = $derived(hasBlockingIssues(issues));
	const fieldCount = $derived(allFields(data.schema).length);

	/**
	 * Seeds the editable form state from the loaded entity once. `untrack` is the documented way to read a prop as an
	 * initial value without re-running on every `data` change — action responses reuse the component, so edits survive
	 * a failed save or a validate round-trip.
	 */
	let values = $state<Record<string, string | boolean>>(
		untrack(() => initialValues(data.payload, data.schema))
	);

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

<svelte:head>
	<title>Edit {data.summary.displayName ?? data.slug} — Foundry</title>
</svelte:head>

<div class="mx-auto max-w-4xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={`/content/${data.contentType}/${data.slug}`}>
		← {data.summary.displayName ?? data.slug}
	</a>

	<header class="space-y-1">
		<p class="text-xs tracking-wide text-surface-600-400 uppercase">
			{data.schema.displayLabel} · {data.contentType}
		</p>
		<h1 class="h1">Edit {data.summary.displayName ?? data.slug}</h1>
		<p class="text-surface-600-400">{data.schema.description}</p>
	</header>

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

		<form method="POST" action="?/save" class="space-y-4">
			{#each data.schema.groups as group (group.label)}
				<section class="space-y-3 card p-4">
					<h2 class="h3">{group.label}</h2>

					{#each group.fields as field (field.name)}
						{@const fieldIssues = grouped.get(field.name) ?? []}
						<div class="space-y-1">
							<label class="label" for={field.name}>
								{field.label}{#if field.required}<span class="text-error-500"> *</span>{/if}
							</label>

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
									rows="3"
									bind:value={values[field.name]}></textarea>
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
									value={toInputValue(values[field.name])}
									oninput={(event) => (values[field.name] = event.currentTarget.value)}
								/>
							{/if}

							{#if field.hint}
								<p class="text-xs text-surface-600-400">{field.hint}</p>
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

			<div class="flex items-center gap-2">
				<button class="btn preset-filled-primary-500" type="submit" disabled={blocking}>
					Save
				</button>
				<button class="btn preset-tonal-surface" type="submit" formaction="?/validate">
					Validate
				</button>
				<button class="btn preset-tonal-error" type="submit" formaction="?/deactivate">
					Deactivate
				</button>
			</div>
		</form>
	{/if}
</div>
