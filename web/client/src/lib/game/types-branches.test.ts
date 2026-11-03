import { describe, expect, it } from 'vitest';
import { effectLabel, itemSubtitle } from './types';
import type { InventoryItem } from './types';

/**
 * Branch-focused companion to `types.test.ts`: the item presentation helpers whose
 * fallbacks (empty subtitle, unrecognised effect) the original suite never hits.
 */
function item(overrides: Partial<InventoryItem> = {}): InventoryItem {
	return {
		itemRef: 'bronze-sword',
		name: 'Bronze Sword',
		rarity: 'Common',
		type: 'Weapon',
		quantity: 1,
		stackSize: 1,
		isStackable: false,
		isEquipped: false,
		price: 1,
		weight: 1,
		effectPower: 0,
		isTwoHanded: false,
		requirementsLevel: 1,
		requirements: {},
		bonuses: {},
		...overrides
	};
}

describe('itemSubtitle', () => {
	it('joins rarity, type, and durability when all are present', () => {
		expect(itemSubtitle(item({ rarity: 'Rare', type: 'Weapon', durability: 80 }))).toBe(
			'Rare · Weapon · 80% durability'
		);
	});

	it('falls back to a placeholder when nothing is known', () => {
		expect(itemSubtitle(item({ rarity: '', type: '' }))).toBe('Item');
	});
});

describe('effectLabel', () => {
	it('names no effect for an unrecognised effect key', () => {
		expect(effectLabel(item({ effect: 'teleport' }))).toBe('No usable effect');
	});

	it('names no effect when the item has none', () => {
		expect(effectLabel(item({ effect: '' }))).toBe('No usable effect');
	});

	it('describes a heal-over-time consumable', () => {
		expect(effectLabel(item({ effect: 'heal_overtime', effectPower: 9 }))).toBe(
			'Restores about 4 health over time'
		);
	});

	it('describes the remaining recognised effects', () => {
		expect(effectLabel(item({ effect: 'heal', effectPower: 12 }))).toBe('Restores 12 health');
		expect(effectLabel(item({ effect: 'restore', effectPower: 5 }))).toBe('Restores 5 mana');
		expect(effectLabel(item({ effect: 'cure_poison' }))).toBe('Cures poison');
	});
});
