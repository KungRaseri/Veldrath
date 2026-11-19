<script lang="ts">
	import {
		appendTo,
		moveAt,
		removeAt,
		rowFromItems,
		setRowValue,
		toInputValue,
		type ContentField,
		type ContentRow
	} from '#lib/foundry/content-editor';

	/**
	 * The editor for an `ObjectList` — quest objectives, recipe ingredients, loot entries.
	 *
	 * Each row is shaped by the field's own `items`, so a new content type needs no new component: declare
	 * the row and this renders it. `KeyValue` reuses it with two descriptors (`key`, `value`) because the
	 * storage is an object but the editing is rows.
	 */
	let {
		name,
		items,
		rows,
		onchange,
		refOptions = {}
	}: {
		/** The field name, used for unique ids and accessible labels. */
		name: string;
		/** The row shape, relative to the row. */
		items: ContentField[];
		/** The current rows. */
		rows: ContentRow[];
		/** Receives the whole next list of rows. */
		onchange: (next: ContentRow[]) => void;
		/** Slugs a `Reference` row field may name, keyed by content type. */
		refOptions?: Record<string, { value: string; label: string }[]>;
	} = $props();

	function idFor(rowIndex: number, property: string): string {
		return `${name}-${rowIndex}-${property}`;
	}
</script>

<div class="space-y-2">
	{#if rows.length === 0}
		<p class="text-xs text-surface-600-400">No entries yet.</p>
	{/if}

	{#each rows as row, rowIndex (rowIndex)}
		<fieldset class="grid gap-3 card p-3 sm:grid-cols-2">
			<legend class="text-xs tracking-wide text-surface-600-400 uppercase">
				Entry {rowIndex + 1}
			</legend>

			{#each items as item (item.name)}
				{@const id = idFor(rowIndex, item.name)}
				<div class="space-y-1">
					<label class="label" for={id}>
						{item.label}{#if item.required}<span class="text-error-500" aria-hidden="true">
								*</span
							>{/if}
					</label>

					{#if item.fieldType === 'Boolean'}
						<input
							{id}
							type="checkbox"
							class="checkbox"
							checked={row[item.name] === true}
							onchange={(event) =>
								onchange(setRowValue(rows, rowIndex, item.name, event.currentTarget.checked))}
						/>
					{:else if item.enumValues && item.enumValues.length > 0}
						<select
							{id}
							class="select"
							value={toInputValue(row[item.name])}
							onchange={(event) =>
								onchange(setRowValue(rows, rowIndex, item.name, event.currentTarget.value))}
						>
							<option value="">—</option>
							{#each item.enumValues as option (option)}
								<option value={option}>{option || '(empty)'}</option>
							{/each}
						</select>
					{:else}
						<input
							{id}
							class="input"
							type={item.fieldType === 'Integer' || item.fieldType === 'Decimal'
								? 'number'
								: 'text'}
							min={item.min ?? undefined}
							max={item.max ?? undefined}
							list={item.fieldType === 'Reference' && item.refType ? `${id}-options` : undefined}
							value={toInputValue(row[item.name])}
							oninput={(event) =>
								onchange(setRowValue(rows, rowIndex, item.name, event.currentTarget.value))}
						/>
						{#if item.fieldType === 'Reference' && item.refType}
							<datalist id={`${id}-options`}>
								{#each refOptions[item.refType] ?? [] as option (option.value)}
									<option value={option.value}>{option.label}</option>
								{/each}
							</datalist>
						{/if}
					{/if}

					{#if item.hint}
						<p class="text-xs text-surface-600-400">{item.hint}</p>
					{/if}
				</div>
			{/each}

			<div class="flex flex-wrap gap-2 sm:col-span-2">
				<button
					class="btn preset-tonal-surface btn-sm"
					type="button"
					disabled={rowIndex === 0}
					aria-label={`Move entry ${rowIndex + 1} up`}
					onclick={() => onchange(moveAt(rows, rowIndex, -1))}
				>
					↑
				</button>
				<button
					class="btn preset-tonal-surface btn-sm"
					type="button"
					disabled={rowIndex === rows.length - 1}
					aria-label={`Move entry ${rowIndex + 1} down`}
					onclick={() => onchange(moveAt(rows, rowIndex, 1))}
				>
					↓
				</button>
				<button
					class="btn preset-tonal-error btn-sm"
					type="button"
					aria-label={`Remove entry ${rowIndex + 1}`}
					onclick={() => onchange(removeAt(rows, rowIndex))}
				>
					Remove
				</button>
			</div>
		</fieldset>
	{/each}

	<button
		class="btn preset-tonal-surface btn-sm"
		type="button"
		onclick={() => onchange(appendTo(rows, rowFromItems(items)))}
	>
		Add entry
	</button>
</div>
