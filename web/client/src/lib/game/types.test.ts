import { describe, expect, it } from 'vitest';
import {
	EQUIPMENT_SLOTS,
	isEquippable,
	itemLabel,
	itemSubtitle,
	rarityClass,
	type InventoryItem
} from './types';

const sword: InventoryItem = {
	itemRef: 'iron_sword',
	name: 'Iron Sword',
	description: null,
	rarity: 'Rare',
	type: 'Weapon',
	typeKey: 'heavy-blades',
	slot: 'MainHand',
	quantity: 1,
	stackSize: 1,
	isStackable: false,
	isEquipped: false,
	price: 15,
	weight: 3,
	durability: 72
};

const potion: InventoryItem = {
	...sword,
	itemRef: 'health_potion',
	name: 'Health Potion',
	rarity: 'Common',
	type: 'Consumable',
	slot: null,
	quantity: 4,
	durability: null
};

describe('rarityClass', () => {
	it('maps known rarities onto the VDS colour classes', () => {
		expect(rarityClass('Legendary')).toBe('rarity-legendary');
		expect(rarityClass('uncommon')).toBe('rarity-uncommon');
	});

	it('falls back to common for missing or unknown rarities', () => {
		expect(rarityClass(undefined)).toBe('rarity-common');
		expect(rarityClass('mythic')).toBe('rarity-common');
	});
});

describe('item helpers', () => {
	it('labels items by name with an itemRef fallback', () => {
		expect(itemLabel(sword)).toBe('Iron Sword');
		expect(itemLabel({ ...sword, name: '' })).toBe('iron_sword');
	});

	it('summarises rarity, type and durability', () => {
		expect(itemSubtitle(sword)).toBe('Rare · Weapon · 72% durability');
		expect(itemSubtitle(potion)).toBe('Common · Consumable');
	});

	it('only treats items with a slot as equippable', () => {
		expect(isEquippable(sword)).toBe(true);
		expect(isEquippable(potion)).toBe(false);
	});
});

describe('EQUIPMENT_SLOTS', () => {
	it('lists the eight canonical slots the server validates', () => {
		expect(EQUIPMENT_SLOTS.map((slot) => slot.id)).toEqual([
			'MainHand',
			'OffHand',
			'Head',
			'Chest',
			'Legs',
			'Feet',
			'Ring',
			'Amulet'
		]);
	});
});
