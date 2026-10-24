import type { PointBuyConfig } from './creation-types';

/** The six core attributes used by character creation. */
export const STAT_NAMES = [
	'Strength',
	'Dexterity',
	'Constitution',
	'Intelligence',
	'Wisdom',
	'Charisma'
] as const;

/** One of the six core attributes. */
export type StatName = (typeof STAT_NAMES)[number];

/** Point-buy allocation across all six core attributes. */
export type StatAllocation = Record<StatName, number>;

function costFor(config: PointBuyConfig, value: number): number {
	const cost = Number(config.costTable[String(value)]);
	return Number.isFinite(cost) ? cost : 0;
}

/** Builds a starting allocation at the configured minimum value. */
export function initialAllocation(config: PointBuyConfig | null): StatAllocation {
	const min = config?.minStatValue ?? 8;

	return {
		Strength: min,
		Dexterity: min,
		Constitution: min,
		Intelligence: min,
		Wisdom: min,
		Charisma: min
	};
}

/** Total points already spent on the allocation. */
export function spentPoints(config: PointBuyConfig, allocation: StatAllocation): number {
	return STAT_NAMES.reduce((sum, stat) => sum + costFor(config, allocation[stat]), 0);
}

/** Points left in the budget. */
export function pointsRemaining(config: PointBuyConfig | null, allocation: StatAllocation): number {
	if (!config) {
		return 0;
	}

	return config.totalPoints - spentPoints(config, allocation);
}

/** Whether increasing the stat by one fits the budget and the configured cap. */
export function canIncrease(
	config: PointBuyConfig | null,
	allocation: StatAllocation,
	stat: StatName
): boolean {
	if (!config) {
		return false;
	}

	const current = allocation[stat];
	if (current >= config.maxStatValue) {
		return false;
	}

	const delta = costFor(config, current + 1) - costFor(config, current);
	return pointsRemaining(config, allocation) >= delta;
}

/** Whether the stat can be lowered without going below the configured minimum. */
export function canDecrease(
	config: PointBuyConfig | null,
	allocation: StatAllocation,
	stat: StatName
): boolean {
	return Boolean(config) && allocation[stat] > (config?.minStatValue ?? 8);
}

/** Returns a new allocation with the stat increased, or the original when not allowed. */
export function increaseAllocation(
	config: PointBuyConfig | null,
	allocation: StatAllocation,
	stat: StatName
): StatAllocation {
	if (!canIncrease(config, allocation, stat)) {
		return allocation;
	}

	return { ...allocation, [stat]: allocation[stat] + 1 };
}

/** Returns a new allocation with the stat decreased, or the original when not allowed. */
export function decreaseAllocation(
	config: PointBuyConfig | null,
	allocation: StatAllocation,
	stat: StatName
): StatAllocation {
	if (!canDecrease(config, allocation, stat)) {
		return allocation;
	}

	return { ...allocation, [stat]: allocation[stat] - 1 };
}
