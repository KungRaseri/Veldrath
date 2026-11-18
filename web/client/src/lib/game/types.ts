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

/** One material a recipe needs, with how many the character already holds. */
export interface CraftMaterial {
	itemRef: string;
	required: number;
	owned: number;
}

/**
 * A catalog recipe as the crafting panel needs it.
 *
 * `requiredStation` is informational: nothing in the world model places a station in a location, so
 * the server cannot refuse a craft for it and neither does the panel.
 */
export interface CraftableRecipe {
	slug: string;
	name: string;
	description: string;
	category: string;
	requiredLevel: number;
	requiredSkill?: string | null;
	requiredSkillLevel: number;
	skillRank: number;
	requiredStation?: string | null;
	requiredStationTier: number;
	materials: CraftMaterial[];
	outputItemRef: string;
	outputQuantity: number;
	experienceGained: number;
	available: boolean;
	unavailableReason?: string | null;
}

/** The caller's craftable recipes, sent in answer to `GetCraftableRecipes`. */
export interface CraftableRecipesPayload {
	recipes: CraftableRecipe[];
}

/** One material a completed craft consumed. */
export interface ConsumedMaterial {
	itemRef: string;
	quantity: number;
}

/** A craft that succeeded, broadcast to the zone. */
export interface ItemCraftedPayload {
	characterId?: string;
	recipeSlug: string;
	recipeName?: string | null;
	itemRef?: string | null;
	outputQuantity: number;
	consumedMaterials?: ConsumedMaterial[] | null;
	skillId?: string | null;
	skillXp: number;
}

/**
 * One learned power, resolved against the power catalog by the server.
 *
 * `resolved` is false when the character knows a slug the catalog has no entry for; the UI names
 * that case explicitly instead of printing the identifier as if it were a name.
 */
export interface LearnedPower {
	slug: string;
	resolved: boolean;
	displayName?: string | null;
	description?: string | null;
	school?: string | null;
	powerType: string;
	effectType: string;
	manaCost: number;
	cooldown: number;
	baseDamage?: string | null;
	baseEffectValue?: string | null;
	damageType?: string | null;
	range?: number | null;
	duration?: number | null;
	maxTargets?: number | null;
	tier: number;
	requiredLevel: number;
}

/**
 * One practised skill, resolved against the skill catalog by the server.
 *
 * `resolved` is false when the character has practised a skill key the catalog has no entry for.
 * Progress is expressed as `xpIntoRank` over `xpToNextRank` because the server owns that rule; the
 * client never divides by a constant of its own.
 */
export interface SkillView {
	skillId: string;
	resolved: boolean;
	name?: string | null;
	description?: string | null;
	category?: string | null;
	governingAttribute?: string | null;
	rank: number;
	totalXp: number;
	xpIntoRank: number;
	xpToNextRank: number;
	maxRank: number;
}

/** A skill XP award, broadcast after the server has persisted the new total. */
export interface SkillXpGainedPayload {
	characterId: string;
	skillId: string;
	totalXp: number;
	currentRank: number;
	rankedUp: boolean;
	xpIntoRank: number;
	xpToNextRank: number;
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
	/** Learned powers with their catalog details; empty when the server could not resolve them. */
	learnedPowers?: LearnedPower[] | null;
	/** Practised skills with their rank and progress; empty when nothing has been practised. */
	skills?: SkillView[] | null;
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
	/** The scope the character is now in: the shared zone, or a run's own group. */
	zoneGroup?: string | null;
	/** The instance run the character is inside, or `null` in the shared zone. */
	zoneInstanceId?: string | null;
	/** Rooms this run has already cleared, so the dungeon panel needs no second call. */
	clearedLocations?: string[] | null;
	locationTypeKey?: string | null;
	isDungeonLocation?: boolean;
	isInstanced?: boolean;
	isSafeRespawn?: boolean;
	/** Level this place expects, or `null` when it sets no expectation. */
	recommendedLevel?: number | null;
	/** The arriving character's level, so the warning can be read at a glance. */
	yourLevel?: number;
	/** Advisory wording for an under-levelled arrival. It never blocks entry. */
	levelWarning?: string | null;
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

/**
 * A slash command the caller is allowed to run, as returned by the hub's `GetChatCommands`.
 * The server filters the list by the caller's permissions, so the client never has to.
 */
export interface ChatCommandInfo {
	command: string;
	usage: string;
	description: string;
	requiredPermission?: string | null;
}

/** A moderator issued a warning against this account. */
export interface WarnedPayload {
	reason?: string | null;
	newWarnCount?: number;
}

/** This account was muted, permanently when `until` is null. */
export interface MutedPayload {
	reason?: string | null;
	until?: string | null;
}

/** Another character performed an emote visible to the zone. */
export interface EmotePayload {
	characterId?: string;
	characterName?: string;
	action?: string;
}

/** A server-wide announcement broadcast by staff. */
export interface AnnouncementPayload {
	message?: string;
	severity?: string;
}

/** Staff moved this character to another zone. */
export interface TeleportedPayload {
	zoneId?: string;
	zoneName?: string;
}

/** Staff summoned this character to another player's zone. */
export interface SummonedPayload {
	byCharacterName?: string;
	zoneId?: string;
}

/** Staff granted this character an item. */
export interface ItemReceivedPayload {
	itemSlug?: string;
	quantity?: number;
	givenByUsername?: string;
}

/** Confirmation that an `/ignore` toggle was applied server-side. */
export interface CharacterIgnoredPayload {
	characterId?: string;
	characterName?: string;
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

/** One fallen hardcore character in the account memorial. */
export interface MemorialEntry {
	characterId: string;
	name: string;
	className: string;
	level: number;
	experience: number;
	diedAt: string;
	currentZoneId: string;
}

/** One row of the global hardcore board. */
export interface HardcoreLeaderboardEntry {
	characterId: string;
	name: string;
	className: string;
	level: number;
	experience: number;
	diedAt: string | null;
	isOwn: boolean;
}

/** One page of the global hardcore board. */
export interface HardcoreLeaderboardPage {
	page: number;
	pageSize: number;
	total: number;
	items: HardcoreLeaderboardEntry[];
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

/** Broadcast when a room in an instance run has been cleared. */
export interface LocationClearedPayload {
	characterId: string;
	instanceId?: string | null;
	locationSlug: string;
	firstClearOfRun: boolean;
	firstClearForCharacter: boolean;
	unlockedLocationSlugs?: string[] | null;
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
	/** What the defeated enemy dropped into the bag, so the log can name it. */
	itemsDropped?: { itemSlug: string; quantity: number }[] | null;
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

/** One character in a shared fight, as everyone at the location hears about them. */
export interface CombatParticipantView {
	characterId: string;
	name?: string | null;
	currentHealth: number;
	maxHealth: number;
	damageDealt: number;
	contributionPercent: number;
}

/**
 * The shared state of a fight, broadcast to the whole location.
 *
 * It carries no rewards on purpose: XP and gold are per-character and stay on the caller-only
 * `CombatTurn`.
 */
export interface CombatParticipantsPayload {
	enemyId: string;
	enemyName: string;
	enemyLevel: number;
	enemyCurrentHealth: number;
	enemyMaxHealth: number;
	enemyAbilityNames: string[];
	participants: CombatParticipantView[];
}

/** How a fight ended, using the caller's own numbers. */
export interface CombatOutcome {
	kind: 'victory' | 'defeat' | 'fled';
	xpEarned: number;
	goldEarned: number;
	xpLost: number;
	goldLost: number;
	hardcore: boolean;
}

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
	/** Everyone fighting the same enemy, highest contributor first. */
	participants?: CombatParticipantView[];
	/** Set once the fight resolves, so the panel can summarise it. */
	outcome?: CombatOutcome | null;
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
