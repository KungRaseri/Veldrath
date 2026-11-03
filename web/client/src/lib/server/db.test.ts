// @vitest-environment node
import { afterEach, describe, expect, it } from 'vitest';
import { withEnv } from '#lib/testing/env';
import { getAuthDb } from '#lib/server/db';

/**
 * The auth database connection must stay lazy: `vite build` runs without a database, so the only
 * failure allowed at import time is a missing `DATABASE_URL` when a request actually needs it.
 */

const restores: Array<() => void> = [];

afterEach(() => {
	for (const restore of restores.splice(0)) restore();
});

describe('getAuthDb', () => {
	it('names the missing setting instead of connecting to nothing', () => {
		restores.push(withEnv({ DATABASE_URL: undefined }));

		expect(() => getAuthDb()).toThrow(
			'DATABASE_URL is not configured for the SvelteKit auth database.'
		);
	});

	it('constructs one shared drizzle client without connecting', () => {
		restores.push(withEnv({ DATABASE_URL: 'postgres://user:pass@127.0.0.1:5432/test' }));

		const first = getAuthDb();
		const second = getAuthDb();

		expect(first).toBe(second);
		expect(typeof first.select).toBe('function');
	});
});
