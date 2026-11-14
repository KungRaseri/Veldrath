import { error, fail } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { requireUser, toGameIdentity } from '#lib/server/access';
import { getMap, saveMap, validateMap } from '#lib/server/maps-api';
import { applyNodeEdits, nodeRows, readNodeEdit, type NodeEdit } from '#lib/foundry/map-editor';

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

export const actions: Actions = {
	/**
	 * Validates and saves the edited graph.
	 *
	 * The version travels with the form, so a second author who started from the same graph gets a conflict
	 * instead of silently overwriting this one.
	 */
	save: async ({ locals, params, request }) => {
		const user = requireUser(locals);
		const identity = toGameIdentity(user);

		const current = await getMap(identity, params.mapId);
		if (!current.data) {
			return fail(404, { message: current.error ?? 'Map not found.' });
		}

		const form = await request.formData();
		const edits: Record<string, NodeEdit> = {};
		for (const node of current.data.nodes) {
			edits[node.id] = readNodeEdit(node, form);
		}

		const candidate = applyNodeEdits(current.data, edits);
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

	/**
	 * Runs the world validator without writing, so an author can iterate on a graph.
	 *
	 * It reads the candidate the same way the save does — the stored graph plus the form's node edits — so
	 * what the author sees here is exactly what a save would refuse on.
	 */
	validate: async ({ locals, params, request }) => {
		const user = requireUser(locals);
		const identity = toGameIdentity(user);

		const current = await getMap(identity, params.mapId);
		if (!current.data) {
			return fail(404, { message: current.error ?? 'Map not found.' });
		}

		const form = await request.formData();
		const edits: Record<string, NodeEdit> = {};
		for (const node of current.data.nodes) {
			edits[node.id] = readNodeEdit(node, form);
		}

		const result = await validateMap(identity, params.mapId, applyNodeEdits(current.data, edits));
		if (!result.data) {
			return fail(result.status || 502, {
				message: result.error ?? 'The graph could not be validated.'
			});
		}

		return { validated: true, isValid: result.data.isValid, issues: result.data.issues };
	}
};
