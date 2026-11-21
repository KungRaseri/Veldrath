<script lang="ts">
	import { Flame, Ghost, PawPrint, Swords, UserRound } from '@lucide/svelte';
	import {
		archetypePortraitPath,
		type ArchetypePortraitSize,
		type ArchetypeSilhouette,
		type ArchetypeTier
	} from './archetype-portraits';

	/**
	 * One archetype portrait, framed by its tier.
	 *
	 * The file is addressed by slug convention — `archetype-<slug>-portrait-<size>.webp` — so content and
	 * art land independently: when the file does not exist the browser reports a load error, this
	 * component swaps in the class's Lucide glyph, and the surrounding name stays readable. A missing
	 * portrait is not an error and is not logged.
	 *
	 * The box is 64 px, the largest size the visual system renders. Both served exports are offered
	 * through `srcset` (64 px at 1×, 128 px at 2×) and `size` only picks the primary source: the tile
	 * names the 1× file, the detail surface names the 2× one.
	 *
	 * The frame is CSS (R3): a re-banded archetype changes its tier prop and never its art.
	 */
	let {
		slug = null,
		size = 64,
		tier = 'regular',
		silhouette = 'humanoid',
		alt = ''
	}: {
		/** The archetype slug the portrait is addressed by; absent means "fall back". */
		slug?: string | null;
		/** The primary source width: 64 for a tile, 128 for the detail surface. */
		size?: ArchetypePortraitSize;
		/** The frame to draw — never baked into the image. */
		tier?: ArchetypeTier;
		/** The glyph that stands in when no file exists, by silhouette class. */
		silhouette?: ArchetypeSilhouette;
		/** Accessible description; empty when the archetype's name already sits beside the frame. */
		alt?: string;
	} = $props();

	const SILHOUETTE_GLYPHS = {
		beast: PawPrint,
		humanoid: Swords,
		unquiet: Ghost,
		spirit: Flame,
		townsfolk: UserRound
	};

	/** The file that failed to load. Remembering it (rather than a bare boolean) retries when the slug changes. */
	let brokenSrc = $state<string | null>(null);

	const src = $derived(archetypePortraitPath(slug, size));
	const oneToOne = $derived(archetypePortraitPath(slug, 64));
	const twoToOne = $derived(archetypePortraitPath(slug, 128));
	const srcset = $derived(oneToOne && twoToOne ? `${oneToOne} 64w, ${twoToOne} 128w` : undefined);
	const showImage = $derived(src !== null && src !== brokenSrc);
	const Glyph = $derived(SILHOUETTE_GLYPHS[silhouette]);
</script>

<span
	class="archetype-portrait archetype-portrait--{tier}"
	data-testid="archetype-portrait"
	data-tier={tier}
>
	{#if showImage && src}
		<img
			class="archetype-portrait__image"
			{src}
			{srcset}
			sizes="64px"
			{alt}
			width={64}
			height={64}
			loading="lazy"
			decoding="async"
			onerror={() => (brokenSrc = src)}
		/>
	{:else}
		<span class="archetype-portrait__fallback" aria-hidden="true">
			<Glyph size={size === 128 ? 32 : 24} />
		</span>
	{/if}
</span>
