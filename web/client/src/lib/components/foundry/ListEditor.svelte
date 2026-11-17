<script lang="ts">
	import { appendTo, moveAt, removeAt } from '#lib/foundry/content-editor';

	/**
	 * The editor for a `StringList` — tags, aliases, effect names.
	 *
	 * Order matters for some of those (a spell's effect chain), so each entry can be moved; the value is
	 * handed back whole rather than mutated, which keeps the page's state the single owner.
	 */
	let {
		name,
		values,
		onchange
	}: {
		/** The field name, used to build unique accessible labels. */
		name: string;
		/** The current entries. */
		values: string[];
		/** Receives the whole next list. */
		onchange: (next: string[]) => void;
	} = $props();

	function setEntry(index: number, value: string) {
		onchange(values.map((entry, position) => (position === index ? value : entry)));
	}
</script>

<div class="space-y-2">
	{#if values.length === 0}
		<p class="text-xs text-surface-600-400">No entries yet.</p>
	{/if}

	<ul class="space-y-1">
		{#each values as value, index (index)}
			<li class="flex items-center gap-2">
				<input
					class="input flex-1"
					aria-label={`${name} ${index + 1}`}
					{value}
					oninput={(event) => setEntry(index, event.currentTarget.value)}
				/>
				<button
					class="btn preset-tonal-surface btn-sm"
					type="button"
					disabled={index === 0}
					aria-label={`Move ${name} ${index + 1} up`}
					onclick={() => onchange(moveAt(values, index, -1))}
				>
					↑
				</button>
				<button
					class="btn preset-tonal-surface btn-sm"
					type="button"
					disabled={index === values.length - 1}
					aria-label={`Move ${name} ${index + 1} down`}
					onclick={() => onchange(moveAt(values, index, 1))}
				>
					↓
				</button>
				<button
					class="btn preset-tonal-error btn-sm"
					type="button"
					aria-label={`Remove ${name} ${index + 1}`}
					onclick={() => onchange(removeAt(values, index))}
				>
					×
				</button>
			</li>
		{/each}
	</ul>

	<button
		class="btn preset-tonal-surface btn-sm"
		type="button"
		onclick={() => onchange(appendTo(values, ''))}
	>
		Add entry
	</button>
</div>
