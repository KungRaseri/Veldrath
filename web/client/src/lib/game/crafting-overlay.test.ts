import { fireEvent, render, screen, within } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';
import CraftingOverlay from './CraftingOverlay.svelte';
import { GameSession } from './session.svelte';
import type { CraftableRecipe } from './types';

vi.mock('#lib/hub', () => ({ createGameHub: () => null }));

function recipe(overrides: Partial<CraftableRecipe> = {}): CraftableRecipe {
	return {
		slug: 'iron-longsword',
		name: 'Iron longsword',
		description: 'A plain blade.',
		category: 'weapons',
		requiredLevel: 3,
		requiredSkill: 'blacksmithing',
		requiredSkillLevel: 2,
		skillRank: 2,
		requiredStation: 'blacksmith_forge',
		requiredStationTier: 1,
		materials: [{ itemRef: '@items/materials/ingots:iron-ingot', required: 2, owned: 5 }],
		outputItemRef: '@items/weapons/swords:iron-longsword',
		outputQuantity: 1,
		experienceGained: 15,
		available: true,
		unavailableReason: null,
		...overrides
	};
}

function open(recipes: CraftableRecipe[]): GameSession {
	const game = new GameSession();
	game.showCrafting = true;
	game.craftableRecipes = recipes;
	return game;
}

describe('crafting overlay', () => {
	it('says so when nothing can be made yet', () => {
		render(CraftingOverlay, { session: open([]) });

		const dialog = screen.getByRole('dialog', { name: 'Crafting' });
		expect(within(dialog).getByText('No recipes are known to you yet.')).toBeTruthy();
	});

	it('lists a ready recipe with its facts and material holdings', () => {
		render(CraftingOverlay, { session: open([recipe()]) });

		const dialog = screen.getByRole('dialog', { name: 'Crafting' });
		expect(within(dialog).getByRole('heading', { name: 'Ready to craft' })).toBeTruthy();
		expect(within(dialog).getByText('Iron longsword')).toBeTruthy();
		expect(within(dialog).getByText('A plain blade.')).toBeTruthy();
		expect(within(dialog).getByText('blacksmithing 2 / 2')).toBeTruthy();
		expect(within(dialog).getByText('blacksmith_forge')).toBeTruthy();
		expect(within(dialog).getByText('@items/materials/ingots:iron-ingot')).toBeTruthy();
		expect(within(dialog).getByText('5 / 2')).toBeTruthy();
	});

	it('crafts the recipe the player picks', async () => {
		const game = open([recipe()]);
		const craft = vi.spyOn(game, 'craft').mockResolvedValue(undefined);

		render(CraftingOverlay, { session: game });
		await fireEvent.click(screen.getByRole('button', { name: 'Craft Iron longsword' }));

		expect(craft).toHaveBeenCalledWith('iron-longsword');
	});

	it('shows the server reason and refuses the craft when a recipe is not ready', () => {
		const game = open([
			recipe({
				available: false,
				unavailableReason: 'Short of 1 more @items/materials/handles:oak.',
				materials: [{ itemRef: '@items/materials/handles:oak', required: 1, owned: 0 }]
			})
		]);

		render(CraftingOverlay, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Crafting' });
		expect(within(dialog).getByRole('heading', { name: 'Not yet' })).toBeTruthy();
		expect(within(dialog).getByText('Short of 1 more @items/materials/handles:oak.')).toBeTruthy();
		expect(within(dialog).getByText('0 / 1')).toBeTruthy();
		expect(
			(within(dialog).getByRole('button', { name: 'Craft Iron longsword' }) as HTMLButtonElement)
				.disabled
		).toBe(true);
	});

	it('separates the ready recipes from the blocked ones', () => {
		const game = open([
			recipe(),
			recipe({
				slug: 'oak-shield',
				name: 'Oak shield',
				available: false,
				unavailableReason: 'Requires level 9; you are level 3.'
			})
		]);

		render(CraftingOverlay, { session: game });

		const ready = screen.getByRole('heading', { name: 'Ready to craft' }).parentElement!;
		const locked = screen.getByRole('heading', { name: 'Not yet' }).parentElement!;
		expect(within(ready).getByText('Iron longsword')).toBeTruthy();
		expect(within(locked).getByText('Oak shield')).toBeTruthy();
	});
});
