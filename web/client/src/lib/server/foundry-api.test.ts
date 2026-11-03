import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
	createSubmission,
	getSubmission,
	listNotifications,
	listSubmissions,
	markNotificationRead,
	reviewSubmission,
	voteSubmission
} from '#lib/server/foundry-api';

/**
 * Foundry submissions are the community content path: a wrong verb here means a vote that does
 * nothing or a review that never reaches the curator queue.
 */

const gameApiFetch = vi.hoisted(() => vi.fn());

vi.mock('#lib/server/game-request', () => ({ gameApiFetch }));

const identity = { id: 'user-1', username: 'tester' };

beforeEach(() => {
	gameApiFetch.mockResolvedValue({ status: 200, data: {}, error: null });
});

afterEach(() => {
	gameApiFetch.mockReset();
});

/** Last recorded call, padded to three entries so `options` can be asserted as `undefined`. */
const lastCall = () => {
	const call = gameApiFetch.mock.calls.at(-1) as unknown[];
	return [call[0], call[1], call[2]] as [unknown, string, unknown];
};

describe('submission reads', () => {
	it('lists submissions with the default window and empty filters', () => {
		listSubmissions(identity);

		expect(lastCall()).toEqual([
			identity,
			'/api/foundry/submissions',
			{
				query: {
					status: undefined,
					contentType: undefined,
					search: undefined,
					page: 1,
					pageSize: 20
				}
			}
		]);
	});

	it('passes filters through, including paging', () => {
		listSubmissions(identity, { status: 'Pending', contentType: 'item', search: 'sword', page: 2 });

		expect(lastCall()[2]).toEqual({
			query: { status: 'Pending', contentType: 'item', search: 'sword', page: 2, pageSize: 20 }
		});
	});

	it('allows an anonymous browse of the public list', () => {
		listSubmissions(null, { pageSize: 5 });

		expect(lastCall()[0]).toBeNull();
		expect((lastCall()[2] as { query: { pageSize: number } }).query.pageSize).toBe(5);
	});

	it('loads one submission by id', () => {
		getSubmission(identity, 'sub-1');

		expect(lastCall()).toEqual([identity, '/api/foundry/submissions/sub-1', undefined]);
	});
});

describe('submission writes', () => {
	it('creates a submission with POST', () => {
		const input = { contentType: 'item', title: 'Iron Sword', payload: '{}' };
		createSubmission(identity, input);

		expect(lastCall()).toEqual([identity, '/api/foundry/submissions', { json: input }]);
	});

	it('votes with a signed value', () => {
		voteSubmission(identity, 'sub-1', -1);

		expect(lastCall()).toEqual([
			identity,
			'/api/foundry/submissions/sub-1/vote',
			{ json: { value: -1 } }
		]);
	});

	it('reviews with notes', () => {
		reviewSubmission(identity, 'sub-1', true, 'Looks good');

		expect(lastCall()).toEqual([
			identity,
			'/api/foundry/submissions/sub-1/review',
			{ json: { approved: true, notes: 'Looks good' } }
		]);
	});

	it('reviews without notes', () => {
		reviewSubmission(identity, 'sub-1', false);

		expect(lastCall()[2]).toEqual({ json: { approved: false, notes: undefined } });
	});
});

describe('notifications', () => {
	it('lists the signed-in user notifications', () => {
		listNotifications(identity);

		expect(lastCall()).toEqual([identity, '/api/foundry/notifications', undefined]);
	});

	it('marks one notification read with POST', () => {
		markNotificationRead(identity, 'note-1');

		expect(lastCall()).toEqual([
			identity,
			'/api/foundry/notifications/note-1/read',
			{ method: 'POST' }
		]);
	});
});
