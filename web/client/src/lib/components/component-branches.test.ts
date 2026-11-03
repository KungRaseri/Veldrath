import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import Pager from '#lib/components/Pager.svelte';
import SocialLoginButtons from '#lib/components/auth/SocialLoginButtons.svelte';

/**
 * Branch-coverage companion for the shared chrome components.
 *
 * `Pager` is pure markup, so the cases here push the two boundary values the existing tests miss:
 * a page count the loader could not parse (`null`) and a query whose optional filters are absent.
 *
 * `SocialLoginButtons` owns the only OAuth click path on the site. Each outcome is asserted
 * separately because the four of them are what the component's error handling exists for: a
 * provider redirect, a provider error with a message, a provider error without one, and a thrown
 * transport error. `#lib/auth-client` is mocked — the real client would try to reach Better Auth.
 */
const auth = vi.hoisted(() => ({
	signIn: { social: vi.fn() }
}));

vi.mock('#lib/auth-client', () => ({ authClient: auth }));

beforeEach(() => {
	auth.signIn.social.mockReset();
});

afterEach(() => {
	cleanup();
	vi.restoreAllMocks();
});

describe('pager boundaries', () => {
	it('renders a middle page with working previous and next links', () => {
		render(Pager, { page: 2, pageSize: 10, total: 30, basePath: '/admin/users' });

		expect(screen.getByText('Page 2 of 3')).toBeTruthy();
		expect(screen.getByRole('link', { name: '← Prev' }).getAttribute('href')).toBe(
			'/admin/users?page=1'
		);
		expect(screen.getByRole('link', { name: 'Next →' }).getAttribute('href')).toBe(
			'/admin/users?page=3'
		);
	});

	it('disables the next link on the last page and skips empty query values', () => {
		const { container } = render(Pager, {
			page: 3,
			pageSize: 10,
			total: 30,
			basePath: '/mod/reports',
			query: { resolved: null, search: undefined, status: 'active' }
		});

		const next = screen.getByRole('link', { name: 'Next →' });
		expect(next.getAttribute('aria-disabled')).toBe('true');
		// Only the set filter survives into the URL, and the last page clamps to itself.
		expect(next.getAttribute('href')).toBe('/mod/reports?status=active&page=3');
		expect(container.querySelector('.pointer-events-none')).toBeTruthy();
	});

	it('renders the fallback page label when the loader handed over no page number', () => {
		render(Pager, { page: null as never, pageSize: 10, total: 30, basePath: '/lore' });

		expect(screen.getByText(/Page/).textContent).toContain('of 3');
	});
});

describe('social login buttons', () => {
	it('lists every provider and redirects on a successful sign-in', async () => {
		auth.signIn.social.mockResolvedValue({
			data: { url: 'https://discord.example/oauth' },
			error: null
		});

		render(SocialLoginButtons, {});

		expect(screen.getByRole('button', { name: /Continue with Discord/ })).toBeTruthy();
		expect(screen.getByRole('button', { name: /Continue with Google/ })).toBeTruthy();
		expect(screen.getByRole('button', { name: /Continue with Microsoft/ })).toBeTruthy();

		await fireEvent.click(screen.getByRole('button', { name: /Continue with Discord/ }));

		await waitFor(() => expect(auth.signIn.social).toHaveBeenCalledTimes(1));
		expect(screen.queryByText(/sign-in failed/)).toBeNull();
	});

	it('shows the provider error message when sign-in fails', async () => {
		auth.signIn.social.mockResolvedValue({ data: null, error: { message: 'Discord is down' } });

		render(SocialLoginButtons, {});
		await fireEvent.click(screen.getByRole('button', { name: /Continue with Discord/ }));

		expect(await screen.findByText('Discord is down')).toBeTruthy();
	});

	it('falls back to generic copy when the provider error carries no message', async () => {
		auth.signIn.social.mockResolvedValue({ data: null, error: {} });

		render(SocialLoginButtons, {});
		await fireEvent.click(screen.getByRole('button', { name: /Continue with Google/ }));

		expect(await screen.findByText('google sign-in failed.')).toBeTruthy();
	});

	it('reports a thrown transport error and a non-Error rejection', async () => {
		auth.signIn.social.mockRejectedValue(new Error('network unreachable'));

		render(SocialLoginButtons, {});
		await fireEvent.click(screen.getByRole('button', { name: /Continue with Microsoft/ }));
		expect(await screen.findByText('network unreachable')).toBeTruthy();

		auth.signIn.social.mockRejectedValue('nope');
		await fireEvent.click(screen.getByRole('button', { name: /Continue with Microsoft/ }));
		expect(await screen.findByText('microsoft sign-in failed.')).toBeTruthy();
	});

	it('leaves the buttons enabled when a call resolves without a redirect url', async () => {
		auth.signIn.social.mockResolvedValue({ data: {}, error: null });

		render(SocialLoginButtons, {});
		const button = screen.getByRole('button', { name: /Continue with Discord/ });
		await fireEvent.click(button);

		await waitFor(() => expect(auth.signIn.social).toHaveBeenCalledTimes(1));
		expect((button as HTMLButtonElement).disabled).toBe(false);
	});
});
