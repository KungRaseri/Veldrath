import { describe, expect, it } from 'vitest';
import {
	EQUIPMENT_SLOTS,
	armorLabel,
	damageLabel,
	effectLabel,
	EQUIPMENT_SLOTS as SLOTS,
	isEquippable,
	isUsable,
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
	durability: 72,
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

const potion: InventoryItem = {
	...sword,
	itemRef: 'health_potion',
	name: 'Health Potion',
	rarity: 'Common',
	type: 'Consumable',
	slot: null,
	quantity: 4,
	durability: null,
	effect: 'restore',
	effectPower: 12,
	weaponType: null,
	damageMin: null,
	damageMax: null
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
	it('lists the twelve canonical slots the server validates', () => {
		expect(SLOTS.map((slot) => slot.id)).toEqual([
			'MainHand',
			'OffHand',
			'Head',
			'Shoulders',
			'Chest',
			'Bracers',
			'Gloves',
			'Belt',
			'Legs',
			'Feet',
			'Ring',
			'Amulet'
		]);
		expect(EQUIPMENT_SLOTS.length).toBe(12);
	});
});

describe('consumable and combat labels', () => {
	it('only treats items with an effect as usable', () => {
		expect(isUsable(potion)).toBe(true);
		expect(isUsable(sword)).toBe(false);
	});

	it('describes each supported effect', () => {
		expect(effectLabel(potion)).toBe('Restores 12 mana');
		expect(effectLabel({ ...potion, effect: 'heal', effectPower: 25 })).toBe('Restores 25 health');
		expect(effectLabel({ ...potion, effect: 'cure_poison' })).toBe('Cures poison');
		expect(effectLabel(sword)).toBe('No usable effect');
	});

	it('formats weapon damage and armour summaries', () => {
		expect(damageLabel(sword)).toBe('3–8 damage');
		expect(damageLabel(potion)).toBeNull();
		expect(armorLabel({ ...sword, armorClass: 'heavy', armorType: 'heavy' })).toBe(
			'heavy · heavy armour'
		);
		expect(armorLabel(sword)).toBeNull();
	});
});
