import { describe, expect, it } from 'vitest';
import type { PointBuyConfig } from './creation-types';
import {
	canDecrease,
	canIncrease,
	initialAllocation,
	pointsRemaining,
	spentPoints
} from './point-buy';

/**
 * Branch-focused companion to `point-buy.test.ts`: the null-configuration guards and the
 * cost-table fallback for values that were never priced.
 */
const config: PointBuyConfig = {
	totalPoints: 27,
	minStatValue: 8,
	maxStatValue: 15,
	costTable: { '8': 0, '9': 1, '10': 2 }
};

describe('cost lookups', () => {
	it('treats an unpriced value as free', () => {
		expect(spentPoints(config, { ...initialAllocation(config), Strength: 99 })).toBe(0);
	});

	it('treats a non-numeric cost entry as free', () => {
		const broken: PointBuyConfig = { ...config, costTable: { '8': Number.NaN } };

		expect(spentPoints(broken, { ...initialAllocation(config), Strength: 8 })).toBe(0);
	});
});

describe('null configuration guards', () => {
	it('reports no remaining points', () => {
		expect(pointsRemaining(null, initialAllocation(null))).toBe(0);
	});

	it('cannot decrease any stat', () => {
		expect(canDecrease(null, initialAllocation(null), 'Strength')).toBe(false);
	});

	it('cannot increase any stat', () => {
		expect(canIncrease(null, initialAllocation(null), 'Strength')).toBe(false);
	});

	it('falls back to the default minimum when the config omits one', () => {
		const withoutMinimum = { ...config, minStatValue: undefined } as unknown as PointBuyConfig;
		const allocation = { ...initialAllocation(config), Strength: 9 };

		expect(canDecrease(withoutMinimum, allocation, 'Strength')).toBe(true);
	});

	it('uses the default minimum when no config supplies one', () => {
		expect(initialAllocation(null).Charisma).toBe(8);
	});
});

describe('canDecrease', () => {
	it('allows lowering a stat that sits above the minimum', () => {
		const allocation = { ...initialAllocation(config), Strength: 9 };

		expect(canDecrease(config, allocation, 'Strength')).toBe(true);
		expect(canDecrease(config, allocation, 'Dexterity')).toBe(false);
	});
});
