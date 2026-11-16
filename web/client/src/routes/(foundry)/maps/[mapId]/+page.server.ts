import { error, fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { requireUser, toGameIdentity } from '#lib/server/access';
import {
	deactivateMap,
	deleteMap,
	getMap,
	getMapTargets,
	reviewMap,
	saveMap,
	submitMap,
	validateMap
} from '#lib/server/maps-api';
import {
	nodeRows,
	parseDraftGraph,
	type MapGraphDto,
	type MapLifecycleResult
} from '#lib/foundry/map-editor';

/**
 * Loads one graph, its node rows, and whether this account may save it.
 *
 * Reading needs a signed-in account; saving requires `content.manage` and a decision requires
 * `content.review`. The page is not the guard for the write — the API enforces it — but `canEdit` decides
 * whether the controls render at all.
 */
export const load: PageServerLoad = async ({ locals, params, parent }) => {
	const user = requireUser(locals);
	const { canManageContent, canReview } = await parent();

	const identity = toGameIdentity(user);
	const [result, targets] = await Promise.all([
		getMap(identity, params.mapId),
		getMapTargets(identity)
	]);

	if (!result.data) {
		error(result.status === 404 ? 404 : result.status || 502, result.error ?? 'Map not found.');
	}

	if (!targets.data) {
		error(targets.status || 502, targets.error ?? 'Could not load the reference catalog.');
	}

	return {
		canEdit: canManageContent,
		canReview,
		graph: result.data,
		rows: nodeRows(result.data),
		targets: targets.data
	};
};

/**
 * Turns a lifecycle outcome into the banner the page shows.
 *
 * A refusal keeps the message the server worded, because it names the reason — "other maps still exit here"
 * is actionable in a way a generic failure is not.
 */
function lifecycle(
	result: { status: number; data: MapLifecycleResult | null; error: string | null },
	fallback: string
) {
	if (!result.data) {
		return fail(result.status || 502, { message: result.error ?? fallback });
	}

	if (!result.data.success) {
		return fail(result.data.status === 'conflict' ? 409 : 400, {
			message: result.data.message ?? fallback,
			issues: result.data.issues
		});
	}

	return { done: true, message: result.data.message ?? fallback, issues: result.data.issues };
}

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
	},

	/** Puts the draft forward for review. */
	submit: async ({ locals, params }) => {
		const user = requireUser(locals);
		const result = await submitMap(toGameIdentity(user), params.mapId);
		return lifecycle(result, 'Submitted for review.');
	},

	/** A reviewer's decision — the only path that makes a map live. */
	review: async ({ locals, params, request }) => {
		const user = requireUser(locals);
		const form = await request.formData();
		const approved = String(form.get('approved') ?? '') === 'true';
		const notes = form.get('notes') === null ? null : String(form.get('notes'));

		const result = await reviewMap(toGameIdentity(user), params.mapId, { approved, notes });
		return lifecycle(result, approved ? 'Approved; the map is live.' : 'Rejected.');
	},

	/** Retires a live map. */
	deactivate: async ({ locals, params }) => {
		const user = requireUser(locals);
		const result = await deactivateMap(toGameIdentity(user), params.mapId);
		return lifecycle(result, 'Deactivated.');
	},

	/** Removes a draft. A live map is retired instead. */
	delete: async ({ locals, params }) => {
		const user = requireUser(locals);
		const result = await deleteMap(toGameIdentity(user), params.mapId);

		if (result.data?.success) {
			redirect(303, '/maps');
		}

		return lifecycle(result, 'The draft could not be deleted.');
	}
};
