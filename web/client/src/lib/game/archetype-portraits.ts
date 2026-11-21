/**
 * Where an archetype portrait lives, and which frame it wears.
 *
 * The art is addressed by convention, never by a content column. The approved visual system
 * (`docs/design/content/archetype-visuals.md` §5.2; the weights are `asset-specs.md` §3) fixes one
 * family per slug:
 *
 *     archetype-<slug>-portrait-<size>.<ext>
 *
 * A slug has three files under `static/images/archetypes/`: a 256 px PNG-24 master that is the export
 * source and is never served, plus the two WebPs this module addresses — 128 px (2× the rendered box)
 * and 64 px (1×). The largest rendered portrait is 64 px, so those are the only two URLs a surface may
 * request. Content and art therefore land independently: an archetype with no file simply has no
 * portrait, and the surface draws its fallback instead of failing.
 *
 * The frame is CSS (R3), so re-banding an archetype changes a prop and never a drawing.
 */

/** The two served portrait widths. Both are WebP; the 256 px PNG is a master, not a surface. */
export type ArchetypePortraitSize = 128 | 64;

/** The five silhouette classes of the visual system (R1). */
export type ArchetypeSilhouette = 'beast' | 'humanoid' | 'unquiet' | 'spirit' | 'townsfolk';

/**
 * The frame the UI draws around a portrait (R3).
 *
 * Deliberately named separately from the silhouette: re-banding an archetype
 * changes the tier and never the art.
 */
export type ArchetypeTier = 'boss' | 'elite' | 'regular' | 'townsfolk';

/** Directory the portrait family is served from (`web/client/static/images/archetypes/`). */
export const ARCHETYPE_PORTRAIT_DIR = '/images/archetypes';

/**
 * Builds the portrait URL for a slug and export size.
 *
 * @param slug The archetype slug; blank or absent means "no portrait".
 * @param size The served file width: 64 for the 1× tile, 128 for the 2× source.
 * @returns The URL, or `null` when there is no slug to address.
 */
export function archetypePortraitPath(
	slug: string | null | undefined,
	size: ArchetypePortraitSize
): string | null {
	const key = slug?.trim();
	if (!key) return null;
	return `${ARCHETYPE_PORTRAIT_DIR}/archetype-${key}-portrait-${size}.webp`;
}

/**
 * Maps a content `typeKey` onto one of the five silhouette classes.
 *
 * `elementals` share the `spirit` class (one non-limbed mass) and anything
 * unrecognised — a new type key, a sparse record — falls back to `humanoid`,
 * which is the safest glyph to show for something that stands upright.
 *
 * @param typeKey The archetype's `typeKey`, as stored.
 * @returns The silhouette class.
 */
export function silhouetteForTypeKey(typeKey: string | null | undefined): ArchetypeSilhouette {
	switch (typeKey?.trim().toLowerCase()) {
		case 'beasts':
			return 'beast';
		case 'undead':
			return 'unquiet';
		case 'spirits':
		case 'elementals':
			return 'spirit';
		case 'townspeople':
			return 'townsfolk';
		default:
			return 'humanoid';
	}
}

/**
 * The slugs whose drawn class is not what their `typeKey` says.
 *
 * The visual system records exactly one: `goblin-scout` is seeded `beasts` while it is a biped with a
 * spear, so its visual class is humanoid (`archetype-visuals.md` §4.2). The `typeKey` is a
 * content-taxonomy field, not a drawing instruction, and the fallback glyph has to match the
 * silhouette the art will actually have. This is one row, and it goes away when the seeder is
 * corrected — it is not a second taxonomy.
 */
const SILHOUETTE_BY_SLUG: Readonly<Record<string, ArchetypeSilhouette>> = {
	'goblin-scout': 'humanoid'
};

/**
 * The visual class an archetype's portrait and fallback glyph use.
 *
 * Prefers the recorded per-slug correction, then the content `typeKey`. A caller that has only a slug
 * (the combat tile) still gets the recorded class; a content surface passes the type key so a new
 * archetype classifies without a client change.
 *
 * @param slug The archetype slug.
 * @param typeKey The archetype's `typeKey`, when the caller has one.
 * @returns The silhouette class.
 */
export function archetypeSilhouette(
	slug: string | null | undefined,
	typeKey?: string | null
): ArchetypeSilhouette {
	const key = slug?.trim().toLowerCase();
	if (key && key in SILHOUETTE_BY_SLUG) return SILHOUETTE_BY_SLUG[key];
	return silhouetteForTypeKey(typeKey);
}

/**
 * Reads the tier a content payload should draw.
 *
 * Townspeople are the plate tier and are recognised by content type before any trait flag, because a
 * shopkeeper carrying `Boss: Yes` by mistake should still read as a townsperson. Everything else is
 * boss over elite over regular, read off the inert `traits` labels.
 *
 * @param traits The `traits` object of a content payload, if any.
 * @param typeKey The archetype's `typeKey`, which fixes the plate tier.
 * @returns The tier the frame should draw.
 */
export function archetypeTierFromTraits(traits: unknown, typeKey?: string | null): ArchetypeTier {
	if (typeKey?.trim().toLowerCase() === 'townspeople') return 'townsfolk';
	if (traits === null || typeof traits !== 'object') return 'regular';
	const record = traits as Record<string, unknown>;
	if (record.boss === true) return 'boss';
	if (record.elite === true) return 'elite';
	return 'regular';
}

/**
 * Normalises a tier string that arrived over the wire.
 *
 * The server sends `boss`, `elite` or `regular`; a townspeople archetype upgrades to the plate from
 * its `typeKey`, and anything else (a future tier, a missing field) renders as an unframed regular
 * rather than throwing.
 *
 * @param value The tier as received.
 * @param typeKey The enemy's archetype `typeKey`, when the wire carried one.
 * @returns A known tier.
 */
export function toArchetypeTier(
	value: string | null | undefined,
	typeKey?: string | null
): ArchetypeTier {
	if (typeKey?.trim().toLowerCase() === 'townspeople') return 'townsfolk';
	return value === 'boss' || value === 'elite' || value === 'townsfolk' ? value : 'regular';
}
