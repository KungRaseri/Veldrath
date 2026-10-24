import { describe, expect, it } from 'vitest';
import type { PointBuyConfig } from './creation-types';
import {
	STAT_NAMES,
	canDecrease,
	canIncrease,
	decreaseAllocation,
	increaseAllocation,
	initialAllocation,
	pointsRemaining,
	spentPoints,
	type StatAllocation
} from './point-buy';

const config: PointBuyConfig = {
	totalPoints: 27,
	minStatValue: 8,
	maxStatValue: 15,
	costTable: { '8': 0, '9': 1, '10': 2, '11': 3, '12': 4, '13': 5, '14': 7, '15': 9 }
};

describe('initialAllocation', () => {
	it('starts every stat at the configured minimum', () => {
		const allocation = initialAllocation(config);

		for (const stat of STAT_NAMES) {
			expect(allocation[stat]).toBe(8);
		}
	});

	it('falls back to 8 when no configuration is available', () => {
		expect(initialAllocation(null).Strength).toBe(8);
	});
});

describe('spentPoints and pointsRemaining', () => {
	it('returns the full budget for a fresh allocation', () => {
		expect(pointsRemaining(config, initialAllocation(config))).toBe(27);
	});

	it('subtracts cumulative stat costs', () => {
		const allocation: StatAllocation = { ...initialAllocation(config), Strength: 10 };

		expect(spentPoints(config, allocation)).toBe(2);
		expect(pointsRemaining(config, allocation)).toBe(25);
	});

	it('returns zero when no configuration is available', () => {
		expect(pointsRemaining(null, initialAllocation(null))).toBe(0);
	});
});

describe('increaseAllocation', () => {
	it('spends the marginal cost of the next point', () => {
		const allocation = increaseAllocation(config, initialAllocation(config), 'Strength');

		expect(allocation.Strength).toBe(9);
		expect(pointsRemaining(config, allocation)).toBe(26);
	});

	it('does not exceed the configured maximum', () => {
		const maxed: StatAllocation = { ...initialAllocation(config), Strength: 15 };

		expect(canIncrease(config, maxed, 'Strength')).toBe(false);
		expect(increaseAllocation(config, maxed, 'Strength')).toBe(maxed);
	});

	it('does not overspend the budget', () => {
		const small: PointBuyConfig = { ...config, totalPoints: 1 };
		let allocation = initialAllocation(small);

		allocation = increaseAllocation(small, allocation, 'Strength');
		expect(allocation.Strength).toBe(9);

		allocation = increaseAllocation(small, allocation, 'Strength');
		expect(allocation.Strength).toBe(9);
	});
});

describe('decreaseAllocation', () => {
	it('refunds the marginal cost of the previous point', () => {
		const raised: StatAllocation = { ...initialAllocation(config), Strength: 10 };
		const lowered = decreaseAllocation(config, raised, 'Strength');

		expect(lowered.Strength).toBe(9);
		expect(pointsRemaining(config, lowered)).toBe(26);
	});

	it('does not go below the configured minimum', () => {
		const minimum = initialAllocation(config);

		expect(canDecrease(config, minimum, 'Wisdom')).toBe(false);
		expect(decreaseAllocation(config, minimum, 'Wisdom')).toBe(minimum);
	});
});
