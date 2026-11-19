<script lang="ts">
	import FoundryBreadcrumbs from '#lib/components/foundry/FoundryBreadcrumbs.svelte';
	import FoundryPageHeader from '#lib/components/foundry/FoundryPageHeader.svelte';
	import { allFields, isScalarKind, toInputValue } from '#lib/foundry/content-editor';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();

	/** The fields this form can author; collections are added in the editor once the entity exists. */
	const authorable = $derived(allFields(data.schema).filter(isScalarKind));
	const collections = $derived(allFields(data.schema).filter((field) => !isScalarKind(field)));
</script>

<svelte:head>
	<title>New {data.schema.displayLabel} — Foundry</title>
</svelte:head>

<div class="space-y-4">
	<FoundryBreadcrumbs
		items={[
			{ label: 'Foundry', href: '/foundry' },
			{ label: data.schema.displayLabel, href: `/content/${data.contentType}` },
			{ label: 'New' }
		]}
	/>

	<FoundryPageHeader
		title={`New ${data.schema.displayLabel}`}
		description={data.schema.description}
	/>

	{#if form?.message}
		<p class="card p-3 text-sm text-error-500" role="alert">{form.message}</p>
	{/if}

	{#if !data.canEdit}
		<p class="card p-4 text-sm text-surface-600-400">
			Creating a {data.schema.displayLabel} entry requires the content-editor permission.
		</p>
	{:else}
		<form class="space-y-3 card p-4" method="POST">
			{#each authorable as field (field.name)}
				<div class="space-y-1">
					<label class="label" for={field.name}>
						{field.label}{#if field.required}<span class="text-error-500" aria-hidden="true">
								*</span
							>{/if}
					</label>

					{#if field.fieldType === 'Boolean'}
						<!--
							The schema's default is what the field means when the author expresses no preference
							(`isActive` defaults to "true"). A checkbox that ignores it created every entry inactive,
							so the brand-new entity was missing from the catalog the form redirects to.
						-->
						<input
							id={field.name}
							name={field.name}
							type="checkbox"
							class="checkbox"
							value="true"
							checked={field.default === 'true'}
						/>
					{:else if field.enumValues && field.enumValues.length > 0}
						<select id={field.name} name={field.name} class="select">
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
							value={toInputValue(field.default)}></textarea>
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
							list={field.fieldType === 'Reference' && field.refType
								? `${field.name}-options`
								: undefined}
							value={toInputValue(field.default)}
						/>
						{#if field.fieldType === 'Reference' && field.refType}
							<datalist id={`${field.name}-options`}>
								{#each data.refOptions?.[field.refType] ?? [] as option (option.value)}
									<option value={option.value}>{option.label}</option>
								{/each}
							</datalist>
						{/if}
					{/if}

					{#if field.hint}
						<p class="text-xs text-surface-600-400">{field.hint}</p>
					{/if}
				</div>
			{/each}

			{#if collections.length > 0}
				<p class="text-xs text-surface-600-400">
					{collections.map((field) => field.label).join(', ')}
					{collections.length === 1 ? 'is' : 'are'} filled in the editor once the entry exists.
				</p>
			{/if}

			<button class="btn preset-filled-primary-500" type="submit">Create draft</button>
		</form>
	{/if}
</div>
