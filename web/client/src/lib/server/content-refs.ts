import { browseContent } from '#lib/server/content-api';
import type { ContentField } from '#lib/foundry/content-editor';

/** The content types a `Reference` field names, including ones nested inside an `ObjectList` row. */
export function referenceTypes(fields: readonly ContentField[]): string[] {
	const found = new Set<string>();

	for (const field of fields) {
		if (field.fieldType === 'Reference' && field.refType) found.add(field.refType);
		for (const type of referenceTypes(field.items ?? [])) found.add(type);
	}

	return [...found];
}

/**
 * Loads the slug list behind every `Reference` field in a schema.
 *
 * A picker that names content has to show what exists, or it is a text box with extra steps. Fifty is the
 * page size the browse endpoint caps at for a comfortable type-ahead, and the field stays free text so an
 * author can still point at a slug that has not been created yet.
 *
 * @param fields Every field of the type, including row descriptors.
 * @returns The options keyed by content type; a failed browse yields an empty list.
 */
export async function referenceOptions(
	fields: readonly ContentField[]
): Promise<Record<string, { value: string; label: string }[]>> {
	const entries = await Promise.all(
		referenceTypes(fields).map(async (refType) => {
			const result = await browseContent(refType, null, 1, 50);
			const options = (result.data?.items ?? []).map((entry) => ({
				value: entry.slug,
				label: entry.displayName ?? entry.slug
			}));

			return [refType, options] as const;
		})
	);

	return Object.fromEntries(entries) as Record<string, { value: string; label: string }[]>;
}
