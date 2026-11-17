<script lang="ts">
	import FoundryEmptyState from '#lib/components/foundry/FoundryEmptyState.svelte';
	import FoundryPageHeader from '#lib/components/foundry/FoundryPageHeader.svelte';
	import FoundrySection from '#lib/components/foundry/FoundrySection.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>World health — Foundry</title>
</svelte:head>

<FoundryPageHeader
	title="World health"
	description="The content audit: dangling references between entities, run over the whole catalog. The server runs the same rules at startup, and CI runs them as a guard."
/>

{#if data.error}
	<p class="card p-3 text-sm text-error-500" role="alert">{data.error}</p>
{/if}

<FoundrySection
	title="Findings"
	count={data.issues.length}
	description={`${data.errors} error(s) and ${data.warnings} warning(s). An error is content that will misbehave in play.`}
>
	{#if data.issues.length === 0}
		<FoundryEmptyState
			message="The catalog is consistent: every reference the audit checks resolves."
		/>
	{:else}
		<div class="overflow-x-auto">
			<table class="table w-full text-sm">
				<thead>
					<tr>
						<th scope="col">Severity</th>
						<th scope="col">Rule</th>
						<th scope="col">Entity</th>
						<th scope="col">Where</th>
						<th scope="col">Issue</th>
					</tr>
				</thead>
				<tbody>
					{#each data.issues as issue, index (index)}
						<tr>
							<td>
								<span
									class="badge text-xs"
									class:preset-tonal-error={issue.severity === 'Error'}
									class:preset-tonal-surface={issue.severity !== 'Error'}
								>
									{issue.severity}
								</span>
							</td>
							<td class="font-mono text-xs">{issue.rule}</td>
							<td class="font-mono text-xs">{issue.identifier ?? '—'}</td>
							<td class="font-mono text-xs">{issue.field ?? '—'}</td>
							<td>{issue.message}</td>
						</tr>
					{/each}
				</tbody>
			</table>
		</div>
		<p class="text-xs text-surface-600-400">
			The audit reports the entity's slug but not its content type, so these are not deep links yet.
			Find the entry in the content browser by slug.
		</p>
	{/if}
</FoundrySection>
