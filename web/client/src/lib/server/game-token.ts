import { createHash } from 'node:crypto';
import { SignJWT } from 'jose';
import { env } from '#lib/server/env';

/** Short-lived tokens are minted on demand and kept in browser memory. */
export const GAME_TOKEN_TTL_SECONDS = 600;

let loggedConfiguration = false;

/** Short, non-reversible fingerprint used to compare the signing key across containers. */
function keyFingerprint(key: string): string {
	return createHash('sha256').update(key, 'utf8').digest('hex').slice(0, 12);
}

/**
 * Mint a Veldrath.Server-compatible JWT.
 *
 * The token intentionally contains only identity claims. Roles and permissions
 * are enriched server-side by Veldrath.Server's claims transformation so the
 * existing RBAC tables remain the source of truth. Email claims are included so
 * the server can just-in-time provision a matching Identity account.
 */
export async function mintGameToken(
	userId: string,
	username: string,
	email?: string,
	emailVerified = false
): Promise<string> {
	if (!env.JWT_KEY) {
		throw new Error('JWT_KEY is not configured.');
	}
	if (!env.JWT_ISSUER) {
		throw new Error('JWT_ISSUER is not configured.');
	}
	if (!env.JWT_AUDIENCE) {
		throw new Error('JWT_AUDIENCE is not configured.');
	}

	// Veldrath.Server logs the same fingerprint at startup. If the two differ the server
	// rejects every minted token with 401, so surface it once in the web container logs.
	if (!loggedConfiguration) {
		loggedConfiguration = true;
		console.info(
			`[game-token] issuer=${env.JWT_ISSUER} audience=${env.JWT_AUDIENCE} keyFingerprint=${keyFingerprint(env.JWT_KEY)}`
		);
	}

	const key = new TextEncoder().encode(env.JWT_KEY);

	const claims: Record<string, unknown> = { unique_name: username, email_verified: emailVerified };
	if (email) {
		claims.email = email;
	}

	return new SignJWT(claims)
		.setProtectedHeader({ alg: 'HS256', typ: 'JWT' })
		.setSubject(userId)
		.setIssuer(env.JWT_ISSUER)
		.setAudience(env.JWT_AUDIENCE)
		.setIssuedAt()
		.setJti(crypto.randomUUID())
		.setExpirationTime(`${GAME_TOKEN_TTL_SECONDS}s`)
		.sign(key);
}
