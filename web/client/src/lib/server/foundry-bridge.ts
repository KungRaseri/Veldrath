import { eq } from 'drizzle-orm';
import { env } from '#lib/server/env';
import { account } from '@veldrath/shared/db';
import { getAuthDb } from './db';

/** An OAuth login linked to a Better Auth account. */
export interface FoundryExternalLogin {
	provider: string;
	providerKey: string;
	displayName?: string;
}

/** Account data mirrored into ASP.NET Core Identity for the desktop client's native sign-in. */
export interface FoundryAccountSyncPayload {
	accountId: string;
	username?: string | null;
	email?: string | null;
	emailVerified?: boolean;
	displayName?: string | null;
	password?: string;
	externalLogins?: FoundryExternalLogin[];
}

const SYNC_TIMEOUT_MS = 5000;

/**
 * Mirrors a Better Auth identity into the .NET Identity tables through the temporary
 * internal account bridge. This keeps the desktop client's native Identity login working
 * while Better Auth owns web credentials.
 *
 * Failures are logged and reported to the caller but must never block web authentication.
 */
export async function syncAccountToGameServer(
	payload: FoundryAccountSyncPayload
): Promise<boolean> {
	const baseUrl = env.INTERNAL_BRIDGE_URL ?? env.PUBLIC_API_URL;
	const token = env.INTERNAL_BRIDGE_TOKEN;

	if (!baseUrl || !token) {
		console.warn(
			'[foundry-bridge] skipped: INTERNAL_BRIDGE_URL and INTERNAL_BRIDGE_TOKEN must be set ' +
				'so web accounts are mirrored into ASP.NET Identity (Coolify: ' +
				'INTERNAL_BRIDGE_URL=http://veldrath-server:8080).'
		);
		return false;
	}

	try {
		const response = await fetch(`${baseUrl.replace(/\/+$/, '')}/api/internal/accounts/sync`, {
			method: 'POST',
			headers: {
				'content-type': 'application/json',
				'x-internal-bridge-token': token
			},
			body: JSON.stringify({
				accountId: payload.accountId,
				username: payload.username ?? null,
				email: payload.email ?? null,
				emailVerified: payload.emailVerified ?? false,
				displayName: payload.displayName ?? null,
				password: payload.password ?? null,
				externalLogins: payload.externalLogins ?? []
			}),
			signal: AbortSignal.timeout(SYNC_TIMEOUT_MS)
		});

		if (!response.ok) {
			console.warn(
				`[foundry-bridge] account sync failed with HTTP ${response.status} for ${payload.accountId}.`
			);
			return false;
		}

		return true;
	} catch (error) {
		console.warn('[foundry-bridge] account sync request failed.', error);
		return false;
	}
}

/**
 * Loads the OAuth providers linked to a Better Auth user so the .NET server can create
 * matching `AspNetUserLogins` rows. The email/password provider (`credential`) is skipped.
 */
export async function loadExternalLogins(userId: string): Promise<FoundryExternalLogin[]> {
	const rows = await getAuthDb()
		.select({ providerId: account.providerId, accountId: account.accountId })
		.from(account)
		.where(eq(account.userId, userId));

	return rows
		.filter((row) => row.providerId !== 'credential')
		.map((row) => ({
			provider: row.providerId,
			providerKey: row.accountId,
			displayName: row.providerId
		}));
}
