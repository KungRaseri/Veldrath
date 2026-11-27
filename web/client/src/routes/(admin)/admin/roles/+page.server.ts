import { fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import {
	Permissions,
	Roles,
	hasPermission,
	requireActionPermission,
	toGameIdentity
} from '#lib/server/access';
import { getUserAuthorityByUsername } from '#lib/server/admin-api';
import { stringField } from '#lib/server/form';

const roleCatalog = [
	{
		name: Roles.Admin,
		description: 'Full system access — all permissions granted.',
		permissions: Object.values(Permissions)
	},
	{
		name: Roles.Moderator,
		description: 'Player-facing moderation: ban, kick, warn, mute, and view players.',
		permissions: [
			Permissions.BanPlayers,
			Permissions.KickPlayers,
			Permissions.WarnPlayers,
			Permissions.MutePlayers,
			Permissions.ViewPlayers,
			Permissions.SendAnnouncements
		]
	},
	{
		name: Roles.Curator,
		description: 'Community content curation: review Foundry submissions.',
		permissions: [Permissions.ReviewSubmissions, Permissions.ViewContent]
	},
	{
		name: Roles.GameMaster,
		description: 'In-game powers: teleport, give items, run events, view players.',
		permissions: [
			Permissions.KickPlayers,
			Permissions.WarnPlayers,
			Permissions.MutePlayers,
			Permissions.ViewPlayers,
			Permissions.SendAnnouncements
		]
	},
	{
		name: Roles.Developer,
		description: 'Debug and diagnostic access plus content read access.',
		permissions: [Permissions.ViewContent, Permissions.ViewPlayers]
	},
	{
		name: Roles.ContentEditor,
		description: 'Manage and view canonical game content in the database.',
		permissions: [Permissions.ManageContent, Permissions.ViewContent]
	}
];

export const load: PageServerLoad = async ({ parent }) => {
	const { access } = await parent();

	return {
		roles: roleCatalog,
		allPermissions: Object.values(Permissions),
		// The group admits admin.users as well, and that half can read this catalogue but not open an
		// account from it — the account list belongs to account administration.
		canManageRoles: hasPermission(access, Permissions.ManageRoles),
		canManageUsers: hasPermission(access, Permissions.ManageUsers)
	};
};

export const actions: Actions = {
	/**
	 * Opens one account's role controls by username.
	 *
	 * The role controls live on the account page, and the account list that would normally lead there
	 * needs `admin.users`. A role manager therefore types the account's username; the API resolves it
	 * under `admin.roles` and the browser is sent to the page that owns the forms.
	 */
	open: async ({ request, locals }) => {
		await requireActionPermission(
			locals,
			Permissions.ManageRoles,
			'Managing roles requires the admin.roles permission.'
		);

		const username = stringField(await request.formData(), 'username').trim();
		if (!username) {
			return fail(400, {
				error: 'Enter the username of the account whose roles you want to manage.'
			});
		}

		const result = await getUserAuthorityByUsername(toGameIdentity(locals.user!), username);
		if (!result.data) {
			return fail(404, {
				error: result.error ?? `No account is named "${username}".`
			});
		}

		redirect(303, `/admin/users/${result.data.id}`);
	}
};
