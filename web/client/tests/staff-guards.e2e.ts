import { expect, test } from '@playwright/test';

const staffRoutes = ['/submissions', '/content', '/editorial', '/admin', '/mod'] as const;

for (const route of staffRoutes) {
	test(`${route} requires authentication`, async ({ page }) => {
		await page.goto(route);
		await expect(page).toHaveURL(/\/login/);
		await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
	});
}
