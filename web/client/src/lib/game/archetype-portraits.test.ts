import { describe, expect, it } from 'vitest';
import {
	ARCHETYPE_PORTRAIT_DIR,
	archetypePortraitPath,
	archetypeSilhouette,
	archetypeTierFromTraits,
	silhouetteForTypeKey,
	toArchetypeTier
} from './archetype-portraits';

describe('archetype portrait path', () => {
	it('derives the 1× tile file from the slug the way §5.2 names it', () => {
		expect(archetypePortraitPath('giant-rat', 64)).toBe(
			`${ARCHETYPE_PORTRAIT_DIR}/archetype-giant-rat-portrait-64.webp`
		);
	});

	it('derives the 2× file from the same slug, with no master and no @2x suffix', () => {
		expect(archetypePortraitPath('giant-rat', 128)).toBe(
			`${ARCHETYPE_PORTRAIT_DIR}/archetype-giant-rat-portrait-128.webp`
		);
	});

	it('has no path when there is no slug, so the caller draws the fallback', () => {
		expect(archetypePortraitPath(null, 64)).toBeNull();
		expect(archetypePortraitPath(undefined, 128)).toBeNull();
		expect(archetypePortraitPath('   ', 64)).toBeNull();
	});

	it('trims a slug rather than building a path with whitespace inside it', () => {
		expect(archetypePortraitPath(' gull-rider-skirmisher ', 128)).toBe(
			`${ARCHETYPE_PORTRAIT_DIR}/archetype-gull-rider-skirmisher-portrait-128.webp`
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

	it('reads the class from the slug and the type key together', () => {
		expect(archetypeSilhouette('giant-rat', 'beasts')).toBe('beast');
		expect(archetypeSilhouette('moor-wight', 'undead')).toBe('unquiet');
		expect(archetypeSilhouette('cinderling', 'spirits')).toBe('spirit');
		expect(archetypeSilhouette('village-blacksmith', 'townspeople')).toBe('townsfolk');
	});

	it('draws goblin-scout as humanoid despite its beasts type key (§4.2)', () => {
		expect(archetypeSilhouette('goblin-scout', 'beasts')).toBe('humanoid');
		expect(silhouetteForTypeKey('beasts')).toBe('beast');
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

	it('gives a townspeople row the plate before any trait flag', () => {
		expect(archetypeTierFromTraits(undefined, 'townspeople')).toBe('townsfolk');
		expect(archetypeTierFromTraits({ boss: true }, 'townspeople')).toBe('townsfolk');
		expect(archetypeTierFromTraits({ elite: true }, 'townspeople')).toBe('townsfolk');
	});

	it('normalises a wire tier and keeps an unknown value unframed', () => {
		expect(toArchetypeTier('boss')).toBe('boss');
		expect(toArchetypeTier('elite')).toBe('elite');
		expect(toArchetypeTier('townsfolk')).toBe('townsfolk');
		expect(toArchetypeTier('legendary')).toBe('regular');
		expect(toArchetypeTier(undefined)).toBe('regular');
	});

	it('upgrades a townspeople wire tier to the plate', () => {
		expect(toArchetypeTier('regular', 'townspeople')).toBe('townsfolk');
		expect(toArchetypeTier(undefined, 'townspeople')).toBe('townsfolk');
		expect(toArchetypeTier('boss', 'beasts')).toBe('boss');
	});
});
