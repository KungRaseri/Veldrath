/**
 * Where an archetype portrait lives, and which frame it wears.
 *
 * The art is addressed by convention, never by a content column: a portrait for
 * `giant-rat` is served from `<dir>/giant-rat-128.webp` (tile) or
 * `<dir>/giant-rat-256.webp` (detail). Content and art therefore land
 * independently — an archetype with no file simply has no portrait, and the
 * surface draws its fallback instead of failing.
 *
 * The contract is `docs/design/content/archetype-visuals.md` §1 (approved
 * 2026-11-20) and the weights are `docs/design/asset-specs.md` §3.
 */

/** The two served portrait widths. A file is 2× the box it renders in. */
export type ArchetypePortraitSize = 128 | 256;

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
 * @param size The served file width (128 for a tile, 256 for a detail view).
 * @returns The URL, or `null` when there is no slug to address.
 */
export function archetypePortraitPath(
	slug: string | null | undefined,
	size: ArchetypePortraitSize
): string | null {
	const key = slug?.trim();
	if (!key) return null;
	return `${ARCHETYPE_PORTRAIT_DIR}/${key}-${size}.webp`;
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
 * Reads the tier flags off an archetype's `traits` object.
 *
 * Boss wins over elite; both are inert content labels today, which is exactly
 * why the frame is drawn from the data rather than baked into the portrait.
 *
 * @param traits The `traits` object of a content payload, if any.
 * @returns The tier the frame should draw.
 */
export function archetypeTierFromTraits(traits: unknown): ArchetypeTier {
	if (traits === null || typeof traits !== 'object') return 'regular';
	const record = traits as Record<string, unknown>;
	if (record.boss === true) return 'boss';
	if (record.elite === true) return 'elite';
	return 'regular';
}

/**
 * Normalises a tier string that arrived over the wire.
 *
 * The server sends `boss`, `elite` or `regular`; anything else (a future tier,
 * a missing field) renders as an unframed regular rather than throwing.
 *
 * @param value The tier as received.
 * @returns A known tier.
 */
export function toArchetypeTier(value: string | null | undefined): ArchetypeTier {
	return value === 'boss' || value === 'elite' || value === 'townsfolk' ? value : 'regular';
}
