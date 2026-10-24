import { Permissions, requirePermission, requireUser } from '#lib/server/access';

/** Editorial management requires the `content.manage` permission. */
export const load = async ({ locals, parent }) => {
	requireUser(locals);
	const { access } = await parent();
	requirePermission(
		access,
		Permissions.ManageContent,
		'Editorial management requires content access.'
	);
	return { access };
};
