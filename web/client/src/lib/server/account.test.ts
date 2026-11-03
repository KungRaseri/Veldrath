import { afterEach, describe, expect, it, vi } from 'vitest';
import { listLinkedProviders } from '#lib/server/account';

/**
 * Provider links come from Better Auth's own tables, not from the .NET account row. The query is
 * mocked here because the point is the shape handed to the account page, not the SQL.
 */

const rows = vi.hoisted(() => ({ value: [] as unknown[] }));
const select = vi.hoisted(() => vi.fn());

vi.mock('#lib/server/db', () => ({ getAuthDb: () => ({ select }) }));

afterEach(() => {
	select.mockReset();
	rows.value = [];
});

describe('listLinkedProviders', () => {
	it('maps provider rows to ISO timestamps', async () => {
		rows.value = [
			{
				id: 'acc-1',
				providerId: 'discord',
				createdAt: new Date('2026-01-02T03:04:05.000Z')
			},
			{ id: 'acc-2', providerId: 'google', createdAt: new Date('2026-02-03T00:00:00.000Z') }
		];
		select.mockReturnValue({ from: () => ({ where: async () => rows.value }) });

		await expect(listLinkedProviders('user-1')).resolves.toEqual([
			{ id: 'acc-1', providerId: 'discord', createdAt: '2026-01-02T03:04:05.000Z' },
			{ id: 'acc-2', providerId: 'google', createdAt: '2026-02-03T00:00:00.000Z' }
		]);
		expect(select).toHaveBeenCalledWith({
			id: expect.anything(),
			providerId: expect.anything(),
			createdAt: expect.anything()
		});
	});

	it('returns an empty list when nothing is linked', async () => {
		select.mockReturnValue({ from: () => ({ where: async () => rows.value }) });

		await expect(listLinkedProviders('user-1')).resolves.toEqual([]);
	});
});
