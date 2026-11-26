import { fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { toGameIdentity } from '#lib/server/access';
import { getContentDetail } from '#lib/server/content-api';
import { createSubmission } from '#lib/server/foundry-api';
import { stringField } from '#lib/server/form';

export const load: PageServerLoad = async () => ({});

function splitList(value: string): string[] {
	return value
		.split(',')
		.map((entry) => entry.trim())
		.filter(Boolean);
}

/** Reads a nested payload object, so a stored language can seed what this form builds. */
function objectAt(value: unknown): Record<string, unknown> {
	return value !== null && typeof value === 'object' && !Array.isArray(value)
		? (value as Record<string, unknown>)
		: {};
}

/**
 * The stored language at this slug, or `{}` when the form is proposing a new one.
 *
 * This form has no field for consonants, vowels, junction rules, morphology or the register system, so a
 * correction submitted for an existing slug would otherwise carry only the fields above — and approval, which
 * replaces the entity, would delete the rest. Corrections start from the stored row instead; every field the
 * form does submit still wins.
 */
async function storedLanguage(slug: string): Promise<Record<string, unknown>> {
	const existing = await getContentDetail('language', slug);
	const payload = existing.data?.payload;

	return payload && payload.slug === slug ? payload : {};
}

export const actions: Actions = {
	default: async ({ request, locals }) => {
		const form = await request.formData();
		const displayName = stringField(form, 'displayName');
		const slug = stringField(form, 'slug');
		const description = stringField(form, 'description');

		if (!displayName || !slug) {
			return fail(400, { error: 'Display name and slug are required.' });
		}

		if (!/^[a-z0-9-]+$/.test(slug)) {
			return fail(400, {
				error: 'The slug may only contain lowercase letters, numbers, and hyphens.'
			});
		}

		const base = await storedLanguage(slug);
		const morphology = objectAt(base.morphology);
		const registerSystem = objectAt(base.registerSystem);

		const payload = {
			...base,
			slug,
			displayName,
			typeKey: stringField(form, 'typeKey'),
			tonalCharacter: stringField(form, 'tonalCharacter'),
			description,
			rarityWeight: typeof base.rarityWeight === 'number' ? base.rarityWeight : 50,
			isActive: typeof base.isActive === 'boolean' ? base.isActive : true,
			phonology: {
				...objectAt(base.phonology),
				allowedSyllablePatterns: splitList(stringField(form, 'syllablePatterns')),
				allowedInitialClusters: splitList(stringField(form, 'initialClusters')),
				forbiddenClusters: splitList(stringField(form, 'forbiddenClusters')),
				allowedFinalClusters: splitList(stringField(form, 'finalClusters'))
			},
			morphology:
				Object.keys(morphology).length > 0 ? morphology : { prefixes: [], suffixes: [], roots: [] },
			registerSystem: Object.keys(registerSystem).length > 0 ? registerSystem : { registers: [] },
			sampleText: stringField(form, 'sampleText')
		};

		const result = await createSubmission(toGameIdentity(locals.user!), {
			contentType: 'language',
			title: displayName,
			payload: JSON.stringify(payload),
			description
		});

		if (result.error || !result.data) {
			return fail(400, { error: result.error ?? 'Could not submit the language.' });
		}

		redirect(303, `/submissions/${result.data.id}`);
	}
};
