import { SignJWT } from 'jose';
import { env } from '$env/dynamic/private';

/** Short-lived tokens are minted on demand and kept in browser memory. */
export const GAME_TOKEN_TTL_SECONDS = 600;

/**
 * Mint a Veldrath.Server-compatible JWT.
 *
 * The token intentionally contains only identity claims. Roles and permissions
 * are enriched server-side by Veldrath.Server's claims transformation so the
 * existing RBAC tables remain the source of truth.
 */
export async function mintGameToken(userId: string, username: string): Promise<string> {
	if (!env.JWT_KEY) {
		throw new Error('JWT_KEY is not configured.');
	}
	if (!env.JWT_ISSUER) {
		throw new Error('JWT_ISSUER is not configured.');
	}
	if (!env.JWT_AUDIENCE) {
		throw new Error('JWT_AUDIENCE is not configured.');
	}

	const key = new TextEncoder().encode(env.JWT_KEY);

	return new SignJWT({ unique_name: username })
		.setProtectedHeader({ alg: 'HS256', typ: 'JWT' })
		.setSubject(userId)
		.setIssuer(env.JWT_ISSUER)
		.setAudience(env.JWT_AUDIENCE)
		.setIssuedAt()
		.setJti(crypto.randomUUID())
		.setExpirationTime(`${GAME_TOKEN_TTL_SECONDS}s`)
		.sign(key);
}
