import type { CraftMaterial, CraftableRecipe, ItemCraftedPayload } from './types';

/**
 * Pure helpers for the crafting panel.
 *
 * The server decides what is craftable and says why not, so this module only decides how to name a
 * recipe, how to read a material line, and how to describe a completed craft. It never re-derives
 * availability from the numbers — that would be a second opinion the panel could disagree with.
 */

/** A fact the panel renders beside a recipe. */
export interface CraftFact {
	label: string;
	value: string;
}

/** The recipe's name, falling back to its slug only when the name is blank. */
export function recipeLabel(recipe: CraftableRecipe): string {
	const name = recipe.name?.trim();
	return name && name.length > 0 ? name : recipe.slug;
}

/** `have / need` for one material, with the shortfall spelled out when there is one. */
export function materialLine(material: CraftMaterial): string {
	return `${material.owned} / ${material.required}`;
}

/** True when the character holds at least what the material needs. */
export function materialSatisfied(material: CraftMaterial): boolean {
	return material.owned >= material.required;
}

/** The requirements a recipe states, as label/value pairs. */
export function recipeFacts(recipe: CraftableRecipe): CraftFact[] {
	const facts: CraftFact[] = [{ label: 'Level', value: String(recipe.requiredLevel) }];

	if (recipe.requiredSkill && recipe.requiredSkillLevel > 0) {
		facts.push({
			label: 'Skill',
			value: `${recipe.requiredSkill} ${recipe.skillRank} / ${recipe.requiredSkillLevel}`
		});
	}

	if (recipe.requiredStation) {
		facts.push({ label: 'Station', value: recipe.requiredStation });
	}

	facts.push({ label: 'Makes', value: `${recipe.outputQuantity} × ${recipe.outputItemRef}` });

	if (recipe.experienceGained > 0 && recipe.requiredSkill) {
		facts.push({ label: 'Trains', value: `${recipe.requiredSkill} +${recipe.experienceGained}` });
	}

	return facts;
}

/** The reason the craft button is disabled, or `null` when the character can craft it. */
export function craftBlockedReason(recipe: CraftableRecipe): string | null {
	if (recipe.available) return null;
	const reason = recipe.unavailableReason?.trim();
	return reason && reason.length > 0 ? reason : 'You cannot craft this yet.';
}

/** A one-line description of what a completed craft did, for the log and the toast. */
export function craftedSummary(payload: ItemCraftedPayload): string {
	const label = payload.recipeName?.trim() || payload.recipeSlug;
	const quantity = payload.outputQuantity > 0 ? payload.outputQuantity : 1;

	// Without an item reference there is nothing to name beyond the recipe, so say that once.
	const parts = payload.itemRef
		? [`Crafted ${quantity} × ${payload.itemRef} from ${label}.`]
		: [`Crafted ${label}.`];

	const consumed = payload.consumedMaterials ?? [];
	if (consumed.length > 0) {
		parts.push(`Used ${consumed.map((item) => `${item.quantity} × ${item.itemRef}`).join(', ')}.`);
	}

	if (payload.skillXp > 0 && payload.skillId) {
		parts.push(`+${payload.skillXp} ${payload.skillId}.`);
	}

	return parts.join(' ');
}

/** Recipes split the way the panel lists them: what can be made now, then the rest. */
export function partitionRecipes(recipes: CraftableRecipe[]): {
	craftable: CraftableRecipe[];
	locked: CraftableRecipe[];
} {
	return {
		craftable: recipes.filter((recipe) => recipe.available),
		locked: recipes.filter((recipe) => !recipe.available)
	};
}
