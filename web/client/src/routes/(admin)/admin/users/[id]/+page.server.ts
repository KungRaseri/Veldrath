import { error, fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import {
	Permissions,
	Roles,
	hasPermission,
	requireActionPermission,
	toGameIdentity
} from '#lib/server/access';
import {
	assignRole,
	banPlayer,
	getUser,
	getUserAuthority,
	grantPermission,
	kickPlayer,
	mutePlayer,
	revokePermission,
	revokeRole,
	unbanPlayer,
	unmutePlayer,
	warnPlayer,
	type AccountAuthority
} from '#lib/server/admin-api';
import { numberField, stringField } from '#lib/server/form';

/** The two halves of the staff split this page serves. */
const accountAdmin = 'Account administration requires user management permission.';
const roleAdmin = 'Managing roles and permissions requires the admin.roles permission.';

export const load: PageServerLoad = async ({ parent, locals, params }) => {
	// Await the group layout first: it admits either half of the split. The page then decides which
	// half the caller holds, because the two halves read the account from different endpoints:
	// `admin.users` from the account detail (which carries ban and mute state), `admin.roles` from
	// the authority read (which deliberately does not). A caller with neither is refused here, so a
	// refused page still makes no admin API call.
	const { access } = await parent();
	const canManageUsers = hasPermission(access, Permissions.ManageUsers);
	const canManageRoles = hasPermission(access, Permissions.ManageRoles);

	if (!canManageUsers && !canManageRoles) {
		error(403, 'This page requires user or role management permission.');
	}

	const identity = toGameIdentity(locals.user!);
	const catalog = {
		canManageUsers,
		canManageRoles,
		allRoles: Object.values(Roles),
		allPermissions: Object.values(Permissions)
	};

	if (canManageUsers) {
		const result = await getUser(identity, params.id);
		if (!result.data) error(404, result.error ?? 'Account not found.');

		const player = result.data;
		const authority: AccountAuthority = {
			id: player.id,
			username: player.username,
			roles: player.roles,
			userPermissions: player.userPermissions,
			effectivePermissions: player.effectivePermissions
		};

		return { account: player, authority, ...catalog };
	}

	const result = await getUserAuthority(identity, params.id);
	if (!result.data) error(404, result.error ?? 'Account not found.');

	return { account: null, authority: result.data, ...catalog };
};

function back(params: { id: string }) {
	redirect(303, `/admin/users/${params.id}`);
}

export const actions: Actions = {
	assignRole: async ({ request, locals, params }) => {
		await requireActionPermission(locals, Permissions.ManageRoles, roleAdmin);

		const role = stringField(await request.formData(), 'role');
		const result = await assignRole(toGameIdentity(locals.user!), params.id, role);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	},

	revokeRole: async ({ request, locals, params }) => {
		await requireActionPermission(locals, Permissions.ManageRoles, roleAdmin);

		const role = stringField(await request.formData(), 'role');
		const result = await revokeRole(toGameIdentity(locals.user!), params.id, role);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	},

	grantPermission: async ({ request, locals, params }) => {
		await requireActionPermission(locals, Permissions.ManageRoles, roleAdmin);

		const permission = stringField(await request.formData(), 'permission');
		const result = await grantPermission(toGameIdentity(locals.user!), params.id, permission);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	},

	revokePermission: async ({ request, locals, params }) => {
		await requireActionPermission(locals, Permissions.ManageRoles, roleAdmin);

		const permission = stringField(await request.formData(), 'permission');
		const result = await revokePermission(toGameIdentity(locals.user!), params.id, permission);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	},

	kick: async ({ request, locals, params }) => {
		await requireActionPermission(locals, Permissions.ManageUsers, accountAdmin);

		const reason = stringField(await request.formData(), 'reason');
		const result = await kickPlayer(toGameIdentity(locals.user!), params.id, reason || null);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	},

	warn: async ({ request, locals, params }) => {
		await requireActionPermission(locals, Permissions.ManageUsers, accountAdmin);

		const reason = stringField(await request.formData(), 'reason');
		if (!reason) return fail(400, { error: 'A reason is required.' });
		const result = await warnPlayer(toGameIdentity(locals.user!), params.id, reason);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	},

	mute: async ({ request, locals, params }) => {
		await requireActionPermission(locals, Permissions.ManageUsers, accountAdmin);

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
		await requireActionPermission(locals, Permissions.ManageUsers, accountAdmin);

		const result = await unmutePlayer(toGameIdentity(locals.user!), params.id);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	},

	ban: async ({ request, locals, params }) => {
		await requireActionPermission(locals, Permissions.ManageUsers, accountAdmin);

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
		await requireActionPermission(locals, Permissions.ManageUsers, accountAdmin);

		const result = await unbanPlayer(toGameIdentity(locals.user!), params.id);
		if (result.error) return fail(400, { error: result.error });
		back(params);
	}
};
