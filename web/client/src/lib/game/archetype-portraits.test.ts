import { describe, expect, it } from 'vitest';
import {
	ARCHETYPE_PORTRAIT_DIR,
	archetypePortraitPath,
	archetypeTierFromTraits,
	silhouetteForTypeKey,
	toArchetypeTier
} from './archetype-portraits';

describe('archetype portrait path', () => {
	it('derives the tile file from the slug and the 128 width', () => {
		expect(archetypePortraitPath('giant-rat', 128)).toBe(
			`${ARCHETYPE_PORTRAIT_DIR}/giant-rat-128.webp`
		);
	});

	it('derives the detail file from the same slug and the 256 width', () => {
		expect(archetypePortraitPath('giant-rat', 256)).toBe(
			`${ARCHETYPE_PORTRAIT_DIR}/giant-rat-256.webp`
		);
	});

	it('has no path when there is no slug, so the caller draws the fallback', () => {
		expect(archetypePortraitPath(null, 128)).toBeNull();
		expect(archetypePortraitPath(undefined, 256)).toBeNull();
		expect(archetypePortraitPath('   ', 128)).toBeNull();
	});

	it('trims a slug rather than building a path with whitespace inside it', () => {
		expect(archetypePortraitPath(' gull-rider-skirmisher ', 128)).toBe(
			`${ARCHETYPE_PORTRAIT_DIR}/gull-rider-skirmisher-128.webp`
		);
	});
});

describe('silhouette class', () => {
	it('maps every seeded type key onto one of the five classes', () => {
		expect(silhouetteForTypeKey('beasts')).toBe('beast');
		expect(silhouetteForTypeKey('humanoids')).toBe('humanoid');
		expect(silhouetteForTypeKey('undead')).toBe('unquiet');
		expect(silhouetteForTypeKey('spirits')).toBe('spirit');
		expect(silhouetteForTypeKey('elementals')).toBe('spirit');
		expect(silhouetteForTypeKey('townspeople')).toBe('townsfolk');
	});

	it('falls back to humanoid for an unknown or absent type key', () => {
		expect(silhouetteForTypeKey('constructs')).toBe('humanoid');
		expect(silhouetteForTypeKey(null)).toBe('humanoid');
	});
});

describe('tier', () => {
	it('reads the tier off the content traits, boss before elite', () => {
		expect(archetypeTierFromTraits({ boss: true, elite: true })).toBe('boss');
		expect(archetypeTierFromTraits({ boss: false, elite: true })).toBe('elite');
		expect(archetypeTierFromTraits({ boss: false, elite: false })).toBe('regular');
	});

	it('treats absent traits and non-boolean flags as regular', () => {
		expect(archetypeTierFromTraits(undefined)).toBe('regular');
		expect(archetypeTierFromTraits(null)).toBe('regular');
		expect(archetypeTierFromTraits({ boss: 'true', elite: 1 })).toBe('regular');
	});

	it('normalises a wire tier and keeps an unknown value unframed', () => {
		expect(toArchetypeTier('boss')).toBe('boss');
		expect(toArchetypeTier('elite')).toBe('elite');
		expect(toArchetypeTier('townsfolk')).toBe('townsfolk');
		expect(toArchetypeTier('legendary')).toBe('regular');
		expect(toArchetypeTier(undefined)).toBe('regular');
	});
});
