import type { PageServerLoad } from './$types';
import { getServerHealth, listAnnouncements } from '$lib/server/editorial-api';

export const load: PageServerLoad = async () => {
	const [{ page, error }, online] = await Promise.all([listAnnouncements(), getServerHealth()]);

	return { announcements: page.items, error, online };
};
