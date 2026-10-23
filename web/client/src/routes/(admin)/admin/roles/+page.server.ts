import type { PageServerLoad } from './$types';
import { Permissions, Roles } from '$lib/server/access';

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

export const load: PageServerLoad = async () => ({
	roles: roleCatalog,
	allPermissions: Object.values(Permissions)
});
