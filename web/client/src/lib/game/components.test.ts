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
	durability: 80,
	effect: null,
	effectPower: 0,
	isTwoHanded: false,
	armorClass: null,
	armorType: null,
	weaponType: 'swords',
	damageMin: 3,
	damageMax: 8,
	requirementsLevel: 1,
	requirements: {},
	bonuses: { strength: 1 }
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
	durability: null,
	effect: 'heal',
	effectPower: 25,
	isTwoHanded: false,
	armorClass: null,
	armorType: null,
	weaponType: null,
	damageMin: null,
	damageMax: null,
	requirementsLevel: 1,
	requirements: {},
	bonuses: {}
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
		attributes: {
			strength: 12,
			dexterity: 11,
			constitution: 10,
			intelligence: 9,
			wisdom: 8,
			charisma: 7
		},
		inspectedItem: null as InventoryItem | null,
		lastSearch: null,
		equippedEntries: [],
		equipped: {} as Record<string, InventoryItem>,
		inventory: [ironSword, healthPotion],
		groundItems: [] as {
			id: string;
			itemRef: string;
			name: string;
			rarity: string;
			quantity: number;
		}[],
		bagItems: [healthPotion, ironSword],
		carriedItemCount: 1,
		quests: [] as unknown[],
		availableQuests: [] as unknown[],
		shopCatalog: [],
		shopZone: null,
		canShop: false,
		canRest: true,
		zone: { id: 'zone-1', name: 'Aldenmere', hasInn: true, hasMerchant: true },
		showShop: false,
		showQuests: false,
		showSettings: false,
		showAbilities: false,
		showAttributes: false,
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
		buy: vi.fn(),
		useItem: vi.fn(),
		acceptQuest: vi.fn(),
		abandonQuest: vi.fn(),
		turnInQuest: vi.fn(),
		dropToGround: vi.fn(),
		pickUpGroundItem: vi.fn(),
		requestGroundItems: vi.fn(),
		inspectItem: vi.fn(),
		closeInspect: vi.fn(),
		allocateAttributePoints: vi.fn(),
		useAbility: vi.fn()
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
		expect(screen.getByText('+2')).toBeTruthy();
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
			quests: [
				{
					slug: 'rats',
					title: 'Clear the cellar',
					description: 'Cull the rats below the inn.',
					status: 'Active',
					goldReward: 50,
					xpReward: 120,
					canTurnIn: true,
					objectives: [{ key: 'kill:giant-rat', current: 3, required: 3, complete: true }]
				}
			]
		});

		render(GameOverlays, { props: { session } });

		expect(await screen.findByText('Clear the cellar')).toBeTruthy();
		expect(screen.getByText('3/3')).toBeTruthy();
		screen.getByRole('button', { name: 'Turn in' }).click();
		expect(session.turnInQuest).toHaveBeenCalledWith('rats');
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

describe('GameOverlays (item details)', () => {
	it('describes a consumable and offers its use', async () => {
		const session = fakeSession({ inspectedItem: healthPotion });
		render(GameOverlays, { props: { session } });

		expect(await screen.findByText('Restores 25 health')).toBeTruthy();
		expect(screen.getByText('Common · Consumable')).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Use' })).toBeTruthy();
	});

	it('shows weapon damage, bonuses and an equip action', async () => {
		const spare = { ...ironSword, isEquipped: false };
		const session = fakeSession({ inspectedItem: spare, bagItems: [healthPotion, spare] });
		render(GameOverlays, { props: { session } });

		expect(await screen.findByText('3–8 damage')).toBeTruthy();
		expect(screen.getByText('strength +1')).toBeTruthy();
		expect(screen.getByText(/Main hand/)).toBeTruthy();
		expect(screen.getByRole('button', { name: 'Equip' })).toBeTruthy();
	});
});

describe('GameOverlays (attributes)', () => {
	it('accumulates points locally and submits capitalised allocations', async () => {
		const session = fakeSession({ showAttributes: true });
		render(GameOverlays, { props: { session } });

		(await screen.findByRole('button', { name: 'Increase Strength' })).click();
		const spend = await screen.findByRole('button', { name: 'Spend 1 point' });
		spend.click();

		expect(session.allocateAttributePoints).toHaveBeenCalledWith({ Strength: 1 });
	});
});

describe('LeftSidebar (item actions)', () => {
	it('uses consumables from the bag', async () => {
		const session = fakeSession();
		render(LeftSidebar, { props: { session } });

		(await screen.findByRole('button', { name: 'Use Health Potion' })).click();
		expect(session.useItem).toHaveBeenCalledWith('health_potion');
	});

	it('opens the detail dialog when a bag row is clicked', async () => {
		const session = fakeSession();
		render(LeftSidebar, { props: { session } });

		(await screen.findByTitle('Inspect Health Potion')).click();
		expect(session.inspectItem).toHaveBeenCalledWith(healthPotion);
	});
});

describe('LeftSidebar (ground items)', () => {
	it('drops a bag item onto the ground', async () => {
		const session = fakeSession();
		render(LeftSidebar, { props: { session } });

		(await screen.findByRole('button', { name: 'Drop Health Potion' })).click();
		expect(session.dropToGround).toHaveBeenCalledWith('health_potion');
	});
});

describe('GameOverlays (available quests)', () => {
	it('accepts a catalog quest', async () => {
		const session = fakeSession({
			showQuests: true,
			availableQuests: [
				{
					slug: 'wolves',
					title: 'Thin the pack',
					description: '',
					goldReward: 25,
					xpReward: 60,
					objectives: ['kill:barrow-wolf']
				}
			]
		});

		render(GameOverlays, { props: { session } });

		(await screen.findByRole('button', { name: 'Accept' })).click();
		expect(session.acceptQuest).toHaveBeenCalledWith('wolves');
	});
});
