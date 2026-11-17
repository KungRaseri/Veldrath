import type { PageServerLoad } from './$types';
import { getContentTypeSchema, listContentTypes } from '#lib/server/content-api';
import type { ContentTypeSchema } from '#lib/foundry/content-editor';

/**
 * The schema registry, as a page.
 *
 * Every form in the portal is generated from these descriptors, so they are the reference an author reads to
 * understand what a field means — and the reference the field-kind parity suite compares against. It is open
 * to every signed-in account because the schema is what a contributor needs before they write anything.
 */
export const load: PageServerLoad = async () => {
	const types = await listContentTypes();
	const loaded = await Promise.all(
		(types.data ?? []).map(async (type) => {
			const schema = await getContentTypeSchema(type.contentType);
			return schema.data as ContentTypeSchema | null;
		})
	);

	return {
		// A type whose schema failed to load is simply absent rather than an empty shell.
		schemas: loaded.filter((schema): schema is ContentTypeSchema => schema !== null),
		error: types.error
	};
};
