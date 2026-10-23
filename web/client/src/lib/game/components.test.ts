import { render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';
import CombatPanel from './CombatPanel.svelte';
import GameHeader from './GameHeader.svelte';
import GameOverlays from './GameOverlays.svelte';
import type { CharacterStats, CombatState, InventoryItem } from './types';
import type { GameSession } from './session.svelte';

const stats: CharacterStats = {
	currentHealth: 80,
	maxHealth: 100,
	currentMana: 25,
	maxMana: 40,
	gold: 42,
	level: 3,
	experience: 250,
	unspentAttributePoints: 0
};

function fakeSession(overrides: Record<string, unknown> = {}): GameSession {
	const base = {
		character: {
			id: 'char-1',
			name: 'Thornwarden',
			className: 'Warrior',
			level: 3,
			learnedAbilities: ['heal', 'cleave']
		},
		stats,
		zoneLabel: 'Aldenmere',
		location: { slug: 'grey-cup', name: 'The Grey Cup', type: 'Inn' },
		occupants: [],
		serverInfo: { connectionId: 'conn-1', serverVersion: '0.1', minCompatibleClientVersion: '0.1' },
		phase: 'connected',
		map: null,
		currentNode: null,
		combat: { active: false, enemy: null, log: [] } satisfies CombatState,
		equippedEntries: [],
		inventory: [] as InventoryItem[],
		quests: [],
		shopCatalog: [],
		shopZone: null,
		canShop: false,
		showInventory: false,
		showShop: false,
		showQuests: false,
		showSettings: false,
		openInventory: vi.fn(),
		loadQuests: vi.fn(),
		visitShop: vi.fn(),
		requestMapGraph: vi.fn(),
		attack: vi.fn(),
		defend: vi.fn(),
		flee: vi.fn(),
		useCombatAbility: vi.fn(),
		equip: vi.fn(),
		sell: vi.fn(),
		drop: vi.fn(),
		buy: vi.fn()
	};

	return { ...base, ...overrides } as unknown as GameSession;
}

describe('GameHeader', () => {
	it('renders character identity and vitals', async () => {
		render(GameHeader, { props: { session: fakeSession(), onLeave: vi.fn() } });

		expect(await screen.findByText(/Thornwarden/)).toBeTruthy();
		expect(screen.getByText('80 / 100')).toBeTruthy();
		expect(screen.getByText('25 / 40')).toBeTruthy();
		expect(screen.getByText('42')).toBeTruthy();
		expect(screen.getAllByRole('button').map((button) => button.textContent?.trim())).toContain(
			'Inventory'
		);
	});
});

describe('CombatPanel', () => {
	it('renders enemy health and combat actions', async () => {
		const session = fakeSession({
			combat: {
				active: true,
				enemy: {
					id: 'enemy-1',
					name: 'Barrow Wolf',
					level: 2,
					currentHealth: 12,
					maxHealth: 30,
					abilities: ['Bite']
				},
				log: ['Combat started: Barrow Wolf (level 2).']
			} satisfies CombatState
		});

		render(CombatPanel, { props: { session } });

		expect(await screen.findByRole('heading', { name: /Barrow Wolf/ })).toBeTruthy();
		expect(screen.getByText('12 / 30')).toBeTruthy();
		expect(screen.getByText('Combat started: Barrow Wolf (level 2).')).toBeTruthy();
		for (const label of ['Attack', 'Defend', 'Flee', 'heal', 'cleave']) {
			expect(screen.getByRole('button', { name: label })).toBeTruthy();
		}
	});
});

describe('GameOverlays', () => {
	it('renders the empty inventory state', async () => {
		render(GameOverlays, { props: { session: fakeSession({ showInventory: true }) } });

		expect(await screen.findByText('Bag')).toBeTruthy();
		expect(screen.getByText('Your bag is empty.')).toBeTruthy();
	});

	it('renders shop catalog entries with buy buttons', async () => {
		const session = fakeSession({
			showShop: true,
			canShop: true,
			shopZone: 'Aldenmere',
			shopCatalog: [{ slug: 'iron-sword', name: 'Iron Sword', rarity: 'Common', price: 15 }]
		});

		render(GameOverlays, { props: { session } });

		expect(await screen.findByText('Iron Sword')).toBeTruthy();
		expect(screen.getByText('15 g')).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Buy' })).toBeTruthy();
	});
});
