import { execFileSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { expect, test } from '@playwright/test';
import { makeAccount, register, stackEnabled, stackSkipReason } from './support/stack';

/**
 * What the pair of apps does when the API is not there.
 *
 * The SvelteKit server renders every page by calling `Veldrath.Server`, so "the API is down" is not an
 * edge case — it is what a deploy, a crash or a restart looks like to a player. Each page already has an
 * error state for it; the point of this spec is that the state is *words*, not a blank page or a spinner
 * that never ends, and that the page recovers once the API is back.
 *
 * It stops the API through the stack script rather than mocking a failure, because a mock cannot prove the
 * real fetch path handles a dead socket. Run the stack suite with one worker: this spec takes the API away
 * from everything else for a few seconds.
 */
// This file lives at web/client/tests/, so three levels up is the repository root.
const repoRoot = fileURLToPath(new URL('../../../', import.meta.url));
const stack = (command: string) =>
	execFileSync(`${repoRoot}scripts/e2e-stack.sh`, [command], { stdio: 'pipe', timeout: 120_000 });

// The pid file is written by the script when it starts the API. Its absence means the API was started some
// other way — CI runs `dotnet run` itself against a service container — and this spec has nothing to stop.
const managedByScript = existsSync(`${repoRoot}.e2e-server.pid`);

test.describe('upstream failure', () => {
	test.skip(!stackEnabled, stackSkipReason);
	test.skip(
		!managedByScript,
		'needs an API started by `scripts/e2e-stack.sh` (this spec stops and restarts it)'
	);
	test.describe.configure({ timeout: 180_000 });

	test('the app says the API is unreachable, then recovers when it returns', async ({ page }) => {
		// Signed in while the API is still up: the account and session have to exist before the outage.
		await register(page, makeAccount('e2eoutage'));

		try {
			stack('api-stop');

			// A Foundry page and a public page, because they fail through different loaders.
			await page.goto('/content');
			await expect(page.getByText('Could not reach the game server.')).toBeVisible({
				timeout: 20_000
			});

			await page.goto('/lore');
			await expect(page.getByText('Could not reach the game server.')).toBeVisible({
				timeout: 20_000
			});

			// The page kept rendering rather than blanking: its own chrome is still there.
			await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
		} finally {
			// Never leave the API down, even when an assertion above failed.
			stack('api-start');
		}

		// Recovery is a reload of the same page — no client-side state to reset.
		await page.goto('/content');
		await expect(page.getByRole('heading', { name: 'Base content' })).toBeVisible({
			timeout: 30_000
		});
		await expect(page.getByText('Could not reach the game server.')).toHaveCount(0);
		await expect(page.getByRole('link', { name: 'Browse' }).first()).toBeVisible({
			timeout: 20_000
		});
	});
});
