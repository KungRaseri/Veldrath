import type { CharacterSummary } from '$lib/game/types';
import type {
	AllocateCreationAttributesResponse,
	BeginCreationSessionResponse,
	CharacterPreview,
	CharacterSlots,
	CheckNameAvailabilityResponse,
	CreationCatalog,
	CreationEquipmentPreferences,
	CreationOption,
	LastSession,
	SetCreationChoiceResponse
} from '$lib/game/creation-types';
import { toGameIdentity } from './access';
import { gameApiFetch, type GameApiResult } from './game-request';

export type {
	AllocateCreationAttributesResponse,
	BeginCreationSessionResponse,
	CharacterPreview,
	CharacterSlots,
	CheckNameAvailabilityResponse,
	CreationCatalog,
	CreationEquipmentPreferences,
	CreationOption,
	EquipmentTypeCatalog,
	EquipmentTypeOption,
	LastSession,
	PointBuyConfig,
	SetCreationChoiceResponse
} from '$lib/game/creation-types';

const BASE = '/api/character-creation/sessions';

/** Loads the account's character slot usage. */
export function fetchCharacterSlots(user: AuthUser): Promise<GameApiResult<CharacterSlots>> {
	return gameApiFetch<CharacterSlots>(toGameIdentity(user), '/api/characters/slots');
}

/** Soft-deletes one of the account's characters. */
export function deleteCharacter(user: AuthUser, characterId: string): Promise<GameApiResult<null>> {
	return gameApiFetch<null>(toGameIdentity(user), `/api/characters/${characterId}`, {
		method: 'DELETE'
	});
}

/** Loads the most recently played character, or `null` when the account has no history. */
export async function fetchLastSession(user: AuthUser): Promise<GameApiResult<LastSession | null>> {
	const result = await gameApiFetch<LastSession>(
		toGameIdentity(user),
		'/api/characters/last-session'
	);

	return result.status === 204 ? { status: 204, data: null, error: null } : result;
}

/** Clears the account's last-played markers. */
export function clearLastSession(user: AuthUser): Promise<GameApiResult<null>> {
	return gameApiFetch<null>(toGameIdentity(user), '/api/characters/last-session', {
		method: 'DELETE'
	});
}

/** Loads classes, species, and backgrounds for the creation wizard. */
export async function fetchCreationCatalog(user: AuthUser): Promise<{
	catalog: CreationCatalog;
	error: string | null;
}> {
	const identity = toGameIdentity(user);
	const [classes, species, backgrounds] = await Promise.all([
		gameApiFetch<CreationOption[]>(identity, '/api/content/classes'),
		gameApiFetch<CreationOption[]>(identity, '/api/content/species'),
		gameApiFetch<CreationOption[]>(identity, '/api/content/backgrounds')
	]);

	return {
		catalog: {
			classes: classes.data ?? [],
			species: species.data ?? [],
			backgrounds: backgrounds.data ?? []
		},
		error: classes.error ?? species.error ?? backgrounds.error
	};
}

/** Starts a new character creation session. */
export function beginCreationSession(
	user: AuthUser
): Promise<GameApiResult<BeginCreationSessionResponse>> {
	return gameApiFetch<BeginCreationSessionResponse>(toGameIdentity(user), BASE, {
		method: 'POST'
	});
}

/** Checks whether a character name is well-formed and unused. */
export function checkCharacterName(
	user: AuthUser,
	name: string
): Promise<GameApiResult<CheckNameAvailabilityResponse>> {
	return gameApiFetch<CheckNameAvailabilityResponse>(toGameIdentity(user), `${BASE}/check-name`, {
		query: { name }
	});
}

/** Applies a single name/class/species/background mutation to the session. */
function setChoice(
	user: AuthUser,
	sessionId: string,
	step: 'name' | 'class' | 'species' | 'background',
	json: Record<string, string>
): Promise<GameApiResult<SetCreationChoiceResponse>> {
	return gameApiFetch<SetCreationChoiceResponse>(
		toGameIdentity(user),
		`${BASE}/${sessionId}/${step}`,
		{ method: 'PATCH', json }
	);
}

/** Sets the character name on the session. */
export function setCreationName(
	user: AuthUser,
	sessionId: string,
	characterName: string
): Promise<GameApiResult<SetCreationChoiceResponse>> {
	return setChoice(user, sessionId, 'name', { characterName });
}

/** Sets the class on the session. */
export function setCreationClass(
	user: AuthUser,
	sessionId: string,
	className: string
): Promise<GameApiResult<SetCreationChoiceResponse>> {
	return setChoice(user, sessionId, 'class', { className });
}

/** Sets the species on the session. */
export function setCreationSpecies(
	user: AuthUser,
	sessionId: string,
	speciesSlug: string
): Promise<GameApiResult<SetCreationChoiceResponse>> {
	return setChoice(user, sessionId, 'species', { speciesSlug });
}

/** Sets the background on the session. */
export function setCreationBackground(
	user: AuthUser,
	sessionId: string,
	backgroundId: string
): Promise<GameApiResult<SetCreationChoiceResponse>> {
	return setChoice(user, sessionId, 'background', { backgroundId });
}

/** Applies point-buy attribute allocations to the session. */
export function setCreationAttributes(
	user: AuthUser,
	sessionId: string,
	allocations: Record<string, number>
): Promise<GameApiResult<AllocateCreationAttributesResponse>> {
	return gameApiFetch<AllocateCreationAttributesResponse>(
		toGameIdentity(user),
		`${BASE}/${sessionId}/attributes`,
		{ method: 'PATCH', json: { allocations } }
	);
}

/** Sets the equipment preferences on the session. */
export function setCreationEquipment(
	user: AuthUser,
	sessionId: string,
	preferences: CreationEquipmentPreferences
): Promise<GameApiResult<SetCreationChoiceResponse>> {
	return gameApiFetch<SetCreationChoiceResponse>(
		toGameIdentity(user),
		`${BASE}/${sessionId}/equipment`,
		{ method: 'PATCH', json: { ...preferences } }
	);
}

/** Loads the non-persisted preview for the session. */
export function getCreationPreview(
	user: AuthUser,
	sessionId: string
): Promise<GameApiResult<CharacterPreview>> {
	return gameApiFetch<CharacterPreview>(toGameIdentity(user), `${BASE}/${sessionId}/preview`);
}

/** Finalizes the session and creates the character. */
export function finalizeCreationSession(
	user: AuthUser,
	sessionId: string,
	characterName: string,
	difficultyMode: 'normal' | 'hardcore'
): Promise<GameApiResult<CharacterSummary>> {
	return gameApiFetch<CharacterSummary>(toGameIdentity(user), `${BASE}/${sessionId}/finalize`, {
		method: 'POST',
		json: { characterName, difficultyMode }
	});
}

/** Abandons an in-progress creation session. */
export function abandonCreationSession(
	user: AuthUser,
	sessionId: string
): Promise<GameApiResult<null>> {
	return gameApiFetch<null>(toGameIdentity(user), `${BASE}/${sessionId}`, {
		method: 'DELETE'
	});
}
