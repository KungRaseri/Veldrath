import { describe, expect, it } from 'vitest';
import { CHAT_BUFFER_LIMIT, appendChat, barPercent, describeCombatTurn } from './reducers';
import type { CombatTurnPayload } from './types';

/**
 * Branch-focused companion to `reducers.test.ts`: the flee and unrecognised combat
 * actions, plus the buffer and progress-bar edges.
 */
function turn(overrides: Partial<CombatTurnPayload> = {}): CombatTurnPayload {
	return { action: 'attack', ...overrides } as CombatTurnPayload;
}

describe('describeCombatTurn action switch', () => {
	it('describes a flee attempt', () => {
		expect(describeCombatTurn(turn({ action: 'flee' }))).toContain('You try to escape.');
	});

	it('falls back to naming an unrecognised action', () => {
		expect(describeCombatTurn(turn({ action: 'taunt' }))).toContain('Action: taunt.');
	});

	it('renders a missing action as undefined in the fallback', () => {
		expect(describeCombatTurn(turn({ action: undefined }))).toContain('Action: undefined.');
	});

	it('names what a kill dropped, because loot cannot be inferred from the numbers', () => {
		const line = describeCombatTurn(
			turn({
				enemyDefeated: true,
				itemsDropped: [
					{ itemSlug: 'health-potion', quantity: 1 },
					{ itemSlug: 'iron-ingot', quantity: 2 }
				]
			})
		);

		expect(line).toContain('Found: 1× health-potion, 2× iron-ingot.');
	});

	it('says nothing about loot when nothing dropped', () => {
		expect(describeCombatTurn(turn({ enemyDefeated: true }))).not.toContain('Found:');
		expect(describeCombatTurn(turn({ itemsDropped: null }))).not.toContain('Found:');
	});
});

describe('describeCombatTurn ability detail', () => {
	it('lists damage, healing, and mana for a full ability turn', () => {
		const line = describeCombatTurn(
			turn({
				action: 'ability',
				abilityId: 'fireball',
				abilityDamage: 12,
				healthRestored: 4,
				manaCost: 6
			})
		);

		expect(line).toContain('You used fireball.');
		expect(line).toContain('It dealt 12 damage.');
		expect(line).toContain('It restored 4 HP.');
		expect(line).toContain('Mana spent: 6.');
	});

	it('omits the detail lines for a bare ability turn', () => {
		expect(describeCombatTurn(turn({ action: 'ability' }))).toBe('You used an ability.');
	});
});

describe('describeCombatTurn enemy damage', () => {
	it('names the enemy ability when one was used', () => {
		expect(describeCombatTurn(turn({ enemyDamage: 7, enemyAbilityUsed: 'bite' }))).toContain(
			'bite hits you for 7 damage.'
		);
	});

	it('uses a generic enemy line when no ability was named', () => {
		expect(describeCombatTurn(turn({ enemyDamage: 7 }))).toContain(
			'The enemy hits you for 7 damage.'
		);
	});
});

describe('appendChat buffer', () => {
	it('trims the oldest entry beyond the limit', () => {
		const messages = Array.from({ length: CHAT_BUFFER_LIMIT }, (_, index) => ({
			channel: 'Zone',
			sender: 'Tester',
			message: `line ${index}`,
			timestamp: '2026-01-01T00:00:00Z'
		}));

		const next = appendChat(messages, {
			channel: 'Zone',
			sender: 'Tester',
			message: 'newest',
			timestamp: '2026-01-01T00:00:01Z'
		});

		expect(next).toHaveLength(CHAT_BUFFER_LIMIT);
		expect(next.at(-1)?.message).toBe('newest');
		expect(next[0]?.message).toBe('line 1');
	});
});

describe('barPercent guards', () => {
	it('returns zero without a value, a maximum, or a positive maximum', () => {
		expect(barPercent(undefined, 10)).toBe(0);
		expect(barPercent(5, undefined)).toBe(0);
		expect(barPercent(5, 0)).toBe(0);
	});
});
