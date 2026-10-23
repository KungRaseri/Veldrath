import type {
	AbilityUsedPayload,
	CharacterRestedPayload,
	CharacterStats,
	ChatMessage,
	CombatTurnPayload
} from './types';

/** Maximum number of chat lines kept in the client buffer. */
export const CHAT_BUFFER_LIMIT = 200;

/** Appends a chat message and trims the oldest entries beyond the buffer limit. */
export function appendChat(
	messages: ChatMessage[],
	message: ChatMessage,
	limit = CHAT_BUFFER_LIMIT
): ChatMessage[] {
	const next = [...messages, message];
	return next.length > limit ? next.slice(next.length - limit) : next;
}

/** Builds the human-readable log line for a resolved combat turn. */
export function describeCombatTurn(turn: CombatTurnPayload): string {
	const parts: string[] = [];

	switch (turn.action?.toLowerCase()) {
		case 'attack':
			parts.push(`You hit for ${turn.playerDamage ?? 0} damage.`);
			break;
		case 'defend':
			parts.push('You brace for the incoming attack.');
			break;
		case 'ability':
			parts.push(`You used ${turn.abilityId ?? 'an ability'}.`);
			if (turn.abilityDamage) parts.push(`It dealt ${turn.abilityDamage} damage.`);
			if (turn.healthRestored) parts.push(`It restored ${turn.healthRestored} HP.`);
			if (turn.manaCost) parts.push(`Mana spent: ${turn.manaCost}.`);
			break;
		case 'flee':
			parts.push('You try to escape.');
			break;
		default:
			parts.push(`Action: ${turn.action}.`);
	}

	if (turn.enemyDefeated) parts.push('The enemy is defeated!');
	if (turn.enemyDamage) {
		parts.push(
			`${turn.enemyAbilityUsed ? `${turn.enemyAbilityUsed} hits` : 'The enemy hits'} you for ${turn.enemyDamage} damage.`
		);
	}
	if (turn.xpEarned) parts.push(`+${turn.xpEarned} XP.`);
	if (turn.goldEarned) parts.push(`+${turn.goldEarned} gold.`);
	if (turn.playerDefeated) parts.push('You have been defeated.');

	return parts.join(' ');
}

/** Applies a combat turn to the header stats. */
export function applyCombatTurnToStats(
	stats: CharacterStats,
	turn: CombatTurnPayload
): CharacterStats {
	return {
		...stats,
		currentHealth: turn.playerRemainingHealth ?? stats.currentHealth,
		currentMana: turn.playerRemainingMana ?? stats.currentMana,
		experience: stats.experience + (turn.xpEarned ?? 0),
		gold: stats.gold + (turn.goldEarned ?? 0)
	};
}

/** Applies a rest result to the header stats. */
export function applyRestToStats(
	stats: CharacterStats,
	rest: CharacterRestedPayload
): CharacterStats {
	return {
		...stats,
		currentHealth: rest.currentHealth,
		maxHealth: rest.maxHealth,
		currentMana: rest.currentMana,
		maxMana: rest.maxMana,
		gold: rest.goldRemaining
	};
}

/** Applies a mana/heal ability result to the header stats. */
export function applyAbilityToStats(
	stats: CharacterStats,
	ability: AbilityUsedPayload
): CharacterStats {
	return {
		...stats,
		currentMana: ability.remainingMana,
		currentHealth: Math.min(stats.maxHealth, stats.currentHealth + ability.healthRestored)
	};
}

/** Applies a damage-taken payload to the header stats. */
export function applyDamageToStats(
	stats: CharacterStats,
	damage: { currentHP: number; maxHP: number }
): CharacterStats {
	return { ...stats, currentHealth: damage.currentHP, maxHealth: damage.maxHP };
}

/** Clamps a value to a percentage of its maximum for progress bars. */
export function barPercent(value: number | undefined, max: number | undefined): number {
	if (!value || !max || max <= 0) return 0;
	return Math.min(100, Math.max(0, Math.round((value / max) * 100)));
}
