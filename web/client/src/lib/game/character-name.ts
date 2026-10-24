/** Result of validating a character name before asking the server. */
export interface CharacterNameValidation {
	valid: boolean;
	error: string | null;
}

const NAME_PATTERN = /^[a-zA-Z ]+$/;
const NAME_MIN_LENGTH = 2;
const NAME_MAX_LENGTH = 20;

/**
 * Mirrors the server-side name rules in `CharacterCreationSessionEndpoints.ValidateNameFormat`
 * so the wizard can show immediate feedback without a round trip.
 */
export function validateCharacterName(value: string): CharacterNameValidation {
	const name = value.trim();

	if (!name) {
		return { valid: false, error: 'Name is required.' };
	}

	if (name.length < NAME_MIN_LENGTH) {
		return { valid: false, error: `Name must be at least ${NAME_MIN_LENGTH} characters.` };
	}

	if (name.length > NAME_MAX_LENGTH) {
		return { valid: false, error: `Name must be at most ${NAME_MAX_LENGTH} characters.` };
	}

	if (!NAME_PATTERN.test(name)) {
		return { valid: false, error: 'Name may only contain letters and spaces.' };
	}

	return { valid: true, error: null };
}
