import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { goto, resetNavigation } from '#lib/testing/app-navigation';
import { installFetch, jsonResponse, textResponse, type FetchCall } from '#lib/testing/fetch';
import CreationPage from './characters/new/+page.svelte';

/**
 * The character creation wizard.
 *
 * The wizard is the longest client-side flow in the app and every step writes to the server before
 * it advances, so the tests drive it the way a player does: type a name, wait for the debounced
 * availability check, then press Continue once per step and assert what was sent. Failures are
 * asserted at the two places they can happen — starting the session, and saving a step.
 */

const catalog = {
	classes: [
		{ slug: 'warrior', displayName: 'Warrior' },
		{ slug: 'mage', displayName: 'Mage', description: 'Weaves the arcane.' }
	],
	species: [{ slug: 'human', displayName: 'Human', description: 'Adaptable.' }],
	backgrounds: [{ slug: 'noble', displayName: 'Noble' }]
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

/** Installs a fetch stub that answers by `METHOD path` and rejects on an unknown route. */
function stubFetch(overrides: Record<string, Route> = {}) {
	const table = routes(overrides);

	return installFetch((call: FetchCall) => {
		const key = `${call.method} ${new URL(call.url, 'http://localhost').pathname}`;
		const handler = table[key];
		if (!handler) throw new Error(`unexpected fetch ${key}`);
		return handler();
	});
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

const pageSays = (text: string | RegExp) => screen.findByText(text);
const continueButton = () =>
	screen.getByRole('button', { name: /Continue|Create character|Saving/ });

/**
 * Waits for the session to start.
 *
 * `onMount` kicks off a fetch, so the step body does not exist when `render` resolves and the step
 * header says nothing about it — the Cancel button is the first element that only renders once the
 * session is live.
 */
const started = () => screen.findByRole('button', { name: /Cancel/ });

/** Completes the name step, which is the only one that needs a debounced server check. */
async function nameStep(container: HTMLElement, value = 'Kaleth'): Promise<void> {
	await started();
	const input = container.querySelector('input[type="text"]') as HTMLInputElement;
	await fireEvent.input(input, { target: { value } });
	await pageSays('That name is available.');
}

const stubs: Array<{ restore(): void }> = [];

function useStub(overrides: Record<string, Route> = {}) {
	const stub = stubFetch(overrides);
	stubs.push(stub);
	return stub;
}

afterEach(() => {
	for (const stub of stubs.splice(0)) stub.restore();
	resetNavigation();
	vi.restoreAllMocks();
});

describe('starting the session', () => {
	it('says it is preparing while the session is being created', async () => {
		useStub({
			'POST /api/characters/creation': () => new Promise<Response>(() => {})
		});

		renderWizard();

		expect(screen.getByText('Preparing your creation session…')).toBeTruthy();
	});

	it('renders the slot count from the load and starts on the name step', async () => {
		useStub();
		const { container } = renderWizard();

		expect(screen.getByText('1 / 5 slots used')).toBeTruthy();
		await nameStep(container, 'Kaleth');
		expect(screen.getByText('Step 1 of 7 · Name')).toBeTruthy();
		expect(screen.getByText('That name is available.')).toBeTruthy();
	});

	it('shows the upstream reason when the session cannot start, then retries', async () => {
		let attempts = 0;
		const stub = useStub({
			'POST /api/characters/creation': () => {
				attempts += 1;
				return attempts === 1
					? jsonResponse({ error: 'You already have a character in creation.' }, { status: 409 })
					: jsonResponse(session);
			}
		});

		renderWizard();

		expect(await pageSays('You already have a character in creation.')).toBeTruthy();
		expect(screen.getByRole('link', { name: 'Back to characters' })).toBeTruthy();

		await fireEvent.click(screen.getByRole('button', { name: 'Try again' }));

		expect(await screen.findByText('Step 1 of 7 · Name')).toBeTruthy();
		expect(stub.calls.filter((call) => call.method === 'POST')).toHaveLength(2);
	});

	it('falls back to a generic message when the failure body is not JSON', async () => {
		useStub({ 'POST /api/characters/creation': () => textResponse('boom', { status: 500 }) });

		renderWizard();

		expect(await pageSays('Could not start a character creation session.')).toBeTruthy();
	});

	it('reports an unreachable server distinctly from a rejected request', async () => {
		useStub({
			'POST /api/characters/creation': () => {
				throw new TypeError('Failed to fetch');
			}
		});

		renderWizard();

		expect(await pageSays('Could not reach the game server. Try again in a moment.')).toBeTruthy();
	});

	it('shows a catalog error from the load without blocking the wizard', async () => {
		useStub();

		renderWizard({ catalogError: 'The class list is unavailable.' });

		expect(await pageSays('The class list is unavailable.')).toBeTruthy();
		expect(await screen.findByText('Step 1 of 7 · Name')).toBeTruthy();
	});
});

describe('the name step', () => {
	it('refuses a name that fails local validation without calling the server', async () => {
		const stub = useStub();
		const { container } = renderWizard();
		await started();

		const input = container.querySelector('input[type="text"]') as HTMLInputElement;
		await fireEvent.input(input, { target: { value: 'K1!' } });

		expect(screen.getByText('Name may only contain letters and spaces.')).toBeTruthy();
		expect(continueButton()).toHaveProperty('disabled', true);
		expect(stub.calls.some((call) => call.url.includes('check-name'))).toBe(false);
	});

	it('enables Continue only once the server confirms the name', async () => {
		const stub = useStub();
		const { container } = renderWizard();
		await started();

		const input = container.querySelector('input[type="text"]') as HTMLInputElement;
		await fireEvent.input(input, { target: { value: 'Kaleth' } });

		expect(screen.getByText('Checking availability…')).toBeTruthy();
		await pageSays('That name is available.');
		expect(continueButton()).toHaveProperty('disabled', false);
		expect(stub.calls.some((call) => call.url.endsWith('/check-name?name=Kaleth'))).toBe(true);
	});

	it('reports a taken name from the check response', async () => {
		useStub({
			'GET /api/characters/creation/check-name': () =>
				jsonResponse({ available: false, error: 'That name is taken.' })
		});
		const { container } = renderWizard();
		await started();

		await fireEvent.input(container.querySelector('input[type="text"]') as HTMLInputElement, {
			target: { value: 'Kaleth' }
		});

		expect(await pageSays('That name is taken.')).toBeTruthy();
		expect(continueButton()).toHaveProperty('disabled', true);
	});

	it('reports a check that could not be performed', async () => {
		useStub({
			'GET /api/characters/creation/check-name': () => {
				throw new TypeError('Failed to fetch');
			}
		});
		const { container } = renderWizard();
		await started();

		await fireEvent.input(container.querySelector('input[type="text"]') as HTMLInputElement, {
			target: { value: 'Kaleth' }
		});

		expect(await pageSays('Could not check that name. Try again.')).toBeTruthy();
	});

	it('saves the trimmed name and advances', async () => {
		const stub = useStub();
		const { container } = renderWizard();

		await nameStep(container, '  Kaleth  ');
		await fireEvent.click(continueButton());

		expect(await screen.findByText('Step 2 of 7 · Class')).toBeTruthy();
		const patch = stub.calls.find((call) => call.method === 'PATCH');
		expect(patch?.url).toContain('/creation/s1/name');
		expect(JSON.parse(patch?.body ?? '{}')).toEqual({ characterName: 'Kaleth' });
	});
});

describe('selection steps', () => {
	it('sends the class, species and background the player picked', async () => {
		const stub = useStub();
		const { container } = renderWizard();

		await nameStep(container);
		await fireEvent.click(continueButton());
		await screen.findByText('Step 2 of 7 · Class');

		await fireEvent.click(screen.getByRole('button', { name: /Mage/ }));
		expect(screen.getByRole('button', { name: /Mage/ }).getAttribute('aria-pressed')).toBe('true');
		await fireEvent.click(continueButton());
		await screen.findByText('Step 3 of 7 · Species');

		await fireEvent.click(continueButton());
		await screen.findByText('Step 4 of 7 · Background');

		await fireEvent.click(continueButton());
		await screen.findByText('Step 5 of 7 · Attributes');

		const patches = stub.calls.filter((call) => call.method === 'PATCH');
		expect(patches.map((call) => JSON.parse(call.body ?? '{}'))).toEqual([
			{ characterName: 'Kaleth' },
			{ className: 'mage' },
			{ speciesSlug: 'human' },
			{ backgroundId: 'noble' }
		]);
	});

	it('keeps the player on the step and shows the reason when a save is rejected', async () => {
		useStub({
			'PATCH /api/characters/creation/s1/class': () =>
				jsonResponse({ error: 'That class is locked.' }, { status: 422 })
		});
		const { container } = renderWizard();

		await nameStep(container);
		await fireEvent.click(continueButton());
		await screen.findByText('Step 2 of 7 · Class');

		await fireEvent.click(continueButton());

		expect(await pageSays('That class is locked.')).toBeTruthy();
		expect(screen.getByText('Step 2 of 7 · Class')).toBeTruthy();
	});

	it('falls back to a step-specific message when the save is rejected without one', async () => {
		useStub({
			'PATCH /api/characters/creation/s1/background': () => textResponse('nope', { status: 400 })
		});
		const { container } = renderWizard();

		await nameStep(container);
		await fireEvent.click(continueButton());
		await screen.findByText('Step 2 of 7 · Class');
		await fireEvent.click(continueButton());
		await screen.findByText('Step 3 of 7 · Species');
		await fireEvent.click(continueButton());
		await screen.findByText('Step 4 of 7 · Background');
		await fireEvent.click(continueButton());

		expect(await pageSays('The server rejected that choice.')).toBeTruthy();
	});

	it('clears the error when the player steps back', async () => {
		useStub({
			'PATCH /api/characters/creation/s1/class': () =>
				jsonResponse({ error: 'That class is locked.' }, { status: 422 })
		});
		const { container } = renderWizard();

		await nameStep(container);
		await fireEvent.click(continueButton());
		await screen.findByText('Step 2 of 7 · Class');
		await fireEvent.click(continueButton());
		await pageSays('That class is locked.');

		await fireEvent.click(screen.getByRole('button', { name: /Back/ }));

		expect(screen.getByText('Step 1 of 7 · Name')).toBeTruthy();
		expect(screen.queryByText('That class is locked.')).toBeNull();
	});
});

describe('attributes and equipment', () => {
	it('sends the allocation the player built and the equipment preferences', async () => {
		const stub = useStub();
		const { container } = renderWizard();

		await nameStep(container);
		await fireEvent.click(continueButton());
		await screen.findByText('Step 2 of 7 · Class');
		await fireEvent.click(continueButton());
		await screen.findByText('Step 3 of 7 · Species');
		await fireEvent.click(continueButton());
		await screen.findByText('Step 4 of 7 · Background');
		await fireEvent.click(continueButton());
		await screen.findByText('Step 5 of 7 · Attributes');

		// The allocation itself is asserted from the PATCH body below; '9' is ambiguous in the DOM
		// because the remaining-points counter drops to 9 at the same time.
		await fireEvent.click(screen.getByRole('button', { name: 'Increase Strength' }));
		await fireEvent.click(continueButton());
		await screen.findByText('Step 6 of 7 · Equipment');

		await fireEvent.change(container.querySelector('select') as HTMLSelectElement, {
			target: { value: 'light' }
		});
		await fireEvent.click(screen.getByRole('checkbox'));
		await fireEvent.click(continueButton());
		await screen.findByText('Step 7 of 7 · Review');

		const patches = stub.calls.filter((call) => call.method === 'PATCH');
		expect(JSON.parse(patches.at(-2)?.body ?? '{}')).toEqual({
			allocations: {
				Strength: 9,
				Dexterity: 8,
				Constitution: 8,
				Intelligence: 8,
				Wisdom: 8,
				Charisma: 8
			}
		});
		expect(JSON.parse(patches.at(-1)?.body ?? '{}')).toEqual({
			preferredArmorType: 'light',
			preferredWeaponType: 'sword',
			includeShield: true
		});
	});
});

describe('review and finalize', () => {
	/** Drives the wizard to the review step with the default choices. */
	async function toReview(container: HTMLElement): Promise<void> {
		await nameStep(container);
		await fireEvent.click(continueButton());
		await screen.findByText('Step 2 of 7 · Class');
		for (const label of ['Species', 'Background', 'Attributes', 'Equipment']) {
			await fireEvent.click(continueButton());
			await screen.findByText(new RegExp(`· ${label}$`));
		}
		await fireEvent.click(continueButton());
		await screen.findByText('Step 7 of 7 · Review');
	}

	it('shows the preview, warns about hardcore, and creates the character', async () => {
		const stub = useStub();
		const { container } = renderWizard();

		await toReview(container);

		expect(await pageSays('Kaleth')).toBeTruthy();
		expect(screen.getByText('120 / 40')).toBeTruthy();

		await fireEvent.click(screen.getByRole('radio', { name: /Hardcore/ }));
		expect(screen.getByText('Hardcore characters cannot be restored after death.')).toBeTruthy();
		await fireEvent.click(continueButton());

		await waitFor(() => expect(goto).toHaveBeenCalled());
		expect(String(goto.mock.calls[0][0])).toContain('/play/char-1');
		const finalize = stub.calls.find(
			(call) => call.method === 'POST' && call.url.includes('finalize')
		);
		expect(JSON.parse(finalize?.body ?? '{}')).toEqual({
			characterName: 'Kaleth',
			difficultyMode: 'hardcore'
		});
	});

	it('reports a preview that could not be loaded', async () => {
		useStub({
			'GET /api/characters/creation/s1/preview': () =>
				jsonResponse({ error: 'Preview is unavailable.' }, { status: 502 })
		});
		const { container } = renderWizard();

		await toReview(container);

		expect(await pageSays('Preview is unavailable.')).toBeTruthy();
	});

	it('keeps the player on the review step when the character cannot be created', async () => {
		useStub({
			'POST /api/characters/creation/s1/finalize': () =>
				jsonResponse({ error: 'Name already in use.' }, { status: 409 })
		});
		const { container } = renderWizard();

		await toReview(container);
		await fireEvent.click(continueButton());

		expect(await pageSays('Name already in use.')).toBeTruthy();
		expect(goto).not.toHaveBeenCalled();
		expect(screen.getByText('Step 7 of 7 · Review')).toBeTruthy();
	});
});

describe('cancelling', () => {
	it('deletes the session and returns to the roster', async () => {
		const stub = useStub();
		renderWizard();

		await started();
		await fireEvent.click(screen.getByRole('button', { name: /Cancel/ }));

		await waitFor(() => expect(goto).toHaveBeenCalled());
		expect(stub.calls.some((call) => call.method === 'DELETE')).toBe(true);
		expect(String(goto.mock.calls[0][0])).toBe('/characters');
	});

	it('still returns to the roster when the delete fails', async () => {
		useStub({
			'DELETE /api/characters/creation/s1': () => {
				throw new TypeError('Failed to fetch');
			}
		});
		renderWizard();

		await started();
		await fireEvent.click(screen.getByRole('button', { name: /Cancel/ }));

		await waitFor(() => expect(goto).toHaveBeenCalled());
	});
});
