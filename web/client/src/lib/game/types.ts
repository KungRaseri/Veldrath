/** Minimal client-side contracts for the Phase 1 game slice. */

export interface CharacterSummary {
	id: string;
	slotIndex: number;
	name: string;
	className: string;
	level: number;
	experience: number;
	lastPlayedAt: string;
	currentZoneId: string;
	difficultyMode: string;
	isOnline: boolean;
	isHardcore: boolean;
	backgroundId?: string | null;
	speciesSlug?: string | null;
	currentZoneLocationSlug?: string | null;
}

export interface ServerInfoPayload {
	connectionId: string;
	serverVersion: string;
	minCompatibleClientVersion: string;
}

export interface CharacterSelectedPayload {
	id: string;
	name: string;
	className: string;
	level: number;
	experience: number;
	currentZoneId?: string | null;
	regionId: string;
	currentMapId?: string | null;
	currentNodeId?: string | null;
	currentHealth: number;
	maxHealth: number;
	currentMana: number;
	maxMana: number;
	gold: number;
	unspentAttributePoints: number;
	strength: number;
	dexterity: number;
	constitution: number;
	intelligence: number;
	wisdom: number;
	charisma: number;
	learnedAbilities: string[];
	selectedAt: string;
	equipment?: Record<string, string> | null;
}

export interface OccupantEntry {
	characterId: string;
	characterName: string;
	enteredAt: string;
}

export interface ZoneEnteredPayload {
	id: string;
	name: string;
	description: string;
	zoneType: string;
	occupants: OccupantEntry[];
	currentMapId?: string | null;
	currentNodeId?: string | null;
	hasInn: boolean;
	hasMerchant: boolean;
}

export interface MapNode {
	id: string;
	mapId: string;
	nodeType: string;
	refId?: string | null;
	displayName: string;
	description: string;
	iconKey?: string | null;
	layoutX?: number | null;
	layoutY?: number | null;
	isEntryPoint: boolean;
	isExitPoint: boolean;
	targetMapId?: string | null;
	targetNodeId?: string | null;
	isHidden: boolean;
	unlockType?: string | null;
	unlockKey?: string | null;
	discoverThreshold?: number | null;
	sortOrder: number;
}

export interface MapEdge {
	id: string;
	mapId: string;
	fromNodeId: string;
	toNodeId: string;
	direction: string;
	edgeType: string;
	label?: string | null;
	isTraversable: boolean;
	isHidden: boolean;
}

export interface MapGraph {
	id: string;
	mapType: string;
	ownerId: string;
	name: string;
	description: string;
	entryNodeId?: string | null;
	nodes: MapNode[];
	edges: MapEdge[];
}

export interface MapGraphReceivedPayload {
	success: boolean;
	errorMessage?: string | null;
	map?: MapGraph | null;
	currentNodeId?: string | null;
	availableMaps?: unknown[] | null;
}

export interface NodeTraveledPayload {
	success: boolean;
	errorMessage?: string | null;
	mapId?: string | null;
	nodeId?: string | null;
	nodeType?: string | null;
	mapChanged: boolean;
	message?: string | null;
	zoneId?: string | null;
	regionId?: string | null;
	locationSlug?: string | null;
	passiveDiscoveries?: unknown[] | null;
	spawnedEnemies?: SpawnedEnemy[] | null;
}

/** Subset of the engine `Item` model that the inventory overlay renders. */
export interface InventoryItem {
	itemRef: string;
	name: string;
	description?: string | null;
	rarity: string;
	type: string;
	typeKey?: string | null;
	/** Canonical equipment slot (e.g. `"MainHand"`), or `null` when the item is not equippable. */
	slot?: string | null;
	quantity: number;
	stackSize: number;
	isStackable: boolean;
	/** True when the entry is currently in the equipment blob; the sheet shows an equip badge. */
	isEquipped: boolean;
	price: number;
	weight: number;
	durability?: number | null;
	/** Consumable effect key (`heal`, `restore`, `heal_overtime`, `cure_poison`), if any. */
	effect?: string | null;
	/** Magnitude of `effect` (0 when the item has no effect). */
	effectPower: number;
	/** Whether the weapon occupies both hands. */
	isTwoHanded: boolean;
	armorClass?: string | null;
	armorType?: string | null;
	weaponType?: string | null;
	damageMin?: number | null;
	damageMax?: number | null;
	/** Minimum character level required to equip or use the item. */
	requirementsLevel: number;
	/** Minimum attribute values required, keyed by attribute name. */
	requirements: Record<string, number>;
	/** Attribute bonuses granted while equipped, keyed by attribute name. */
	bonuses: Record<string, number>;
}

export interface InventoryLoadedPayload {
	characterId?: string;
	/** Every owned item, equipped ones included and flagged through `isEquipped`. */
	items?: InventoryItem[] | null;
	/** Equipped items keyed by canonical slot name. */
	equipped?: Record<string, InventoryItem> | null;
	openOverlay?: boolean;
}

export interface KickedPayload {
	reason?: string | null;
}

/** An item lying on the ground at the character's current location. */
export interface GroundItem {
	id: string;
	itemRef: string;
	name: string;
	rarity: string;
	quantity: number;
}

export interface GroundItemsChangedPayload {
	characterId?: string;
	itemRef?: string;
	items?: GroundItem[] | null;
}

export interface ChatMessage {
	characterId?: string;
	channel: string;
	sender: string;
	message: string;
	timestamp: string;
}

export interface ShopCatalogItem {
	slug: string;
	name: string;
	rarity?: string;
	price: number;
}

export interface ShopCatalogPayload {
	zoneId: string;
	catalog: ShopCatalogItem[];
}

export interface QuestObjective {
	key: string;
	current: number;
	required: number;
	complete: boolean;
}

export interface QuestLogEntry {
	slug: string;
	title: string;
	description: string;
	status: string;
	goldReward: number;
	xpReward: number;
	canTurnIn: boolean;
	objectives: QuestObjective[];
}

/** A quest line for the HUD tracker: the quest plus its nearest incomplete objective. */
export interface TrackedQuest {
	slug: string;
	title: string;
	objective: QuestObjective | null;
}

export interface AvailableQuest {
	slug: string;
	title: string;
	description: string;
	goldReward: number;
	xpReward: number;
	objectives: string[];
}

export interface QuestLogReceivedPayload {
	characterId?: string;
	quests?: QuestLogEntry[] | null;
	available?: AvailableQuest[] | null;
}

export interface QuestTurnedInPayload {
	characterId?: string;
	slug: string;
	goldReward: number;
	xpReward: number;
	quests?: QuestLogEntry[] | null;
}

export interface SpawnedEnemy {
	id: string;
	name: string;
	level: number;
	currentHealth: number;
	maxHealth: number;
}

export interface LocationEnteredPayload {
	characterId?: string;
	locationSlug: string;
	locationDisplayName: string;
	typeKey: string;
	spawnedEnemies?: SpawnedEnemy[] | null;
}

/** A location revealed by a successful area search. */
export interface SearchFinding {
	slug: string;
	displayName: string;
	typeKey: string;
}

export interface AreaSearchedPayload {
	characterId?: string;
	rollValue: number;
	anyFound: boolean;
	discovered?: SearchFinding[];
}

export interface CharacterRestedPayload {
	characterId?: string;
	locationId: string;
	currentHealth: number;
	maxHealth: number;
	currentMana: number;
	maxMana: number;
	goldRemaining: number;
}

export interface AbilityUsedPayload {
	characterId?: string;
	abilityId: string;
	manaCost: number;
	remainingMana: number;
	healthRestored: number;
}

export interface CombatStartedPayload {
	characterId: string;
	enemyId: string;
	enemyName: string;
	enemyLevel: number;
	enemyCurrentHealth: number;
	enemyMaxHealth: number;
	enemyAbilityNames: string[];
}

export interface CombatTurnPayload {
	action: string;
	playerDamage?: number;
	enemyRemainingHealth?: number;
	enemyDefeated?: boolean;
	enemyDamage?: number;
	enemyAbilityUsed?: string | null;
	playerRemainingHealth?: number;
	playerDefeated?: boolean;
	playerHardcoreDeath?: boolean;
	/** Gold the death cost, reported so the client can name the exact loss. */
	goldLost?: number;
	/** Experience the death cost, reported so the client can name the exact loss. */
	xpLost?: number;
	xpEarned?: number;
	goldEarned?: number;
	abilityId?: string | null;
	abilityDamage?: number;
	healthRestored?: number;
	manaCost?: number;
	playerRemainingMana?: number;
}

export interface CombatEndedPayload {
	characterId: string;
	reason: string;
}

export interface PlayerEnteredPayload {
	characterId: string;
	characterName: string;
	zoneId: string;
}

export interface PlayerLeftPayload {
	characterId: string;
	characterName: string;
	zoneId: string;
}

/** Sent to the caller when they respawn after a normal-mode death. */
export interface CharacterRespawnedPayload {
	characterId: string;
	currentHealth: number;
	currentMana: number;
	/** Location the character woke up at, or null when death left them where they fell. */
	locationSlug?: string | null;
	locationDisplayName?: string | null;
	/** When death sickness wears off, or null when none was applied. */
	sicknessExpiresAt?: string | null;
}

/** One member of the player's party, as the server broadcasts it. */
export interface PartyMemberView {
	characterId: string;
	name: string;
	level: number;
	currentHealth: number;
	maxHealth: number;
	isLeader: boolean;
}

/** Complete party snapshot. Every mutation rebroadcasts one of these, so the client never assembles deltas. */
export interface PartySnapshot {
	partyId: string;
	leaderCharacterId: string;
	lootRule: string;
	members: PartyMemberView[];
}

/** A pending invitation waiting for the player's answer. */
export interface PartyInviteView {
	partyId: string;
	partyLeaderName: string;
	invitedByName: string;
	expiresAt: string;
}

/** Sent to the party that just lost its last member. */
export interface PartyDisbandedPayload {
	partyId: string;
}

/**
 * Nudge to refresh the invite list. A `partyId` means that specific invite is gone (declined here or
 * expired); `null` means the server invited us, so the list has to be re-read.
 */
export interface PartyInvitesChangedPayload {
	partyId?: string | null;
}

export interface CharacterStats {
	currentHealth: number;
	maxHealth: number;
	currentMana: number;
	maxMana: number;
	gold: number;
	level: number;
	experience: number;
	unspentAttributePoints: number;
}

/** Primary attributes, mirrored from the character's `Attributes` blob. */
export interface CharacterAttributes {
	strength: number;
	dexterity: number;
	constitution: number;
	intelligence: number;
	wisdom: number;
	charisma: number;
}

/** Display order and labels for the primary attributes. */
export const ATTRIBUTE_ORDER: { key: keyof CharacterAttributes; label: string }[] = [
	{ key: 'strength', label: 'Strength' },
	{ key: 'dexterity', label: 'Dexterity' },
	{ key: 'constitution', label: 'Constitution' },
	{ key: 'intelligence', label: 'Intelligence' },
	{ key: 'wisdom', label: 'Wisdom' },
	{ key: 'charisma', label: 'Charisma' }
];

export interface CombatState {
	active: boolean;
	enemy: {
		id: string;
		name: string;
		level: number;
		currentHealth: number;
		maxHealth: number;
		abilities: string[];
	} | null;
	log: string[];
}

/** Canonical equipment slots, in the order the character sheet renders them. */
export const EQUIPMENT_SLOTS = [
	{ id: 'MainHand', label: 'Main hand' },
	{ id: 'OffHand', label: 'Off hand' },
	{ id: 'Head', label: 'Head' },
	{ id: 'Shoulders', label: 'Shoulders' },
	{ id: 'Chest', label: 'Chest' },
	{ id: 'Bracers', label: 'Bracers' },
	{ id: 'Gloves', label: 'Gloves' },
	{ id: 'Belt', label: 'Belt' },
	{ id: 'Legs', label: 'Legs' },
	{ id: 'Feet', label: 'Feet' },
	{ id: 'Ring', label: 'Ring' },
	{ id: 'Amulet', label: 'Amulet' }
] as const;

const KNOWN_RARITIES = ['common', 'uncommon', 'rare', 'epic', 'legendary'];

export function itemLabel(item: InventoryItem): string {
	return item.name || item.itemRef;
}

/** Maps a rarity name onto the `rarity-*` colour class defined in `brand.css`. */
export function rarityClass(rarity: string | null | undefined): string {
	const key = (rarity ?? '').toLowerCase();
	return KNOWN_RARITIES.includes(key) ? `rarity-${key}` : 'rarity-common';
}

export function itemSubtitle(item: InventoryItem): string {
	const parts: string[] = [];
	if (item.rarity) parts.push(item.rarity);
	if (item.type) parts.push(item.type);
	if (item.durability != null) parts.push(`${item.durability}% durability`);
	return parts.length > 0 ? parts.join(' · ') : 'Item';
}

/** True when the item can be equipped into a slot. */
export function isEquippable(item: InventoryItem): boolean {
	return Boolean(item.slot);
}

/** True when the item is a consumable with a recognised effect. */
export function isUsable(item: InventoryItem): boolean {
	return Boolean(item.effect) && item.effect !== '';
}

/** One-line description of what a consumable does, for the Use action and the detail dialog. */
export function effectLabel(item: InventoryItem): string {
	switch (item.effect?.toLowerCase()) {
		case 'heal':
			return `Restores ${item.effectPower} health`;
		case 'heal_overtime':
			return `Restores about ${Math.max(1, Math.floor(item.effectPower / 2))} health over time`;
		case 'restore':
			return `Restores ${item.effectPower} mana`;
		case 'cure_poison':
			return 'Cures poison';
		default:
			return 'No usable effect';
	}
}

/** Weapon damage range, or `null` when the item is not a weapon. */
export function damageLabel(item: InventoryItem): string | null {
	if (item.damageMin == null || item.damageMax == null) return null;
	return `${item.damageMin}–${item.damageMax} damage${item.isTwoHanded ? ' · two-handed' : ''}`;
}

/** Armor class / proficiency summary, or `null` when the item is not armour. */
export function armorLabel(item: InventoryItem): string | null {
	const parts = [item.armorClass, item.armorType].filter(
		(part): part is string => Boolean(part) && part !== 'shield'
	);
	return parts.length > 0 ? `${parts.join(' · ')} armour` : null;
}
