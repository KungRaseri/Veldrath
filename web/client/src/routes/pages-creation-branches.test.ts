import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { goto, resetNavigation } from '#lib/testing/app-navigation';
import { callLoad, fakeUser, makeServerEvent } from '#lib/testing/event';
import { installFetch, jsonResponse, textResponse, type FetchCall } from '#lib/testing/fetch';
import CreationPage from './characters/new/+page.svelte';
import { load as newCharacterLoad } from './characters/new/+page.server';

/**
 * Second pass over the character creation wizard.
 *
 * `pages-creation.test.ts` drives the happy path and the two headline failures. This file settles the
 * quieter decisions: the defaults a sparse session leaves behind, the name-check race guard, one
 * rejection per step, the copy shown when the server sends no reason, and the review fields that
 * fall back to a dash or an empty string when the preview omits them. The `/characters/new` load is
 * asserted here too, because its slot guard is the only thing that sends a player away from the
 * wizard before it starts.
 */

const characterApi = vi.hoisted(() => ({
	fetchCharacterSlots: vi.fn(),
	fetchCreationCatalog: vi.fn()
}));

vi.mock('#lib/server/character-api', () => characterApi);

const catalog = {
	classes: [
		{ slug: 'warrior', displayName: 'Warrior' },
		{ slug: 'mage', displayName: 'Mage', description: 'Weaves the arcane.' }
	],
	species: [
		{ slug: 'human', displayName: 'Human', description: 'Adaptable.' },
		{ slug: 'elf', displayName: 'Elf' }
	],
	backgrounds: [
		{ slug: 'noble', displayName: 'Noble' },
		{ slug: 'scholar', displayName: 'Scholar', description: 'Studied the old tongues.' }
	]
};

const session = {
	sessionId: 's1',
	pointBuyConfig: {
		totalPoints: 10,
		minStatValue: 8,
		maxStatValue: 15,
		costTable: { '8': 0, '9': 1, '10': 2, '11': 3, '12': 4, '13': 5, '14': 6, '15': 7 }
	},
	equipmentTypeCatalog: {
		armorTypes: [{ slug: 'light', displayName: 'Light armor' }],
		weaponTypes: [{ slug: 'sword', displayName: 'Sword' }]
	}
};

const preview = {
	name: 'Kaleth',
	className: 'Warrior',
	speciesName: 'Human',
	backgroundName: 'Noble',
	health: 120,
	mana: 40,
	strength: 9,
	dexterity: 8,
	constitution: 8,
	intelligence: 8,
	wisdom: 8,
	charisma: 8
};

type Route = () => Response | Promise<Response>;

function routes(overrides: Record<string, Route> = {}): Record<string, Route> {
	return {
		'POST /api/characters/creation': () => jsonResponse(session),
		'GET /api/characters/creation/check-name': () => jsonResponse({ available: true }),
		'PATCH /api/characters/creation/s1/name': () => jsonResponse({ ok: true }),
		'PATCH /api/characters/creation/s1/class': () => jsonResponse({ ok: true }),
		'PATCH /api/characters/creation/s1/species': () => jsonResponse({ ok: true }),
		'PATCH /api/characters/creation/s1/background': () => jsonResponse({ ok: true }),
		'PATCH /api/characters/creation/s1/attributes': () => jsonResponse({ ok: true }),
		'PATCH /api/characters/creation/s1/equipment': () => jsonResponse({ ok: true }),
		'GET /api/characters/creation/s1/preview': () => jsonResponse(preview),
		'POST /api/characters/creation/s1/finalize': () => jsonResponse({ id: 'char-1' }),
		'DELETE /api/characters/creation/s1': () => new Response(null, { status: 204 }),
		...overrides
	};
}

const stubs: Array<{ restore(): void }> = [];

function useStub(overrides: Record<string, Route> = {}) {
	const table = routes(overrides);
	const stub = installFetch((call: FetchCall) => {
		const key = `${call.method} ${new URL(call.url, 'http://localhost').pathname}`;
		const handler = table[key];
		if (!handler) throw new Error(`unexpected fetch ${key}`);
		return handler();
	});
	stubs.push(stub);
	return stub;
}

function renderWizard(data: Record<string, unknown> = {}) {
	return render(CreationPage, {
		data: {
			catalog,
			catalogError: null,
			slots: { used: 1, max: 5, remaining: 4 },
			slotsError: null,
			...data
		} as never
	});
}

/** The one button that moves the wizard forwards, whatever it currently says. */
const continueButton = () =>
	screen.getByRole('button', { name: /Continue|Create character|Saving/ });

/** The session is live once the Cancel button exists, which only happens after `begin()` resolves. */
const started = () => screen.findByRole('button', { name: /Cancel/ });

function nameInput(container: HTMLElement): HTMLInputElement {
	return container.querySelector('input[type="text"]') as HTMLInputElement;
}

/** Types a name and waits for the debounced availability check to come back. */
async function nameAccepted(container: HTMLElement, value = 'Kaleth'): Promise<void> {
	await started();
	await fireEvent.input(nameInput(container), { target: { value } });
	await screen.findByText('That name is available.');
}

/** Presses Continue and waits for the step header of the hop to render. */
async function advanceTo(label: string): Promise<void> {
	await fireEvent.click(continueButton());
	await screen.findByText(new RegExp(`· ${label}$`));
}

/** Walks the default choices from the name step to the equipment step. */
async function toEquipment(container: HTMLElement): Promise<void> {
	await nameAccepted(container);
	await advanceTo('Class');
	await advanceTo('Species');
	await advanceTo('Background');
	await advanceTo('Attributes');
	await advanceTo('Equipment');
}

afterEach(() => {
	for (const stub of stubs.splice(0)) stub.restore();
	resetNavigation();
	vi.restoreAllMocks();
});

describe('sparse creation data', () => {
	it('assumes blank defaults when the catalog, the point-buy config and the equipment types are missing', async () => {
		const stub = useStub({
			'POST /api/characters/creation': () =>
				jsonResponse({ sessionId: 's1', pointBuyConfig: null, equipmentTypeCatalog: null })
		});

		const { container } = renderWizard({
			catalog: { classes: [], species: [], backgrounds: [] }
		});
		await started();
		await nameAccepted(container);

		await advanceTo('Class');
		expect(container.querySelectorAll('button[aria-pressed]')).toHaveLength(0);

		await advanceTo('Species');
		await advanceTo('Background');
		await advanceTo('Attributes');

		// No config means no budget to describe and no way to spend it.
		expect(screen.getByText(/Points remaining:/)).toBeTruthy();
		expect(document.body.textContent).not.toContain('per stat)');
		expect(screen.getByRole('button', { name: 'Increase Strength' })).toHaveProperty(
			'disabled',
			true
		);

		await advanceTo('Equipment');
		// "No preference" on each select, and nothing after it.
		expect(container.querySelectorAll('select option')).toHaveLength(2);

		await advanceTo('Review');

		const sent = JSON.parse(
			stub.calls.find((call) => call.url.includes('/equipment'))?.body ?? '{}'
		);
		expect(sent).toEqual({
			preferredArmorType: null,
			preferredWeaponType: null,
			includeShield: false
		});
	});

	it('serves equipment options that carry no slug as blank values', async () => {
		useStub({
			'POST /api/characters/creation': () =>
				jsonResponse({
					...session,
					equipmentTypeCatalog: { armorTypes: [{}], weaponTypes: [{}] }
				})
		});

		const { container } = renderWizard();
		await started();
		await toEquipment(container);

		const selects = container.querySelectorAll('select');
		expect(selects).toHaveLength(2);
		// "No preference" plus the one slug-less entry, whose value degrades to blank.
		expect(selects[0].querySelectorAll('option')).toHaveLength(2);
		expect(selects[0].querySelectorAll('option')[1]?.getAttribute('value')).toBe('');
	});

	it('drops the slot counter when the load has no slot data', async () => {
		useStub();
		renderWizard({ slots: null });

		await started();

		expect(screen.queryByText(/slots used/)).toBeNull();
		expect(screen.getByText('Create your character')).toBeTruthy();
	});

	it('tolerates a slot record that omits the used and max figures', async () => {
		useStub();
		renderWizard({ slots: { remaining: 4 } });

		await started();

		expect(screen.getByText(/slots used/)).toBeTruthy();
	});
});

describe('the availability check', () => {
	it('ignores a verdict that arrives after a newer check was started', async () => {
		let releaseFirst: (response: Response) => void = () => {};
		const firstAnswer = new Promise<Response>((resolve) => {
			releaseFirst = resolve;
		});
		let asked = 0;
		useStub({
			'GET /api/characters/creation/check-name': () => {
				asked += 1;
				return asked === 1 ? firstAnswer : jsonResponse({ available: true });
			}
		});

		const { container } = renderWizard();
		await started();

		await fireEvent.input(nameInput(container), { target: { value: 'Kaleth' } });
		// Long enough for the debounce to fire the first, still-unanswered request.
		await new Promise((resolve) => setTimeout(resolve, 400));
		await fireEvent.input(nameInput(container), { target: { value: 'Kale' } });
		await screen.findByText('That name is available.');

		releaseFirst(jsonResponse({ available: false, error: 'Stale verdict.' }));
		await new Promise((resolve) => setTimeout(resolve, 20));

		expect(screen.queryByText('Stale verdict.')).toBeNull();
		expect(screen.getByText('That name is available.')).toBeTruthy();
	});

	it('ignores a failure from a check that has been superseded', async () => {
		let failFirst: (error: Error) => void = () => {};
		const firstAnswer = new Promise<Response>((_resolve, reject) => {
			failFirst = reject;
		});
		let asked = 0;
		useStub({
			'GET /api/characters/creation/check-name': () => {
				asked += 1;
				return asked === 1 ? firstAnswer : jsonResponse({ available: true });
			}
		});

		const { container } = renderWizard();
		await started();

		await fireEvent.input(nameInput(container), { target: { value: 'Kaleth' } });
		await new Promise((resolve) => setTimeout(resolve, 400));
		await fireEvent.input(nameInput(container), { target: { value: 'Kale' } });
		await screen.findByText('That name is available.');

		failFirst(new TypeError('Failed to fetch'));
		await new Promise((resolve) => setTimeout(resolve, 20));

		expect(screen.queryByText('Could not check that name. Try again.')).toBeNull();
		expect(screen.getByText('That name is available.')).toBeTruthy();
	});

	it('uses its own wording when a check denies the name without a reason', async () => {
		useStub({
			'GET /api/characters/creation/check-name': () => jsonResponse({ available: false })
		});

		const { container } = renderWizard();
		await started();

		await fireEvent.input(nameInput(container), { target: { value: 'Kaleth' } });

		expect(await screen.findByText('That name is not available.')).toBeTruthy();
	});

	it('refuses to advance without an available name and says which rule failed', async () => {
		const stub = useStub();
		const { container } = renderWizard();
		await started();

		// Continue is disabled here, but the handler still has to decide.
		await fireEvent.click(continueButton());
		expect(screen.getByText('Choose an available character name.')).toBeTruthy();

		await fireEvent.input(nameInput(container), { target: { value: 'K1!' } });
		await fireEvent.click(continueButton());

		// The name field and the step banner both show the validation message.
		expect(screen.getAllByText('Name may only contain letters and spaces.')).toHaveLength(2);
		expect(stub.calls.filter((call) => call.method === 'PATCH')).toHaveLength(0);
	});
});

describe('selection defaults', () => {
	it('records a species without a description and a background with one', async () => {
		const stub = useStub();
		const { container } = renderWizard();

		await nameAccepted(container);
		await advanceTo('Class');
		await advanceTo('Species');

		const elf = screen.getByRole('button', { name: /Elf/ });
		await fireEvent.click(elf);
		expect(elf.getAttribute('aria-pressed')).toBe('true');
		expect(screen.getByRole('button', { name: /Human/ }).getAttribute('aria-pressed')).toBe(
			'false'
		);

		await advanceTo('Background');
		const scholar = screen.getByRole('button', { name: /Scholar/ });
		await fireEvent.click(scholar);
		expect(scholar.getAttribute('aria-pressed')).toBe('true');

		await advanceTo('Attributes');

		const patches = stub.calls.filter((call) => call.method === 'PATCH');
		expect(JSON.parse(patches[2].body ?? '{}')).toEqual({ speciesSlug: 'elf' });
		expect(JSON.parse(patches[3].body ?? '{}')).toEqual({ backgroundId: 'scholar' });
	});
});

describe('rejected steps', () => {
	it('keeps the player on the name step when the name cannot be saved', async () => {
		useStub({
			'PATCH /api/characters/creation/s1/name': () => textResponse('nope', { status: 400 })
		});

		const { container } = renderWizard();
		await nameAccepted(container);
		await fireEvent.click(continueButton());

		expect(await screen.findByText('The server rejected that choice.')).toBeTruthy();
		expect(screen.getByText('Step 1 of 7 · Name')).toBeTruthy();
	});

	it('reports a rejected species save', async () => {
		useStub({
			'PATCH /api/characters/creation/s1/species': () => textResponse('nope', { status: 400 })
		});

		const { container } = renderWizard();
		await nameAccepted(container);
		await advanceTo('Class');
		await advanceTo('Species');
		await fireEvent.click(continueButton());

		expect(await screen.findByText('The server rejected that choice.')).toBeTruthy();
		expect(screen.getByText('Step 3 of 7 · Species')).toBeTruthy();
	});

	it('reports a rejected attribute allocation', async () => {
		useStub({
			'PATCH /api/characters/creation/s1/attributes': () => textResponse('nope', { status: 400 })
		});

		const { container } = renderWizard();
		await nameAccepted(container);
		await advanceTo('Class');
		await advanceTo('Species');
		await advanceTo('Background');
		await advanceTo('Attributes');
		await fireEvent.click(continueButton());

		expect(await screen.findByText('The server rejected that choice.')).toBeTruthy();
		expect(screen.getByText('Step 5 of 7 · Attributes')).toBeTruthy();
	});

	it('reports rejected equipment preferences', async () => {
		useStub({
			'PATCH /api/characters/creation/s1/equipment': () => textResponse('nope', { status: 400 })
		});

		const { container } = renderWizard();
		await toEquipment(container);
		await fireEvent.click(continueButton());

		expect(await screen.findByText('The server rejected that choice.')).toBeTruthy();
		expect(screen.getByText('Step 6 of 7 · Equipment')).toBeTruthy();
	});

	it('reports a creation that failed without a reason', async () => {
		useStub({
			'POST /api/characters/creation/s1/finalize': () => textResponse('boom', { status: 500 })
		});

		const { container } = renderWizard();
		await toEquipment(container);
		await advanceTo('Review');
		await fireEvent.click(continueButton());

		expect(await screen.findByText('Could not create the character.')).toBeTruthy();
		expect(goto).not.toHaveBeenCalled();
	});
});

describe('the review step', () => {
	it('shows a dash for every preview field the server left out', async () => {
		useStub({ 'GET /api/characters/creation/s1/preview': () => jsonResponse({}) });

		const { container } = renderWizard();
		await toEquipment(container);
		await advanceTo('Review');

		await waitFor(() => expect(screen.getAllByText('—')).toHaveLength(3));
		expect(document.body.textContent).toContain('STR');
	});

	it('keeps the loading message when the preview fails without a reason', async () => {
		useStub({
			'GET /api/characters/creation/s1/preview': () => textResponse('down', { status: 502 })
		});

		const { container } = renderWizard();
		await toEquipment(container);
		await advanceTo('Review');

		expect(await screen.findByText('Could not load the preview.')).toBeTruthy();
		expect(screen.getByText('Loading preview…')).toBeTruthy();
	});

	it('reports the missing session when the wizard is driven past the end', async () => {
		const stub = useStub();
		const { container } = renderWizard();
		await toEquipment(container);
		await advanceTo('Review');

		await fireEvent.click(continueButton());
		await waitFor(() => expect(goto).toHaveBeenCalled());
		await waitFor(() => expect(continueButton()).toHaveProperty('disabled', false));

		// The session was consumed by the finalize, so there is nothing left to advance.
		await fireEvent.click(continueButton());
		expect(
			await screen.findByText('No active creation session. Cancel and start again.')
		).toBeTruthy();

		await fireEvent.click(screen.getByRole('button', { name: /Cancel/ }));
		await waitFor(() => expect(String(goto.mock.calls.at(-1)?.[0])).toBe('/characters'));
		expect(stub.calls.some((call) => call.method === 'DELETE')).toBe(false);
	});
});

describe('/characters/new load', () => {
	const user = fakeUser();

	beforeEach(() => {
		characterApi.fetchCreationCatalog.mockResolvedValue({ catalog, error: null });
		characterApi.fetchCharacterSlots.mockResolvedValue({
			status: 200,
			data: { used: 1, max: 5, remaining: 4 },
			error: null
		});
	});

	it('sends a player with no slots left back to the roster', async () => {
		characterApi.fetchCharacterSlots.mockResolvedValue({
			status: 200,
			data: { used: 5, max: 5, remaining: 0 },
			error: null
		});

		await expect(callLoad(newCharacterLoad, makeServerEvent({ user }))).rejects.toMatchObject({
			status: 303,
			location: '/characters'
		});
	});

	it('returns the catalog and the slot usage for the wizard', async () => {
		characterApi.fetchCreationCatalog.mockResolvedValue({
			catalog,
			error: 'The class list is unavailable.'
		});

		await expect(callLoad(newCharacterLoad, makeServerEvent({ user }))).resolves.toEqual({
			catalog,
			catalogError: 'The class list is unavailable.',
			slots: { used: 1, max: 5, remaining: 4 },
			slotsError: null
		});
	});

	it('keeps the wizard reachable when the slot count could not be read', async () => {
		characterApi.fetchCharacterSlots.mockResolvedValue({
			status: 502,
			data: null,
			error: 'Slots unavailable'
		});

		await expect(callLoad(newCharacterLoad, makeServerEvent({ user }))).resolves.toEqual({
			catalog,
			catalogError: null,
			slots: null,
			slotsError: 'Slots unavailable'
		});
	});

	it('redirects an anonymous visitor to sign in', async () => {
		await expect(callLoad(newCharacterLoad, makeServerEvent())).rejects.toMatchObject({
			status: 303,
			location: '/login'
		});
	});
});
