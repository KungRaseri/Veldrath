import { describe, expect, it } from 'vitest';
import { gameResultResponse } from './game-response';

describe('gameResultResponse', () => {
	it('forwards successful payloads', async () => {
		const response = gameResultResponse({ status: 200, data: { ok: true }, error: null });

		expect(response.status).toBe(200);
		await expect(response.json()).resolves.toEqual({ ok: true });
	});

	it('preserves client-error status and message', async () => {
		const response = gameResultResponse({ status: 409, data: null, error: 'Name taken' });

		expect(response.status).toBe(409);
		await expect(response.json()).resolves.toEqual({ error: 'Name taken' });
	});

	it('maps an unreachable upstream to 502', async () => {
		const response = gameResultResponse({ status: 0, data: null, error: 'Server unavailable' });

		expect(response.status).toBe(502);
		await expect(response.json()).resolves.toEqual({ error: 'Server unavailable' });
	});

	it('returns a bodyless 204 for successful deletes', () => {
		const response = gameResultResponse({ status: 204, data: null, error: null });

		expect(response.status).toBe(204);
		expect(response.body).toBeNull();
	});
});
