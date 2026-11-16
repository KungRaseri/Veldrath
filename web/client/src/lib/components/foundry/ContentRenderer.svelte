<script lang="ts">
	import {
		readPath,
		type ContentField,
		type ContentTypeSchema,
		type ContentRow
	} from '#lib/foundry/content-editor';

	/**
	 * The read-only view of a content payload.
	 *
	 * The portal used to show a `<pre>` of the stored JSON, which meant a curator reviewing a quest read a
	 * blob rather than a quest. This renders the *same schema* the editor uses — labels, groups, row shapes —
	 * so a value is read as the field it belongs to. Anything the schema does not describe is listed under
	 * "Other fields" rather than hidden, and the raw JSON stays available behind a disclosure for debugging.
	 */
	let {
		schema,
		payload = {}
	}: {
		/** The content type's schema, or `null` when the type has none (then everything is "other"). */
		schema: ContentTypeSchema | null;
		/** The stored payload; an absent one renders as an empty schema view rather than a crash. */
		payload?: Record<string, unknown>;
	} = $props();

	/** Keys the schema does not describe, so nothing is silently dropped from the view. */
	const others = $derived.by(() => {
		const described = new Set(
			(schema?.groups ?? []).flatMap((group) =>
				group.fields.map((field) => field.name.split('.')[0])
			)
		);

		return Object.keys(payload).filter((key) => !described.has(key));
	});

	const prettyJson = $derived(JSON.stringify(payload, null, 2));

	/** How a scalar reads back: a boolean as a word, an empty value as a dash. */
	function scalar(value: unknown): string {
		if (value === null || value === undefined || value === '') return '—';
		if (typeof value === 'boolean') return value ? 'Yes' : 'No';
		return String(value);
	}

	function list(value: unknown): string[] {
		return Array.isArray(value)
			? value.filter((entry): entry is string => typeof entry === 'string')
			: [];
	}

	function rows(value: unknown): ContentRow[] {
		return Array.isArray(value)
			? value.filter(
					(entry): entry is ContentRow =>
						entry !== null && typeof entry === 'object' && !Array.isArray(entry)
				)
			: [];
	}

	function pairs(value: unknown): { key: string; value: string }[] {
		if (value === null || typeof value !== 'object' || Array.isArray(value)) return [];
		return Object.entries(value as Record<string, unknown>).map(([key, entry]) => ({
			key,
			value: scalar(entry)
		}));
	}

	/** A row cell, read through the row descriptor so a boolean still reads as a word. */
	function cell(item: ContentField, row: ContentRow): string {
		return scalar(row[item.name]);
	}
</script>

{#each schema?.groups ?? [] as group (group.label)}
	<section class="space-y-3 card p-4">
		<h2 class="h3">{group.label}</h2>

		<dl class="grid gap-3 sm:grid-cols-2">
			{#each group.fields as field (field.name)}
				{@const value = readPath(payload, field.name)}
				<div class="space-y-1">
					<dt class="text-xs tracking-wide text-surface-600-400 uppercase">{field.label}</dt>
					<dd class="text-sm">
						{#if field.fieldType === 'LongText'}
							{#if value}
								<p class="whitespace-pre-line">{String(value)}</p>
							{:else}
								—
							{/if}
						{:else if field.fieldType === 'StringList'}
							{#if list(value).length > 0}
								<span class="flex flex-wrap gap-1">
									{#each list(value) as entry (entry)}
										<span class="badge preset-tonal-surface text-xs">{entry}</span>
									{/each}
								</span>
							{:else}
								—
							{/if}
						{:else if field.fieldType === 'KeyValue'}
							{#if pairs(value).length > 0}
								<table class="table w-full text-xs">
									<tbody>
										{#each pairs(value) as pair (pair.key)}
											<tr>
												<th scope="row" class="text-left font-normal">{pair.key}</th>
												<td>{pair.value}</td>
											</tr>
										{/each}
									</tbody>
								</table>
							{:else}
								—
							{/if}
						{:else if field.fieldType === 'ObjectList'}
							{#if rows(value).length > 0}
								<div class="overflow-x-auto">
									<table class="table w-full text-xs">
										<thead>
											<tr>
												{#each field.items ?? [] as item (item.name)}
													<th scope="col">{item.label}</th>
												{/each}
											</tr>
										</thead>
										<tbody>
											{#each rows(value) as row, index (index)}
												<tr>
													{#each field.items ?? [] as item (item.name)}
														<td>{cell(item, row)}</td>
													{/each}
												</tr>
											{/each}
										</tbody>
									</table>
								</div>
							{:else}
								—
							{/if}
						{:else if field.fieldType === 'Reference' && field.refType && value}
							<a class="link" href={`/content/${field.refType}/${value}`}>{String(value)}</a>
						{:else}
							{scalar(value)}
						{/if}
					</dd>
					{#if field.hint}
						<p class="text-xs text-surface-600-400">{field.hint}</p>
					{/if}
				</div>
			{/each}
		</dl>
	</section>
{/each}

{#if others.length > 0}
	<section class="space-y-3 card p-4">
		<h2 class="h3">Other fields</h2>
		<dl class="grid gap-3 sm:grid-cols-2">
			{#each others as key (key)}
				<div>
					<dt class="text-xs tracking-wide text-surface-600-400 uppercase">{key}</dt>
					<dd class="text-sm">{scalar(payload[key])}</dd>
				</div>
			{/each}
		</dl>
	</section>
{/if}

<details class="card p-4">
	<summary class="cursor-pointer text-sm text-surface-600-400">Advanced — raw JSON</summary>
	<pre class="mt-2 overflow-auto text-xs">{prettyJson}</pre>
</details>
