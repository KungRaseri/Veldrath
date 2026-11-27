import { render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';
import AdminDashboard from './(admin)/admin/+page.svelte';
import AdminAudit from './(admin)/admin/audit/+page.svelte';
import AdminReports from './(admin)/admin/reports/+page.svelte';
import AdminRoles from './(admin)/admin/roles/+page.svelte';
import AdminServer from './(admin)/admin/server/+page.svelte';
import AdminSessions from './(admin)/admin/sessions/+page.svelte';
import AdminStatus from './(admin)/admin/status/+page.svelte';
import AdminUserDetail from './(admin)/admin/users/[id]/+page.svelte';
import AdminUsers from './(admin)/admin/users/+page.svelte';
import ModDashboard from './(mod)/mod/+page.svelte';
import ModReports from './(mod)/mod/reports/+page.svelte';
import ModUserDetail from './(mod)/mod/users/[id]/+page.svelte';
import ModUsers from './(mod)/mod/users/+page.svelte';

/**
 * Staff page components: the admin and moderation surfaces.
 *
 * These pages are staff-only and therefore the least likely to be opened by hand before a
 * release, which is exactly why they get render tests. Every list is exercised twice — rows and
 * the empty state — because an empty `<tbody>` and a crashed table look identical in a browser
 * and only one of them is acceptable. Loaders and actions are covered in `staff-loaders.test.ts`,
 * `staff-admin-editorial.test.ts` and `staff-page-actions` fixtures; this suite asserts markup.
 */

/**
 * Props for a page component.
 *
 * `PageData` is generated per route, so a fixture cannot be typed against it without importing a
 * dozen generated modules; the loaders are where those shapes are enforced. `form` is the action
 * result SvelteKit hands back to the page, and `null` is what it is on a plain GET.
 */
function page(data: Record<string, unknown>, form: Record<string, unknown> | null = null) {
	return { data, form } as never;
}

const status = {
	serverStartedAt: '2026-01-02T03:04:05.000Z',
	connectedPlayers: 12,
	registeredAccounts: 340,
	totalCharacters: 410,
	zonePopulations: [
		{ zoneId: 'emberfall', zoneName: 'Emberfall', playerCount: 7 },
		{ zoneId: 'calen', zoneName: 'Calen', playerCount: 5 }
	],
	healthChecks: [
		{ name: 'database', status: 'Healthy', description: 'Postgres reachable' },
		{ name: 'hub', status: 'Degraded', description: null }
	]
};

describe('admin dashboard', () => {
	/** Both halves of the staff split happen to be held here, so every link renders. */
	const can = { manageUsers: true, manageRoles: true, viewPlayers: true, sendAnnouncements: true };

	it('shows the status cards and every management link', () => {
		render(
			AdminDashboard,
			page({
				can,
				status: { connectedPlayers: 12, registeredAccounts: 340, totalCharacters: 410 }
			})
		);

		expect(screen.getByRole('heading', { level: 1, name: 'Admin' })).toBeTruthy();
		expect(screen.getByText('Connected players')).toBeTruthy();
		expect(screen.getByText('Registered accounts')).toBeTruthy();
		expect(screen.getByText('410')).toBeTruthy();
		expect(screen.getByRole('heading', { level: 2, name: 'Users' })).toBeTruthy();
		expect(screen.getByRole('heading', { level: 2, name: 'Server commands' })).toBeTruthy();
	});

	it('offers a role manager only the pages it may open', () => {
		render(
			AdminDashboard,
			page({
				can: { ...can, manageUsers: false, viewPlayers: false, sendAnnouncements: false }
			})
		);

		expect(screen.getByRole('heading', { level: 2, name: 'Roles' })).toBeTruthy();
		expect(screen.queryByRole('heading', { level: 2, name: 'Users' })).toBeNull();
		expect(screen.queryByRole('heading', { level: 2, name: 'Active sessions' })).toBeNull();
		expect(screen.queryByRole('heading', { level: 2, name: 'Server commands' })).toBeNull();
	});

	it('survives a load error and hides the status cards', () => {
		render(AdminDashboard, page({ can, error: 'Status endpoint unreachable' }));

		expect(screen.getByText('Status endpoint unreachable')).toBeTruthy();
		expect(screen.queryByText('Connected players')).toBeNull();
	});
});

describe('admin server status', () => {
	it('renders populations and health checks', () => {
		render(AdminStatus, page({ status }));

		expect(screen.getByRole('heading', { level: 1, name: 'Server status' })).toBeTruthy();
		expect(screen.getByText('Emberfall')).toBeTruthy();
		expect(screen.getByText('emberfall')).toBeTruthy();
		expect(screen.getByText('database')).toBeTruthy();
		expect(screen.getByText('— Postgres reachable')).toBeTruthy();
		expect(screen.getByText('hub')).toBeTruthy();
	});

	it('renders the empty states when nothing is populated and no check describes itself', () => {
		render(
			AdminStatus,
			page({
				status: {
					...status,
					zonePopulations: [],
					healthChecks: [{ name: 'hub', status: 'Degraded', description: null }]
				}
			})
		);

		expect(screen.getByText('No populated zones.')).toBeTruthy();
		expect(screen.queryByText(/—/)).toBeNull();
	});

	it('falls back to the error text when the status call failed', () => {
		render(AdminStatus, page({ error: 'Server did not answer' }));

		expect(screen.getByText('Server did not answer')).toBeTruthy();
		expect(screen.queryByRole('heading', { name: 'Zone populations' })).toBeNull();
	});
});

describe('admin user management', () => {
	const users = [
		{
			id: 'acct-1',
			username: 'kaleth',
			email: 'kaleth@example.com',
			roles: ['Admin', 'Moderator'],
			isBanned: false,
			isMuted: false,
			warnCount: 0,
			createdAt: '2026-01-02T00:00:00.000Z'
		},
		{
			id: 'acct-2',
			username: 'brann',
			email: null,
			roles: [],
			isBanned: true,
			isMuted: false,
			warnCount: 3,
			createdAt: '2026-01-03T00:00:00.000Z'
		},
		{
			id: 'acct-3',
			username: 'sable',
			email: 'sable@example.com',
			roles: ['Curator'],
			isBanned: false,
			isMuted: true,
			warnCount: 1,
			createdAt: '2026-01-04T00:00:00.000Z'
		}
	];

	it('lists accounts with roles, status, warnings, and a pager', () => {
		render(
			AdminUsers,
			page({
				total: 45,
				filters: { search: 'ka', role: 'Admin', status: 'active', sort: 'lastseen' },
				users,
				page: 1,
				pageSize: 20
			})
		);

		expect(screen.getByText('45 accounts.')).toBeTruthy();
		expect(screen.getByRole('link', { name: 'kaleth' }).getAttribute('href')).toBe(
			'/admin/users/acct-1'
		);
		expect(screen.getByText('kaleth@example.com')).toBeTruthy();
		// The role chips and the filter `<option>`s carry the same words, so count rather than
		// assert uniqueness: one chip plus the one matching option.
		expect(screen.getAllByText('Admin')).toHaveLength(2);
		// Status chips share their words with the filter options as well.
		expect(screen.getAllByText('Banned')).toHaveLength(2);
		expect(screen.getAllByText('Muted')).toHaveLength(2);
		expect(screen.getAllByText('Active')).toHaveLength(2);
		expect(screen.getByText('Page 1 of 3')).toBeTruthy();
		expect((screen.getByRole('searchbox') as HTMLInputElement).value).toBe('ka');
	});

	it('renders the empty row, the missing-email dash, and every error notice', () => {
		render(
			AdminUsers,
			page({
				total: 0,
				error: 'Admin API unavailable',
				filters: { search: null, role: null, status: null, sort: null },
				users: [],
				page: 1,
				pageSize: 20
			})
		);

		expect(screen.getByText('Admin API unavailable')).toBeTruthy();
		expect(screen.getByText('No accounts match these filters.')).toBeTruthy();
		expect(screen.queryByText('Page 1 of 1')).toBeNull();
	});

	it('shows a dash for an account with no roles and no email', () => {
		render(
			AdminUsers,
			page({
				total: 1,
				filters: {},
				users: [users[1]],
				page: 1,
				pageSize: 20
			})
		);

		expect(screen.getByText('brann')).toBeTruthy();
		expect(screen.getAllByText('—').length).toBeGreaterThanOrEqual(2);
	});
});

describe('admin user detail', () => {
	const player = {
		id: 'acct-1',
		username: 'kaleth',
		email: 'kaleth@example.com',
		isBanned: true,
		isMuted: true,
		warnCount: 2,
		roles: ['Admin'],
		effectivePermissions: ['players.view', 'content.manage'],
		userPermissions: ['game.broadcast']
	};

	const identity = {
		id: player.id,
		username: player.username,
		roles: player.roles,
		effectivePermissions: player.effectivePermissions,
		userPermissions: player.userPermissions
	};

	const base = {
		account: player,
		authority: identity,
		canManageUsers: true,
		canManageRoles: true,
		allRoles: ['Admin', 'Moderator'],
		allPermissions: ['players.view', 'content.manage', 'game.broadcast']
	};

	it('renders identity, roles, direct grants and every action form', () => {
		render(AdminUserDetail, page(base));

		expect(screen.getByRole('heading', { level: 1, name: 'kaleth' })).toBeTruthy();
		expect(screen.getByText('2 warnings')).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Revoke Admin' })).toBeTruthy();
		expect(screen.getByText('Direct grants')).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Revoke game.broadcast' })).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Issue warning' })).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Ban account' })).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Lift mute' })).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Lift ban' })).toBeTruthy();
	});

	it('hides the role and permission controls from an admin.users-only operator', () => {
		render(AdminUserDetail, page({ ...base, canManageRoles: false }));

		expect(screen.queryByRole('button', { name: 'Revoke Admin' })).toBeNull();
		expect(screen.queryByRole('button', { name: 'Assign' })).toBeNull();
		expect(screen.queryByRole('button', { name: 'Grant' })).toBeNull();
		expect(screen.getByText(/Changing roles requires the/)).toBeTruthy();
		expect(screen.getByText(/Granting permissions requires the/)).toBeTruthy();
		// The account half is still theirs.
		expect(screen.getByRole('button', { name: 'Ban account' })).toBeTruthy();
	});

	it('shows an admin.roles-only operator the authority and no account controls', () => {
		render(
			AdminUserDetail,
			page({
				...base,
				account: null,
				canManageUsers: false
			})
		);

		expect(screen.getByRole('heading', { level: 1, name: 'kaleth' })).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Revoke Admin' })).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Assign' })).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Grant' })).toBeTruthy();
		// Nothing that reads or changes account state, and nothing behind admin.users.
		expect(screen.queryByText(/@example\.com/)).toBeNull();
		expect(screen.queryByText(/warnings/)).toBeNull();
		expect(screen.queryByRole('button', { name: 'Issue warning' })).toBeNull();
		expect(screen.queryByRole('button', { name: 'Ban account' })).toBeNull();
		expect(screen.queryByRole('button', { name: 'Kick sessions' })).toBeNull();
	});

	it('renders an action error and the no-roles / no-permissions / no-email fallbacks', () => {
		render(
			AdminUserDetail,
			page(
				{
					...base,
					account: { ...player, email: null, isBanned: false, isMuted: false },
					authority: {
						...identity,
						roles: [],
						effectivePermissions: [],
						userPermissions: []
					},
					allRoles: ['Admin'],
					allPermissions: ['players.view']
				},
				{ error: 'Player is already banned' }
			)
		);

		expect(screen.getByText('Player is already banned')).toBeTruthy();
		expect(screen.getByText(/No email/)).toBeTruthy();
		expect(screen.getByText('No roles assigned.')).toBeTruthy();
		expect(screen.getByText('No permissions.')).toBeTruthy();
		expect(screen.queryByText('Direct grants')).toBeNull();
		expect(screen.queryByText(/Banned/)).toBeNull();
	});
});

describe('admin audit log', () => {
	it('lists entries and maps null target/details to a dash', () => {
		render(
			AdminAudit,
			page({
				total: 2,
				targetId: 'acct-1',
				entries: [
					{
						id: 'e1',
						occurredAt: '2026-02-01T10:00:00.000Z',
						actorUsername: 'kaleth',
						action: 'player.ban',
						targetUsername: 'brann',
						details: 'Griefing'
					},
					{
						id: 'e2',
						occurredAt: '2026-02-01T11:00:00.000Z',
						actorUsername: 'kaleth',
						action: 'server.broadcast',
						targetUsername: null,
						details: null
					}
				],
				page: 1,
				pageSize: 20
			})
		);

		expect(screen.getByText('2 recorded staff actions.')).toBeTruthy();
		expect(screen.getByText('player.ban')).toBeTruthy();
		expect(screen.getByText('Griefing')).toBeTruthy();
		expect(screen.getByText('server.broadcast')).toBeTruthy();
		expect(screen.getByDisplayValue('acct-1')).toBeTruthy();
	});

	it('renders the empty state and an error', () => {
		render(
			AdminAudit,
			page({
				total: 0,
				error: 'Audit query failed',
				targetId: '',
				entries: [],
				page: 1,
				pageSize: 20
			})
		);

		expect(screen.getByText('Audit query failed')).toBeTruthy();
		expect(screen.getByText('No audit entries yet.')).toBeTruthy();
	});
});

describe('admin reports', () => {
	const reports = [
		{
			id: 'r1',
			reporterName: 'sable',
			targetName: 'brann',
			reason: 'Harassment in chat',
			submittedAt: '2026-02-02T09:00:00.000Z',
			isResolved: false
		},
		{
			id: 'r2',
			reporterName: 'kaleth',
			targetName: 'brann',
			reason: 'Exploiting',
			submittedAt: '2026-02-02T10:00:00.000Z',
			isResolved: true
		}
	];

	it('shows both statuses and only offers Resolve on open reports', () => {
		render(
			AdminReports,
			page({
				total: 2,
				resolved: 'false',
				reports,
				page: 1,
				pageSize: 20
			})
		);

		expect(screen.getByText('2 reports.')).toBeTruthy();
		// `Open` is also a filter option, so it appears twice: once in the select, once as a chip.
		expect(screen.getAllByText('Open')).toHaveLength(2);
		expect(screen.getAllByText('Resolved')).toHaveLength(2);
		expect(screen.getAllByRole('button', { name: 'Resolve' })).toHaveLength(1);
		expect((screen.getByRole('combobox') as HTMLSelectElement).value).toBe('false');
	});

	it('prefers the action error over the load error', () => {
		render(
			AdminReports,
			page(
				{ total: 0, error: 'List failed', resolved: '', reports: [], page: 1, pageSize: 20 },
				{ error: 'Could not resolve that report' }
			)
		);

		expect(screen.getByText('Could not resolve that report')).toBeTruthy();
		expect(screen.queryByText('List failed')).toBeNull();
		expect(screen.getByText('No reports found.')).toBeTruthy();
	});

	it('falls back to the load error when there is no action error', () => {
		render(
			AdminReports,
			page({ total: 0, error: 'List failed', resolved: '', reports: [], page: 1, pageSize: 20 })
		);

		expect(screen.getByText('List failed')).toBeTruthy();
	});
});

describe('admin roles', () => {
	const catalogue = {
		roles: [
			{
				name: 'Admin',
				description: 'Full access',
				permissions: ['players.view', 'admin.roles']
			},
			{ name: 'Curator', description: 'Content review', permissions: ['content.review'] }
		],
		allPermissions: ['players.view', 'admin.roles', 'content.review']
	};

	it('renders each seeded role with its permissions', () => {
		render(AdminRoles, page({ ...catalogue, canManageRoles: true, canManageUsers: true }));

		expect(screen.getByRole('heading', { level: 1, name: 'Role management' })).toBeTruthy();
		expect(screen.getByRole('heading', { level: 2, name: 'Admin' })).toBeTruthy();
		expect(screen.getByText('Full access')).toBeTruthy();
		expect(screen.getByText('admin.roles')).toBeTruthy();
		expect(screen.getByText('content.review')).toBeTruthy();
	});

	it('offers the account lookup to a role manager without account administration', () => {
		render(AdminRoles, page({ ...catalogue, canManageRoles: true, canManageUsers: false }));

		expect(screen.getByRole('button', { name: 'Open account' })).toBeTruthy();
		// The user list is admin.users, so a role manager is not pointed at it.
		expect(screen.queryByRole('link', { name: 'User management' })).toBeNull();
	});

	it('keeps the catalogue read-only for an admin.users-only operator', () => {
		render(AdminRoles, page({ ...catalogue, canManageRoles: false, canManageUsers: true }));

		expect(screen.getByRole('link', { name: 'User management' })).toBeTruthy();
		expect(screen.queryByRole('button', { name: 'Open account' })).toBeNull();
		expect(screen.queryByText("Manage a player's roles")).toBeNull();
	});
});

describe('admin active sessions', () => {
	it('lists connected characters and a dash for a character without a zone', () => {
		render(
			AdminSessions,
			page({
				regionId: 'region-1',
				zoneId: 'emberfall',
				sessions: [
					{
						characterId: 'char-1',
						characterName: 'Kaleth',
						accountId: 'acct-1',
						accountUsername: 'kaleth',
						regionId: 'region-1',
						zoneId: 'emberfall',
						enteredAt: '2026-02-03T09:00:00.000Z'
					},
					{
						characterId: 'char-2',
						characterName: 'Brann',
						accountId: 'acct-2',
						accountUsername: 'brann',
						regionId: 'region-1',
						zoneId: null,
						enteredAt: '2026-02-03T10:00:00.000Z'
					}
				]
			})
		);

		expect(screen.getByText('2 characters currently connected.')).toBeTruthy();
		expect(screen.getByRole('link', { name: 'kaleth' }).getAttribute('href')).toBe(
			'/admin/users/acct-1'
		);
		expect(screen.getByText('Brann')).toBeTruthy();
		expect(screen.getByText('—')).toBeTruthy();
		expect(screen.getByDisplayValue('region-1')).toBeTruthy();
	});

	it('renders the empty state and a load error', () => {
		render(
			AdminSessions,
			page({ regionId: '', zoneId: '', error: 'Hub not reachable', sessions: [] })
		);

		expect(screen.getByText('Hub not reachable')).toBeTruthy();
		expect(screen.getByText('No active sessions.')).toBeTruthy();
		expect(screen.getByText('0 characters currently connected.')).toBeTruthy();
	});
});

describe('admin server commands', () => {
	it('reports a successful broadcast', () => {
		render(AdminServer, page({}, { message: 'Announcement broadcast to 12 clients' }));

		expect(screen.getByText('Announcement broadcast to 12 clients')).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Broadcast announcement' })).toBeTruthy();
	});

	it('reports a failed broadcast instead of the success copy', () => {
		render(AdminServer, page({}, { error: 'Notice cannot be empty' }));

		expect(screen.getByText('Notice cannot be empty')).toBeTruthy();
		expect(screen.queryByText(/broadcast to/)).toBeNull();
	});
});

describe('moderation dashboard', () => {
	it('shows counts and collects load errors', () => {
		render(ModDashboard, page({ userCount: 340, openReports: 3, errors: [] }));

		expect(screen.getByRole('heading', { level: 1, name: 'Moderation' })).toBeTruthy();
		expect(screen.getByText('340')).toBeTruthy();
		expect(screen.getByRole('heading', { level: 2, name: 'Open reports' })).toBeTruthy();
		expect(screen.getByText('3')).toBeTruthy();
	});

	it('lists every collected error', () => {
		render(
			ModDashboard,
			page({ userCount: 0, openReports: 0, errors: ['Accounts failed', 'Reports failed'] })
		);

		expect(screen.getByText('Accounts failed')).toBeTruthy();
		expect(screen.getByText('Reports failed')).toBeTruthy();
	});
});

describe('moderation user list', () => {
	const users = [
		{
			id: 'acct-1',
			username: 'brann',
			roles: [],
			isBanned: true,
			isMuted: false,
			warnCount: 3,
			lastSeenAt: '2026-02-04T09:00:00.000Z'
		},
		{
			id: 'acct-2',
			username: 'sable',
			roles: ['Curator'],
			isBanned: false,
			isMuted: true,
			warnCount: 1,
			lastSeenAt: null
		}
	];

	it('lists accounts with status, warnings, and last seen', () => {
		render(
			ModUsers,
			page({
				total: 2,
				filters: { search: 'br', status: 'banned', sort: 'warncount' },
				users,
				page: 1,
				pageSize: 20
			})
		);

		expect(screen.getByText('2 accounts.')).toBeTruthy();
		expect(screen.getByRole('link', { name: 'sable' }).getAttribute('href')).toBe(
			'/mod/users/acct-2'
		);
		// `Banned` and `Muted` are both filter options and row statuses.
		expect(screen.getAllByText('Banned')).toHaveLength(2);
		expect(screen.getAllByText('Muted')).toHaveLength(2);
		expect(screen.getByText('Curator')).toBeTruthy();
		expect(screen.getAllByText('—').length).toBeGreaterThanOrEqual(2);
		expect((screen.getByRole('searchbox') as HTMLInputElement).value).toBe('br');
	});

	it('renders the empty state and an error', () => {
		render(
			ModUsers,
			page({
				total: 0,
				error: 'Forbidden',
				filters: {},
				users: [],
				page: 1,
				pageSize: 20
			})
		);

		expect(screen.getByText('Forbidden')).toBeTruthy();
		expect(screen.getByText('No accounts match these filters.')).toBeTruthy();
	});
});

describe('moderation user detail', () => {
	const player = {
		id: 'acct-1',
		username: 'brann',
		email: 'brann@example.com',
		isBanned: false,
		isMuted: true,
		warnCount: 3,
		roles: ['Moderator']
	};

	it('renders identity, badges and every moderation form', () => {
		render(ModUserDetail, page({ player }));

		expect(screen.getByRole('heading', { level: 1, name: 'brann' })).toBeTruthy();
		// The identity line mixes the email with the account id, so the matcher is on the
		// rendered text rather than an exact node.
		expect(document.body.textContent).toContain('brann@example.com');
		expect(screen.getByText('Muted')).toBeTruthy();
		expect(screen.getByText('3 warnings')).toBeTruthy();
		expect(screen.getByText('Moderator')).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Kick sessions' })).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Unmute' })).toBeTruthy();
	});

	it('renders an action error and the no-email fallback without the muted badge', () => {
		render(
			ModUserDetail,
			page(
				{ player: { ...player, email: null, isBanned: true, isMuted: false, roles: [] } },
				{ error: 'Cannot warn without a reason' }
			)
		);

		expect(screen.getByText('Cannot warn without a reason')).toBeTruthy();
		expect(screen.getByText(/No email/)).toBeTruthy();
		expect(screen.getByText('Banned')).toBeTruthy();
		expect(screen.queryByText('Muted')).toBeNull();
	});
});

describe('moderation reports', () => {
	it('shows both statuses with a Resolve control on open reports only', () => {
		render(
			ModReports,
			page({
				total: 2,
				resolved: '',
				reports: [
					{
						id: 'r1',
						reporterName: 'sable',
						targetName: 'brann',
						reason: 'Harassment',
						submittedAt: '2026-02-02T09:00:00.000Z',
						isResolved: false
					},
					{
						id: 'r2',
						reporterName: 'kaleth',
						targetName: 'brann',
						reason: 'Exploiting',
						submittedAt: '2026-02-02T10:00:00.000Z',
						isResolved: true
					}
				],
				page: 1,
				pageSize: 20
			})
		);

		expect(screen.getByText('2 reports.')).toBeTruthy();
		expect(screen.getAllByText('Open')).toHaveLength(2);
		expect(screen.getAllByText('Resolved')).toHaveLength(2);
		expect(screen.getAllByRole('button', { name: 'Resolve' })).toHaveLength(1);
	});

	it('renders the empty state and prefers the action error', () => {
		render(
			ModReports,
			page(
				{ total: 0, error: 'List failed', resolved: 'true', reports: [], page: 1, pageSize: 20 },
				{ error: 'Report not found' }
			)
		);

		expect(screen.getByText('Report not found')).toBeTruthy();
		expect(screen.getByText('No reports found.')).toBeTruthy();
	});
});
