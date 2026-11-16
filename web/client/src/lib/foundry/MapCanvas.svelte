<script lang="ts">
	import { Move } from '@lucide/svelte';
	import {
		CARD,
		CELL,
		layoutCanvas,
		type CanvasCell,
		type CanvasPlacement
	} from '#lib/foundry/map-canvas';
	import type { MapGraphDto } from '#lib/foundry/map-editor';

	/**
	 * The graph as a picture you can point at.
	 *
	 * Nothing here decides where a node goes — `map-canvas.ts` owns that, including the rule that a node
	 * with no authored position is *not* assigned one. This component renders the result, reports a
	 * selection and, when the account may edit, a nudge.
	 */
	let {
		graph,
		canEdit = false,
		selectedId = null,
		onSelect,
		onMove,
		onTidy
	}: {
		graph: MapGraphDto;
		canEdit?: boolean;
		selectedId?: string | null;
		onSelect?: (nodeId: string) => void;
		onMove?: (nodeId: string, cell: CanvasCell) => void;
		onTidy?: () => void;
	} = $props();

	const layout = $derived(layoutCanvas(graph));
	const placedById = $derived(new Map(layout.placed.map((placement) => [placement.id, placement])));
	const width = $derived(Math.max(layout.bounds.width, 1) * CELL);
	const height = $derived(Math.max(layout.bounds.height, 1) * CELL);

	const lines = $derived(
		graph.edges.flatMap((edge) => {
			const from = placedById.get(edge.fromNodeId);
			const to = placedById.get(edge.toNodeId);
			return from && to ? [{ edge, from, to }] : [];
		})
	);

	const ARROWS: Record<string, CanvasCell> = {
		ArrowUp: { x: 0, y: -1 },
		ArrowDown: { x: 0, y: 1 },
		ArrowLeft: { x: -1, y: 0 },
		ArrowRight: { x: 1, y: 0 }
	};

	const offset = (cell: CanvasCell) => ({
		left: (cell.x - layout.bounds.minX) * CELL + (CELL - CARD) / 2,
		top: (cell.y - layout.bounds.minY) * CELL + (CELL - CARD) / 2
	});

	function nudge(event: KeyboardEvent, nodeId: string, cell: CanvasCell): void {
		const delta = ARROWS[event.key];
		if (!delta || !canEdit) return;

		event.preventDefault();
		onMove?.(nodeId, { x: cell.x + delta.x, y: cell.y + delta.y });
	}

	/** Where a pointer drag started, so the drop can be converted into whole cells. */
	let drag = $state<{ id: string; clientX: number; clientY: number } | null>(null);

	function startDrag(event: PointerEvent, placement: CanvasPlacement): void {
		if (!canEdit) return;

		drag = { id: placement.id, clientX: event.clientX, clientY: event.clientY };
		(event.currentTarget as HTMLElement | null)?.setPointerCapture?.(event.pointerId);
	}

	function endDrag(event: PointerEvent, placement: CanvasPlacement): void {
		if (!drag || drag.id !== placement.id) return;

		// Whole cells or nothing: a two-pixel wobble must not nudge the map.
		const dx = Math.round((event.clientX - drag.clientX) / CELL);
		const dy = Math.round((event.clientY - drag.clientY) / CELL);
		drag = null;

		if (dx === 0 && dy === 0) return;
		onMove?.(placement.id, { x: placement.x + dx, y: placement.y + dy });
	}
</script>

<section class="space-y-2" aria-label="Map canvas">
	<div class="flex flex-wrap items-center justify-between gap-2">
		<p class="text-xs text-surface-600-400">
			{layout.placed.length} positioned · {layout.unplaced.length} not positioned
		</p>
		{#if canEdit && onTidy}
			<button class="btn preset-tonal-surface btn-sm" type="button" onclick={onTidy}>
				<Move size={14} aria-hidden="true" />
				Tidy layout
			</button>
		{/if}
	</div>

	<div class="game-panel overflow-auto p-3" data-testid="map-canvas">
		<div class="relative" style:width="{width}px" style:height="{height}px">
			<svg
				class="pointer-events-none absolute inset-0 text-surface-500"
				style:width="{width}px"
				style:height="{height}px"
				aria-hidden="true"
			>
				{#each lines as line (line.edge.id)}
					<line
						x1={(line.from.x - layout.bounds.minX) * CELL + CELL / 2}
						y1={(line.from.y - layout.bounds.minY) * CELL + CELL / 2}
						x2={(line.to.x - layout.bounds.minX) * CELL + CELL / 2}
						y2={(line.to.y - layout.bounds.minY) * CELL + CELL / 2}
						stroke="currentColor"
						stroke-width="1.5"
						stroke-dasharray={line.edge.isHidden ? '4 3' : undefined}
					/>
				{/each}
			</svg>

			{#each layout.placed as placement (placement.id)}
				<button
					type="button"
					class="absolute flex flex-col items-center justify-center rounded border p-1 text-xs"
					class:border-primary-500={selectedId === placement.id}
					class:border-surface-600={selectedId !== placement.id}
					class:bg-surface-100-900={selectedId === placement.id}
					class:cursor-move={canEdit}
					style:left="{offset(placement).left}px"
					style:top="{offset(placement).top}px"
					style:width="{CARD}px"
					style:height="{CARD}px"
					aria-pressed={selectedId === placement.id}
					onclick={() => onSelect?.(placement.id)}
					onkeydown={(event) => nudge(event, placement.id, placement)}
					onpointerdown={(event) => startDrag(event, placement)}
					onpointerup={(event) => endDrag(event, placement)}
				>
					<span class="line-clamp-2 text-center">{placement.label}</span>
					{#if placement.isEntryPoint}
						<span class="badge preset-tonal-primary text-[10px]">entry</span>
					{:else if placement.isExitPoint}
						<span class="badge preset-tonal-surface text-[10px]">exit</span>
					{/if}
					<span class="text-[10px] text-surface-600-400">{placement.x},{placement.y}</span>
				</button>
			{/each}
		</div>
	</div>

	{#if layout.unplaced.length > 0}
		<div class="game-panel space-y-1 p-3">
			<p class="text-xs text-surface-600-400">
				No authored position. These are drawn from the auto-layout, not from the map, so moving one
				is what makes its position real.
			</p>
			<ul class="flex flex-wrap gap-2">
				{#each layout.unplaced as placement (placement.id)}
					<li>
						<button
							type="button"
							class="btn preset-tonal-surface btn-sm"
							onclick={() => onSelect?.(placement.id)}
						>
							{placement.label}
							<span class="badge preset-tonal-warning text-[10px]">not positioned</span>
						</button>
					</li>
				{/each}
			</ul>
		</div>
	{/if}
</section>
