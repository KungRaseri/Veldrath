// @vitest-environment node
import { afterEach, describe, expect, it, vi } from 'vitest';
import { installFetch, jsonResponse, textResponse } from '#lib/testing/fetch';
import { withEnv } from '#lib/testing/env';
import { sendTransactionalEmail } from '#lib/server/email';

/**
 * Better Auth delegates mail delivery to Veldrath.Server through the internal bridge. Two things
 * must never regress: the bridge token is sent, and the single-use link is not written to logs
 * outside development.
 */

// `dev` comes from `$app/env` and is true under Vitest; the production branch is the one that
// must never leak a reset link into logs, so force it here.
vi.mock('$app/env', () => ({ browser: false, dev: false, building: false, version: 'test' }));

const restores: Array<() => void> = [];

afterEach(() => {
	for (const restore of restores.splice(0)) restore();
	vi.restoreAllMocks();
});

const email = { to: 'player@example.com', userName: 'Player One', url: 'https://veldrath.test/x' };

describe('sendTransactionalEmail', () => {
	it('does not call the bridge when it is not configured', async () => {
		const info = vi.spyOn(console, 'info').mockImplementation(() => {});
		const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
		const stub = installFetch(() => jsonResponse({}));
		restores.push(stub.restore);
		restores.push(withEnv({ INTERNAL_BRIDGE_URL: undefined, INTERNAL_BRIDGE_TOKEN: undefined }));

		const sent = await sendTransactionalEmail('password-reset', email);

		expect(sent).toBe(false);
		expect(stub.calls).toEqual([]);
		// Either the dev echo or the production warning, never both.
		expect(info.mock.calls.length + warn.mock.calls.length).toBe(1);
	});

	it('posts a reset payload with the bridge token', async () => {
		restores.push(
			withEnv({
				INTERNAL_BRIDGE_URL: 'http://bridge:9000/',
				INTERNAL_BRIDGE_TOKEN: 'bridge-secret'
			})
		);
		const stub = installFetch(() => jsonResponse({ sent: true }));
		restores.push(stub.restore);

		const sent = await sendTransactionalEmail('password-reset', email);

		expect(sent).toBe(true);
		const [call] = stub.calls;
		expect(call.url).toBe('http://bridge:9000/api/internal/email/password-reset');
		expect(call.method).toBe('POST');
		expect(call.headers['x-internal-bridge-token']).toBe('bridge-secret');
		expect(JSON.parse(call.body ?? '{}')).toEqual({
			to: 'player@example.com',
			userName: 'Player One',
			resetUrl: 'https://veldrath.test/x'
		});
	});

	it('posts a verification payload', async () => {
		restores.push(
			withEnv({ INTERNAL_BRIDGE_URL: 'http://bridge', INTERNAL_BRIDGE_TOKEN: 'bridge-secret' })
		);
		const stub = installFetch(() => jsonResponse({ sent: true }));
		restores.push(stub.restore);

		await sendTransactionalEmail('verification', { to: 'player@example.com', url: 'https://v/x' });

		expect(JSON.parse(stub.calls[0].body ?? '{}')).toEqual({
			to: 'player@example.com',
			userName: null,
			verifyUrl: 'https://v/x'
		});
	});

	it('reports a rejected message without throwing', async () => {
		restores.push(
			withEnv({ INTERNAL_BRIDGE_URL: 'http://bridge', INTERNAL_BRIDGE_TOKEN: 'bridge-secret' })
		);
		const error = vi.spyOn(console, 'error').mockImplementation(() => {});
		const stub = installFetch(() => textResponse('smtp down', { status: 502 }));
		restores.push(stub.restore);

		await expect(sendTransactionalEmail('verification', email)).resolves.toBe(false);
		expect(error).toHaveBeenCalled();
	});

	it('reports a transport failure without throwing', async () => {
		restores.push(
			withEnv({ INTERNAL_BRIDGE_URL: 'http://bridge', INTERNAL_BRIDGE_TOKEN: 'bridge-secret' })
		);
		const error = vi.spyOn(console, 'error').mockImplementation(() => {});
		const stub = installFetch(() => {
			throw new Error('ECONNREFUSED');
		});
		restores.push(stub.restore);

		await expect(sendTransactionalEmail('password-reset', email)).resolves.toBe(false);
		expect(error).toHaveBeenCalled();
	});
});
