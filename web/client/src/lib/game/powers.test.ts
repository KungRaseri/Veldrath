import { describe, expect, it } from 'vitest';
import {
	combatUsageReason,
	outOfCombatUsageReason,
	powerFacts,
	powerLabel,
	powerSummary,
	resolvePowers,
	unresolvedPower,
	type PowerContext
} from './powers';
import type { LearnedPower } from './types';

function fireball(overrides: Partial<LearnedPower> = {}): LearnedPower {
	return {
		slug: 'fireball',
		resolved: true,
		displayName: 'Fireball',
		description: 'A bead of fire that bursts on impact.',
		school: 'fire',
		powerType: 'Spell',
		effectType: 'Damage',
		manaCost: 12,
		cooldown: 3,
		baseDamage: '8d6',
		damageType: 'fire',
		tier: 3,
		requiredLevel: 5,
		...overrides
	};
}

function context(overrides: Partial<PowerContext> = {}): PowerContext {
	return { mana: 20, level: 5, inCombat: false, dead: false, ...overrides };
}

describe('resolving learned powers', () => {
	it('returns nothing without a character', () => {
		expect(resolvePowers(null)).toEqual([]);
	});

	it('prefers the resolved powers the server sent', () => {
		const powers = resolvePowers({
			learnedPowers: [fireball()],
			learnedAbilities: ['fireball']
		});

		expect(powers).toHaveLength(1);
		expect(powers[0].displayName).toBe('Fireball');
	});

	it('drops entries that carry no slug', () => {
		const powers = resolvePowers({
			learnedPowers: [fireball(), { slug: '' } as LearnedPower]
		});

		expect(powers.map((power) => power.slug)).toEqual(['fireball']);
	});

	it('falls back to placeholders built from the slug list', () => {
		const powers = resolvePowers({ learnedAbilities: [' heal ', '', 'cleave'] });

		expect(powers.map((power) => power.slug)).toEqual(['heal', 'cleave']);
		expect(powers.every((power) => power.resolved === false)).toBe(true);
	});

	it('treats a missing learned list as nothing learned', () => {
		expect(resolvePowers({})).toEqual([]);
	});

	it('builds an unresolved placeholder with zeroed numbers', () => {
		expect(unresolvedPower('ghost')).toEqual({
			slug: 'ghost',
			resolved: false,
			powerType: '',
			effectType: '',
			manaCost: 0,
			cooldown: 0,
			tier: 0,
			requiredLevel: 0
		});
	});
});

describe('naming a power', () => {
	it('uses the display name', () => {
		expect(powerLabel(fireball())).toBe('Fireball');
	});

	it('falls back to an explicit unknown label rather than the slug alone', () => {
		expect(powerLabel(fireball({ displayName: '   ' }))).toBe('Unknown ability (fireball)');
		expect(powerLabel(unresolvedPower('ghost'))).toBe('Unknown ability (ghost)');
	});
});

describe('power facts', () => {
	it('lists nothing for an unresolved power', () => {
		expect(powerFacts(unresolvedPower('ghost'))).toEqual([]);
	});

	it('lists school, type, effect, cost, cooldown and damage in reading order', () => {
		const facts = powerFacts(fireball());

		expect(facts).toEqual([
			{ label: 'School', value: 'Fire' },
			{ label: 'Type', value: 'Spell' },
			{ label: 'Effect', value: 'Damage' },
			{ label: 'Mana', value: '12' },
			{ label: 'Cooldown', value: '3 turns' },
			{ label: 'Power', value: '8d6 fire' },
			{ label: 'Requires', value: 'Level 5' }
		]);
	});

	it('says free and none instead of printing zeroes', () => {
		const facts = powerFacts(
			fireball({
				school: null,
				effectType: 'None',
				manaCost: 0,
				cooldown: 0,
				baseDamage: null,
				requiredLevel: 1
			})
		);

		expect(facts).toEqual([
			{ label: 'Type', value: 'Spell' },
			{ label: 'Mana', value: 'Free' },
			{ label: 'Cooldown', value: 'None' }
		]);
	});

	it('uses the singular for a one-turn cooldown', () => {
		const facts = powerFacts(fireball({ cooldown: 1 }));

		expect(facts).toContainEqual({ label: 'Cooldown', value: '1 turn' });
	});

	it('falls back to the effect value when there is no damage dice', () => {
		const facts = powerFacts(
			fireball({ baseDamage: null, baseEffectValue: '1d8+WIS', damageType: null })
		);

		expect(facts).toContainEqual({ label: 'Power', value: '1d8+WIS' });
	});

	it('lists range, duration and targets when the catalog sets them', () => {
		const facts = powerFacts(fireball({ range: 30, duration: 2, maxTargets: 4 }));

		expect(facts).toContainEqual({ label: 'Range', value: '30' });
		expect(facts).toContainEqual({ label: 'Duration', value: '2 turns' });
		expect(facts).toContainEqual({ label: 'Targets', value: '4' });
	});
});

describe('power summary', () => {
	it('names the cost and the cooldown', () => {
		expect(powerSummary(fireball())).toBe('12 mana · 3 turn cooldown');
	});

	it('says no cost or cooldown when they are zero', () => {
		expect(powerSummary(fireball({ manaCost: 0, cooldown: 0 }))).toBe('No mana cost · No cooldown');
	});

	it('stays empty for an unresolved power so the label is not repeated', () => {
		expect(powerSummary(unresolvedPower('ghost'))).toBe('');
	});
});

describe('using a power outside combat', () => {
	it('allows it when nothing blocks it', () => {
		expect(
			outOfCombatUsageReason(fireball({ manaCost: 5, requiredLevel: 1 }), context())
		).toBeNull();
	});

	it('refuses a power the catalog could not resolve', () => {
		expect(outOfCombatUsageReason(unresolvedPower('ghost'), context())).toBe(
			'The catalog has no entry for this ability.'
		);
	});

	it('refuses while dead', () => {
		expect(outOfCombatUsageReason(fireball(), context({ dead: true }))).toBe(
			'You cannot use abilities while dead.'
		);
	});

	it('sends the player to the combat panel mid-fight', () => {
		expect(outOfCombatUsageReason(fireball(), context({ inCombat: true }))).toBe(
			'Finish the fight first — use it from the combat panel.'
		);
	});

	it('refuses a power above the character level', () => {
		expect(outOfCombatUsageReason(fireball(), context({ level: 2 }))).toBe(
			'Requires level 5; you are level 2.'
		);
	});

	it('refuses a power the character cannot pay for', () => {
		expect(outOfCombatUsageReason(fireball(), context({ mana: 4 }))).toBe(
			'Needs 12 mana; you have 4.'
		);
	});

	it('reports the level before the mana, so the message names the lasting problem', () => {
		expect(outOfCombatUsageReason(fireball(), context({ level: 1, mana: 0 }))).toBe(
			'Requires level 5; you are level 1.'
		);
	});
});

describe('using a power in combat', () => {
	it('allows it mid-fight when mana covers the cost', () => {
		expect(combatUsageReason(fireball(), context({ inCombat: true }))).toBeNull();
	});

	it('refuses an unresolved power', () => {
		expect(combatUsageReason(unresolvedPower('ghost'), context({ inCombat: true }))).toBe(
			'The catalog has no entry for this ability.'
		);
	});

	it('refuses between fights', () => {
		expect(combatUsageReason(fireball(), context())).toBe('Not in combat.');
	});

	it('refuses while dead', () => {
		expect(combatUsageReason(fireball(), context({ inCombat: true, dead: true }))).toBe(
			'You cannot act while dead.'
		);
	});

	it('refuses when mana is short', () => {
		expect(combatUsageReason(fireball(), context({ inCombat: true, mana: 2 }))).toBe(
			'Needs 12 mana; you have 2.'
		);
	});

	it('does not re-check the level requirement in combat', () => {
		expect(combatUsageReason(fireball(), context({ inCombat: true, level: 1 }))).toBeNull();
	});
});
