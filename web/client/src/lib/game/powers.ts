import type { LearnedPower } from './types';

/**
 * Pure helpers for rendering learned powers.
 *
 * The server sends resolved catalog entries on `CharacterSelected.learnedPowers`. Everything the
 * panels need to decide — what to call a power, whether the player can use it, which facts to list —
 * is decided here so the components stay markup, and so the rules can be tested without a DOM.
 */

/** The character state the availability rules depend on. */
export interface PowerContext {
	/** Mana the character has right now. */
	mana: number;
	/** Character level, for the catalog level requirement. */
	level: number;
	/** True while a turn-based fight is running. */
	inCombat: boolean;
	/** True after a death the player has not respawned from. */
	dead: boolean;
}

/** Names a slug the server could not resolve, without pretending the slug is the name. */
export function unknownPowerLabel(slug: string): string {
	return `Unknown ability (${slug})`;
}

/** Builds the unresolved placeholder the panels render when the catalog has no entry. */
export function unresolvedPower(slug: string): LearnedPower {
	return {
		slug,
		resolved: false,
		powerType: '',
		effectType: '',
		manaCost: 0,
		cooldown: 0,
		tier: 0,
		requiredLevel: 0
	};
}

/**
 * The powers to render: the server's resolved list when it sent one, otherwise placeholders built
 * from the raw slug list, so an older server still lists what the character knows.
 */
export function resolvePowers(
	character: {
		learnedPowers?: LearnedPower[] | null;
		learnedAbilities?: string[] | null;
	} | null
): LearnedPower[] {
	if (!character) return [];

	const resolved = character.learnedPowers ?? [];
	if (resolved.length > 0) return resolved.filter((power) => Boolean(power?.slug));

	return (character.learnedAbilities ?? [])
		.filter((slug): slug is string => typeof slug === 'string' && slug.trim().length > 0)
		.map((slug) => unresolvedPower(slug.trim()));
}

/** The player-facing name, falling back to an explicit unknown label. */
export function powerLabel(power: LearnedPower): string {
	const name = power.displayName?.trim();
	return name && name.length > 0 ? name : unknownPowerLabel(power.slug);
}

/** One fact about a power, ready to render as a label/value pair. */
export interface PowerFact {
	label: string;
	value: string;
}

function capitalize(value: string): string {
	return value.length === 0 ? value : value[0].toUpperCase() + value.slice(1);
}

/** The facts the detail view lists, in reading order. Empty for an unresolved power. */
export function powerFacts(power: LearnedPower): PowerFact[] {
	if (!power.resolved) return [];

	const facts: PowerFact[] = [];

	if (power.school) facts.push({ label: 'School', value: capitalize(power.school) });
	if (power.powerType) facts.push({ label: 'Type', value: power.powerType });
	if (power.effectType && power.effectType !== 'None') {
		facts.push({ label: 'Effect', value: power.effectType });
	}

	facts.push({ label: 'Mana', value: power.manaCost > 0 ? String(power.manaCost) : 'Free' });
	facts.push({
		label: 'Cooldown',
		value:
			power.cooldown > 0 ? `${power.cooldown} ${power.cooldown === 1 ? 'turn' : 'turns'}` : 'None'
	});

	const damage = power.baseDamage ?? power.baseEffectValue;
	if (damage) {
		facts.push({
			label: 'Power',
			value: power.damageType ? `${damage} ${power.damageType}` : damage
		});
	}

	if (power.range != null) facts.push({ label: 'Range', value: String(power.range) });
	if (power.duration != null) facts.push({ label: 'Duration', value: `${power.duration} turns` });
	if (power.maxTargets != null) facts.push({ label: 'Targets', value: String(power.maxTargets) });
	if (power.requiredLevel > 1) {
		facts.push({ label: 'Requires', value: `Level ${power.requiredLevel}` });
	}

	return facts;
}

/**
 * A one-line summary for a button tooltip: the cost and the cooldown, nothing invented.
 *
 * Empty for an unresolved power — the label already carries the unknown name and the reason says
 * why it is unavailable, so repeating it here would print the same sentence twice.
 */
export function powerSummary(power: LearnedPower): string {
	if (!power.resolved) return '';

	const parts = [power.manaCost > 0 ? `${power.manaCost} mana` : 'No mana cost'];
	parts.push(power.cooldown > 0 ? `${power.cooldown} turn cooldown` : 'No cooldown');
	return parts.join(' · ');
}

/**
 * Why the player cannot use this power out of combat right now, or `null` when they can.
 *
 * The first applicable reason wins, so the message names the thing the player can act on.
 */
export function outOfCombatUsageReason(power: LearnedPower, context: PowerContext): string | null {
	if (!power.resolved) return 'The catalog has no entry for this ability.';
	if (context.dead) return 'You cannot use abilities while dead.';
	if (context.inCombat) return 'Finish the fight first — use it from the combat panel.';
	if (power.requiredLevel > context.level) {
		return `Requires level ${power.requiredLevel}; you are level ${context.level}.`;
	}
	if (power.manaCost > context.mana) {
		return `Needs ${power.manaCost} mana; you have ${context.mana}.`;
	}

	return null;
}

/**
 * Why the player cannot use this power in combat right now, or `null` when they can.
 *
 * The level rule is deliberately absent: the server already refuses a power above the character's
 * level when it was learned, and re-checking it here would disable a working button after a
 * level-up reset rather than a real refusal.
 */
export function combatUsageReason(power: LearnedPower, context: PowerContext): string | null {
	if (!power.resolved) return 'The catalog has no entry for this ability.';
	if (!context.inCombat) return 'Not in combat.';
	if (context.dead) return 'You cannot act while dead.';
	if (power.manaCost > context.mana) {
		return `Needs ${power.manaCost} mana; you have ${context.mana}.`;
	}

	return null;
}
