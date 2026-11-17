import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { setPage } from '#lib/testing/app-state';
import { goto, resetNavigation } from '#lib/testing/app-navigation';
import ForgotPasswordPage from './forgot-password/+page.svelte';
import LoginPage from './login/+page.svelte';
import RegisterPage from './register/+page.svelte';
import ResetPasswordPage from './reset-password/+page.svelte';
import VerifyEmailPage from './verify-email/+page.svelte';

/**
 * The auth page components.
 *
 * These are the only pages a signed-out visitor can reach, and every branch here ends in either a
 * navigation or a message the player needs to read. The server loads are covered in
 * `auth-pages.test.ts`; this suite covers what the forms do once they are on screen, with
 * `#lib/auth-client` mocked and `$app/navigation` / `$app/state` aliased to the stubs in
 * `src/lib/testing/` (see `vite.config.ts`).
 */

const auth = vi.hoisted(() => ({
	signInEmail: vi.fn(),
	signInUsername: vi.fn(),
	signInSocial: vi.fn(),
	signUpEmail: vi.fn(),
	sendVerificationEmail: vi.fn(),
	requestPasswordReset: vi.fn(),
	resetPassword: vi.fn()
}));

vi.mock('#lib/auth-client', () => ({
	authClient: {
		signIn: {
			email: auth.signInEmail,
			username: auth.signInUsername,
			social: auth.signInSocial
		},
		signUp: { email: auth.signUpEmail },
		sendVerificationEmail: auth.sendVerificationEmail,
		requestPasswordReset: auth.requestPasswordReset,
		resetPassword: auth.resetPassword
	}
}));

/** Types into the first element matching `selector` and lets Svelte's binding settle. */
async function fill(container: HTMLElement, selector: string, value: string): Promise<void> {
	const input = container.querySelector(selector);
	if (!input) throw new Error(`no element matched ${selector}`);
	await fireEvent.input(input, { target: { value } });
}

function form(container: HTMLElement): HTMLFormElement {
	const element = container.querySelector('form');
	if (!element) throw new Error('page rendered no form');
	return element as HTMLFormElement;
}

/** The URL `goto` was asked for, as a string, so a base prefix cannot make the assertion lie. */
function navigatedTo(): string {
	expect(goto).toHaveBeenCalled();
	return String(goto.mock.calls[0][0]);
}

beforeEach(() => {
	auth.signInEmail.mockResolvedValue({ data: {}, error: null });
	auth.signInUsername.mockResolvedValue({ data: {}, error: null });
	auth.signUpEmail.mockResolvedValue({ data: {}, error: null });
	auth.sendVerificationEmail.mockResolvedValue({ data: {}, error: null });
	auth.requestPasswordReset.mockResolvedValue({ data: {}, error: null });
	auth.resetPassword.mockResolvedValue({ data: {}, error: null });
	setPage();
	resetNavigation();
});

afterEach(() => {
	for (const spy of [
		auth.signInEmail,
		auth.signInUsername,
		auth.signInSocial,
		auth.signUpEmail,
		auth.sendVerificationEmail,
		auth.requestPasswordReset,
		auth.resetPassword
	]) {
		spy.mockReset();
	}
	vi.useRealTimers();
});

describe('login page', () => {
	it('renders the cancel message a provider redirect can carry', async () => {
		setPage({ url: new URL('http://localhost/login?error=access_denied') });

		render(LoginPage);

		expect(screen.getByText('Sign-in was cancelled.')).toBeTruthy();
	});

	it('explains an unlinked provider and falls back to a generic message', async () => {
		setPage({ url: new URL('http://localhost/login?error=account_not_linked') });
		const first = render(LoginPage);
		expect(screen.getByText(/not linked to this account/)).toBeTruthy();
		first.unmount();

		setPage({ url: new URL('http://localhost/login?error=server_error') });
		render(LoginPage);
		expect(screen.getByText(/Social sign-in failed/)).toBeTruthy();
	});

	it('signs in by email when the identifier contains an @', async () => {
		const { container } = render(LoginPage);

		await fill(container, 'input[type="text"]', '  player@example.com  ');
		await fill(container, 'input[type="password"]', 'swordfish');
		await fireEvent.submit(form(container));

		await waitFor(() =>
			expect(auth.signInEmail).toHaveBeenCalledWith({
				email: 'player@example.com',
				password: 'swordfish'
			})
		);
		expect(auth.signInUsername).not.toHaveBeenCalled();
		expect(navigatedTo()).toContain('/characters');
	});

	it('signs in by username otherwise', async () => {
		const { container } = render(LoginPage);

		await fill(container, 'input[type="text"]', 'kaleth');
		await fill(container, 'input[type="password"]', 'swordfish');
		await fireEvent.submit(form(container));

		await waitFor(() =>
			expect(auth.signInUsername).toHaveBeenCalledWith({
				username: 'kaleth',
				password: 'swordfish'
			})
		);
		expect(auth.signInEmail).not.toHaveBeenCalled();
		expect(navigatedTo()).toContain('/characters');
	});

	it('returns to the guarded page that sent the visitor here', async () => {
		setPage({ url: new URL('http://localhost/login?redirectTo=%2Ffoundry%3Ftab%3Dmine') });
		const { container } = render(LoginPage);

		await fill(container, 'input[type="text"]', 'kaleth');
		await fill(container, 'input[type="password"]', 'swordfish');
		await fireEvent.submit(form(container));

		await waitFor(() => expect(goto).toHaveBeenCalled());
		expect(navigatedTo()).toContain('/foundry?tab=mine');
	});

	it('shows the failure and stays on the page when the credentials are rejected', async () => {
		auth.signInUsername.mockResolvedValue({ data: null, error: { message: 'Invalid password' } });
		const { container } = render(LoginPage);

		await fill(container, 'input[type="text"]', 'kaleth');
		await fill(container, 'input[type="password"]', 'wrong');
		await fireEvent.submit(form(container));

		expect(await screen.findByText('Invalid password')).toBeTruthy();
		expect(goto).not.toHaveBeenCalled();
		expect(screen.getByRole('button', { name: /Sign in/ })).toBeTruthy();
	});

	it('falls back to a generic message when the failure carries none', async () => {
		auth.signInUsername.mockResolvedValue({ data: null, error: {} });
		const { container } = render(LoginPage);

		await fill(container, 'input[type="text"]', 'kaleth');
		await fill(container, 'input[type="password"]', 'wrong');
		await fireEvent.submit(form(container));

		expect(await screen.findByText('Sign-in failed.')).toBeTruthy();
	});

	it('offers the three providers and reports a provider failure', async () => {
		auth.signInSocial.mockResolvedValue({ data: null, error: { message: 'Discord is down' } });
		const { container } = render(LoginPage);

		expect(screen.getByRole('button', { name: /Continue with Discord/ })).toBeTruthy();
		expect(screen.getByRole('button', { name: /Continue with Google/ })).toBeTruthy();
		expect(screen.getByRole('button', { name: /Continue with Microsoft/ })).toBeTruthy();

		await fireEvent.click(container.querySelector('.oauth-discord') as HTMLButtonElement);

		expect(await screen.findByText('Discord is down')).toBeTruthy();
		expect(auth.signInSocial).toHaveBeenCalledWith({
			provider: 'discord',
			callbackURL: `${window.location.origin}/characters`,
			errorCallbackURL: `${window.location.origin}/login`
		});
	});

	it('falls back to a provider-specific message when the failure carries none', async () => {
		auth.signInSocial.mockResolvedValue({ data: null, error: {} });
		const { container } = render(LoginPage);

		await fireEvent.click(container.querySelector('.oauth-google') as HTMLButtonElement);

		expect(await screen.findByText('google sign-in failed.')).toBeTruthy();
	});

	it('reports a thrown provider error instead of leaving the button spinning', async () => {
		auth.signInSocial.mockRejectedValue(new Error('network is down'));
		const { container } = render(LoginPage);

		await fireEvent.click(container.querySelector('.oauth-microsoft') as HTMLButtonElement);

		expect(await screen.findByText('network is down')).toBeTruthy();
		expect(screen.getByRole('button', { name: /Continue with Microsoft/ })).toBeTruthy();
	});

	it('describes a thrown non-error as a provider failure', async () => {
		auth.signInSocial.mockRejectedValue('nope');
		const { container } = render(LoginPage);

		await fireEvent.click(container.querySelector('.oauth-discord') as HTMLButtonElement);

		expect(await screen.findByText('discord sign-in failed.')).toBeTruthy();
	});
});

describe('register page', () => {
	it('refuses a password that does not match its confirmation', async () => {
		const { container } = render(RegisterPage);

		await fill(container, 'input[type="text"]', 'kaleth');
		await fill(container, 'input[type="email"]', 'kaleth@example.com');
		const passwords = container.querySelectorAll('input[type="password"]');
		await fireEvent.input(passwords[0], { target: { value: 'swordfish' } });
		await fireEvent.input(passwords[1], { target: { value: 'swordfish2' } });
		await fireEvent.submit(form(container));

		expect(screen.getByText('Passwords do not match.')).toBeTruthy();
		expect(auth.signUpEmail).not.toHaveBeenCalled();
		expect(goto).not.toHaveBeenCalled();
	});

	it('creates the account and enters the game', async () => {
		const { container } = render(RegisterPage);

		await fill(container, 'input[type="text"]', 'kaleth');
		await fill(container, 'input[type="email"]', 'kaleth@example.com');
		const passwords = container.querySelectorAll('input[type="password"]');
		await fireEvent.input(passwords[0], { target: { value: 'swordfish' } });
		await fireEvent.input(passwords[1], { target: { value: 'swordfish' } });
		await fireEvent.submit(form(container));

		await waitFor(() => expect(auth.signUpEmail).toHaveBeenCalled());
		expect(auth.signUpEmail).toHaveBeenCalledWith({
			email: 'kaleth@example.com',
			password: 'swordfish',
			name: 'kaleth',
			username: 'kaleth'
		});
		expect(navigatedTo()).toContain('/game');
	});

	it('shows the registration failure and does not navigate', async () => {
		auth.signUpEmail.mockResolvedValue({ data: null, error: { message: 'Username taken' } });
		const { container } = render(RegisterPage);

		await fill(container, 'input[type="text"]', 'kaleth');
		await fill(container, 'input[type="email"]', 'kaleth@example.com');
		const passwords = container.querySelectorAll('input[type="password"]');
		await fireEvent.input(passwords[0], { target: { value: 'swordfish' } });
		await fireEvent.input(passwords[1], { target: { value: 'swordfish' } });
		await fireEvent.submit(form(container));

		expect(await screen.findByText('Username taken')).toBeTruthy();
		expect(goto).not.toHaveBeenCalled();
	});

	it('falls back to a generic registration message', async () => {
		auth.signUpEmail.mockResolvedValue({ data: null, error: {} });
		const { container } = render(RegisterPage);

		await fill(container, 'input[type="text"]', 'kaleth');
		await fill(container, 'input[type="email"]', 'kaleth@example.com');
		const passwords = container.querySelectorAll('input[type="password"]');
		await fireEvent.input(passwords[0], { target: { value: 'swordfish' } });
		await fireEvent.input(passwords[1], { target: { value: 'swordfish' } });
		await fireEvent.submit(form(container));

		expect(await screen.findByText('Registration failed.')).toBeTruthy();
	});
});

describe('verify-email page', () => {
	it('confirms a verified address and hides the resend form', () => {
		const { container } = render(VerifyEmailPage, { data: { status: 'verified' } as never });

		expect(screen.getByText('Email verified')).toBeTruthy();
		expect(container.querySelector('form')).toBeNull();
		expect(screen.getByRole('link', { name: 'Account' })).toBeTruthy();
	});

	it('explains a link that arrived without its token', () => {
		const { container } = render(VerifyEmailPage, { data: { status: 'missing' } as never });

		expect(screen.getByText('Verification link incomplete')).toBeTruthy();
		expect(container.querySelector('form')).not.toBeNull();
	});

	it('explains an invalid or expired link', () => {
		render(VerifyEmailPage, { data: { status: 'invalid' } as never });

		expect(screen.getByText('Verification failed')).toBeTruthy();
	});

	it('sends a new link and says so without promising the address exists', async () => {
		const { container } = render(VerifyEmailPage, { data: { status: 'invalid' } as never });

		await fill(container, 'input[type="email"]', 'kaleth@example.com');
		await fireEvent.submit(form(container));

		await waitFor(() => expect(auth.sendVerificationEmail).toHaveBeenCalled());
		expect(auth.sendVerificationEmail).toHaveBeenCalledWith({
			email: 'kaleth@example.com',
			callbackURL: `${window.location.origin}/account`
		});
		expect(
			await screen.findByText(
				'If an account exists for that address, a new confirmation link has been sent.'
			)
		).toBeTruthy();
	});

	it('reports a resend failure', async () => {
		auth.sendVerificationEmail.mockResolvedValue({
			data: null,
			error: { message: 'Rate limited' }
		});
		const { container } = render(VerifyEmailPage, { data: { status: 'missing' } as never });

		await fill(container, 'input[type="email"]', 'kaleth@example.com');
		await fireEvent.submit(form(container));

		expect(await screen.findByText('Rate limited')).toBeTruthy();
	});

	it('falls back to a generic resend failure message', async () => {
		auth.sendVerificationEmail.mockResolvedValue({ data: null, error: {} });
		const { container } = render(VerifyEmailPage, { data: { status: 'missing' } as never });

		await fill(container, 'input[type="email"]', 'kaleth@example.com');
		await fireEvent.submit(form(container));

		expect(await screen.findByText('Could not send a new verification email.')).toBeTruthy();
	});
});

describe('forgot password page', () => {
	it('asks Better Auth for a reset link pointing back at the reset page', async () => {
		const { container } = render(ForgotPasswordPage);

		await fill(container, 'input[type="email"]', 'kaleth@example.com');
		await fireEvent.submit(form(container));

		await waitFor(() => expect(auth.requestPasswordReset).toHaveBeenCalled());
		expect(auth.requestPasswordReset).toHaveBeenCalledWith({
			email: 'kaleth@example.com',
			redirectTo: `${window.location.origin}/reset-password`
		});
		expect(
			await screen.findByText(
				'If an account exists for that address, a reset link has been sent. Check your email.'
			)
		).toBeTruthy();
	});

	it('reports a request failure', async () => {
		auth.requestPasswordReset.mockResolvedValue({ data: null, error: { message: 'Mail is down' } });
		const { container } = render(ForgotPasswordPage);

		await fill(container, 'input[type="email"]', 'kaleth@example.com');
		await fireEvent.submit(form(container));

		expect(await screen.findByText('Mail is down')).toBeTruthy();
	});

	it('falls back to a generic request failure message', async () => {
		auth.requestPasswordReset.mockResolvedValue({ data: null, error: {} });
		const { container } = render(ForgotPasswordPage);

		await fill(container, 'input[type="email"]', 'kaleth@example.com');
		await fireEvent.submit(form(container));

		expect(await screen.findByText('Could not request a password reset.')).toBeTruthy();
	});
});

describe('reset password page', () => {
	it('sends the player to the request page when the link has no token', () => {
		const { container } = render(ResetPasswordPage);

		expect(screen.getByText(/Request a new link from the/)).toBeTruthy();
		expect(container.querySelector('form')).toBeNull();
	});

	it('refuses a link with no token even if a form were submitted', async () => {
		// The form is not rendered without a token, so the guard is exercised by the template only;
		// this asserts the visible fallback rather than a submit path the player cannot reach.
		render(ResetPasswordPage);

		expect(screen.getByRole('link', { name: 'forgot password' })).toBeTruthy();
		expect(auth.resetPassword).not.toHaveBeenCalled();
	});

	it('refuses mismatched passwords before calling the API', async () => {
		setPage({ url: new URL('http://localhost/reset-password?token=abc') });
		const { container } = render(ResetPasswordPage);

		const inputs = container.querySelectorAll('input[type="password"]');
		await fireEvent.input(inputs[0], { target: { value: 'swordfish' } });
		await fireEvent.input(inputs[1], { target: { value: 'swordfish2' } });
		await fireEvent.submit(form(container));

		expect(screen.getByText('The passwords do not match.')).toBeTruthy();
		expect(auth.resetPassword).not.toHaveBeenCalled();
	});

	it('resets the password with the token from the URL and then signs in', async () => {
		setPage({ url: new URL('http://localhost/reset-password?token=abc') });
		vi.useFakeTimers();
		const { container } = render(ResetPasswordPage);

		const inputs = container.querySelectorAll('input[type="password"]');
		await fireEvent.input(inputs[0], { target: { value: 'swordfish' } });
		await fireEvent.input(inputs[1], { target: { value: 'swordfish' } });
		await fireEvent.submit(form(container));

		expect(auth.resetPassword).toHaveBeenCalledWith({ newPassword: 'swordfish', token: 'abc' });
		expect(
			screen.getByText('Password updated. You can sign in with your new password.')
		).toBeTruthy();
		expect(goto).not.toHaveBeenCalled();

		await vi.advanceTimersByTimeAsync(1500);
		expect(navigatedTo()).toContain('/login');
	});

	it('reports a reset failure and stays put', async () => {
		setPage({ url: new URL('http://localhost/reset-password?token=abc') });
		auth.resetPassword.mockResolvedValue({ data: null, error: { message: 'Token expired' } });
		const { container } = render(ResetPasswordPage);

		const inputs = container.querySelectorAll('input[type="password"]');
		await fireEvent.input(inputs[0], { target: { value: 'swordfish' } });
		await fireEvent.input(inputs[1], { target: { value: 'swordfish' } });
		await fireEvent.submit(form(container));

		expect(await screen.findByText('Token expired')).toBeTruthy();
		expect(goto).not.toHaveBeenCalled();
	});

	it('falls back to a generic reset failure message', async () => {
		setPage({ url: new URL('http://localhost/reset-password?token=abc') });
		auth.resetPassword.mockResolvedValue({ data: null, error: {} });
		const { container } = render(ResetPasswordPage);

		const inputs = container.querySelectorAll('input[type="password"]');
		await fireEvent.input(inputs[0], { target: { value: 'swordfish' } });
		await fireEvent.input(inputs[1], { target: { value: 'swordfish' } });
		await fireEvent.submit(form(container));

		expect(await screen.findByText('Could not reset the password.')).toBeTruthy();
	});
});
