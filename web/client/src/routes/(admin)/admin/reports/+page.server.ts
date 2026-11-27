import { fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import {
	Permissions,
	requireActionPermission,
	requirePermission,
	toGameIdentity
} from '#lib/server/access';
import { listReports, resolveReport } from '#lib/server/admin-api';
import { stringField } from '#lib/server/form';

function parseResolved(value: string | null): boolean | null {
	if (value === 'true') return true;
	if (value === 'false') return false;
	return null;
}

export const load: PageServerLoad = async ({ parent, locals, url }) => {
	// Await the group layout first: it admits both halves of the staff split, and this page then owns
	// the permission its own API call needs. Without this the page load races the layout and calls
	// the API for a request that is already being refused.
	const { access } = await parent();
	requirePermission(
		access,
		Permissions.ViewPlayers,
		'Reports require the players.view permission.'
	);

	const resolved = parseResolved(url.searchParams.get('resolved'));
	const page = Math.max(1, Number(url.searchParams.get('page') ?? 1));
	const result = await listReports(toGameIdentity(locals.user!), resolved, page, 50);

	return {
		reports: result.data?.items ?? [],
		total: result.data?.total ?? 0,
		page: result.data?.page ?? page,
		pageSize: result.data?.pageSize ?? 50,
		resolved: url.searchParams.get('resolved') ?? '',
		error: result.error
	};
};

export const actions: Actions = {
	resolve: async ({ request, locals }) => {
		await requireActionPermission(
			locals,
			Permissions.ViewPlayers,
			'Resolving a report requires the players.view permission.'
		);

		const id = stringField(await request.formData(), 'id');
		const result = await resolveReport(toGameIdentity(locals.user!), id);
		if (result.error) return fail(400, { error: result.error });
		redirect(303, '/admin/reports');
	}
};
