import { fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { toGameIdentity } from '$lib/server/access';
import { createSubmission } from '$lib/server/foundry-api';
import { stringField } from '$lib/server/form';

export const load: PageServerLoad = async () => ({});

function splitList(value: string): string[] {
	return value
		.split(',')
		.map((entry) => entry.trim())
		.filter(Boolean);
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

		const payload = {
			slug,
			displayName,
			typeKey: stringField(form, 'typeKey'),
			tonalCharacter: stringField(form, 'tonalCharacter'),
			description,
			rarityWeight: 50,
			isActive: true,
			phonology: {
				consonants: [],
				vowels: [],
				allowedSyllablePatterns: splitList(stringField(form, 'syllablePatterns')),
				allowedInitialClusters: splitList(stringField(form, 'initialClusters')),
				forbiddenClusters: splitList(stringField(form, 'forbiddenClusters')),
				allowedFinalClusters: splitList(stringField(form, 'finalClusters')),
				junctionRules: []
			},
			morphology: { prefixes: [], suffixes: [], roots: [] },
			registerSystem: { registers: [] },
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
