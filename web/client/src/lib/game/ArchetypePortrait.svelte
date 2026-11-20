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
	 * The image is addressed by slug convention so content and art land independently. When the file
	 * does not exist the browser reports a load error, this component swaps in the class's Lucide glyph
	 * and the surrounding name stays readable — a missing portrait is not an error and is not logged.
	 *
	 * The frame is CSS (R3): a re-banded archetype changes its tier prop and never its art.
	 */
	let {
		slug = null,
		size = 128,
		tier = 'regular',
		silhouette = 'humanoid',
		alt = ''
	}: {
		/** The archetype slug the portrait is addressed by; absent means "fall back". */
		slug?: string | null;
		/** The served file width. 128 renders a 64 px tile and 256 a 128 px detail. */
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
	const showImage = $derived(src !== null && src !== brokenSrc);
	const Glyph = $derived(SILHOUETTE_GLYPHS[silhouette]);
</script>

<span
	class="archetype-portrait archetype-portrait--{tier}"
	class:archetype-portrait--detail={size === 256}
	data-testid="archetype-portrait"
	data-tier={tier}
>
	{#if showImage && src}
		<img
			class="archetype-portrait__image"
			{src}
			{alt}
			width={size}
			height={size}
			loading="lazy"
			decoding="async"
			onerror={() => (brokenSrc = src)}
		/>
	{:else}
		<span class="archetype-portrait__fallback" aria-hidden="true">
			<Glyph size={size === 256 ? 32 : 24} />
		</span>
	{/if}
</span>
