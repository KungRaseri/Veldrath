import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { goto, invalidateAll, resetNavigation } from '#lib/testing/app-navigation';
import AccountPage from './account/+page.svelte';

/**
 * The account page component.
 *
 * Every control on this page is destructive or identity-changing — unlink a provider, revoke a
 * session, change the password, change the email — so each one is asserted twice: the success path
 * (including whether `invalidateAll` was called so the server data catches up) and the failure path
 * with its fallback copy. The load is covered in `auth-pages.test.ts`.
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

const credential = { id: 'acct-1', providerId: 'credential' };

function accountData(overrides: Record<string, unknown> = {}) {
	return {
		user: baseUser,
		providers: [credential],
		sessions: [],
		currentSessionId: null,
		linkableProviders: [],
		oauthError: null,
		...overrides
	};
}

function renderAccount(overrides: Record<string, unknown> = {}) {
	return render(AccountPage, { data: accountData(overrides) as never });
}

/** The page renders profile, email, and password forms in that order. */
function forms(container: HTMLElement): HTMLFormElement[] {
	return Array.from(container.querySelectorAll('form')) as HTMLFormElement[];
}

async function fill(input: Element | null, value: string): Promise<void> {
	if (!input) throw new Error('no input to fill');
	await fireEvent.input(input, { target: { value } });
}

beforeEach(() => {
	for (const spy of Object.values(auth)) spy.mockResolvedValue({ data: {}, error: null });
	resetNavigation();
	vi.spyOn(window, 'confirm').mockReturnValue(true);
});

afterEach(() => {
	for (const spy of Object.values(auth)) spy.mockReset();
	vi.restoreAllMocks();
	window.location.hash = '';
});

describe('account profile section', () => {
	it('shows the identity, the verified chip, and the created date', () => {
		renderAccount();

		expect(screen.getByText('kaleth')).toBeTruthy();
		expect(screen.getByText('kaleth@example.com')).toBeTruthy();
		expect(screen.getByText('Verified')).toBeTruthy();
		expect(screen.getByText('acct-1')).toBeTruthy();
		expect(screen.queryByRole('button', { name: /Resend verification email/ })).toBeNull();
	});

	it('falls back to the display name when there is no username', () => {
		renderAccount({ user: { ...baseUser, username: null } });

		expect(screen.getAllByText('Kaleth').length).toBeGreaterThan(0);
	});

	it('offers a resend button for an unverified address', async () => {
		renderAccount({ user: { ...baseUser, emailVerified: false } });

		expect(screen.getByText('Unverified')).toBeTruthy();
		await fireEvent.click(screen.getByRole('button', { name: /Resend verification email/ }));

		await waitFor(() => expect(auth.sendVerificationEmail).toHaveBeenCalled());
		expect(auth.sendVerificationEmail).toHaveBeenCalledWith({
			email: 'kaleth@example.com',
			callbackURL: `${window.location.origin}/account`
		});
		expect(
			await screen.findByText('A new confirmation link has been sent to your inbox.')
		).toBeTruthy();
	});

	it('reports a resend failure, with a fallback when the error has no message', async () => {
		auth.sendVerificationEmail.mockResolvedValue({ data: null, error: {} });
		const first = renderAccount({ user: { ...baseUser, emailVerified: false } });

		await fireEvent.click(screen.getByRole('button', { name: /Resend verification email/ }));
		expect(await screen.findByText('Could not send the verification email.')).toBeTruthy();
		first.unmount();

		auth.sendVerificationEmail.mockResolvedValue({
			data: null,
			error: { message: 'Daily limit reached' }
		});
		renderAccount({ user: { ...baseUser, emailVerified: false } });

		await fireEvent.click(screen.getByRole('button', { name: /Resend verification email/ }));
		expect(await screen.findByText('Daily limit reached')).toBeTruthy();
	});
});

describe('account profile edits', () => {
	it('refuses an empty display name', async () => {
		const { container } = renderAccount();

		await fill(container.querySelector('input[autocomplete="nickname"]'), '   ');
		await fireEvent.submit(forms(container)[0]);

		expect(screen.getByText('A display name is required.')).toBeTruthy();
		expect(auth.updateUser).not.toHaveBeenCalled();
	});

	it('saves the trimmed name and username, then refreshes the server data', async () => {
		const { container } = renderAccount();

		await fill(container.querySelector('input[autocomplete="nickname"]'), '  Kaleth the Bold  ');
		await fill(container.querySelector('input[autocomplete="username"]'), ' kaleth_bold ');
		await fireEvent.submit(forms(container)[0]);

		await waitFor(() => expect(auth.updateUser).toHaveBeenCalled());
		expect(auth.updateUser).toHaveBeenCalledWith({
			name: 'Kaleth the Bold',
			username: 'kaleth_bold'
		});
		expect(screen.getByText('Profile updated.')).toBeTruthy();
		expect(invalidateAll).toHaveBeenCalled();
	});

	it('reports a profile failure without refreshing', async () => {
		auth.updateUser.mockResolvedValue({ data: null, error: { message: 'Username taken' } });
		const { container } = renderAccount();

		await fireEvent.submit(forms(container)[0]);
		expect(await screen.findByText('Username taken')).toBeTruthy();
		expect(invalidateAll).not.toHaveBeenCalled();

		auth.updateUser.mockResolvedValue({ data: null, error: {} });
		await fireEvent.submit(forms(container)[0]);
		expect(await screen.findByText('Could not update the profile.')).toBeTruthy();
	});
});

describe('account email change', () => {
	it('refuses an empty address', async () => {
		const { container } = renderAccount();

		await fill(container.querySelector('input[type="email"]'), '   ');
		await fireEvent.submit(forms(container)[1]);

		expect(screen.getByText('Enter the new email address.')).toBeTruthy();
		expect(auth.changeEmail).not.toHaveBeenCalled();
	});

	it('asks for confirmation and clears the field', async () => {
		const { container } = renderAccount();
		const input = container.querySelector('input[type="email"]') as HTMLInputElement;

		await fill(input, '  new@example.com ');
		await fireEvent.submit(forms(container)[1]);

		await waitFor(() => expect(auth.changeEmail).toHaveBeenCalled());
		expect(auth.changeEmail).toHaveBeenCalledWith({
			newEmail: 'new@example.com',
			callbackURL: `${window.location.origin}/account`
		});
		expect(
			screen.getByText(
				'Check the new inbox for a confirmation link. Your address changes once you click it.'
			)
		).toBeTruthy();
		expect(input.value).toBe('');
	});

	it('reports a change failure with and without a message', async () => {
		auth.changeEmail.mockResolvedValue({ data: null, error: {} });
		const { container } = renderAccount();

		await fill(container.querySelector('input[type="email"]'), 'new@example.com');
		await fireEvent.submit(forms(container)[1]);
		expect(await screen.findByText('Could not start the email change.')).toBeTruthy();

		auth.changeEmail.mockResolvedValue({ data: null, error: { message: 'Address in use' } });
		await fireEvent.submit(forms(container)[1]);
		expect(await screen.findByText('Address in use')).toBeTruthy();
	});
});

describe('account linked providers', () => {
	it('says so when nothing is linked', () => {
		renderAccount({ providers: [] });

		expect(screen.getByText('No linked providers found.')).toBeTruthy();
	});

	it('labels a provider and offers to unlink it, but never the password', async () => {
		renderAccount({
			providers: [credential, { id: 'acct-2', providerId: 'discord' }]
		});

		expect(screen.getByText('Email & password')).toBeTruthy();
		expect(screen.getByText('Discord')).toBeTruthy();
		expect(screen.getAllByRole('button', { name: /Unlink/ })).toHaveLength(1);

		await fireEvent.click(screen.getByRole('button', { name: /Unlink/ }));

		await waitFor(() => expect(auth.unlinkAccount).toHaveBeenCalled());
		expect(auth.unlinkAccount).toHaveBeenCalledWith({ accountId: 'acct-2' });
		expect(screen.getByText('Discord unlinked.')).toBeTruthy();
		expect(invalidateAll).toHaveBeenCalled();
	});

	it('shows an unknown provider id verbatim', () => {
		renderAccount({ providers: [{ id: 'acct-3', providerId: 'steam' }] });

		expect(screen.getByText('steam')).toBeTruthy();
	});

	it('does nothing when the player cancels the unlink prompt', async () => {
		vi.spyOn(window, 'confirm').mockReturnValue(false);
		renderAccount({ providers: [credential, { id: 'acct-2', providerId: 'discord' }] });

		await fireEvent.click(screen.getByRole('button', { name: /Unlink/ }));

		expect(auth.unlinkAccount).not.toHaveBeenCalled();
	});

	it('reports an unlink failure with and without a message', async () => {
		auth.unlinkAccount.mockResolvedValue({ data: null, error: {} });
		const { container } = renderAccount({
			providers: [credential, { id: 'acct-2', providerId: 'discord' }]
		});

		await fireEvent.click(screen.getByRole('button', { name: /Unlink/ }));
		expect(await screen.findByText('Could not unlink that provider.')).toBeTruthy();

		auth.unlinkAccount.mockResolvedValue({ data: null, error: { message: 'Last method' } });
		await fireEvent.click(container.querySelector('.preset-tonal-error-500') as HTMLElement);
		expect(await screen.findByText('Last method')).toBeTruthy();
	});

	it('only offers to link a provider that is not linked yet', () => {
		renderAccount({
			providers: [credential],
			linkableProviders: [
				{ id: 'discord', label: 'Discord' },
				{ id: 'google', label: 'Google' }
			]
		});

		expect(screen.getByRole('button', { name: /Link Discord/ })).toBeTruthy();
		expect(screen.getByRole('button', { name: /Link Google/ })).toBeTruthy();
	});

	it('hides the link button for an already linked provider', () => {
		renderAccount({
			providers: [credential, { id: 'acct-2', providerId: 'discord' }],
			linkableProviders: [{ id: 'discord', label: 'Discord' }]
		});

		expect(screen.queryByRole('button', { name: /Link Discord/ })).toBeNull();
	});

	it('follows the URL the provider handshake returns', async () => {
		// jsdom only permits same-document navigation, so a fragment stands in for the absolute URL
		// the real provider returns; the assignment under test is the same one.
		auth.linkSocial.mockResolvedValue({ data: { url: '#linked' }, error: null });
		renderAccount({ linkableProviders: [{ id: 'discord', label: 'Discord' }] });

		await fireEvent.click(screen.getByRole('button', { name: /Link Discord/ }));

		await waitFor(() => expect(auth.linkSocial).toHaveBeenCalled());
		expect(auth.linkSocial).toHaveBeenCalledWith({
			provider: 'discord',
			callbackURL: `${window.location.origin}/account`,
			errorCallbackURL: `${window.location.origin}/account`
		});
		expect(window.location.hash).toBe('#linked');
	});

	it('reports a link failure with and without a message', async () => {
		auth.linkSocial.mockResolvedValue({ data: null, error: { message: 'Already linked' } });
		renderAccount({ linkableProviders: [{ id: 'google', label: 'Google' }] });

		await fireEvent.click(screen.getByRole('button', { name: /Link Google/ }));
		expect(await screen.findByText('Already linked')).toBeTruthy();

		auth.linkSocial.mockResolvedValue({ data: null, error: {} });
		await fireEvent.click(screen.getByRole('button', { name: /Link Google/ }));
		expect(await screen.findByText('Could not link that provider.')).toBeTruthy();
	});

	it('translates the OAuth error the load hands back', () => {
		const first = renderAccount({ oauthError: 'access_denied' });
		expect(screen.getByText('The provider sign-in was cancelled.')).toBeTruthy();
		first.unmount();

		const second = renderAccount({ oauthError: 'account_not_linked' });
		expect(
			screen.getByText('That provider account is not linked to this Veldrath account.')
		).toBeTruthy();
		second.unmount();

		renderAccount({ oauthError: 'server_error' });
		expect(
			screen.getByText(
				'Linking failed. Make sure the provider account uses the same email address as this account.'
			)
		).toBeTruthy();
	});
});

describe('account sessions', () => {
	const session = (id: string, overrides: Record<string, unknown> = {}) => ({
		id,
		token: `token-${id}`,
		createdAt: '2026-01-02T03:04:05.000Z',
		expiresAt: '2026-02-02T03:04:05.000Z',
		ipAddress: '203.0.113.7',
		userAgent: 'Firefox on Linux',
		...overrides
	});

	it('says so when there are no sessions', () => {
		renderAccount({ sessions: [] });

		expect(screen.getByText('No active sessions found.')).toBeTruthy();
	});

	it('marks the current device and offers no revoke for it', () => {
		renderAccount({ sessions: [session('s1')], currentSessionId: 's1' });

		expect(screen.getByText('Firefox on Linux')).toBeTruthy();
		expect(screen.getByText('This device')).toBeTruthy();
		expect(screen.queryByRole('button', { name: /Revoke/ })).toBeNull();
	});

	it('describes a session without a user agent', () => {
		renderAccount({ sessions: [session('s1', { userAgent: null, ipAddress: null })] });

		expect(screen.getByText('Unknown device')).toBeTruthy();
		// The address shares its paragraph with the timestamps, so match on the prefix.
		expect(screen.getByText(/Unknown IP/)).toBeTruthy();
	});

	it('truncates a long user agent to ninety characters', () => {
		renderAccount({ sessions: [session('s1', { userAgent: 'A'.repeat(120) })] });

		expect(screen.getByText(`${'A'.repeat(90)}…`)).toBeTruthy();
	});

	it('revokes another session and refreshes', async () => {
		renderAccount({
			sessions: [session('s1'), session('s2')],
			currentSessionId: 's1'
		});

		await fireEvent.click(screen.getByRole('button', { name: /Revoke/ }));

		await waitFor(() => expect(auth.revokeSession).toHaveBeenCalled());
		expect(auth.revokeSession).toHaveBeenCalledWith({ token: 'token-s2' });
		expect(screen.getByText('Session revoked.')).toBeTruthy();
		expect(invalidateAll).toHaveBeenCalled();
	});

	it('reports a revoke failure with and without a message', async () => {
		auth.revokeSession.mockResolvedValue({ data: null, error: {} });
		renderAccount({ sessions: [session('s1'), session('s2')], currentSessionId: 's1' });

		await fireEvent.click(screen.getByRole('button', { name: /Revoke/ }));
		expect(await screen.findByText('Could not revoke that session.')).toBeTruthy();
	});

	it('signs out the other sessions in one go', async () => {
		renderAccount({ sessions: [session('s1'), session('s2')], currentSessionId: 's1' });

		await fireEvent.click(screen.getByRole('button', { name: /Sign out other sessions/ }));

		await waitFor(() => expect(auth.revokeOtherSessions).toHaveBeenCalled());
		expect(screen.getByText('All other sessions have been signed out.')).toBeTruthy();
		expect(invalidateAll).toHaveBeenCalled();
	});

	it('reports a bulk revoke failure, with a fallback message', async () => {
		auth.revokeOtherSessions.mockResolvedValue({ data: null, error: {} });
		const first = renderAccount({
			sessions: [session('s1'), session('s2')],
			currentSessionId: 's1'
		});

		await fireEvent.click(screen.getByRole('button', { name: /Sign out other sessions/ }));
		expect(await screen.findByText('Could not revoke the other sessions.')).toBeTruthy();
		first.unmount();

		auth.revokeOtherSessions.mockResolvedValue({ data: null, error: { message: 'Server busy' } });
		renderAccount({ sessions: [session('s1'), session('s2')], currentSessionId: 's1' });

		await fireEvent.click(screen.getByRole('button', { name: /Sign out other sessions/ }));
		expect(await screen.findByText('Server busy')).toBeTruthy();
	});

	it('hides the bulk revoke when there is only one session', () => {
		renderAccount({ sessions: [session('s1')], currentSessionId: 's1' });

		expect(screen.queryByRole('button', { name: /Sign out other sessions/ })).toBeNull();
	});
});

describe('account password', () => {
	it('refuses mismatched new passwords', async () => {
		const { container } = renderAccount();
		const inputs = forms(container)[2].querySelectorAll('input[type="password"]');

		await fill(inputs[0], 'current');
		await fill(inputs[1], 'swordfish');
		await fill(inputs[2], 'swordfish2');
		await fireEvent.submit(forms(container)[2]);

		expect(screen.getByText('The new passwords do not match.')).toBeTruthy();
		expect(auth.changePassword).not.toHaveBeenCalled();
	});

	it('changes the password, keeps other sessions, and clears the form', async () => {
		const { container } = renderAccount();
		const form = forms(container)[2];
		const inputs = form.querySelectorAll('input[type="password"]');

		await fill(inputs[0], 'current');
		await fill(inputs[1], 'swordfish');
		await fill(inputs[2], 'swordfish');
		await fireEvent.submit(form);

		await waitFor(() => expect(auth.changePassword).toHaveBeenCalled());
		expect(auth.changePassword).toHaveBeenCalledWith({
			currentPassword: 'current',
			newPassword: 'swordfish',
			revokeOtherSessions: false
		});
		expect(screen.getByText('Password updated.')).toBeTruthy();
		expect(Array.from(inputs).every((input) => (input as HTMLInputElement).value === '')).toBe(
			true
		);
	});

	it('reports a password failure with and without a message', async () => {
		auth.changePassword.mockResolvedValue({ data: null, error: {} });
		const { container } = renderAccount();
		const form = forms(container)[2];
		const inputs = form.querySelectorAll('input[type="password"]');

		await fill(inputs[1], 'swordfish');
		await fill(inputs[2], 'swordfish');
		await fireEvent.submit(form);
		expect(await screen.findByText('Could not change the password.')).toBeTruthy();

		auth.changePassword.mockResolvedValue({
			data: null,
			error: { message: 'Current password is wrong' }
		});
		await fireEvent.submit(form);
		expect(await screen.findByText('Current password is wrong')).toBeTruthy();
	});

	it('signs out and returns to the home page', async () => {
		renderAccount();

		await fireEvent.click(screen.getByRole('button', { name: 'Sign out' }));

		await waitFor(() => expect(auth.signOut).toHaveBeenCalled());
		await waitFor(() => expect(goto).toHaveBeenCalled());
		expect(String(goto.mock.calls[0][0])).toBe('/');
	});
});
