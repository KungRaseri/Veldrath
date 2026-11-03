import { describe, expect, it } from 'vitest';
import { authClient } from '#lib/auth-client';

/**
 * `authClient` is configured once at module scope. A missing plugin silently removes the
 * `username` field from every sign-up form, which is the kind of regression that only shows up when
 * a player tries to register.
 */
describe('authClient', () => {
	it('exposes the email, sign-out, and session APIs the pages call', () => {
		expect(typeof authClient.signIn.email).toBe('function');
		expect(typeof authClient.signUp.email).toBe('function');
		expect(typeof authClient.signOut).toBe('function');
		expect(typeof authClient.useSession).toBe('function');
	});

	it('includes the username plugin so sign-in and sign-up accept a username', () => {
		// `signIn.username` only exists when the plugin is registered. The plugin's availability
		// helper is deliberately not touched: reading it issues a network request.
		expect(typeof authClient.signIn.username).toBe('function');
	});
});
