import { describe, expect, it } from 'vitest';
import {
	UNCATEGORISED_LABEL,
	applySkillProgress,
	groupSkillsByCategory,
	resolveSkills,
	skillLabel,
	skillProgressPercent,
	unknownSkillLabel
} from './skills';
import type { SkillView, SkillXpGainedPayload } from './types';

function skill(overrides: Partial<SkillView> = {}): SkillView {
	return {
		skillId: 'swordsmanship',
		resolved: true,
		name: 'Swordsmanship',
		description: 'Skill with a blade.',
		category: 'combat',
		governingAttribute: 'strength',
		rank: 2,
		totalXp: 250,
		xpIntoRank: 50,
		xpToNextRank: 100,
		maxRank: 50,
		...overrides
	};
}

describe('resolving the skill list', () => {
	it('drops entries with no skill id', () => {
		const skills = resolveSkills([skill(), { skillId: '' } as SkillView, null as never]);

		expect(skills.map((entry) => entry.skillId)).toEqual(['swordsmanship']);
	});

	it('treats a missing list as nothing practised', () => {
		expect(resolveSkills(null)).toEqual([]);
		expect(resolveSkills(undefined)).toEqual([]);
	});
});

describe('naming a skill', () => {
	it('uses the catalog name', () => {
		expect(skillLabel(skill())).toBe('Swordsmanship');
	});

	it('falls back to an explicit unknown label rather than the key alone', () => {
		expect(skillLabel(skill({ name: '   ' }))).toBe(unknownSkillLabel('swordsmanship'));
		expect(skillLabel(skill({ name: null, resolved: false }))).toBe(
			'Unknown skill (swordsmanship)'
		);
	});
});

describe('skill progress', () => {
	it('reports the percentage of the current rank', () => {
		expect(skillProgressPercent(skill({ xpIntoRank: 50, xpToNextRank: 100 }))).toBe(50);
		expect(skillProgressPercent(skill({ xpIntoRank: 0, xpToNextRank: 100 }))).toBe(0);
		expect(skillProgressPercent(skill({ xpIntoRank: 7, xpToNextRank: 10 }))).toBe(70);
	});

	it('reads as complete when the server sends no rank cost', () => {
		expect(skillProgressPercent(skill({ xpIntoRank: 3, xpToNextRank: 0 }))).toBe(100);
	});

	it('clamps a figure the server should never send', () => {
		expect(skillProgressPercent(skill({ xpIntoRank: 500, xpToNextRank: 100 }))).toBe(100);
		expect(skillProgressPercent(skill({ xpIntoRank: -10, xpToNextRank: 100 }))).toBe(0);
	});
});

describe('grouping the sheet', () => {
	it('groups by capitalised category, alphabetically, with Other last', () => {
		const groups = groupSkillsByCategory([
			skill({ skillId: 'herbalism', name: 'Herbalism', category: 'profession' }),
			skill({ skillId: 'stealth', name: 'Stealth', category: null }),
			skill()
		]);

		expect(groups.map((group) => group.label)).toEqual([
			'Combat',
			'Profession',
			UNCATEGORISED_LABEL
		]);
		expect(groups[0].skills.map((entry) => entry.skillId)).toEqual(['swordsmanship']);
		expect(groups[2].category).toBe('');
	});

	it('returns nothing for an empty sheet', () => {
		expect(groupSkillsByCategory([])).toEqual([]);
	});

	it('keeps two skills of the same category together', () => {
		const groups = groupSkillsByCategory([
			skill(),
			skill({ skillId: 'axes', name: 'Axes', category: 'combat' })
		]);

		expect(groups).toHaveLength(1);
		expect(groups[0].skills).toHaveLength(2);
	});
});

describe('folding an award into the sheet', () => {
	function award(overrides: Partial<SkillXpGainedPayload> = {}): SkillXpGainedPayload {
		return {
			characterId: 'char-1',
			skillId: 'swordsmanship',
			totalXp: 260,
			currentRank: 2,
			rankedUp: false,
			xpIntoRank: 60,
			xpToNextRank: 100,
			...overrides
		};
	}

	it('keeps the catalog copy and takes the numbers from the award', () => {
		const updated = applySkillProgress(skill(), award());

		expect(updated).toMatchObject({
			skillId: 'swordsmanship',
			resolved: true,
			name: 'Swordsmanship',
			category: 'combat',
			governingAttribute: 'strength',
			maxRank: 50,
			rank: 2,
			totalXp: 260,
			xpIntoRank: 60,
			xpToNextRank: 100
		});
	});

	it('builds an unresolved entry for a skill the sheet never had', () => {
		const updated = applySkillProgress(null, award({ skillId: 'herbalism', totalXp: 40 }));

		expect(updated).toMatchObject({
			skillId: 'herbalism',
			resolved: false,
			name: null,
			category: null,
			maxRank: 0,
			totalXp: 40
		});
	});
});
