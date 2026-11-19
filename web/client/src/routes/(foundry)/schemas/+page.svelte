<script lang="ts">
	import FoundryEmptyState from '#lib/components/foundry/FoundryEmptyState.svelte';
	import FoundryPageHeader from '#lib/components/foundry/FoundryPageHeader.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>Schemas — Foundry</title>
</svelte:head>

<FoundryPageHeader
	title="Schemas"
	description="Every registered content type, its groups and its fields. The editor, the create form and the read view are all generated from these descriptors."
/>

{#if data.error}
	<p class="card p-3 text-sm text-error-500" role="alert">{data.error}</p>
{/if}

{#if data.schemas.length === 0}
	<FoundryEmptyState message="No content schemas are registered, so there is nothing to author." />
{:else}
	{#each data.schemas as schema (schema.contentType)}
		<section class="space-y-3 card p-4">
			<header class="space-y-1">
				<h2 class="h3">
					{schema.displayLabel}
					<span class="font-mono text-xs text-surface-600-400">{schema.contentType}</span>
				</h2>
				<p class="text-sm text-surface-600-400">{schema.description}</p>
			</header>

			{#each schema.groups as group (group.label)}
				<div class="space-y-1">
					<h3 class="text-xs tracking-wide text-surface-600-400 uppercase">{group.label}</h3>
					<div class="overflow-x-auto">
						<table class="table w-full text-sm">
							<thead>
								<tr>
									<th scope="col">Field</th>
									<th scope="col">Kind</th>
									<th scope="col">Path</th>
									<th scope="col">Rules</th>
								</tr>
							</thead>
							<tbody>
								{#each group.fields as field (field.name)}
									<tr>
										<td>{field.label}</td>
										<td>
											<span class="badge preset-tonal-surface text-xs">{field.fieldType}</span>
										</td>
										<td class="font-mono text-xs">{field.name}</td>
										<td class="text-xs text-surface-600-400">
											{field.required ? 'required' : 'optional'}
											{#if field.min !== null || field.max !== null}
												· {field.min ?? '−∞'}–{field.max ?? '∞'}
											{/if}
											{#if field.enumValues && field.enumValues.length > 0}
												· one of {field.enumValues.join(', ')}
											{/if}
											{#if field.refType}
												· references {field.refType}
											{/if}
											{#if field.items && field.items.length > 0}
												· rows of {field.items.map((item) => item.label).join(', ')}
											{/if}
											{#if field.hint}
												<span class="mt-1 block">{field.hint}</span>
											{/if}
										</td>
									</tr>
								{/each}
							</tbody>
						</table>
					</div>
				</div>
			{/each}
		</section>
	{/each}
{/if}
