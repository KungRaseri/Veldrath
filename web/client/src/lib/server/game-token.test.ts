// @vitest-environment node
import { afterEach, describe, expect, it, vi } from 'vitest';
import { jwtVerify } from 'jose';
import { withEnv } from '#lib/testing/env';
import { GAME_TOKEN_TTL_SECONDS, mintGameToken } from '#lib/server/game-token';

/**
 * The minted JWT is the only credential Veldrath.Server accepts from the web app. A wrong claim,
 * issuer, audience, or key means every API call and the SignalR negotiate fail with an opaque 401,
 * so the shape is pinned here rather than discovered in production.
 */

const JWT_KEY = 'test-jwt-signing-key-change-me-0123456789abcdef';
const ISSUER = 'Veldrath.Server';
const AUDIENCE = 'Veldrath.Client';

const restores: Array<() => void> = [];

const configured = () => withEnv({ JWT_KEY, JWT_ISSUER: ISSUER, JWT_AUDIENCE: AUDIENCE });

afterEach(() => {
	for (const restore of restores.splice(0)) restore();
	vi.restoreAllMocks();
});

describe('mintGameToken', () => {
	it('logs the signing configuration once, so key mismatches are comparable across containers', async () => {
		const info = vi.spyOn(console, 'info').mockImplementation(() => {});
		restores.push(configured());

		await mintGameToken('user-1', 'tester');
		const afterFirst = info.mock.calls.length;
		await mintGameToken('user-2', 'tester-2');

		expect(afterFirst).toBe(1);
		expect(info.mock.calls.length).toBe(1);
		expect(info.mock.calls[0][0]).toContain(`issuer=${ISSUER}`);
		expect(info.mock.calls[0][0]).toContain(`audience=${AUDIENCE}`);
		expect(info.mock.calls[0][0]).toContain('keyFingerprint=');
	});

	it('mints a verifiable token with identity claims only', async () => {
		restores.push(configured());

		const token = await mintGameToken('user-1', 'tester', 'tester@example.com', true);
		const { payload, protectedHeader } = await jwtVerify(token, new TextEncoder().encode(JWT_KEY), {
			issuer: ISSUER,
			audience: AUDIENCE
		});

		expect(protectedHeader.alg).toBe('HS256');
		expect(payload.sub).toBe('user-1');
		expect(payload.unique_name).toBe('tester');
		expect(payload.email).toBe('tester@example.com');
		expect(payload.email_verified).toBe(true);
		expect(payload.exp! - payload.iat!).toBe(GAME_TOKEN_TTL_SECONDS);
		// Roles stay server-side: the claims transformation owns them.
		expect(payload.role).toBeUndefined();
		expect(payload.permission).toBeUndefined();
	});

	it('omits the email claim when no address is supplied', async () => {
		restores.push(configured());

		const token = await mintGameToken('user-1', 'tester');
		const { payload } = await jwtVerify(token, new TextEncoder().encode(JWT_KEY));

		expect(payload.email).toBeUndefined();
		expect(payload.email_verified).toBe(false);
	});

	it.each([
		['JWT_KEY', { JWT_KEY: undefined }],
		['JWT_ISSUER', { JWT_ISSUER: undefined }],
		['JWT_AUDIENCE', { JWT_AUDIENCE: undefined }]
	])('refuses to mint without %s', async (name, override) => {
		restores.push(withEnv({ JWT_KEY, JWT_ISSUER: ISSUER, JWT_AUDIENCE: AUDIENCE, ...override }));

		await expect(mintGameToken('user-1', 'tester')).rejects.toThrow(`${name} is not configured.`);
	});
});
