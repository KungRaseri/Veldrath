import { drizzle, type PostgresJsDatabase } from 'drizzle-orm/postgres-js';
import postgres from 'postgres';
import { env } from '#lib/server/env';
import * as schema from '@veldrath/shared/db';

let client: ReturnType<typeof postgres> | undefined;
let db: PostgresJsDatabase<typeof schema> | undefined;

/**
 * Lazy Postgres connection for Better Auth tables only.
 *
 * The dedicated database role should have `search_path` set to the auth schema,
 * for example: ALTER ROLE veldrath_auth SET search_path = auth, public.
 * Keeping construction lazy ensures `vite build` never needs a database.
 */
export function getAuthDb(): PostgresJsDatabase<typeof schema> {
	if (!env.DATABASE_URL) {
		throw new Error('DATABASE_URL is not configured for the SvelteKit auth database.');
	}

	if (!client) {
		client = postgres(env.DATABASE_URL, { max: 10 });
	}

	if (!db) {
		db = drizzle(client, { schema });
	}

	return db;
}
