import { describe, expect, it } from 'vitest';
import {
	appendChat,
	applyAbilityToStats,
	applyCombatTurnToStats,
	applyDamageToStats,
	applyRestToStats,
	barPercent,
	describeCombatTurn,
	outcomeFromCombatEndReason
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

	it('names a level-up won by the kill', () => {
		const text = describeCombatTurn({
			action: 'attack',
			enemyDefeated: true,
			xpEarned: 10,
			newLevel: 6,
			leveledUp: true
		});

		expect(text).toContain('+10 XP.');
		expect(text).toContain('Level up! You are now level 6.');
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

	it('takes the server level and pools after a levelling kill instead of adding the award', () => {
		// The kill crosses the level-2 threshold: the server banks the remainder and rewrites the pools. Adding the
		// 10 earned XP to the old 90 would show "100 experience at level 2" and keep the level-1 health bar.
		const next = applyCombatTurnToStats(
			{ ...stats, level: 1, experience: 90, currentHealth: 90, maxHealth: 120 },
			{
				action: 'attack',
				enemyDefeated: true,
				playerRemainingHealth: 90,
				xpEarned: 10,
				newLevel: 2,
				newExperience: 0,
				leveledUp: true,
				maxHealth: 132,
				currentHealth: 90,
				maxMana: 22,
				currentMana: 30,
				unspentAttributePoints: 4
			}
		);

		expect(next.level).toBe(2);
		expect(next.experience).toBe(0);
		expect(next.maxHealth).toBe(132);
		expect(next.currentHealth).toBe(90);
		expect(next.maxMana).toBe(22);
		expect(next.unspentAttributePoints).toBe(4);
	});

	it('keeps the level on a kill that does not cross the threshold', () => {
		const next = applyCombatTurnToStats(
			{ ...stats, level: 1, experience: 0, currentHealth: 90 },
			{
				action: 'attack',
				enemyDefeated: true,
				playerRemainingHealth: 90,
				xpEarned: 10,
				newLevel: 1,
				newExperience: 10,
				leveledUp: false
			}
		);

		expect(next.level).toBe(1);
		expect(next.experience).toBe(10);
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

describe('outcomeFromCombatEndReason', () => {
	it('summarises the two endings the player caused', () => {
		expect(outcomeFromCombatEndReason('defeated')?.kind).toBe('defeat');
		expect(outcomeFromCombatEndReason('Fled')?.kind).toBe('fled');
	});

	it('does not read a shared kill as a death', () => {
		// Another character finished the fight. The character is still standing and lost nothing, so there is no
		// summary to draw — and reporting it as a defeat is what the old `=== 'fled' ? fled : defeat` mapping did.
		expect(outcomeFromCombatEndReason('shared kill')).toBeNull();
		expect(outcomeFromCombatEndReason('a reason this client has never seen')).toBeNull();
	});
});
