import type { PageServerLoad } from './$types';
import { Permissions, requirePermission, toGameIdentity } from '#lib/server/access';
import { runContentAudit } from '#lib/server/content-api';

/**
 * The content audit, as a page.
 *
 * The same rules the server runs at startup and CI runs as a guard were only ever visible in a log. This is a
 * spot check rather than something to poll: the audit walks the whole catalog.
 */
export const load: PageServerLoad = async ({ locals, parent }) => {
	const { access } = await parent();

	requirePermission(
		access,
		Permissions.ViewContent,
		'Foundry needs the content permission. Ask a Curator for access.'
	);

	const result = await runContentAudit(toGameIdentity(locals.user!));
	const issues = result.data ?? [];

	return {
		issues,
		errors: issues.filter((issue) => issue.severity === 'Error').length,
		warnings: issues.filter((issue) => issue.severity === 'Warning').length,
		error: result.error
	};
};
