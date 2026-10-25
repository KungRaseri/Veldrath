import { describe, expect, it } from 'vitest';
import {
	appendChat,
	applyAbilityToStats,
	applyCombatTurnToStats,
	applyDamageToStats,
	applyRestToStats,
	barPercent,
	describeCombatTurn
} from './reducers';
import type { CharacterStats, ChatMessage } from './types';

const stats: CharacterStats = {
	currentHealth: 80,
	maxHealth: 100,
	currentMana: 30,
	maxMana: 50,
	gold: 25,
	level: 2,
	experience: 120,
	unspentAttributePoints: 1
};

function chat(message: string): ChatMessage {
	return { channel: 'Zone', sender: 'Tester', message, timestamp: '2026-01-01T00:00:00Z' };
}

describe('appendChat', () => {
	it('appends messages in order', () => {
		const result = appendChat([chat('one')], chat('two'));
		expect(result.map((entry) => entry.message)).toEqual(['one', 'two']);
	});

	it('trims the oldest messages beyond the limit', () => {
		const messages = [chat('one'), chat('two'), chat('three')];
		const result = appendChat(messages, chat('four'), 3);
		expect(result.map((entry) => entry.message)).toEqual(['two', 'three', 'four']);
	});
});

describe('describeCombatTurn', () => {
	it('summarizes an attack with rewards', () => {
		const text = describeCombatTurn({
			action: 'attack',
			playerDamage: 12,
			enemyDamage: 3,
			enemyAbilityUsed: 'Claw',
			xpEarned: 10,
			goldEarned: 4
		});

		expect(text).toContain('You hit for 12 damage.');
		expect(text).toContain('Claw hits you for 3 damage.');
		expect(text).toContain('+10 XP.');
		expect(text).toContain('+4 gold.');
	});

	it('summarizes an ability with heal and mana', () => {
		const text = describeCombatTurn({
			action: 'ability',
			abilityId: 'heal',
			abilityDamage: 0,
			healthRestored: 9,
			manaCost: 5
		});

		expect(text).toContain('You used heal.');
		expect(text).toContain('It restored 9 HP.');
		expect(text).toContain('Mana spent: 5.');
	});

	it('marks defeats', () => {
		const text = describeCombatTurn({
			action: 'attack',
			playerDamage: 99,
			enemyDefeated: true
		});
		expect(text).toContain('The enemy is defeated!');
	});
});

describe('stat reducers', () => {
	it('applies combat turn health, mana, XP, and gold', () => {
		const next = applyCombatTurnToStats(stats, {
			action: 'attack',
			playerRemainingHealth: 70,
			playerRemainingMana: 22,
			xpEarned: 30,
			goldEarned: 5
		});

		expect(next.currentHealth).toBe(70);
		expect(next.currentMana).toBe(22);
		expect(next.experience).toBe(150);
		expect(next.gold).toBe(30);
	});

	it('applies rest results', () => {
		const next = applyRestToStats(stats, {
			locationId: 'the-gilded-flagon',
			currentHealth: 100,
			maxHealth: 100,
			currentMana: 50,
			maxMana: 50,
			goldRemaining: 15
		});

		expect(next).toMatchObject({ currentHealth: 100, currentMana: 50, gold: 15 });
	});

	it('applies ability mana and clamps healing to max health', () => {
		const next = applyAbilityToStats(stats, {
			abilityId: 'heal',
			manaCost: 5,
			remainingMana: 25,
			healthRestored: 50
		});

		expect(next.currentMana).toBe(25);
		expect(next.currentHealth).toBe(stats.maxHealth);
	});

	it('applies damage-taken results', () => {
		const next = applyDamageToStats(stats, { currentHP: 55, maxHP: 120 });
		expect(next.currentHealth).toBe(55);
		expect(next.maxHealth).toBe(120);
	});
});

describe('barPercent', () => {
	it('clamps and rounds', () => {
		expect(barPercent(50, 100)).toBe(50);
		expect(barPercent(120, 100)).toBe(100);
		expect(barPercent(-5, 100)).toBe(0);
		expect(barPercent(undefined, 100)).toBe(0);
		expect(barPercent(1, 0)).toBe(0);
	});
});
