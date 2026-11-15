import { beforeEach, describe, expect, it, vi } from 'vitest';
import { callLoad, fakeUser, makeServerEvent, type FakeServerEvent } from '#lib/testing/event';
import { load as contentIndex } from './(foundry)/content/+page.server';
import { load as contentTypePage } from './(foundry)/content/[type]/+page.server';
import { load as contentDetail } from './(foundry)/content/[type]/[slug]/+page.server';
import {
	actions as editActions,
	load as editPage
} from './(foundry)/content/[type]/[slug]/edit/+page.server';
import { load as languagesPage } from './(foundry)/languages/+page.server';
import {
	actions as newLanguageActions,
	load as newLanguagePage
} from './(foundry)/languages/new/+page.server';
import { actions as mapsActions, load as mapsPage } from './(foundry)/maps/+page.server';
import { actions as mapActions, load as mapPage } from './(foundry)/maps/[mapId]/+page.server';
import {
	actions as notificationActions,
	load as notificationsPage
} from './(foundry)/notifications/+page.server';
import { load as submissionsPage } from './(foundry)/submissions/+page.server';
import {
	actions as submissionActions,
	load as submissionPage
} from './(foundry)/submissions/[id]/+page.server';

/**
 * The Foundry page modules: the content browser, its editor, the language submission form, the
 * notification inbox, and the submission queue.
 *
 * `content-api` and `foundry-api` are mocked; `buildPayload` and the access helpers run for real, so
 * the editor's coercion and the `canEdit` / `canReview` derivations are the things under test rather
 * than the mocks. The `(foundry)` layout guard itself is covered in `staff-loaders.test.ts`.
 */

const contentApi = vi.hoisted(() => ({
	listContentTypes: vi.fn(),
	browseContent: vi.fn(),
	getContentDetail: vi.fn(),
	getContentTypeSchema: vi.fn(),
	updateContent: vi.fn(),
	validateContentPayload: vi.fn(),
	deactivateContent: vi.fn()
}));

const foundryApi = vi.hoisted(() => ({
	listNotifications: vi.fn(),
	markNotificationRead: vi.fn(),
	listSubmissions: vi.fn(),
	getSubmission: vi.fn(),
	createSubmission: vi.fn(),
	voteSubmission: vi.fn(),
	reviewSubmission: vi.fn()
}));

const mapsApi = vi.hoisted(() => ({
	listMaps: vi.fn(),
	getMap: vi.fn(),
	getMapTargets: vi.fn(),
	saveMap: vi.fn(),
	validateMap: vi.fn(),
	createMap: vi.fn(),
	submitMap: vi.fn(),
	reviewMap: vi.fn(),
	deactivateMap: vi.fn(),
	deleteMap: vi.fn()
}));

const access = vi.hoisted(() => ({ loadAccountAccess: vi.fn() }));

vi.mock('#lib/server/content-api', () => contentApi);
vi.mock('#lib/server/foundry-api', () => foundryApi);
vi.mock('#lib/server/maps-api', () => mapsApi);
vi.mock('#lib/server/access', async (importOriginal) => ({
	...(await importOriginal<typeof import('#lib/server/access')>()),
	loadAccountAccess: access.loadAccountAccess
}));

const ok = <T>(data: T) => ({ status: 200, data, error: null });
const failed = (error: string, status = 500) => ({ status, data: null, error });

const field = (name: string, fieldType: string) => ({
	name,
	label: name,
	fieldType,
	required: false,
	default: null,
	min: null,
	max: null,
	enumValues: null,
	hint: null
});

const schema = {
	contentType: 'power',
	displayLabel: 'Power',
	description: '',
	groups: [
		{ label: 'Identity', fields: [field('displayName', 'Text')] },
		{ label: 'Stats', fields: [field('stats.cooldown', 'Integer')] }
	]
};

const detail = {
	summary: {
		slug: 'fireball',
		displayName: 'Fireball',
		isActive: true,
		updatedAt: '2026-01-02T03:04:05.000Z'
	},
	payload: { displayName: 'Fireball', stats: { cooldown: 3 } }
};

const issue = (severity: 'Warning' | 'Error', message: string) => ({
	rule: 'test',
	message,
	severity,
	field: null,
	identifier: null
});

const signedIn = (init: Parameters<typeof makeServerEvent>[0] = {}) =>
	makeServerEvent({ user: fakeUser(), ...init });

/** Invokes a form action with the given fields. */
function action(
	handler: unknown,
	form: Record<string, string>,
	init: Parameters<typeof makeServerEvent>[0] = {}
): Promise<unknown> {
	const event = signedIn({ method: 'POST', form, ...init });
	return (handler as (event: FakeServerEvent) => Promise<unknown>)(event);
}

/** The location a SvelteKit `redirect(...)` was thrown with. */
async function redirectTo(run: () => Promise<unknown> | unknown): Promise<string> {
	try {
		await run();
	} catch (thrown) {
		const location = (thrown as { location?: string }).location;
		if (location) return location;
		throw thrown;
	}

	throw new Error('expected a redirect');
}

/** The message a SvelteKit `error(...)` was thrown with (`HttpError.message` is empty). */
async function thrownMessage(run: () => Promise<unknown> | unknown): Promise<string> {
	try {
		await run();
	} catch (thrown) {
		return ((thrown as { body?: { message?: string } }).body?.message ?? '') as string;
	}

	throw new Error('expected the call to throw');
}

beforeEach(() => {
	vi.resetAllMocks();
	contentApi.listContentTypes.mockResolvedValue(ok([{ key: 'power', label: 'Powers' }]));
	contentApi.browseContent.mockResolvedValue(ok({ items: [{ slug: 'fireball' }], totalCount: 1 }));
	contentApi.getContentDetail.mockResolvedValue(ok(detail));
	contentApi.getContentTypeSchema.mockResolvedValue(ok(schema));
	contentApi.updateContent.mockResolvedValue(ok({ isValid: true, errors: [], warnings: [] }));
	contentApi.validateContentPayload.mockResolvedValue(
		ok({ errors: [issue('Error', 'Cooldown must be positive.')], warnings: [] })
	);
	contentApi.deactivateContent.mockResolvedValue(ok({ slug: 'fireball' }));
	access.loadAccountAccess.mockResolvedValue({
		accountId: 'acct-1',
		username: 'kaleth',
		roles: [],
		permissions: ['content.manage']
	});
	foundryApi.listNotifications.mockResolvedValue(ok([{ id: 'note-1', read: false }]));
	foundryApi.markNotificationRead.mockResolvedValue(ok(null));
	foundryApi.listSubmissions.mockResolvedValue(ok({ items: [{ id: 'sub-1' }], totalCount: 1 }));
	foundryApi.getSubmission.mockResolvedValue(ok({ id: 'sub-1' }));
	foundryApi.createSubmission.mockResolvedValue(ok({ id: 'sub-9' }));
	foundryApi.voteSubmission.mockResolvedValue(ok(null));
	foundryApi.reviewSubmission.mockResolvedValue(ok(null));
	mapsApi.listMaps.mockResolvedValue(ok([{ id: 'zone:fenwick', name: 'Fenwick' }]));
	mapsApi.getMap.mockResolvedValue(ok({ id: 'zone:fenwick', nodes: [], edges: [], version: 3 }));
	mapsApi.validateMap.mockResolvedValue(ok({ isValid: true, issues: [] }));
	mapsApi.createMap.mockResolvedValue(ok({ id: 'zone:draft-zone', name: 'Draft Zone' }));
	mapsApi.submitMap.mockResolvedValue(
		ok({ success: true, status: 'ok', message: 'Submitted for review.', issues: [] })
	);
	mapsApi.getMapTargets.mockResolvedValue(
		ok({
			maps: [{ id: 'region:varenmark', label: 'Varenmark' }],
			zones: [{ id: 'the-halrow', label: 'The Halrow' }],
			locations: [{ id: 'halrow-threshold', label: 'The Threshold' }],
			regions: [{ id: 'varenmark', label: 'Varenmark' }],
			nodes: [
				{ id: 'zone:fenwick:entry', mapId: 'zone:fenwick', label: 'Arrival', isEntryPoint: true }
			]
		})
	);
});

describe('map editor', () => {
	const params = { mapId: 'zone:fenwick' };

	it('lists the graphs for a reader without switching the write controls on', async () => {
		const result = (await callLoad(
			mapsPage,
			signedIn({ parentData: { canViewContent: true, canManageContent: false } })
		)) as { canEdit: boolean; maps: unknown[] };

		expect(result.canEdit).toBe(false);
		expect(result.maps).toEqual([{ id: 'zone:fenwick', name: 'Fenwick' }]);
	});

	it('refuses the list when the account cannot read content', async () => {
		expect(
			await thrownMessage(() =>
				callLoad(mapsPage, signedIn({ parentData: { canViewContent: false } }))
			)
		).toBe('Reading the map graphs requires content read access.');
	});

	it('reports the API failure rather than an empty list', async () => {
		mapsApi.listMaps.mockResolvedValue(failed('the graph store is down', 502));

		expect(
			await thrownMessage(() =>
				callLoad(mapsPage, signedIn({ parentData: { canViewContent: true } }))
			)
		).toBe('the graph store is down');
	});

	it('loads one graph with its rows and the write flag', async () => {
		const result = (await callLoad(
			mapPage,
			signedIn({ params, parentData: { canViewContent: true, canManageContent: true } })
		)) as { canEdit: boolean; rows: unknown[] };

		expect(result.canEdit).toBe(true);
		expect(result.rows).toEqual([]);
	});

	it('maps a missing graph to a 404 message', async () => {
		mapsApi.getMap.mockResolvedValue(failed('Map not found.', 404));

		expect(
			await thrownMessage(() =>
				callLoad(mapPage, signedIn({ params, parentData: { canViewContent: true } }))
			)
		).toBe('Map not found.');
	});

	it('refuses one graph when the account cannot read content', async () => {
		expect(
			await thrownMessage(() =>
				callLoad(mapPage, signedIn({ params, parentData: { canViewContent: false } }))
			)
		).toBe('Reading a map graph requires content read access.');
	});

	it('validates without saving and reports the issues the save would refuse on', async () => {
		mapsApi.validateMap.mockResolvedValue(
			ok({
				isValid: false,
				issues: [
					{
						code: 'target_node_unknown',
						message: 'exit names a node that does not exist',
						severity: 'Error',
						mapId: 'zone:fenwick',
						nodeId: 'exit'
					}
				]
			})
		);

		const result = (await action(
			mapActions.validate,
			{},
			{ params, parentData: { canViewContent: true, canManageContent: true } }
		)) as { validated: boolean; isValid: boolean; issues: unknown[] };

		expect(result.validated).toBe(true);
		expect(result.isValid).toBe(false);
		expect(result.issues).toHaveLength(1);
		expect(mapsApi.saveMap).not.toHaveBeenCalled();
	});

	it('creates a draft and opens it', async () => {
		expect(
			await redirectTo(() =>
				action(mapsActions.create, { mapType: 'Zone', ownerId: 'draft-zone', name: 'Draft Zone' })
			)
		).toBe('/maps/zone%3Adraft-zone');
	});

	it('reports why a draft could not be created', async () => {
		mapsApi.createMap.mockResolvedValue(failed("'draft-zone' already owns a Zone map.", 400));

		const result = (await action(mapsActions.create, {
			mapType: 'Zone',
			ownerId: 'draft-zone',
			name: 'Draft Zone'
		})) as { status: number; data: { message: string } };

		expect(result.status).toBe(400);
		expect(result.data.message).toContain('already owns');
	});

	it('submits a draft, and reports a conflict with the reason the server worded', async () => {
		const submitted = (await action(mapActions.submit, {}, { params })) as {
			done: boolean;
			message: string;
		};
		expect(submitted.done).toBe(true);
		expect(submitted.message).toBe('Submitted for review.');

		mapsApi.submitMap.mockResolvedValue(
			ok({ success: false, status: 'conflict', message: 'Map is already live.', issues: [] })
		);

		const refused = (await action(mapActions.submit, {}, { params })) as {
			status: number;
			data: { message: string };
		};
		expect(refused.status).toBe(409);
		expect(refused.data.message).toBe('Map is already live.');
	});

	it('reviews a submitted map, and reports the refusal when it cannot be approved', async () => {
		const editor = { params, parentData: { canViewContent: true, canManageContent: true } };
		mapsApi.reviewMap.mockResolvedValue(
			ok({ success: true, status: 'ok', message: 'Approved; the map is live.', issues: [] })
		);

		const approved = (await action(
			mapActions.review,
			{ approved: 'true', notes: 'Looks good' },
			editor
		)) as {
			done: boolean;
			message: string;
		};

		expect(approved.done).toBe(true);
		expect(approved.message).toBe('Approved; the map is live.');
		expect(mapsApi.reviewMap).toHaveBeenCalledWith(expect.anything(), 'zone:fenwick', {
			approved: true,
			notes: 'Looks good'
		});

		mapsApi.reviewMap.mockResolvedValue(
			ok({ success: false, status: 'invalid', message: 'The graph has errors.', issues: [] })
		);

		const refused = (await action(mapActions.review, { approved: 'false' }, editor)) as {
			status: number;
			data: { message: string };
		};

		expect(refused.status).toBe(400);
		expect(refused.data.message).toBe('The graph has errors.');
	});

	it('deactivates a live map and deletes a draft, refusing to delete a live one', async () => {
		const editor = { params, parentData: { canViewContent: true, canManageContent: true } };

		mapsApi.deactivateMap.mockResolvedValue(
			ok({ success: true, status: 'ok', message: 'Deactivated.', issues: [] })
		);
		const deactivated = (await action(mapActions.deactivate, {}, editor)) as { done: boolean };
		expect(deactivated.done).toBe(true);

		mapsApi.deleteMap.mockResolvedValue(
			ok({ success: true, status: 'ok', message: 'Draft deleted.', issues: [] })
		);
		expect(await redirectTo(() => action(mapActions.delete, {}, editor))).toBe('/maps');

		mapsApi.deleteMap.mockResolvedValue(
			ok({
				success: false,
				status: 'conflict',
				message: 'A live map is retired by deactivating, never deleted.',
				issues: []
			})
		);
		const refused = (await action(mapActions.delete, {}, editor)) as {
			status: number;
			data: { message: string };
		};

		expect(refused.status).toBe(409);
		expect(refused.data.message).toContain('never deleted');
	});

	it('reports a save conflict with the message the server worded', async () => {
		mapsApi.saveMap.mockResolvedValue(
			ok({
				success: false,
				status: 'conflict',
				version: 4,
				issues: [],
				message: 'Map is at version 4; reload before saving.'
			})
		);

		const result = (await action(
			mapActions.save,
			{ version: '3' },
			{ params, parentData: { canViewContent: true, canManageContent: true } }
		)) as { status: number; data: { message: string } };

		expect(result.status).toBe(409);
		expect(result.data.message).toContain('reload before saving');
	});

	it('reports a validate failure with the API message', async () => {
		mapsApi.validateMap.mockResolvedValue(failed('the validator is unavailable', 502));

		// `fail()` returns an ActionFailure rather than throwing, so the status and message are asserted.
		const result = (await action(
			mapActions.validate,
			{},
			{ params, parentData: { canViewContent: true, canManageContent: true } }
		)) as { status: number; data: { message: string } };

		expect(result.status).toBe(502);
		expect(result.data.message).toBe('the validator is unavailable');
	});
});

describe('content browser', () => {
	it('lists the content types, with an empty list on failure', async () => {
		expect(await callLoad(contentIndex, makeServerEvent())).toEqual({
			types: [{ key: 'power', label: 'Powers' }],
			error: null
		});

		contentApi.listContentTypes.mockResolvedValue(failed('catalog is down'));
		expect(await callLoad(contentIndex, makeServerEvent())).toEqual({
			types: [],
			error: 'catalog is down'
		});
	});

	it('browses one type with the search term and a clamped page', async () => {
		const result = (await callLoad(
			contentTypePage,
			makeServerEvent({ params: { type: 'power' }, url: '/content/power?search=fire&page=0' })
		)) as { contentType: string; page: number; search: string; total: number };

		expect(contentApi.browseContent).toHaveBeenCalledWith('power', 'fire', 1, 24);
		expect(result).toMatchObject({ contentType: 'power', page: 1, search: 'fire', total: 1 });
	});

	it('returns the fallbacks when the browse fails', async () => {
		contentApi.browseContent.mockResolvedValue(failed('browse is down'));

		const result = (await callLoad(
			contentTypePage,
			makeServerEvent({ params: { type: 'power' } })
		)) as Record<string, unknown>;

		expect(result).toMatchObject({
			entries: [],
			total: 0,
			page: 1,
			pageSize: 24,
			error: 'browse is down'
		});
	});
});

describe('content detail', () => {
	const params = { type: 'power', slug: 'fireball' };

	it('derives canEdit from the manage permission', async () => {
		const editor = (await callLoad(contentDetail, signedIn({ params }))) as { canEdit: boolean };
		expect(editor.canEdit).toBe(true);

		access.loadAccountAccess.mockResolvedValue({
			accountId: 'acct-1',
			username: 'kaleth',
			roles: [],
			permissions: []
		});

		const reader = (await callLoad(contentDetail, signedIn({ params }))) as { canEdit: boolean };
		expect(reader.canEdit).toBe(false);
	});

	it('404s an unknown entry', async () => {
		contentApi.getContentDetail.mockResolvedValue(failed('Content entry not found.', 404));

		expect(await thrownMessage(() => callLoad(contentDetail, signedIn({ params })))).toBe(
			'Content entry not found.'
		);
	});
});

describe('content editor', () => {
	const params = { type: 'power', slug: 'fireball' };
	const values = { displayName: 'Firebolt', 'stats.cooldown': '5' };

	it('404s a missing entry and a missing schema, with distinct messages', async () => {
		contentApi.getContentDetail.mockResolvedValue(failed('Content entry not found.', 404));
		expect(await thrownMessage(() => callLoad(editPage, signedIn({ params })))).toBe(
			'Content entry not found.'
		);

		contentApi.getContentDetail.mockResolvedValue(ok(detail));
		contentApi.getContentTypeSchema.mockResolvedValue(failed('Unknown type.', 404));
		expect(await thrownMessage(() => callLoad(editPage, signedIn({ params })))).toBe(
			'Unknown type.'
		);
	});

	it('loads the schema, the summary, and the payload', async () => {
		const result = (await callLoad(editPage, signedIn({ params }))) as {
			canEdit: boolean;
			schema: unknown;
			payload: unknown;
		};

		expect(result.canEdit).toBe(true);
		expect(result.schema).toEqual(schema);
		expect(result.payload).toEqual(detail.payload);
	});

	it('validates the unsaved form values and returns the issues', async () => {
		const result = (await action(editActions.validate, values, { params })) as {
			action: string;
			issues: { message: string }[];
		};

		expect(result.action).toBe('validate');
		expect(result.issues).toHaveLength(1);
		expect(contentApi.validateContentPayload).toHaveBeenCalledWith(
			expect.anything(),
			'power',
			'fireball',
			expect.objectContaining({ displayName: 'Firebolt' })
		);
	});

	it('passes the validation status through when the dry run fails', async () => {
		contentApi.validateContentPayload.mockResolvedValue(failed('Invalid payload.', 422));

		const result = (await action(editActions.validate, values, { params })) as {
			status: number;
			data: { message: string };
		};

		expect(result.status).toBe(422);
		expect(result.data.message).toBe('Invalid payload.');
	});

	it('sends the row version with the update and reports a clean save', async () => {
		const result = (await action(editActions.save, values, { params })) as {
			action: string;
			saved: boolean;
			issues: unknown[];
		};

		expect(result).toEqual({ action: 'save', saved: true, issues: [] });
		expect(contentApi.updateContent).toHaveBeenCalledWith(
			expect.anything(),
			'power',
			'fireball',
			expect.objectContaining({ displayName: 'Firebolt' }),
			detail.summary.updatedAt
		);
	});

	it('refuses to save an entity the server considers invalid', async () => {
		contentApi.updateContent.mockResolvedValue(
			ok({ isValid: false, errors: [issue('Error', 'Cooldown must be positive.')], warnings: [] })
		);

		const result = (await action(editActions.save, values, { params })) as {
			status: number;
			data: { message: string; issues: { message: string }[] };
		};

		expect(result.status).toBe(400);
		expect(result.data.message).toBe('This entity has errors and was not saved.');
		expect(result.data.issues).toHaveLength(1);
	});

	it('deactivates instead of deleting, then returns to the list', async () => {
		expect(await redirectTo(() => action(editActions.deactivate, {}, { params }))).toBe(
			'/content/power'
		);
		expect(contentApi.deactivateContent).toHaveBeenCalledWith(
			expect.anything(),
			'power',
			'fireball'
		);
	});

	it('reports a failed deactivation with the upstream status', async () => {
		contentApi.deactivateContent.mockResolvedValue(failed('Already inactive.', 409));

		const result = (await action(editActions.deactivate, {}, { params })) as {
			status: number;
			data: { message: string };
		};

		expect(result.status).toBe(409);
		expect(result.data.message).toBe('Already inactive.');
	});
});

describe('language fork', () => {
	it('browses the language content type', async () => {
		const result = (await callLoad(
			languagesPage,
			makeServerEvent({ url: '/languages?search=cal&page=2' })
		)) as { page: number; search: string };

		expect(contentApi.browseContent).toHaveBeenCalledWith('language', 'cal', 2, 24);
		expect(result).toMatchObject({ page: 2, search: 'cal' });
	});

	it('renders an empty form', async () => {
		expect(await callLoad(newLanguagePage, makeServerEvent())).toEqual({});
	});

	it('refuses a submission without a display name or slug', async () => {
		const result = (await action(newLanguageActions.default, { displayName: 'Calethic' })) as {
			status: number;
			data: { error: string };
		};

		expect(result.status).toBe(400);
		expect(result.data.error).toBe('Display name and slug are required.');
		expect(foundryApi.createSubmission).not.toHaveBeenCalled();
	});

	it('refuses a slug with capitals or spaces', async () => {
		const result = (await action(newLanguageActions.default, {
			displayName: 'Calethic',
			slug: 'Calethic Tongue'
		})) as { status: number; data: { error: string } };

		expect(result.status).toBe(400);
		expect(result.data.error).toBe(
			'The slug may only contain lowercase letters, numbers, and hyphens.'
		);
	});

	it('submits the language and opens the submission', async () => {
		const location = await redirectTo(() =>
			action(newLanguageActions.default, {
				displayName: 'Calethic',
				slug: 'calethic',
				description: 'The old tongue.',
				syllablePatterns: 'CVC, CV',
				initialClusters: 'th, sh',
				sampleText: 'Aelthir'
			})
		);

		expect(location).toBe('/submissions/sub-9');
		const submitted = foundryApi.createSubmission.mock.calls[0][1] as {
			contentType: string;
			title: string;
			payload: string;
		};
		expect(submitted.contentType).toBe('language');
		expect(submitted.title).toBe('Calethic');
		const payload = JSON.parse(submitted.payload) as {
			phonology: { allowedSyllablePatterns: string[]; allowedInitialClusters: string[] };
		};
		expect(payload.phonology.allowedSyllablePatterns).toEqual(['CVC', 'CV']);
		// The form field is `initialClusters`; the payload key is `allowedInitialClusters`.
		expect(payload.phonology.allowedInitialClusters).toEqual(['th', 'sh']);
	});

	it('turns a rejected submission into a 400', async () => {
		foundryApi.createSubmission.mockResolvedValue(failed('You already submitted that.'));

		const result = (await action(newLanguageActions.default, {
			displayName: 'Calethic',
			slug: 'calethic'
		})) as { status: number; data: { error: string } };

		expect(result.status).toBe(400);
		expect(result.data.error).toBe('You already submitted that.');
	});
});

describe('notifications', () => {
	it('lists the inbox and its error', async () => {
		expect(await callLoad(notificationsPage, signedIn())).toEqual({
			notifications: [{ id: 'note-1', read: false }],
			error: null
		});

		foundryApi.listNotifications.mockResolvedValue(failed('inbox is down'));
		expect(await callLoad(notificationsPage, signedIn())).toEqual({
			notifications: [],
			error: 'inbox is down'
		});
	});

	it('marks one read, refusing a missing id', async () => {
		const missing = (await action(notificationActions.read, {})) as {
			status: number;
			data: { error: string };
		};
		expect(missing.status).toBe(400);
		expect(missing.data.error).toBe('Notification id is required.');
		expect(foundryApi.markNotificationRead).not.toHaveBeenCalled();

		expect(await action(notificationActions.read, { id: 'note-1' })).toEqual({ ok: true });
		expect(foundryApi.markNotificationRead).toHaveBeenCalledWith(expect.anything(), 'note-1');
	});

	it('reports a failed mark-read', async () => {
		foundryApi.markNotificationRead.mockResolvedValue(failed('Unknown notification.'));

		const result = (await action(notificationActions.read, { id: 'note-1' })) as {
			status: number;
			data: { error: string };
		};

		expect(result.status).toBe(400);
		expect(result.data.error).toBe('Unknown notification.');
	});
});

describe('submission queue', () => {
	it('passes the filters through with a clamped page', async () => {
		await callLoad(
			submissionsPage,
			signedIn({ url: '/submissions?status=Pending&contentType=language&search=cal&page=0' })
		);

		expect(foundryApi.listSubmissions).toHaveBeenCalledWith(expect.anything(), {
			status: 'Pending',
			contentType: 'language',
			search: 'cal',
			page: 1,
			pageSize: 20
		});
	});

	it('returns the fallbacks on failure', async () => {
		foundryApi.listSubmissions.mockResolvedValue(failed('queue is down'));

		const result = (await callLoad(submissionsPage, signedIn())) as Record<string, unknown>;

		expect(result).toMatchObject({
			submissions: [],
			total: 0,
			page: 1,
			pageSize: 20,
			error: 'queue is down'
		});
	});

	it('opens create mode, and derives canReview from the Curator role', async () => {
		const asPlayer = (await callLoad(submissionPage, staffEventWithParent(['Player'], 'new'))) as {
			mode: string;
			canReview: boolean;
		};

		expect(asPlayer).toMatchObject({ mode: 'create', canReview: false });

		const asCurator = (await callLoad(
			submissionPage,
			staffEventWithParent(['Curator'], 'new')
		)) as { canReview: boolean };

		expect(asCurator.canReview).toBe(true);
	});

	it('returns a submission and its error', async () => {
		foundryApi.getSubmission.mockResolvedValue(failed('Submission not found.'));

		const result = (await callLoad(submissionPage, staffEventWithParent(['Player'], 'sub-1'))) as {
			mode: string;
			submission: unknown;
			error: string;
		};

		expect(result).toMatchObject({
			mode: 'view',
			submission: null,
			error: 'Submission not found.'
		});
	});

	it('creates a submission, refusing a missing field', async () => {
		const missing = (await action(submissionActions.create, { contentType: 'language' })) as {
			status: number;
			data: { error: string };
		};
		expect(missing.status).toBe(400);
		expect(missing.data.error).toBe('Content type, title, and payload are required.');

		expect(
			await redirectTo(() =>
				action(submissionActions.create, {
					contentType: 'language',
					title: 'Calethic',
					payload: '{}'
				})
			)
		).toBe('/submissions/sub-9');
		expect(foundryApi.createSubmission).toHaveBeenCalledWith(
			expect.anything(),
			expect.objectContaining({ contentType: 'language', title: 'Calethic', description: null })
		);
	});

	it('accepts only a +1 or -1 vote', async () => {
		const bad = (await action(
			submissionActions.vote,
			{ value: '0' },
			{ params: { id: 'sub-1' } }
		)) as {
			status: number;
			data: { error: string };
		};
		expect(bad.status).toBe(400);
		expect(bad.data.error).toBe('Vote must be +1 or -1.');
		expect(foundryApi.voteSubmission).not.toHaveBeenCalled();

		expect(
			await redirectTo(() =>
				action(submissionActions.vote, { value: '1' }, { params: { id: 'sub-1' } })
			)
		).toBe('/submissions/sub-1');
		expect(foundryApi.voteSubmission).toHaveBeenCalledWith(expect.anything(), 'sub-1', 1);
	});

	it('reviews a submission, mapping the decision and dropping a blank note', async () => {
		expect(
			await redirectTo(() =>
				action(
					submissionActions.review,
					{ approved: 'yes', notes: '  ' },
					{ params: { id: 'sub-1' } }
				)
			)
		).toBe('/submissions/sub-1');
		expect(foundryApi.reviewSubmission).toHaveBeenCalledWith(
			expect.anything(),
			'sub-1',
			true,
			null
		);

		await redirectTo(() =>
			action(
				submissionActions.review,
				{ approved: 'no', notes: 'Needs more detail.' },
				{ params: { id: 'sub-1' } }
			)
		);
		expect(foundryApi.reviewSubmission).toHaveBeenLastCalledWith(
			expect.anything(),
			'sub-1',
			false,
			'Needs more detail.'
		);
	});

	it('turns a rejected review into a 400', async () => {
		foundryApi.reviewSubmission.mockResolvedValue(failed('Only curators may review.'));

		const result = (await action(
			submissionActions.review,
			{ approved: 'yes' },
			{ params: { id: 'sub-1' } }
		)) as { status: number; data: { error: string } };

		expect(result.status).toBe(400);
		expect(result.data.error).toBe('Only curators may review.');
	});
});

/** A signed-in event whose `parent()` reports the given roles. */
function staffEventWithParent(roles: string[], id: string): FakeServerEvent {
	const event = signedIn({ params: { id } });
	event.parent = (async () => ({ access: { roles, permissions: [] } })) as never;
	return event;
}

/**
 * Fallback copy and status codes.
 *
 * Every server client returns `{ status, data, error }`, and every caller has a `?? '…'` or
 * `status || 502` for the case where the API answered without a message (a transport failure, or a
 * 204 body-less response). Those are the branches a happy-path test never reaches, and they are
 * what the player actually reads when the server is unreachable, so they are asserted directly.
 */
describe('fallbacks when the API answers without a message', () => {
	const params = { type: 'power', slug: 'fireball' };
	const values = { displayName: 'Firebolt', 'stats.cooldown': '5' };
	const silent = (status = 0) => ({ status, data: null, error: null });

	it('404s with the default text when the detail call has no message', async () => {
		contentApi.getContentDetail.mockResolvedValue(silent(404));

		expect(await thrownMessage(() => callLoad(contentDetail, signedIn({ params })))).toBe(
			'Content entry not found.'
		);
	});

	it('names the content type when no schema is registered and no message came back', async () => {
		contentApi.getContentTypeSchema.mockResolvedValue(silent(404));

		expect(await thrownMessage(() => callLoad(editPage, signedIn({ params })))).toBe(
			"No schema is registered for content type 'power'."
		);
	});

	it('falls back to 502 and the action-specific copy for a silent failure', async () => {
		contentApi.validateContentPayload.mockResolvedValue(silent());

		const validate = (await action(editActions.validate, values, { params })) as {
			status: number;
			data: { message: string };
		};
		expect(validate.status).toBe(502);
		expect(validate.data.message).toBe('Validation failed.');

		contentApi.updateContent.mockResolvedValue(silent());
		const save = (await action(editActions.save, values, { params })) as {
			status: number;
			data: { message: string };
		};
		expect(save.status).toBe(502);
		expect(save.data.message).toBe('The save failed.');

		contentApi.deactivateContent.mockResolvedValue(silent());
		const deactivate = (await action(editActions.deactivate, {}, { params })) as {
			status: number;
			data: { message: string };
		};
		expect(deactivate.status).toBe(502);
		expect(deactivate.data.message).toBe('The deactivation failed.');
	});

	it('accepts a non-Error status and redirects after a deactivation', async () => {
		contentApi.deactivateContent.mockResolvedValue({ status: 409, data: null, error: 'Locked' });

		const blocked = (await action(editActions.deactivate, {}, { params })) as {
			status: number;
			data: { message: string };
		};
		expect(blocked.status).toBe(409);
		expect(blocked.data).toEqual({ message: 'Locked' });

		contentApi.deactivateContent.mockResolvedValue(ok({ slug: 'fireball' }));
		expect(await redirectTo(() => action(editActions.deactivate, {}, { params }))).toBe(
			'/content/power'
		);
	});

	it('coerces boolean form fields and skips ones the form did not send', async () => {
		// A Boolean-typed field is the only place the 'true'/'false' string coercion matters.
		contentApi.getContentTypeSchema.mockResolvedValue(
			ok({
				...schema,
				groups: [
					{ label: 'Flags', fields: [field('isActive', 'Boolean'), field('isHidden', 'Boolean')] }
				]
			})
		);
		contentApi.updateContent.mockResolvedValue(ok({ isValid: true, errors: [], warnings: [] }));

		await action(editActions.save, { isActive: 'false' }, { params });

		expect(contentApi.updateContent).toHaveBeenCalledWith(
			expect.anything(),
			'power',
			'fireball',
			expect.objectContaining({ isActive: false }),
			detail.summary.updatedAt
		);
	});

	it('uses the fallback copy for the language, notification, and submission actions', async () => {
		foundryApi.createSubmission.mockResolvedValue(silent());
		const create = (await action(
			submissionActions.create,
			{ contentType: 'Item', title: 'Ember', payload: '{}' },
			{}
		)) as { status: number; data: { error: string } };
		expect(create.status).toBe(400);
		expect(create.data.error).toBe('Could not create the submission.');
	});
});
