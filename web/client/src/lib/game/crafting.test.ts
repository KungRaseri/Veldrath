import { describe, expect, it } from 'vitest';
import {
	craftBlockedReason,
	craftedSummary,
	materialLine,
	materialSatisfied,
	partitionRecipes,
	recipeFacts,
	recipeLabel
} from './crafting';
import type { CraftableRecipe, ItemCraftedPayload } from './types';

function recipe(overrides: Partial<CraftableRecipe> = {}): CraftableRecipe {
	return {
		slug: 'iron-longsword',
		name: 'Iron longsword',
		description: 'A plain blade.',
		category: 'weapons',
		requiredLevel: 3,
		requiredSkill: 'blacksmithing',
		requiredSkillLevel: 2,
		skillRank: 1,
		requiredStation: 'blacksmith_forge',
		requiredStationTier: 1,
		materials: [
			{ itemRef: '@items/materials/ingots:iron-ingot', required: 2, owned: 5 },
			{ itemRef: '@items/materials/handles:oak', required: 1, owned: 0 }
		],
		outputItemRef: '@items/weapons/swords:iron-longsword',
		outputQuantity: 1,
		experienceGained: 15,
		available: false,
		unavailableReason: 'Short of 1 more oak.',
		...overrides
	};
}

describe('naming a recipe', () => {
	it('uses the name, falling back to the slug only when it is blank', () => {
		expect(recipeLabel(recipe())).toBe('Iron longsword');
		expect(recipeLabel(recipe({ name: '   ' }))).toBe('iron-longsword');
	});
});

describe('reading materials', () => {
	it('renders have over need', () => {
		expect(materialLine({ itemRef: 'x', required: 2, owned: 5 })).toBe('5 / 2');
	});

	it('knows when a material is satisfied', () => {
		expect(materialSatisfied({ itemRef: 'x', required: 2, owned: 2 })).toBe(true);
		expect(materialSatisfied({ itemRef: 'x', required: 2, owned: 1 })).toBe(false);
	});
});

describe('recipe facts', () => {
	it('lists level, skill progress, station, output and training', () => {
		expect(recipeFacts(recipe())).toEqual([
			{ label: 'Level', value: '3' },
			{ label: 'Skill', value: 'blacksmithing 1 / 2' },
			{ label: 'Station', value: 'blacksmith_forge' },
			{ label: 'Makes', value: '1 × @items/weapons/swords:iron-longsword' },
			{ label: 'Trains', value: 'blacksmithing +15' }
		]);
	});

	it('omits the skill and station lines when the recipe names neither', () => {
		const facts = recipeFacts(
			recipe({ requiredSkill: null, requiredStation: null, experienceGained: 0 })
		);

		expect(facts.map((fact) => fact.label)).toEqual(['Level', 'Makes']);
	});
});

describe('craft availability', () => {
	it('passes the server reason through when it has one', () => {
		expect(craftBlockedReason(recipe())).toBe('Short of 1 more oak.');
	});

	it('falls back to its own wording when the server sends no reason', () => {
		expect(craftBlockedReason(recipe({ unavailableReason: '  ' }))).toBe(
			'You cannot craft this yet.'
		);
	});

	it('is null when the recipe is available', () => {
		expect(craftBlockedReason(recipe({ available: true, unavailableReason: null }))).toBeNull();
	});
});

describe('describing a craft', () => {
	function crafted(overrides: Partial<ItemCraftedPayload> = {}): ItemCraftedPayload {
		return {
			characterId: 'char-1',
			recipeSlug: 'iron-longsword',
			recipeName: 'Iron longsword',
			itemRef: '@items/weapons/swords:iron-longsword',
			outputQuantity: 1,
			consumedMaterials: [{ itemRef: '@items/materials/ingots:iron-ingot', quantity: 2 }],
			skillId: 'blacksmithing',
			skillXp: 15,
			...overrides
		};
	}

	it('names the output, the materials and the training', () => {
		expect(craftedSummary(crafted())).toBe(
			'Crafted 1 × @items/weapons/swords:iron-longsword from Iron longsword. ' +
				'Used 2 × @items/materials/ingots:iron-ingot. +15 blacksmithing.'
		);
	});

	it('omits the material and training clauses when there are none', () => {
		expect(craftedSummary(crafted({ consumedMaterials: null, skillId: null, skillXp: 0 }))).toBe(
			'Crafted 1 × @items/weapons/swords:iron-longsword from Iron longsword.'
		);
	});

	it('falls back to the recipe slug and names it once when no item reference came back', () => {
		expect(
			craftedSummary(
				crafted({
					recipeName: ' ',
					outputQuantity: 0,
					itemRef: null,
					consumedMaterials: [],
					skillId: null,
					skillXp: 0
				})
			)
		).toBe('Crafted iron-longsword.');
	});
});

describe('partitioning the list', () => {
	it('splits what can be made now from what cannot', () => {
		const ready = recipe({ slug: 'ready', available: true, unavailableReason: null });
		const locked = recipe({ slug: 'locked' });

		const groups = partitionRecipes([ready, locked]);

		expect(groups.craftable.map((entry) => entry.slug)).toEqual(['ready']);
		expect(groups.locked.map((entry) => entry.slug)).toEqual(['locked']);
	});
});
