import { error, fail } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { requireUser, toGameIdentity } from '#lib/server/access';
import { getMap, saveMap, validateMap } from '#lib/server/maps-api';
import { nodeRows, parseDraftGraph, type MapGraphDto } from '#lib/foundry/map-editor';

/**
 * Loads one graph, its node rows, and whether this account may save it.
 *
 * Reading requires `content.view`; saving requires `content.manage`. The page is not the guard for the
 * write — the API enforces it — but `canEdit` decides whether the controls render at all.
 */
export const load: PageServerLoad = async ({ locals, params, parent }) => {
	const user = requireUser(locals);
	const { canViewContent, canManageContent } = await parent();

	if (!canViewContent) {
		error(403, 'Reading a map graph requires content read access.');
	}

	const result = await getMap(toGameIdentity(user), params.mapId);
	if (!result.data) {
		error(result.status === 404 ? 404 : result.status || 502, result.error ?? 'Map not found.');
	}

	return { canEdit: canManageContent, graph: result.data, rows: nodeRows(result.data) };
};

/**
 * Reads the draft the editor posted.
 *
 * The canvas and the table both mutate one local draft, so the save and the dry run post that draft as a
 * whole rather than a field per input. Anything unparseable falls back to the stored graph, which makes the
 * request a no-op instead of a partial write.
 */
function readDraft(form: FormData, stored: MapGraphDto): MapGraphDto {
	return parseDraftGraph(form.get('graph'), stored, stored.id);
}

export const actions: Actions = {
	/** Validates and saves the posted draft. */
	save: async ({ locals, params, request }) => {
		const user = requireUser(locals);
		const identity = toGameIdentity(user);

		const current = await getMap(identity, params.mapId);
		if (!current.data) {
			return fail(404, { message: current.error ?? 'Map not found.' });
		}

		const form = await request.formData();
		const candidate = readDraft(form, current.data);
		const expected = Number(form.get('version') ?? current.data.version);

		const result = await saveMap(
			identity,
			params.mapId,
			candidate,
			Number.isFinite(expected) ? expected : current.data.version
		);

		if (!result.data) {
			return fail(result.status || 502, { message: result.error ?? 'The map could not be saved.' });
		}

		if (!result.data.success) {
			return fail(result.data.status === 'conflict' ? 409 : 400, {
				message: result.data.message ?? 'The graph was refused.',
				issues: result.data.issues
			});
		}

		return { saved: true, version: result.data.version, issues: result.data.issues };
	},

	/** Runs the world validator over the posted draft without writing. */
	validate: async ({ locals, params, request }) => {
		const user = requireUser(locals);
		const identity = toGameIdentity(user);

		const current = await getMap(identity, params.mapId);
		if (!current.data) {
			return fail(404, { message: current.error ?? 'Map not found.' });
		}

		const form = await request.formData();
		const result = await validateMap(identity, params.mapId, readDraft(form, current.data));

		if (!result.data) {
			return fail(result.status || 502, {
				message: result.error ?? 'The graph could not be validated.'
			});
		}

		return { validated: true, isValid: result.data.isValid, issues: result.data.issues };
	}
};
