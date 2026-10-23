<script lang="ts">
	import { SvelteURLSearchParams } from 'svelte/reactivity';

	let {
		page,
		pageSize,
		total,
		basePath,
		query = {}
	}: {
		page: number;
		pageSize: number;
		total: number;
		basePath: string;
		query?: Record<string, string | null | undefined>;
	} = $props();

	const totalPages = $derived(Math.max(1, Math.ceil(total / Math.max(1, pageSize))));

	function href(target: number): string {
		const params = new SvelteURLSearchParams();
		for (const [key, value] of Object.entries(query)) {
			if (value) params.set(key, value);
		}
		params.set('page', String(target));
		return `${basePath}?${params.toString()}`;
	}
</script>

{#if totalPages > 1}
	<nav
		class="flex flex-wrap items-center justify-center gap-3 py-4 text-sm"
		aria-label="Pagination"
	>
		<a
			class="btn preset-tonal-surface btn-sm {page <= 1 ? 'pointer-events-none opacity-50' : ''}"
			href={href(Math.max(1, page - 1))}
			aria-disabled={page <= 1}
		>
			← Prev
		</a>
		<span class="text-surface-600-400">Page {page} of {totalPages}</span>
		<a
			class="btn preset-tonal-surface btn-sm {page >= totalPages
				? 'pointer-events-none opacity-50'
				: ''}"
			href={href(Math.min(totalPages, page + 1))}
			aria-disabled={page >= totalPages}
		>
			Next →
		</a>
	</nav>
{/if}
