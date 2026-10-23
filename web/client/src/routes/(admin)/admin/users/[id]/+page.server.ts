import { error, fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { Permissions, Roles, toGameIdentity } from '$lib/server/access';
import {
	assignRole,
	banPlayer,
	getUser,
	grantPermission,
	kickPlayer,
	mutePlayer,
	revokePermission,
	revokeRole,
	unbanPlayer,
	unmutePlayer,
	warnPlayer
} from '$lib/server/admin-api';
import { numberField, stringField } from '$lib/server/form';

export const load: PageServerLoad = async ({ locals, params }) => {
	const result = await getUser(toGameIdentity(locals.user!), params.id);
	if (!result.data) {
		error(404, result.error ?? 'Account not found.');
	}

	return {
		player: result.data,
		allRoles: Object.values(Roles),
		allPermissions: Object.values(Permissions)
	};
};

function back(params: { id: string }) {
	redirect(303, `/admin/users/${params.id}`);
}

export const actions: Actions = {
	assignRole: async ({ request, locals, params }) => {
		const role = stringField(await request.formData(), 'role');
		const result = await assignRole(toGameIdentity(locals.user!), params.id, role);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	},

	revokeRole: async ({ request, locals, params }) => {
		const role = stringField(await request.formData(), 'role');
		const result = await revokeRole(toGameIdentity(locals.user!), params.id, role);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	},

	grantPermission: async ({ request, locals, params }) => {
		const permission = stringField(await request.formData(), 'permission');
		const result = await grantPermission(toGameIdentity(locals.user!), params.id, permission);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	},

	revokePermission: async ({ request, locals, params }) => {
		const permission = stringField(await request.formData(), 'permission');
		const result = await revokePermission(toGameIdentity(locals.user!), params.id, permission);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	},

	kick: async ({ request, locals, params }) => {
		const reason = stringField(await request.formData(), 'reason');
		const result = await kickPlayer(toGameIdentity(locals.user!), params.id, reason || null);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	},

	warn: async ({ request, locals, params }) => {
		const reason = stringField(await request.formData(), 'reason');
		if (!reason) return fail(400, { error: 'A reason is required.' });
		const result = await warnPlayer(toGameIdentity(locals.user!), params.id, reason);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	},

	mute: async ({ request, locals, params }) => {
		const form = await request.formData();
		const result = await mutePlayer(
			toGameIdentity(locals.user!),
			params.id,
			stringField(form, 'reason') || null,
			numberField(form, 'durationMinutes')
		);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	},

	unmute: async ({ locals, params }) => {
		const result = await unmutePlayer(toGameIdentity(locals.user!), params.id);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	},

	ban: async ({ request, locals, params }) => {
		const form = await request.formData();
		const reason = stringField(form, 'reason');
		if (!reason) return fail(400, { error: 'A ban reason is required.' });
		const result = await banPlayer(
			toGameIdentity(locals.user!),
			params.id,
			reason,
			numberField(form, 'durationMinutes')
		);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	},

	unban: async ({ locals, params }) => {
		const result = await unbanPlayer(toGameIdentity(locals.user!), params.id);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	}
};
