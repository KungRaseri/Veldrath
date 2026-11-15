import { expect, test } from '@playwright/test';

const staffRoutes = [
	'/submissions',
	'/content',
	'/maps',
	// The detail route as the route-coverage suite sees it: Playwright cannot know a real map id, and
	// the group layout guard runs before any lookup, so any id proves the route is protected.
	'/maps/zone:fenwick',
	'/editorial',
	'/admin',
	'/mod'
] as const;

for (const route of staffRoutes) {
	test(`${route} requires authentication`, async ({ page }) => {
		await page.goto(route);
		await expect(page).toHaveURL(/\/login/);
		await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
	});
}
