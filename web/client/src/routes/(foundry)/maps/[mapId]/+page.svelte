<script lang="ts">
	import { ArrowLeft, Check, ShieldCheck, TriangleAlert } from '@lucide/svelte';
	import { issueLabel, issuesByNode } from '#lib/foundry/map-editor';
	import type { PageData } from './$types';

	let {
		data,
		form
	}: {
		data: PageData;
		form: {
			saved?: boolean;
			validated?: boolean;
			isValid?: boolean;
			version?: number;
			message?: string;
			issues?: {
				code: string;
				message: string;
				severity: string;
				mapId: string | null;
				nodeId: string | null;
			}[];
		} | null;
	} = $props();

	const issues = $derived(form?.issues ?? []);
	const grouped = $derived(issuesByNode(issues));
</script>

<svelte:head>
	<title>{data.graph.name} — Foundry maps</title>
</svelte:head>

<section class="space-y-3">
	<header class="flex flex-wrap items-center justify-between gap-3">
		<div>
			<h1 class="game-panel-header">{data.graph.name}</h1>
			<p class="text-sm text-surface-600-400">
				{data.graph.id} · {data.graph.mapType} · owner {data.graph.ownerId} · v{data.graph.version} ·
				{data.graph.nodes.length} nodes · {data.graph.edges.length} edges
			</p>
		</div>
		<a class="btn preset-tonal-surface" href="/maps">
			<ArrowLeft size={15} aria-hidden="true" />
			All maps
		</a>
	</header>

	{#if form?.validated}
		<div
			class="game-panel space-y-2 p-3 text-sm"
			class:text-success-500={form.isValid}
			class:text-warning-500={!form.isValid}
			role="status"
		>
			<p class="flex items-center gap-2">
				{#if form.isValid}
					<ShieldCheck size={15} aria-hidden="true" />
					No errors — a save would accept this graph.
				{:else}
					<TriangleAlert size={15} aria-hidden="true" />
					This graph would be refused: {issues.filter((issue) => issue.severity === 'Error').length} error(s),
					{issues.filter((issue) => issue.severity === 'Warning').length} warning(s).
				{/if}
			</p>
			{#if grouped.map.length > 0}
				<ul class="list-inside list-disc text-xs text-surface-600-400">
					{#each grouped.map as issue (issue.code + issue.message)}
						<li>{issueLabel(issue)}</li>
					{/each}
				</ul>
			{/if}
		</div>
	{:else if form?.saved}
		<p class="game-panel p-3 text-sm text-success-500" role="status">
			<Check size={15} aria-hidden="true" class="inline" />
			Saved. The map is now at version {form.version ?? data.graph.version}.
		</p>
	{:else if form?.message}
		<div class="game-panel space-y-2 border border-error-500/40 p-3 text-sm" role="alert">
			<p class="flex items-center gap-2 text-error-500">
				<TriangleAlert size={15} aria-hidden="true" />
				{form.message}
			</p>
			{#if issues.length > 0}
				<ul class="list-inside list-disc text-xs text-surface-600-400">
					{#each issues as issue, index (index)}
						<li>{issueLabel(issue)}</li>
					{/each}
				</ul>
			{/if}
		</div>
	{/if}

	<form method="POST" action="?/save" class="game-panel space-y-3 p-4">
		<input type="hidden" name="version" value={data.graph.version} />

		<p class="text-sm text-surface-600-400">
			Node labels, hidden flags, unlock keys and exit targets are editable here. Positions and edges
			are authored on the canvas and are shown read-only.
		</p>

		<div class="overflow-x-auto">
			<table class="table w-full text-sm">
				<thead>
					<tr>
						<th scope="col">Node</th>
						<th scope="col">Type</th>
						<th scope="col">Label</th>
						<th scope="col">Hidden</th>
						<th scope="col">Unlock key</th>
						<th scope="col">Exit target</th>
						<th scope="col">Edges</th>
					</tr>
				</thead>
				<tbody>
					{#each data.rows as node (node.id)}
						{@const rowIssues = grouped.nodes[node.id] ?? []}
						<tr>
							<td>
								<span class="font-mono text-xs">{node.id}</span>
								{#if node.isEntryPoint}
									<span class="ml-1 badge preset-tonal-primary text-xs">entry</span>
								{/if}
								{#if rowIssues.length > 0}
									<span
										class="ml-1 badge preset-tonal-surface text-xs text-error-500"
										title={rowIssues.map(issueLabel).join('\n')}
									>
										{rowIssues.length} issue{rowIssues.length === 1 ? '' : 's'}
									</span>
								{/if}
							</td>
							<td>{node.nodeType}</td>
							<td>
								<input
									class="input w-48"
									name={`node:${node.id}:displayName`}
									value={node.displayName}
									disabled={!data.canEdit}
								/>
							</td>
							<td>
								<select
									class="select w-24"
									name={`node:${node.id}:isHidden`}
									value={String(node.isHidden)}
									disabled={!data.canEdit}
								>
									<option value="false">visible</option>
									<option value="true">hidden</option>
								</select>
							</td>
							<td>
								<input
									class="input w-40"
									name={`node:${node.id}:unlockKey`}
									value={node.unlockKey ?? ''}
									placeholder="—"
									disabled={!data.canEdit}
								/>
							</td>
							<td>
								<input
									class="input w-48"
									name={`node:${node.id}:targetMapId`}
									value={node.targetMapId ?? ''}
									placeholder="—"
									disabled={!data.canEdit}
								/>
							</td>
							<td class="text-xs text-surface-600-400">{node.outgoing} out · {node.incoming} in</td>
						</tr>
					{/each}
				</tbody>
			</table>
		</div>

		{#if data.canEdit}
			<div class="flex justify-end gap-2">
				<button class="btn preset-tonal-surface" type="submit" formaction="?/validate">
					Validate
				</button>
				<button class="btn preset-filled-primary-500" type="submit">Validate and save</button>
			</div>
		{/if}
	</form>
</section>
