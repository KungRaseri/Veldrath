import { render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';
import CombatPanel from './CombatPanel.svelte';
import GameHeader from './GameHeader.svelte';
import GameOverlays from './GameOverlays.svelte';
import LeftSidebar from './LeftSidebar.svelte';
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
	unspentAttributePoints: 2
};

const ironSword: InventoryItem = {
	itemRef: 'iron_sword',
	name: 'Iron Sword',
	description: 'A dependable blade.',
	rarity: 'Rare',
	type: 'Weapon',
	typeKey: 'heavy-blades',
	slot: 'MainHand',
	quantity: 1,
	stackSize: 1,
	isStackable: false,
	isEquipped: true,
	price: 15,
	weight: 3,
	durability: 80
};

const healthPotion: InventoryItem = {
	itemRef: 'health_potion',
	name: 'Health Potion',
	description: null,
	rarity: 'Common',
	type: 'Consumable',
	typeKey: null,
	slot: null,
	quantity: 3,
	stackSize: 20,
	isStackable: true,
	isEquipped: false,
	price: 5,
	weight: 0.5,
	durability: null
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
		location: {
			characterId: 'char-1',
			locationSlug: 'grey-cup',
			locationDisplayName: 'The Grey Cup',
			typeKey: 'Inn'
		},
		occupants: [],
		serverInfo: { connectionId: 'conn-1', serverVersion: '0.1', minCompatibleClientVersion: '0.1' },
		phase: 'connected',
		map: null,
		currentNode: null,
		selectedNode: null,
		currentNodeId: null,
		selectedNodeId: null,
		adjacentNodeIds: [] as string[],
		combat: { active: false, enemy: null, log: [] } satisfies CombatState,
		equippedEntries: [],
		equipped: {} as Record<string, InventoryItem>,
		inventory: [ironSword, healthPotion],
		bagItems: [healthPotion, ironSword],
		carriedItemCount: 1,
		quests: [],
		shopCatalog: [],
		shopZone: null,
		canShop: false,
		canRest: true,
		zone: { id: 'zone-1', name: 'Aldenmere', hasInn: true, hasMerchant: true },
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
		equipItem: vi.fn(),
		unequip: vi.fn(),
		sell: vi.fn(),
		discard: vi.fn(),
		buy: vi.fn()
	};

	return { ...base, ...overrides } as unknown as GameSession;
}

describe('GameHeader', () => {
	it('renders identity, location breadcrumb and connection state', async () => {
		const onLeave = vi.fn();
		render(GameHeader, { props: { session: fakeSession(), onLeave } });

		expect(await screen.findByRole('heading', { name: /Thornwarden/ })).toBeTruthy();
		expect(screen.getByText(/Warrior · Lv 3/)).toBeTruthy();
		expect(screen.getByText(/Aldenmere/)).toBeTruthy();
		expect(screen.getByText('connected')).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Characters' })).toBeTruthy();
	});
});

describe('LeftSidebar (character sheet)', () => {
	it('renders vitals in the sheet instead of the header', async () => {
		render(LeftSidebar, { props: { session: fakeSession() } });

		expect(await screen.findByText('80 / 100')).toBeTruthy();
		expect(screen.getByText('25 / 40')).toBeTruthy();
		expect(screen.getByText('42')).toBeTruthy();
		expect(screen.getByText('250')).toBeTruthy();
		expect(screen.getByText('2 points to spend')).toBeTruthy();
	});

	it('renders the equipment rack with empty and filled slots', async () => {
		render(LeftSidebar, {
			props: {
				session: fakeSession({
					equipped: { MainHand: ironSword },
					equippedEntries: [['MainHand', ironSword]]
				})
			}
		});

		// The equipped item shows in the rack and (flagged) in the bag list.
		expect((await screen.findAllByText('Iron Sword')).length).toBeGreaterThan(1);
		expect(screen.getAllByText('Empty').length).toBeGreaterThan(0);
		expect(screen.getAllByRole('button', { name: 'Unequip Iron Sword' }).length).toBeGreaterThan(0);
	});

	it('marks equipped bag entries and exposes equip, sell and discard actions', async () => {
		const steelSword: InventoryItem = {
			...ironSword,
			itemRef: 'steel_sword',
			name: 'Steel Sword',
			rarity: 'Uncommon',
			isEquipped: false
		};
		const session = fakeSession({
			canShop: true,
			bagItems: [healthPotion, steelSword, ironSword]
		});
		render(LeftSidebar, { props: { session } });

		expect(await screen.findByText('Equipped')).toBeTruthy();
		expect(screen.getByText('Common · Consumable')).toBeTruthy();

		screen.getByRole('button', { name: 'Equip Steel Sword' }).click();
		expect(session.equipItem).toHaveBeenCalledWith('steel_sword');

		screen.getByRole('button', { name: 'Sell Steel Sword' }).click();
		expect(session.sell).toHaveBeenCalledWith('steel_sword');

		screen.getByRole('button', { name: 'Discard Health Potion' }).click();
		expect(await screen.findByRole('button', { name: 'Discard' })).toBeTruthy();
	});

	it('offers the character menu', async () => {
		render(LeftSidebar, { props: { session: fakeSession() } });

		expect(await screen.findByRole('button', { name: /Quest log/ })).toBeTruthy();
		expect(screen.getByRole('button', { name: /Settings/ })).toBeTruthy();
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

describe('GameOverlays (dialogs)', () => {
	it('renders the quest log dialog when open', async () => {
		const session = fakeSession({
			showQuests: true,
			quests: [{ slug: 'rats', title: 'Clear the cellar', status: 'Active' }]
		});

		render(GameOverlays, { props: { session } });

		expect(await screen.findByText('Clear the cellar')).toBeTruthy();
		expect(screen.getByText('Active')).toBeTruthy();
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
