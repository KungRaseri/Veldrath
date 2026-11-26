import type {
	AbilityUsedPayload,
	CharacterRestedPayload,
	CharacterStats,
	ChatMessage,
	CombatOutcome,
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
/**
 * Derives how a fight ended from the caller's own turn payload, or `null` while it is still running.
 *
 * Only the caller knows these numbers, which is why the summary is built here and not from the
 * shared snapshot broadcast to the location.
 */
export function outcomeFrom(turn: CombatTurnPayload): CombatOutcome | null {
	if (turn.enemyDefeated) {
		return {
			kind: 'victory',
			xpEarned: turn.xpEarned ?? 0,
			goldEarned: turn.goldEarned ?? 0,
			xpLost: 0,
			goldLost: 0,
			hardcore: false
		};
	}

	if (turn.playerDefeated) {
		return {
			kind: 'defeat',
			xpEarned: 0,
			goldEarned: 0,
			xpLost: turn.xpLost ?? 0,
			goldLost: turn.goldLost ?? 0,
			hardcore: turn.playerHardcoreDeath ?? false
		};
	}

	return null;
}

/**
 * Derives the summary a `CombatEnded` reason deserves when the caller's own turn did not record one.
 *
 * `defeated` and `fled` are the two endings the player caused and are the only ones summarised. A reason the
 * client does not know ends the fight without a win and without a death — today that is `shared kill`, when
 * another character lands the killing blow on the enemy you were fighting and the server releases your session
 * with everyone else's. Reading an unknown reason as a defeat (the old `reason === 'fled' ? fled : defeat`)
 * would tell a character who is still standing, and lost nothing, that they were defeated.
 *
 * @param reason The reason the server named on `CombatEnded`.
 * @returns The matching outcome, or `null` when the ending was neither a victory nor a death.
 */
export function outcomeFromCombatEndReason(reason: string): CombatOutcome | null {
	const normalised = reason.trim().toLowerCase();

	if (normalised === 'fled') {
		return { kind: 'fled', xpEarned: 0, goldEarned: 0, xpLost: 0, goldLost: 0, hardcore: false };
	}

	if (normalised === 'defeated') {
		return { kind: 'defeat', xpEarned: 0, goldEarned: 0, xpLost: 0, goldLost: 0, hardcore: false };
	}

	return null;
}

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
	if (turn.leveledUp) parts.push(`Level up! You are now level ${turn.newLevel}.`);
	if (turn.goldEarned) parts.push(`+${turn.goldEarned} gold.`);

	// Loot is the one reward the player cannot infer from the numbers, so it is named.
	const dropped = turn.itemsDropped ?? [];
	if (dropped.length > 0) {
		parts.push(`Found: ${dropped.map((drop) => `${drop.quantity}× ${drop.itemSlug}`).join(', ')}.`);
	}
	if (turn.playerDefeated) parts.push('You have been defeated.');

	return parts.join(' ');
}

/** Applies a combat turn to the header stats. */
export function applyCombatTurnToStats(
	stats: CharacterStats,
	turn: CombatTurnPayload
): CharacterStats {
	// A kill that levels is banked server-side: `newExperience` is the remainder toward the next level, not the
	// old total plus the award. Adding `xpEarned` would double-count the crossing and leave `level` behind, which
	// is exactly the stale sheet this payload exists to prevent. Older servers omit the field, so the arithmetic
	// stays the fallback for a turn that carries no progression.
	const newLevel = turn.newLevel ?? null;
	const levelled = newLevel !== null;

	return {
		...stats,
		currentHealth: turn.playerRemainingHealth ?? turn.currentHealth ?? stats.currentHealth,
		currentMana: turn.playerRemainingMana ?? turn.currentMana ?? stats.currentMana,
		maxHealth: turn.maxHealth ?? stats.maxHealth,
		maxMana: turn.maxMana ?? stats.maxMana,
		level: levelled ? newLevel : stats.level,
		experience: levelled
			? (turn.newExperience ?? stats.experience)
			: stats.experience + (turn.xpEarned ?? 0),
		unspentAttributePoints: turn.unspentAttributePoints ?? stats.unspentAttributePoints,
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
