import { betterAuth } from 'better-auth';
import { drizzleAdapter } from 'better-auth/adapters/drizzle';
import { hashPassword, verifyPassword } from 'better-auth/crypto';
import { username } from 'better-auth/plugins';
import { sveltekitCookies } from 'better-auth/svelte-kit';
import { getRequestEvent } from '$app/server';
import { env } from '#lib/server/env';
import { account, session, user, verification } from '@veldrath/shared/db';
import { verifyIdentityPassword } from './auth-password';
import { getAuthDb } from './db';
import { sendTransactionalEmail } from './email';

function buildSocialProviders() {
	return {
		...(env.DISCORD_CLIENT_ID && env.DISCORD_CLIENT_SECRET
			? {
					discord: {
						clientId: env.DISCORD_CLIENT_ID,
						clientSecret: env.DISCORD_CLIENT_SECRET
					}
				}
			: {}),
		...(env.GOOGLE_CLIENT_ID && env.GOOGLE_CLIENT_SECRET
			? {
					google: {
						clientId: env.GOOGLE_CLIENT_ID,
						clientSecret: env.GOOGLE_CLIENT_SECRET
					}
				}
			: {}),
		...(env.MICROSOFT_CLIENT_ID && env.MICROSOFT_CLIENT_SECRET
			? {
					microsoft: {
						clientId: env.MICROSOFT_CLIENT_ID,
						clientSecret: env.MICROSOFT_CLIENT_SECRET,
						...(env.MICROSOFT_TENANT_ID ? { tenantId: env.MICROSOFT_TENANT_ID } : {})
					}
				}
			: {})
	};
}

/**
 * Builds a browser-facing link from a Better Auth token. The token is a self-contained
 * JWT, so the SvelteKit pages can redeem it directly instead of routing the user
 * through the Better Auth API endpoint.
 */
function buildWebLink(path: string, token: string, betterAuthUrl: string): string {
	let origin: string;
	try {
		origin = new URL(betterAuthUrl).origin;
	} catch {
		origin = (env.BETTER_AUTH_URL || env.ORIGIN || '').replace(/\/+$/, '');
	}

	if (!origin) {
		return betterAuthUrl;
	}

	const link = new URL(path, origin);
	link.searchParams.set('token', token);
	return link.toString();
}

function createAuth() {
	if (!env.BETTER_AUTH_SECRET) {
		throw new Error('BETTER_AUTH_SECRET is not configured.');
	}

	return betterAuth({
		appName: 'Veldrath',
		baseURL: env.BETTER_AUTH_URL || env.ORIGIN || undefined,
		secret: env.BETTER_AUTH_SECRET,
		database: drizzleAdapter(getAuthDb(), {
			provider: 'pg',
			schema: { user, session, account, verification }
		}),
		emailAndPassword: {
			enabled: true,
			// Verification emails are sent on sign-up, but sign-in is not blocked so
			// accounts migrated from Identity are not locked out before they confirm.
			requireEmailVerification: false,
			sendResetPassword: async ({ user, url, token }) => {
				await sendTransactionalEmail('password-reset', {
					to: user.email,
					userName: user.name,
					url: buildWebLink('/reset-password', token, url)
				});
			},
			password: {
				hash: hashPassword,
				// Accounts migrated from AspNetUsers keep their Identity PBKDF2 hash until
				// the password is next changed, so existing players are not forced to reset.
				verify: async ({ hash, password }) => {
					if (hash.includes(':')) {
						return verifyPassword({ hash, password });
					}
					return verifyIdentityPassword(hash, password);
				}
			}
		},
		emailVerification: {
			// Send a confirmation link when an email/password account is created.
			sendOnSignUp: true,
			sendVerificationEmail: async ({ user, url, token }) => {
				await sendTransactionalEmail('verification', {
					to: user.email,
					userName: user.name,
					url: buildWebLink('/verify-email', token, url)
				});
			}
		},
		user: {
			// Changes only apply after the new address confirms the link sent by
			// emailVerification.sendVerificationEmail.
			changeEmail: {
				enabled: true
			}
		},
		// Better Auth buckets its limits per client address, and falls back to one bucket shared by
		// every caller of a path when it cannot resolve one. That fallback is the safe behaviour, but
		// it also means a test run of the workflow specs — dozens of accounts registered in a minute
		// from a single machine — starts answering "Too many requests" and the whole suite fails on
		// sign-up. Only the e2e stack sets this flag; the container image never does.
		rateLimit: {
			enabled: env.AUTH_RATE_LIMIT_DISABLED !== 'true'
		},
		socialProviders: buildSocialProviders(),
		advanced: {
			database: {
				// Account IDs are GUIDs so they can be used directly as the
				// Veldrath.Server account identifier.
				generateId: () => crypto.randomUUID()
			},
			ipAddress: {
				// adapter-node sits behind Coolify/Traefik, so the client address only arrives in the
				// forwarded header. Without this, Better Auth cannot bucket per player and a single
				// noisy client would share a bucket with everyone else on the same path.
				ipAddressHeaders: ['x-forwarded-for']
			}
		},
		plugins: [
			username(),
			// Must remain the last plugin.
			sveltekitCookies(getRequestEvent)
		],
		trustedOrigins: [env.ORIGIN, env.BETTER_AUTH_URL].filter((value): value is string =>
			Boolean(value)
		)
	});
}

type Auth = ReturnType<typeof createAuth>;

let authInstance: Auth | undefined;

/**
 * Lazy Better Auth singleton.
 *
 * Better Auth owns web credentials, sessions, OAuth, and email flows. The
 * database connection is deliberately created on first use so the SvelteKit
 * build remains database-free.
 */
export function getAuth(): Auth {
	if (!authInstance) {
		authInstance = createAuth();
	}

	return authInstance;
}
