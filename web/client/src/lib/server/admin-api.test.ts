import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
	assignRole,
	banPlayer,
	broadcastAnnouncement,
	getAuditLog,
	getModUser,
	getServerStatus,
	getUser,
	grantPermission,
	kickPlayer,
	listModReports,
	listModUsers,
	listReports,
	listSessions,
	listUsers,
	mutePlayer,
	resolveModReport,
	resolveReport,
	revokePermission,
	revokeRole,
	submitReport,
	unbanPlayer,
	unmutePlayer,
	warnPlayer
} from '#lib/server/admin-api';

/**
 * Admin and moderation portals are thin wrappers too, but the verb and path decide which
 * permission the .NET endpoint demands. `/api/admin` and `/api/mod` are not interchangeable: the
 * moderator surface is read-only plus report resolution.
 */

const gameApiFetch = vi.hoisted(() => vi.fn());

vi.mock('#lib/server/game-request', () => ({ gameApiFetch }));

const identity = { id: 'user-1', username: 'admin' };

beforeEach(() => {
	gameApiFetch.mockResolvedValue({ status: 200, data: null, error: null });
});

afterEach(() => {
	gameApiFetch.mockReset();
});

const lastCall = () => {
	const call = gameApiFetch.mock.calls.at(-1) as unknown[];
	return { identity: call[0], path: call[1], options: call[2] } as {
		identity: unknown;
		path: string;
		options?: Record<string, unknown>;
	};
};

describe('admin reads', () => {
	it('lists users with the default admin window', () => {
		listUsers(identity);

		expect(lastCall()).toEqual({
			identity,
			path: '/api/admin/users',
			options: {
				query: {
					search: undefined,
					role: undefined,
					status: undefined,
					sort: undefined,
					page: 1,
					pageSize: 25
				}
			}
		});
	});

	it('passes user filters through', () => {
		listUsers(identity, { search: 'aria', role: 'Admin', status: 'banned', sort: 'name', page: 2 });

		expect(lastCall().options?.query).toMatchObject({
			search: 'aria',
			role: 'Admin',
			status: 'banned',
			sort: 'name',
			page: 2,
			pageSize: 25
		});
	});

	it('loads a user, the audit log, sessions, status, and reports', () => {
		getUser(identity, 'acc-1');
		expect(lastCall().path).toBe('/api/admin/users/acc-1');

		getAuditLog(identity, 2, 10, 'acc-1');
		expect(lastCall()).toMatchObject({
			path: '/api/admin/audit',
			options: { query: { targetId: 'acc-1', page: 2, pageSize: 10 } }
		});

		listSessions(identity, 'region-1', 'zone-1');
		expect(lastCall().options?.query).toEqual({ regionId: 'region-1', zoneId: 'zone-1' });

		getServerStatus(identity);
		expect(lastCall().path).toBe('/api/admin/status');

		listReports(identity, true, 1, 20);
		expect(lastCall().options?.query).toEqual({ resolved: true, page: 1, pageSize: 20 });
	});
});

describe('admin writes', () => {
	it('assigns roles and permissions with POST payloads', () => {
		assignRole(identity, 'acc-1', 'Moderator');
		expect(lastCall()).toMatchObject({
			path: '/api/admin/roles/users/acc-1/roles',
			options: { json: { role: 'Moderator' } }
		});

		grantPermission(identity, 'acc-1', 'players.ban');
		expect(lastCall()).toMatchObject({
			path: '/api/admin/roles/users/acc-1/permissions',
			options: { json: { permission: 'players.ban' } }
		});
	});

	it('revokes roles and permissions with encoded DELETE paths', () => {
		revokeRole(identity, 'acc-1', 'Game Master');
		expect(lastCall()).toMatchObject({
			path: '/api/admin/roles/users/acc-1/roles/Game%20Master',
			options: { method: 'DELETE' }
		});

		revokePermission(identity, 'acc-1', 'players.ban');
		expect(lastCall()).toMatchObject({
			path: '/api/admin/roles/users/acc-1/permissions/players.ban',
			options: { method: 'DELETE' }
		});
	});

	it('resolves a report with PUT', () => {
		resolveReport(identity, 'report-1');

		expect(lastCall()).toMatchObject({
			path: '/api/admin/reports/report-1/resolve',
			options: { method: 'PUT' }
		});
	});

	it('applies player actions with the documented payloads', () => {
		kickPlayer(identity, 'acc-1', 'spam');
		expect(lastCall().options?.json).toEqual({ accountId: 'acc-1', reason: 'spam' });

		banPlayer(identity, 'acc-1', 'cheating', 60);
		expect(lastCall()).toMatchObject({
			path: '/api/admin/players/ban',
			options: { json: { accountId: 'acc-1', reason: 'cheating', durationMinutes: 60 } }
		});

		unbanPlayer(identity, 'acc-1');
		expect(lastCall().options?.json).toEqual({ accountId: 'acc-1' });

		warnPlayer(identity, 'acc-1', 'spam');
		expect(lastCall().options?.json).toEqual({ accountId: 'acc-1', reason: 'spam' });

		mutePlayer(identity, 'acc-1', null, 30);
		expect(lastCall()).toMatchObject({
			path: '/api/admin/players/mute',
			options: { json: { accountId: 'acc-1', reason: null, durationMinutes: 30 } }
		});

		unmutePlayer(identity, 'acc-1');
		expect(lastCall().options?.json).toEqual({ accountId: 'acc-1' });
	});

	it('broadcasts with the default severity', () => {
		broadcastAnnouncement(identity, 'Restart in 5');

		expect(lastCall()).toMatchObject({
			path: '/api/admin/broadcast',
			options: { json: { message: 'Restart in 5', severity: 'info' } }
		});
	});
});

describe('moderation surface', () => {
	it('uses the read-only /api/mod endpoints', () => {
		listModUsers(identity, { search: 'aria' });
		expect(lastCall()).toMatchObject({
			path: '/api/mod/users',
			options: { query: { search: 'aria', page: 1, pageSize: 25 } }
		});

		getModUser(identity, 'acc-1');
		expect(lastCall().path).toBe('/api/mod/users/acc-1');

		listModReports(identity);
		expect(lastCall().path).toBe('/api/mod/reports');

		resolveModReport(identity, 'report-1');
		expect(lastCall()).toMatchObject({
			path: '/api/mod/reports/report-1/resolve',
			options: { method: 'PUT' }
		});
	});

	it('submits a player report through the shared endpoint', () => {
		submitReport(identity, 'troll', 'abuse');

		expect(lastCall()).toMatchObject({
			path: '/api/reports',
			options: { json: { targetUsername: 'troll', reason: 'abuse' } }
		});
	});
});
