import { fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { toGameIdentity } from '#lib/server/access';
import { listModReports, resolveModReport } from '#lib/server/admin-api';
import { stringField } from '#lib/server/form';

function parseResolved(value: string | null): boolean | null {
	if (value === 'true') return true;
	if (value === 'false') return false;
	return null;
}

export const load: PageServerLoad = async ({ locals, url }) => {
	const resolved = parseResolved(url.searchParams.get('resolved'));
	const page = Math.max(1, Number(url.searchParams.get('page') ?? 1));
	const result = await listModReports(toGameIdentity(locals.user!), resolved, page, 50);

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
		const id = stringField(await request.formData(), 'id');
		const result = await resolveModReport(toGameIdentity(locals.user!), id);
		if (result.error) return fail(400, { error: result.error });
		redirect(303, '/mod/reports');
	}
};
