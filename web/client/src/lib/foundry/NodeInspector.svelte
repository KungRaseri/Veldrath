<script lang="ts">
	import { Trash2 } from '@lucide/svelte';
	import { isExitNodeType, refOptionsFor, UNLOCK_TYPES } from '#lib/foundry/map-nodes';
	import type { MapNodeDto, MapTargets } from '#lib/foundry/map-editor';

	/**
	 * The inspector for one node.
	 *
	 * It renders and dispatches: every change goes back through `onPatch`, so the page keeps owning the
	 * single draft. The reference fields are pickers wherever the catalog has options — a picker cannot
	 * produce the mistyped slug the world validator has to catch.
	 */
	let {
		node,
		targets,
		canEdit,
		blockedReason = null,
		onPatch,
		onDelete
	}: {
		node: MapNodeDto;
		targets: MapTargets;
		canEdit: boolean;
		blockedReason?: string | null;
		onPatch: (patch: Partial<MapNodeDto>) => void;
		onDelete: () => void;
	} = $props();

	const refOptions = $derived(refOptionsFor(node.nodeType, targets));
	const isExit = $derived(isExitNodeType(node.nodeType));
	const needsKey = $derived(['quest', 'item', 'achievement'].includes(node.unlockType ?? ''));
</script>

<section class="game-panel space-y-3 p-4" aria-label="Node inspector">
	<header class="flex items-start justify-between gap-2">
		<div>
			<h2 class="h4">{node.displayName || node.id}</h2>
			<p class="font-mono text-xs text-surface-600-400">{node.id}</p>
		</div>
		<span class="badge preset-tonal-surface text-xs">{node.nodeType}</span>
	</header>

	<div class="grid gap-3 sm:grid-cols-2">
		<label class="space-y-1 text-sm">
			<span class="text-surface-600-400">Label</span>
			<input
				class="input w-full"
				value={node.displayName}
				disabled={!canEdit}
				oninput={(event) => onPatch({ displayName: event.currentTarget.value })}
			/>
		</label>

		<label class="space-y-1 text-sm">
			<span class="text-surface-600-400">Sort order</span>
			<input
				class="input w-full"
				type="number"
				value={node.sortOrder}
				disabled={!canEdit}
				oninput={(event) =>
					onPatch({
						sortOrder: Number.isFinite(event.currentTarget.valueAsNumber)
							? event.currentTarget.valueAsNumber
							: node.sortOrder
					})}
			/>
		</label>

		<label class="space-y-1 text-sm sm:col-span-2">
			<span class="text-surface-600-400">Description</span>
			<textarea
				class="textarea w-full"
				rows="2"
				value={node.description}
				disabled={!canEdit}
				oninput={(event) => onPatch({ description: event.currentTarget.value })}></textarea>
		</label>

		{#if refOptions.length > 0}
			<label class="space-y-1 text-sm sm:col-span-2">
				<span class="text-surface-600-400">Reference</span>
				<select
					class="select w-full"
					value={node.refId ?? ''}
					disabled={!canEdit}
					onchange={(event) =>
						onPatch({ refId: event.currentTarget.value === '' ? null : event.currentTarget.value })}
				>
					<option value="">— none —</option>
					{#each refOptions as option (option.id)}
						<option value={option.id}>{option.label} · {option.id}</option>
					{/each}
				</select>
			</label>
		{:else}
			<label class="space-y-1 text-sm sm:col-span-2">
				<span class="text-surface-600-400">Reference (free text)</span>
				<input
					class="input w-full"
					value={node.refId ?? ''}
					placeholder="—"
					disabled={!canEdit}
					oninput={(event) =>
						onPatch({ refId: event.currentTarget.value === '' ? null : event.currentTarget.value })}
				/>
			</label>
		{/if}

		<label class="space-y-1 text-sm">
			<span class="text-surface-600-400">Hidden</span>
			<select
				class="select w-full"
				value={String(node.isHidden)}
				disabled={!canEdit}
				onchange={(event) => onPatch({ isHidden: event.currentTarget.value === 'true' })}
			>
				<option value="false">visible</option>
				<option value="true">hidden</option>
			</select>
		</label>

		<label class="space-y-1 text-sm">
			<span class="text-surface-600-400">Unlock type</span>
			<select
				class="select w-full"
				value={node.unlockType ?? ''}
				disabled={!canEdit}
				onchange={(event) =>
					onPatch({
						unlockType: event.currentTarget.value === '' ? null : event.currentTarget.value
					})}
			>
				<option value="">— none —</option>
				{#each UNLOCK_TYPES as unlockType (unlockType)}
					<option value={unlockType}>{unlockType}</option>
				{/each}
			</select>
		</label>

		{#if needsKey}
			<label class="space-y-1 text-sm">
				<span class="text-surface-600-400">Unlock key</span>
				<input
					class="input w-full"
					value={node.unlockKey ?? ''}
					placeholder="quest, item or achievement slug"
					disabled={!canEdit}
					oninput={(event) =>
						onPatch({
							unlockKey: event.currentTarget.value === '' ? null : event.currentTarget.value
						})}
				/>
			</label>
		{:else if node.unlockType === 'skill-check'}
			<label class="space-y-1 text-sm">
				<span class="text-surface-600-400">Discover threshold</span>
				<input
					class="input w-full"
					type="number"
					value={node.discoverThreshold ?? ''}
					disabled={!canEdit}
					oninput={(event) =>
						onPatch({
							discoverThreshold: Number.isFinite(event.currentTarget.valueAsNumber)
								? event.currentTarget.valueAsNumber
								: null
						})}
				/>
			</label>
		{/if}

		{#if isExit}
			<label class="space-y-1 text-sm">
				<span class="text-surface-600-400">Target map</span>
				<select
					class="select w-full"
					value={node.targetMapId ?? ''}
					disabled={!canEdit}
					onchange={(event) =>
						onPatch({
							targetMapId: event.currentTarget.value === '' ? null : event.currentTarget.value
						})}
				>
					<option value="">— none —</option>
					{#each targets.maps as option (option.id)}
						<option value={option.id}>{option.label} · {option.id}</option>
					{/each}
				</select>
			</label>

			<label class="space-y-1 text-sm">
				<span class="text-surface-600-400">Target node</span>
				<input
					class="input w-full"
					value={node.targetNodeId ?? ''}
					placeholder="node id on the target map"
					disabled={!canEdit}
					oninput={(event) =>
						onPatch({
							targetNodeId: event.currentTarget.value === '' ? null : event.currentTarget.value
						})}
				/>
			</label>
		{/if}
	</div>

	<div class="flex flex-wrap items-center justify-between gap-2">
		<p class="text-xs text-surface-600-400">
			{blockedReason ??
				'The node id and kind are fixed after creation; changing either moves meaning.'}
		</p>
		<button
			class="btn preset-tonal-surface btn-sm"
			type="button"
			disabled={!canEdit || blockedReason !== null}
			onclick={onDelete}
		>
			<Trash2 size={14} aria-hidden="true" />
			Delete node
		</button>
	</div>
</section>
