import { eq } from 'drizzle-orm';
import { account } from '@veldrath/shared/db';
import { getAuthDb } from '$lib/server/db';

/** A linked authentication provider for an account. */
export interface LinkedProvider {
	providerId: string;
	createdAt: string;
}

/**
 * Lists the Better Auth provider accounts linked to a user.
 *
 * The `@veldrath/shared/db` import boundary requires this query to live under
 * `src/lib/server/**` rather than directly inside a route module.
 */
export async function listLinkedProviders(userId: string): Promise<LinkedProvider[]> {
	const providers = await getAuthDb()
		.select({ providerId: account.providerId, createdAt: account.createdAt })
		.from(account)
		.where(eq(account.userId, userId));

	return providers.map((provider) => ({
		providerId: provider.providerId,
		createdAt: provider.createdAt.toISOString()
	}));
}
