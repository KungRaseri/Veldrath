import { fireEvent, render, screen, within } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import LeftSidebar from './LeftSidebar.svelte';
import { GameSession } from './session.svelte';
import type {
	CharacterAttributes,
	CharacterSelectedPayload,
	CharacterStats,
	InventoryItem,
	LocationEnteredPayload,
	MapGraph,
	PartySnapshot,
	ZoneEnteredPayload
} from './types';

// `#lib/hub` reaches for `$app/env/public`, which needs a SvelteKit dev global that Vitest does
// not provide. Every action on the session guards on `this.hub`, so a null factory keeps the
// component mounted while the click handlers stay harmless.
vi.mock('#lib/hub', () => ({ createGameHub: () => null }));

/**
 * The character sheet.
 *
 * Everything below drives the real `GameSession`: the sheet reads a dozen derived values
 * (`bagItems`, `carriedItemCount`, `equipped`, `trackedQuests`, `canShop`), so a hand-written
 * fake would duplicate the model and drift from it. Rendering is done per case rather than
 * shared, because the sheet keeps its own pending-discard and tooltip state.
 */

function inventoryItem(overrides: Partial<InventoryItem> = {}): InventoryItem {
	return {
		itemRef: 'item-1',
		name: 'Ember blade',
		description: 'A blade of banked fire.',
		rarity: 'Rare',
		type: 'Weapon',
		typeKey: 'Sword',
		slot: 'MainHand',
		quantity: 1,
		stackSize: 1,
		isStackable: false,
		isEquipped: false,
		price: 120,
		weight: 3,
		durability: null,
		effect: null,
		effectPower: 0,
		isTwoHanded: false,
		armorClass: null,
		armorType: null,
		weaponType: 'sword',
		damageMin: 4,
		damageMax: 9,
		requirementsLevel: 1,
		requirements: {},
		bonuses: {},
		...overrides
	};
}

const character: CharacterSelectedPayload = {
	id: 'char-1',
	name: 'Kaleth',
	className: 'Warden',
	level: 7,
	experience: 1200,
	regionId: 'region-1',
	currentHealth: 40,
	maxHealth: 50,
	currentMana: 12,
	maxMana: 20,
	gold: 1234,
	unspentAttributePoints: 2,
	strength: 14,
	dexterity: 12,
	constitution: 13,
	intelligence: 10,
	wisdom: 9,
	charisma: 8,
	learnedAbilities: [],
	selectedAt: '2026-01-01T00:00:00Z'
};

const stats: CharacterStats = {
	currentHealth: 40,
	maxHealth: 50,
	currentMana: 12,
	maxMana: 20,
	gold: 1234,
	level: 7,
	experience: 1200,
	unspentAttributePoints: 2
};

const attributes: CharacterAttributes = {
	strength: 14,
	dexterity: 12,
	constitution: 13,
	intelligence: 10,
	wisdom: 9,
	charisma: 8
};

const zone: ZoneEnteredPayload = {
	id: 'zone-1',
	name: 'Aldenmere',
	description: 'A market town.',
	zoneType: 'Town',
	occupants: [],
	hasInn: true,
	hasMerchant: true
};

const location: LocationEnteredPayload = {
	locationSlug: 'market',
	locationDisplayName: 'Market',
	typeKey: 'Location'
} as LocationEnteredPayload;

/** A selected character standing in a town, which is what the sheet is built for. */
function sheet(): GameSession {
	const session = new GameSession();
	session.phase = 'connected';
	session.character = { ...character };
	session.stats = { ...stats };
	session.attributes = { ...attributes };
	session.zone = { ...zone };
	session.location = { ...location };
	return session;
}

/** The `<li>` that owns a given equipment slot. */
function equipSlot(container: HTMLElement, label: string): HTMLElement {
	const slot = [...container.querySelectorAll<HTMLElement>('.equip-slot')].find((candidate) =>
		within(candidate).queryByText(label, { selector: '.equip-slot-slot' })
	);
	if (!slot) throw new Error(`no equipment slot for ${label}`);
	return slot;
}

/** The bag row for a given item name, when the name is not unique enough for a plain query. */
function bagRow(container: HTMLElement, name: string): HTMLElement {
	const row = [...container.querySelectorAll<HTMLElement>('.bag-row')].find(
		(candidate) => within(candidate).queryByRole('button', { name }) !== null
	);
	if (!row) throw new Error(`no bag row for ${name}`);
	return row;
}

beforeEach(() => {
	document.body.innerHTML = '';
});

describe('character header', () => {
	it('renders the name, class, level and vitals', () => {
		render(LeftSidebar, { session: sheet() });

		expect(screen.getByText('Kaleth')).toBeTruthy();
		expect(screen.getByText('Warden · Level 7')).toBeTruthy();
		expect(screen.getByText('40 / 50')).toBeTruthy();
		expect(screen.getByText('12 / 20')).toBeTruthy();
	});

	it('falls back to the loading state before a character arrives', () => {
		const session = new GameSession();
		session.stats = { ...stats, unspentAttributePoints: 0 };
		render(LeftSidebar, { session });

		expect(screen.getByText('Entering…')).toBeTruthy();
		expect(screen.getByText('Loading')).toBeTruthy();
		// With no points banked the chip beside the attributes is omitted.
		expect(screen.queryByText('+2')).toBeNull();
	});

	it('opens the attribute dialog from the sheet button', async () => {
		const session = sheet();
		render(LeftSidebar, { session });

		const button = screen.getByTitle('Open attributes');
		expect(within(button).getByText('+2')).toBeTruthy();

		await fireEvent.click(button);
		expect(session.showAttributes).toBe(true);
	});
});

describe('equipment grid', () => {
	it('labels the empty slots and the filled one', () => {
		const session = sheet();
		session.equipped = { MainHand: inventoryItem({ isEquipped: true }) };
		const { container } = render(LeftSidebar, { session });

		expect(within(equipSlot(container, 'Main hand')).getByText('Ember blade')).toBeTruthy();
		// Eleven of the twelve slots start empty.
		expect(screen.getAllByText('Empty')).toHaveLength(11);
	});

	it('shows the tooltip on hover and on focus, and hides it on the way out', async () => {
		const session = sheet();
		session.equipped = { MainHand: inventoryItem({ isEquipped: true }) };
		const { container } = render(LeftSidebar, { session });

		const filled = equipSlot(container, 'Main hand');
		await fireEvent.mouseEnter(filled);
		expect(screen.getByRole('tooltip', { name: 'Ember blade details' })).toBeTruthy();

		await fireEvent.mouseLeave(filled);
		expect(screen.queryByRole('tooltip')).toBeNull();

		await fireEvent.focusIn(filled);
		expect(screen.getByRole('tooltip', { name: 'Ember blade details' })).toBeTruthy();

		await fireEvent.focusOut(filled);
		expect(screen.queryByRole('tooltip')).toBeNull();
	});

	it('leaves the tooltip closed when an empty slot is hovered', async () => {
		const session = sheet();
		session.equipped = { MainHand: inventoryItem({ isEquipped: true }) };
		const { container } = render(LeftSidebar, { session });

		const empty = equipSlot(container, 'Off hand');
		await fireEvent.mouseEnter(empty);
		expect(screen.queryByRole('tooltip')).toBeNull();

		// Focus on an empty slot runs the same guard; it must not open a stale tooltip either.
		await fireEvent.focusIn(equipSlot(container, 'Head'));
		expect(screen.queryByRole('tooltip')).toBeNull();
	});

	it('inspects and unequips straight from the slot', async () => {
		const session = sheet();
		session.equipped = { MainHand: inventoryItem({ isEquipped: true }) };
		const inspect = vi.spyOn(session, 'inspectItem');
		const unequip = vi.spyOn(session, 'unequip');
		const { container } = render(LeftSidebar, { session });

		const slot = within(equipSlot(container, 'Main hand'));
		await fireEvent.click(slot.getByTitle('Inspect Ember blade'));
		await fireEvent.click(slot.getByTitle('Unequip Ember blade'));

		expect(inspect).toHaveBeenCalledWith(expect.objectContaining({ itemRef: 'item-1' }));
		expect(unequip).toHaveBeenCalledWith('MainHand');
	});
});

describe('inventory', () => {
	it('says the bag is empty when nothing is carried', () => {
		render(LeftSidebar, { session: sheet() });

		expect(screen.getByText('Your bag is empty.')).toBeTruthy();
		expect(screen.getByText('(0)')).toBeTruthy();
	});

	it('renders stack sizes, durability, slot and the equipped badge', () => {
		const session = sheet();
		session.inventory = [
			inventoryItem({
				itemRef: 'potion',
				name: 'Healing potion',
				type: 'Consumable',
				typeKey: null,
				slot: null,
				rarity: 'Common',
				effect: 'heal',
				effectPower: 40,
				quantity: 3,
				stackSize: 20,
				isStackable: true
			}),
			inventoryItem({ itemRef: 'item-2', name: 'Iron shield', slot: 'OffHand', durability: 80 }),
			inventoryItem({ itemRef: 'item-3', name: 'Worn blade', isEquipped: true })
		];
		const { container } = render(LeftSidebar, { session });

		expect(screen.getByText('(2)')).toBeTruthy();
		expect(screen.getByText('×3')).toBeTruthy();
		expect(screen.getByText('80%')).toBeTruthy();
		expect(screen.getByLabelText('Equipped')).toBeTruthy();
		expect(within(bagRow(container, 'Iron shield')).getByText('Off hand')).toBeTruthy();
	});

	it('opens the item tooltip from a bag row and closes it again', async () => {
		const session = sheet();
		session.inventory = [inventoryItem()];
		const { container } = render(LeftSidebar, { session });

		const row = bagRow(container, 'Ember blade');
		await fireEvent.mouseEnter(row);
		expect(screen.getByRole('tooltip', { name: 'Ember blade details' })).toBeTruthy();

		await fireEvent.mouseLeave(row);
		expect(screen.queryByRole('tooltip')).toBeNull();

		await fireEvent.focusIn(row);
		expect(screen.getByRole('tooltip', { name: 'Ember blade details' })).toBeTruthy();

		await fireEvent.focusOut(row);
		expect(screen.queryByRole('tooltip')).toBeNull();
	});

	it('wires use, equip, unequip and drop for the matching items', async () => {
		const session = sheet();
		session.inventory = [
			inventoryItem({
				itemRef: 'potion',
				name: 'Healing potion',
				type: 'Consumable',
				slot: null,
				isEquipped: false,
				effect: 'heal',
				effectPower: 40
			}),
			inventoryItem(),
			inventoryItem({ itemRef: 'item-2', name: 'Iron shield', slot: 'OffHand', isEquipped: true })
		];
		const use = vi.spyOn(session, 'useItem');
		const equip = vi.spyOn(session, 'equipItem');
		const unequip = vi.spyOn(session, 'unequip');
		const drop = vi.spyOn(session, 'dropToGround');
		render(LeftSidebar, { session });

		await fireEvent.click(screen.getByRole('button', { name: 'Use Healing potion' }));
		await fireEvent.click(screen.getByRole('button', { name: 'Equip Ember blade' }));
		await fireEvent.click(screen.getByRole('button', { name: 'Unequip Iron shield' }));
		await fireEvent.click(screen.getByRole('button', { name: 'Drop Ember blade' }));

		expect(use).toHaveBeenCalledWith('potion');
		expect(equip).toHaveBeenCalledWith('item-1');
		expect(unequip).toHaveBeenCalledWith('OffHand');
		expect(drop).toHaveBeenCalledWith('item-1');
	});

	it('hides the use button during combat and blocks dropping without a location', async () => {
		const session = sheet();
		session.location = null;
		session.combat = { active: true, enemy: null, log: [] };
		session.inventory = [
			inventoryItem({ itemRef: 'potion', name: 'Healing potion', slot: null, effect: 'heal' })
		];
		const drop = vi.spyOn(session, 'dropToGround');
		render(LeftSidebar, { session });

		expect(screen.queryByRole('button', { name: 'Use Healing potion' })).toBeNull();
		// An equippable item still offers Equip outside combat, but only with a character loaded.
		expect(screen.queryByRole('button', { name: 'Equip Healing potion' })).toBeNull();

		const dropButton = screen.getByRole('button', { name: 'Drop Healing potion' });
		expect((dropButton as HTMLButtonElement).disabled).toBe(true);
		await fireEvent.click(dropButton);
		expect(drop).toHaveBeenCalled();
	});

	it('only offers Equip while a character is selected', () => {
		const session = sheet();
		session.character = null;
		session.inventory = [inventoryItem()];
		render(LeftSidebar, { session });

		expect(screen.queryByRole('button', { name: 'Equip Ember blade' })).toBeNull();
		expect(screen.getByRole('button', { name: 'Discard Ember blade' })).toBeTruthy();
	});

	it('falls back to empty strings when a partial payload leaves fields undefined', async () => {
		const session = sheet();
		// A payload from an older server can omit the vitals; each interpolation falls back rather
		// than rendering the word "undefined".
		session.stats = {
			...stats,
			currentHealth: undefined,
			maxHealth: undefined,
			currentMana: undefined,
			maxMana: undefined,
			level: undefined,
			unspentAttributePoints: 0
		} as unknown as CharacterStats;
		session.character = {
			...character,
			className: undefined
		} as unknown as CharacterSelectedPayload;
		session.inventory = [
			inventoryItem({
				itemRef: 'item-9',
				name: 'Odd trinket',
				isEquipped: true,
				slot: null,
				quantity: undefined as unknown as number
			})
		];
		const unequip = vi.spyOn(session, 'unequip');
		const { container } = render(LeftSidebar, { session });
		const text = container.querySelector('aside.character-sheet')?.textContent ?? '';

		expect(text).toContain(' · Level ');
		expect(text).toContain(' / ');
		// The quantity badge is omitted when the payload has no quantity.
		expect(screen.queryByText(/^×/)).toBeNull();

		await fireEvent.click(screen.getByRole('button', { name: 'Unequip Odd trinket' }));
		expect(unequip).toHaveBeenCalledWith('');
	});

	it('offers Sell in the bag only where a merchant trades', async () => {
		const session = sheet();
		session.inventory = [inventoryItem()];
		const sell = vi.spyOn(session, 'sell');
		render(LeftSidebar, { session });

		await fireEvent.click(screen.getByRole('button', { name: 'Sell Ember blade' }));
		expect(sell).toHaveBeenCalledWith('item-1');
	});

	it('asks for confirmation before discarding, and can back out', async () => {
		const session = sheet();
		session.zone = { ...zone, hasMerchant: false };
		session.inventory = [inventoryItem()];
		const discard = vi.spyOn(session, 'discard').mockResolvedValue(undefined);
		const { container } = render(LeftSidebar, { session });

		expect(screen.queryByRole('button', { name: 'Sell Ember blade' })).toBeNull();

		await fireEvent.click(screen.getByRole('button', { name: 'Discard Ember blade' }));
		const row = bagRow(container, 'Ember blade');
		expect(within(row).getByRole('button', { name: 'Discard' })).toBeTruthy();

		await fireEvent.click(within(row).getByRole('button', { name: 'Keep' }));
		expect(within(row).queryByRole('button', { name: 'Keep' })).toBeNull();
		expect(discard).not.toHaveBeenCalled();

		await fireEvent.click(within(row).getByRole('button', { name: 'Discard Ember blade' }));
		await fireEvent.click(within(row).getByRole('button', { name: 'Discard' }));

		expect(discard).toHaveBeenCalledWith('item-1');
		expect(within(row).queryByRole('button', { name: 'Keep' })).toBeNull();
	});

	it('refreshes the inventory through the session', async () => {
		const session = sheet();
		session.inventory = [inventoryItem()];
		const openInventory = vi.spyOn(session, 'openInventory').mockResolvedValue(undefined);
		render(LeftSidebar, { session });

		await fireEvent.click(screen.getByRole('button', { name: 'Refresh inventory' }));
		expect(openInventory).toHaveBeenCalled();
	});
});

describe('character menu', () => {
	it('opens every overlay and the merchant', async () => {
		const session = sheet();
		const loadQuests = vi.spyOn(session, 'loadQuests').mockResolvedValue(undefined);
		const openParty = vi.spyOn(session, 'openParty').mockResolvedValue(undefined);
		const visitShop = vi.spyOn(session, 'visitShop').mockResolvedValue(undefined);
		render(LeftSidebar, { session });

		await fireEvent.click(screen.getByRole('button', { name: 'Quest log' }));
		await fireEvent.click(screen.getByRole('button', { name: 'Party' }));
		await fireEvent.click(screen.getByRole('button', { name: 'Abilities' }));
		await fireEvent.click(screen.getByRole('button', { name: 'Merchant' }));
		await fireEvent.click(screen.getByRole('button', { name: 'Settings' }));

		expect(loadQuests).toHaveBeenCalled();
		expect(openParty).toHaveBeenCalled();
		expect(session.showAbilities).toBe(true);
		expect(visitShop).toHaveBeenCalled();
		expect(session.showSettings).toBe(true);
	});

	it('badges pending party invites, otherwise shows the member count', () => {
		const invited = sheet();
		invited.partyInvites = [
			{
				partyId: 'p1',
				partyLeaderName: 'Kaleth',
				invitedByName: 'Kaleth',
				expiresAt: '2026-02-01T00:00:00.000Z'
			}
		];
		const first = render(LeftSidebar, { session: invited });
		expect(within(screen.getByRole('button', { name: /Party/ })).getByText('1')).toBeTruthy();
		first.unmount();

		const joined = sheet();
		joined.party = {
			partyId: 'p1',
			leaderCharacterId: 'char-1',
			lootRule: 'RoundRobin',
			members: [
				{
					characterId: 'char-1',
					name: 'Kaleth',
					level: 7,
					currentHealth: 40,
					maxHealth: 50,
					isLeader: true
				},
				{
					characterId: 'char-2',
					name: 'Brann',
					level: 5,
					currentHealth: 30,
					maxHealth: 40,
					isLeader: false
				}
			]
		} as PartySnapshot;
		render(LeftSidebar, { session: joined });
		expect(screen.getByText('(2)')).toBeTruthy();

		// Neither an invite nor a party leaves the Party entry bare.
		expect(screen.queryByText('(3)')).toBeNull();
	});

	it('hides the merchant entry outside a trading zone', () => {
		const session = sheet();
		session.zone = { ...zone, hasMerchant: false };
		render(LeftSidebar, { session });

		expect(screen.queryByRole('button', { name: 'Merchant' })).toBeNull();
		expect(screen.getByRole('button', { name: 'Abilities' })).toBeTruthy();
	});
});

describe('quest tracker', () => {
	it('lists the tracked quest objective, or says it is ready to turn in', () => {
		const session = sheet();
		session.map = null as MapGraph | null;
		session.quests = [
			{
				slug: 'market-rats',
				title: 'Rats in the market',
				description: '',
				status: 'Active',
				goldReward: 10,
				xpReward: 20,
				canTurnIn: false,
				objectives: [{ key: 'kill:rat', current: 1, required: 3, complete: false }]
			},
			{
				slug: 'errand',
				title: 'A finished errand',
				description: '',
				status: 'Active',
				goldReward: 5,
				xpReward: 5,
				canTurnIn: true,
				objectives: []
			}
		];
		render(LeftSidebar, { session });

		expect(screen.getByText('kill:rat · 1/3')).toBeTruthy();
		expect(screen.getByText('Ready to turn in')).toBeTruthy();
	});
});
