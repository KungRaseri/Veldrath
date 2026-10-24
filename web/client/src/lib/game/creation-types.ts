/** Shared client/server contracts for the character creation wizard. */

/** Roster capacity for the signed-in account. */
export interface CharacterSlots {
	used: number;
	max: number;
	remaining: number;
}

/** Most recently played character, used to offer a resume shortcut. */
export interface LastSession {
	characterId: string;
	characterName: string;
	zoneId: string | null;
	regionId: string | null;
	currentMapId: string | null;
	currentNodeId: string | null;
}

/** A selectable class, species, or background. */
export interface CreationOption {
	slug: string;
	displayName: string;
	description?: string;
}

/** Character creation catalog loaded from the content endpoints. */
export interface CreationCatalog {
	classes: CreationOption[];
	species: CreationOption[];
	backgrounds: CreationOption[];
}

/** Server-authoritative point-buy configuration. */
export interface PointBuyConfig {
	totalPoints: number;
	minStatValue: number;
	maxStatValue: number;
	costTable: Record<string, number>;
}

/** A single armor or weapon type choice. */
export interface EquipmentTypeOption {
	slug: string;
	displayName: string;
}

/** Equipment catalog returned when a creation session starts. */
export interface EquipmentTypeCatalog {
	armorTypes: EquipmentTypeOption[];
	weaponTypes: EquipmentTypeOption[];
}

/** Response from starting a character creation session. */
export interface BeginCreationSessionResponse {
	sessionId: string;
	success: boolean;
	pointBuyConfig: PointBuyConfig | null;
	equipmentTypeCatalog: EquipmentTypeCatalog | null;
	sessionTimeoutMinutes: number;
}

/** Response from a single-step creation mutation. */
export interface SetCreationChoiceResponse {
	success: boolean;
	message: string;
}

/** Response from allocating point-buy attributes. */
export interface AllocateCreationAttributesResponse extends SetCreationChoiceResponse {
	remainingPoints: number;
}

/** Non-persisted preview of the character being created. */
export interface CharacterPreview {
	className: string | null;
	speciesName: string | null;
	backgroundName: string | null;
	strength: number;
	dexterity: number;
	constitution: number;
	intelligence: number;
	wisdom: number;
	charisma: number;
	health: number;
	mana: number;
}

/** Name availability result. */
export interface CheckNameAvailabilityResponse {
	available: boolean;
	error: string | null;
}

/** Equipment preferences submitted by the wizard. */
export interface CreationEquipmentPreferences {
	preferredArmorType: string | null;
	preferredWeaponType: string | null;
	includeShield: boolean;
}
