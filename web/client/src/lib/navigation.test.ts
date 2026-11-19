import { describe, expect, it } from 'vitest';
import { localPathFrom } from './navigation';

describe('localPathFrom', () => {
	it('accepts a local path with or without a query', () => {
		expect(localPathFrom('/foundry')).toBe('/foundry');
		expect(localPathFrom('/submissions?scope=all')).toBe('/submissions?scope=all');
	});

	it('rejects a missing, absolute, or protocol-relative destination', () => {
		// `?redirectTo=` comes from the URL, so anything but a local path would make the login page an open
		// redirect — the one thing an auth surface must never be.
		expect(localPathFrom(null)).toBeNull();
		expect(localPathFrom('')).toBeNull();
		expect(localPathFrom('https://elsewhere.example/foundry')).toBeNull();
		expect(localPathFrom('//elsewhere.example')).toBeNull();
	});
});
