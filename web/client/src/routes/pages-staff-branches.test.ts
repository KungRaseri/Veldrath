import { cleanup, render, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it } from 'vitest';
import HardcoreBoard from './leaderboard/hardcore/+page.svelte';
import AdminReports from './(admin)/admin/reports/+page.svelte';
import AdminStatus from './(admin)/admin/status/+page.svelte';
import AdminUserDetail from './(admin)/admin/users/[id]/+page.svelte';
import AdminUsers from './(admin)/admin/users/+page.svelte';
import ModReports from './(mod)/mod/reports/+page.svelte';
import ModUserDetail from './(mod)/mod/users/[id]/+page.svelte';
import ModUsers from './(mod)/mod/users/+page.svelte';
import PlayerProfile from './players/[id]/+page.svelte';

/**
 * Branch-coverage companion for the staff, admin, moderation, and leaderboard pages.
 *
 * Each page here already has a happy-path render test. What those never exercise is the *absent*
 * value: Svelte compiles `{expr}` to `expr ?? ''`, and an API payload that omits a field takes that
 * fallback. These cases render the sparse payloads (no total, no username, no email, no warn
 * count, no roles, no health checks) plus the status permutations the row-level `{#if}` chains
 * have (`Active` needs *both* flags false, which no existing fixture produced) so the empty side of
 * every chain is asserted rather than assumed.
 *
 * Artefact note: the extra branches on `<select value={...}>` lines come from the compiled
 * `if (select_value !== (select_value = ...))` guard, whose false side and inner `?? ''` cannot run
 * from a static render; they are not coverable.
 */

function page(data: Record<string, unknown>, form: Record<string, unknown> | null = null) {
	return { data, form } as never;
}

/**
 * `@testing-library/svelte` only auto-cleans when Vitest runs with globals enabled, which this
 * project does not, so each case would otherwise see the DOM of the previous render.
 */
afterEach(cleanup);

describe('admin user list, sparse totals', () => {
	it('renders an absent total and the empty row', () => {
		render(
			AdminUsers,
			page({
				total: null,
				error: 'Accounts unavailable',
				filters: {},
				users: [],
				page: 1,
				pageSize: 20
			})
		);

		expect(screen.getByText('Accounts unavailable')).toBeTruthy();
		expect(screen.getByText('No accounts match these filters.')).toBeTruthy();
	});

	it('re-renders a filtered list without disturbing the select values', async () => {
		const data = {
			total: 1,
			filters: { search: 'kaleth', role: 'Admin', status: 'active', sort: 'lastseen' },
			users: [
				{
					id: 'acct-1',
					username: 'kaleth',
					email: 'kaleth@example.com',
					roles: ['Admin'],
					isBanned: false,
					isMuted: false,
					warnCount: 0,
					createdAt: '2026-01-02T00:00:00.000Z'
				}
			],
			page: 1,
			pageSize: 20
		};

		const { rerender } = render(AdminUsers, page(data));
		// A second pass with an equal payload re-runs the compiled `<select>` value effects, which
		// only skip their write when the selection has not moved.
		await rerender(page(data));
		await rerender(page(data));

		expect((screen.getByLabelText('Role') as HTMLSelectElement).value).toBe('Admin');
		expect((screen.getByLabelText('Status') as HTMLSelectElement).value).toBe('active');
		expect((screen.getByLabelText('Sort') as HTMLSelectElement).value).toBe('lastseen');
	});
});

describe('admin user detail, sparse identity', () => {
	const player = {
		id: 'acct-1',
		username: 'kaleth',
		email: 'kaleth@example.com',
		isBanned: false,
		isMuted: false,
		warnCount: 2,
		roles: ['Admin'],
		effectivePermissions: ['players.view'],
		userPermissions: ['game.broadcast']
	};

	it('renders a warning count that the payload omitted', () => {
		render(
			AdminUserDetail,
			page({
				player: { ...player, warnCount: null, isBanned: true, isMuted: true },
				allRoles: ['Admin'],
				allPermissions: ['players.view']
			})
		);

		expect(screen.getByText('warnings')).toBeTruthy();
		expect(screen.getByText('Banned')).toBeTruthy();
		expect(screen.getByText('Muted')).toBeTruthy();
	});

	it('renders an account whose username the payload omitted', () => {
		render(
			AdminUserDetail,
			page({
				player: { ...player, username: null },
				allRoles: ['Admin'],
				allPermissions: ['players.view']
			})
		);

		expect(document.body.textContent).toContain('account');
		expect(screen.getByRole('button', { name: 'Revoke Admin' })).toBeTruthy();
	});

	it('renders unlabelled role and permission entries without crashing', () => {
		render(
			AdminUserDetail,
			page({
				player: {
					...player,
					roles: [null],
					effectivePermissions: [null],
					userPermissions: [null]
				},
				allRoles: [null],
				allPermissions: [null]
			})
		);

		expect(screen.getAllByRole('button', { name: 'Revoke null' })).toHaveLength(2);
		expect(screen.getByText('Direct grants')).toBeTruthy();
		expect(screen.getAllByRole('combobox')).toHaveLength(2);
	});
});

describe('moderation user list, active accounts', () => {
	it('renders an active row for a user who is neither banned nor muted', () => {
		render(
			ModUsers,
			page({
				total: null,
				filters: {},
				users: [
					{
						id: 'acct-1',
						username: 'sable',
						roles: ['Curator'],
						isBanned: false,
						isMuted: false,
						warnCount: 0,
						lastSeenAt: null
					}
				],
				page: 1,
				pageSize: 20
			})
		);

		// 'Active' also appears as a filter option, so the row value is counted, not matched once.
		expect(screen.getAllByText('Active')).toHaveLength(2);
		expect(screen.getByText('—')).toBeTruthy();
	});

	it('re-renders a filtered list without disturbing the select values', async () => {
		const data = {
			total: 1,
			filters: { search: 'sable', status: 'active', sort: 'warncount' },
			users: [
				{
					id: 'acct-1',
					username: 'sable',
					roles: [],
					isBanned: false,
					isMuted: false,
					warnCount: 0,
					lastSeenAt: null
				}
			],
			page: 1,
			pageSize: 20
		};

		const { rerender } = render(ModUsers, page(data));
		await rerender(page(data));

		expect((screen.getByLabelText('Status') as HTMLSelectElement).value).toBe('active');
		expect((screen.getByLabelText('Sort') as HTMLSelectElement).value).toBe('warncount');
	});
});

describe('moderation user detail, sparse identity', () => {
	it('renders absent account fields', () => {
		render(
			ModUserDetail,
			page({
				player: {
					id: null,
					username: null,
					email: null,
					isBanned: false,
					isMuted: false,
					warnCount: null,
					roles: []
				}
			})
		);

		expect(screen.getByText(/No email/)).toBeTruthy();
		expect(screen.getByText('warnings')).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Kick sessions' })).toBeTruthy();
	});
});

describe('report queues, sparse totals', () => {
	const report = {
		id: 'r1',
		reporterName: 'kaleth',
		targetName: 'sable',
		reason: 'Abusive chat',
		submittedAt: '2026-02-01T00:00:00.000Z',
		isResolved: false
	};

	it('renders the admin queue with an absent total and an action error', () => {
		render(
			AdminReports,
			page(
				{
					total: null,
					resolved: 'false',
					reports: [{ ...report, isResolved: true }],
					page: 1,
					pageSize: 20
				},
				{ error: 'Report already resolved' }
			)
		);

		expect(screen.getByText('Report already resolved')).toBeTruthy();
		// 'Resolved' is both a filter option and the row status, so both are expected.
		expect(screen.getAllByText('Resolved')).toHaveLength(2);
		expect(screen.queryByRole('button', { name: 'Resolve' })).toBeNull();
	});

	it('renders the moderation queue with an absent total and a load error', () => {
		render(
			ModReports,
			page({
				total: null,
				resolved: '',
				error: 'Report queue unavailable',
				reports: [report],
				page: 1,
				pageSize: 20
			})
		);

		expect(screen.getByText('Report queue unavailable')).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Resolve' })).toBeTruthy();
		// 'Open' is both a filter option and the row status, so both are expected.
		expect(screen.getAllByText('Open')).toHaveLength(2);
	});

	it('re-renders each queue without disturbing the status filter', async () => {
		const adminData = {
			total: 1,
			resolved: 'true',
			reports: [{ ...report, isResolved: true }],
			page: 1,
			pageSize: 20
		};
		const admin = render(AdminReports, page(adminData));
		await admin.rerender(page(adminData));
		expect((screen.getByLabelText('Status') as HTMLSelectElement).value).toBe('true');
		admin.unmount();

		const modData = { ...adminData, resolved: 'false', reports: [report] };
		const mod = render(ModReports, page(modData));
		await mod.rerender(page(modData));
		expect((screen.getByLabelText('Status') as HTMLSelectElement).value).toBe('false');
	});
});

describe('admin server status, sparse health checks', () => {
	it('tolerates a status payload with no health checks', () => {
		render(
			AdminStatus,
			page({
				status: {
					serverStartedAt: '2026-01-02T03:04:05.000Z',
					connectedPlayers: 0,
					registeredAccounts: 0,
					totalCharacters: 0,
					zonePopulations: [],
					healthChecks: null
				}
			})
		);

		expect(screen.getByText('No populated zones.')).toBeTruthy();
		expect(screen.getByText('No health checks reported.')).toBeTruthy();
	});
});

describe('hardcore board, sparse entries', () => {
	const board = {
		page: 1,
		pageSize: 20,
		total: 1,
		items: [
			{
				characterId: 'char-1',
				name: 'Kaleth',
				className: 'Runeblade',
				level: 42,
				experience: 1200,
				diedAt: '2026-02-01T00:00:00.000Z',
				isOwn: true
			}
		]
	};

	it('renders the empty state when the board failed to load', () => {
		render(HardcoreBoard, page({ board: null, loadError: null }));

		expect(screen.getByText(/No hardcore characters yet/)).toBeTruthy();
	});

	it('renders a board with no further pages and a sparse entry', () => {
		render(
			HardcoreBoard,
			page({
				board: {
					...board,
					items: [
						{
							characterId: 'char-2',
							name: null,
							className: null,
							level: null,
							experience: 0,
							diedAt: null,
							isOwn: false
						}
					]
				}
			})
		);

		expect(screen.queryByRole('link', { name: 'Next' })).toBeNull();
		expect(screen.queryByRole('link', { name: 'Previous' })).toBeNull();
		expect(screen.getByText(/Level/)).toBeTruthy();
	});

	it('renders the second page with a previous link', () => {
		render(HardcoreBoard, page({ board: { ...board, page: 2, total: 25 } }));

		expect(screen.getByRole('link', { name: 'Previous' }).getAttribute('href')).toBe(
			'/leaderboard/hardcore?page=1'
		);
	});

	it('falls back to the first page when the board carries no page number', () => {
		render(HardcoreBoard, page({ board: { ...board, page: null } }));

		expect(screen.queryByRole('link', { name: 'Previous' })).toBeNull();
		expect(screen.getByText('Kaleth')).toBeTruthy();
	});
});

describe('player profile, sparse identity', () => {
	it('omits the class and species segments when the payload has neither', () => {
		render(
			PlayerProfile,
			page({
				profile: {
					username: null,
					level: null,
					characterClass: null,
					species: null,
					currentZone: null,
					registeredAt: '2026-01-02T00:00:00.000Z',
					accountId: 'acct-1'
				}
			})
		);

		expect(screen.getByText('Unknown')).toBeTruthy();
		expect(screen.getByText('acct-1')).toBeTruthy();
		expect(screen.queryByText(/Runeblade/)).toBeNull();
	});
});
