import type { SkillView, SkillXpGainedPayload } from './types';

/**
 * Pure helpers for the skill sheet.
 *
 * The server owns the progression rule and sends both the rank and the progress figures, so this
 * module only decides what to call a skill, how wide the bar is, how to group the list, and how a
 * `SkillXpGained` award folds into the sheet.
 */

/** Names a skill key the server could not resolve, without pretending the key is the name. */
export function unknownSkillLabel(skillId: string): string {
	return `Unknown skill (${skillId})`;
}

/** The category label used when a skill carries no category or an unresolved one. */
export const UNCATEGORISED_LABEL = 'Other';

/** Drops entries that carry no skill id. */
export function resolveSkills(skills: SkillView[] | null | undefined): SkillView[] {
	return (skills ?? []).filter((skill) => Boolean(skill?.skillId));
}

/** The player-facing name, falling back to an explicit unknown label. */
export function skillLabel(skill: SkillView): string {
	const name = skill.name?.trim();
	return name && name.length > 0 ? name : unknownSkillLabel(skill.skillId);
}

/** How full the progress bar is, 0–100. A skill with no rank cost yet reads as complete. */
export function skillProgressPercent(skill: SkillView): number {
	if (skill.xpToNextRank <= 0) return 100;

	const percent = Math.round((skill.xpIntoRank / skill.xpToNextRank) * 100);
	return Math.max(0, Math.min(100, percent));
}

/** One category of the sheet. */
export interface SkillGroup {
	/** The raw category key, or an empty string for uncategorised skills. */
	category: string;
	/** The heading to render. */
	label: string;
	skills: SkillView[];
}

/** Groups skills by catalog category, alphabetically, with the uncategorised group last. */
export function groupSkillsByCategory(skills: SkillView[]): SkillGroup[] {
	const groups = new Map<string, SkillView[]>();

	for (const skill of skills) {
		const category = skill.category?.trim().toLowerCase() ?? '';
		const existing = groups.get(category);
		if (existing) existing.push(skill);
		else groups.set(category, [skill]);
	}

	return [...groups.entries()]
		.map(([category, grouped]) => ({
			category,
			label: category.length === 0 ? UNCATEGORISED_LABEL : capitalise(category),
			skills: grouped
		}))
		.sort((left, right) => {
			if (left.category.length === 0) return 1;
			if (right.category.length === 0) return -1;
			return left.label.localeCompare(right.label);
		});
}

function capitalise(value: string): string {
	return value.length === 0 ? value : value[0].toUpperCase() + value.slice(1);
}

/**
 * Folds a `SkillXpGained` award into the sheet entry, keeping the catalog copy the snapshot
 * already resolved. An award for a skill the sheet has never seen becomes an unresolved entry, so
 * practising a skill before opening the sheet still shows up.
 */
export function applySkillProgress(
	previous: SkillView | null,
	payload: SkillXpGainedPayload
): SkillView {
	return {
		skillId: payload.skillId,
		resolved: previous?.resolved ?? false,
		name: previous?.name ?? null,
		description: previous?.description ?? null,
		category: previous?.category ?? null,
		governingAttribute: previous?.governingAttribute ?? null,
		rank: payload.currentRank,
		totalXp: payload.totalXp,
		xpIntoRank: payload.xpIntoRank,
		xpToNextRank: payload.xpToNextRank,
		maxRank: previous?.maxRank ?? 0
	};
}
