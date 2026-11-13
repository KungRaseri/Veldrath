import { error, fail } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import {
	hasPermission,
	loadAccountAccess,
	Permissions,
	requireUser,
	toGameIdentity
} from '#lib/server/access';
import { getMap, saveMap } from '#lib/server/maps-api';
import { applyNodeEdits, nodeRows, readNodeEdit, type NodeEdit } from '#lib/foundry/map-editor';

/** Loads one graph, its node rows, and whether this account may save it. */
export const load: PageServerLoad = async ({ locals, params }) => {
	const user = requireUser(locals);
	const access = await loadAccountAccess(user);
	const canEdit = hasPermission(access, Permissions.ManageContent);

	const result = await getMap(toGameIdentity(user), params.mapId);
	if (!result.data) {
		error(result.status === 404 ? 404 : result.status || 502, result.error ?? 'Map not found.');
	}

	return { canEdit, graph: result.data, rows: nodeRows(result.data) };
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
	}
};
