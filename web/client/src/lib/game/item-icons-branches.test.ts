import { describe, expect, it } from 'vitest';
import { itemIcon, slotIcon, slotLabel } from './item-icons';
import type { InventoryItem } from './types';

/**
 * Branch-focused companion to the component suites: every fallback in the icon and
 * slot-label lookups.
 */
function item(type?: string | null): InventoryItem {
	return {
		itemRef: 'x',
		name: 'X',
		rarity: 'Common',
		type: type as string,
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
		bonuses: {}
	};
}

describe('itemIcon', () => {
	it('falls back to the package icon for a missing or unknown type', () => {
		const fallback = slotIcon('NotASlot');

		expect(itemIcon(item(null))).toBe(fallback);
		expect(itemIcon(item('Relic'))).toBe(fallback);
	});
});

describe('slotIcon and slotLabel', () => {
	it('falls back to the package icon for an unknown slot', () => {
		// Both the unknown type and the unknown slot resolve to the same default icon.
		expect(slotIcon('NotASlot')).toBe(itemIcon(item('Relic')));
		expect(slotIcon('MainHand')).not.toBe(slotIcon('NotASlot'));
	});

	it('falls back to the slot name when there is no label', () => {
		expect(slotLabel('NotASlot')).toBe('NotASlot');
		expect(slotLabel('MainHand')).toBe('Main hand');
	});
});
