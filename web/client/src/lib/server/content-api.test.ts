import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
	browseContent,
	createContent,
	deactivateContent,
	getContentDetail,
	getContentTypeSchema,
	listContentTypes,
	listZoneLocations,
	updateContent,
	validateContent,
	validateContentPayload
} from '#lib/server/content-api';

/**
 * Content reads are public; writes are RBAC-gated and concurrency-checked. The paths, verbs, and
 * query parameters are the contract with `ContentEndpoints`/`ContentWriteEndpoints`, and a wrong
 * one is a 404 or a silently unvalidated save rather than a type error.
 */

const gameApiFetch = vi.hoisted(() => vi.fn());

vi.mock('#lib/server/game-request', () => ({ gameApiFetch }));

const identity = { id: 'user-1', username: 'curator' };

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

describe('content reads', () => {
	it('lists the registered content types anonymously', () => {
		listContentTypes();

		expect(lastCall()).toEqual([null, '/api/content/schema', undefined]);
	});

	it('browses with the default paging window', () => {
		browseContent('item');

		expect(lastCall()).toEqual([
			null,
			'/api/content/browse',
			{ query: { type: 'item', search: null, page: 1, pageSize: 20 } }
		]);
	});

	it('browses with a search term and explicit paging', () => {
		browseContent('weapon', 'sword', 3, 10);

		expect(lastCall()[2]).toEqual({
			query: { type: 'weapon', search: 'sword', page: 3, pageSize: 10 }
		});
	});

	it('loads a detail and encodes both path segments', () => {
		getContentDetail('item', 'iron sword');

		expect(lastCall()[1]).toBe('/api/content/browse/item/iron%20sword');
	});

	it('lists the zone locations anonymously, for the map editor to narrow by zone', () => {
		listZoneLocations();

		expect(lastCall()).toEqual([null, '/api/content/zone-locations', undefined]);
	});

	it('loads the editable schema for a type', () => {
		getContentTypeSchema('zone location');

		expect(lastCall()).toEqual([null, '/api/content/zone%20location/schema', undefined]);
	});
});

describe('content writes', () => {
	it('validates a stored entity as the signed-in curator', () => {
		validateContent(identity, 'item', 'iron-sword');

		expect(lastCall()).toEqual([identity, '/api/content/item/iron-sword/validate', undefined]);
	});

	it('validates an unsaved payload by POSTing it to the same path', () => {
		validateContentPayload(identity, 'item', 'iron-sword', { displayName: 'Iron Sword' });

		expect(lastCall()).toEqual([
			identity,
			'/api/content/item/iron-sword/validate',
			{ method: 'POST', json: { displayName: 'Iron Sword' } }
		]);
	});

	it('creates an entity with POST', () => {
		createContent(identity, 'quest', { slug: 'rats' });

		expect(lastCall()).toEqual([
			identity,
			'/api/content/quest',
			{ method: 'POST', json: { slug: 'rats' } }
		]);
	});

	it('updates an entity with PUT and an optimistic-concurrency stamp', () => {
		updateContent(identity, 'quest', 'rats', { slug: 'rats' }, '2026-01-01T00:00:00Z');

		expect(lastCall()).toEqual([
			identity,
			'/api/content/quest/rats',
			{
				method: 'PUT',
				json: { slug: 'rats' },
				query: { expectedUpdatedAt: '2026-01-01T00:00:00Z' }
			}
		]);
	});

	it('deactivates an entity with DELETE', () => {
		deactivateContent(identity, 'quest', 'rats');

		expect(lastCall()).toEqual([identity, '/api/content/quest/rats', { method: 'DELETE' }]);
	});
});
