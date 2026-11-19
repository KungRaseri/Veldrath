<script lang="ts">
	import type { FieldChange } from '#lib/foundry/content-editor';

	/**
	 * What approving a submission will write, field by field.
	 *
	 * The reviewer used to decide from a JSON blob and a status. This shows the same comparison the promotion
	 * makes: the stored entity's value beside the proposed one, so "approve" is a decision about content rather
	 * than about text.
	 */
	let {
		changes,
		slug
	}: {
		/** The fields that differ, in schema order. */
		changes: FieldChange[];
		/** The slug the payload would be published under, or `null` when it has none. */
		slug: string | null;
	} = $props();
</script>

<section class="space-y-2 card p-4">
	<h2 class="h3">What approval writes</h2>

	{#if slug}
		<p class="text-sm text-surface-600-400">
			Slug <span class="font-mono">{slug}</span>
			{#if changes.length > 0 && changes[0].kind === 'added'}
				· this would create a new entry
			{:else}
				· this would replace the stored entry
			{/if}
		</p>
	{:else}
		<p class="text-sm text-warning-500">
			The payload has no slug, so approval would be refused until one is added.
		</p>
	{/if}

	{#if changes.length === 0}
		<p class="text-sm text-surface-600-400">Nothing in the payload differs from the catalog.</p>
	{:else}
		<div class="overflow-x-auto">
			<table class="table w-full text-xs">
				<thead>
					<tr>
						<th scope="col">Field</th>
						<th scope="col">Now</th>
						<th scope="col">Proposed</th>
					</tr>
				</thead>
				<tbody>
					{#each changes as change (change.field)}
						<tr>
							<th scope="row" class="text-left font-normal">{change.label}</th>
							<td class="text-surface-600-400">{change.before}</td>
							<td class="text-warning-500">{change.after}</td>
						</tr>
					{/each}
				</tbody>
			</table>
		</div>
	{/if}
</section>
