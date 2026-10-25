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
	id?: string;
	itemRef?: string;
	slug?: string;
	name?: string;
	displayName?: string;
	description?: string;
	quantity?: number;
	rarity?: string;
	itemType?: string;
	type?: string;
	isStackable?: boolean;
}

export interface InventoryLoadedPayload {
	characterId?: string;
	itemDetails?: InventoryItem[] | null;
	equippedItems?: Record<string, InventoryItem> | null;
	openOverlay?: boolean;
}

export interface KickedPayload {
	reason?: string | null;
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

export interface QuestLogEntry {
	slug: string;
	title: string;
	status: string;
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

export interface AreaSearchedPayload {
	characterId?: string;
	rollValue: number;
	anyFound: boolean;
	discovered?: { slug: string; displayName: string; typeKey: string }[];
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

export function itemLabel(item: InventoryItem): string {
	return item.displayName ?? item.name ?? item.itemRef ?? item.slug ?? 'Unknown item';
}

export function itemSubtitle(item: InventoryItem): string {
	const parts = [item.rarity, item.itemType ?? item.type].filter(Boolean);
	return parts.length > 0 ? parts.join(' · ') : 'Item';
}
