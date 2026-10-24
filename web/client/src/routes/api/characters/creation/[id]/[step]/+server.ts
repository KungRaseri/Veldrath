import { json } from '@sveltejs/kit';
import type { RequestHandler } from './$types';
import { requireUser } from '#lib/server/access';
import {
	finalizeCreationSession,
	getCreationPreview,
	setCreationAttributes,
	setCreationBackground,
	setCreationClass,
	setCreationEquipment,
	setCreationName,
	setCreationSpecies
} from '#lib/server/character-api';
import { gameResultResponse } from '#lib/server/game-response';

const choiceSteps = new Set(['name', 'class', 'species', 'background', 'attributes', 'equipment']);

async function readJson(request: Request): Promise<Record<string, unknown> | null> {
	const value = await request.json().catch(() => null);
	return value && typeof value === 'object' && !Array.isArray(value)
		? (value as Record<string, unknown>)
		: null;
}

/** Advances one creation wizard step (name, class, species, background, attributes, equipment). */
export const PATCH: RequestHandler = async ({ locals, params, request }) => {
	const user = requireUser(locals);
	if (!choiceSteps.has(params.step)) {
		return json({ error: 'Unknown creation step.' }, { status: 404 });
	}

	const body = await readJson(request);
	if (!body) {
		return json({ error: 'A JSON request body is required.' }, { status: 400 });
	}

	switch (params.step) {
		case 'name': {
			if (typeof body.characterName !== 'string') {
				return json({ error: 'characterName is required.' }, { status: 400 });
			}
			return gameResultResponse(await setCreationName(user, params.id, body.characterName));
		}
		case 'class': {
			if (typeof body.className !== 'string') {
				return json({ error: 'className is required.' }, { status: 400 });
			}
			return gameResultResponse(await setCreationClass(user, params.id, body.className));
		}
		case 'species': {
			if (typeof body.speciesSlug !== 'string') {
				return json({ error: 'speciesSlug is required.' }, { status: 400 });
			}
			return gameResultResponse(await setCreationSpecies(user, params.id, body.speciesSlug));
		}
		case 'background': {
			if (typeof body.backgroundId !== 'string') {
				return json({ error: 'backgroundId is required.' }, { status: 400 });
			}
			return gameResultResponse(await setCreationBackground(user, params.id, body.backgroundId));
		}
		case 'attributes': {
			const raw = body.allocations;
			if (!raw || typeof raw !== 'object' || Array.isArray(raw)) {
				return json({ error: 'allocations is required.' }, { status: 400 });
			}

			const allocations: Record<string, number> = {};
			for (const [key, value] of Object.entries(raw)) {
				if (typeof value === 'number' && Number.isInteger(value)) {
					allocations[key] = value;
				}
			}

			if (Object.keys(allocations).length === 0) {
				return json({ error: 'allocations must contain at least one stat.' }, { status: 400 });
			}

			return gameResultResponse(await setCreationAttributes(user, params.id, allocations));
		}
		case 'equipment': {
			const preferredArmorType =
				typeof body.preferredArmorType === 'string' && body.preferredArmorType
					? body.preferredArmorType
					: null;
			const preferredWeaponType =
				typeof body.preferredWeaponType === 'string' && body.preferredWeaponType
					? body.preferredWeaponType
					: null;

			return gameResultResponse(
				await setCreationEquipment(user, params.id, {
					preferredArmorType,
					preferredWeaponType,
					includeShield: body.includeShield === true
				})
			);
		}
	}

	return json({ error: 'Unknown creation step.' }, { status: 404 });
};

/** Loads the creation session preview. */
export const GET: RequestHandler = async ({ locals, params }) => {
	const user = requireUser(locals);
	if (params.step !== 'preview') {
		return json({ error: 'Unknown creation step.' }, { status: 404 });
	}

	return gameResultResponse(await getCreationPreview(user, params.id));
};

/** Finalizes the creation session and creates the character. */
export const POST: RequestHandler = async ({ locals, params, request }) => {
	const user = requireUser(locals);
	if (params.step !== 'finalize') {
		return json({ error: 'Unknown creation step.' }, { status: 404 });
	}

	const body = await readJson(request);
	if (!body || typeof body.characterName !== 'string' || !body.characterName.trim()) {
		return json({ error: 'characterName is required.' }, { status: 400 });
	}

	const difficultyMode = body.difficultyMode === 'hardcore' ? 'hardcore' : 'normal';

	return gameResultResponse(
		await finalizeCreationSession(user, params.id, body.characterName.trim(), difficultyMode)
	);
};
