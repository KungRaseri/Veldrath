<script lang="ts">
	import { untrack } from 'svelte';
	import { ArrowLeft, Check, Redo2, ShieldCheck, TriangleAlert, Undo2 } from '@lucide/svelte';
	import EdgeList from '#lib/foundry/EdgeList.svelte';
	import MapCanvas from '#lib/foundry/MapCanvas.svelte';
	import NodeInspector from '#lib/foundry/NodeInspector.svelte';
	import { moveNode as moveNodeTo, tidyLayout, type CanvasCell } from '#lib/foundry/map-canvas';
	import { connectNodes, deleteEdge, patchEdge, reciprocalOf } from '#lib/foundry/map-edges';
	import { addNode, deleteBlockedReason, deleteNode, NODE_TYPES } from '#lib/foundry/map-nodes';
	import {
		issueLabel,
		issuesByNode,
		nodeRows,
		normalizeDraft,
		type MapEdgeDto,
		type MapGraphDto,
		type MapNodeDto
	} from '#lib/foundry/map-editor';
	import type { PageData } from './$types';

	interface MapForm {
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
	}

	let { data, form }: { data: PageData; form: MapForm | null } = $props();

	/**
	 * One draft, one writer.
	 *
	 * The canvas and the table both mutate this object; the actions post it whole. Nothing reconstructs a
	 * graph from individual fields on the way out, so a canvas gesture cannot be silently dropped.
	 */
	// `untrack` is deliberate: the draft is seeded once and then owned by the editor. A new map resets it
	// (below); a save does not, because the draft is what was just saved.
	let draft = $state<MapGraphDto>(untrack(() => data.graph));
	let loadedMapId = untrack(() => data.graph.id);
	let past = $state<MapGraphDto[]>([]);
	let future = $state<MapGraphDto[]>([]);
	let selectedId = $state<string | null>(null);
	let notice = $state<string | null>(null);
	let newNodeType = $state<string>('Location');
	let newNodeName = $state<string>('');
	let connectFrom = $state<string | null>(null);

	const rows = $derived(nodeRows(draft));
	const issues = $derived(form?.issues ?? []);
	const grouped = $derived(issuesByNode(issues));
	const posted = $derived(JSON.stringify(normalizeDraft(draft)));
	const isDirty = $derived(posted !== JSON.stringify(normalizeDraft(data.graph)));
	const selectedNode = $derived(draft.nodes.find((node) => node.id === selectedId) ?? null);
	const blockedReason = $derived(selectedNode ? deleteBlockedReason(draft, selectedNode.id) : null);

	/** Records a structural change so it can be undone. Text edits deliberately do not push history. */
	function remember(next: MapGraphDto): void {
		past = [...past, draft];
		future = [];
		draft = next;
	}

	function move(id: string, cell: CanvasCell): void {
		const next = moveNodeTo(draft, id, cell);

		if (next === draft) {
			notice = 'That cell is already taken. Move the other node first.';
			return;
		}

		notice = null;
		remember(next);
	}

	function tidy(): void {
		remember(tidyLayout(draft));
		notice = null;
	}

	function patchNode(id: string, patch: Partial<MapNodeDto>): void {
		draft = {
			...draft,
			nodes: draft.nodes.map((node) => (node.id === id ? { ...node, ...patch } : node))
		};
	}

	/** Adds a node of the chosen kind and selects it, so the inspector is where the author lands. */
	function createNode(): void {
		const next = addNode(draft, { nodeType: newNodeType, displayName: newNodeName });
		const added = next.nodes[next.nodes.length - 1];

		notice = null;
		remember(next);
		selectedId = added.id;
		newNodeName = '';
	}

	function removeSelected(): void {
		if (!selectedNode || blockedReason !== null) return;

		remember(deleteNode(draft, selectedNode.id));
		selectedId = null;
		notice = null;
	}

	/** The canvas selection is either an inspector target or the second half of a connect gesture. */
	function selectNode(nodeId: string): void {
		if (!connectFrom) {
			selectedId = nodeId;
			return;
		}

		if (connectFrom === nodeId) {
			connectFrom = null;
			return;
		}

		const result = connectNodes(draft, connectFrom, nodeId, { reciprocal: true });
		connectFrom = null;

		if (!result.ok) {
			notice = result.reason;
			return;
		}

		notice = null;
		remember(result.graph);
		selectedId = nodeId;
	}

	function startConnect(): void {
		if (connectFrom) {
			connectFrom = null;
			return;
		}

		if (!selectedId) {
			notice = 'Select the node the edge should start from first.';
			return;
		}

		connectFrom = selectedId;
		notice = null;
	}

	/** A hand-edited direction must not create the duplicate the validator refuses. */
	function patchEdgeField(edgeId: string, patch: Partial<MapEdgeDto>): void {
		const edge = draft.edges.find((candidate) => candidate.id === edgeId);
		if (!edge) return;

		if (patch.direction) {
			const clash = draft.edges.some(
				(candidate) =>
					candidate.id !== edgeId &&
					candidate.fromNodeId === edge.fromNodeId &&
					candidate.direction === patch.direction
			);

			if (clash) {
				notice = `That node already has an edge going ${patch.direction}.`;
				return;
			}
		}

		notice = null;
		remember(patchEdge(draft, edgeId, patch));
	}

	/** A connection that runs both ways is removed both ways: a stranded one-way edge is a trap. */
	function removeEdge(edgeId: string): void {
		const edge = draft.edges.find((candidate) => candidate.id === edgeId);
		remember(deleteEdge(draft, edgeId, edge ? reciprocalOf(draft, edge) !== null : false));
		notice = null;
	}

	function undo(): void {
		if (past.length === 0) return;
		future = [draft, ...future];
		draft = past[past.length - 1];
		past = past.slice(0, -1);
	}

	function redo(): void {
		if (future.length === 0) return;
		past = [...past, draft];
		draft = future[0];
		future = future.slice(1);
	}

	// Navigating to another graph reuses this component, so the draft has to be re-seeded for a new map.
	$effect(() => {
		if (data.graph.id === loadedMapId) return;

		loadedMapId = data.graph.id;
		draft = data.graph;
		past = [];
		future = [];
		selectedId = null;
	});

	// A draft that would be lost by leaving is worth one browser-level confirmation.
	$effect(() => {
		if (!isDirty) return;

		const warn = (event: BeforeUnloadEvent) => event.preventDefault();
		window.addEventListener('beforeunload', warn);
		return () => window.removeEventListener('beforeunload', warn);
	});
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
				{draft.nodes.length} nodes · {draft.edges.length} edges
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

	{#if notice}
		<p class="game-panel p-3 text-sm text-warning-500" role="status">{notice}</p>
	{/if}

	<MapCanvas
		graph={draft}
		canEdit={data.canEdit}
		{selectedId}
		onSelect={selectNode}
		onMove={move}
		onTidy={tidy}
	/>

	{#if data.canEdit}
		<div class="game-panel flex flex-wrap items-end gap-2 p-3">
			<label class="space-y-1 text-sm">
				<span class="text-surface-600-400">Add a node</span>
				<select class="select" bind:value={newNodeType}>
					{#each NODE_TYPES as nodeType (nodeType)}
						<option value={nodeType}>{nodeType}</option>
					{/each}
				</select>
			</label>
			<label class="space-y-1 text-sm">
				<span class="text-surface-600-400">Label</span>
				<input class="input" bind:value={newNodeName} placeholder="New node" />
			</label>
			<button class="btn preset-tonal-surface btn-sm" type="button" onclick={createNode}>
				Add
			</button>
			<button
				class="btn preset-tonal-surface btn-sm"
				class:preset-filled-primary-500={connectFrom !== null}
				type="button"
				onclick={startConnect}
			>
				{connectFrom ? 'Pick the destination…' : 'Connect from selection'}
			</button>
		</div>
	{/if}

	{#if selectedNode}
		<NodeInspector
			node={selectedNode}
			targets={data.targets}
			canEdit={data.canEdit}
			{blockedReason}
			connections={draft.edges.filter(
				(edge) => edge.fromNodeId === selectedNode.id || edge.toNodeId === selectedNode.id
			).length}
			onPatch={(patch) => patchNode(selectedNode.id, patch)}
			onDelete={removeSelected}
		/>
	{/if}

	<EdgeList graph={draft} canEdit={data.canEdit} onPatch={patchEdgeField} onDelete={removeEdge} />

	<form method="POST" action="?/save" class="game-panel space-y-3 p-4">
		<input type="hidden" name="version" value={data.graph.version} />
		<input type="hidden" name="graph" value={posted} />

		<div class="flex flex-wrap items-center justify-between gap-2">
			<p class="text-sm text-surface-600-400">
				Labels, hidden flags, unlock keys and exit targets are edited here. Positions are authored
				on the canvas above.
			</p>
			<div class="flex items-center gap-2">
				{#if isDirty}
					<span class="badge preset-tonal-warning text-xs">unsaved changes</span>
				{/if}
				<button
					class="btn preset-tonal-surface btn-sm"
					type="button"
					onclick={undo}
					disabled={past.length === 0}
				>
					<Undo2 size={14} aria-hidden="true" />
					Undo
				</button>
				<button
					class="btn preset-tonal-surface btn-sm"
					type="button"
					onclick={redo}
					disabled={future.length === 0}
				>
					<Redo2 size={14} aria-hidden="true" />
					Redo
				</button>
			</div>
		</div>

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
					{#each rows as node (node.id)}
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
									oninput={(event) =>
										patchNode(node.id, { displayName: event.currentTarget.value })}
								/>
							</td>
							<td>
								<select
									class="select w-24"
									name={`node:${node.id}:isHidden`}
									value={String(node.isHidden)}
									disabled={!data.canEdit}
									onchange={(event) =>
										patchNode(node.id, { isHidden: event.currentTarget.value === 'true' })}
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
									oninput={(event) => patchNode(node.id, { unlockKey: event.currentTarget.value })}
								/>
							</td>
							<td>
								<input
									class="input w-48"
									name={`node:${node.id}:targetMapId`}
									value={node.targetMapId ?? ''}
									placeholder="—"
									disabled={!data.canEdit}
									oninput={(event) =>
										patchNode(node.id, { targetMapId: event.currentTarget.value })}
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
