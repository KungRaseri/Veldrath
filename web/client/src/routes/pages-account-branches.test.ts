import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import AccountPage from './account/+page.svelte';

/**
 * Branch-coverage companion for the account page's provider section.
 *
 * The main account suite covers the link failure paths and a provider handshake that returns a
 * URL. Two branches are left: a handshake that succeeds without returning anything (`data` is
 * null, so the redirect must be skipped without an error), and a linked provider the label map
 * does not know about (a provider added on the server before this client learned its name), which
 * must render its raw id rather than `undefined`.
 *
 * `#lib/auth-client` is mocked here as well; the module graph is per test file.
 */
const auth = vi.hoisted(() => ({
	updateUser: vi.fn(),
	changeEmail: vi.fn(),
	sendVerificationEmail: vi.fn(),
	linkSocial: vi.fn(),
	unlinkAccount: vi.fn(),
	revokeSession: vi.fn(),
	revokeOtherSessions: vi.fn(),
	changePassword: vi.fn(),
	signOut: vi.fn()
}));

vi.mock('#lib/auth-client', () => ({ authClient: auth }));

const baseUser = {
	id: 'acct-1',
	name: 'Kaleth',
	username: 'kaleth',
	email: 'kaleth@example.com',
	emailVerified: true,
	createdAt: '2026-01-02T03:04:05.000Z'
};

function accountData(overrides: Record<string, unknown> = {}) {
	return {
		user: baseUser,
		providers: [{ id: 'acct-1', providerId: 'credential' }],
		sessions: [],
		currentSessionId: null,
		linkableProviders: [],
		oauthError: null,
		...overrides
	};
}

beforeEach(() => {
	for (const spy of Object.values(auth)) spy.mockResolvedValue({ data: {}, error: null });
	vi.spyOn(window, 'confirm').mockReturnValue(true);
});

afterEach(() => {
	cleanup();
	vi.restoreAllMocks();
	window.location.hash = '';
});

describe('account provider links', () => {
	it('does not redirect when the handshake returns neither data nor an error', async () => {
		auth.linkSocial.mockResolvedValue({ data: null, error: null });
		window.location.hash = '';

		render(AccountPage, {
			data: accountData({ linkableProviders: [{ id: 'discord', label: 'Discord' }] }) as never
		});

		await fireEvent.click(screen.getByRole('button', { name: /Link Discord/ }));

		await waitFor(() => expect(auth.linkSocial).toHaveBeenCalledTimes(1));
		expect(window.location.hash).toBe('');
		expect(screen.queryByText(/Could not link/)).toBeNull();
	});

	it('renders an unknown provider by its raw id and still offers Unlink', async () => {
		render(AccountPage, {
			data: accountData({
				providers: [
					{ id: 'acct-1', providerId: 'credential' },
					{ id: 'acct-2', providerId: 'github' }
				]
			}) as never
		});

		expect(screen.getByText('Email & password')).toBeTruthy();
		expect(screen.getByText('github')).toBeTruthy();
		expect(screen.getAllByRole('button', { name: /Unlink/ })).toHaveLength(1);

		await fireEvent.click(screen.getByRole('button', { name: /Unlink/ }));
		await waitFor(() => expect(auth.unlinkAccount).toHaveBeenCalledWith({ accountId: 'acct-2' }));
	});
});
