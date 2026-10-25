import type { HubConnection } from '@microsoft/signalr';
import { SvelteDate } from 'svelte/reactivity';
import { createGameHub } from '#lib/hub';
import {
	appendChat,
	applyAbilityToStats,
	applyCombatTurnToStats,
	applyDamageToStats,
	applyRestToStats,
	describeCombatTurn
} from './reducers';
import type {
	AbilityUsedPayload,
	AreaSearchedPayload,
	AvailableQuest,
	CharacterRestedPayload,
	CharacterAttributes,
	CharacterRespawnedPayload,
	CharacterSelectedPayload,
	CharacterStats,
	ChatMessage,
	CombatEndedPayload,
	CombatStartedPayload,
	CombatState,
	CombatTurnPayload,
	InventoryItem,
	InventoryLoadedPayload,
	KickedPayload,
	GroundItem,
	LocationEnteredPayload,
	MapGraph,
	MapGraphReceivedPayload,
	NodeTraveledPayload,
	OccupantEntry,
	PartyDisbandedPayload,
	PartyInviteView,
	PartyInvitesChangedPayload,
	PartySnapshot,
	PlayerEnteredPayload,
	PlayerLeftPayload,
	QuestLogEntry,
	QuestLogReceivedPayload,
	QuestTurnedInPayload,
	ServerInfoPayload,
	ShopCatalogItem,
	ShopCatalogPayload,
	SpawnedEnemy,
	TrackedQuest,
	ZoneEnteredPayload
} from './types';
import type { GroundItemsChangedPayload, SearchFinding } from './types';
import { itemLabel } from './types';

export type HubPhase =
	'idle' | 'connecting' | 'connected' | 'reconnecting' | 'failed' | 'disconnected';
export type ChatChannel = 'Zone' | 'Party' | 'Global' | 'Whisper';

const EMPTY_STATS: CharacterStats = {
	currentHealth: 0,
	maxHealth: 0,
	currentMana: 0,
	maxMana: 0,
	gold: 0,
	level: 0,
	experience: 0,
	unspentAttributePoints: 0
};

const EMPTY_ATTRIBUTES: CharacterAttributes = {
	strength: 0,
	dexterity: 0,
	constitution: 0,
	intelligence: 0,
	wisdom: 0,
	charisma: 0
};

/**
 * Reactive game session for the SvelteKit game client.
 *
 * Owns the SignalR connection and reduces every hub event into runes used by the
 * shell components (header, footer, sidebars, map, combat, overlays).
 */
export class GameSession {
	phase = $state<HubPhase>('idle');
	serverInfo = $state<ServerInfoPayload | null>(null);
	character = $state<CharacterSelectedPayload | null>(null);
	stats = $state<CharacterStats>({ ...EMPTY_STATS });
	attributes = $state<CharacterAttributes>({ ...EMPTY_ATTRIBUTES });
	zone = $state<ZoneEnteredPayload | null>(null);
	occupants = $state<OccupantEntry[]>([]);
	party = $state<PartySnapshot | null>(null);
	partyInvites = $state<PartyInviteView[]>([]);
	map = $state<MapGraph | null>(null);
	currentNodeId = $state<string | null>(null);
	selectedNodeId = $state<string | null>(null);
	location = $state<LocationEnteredPayload | null>(null);
	enemies = $state<SpawnedEnemy[]>([]);
	inventory = $state<InventoryItem[]>([]);
	groundItems = $state<GroundItem[]>([]);
	equipped = $state<Record<string, InventoryItem>>({});
	quests = $state<QuestLogEntry[]>([]);
	availableQuests = $state<AvailableQuest[]>([]);
	/** Turn-in announcement shown as a toast, or `null` when there is nothing to announce. */
	questTurnInToast = $state<{ slug: string; gold: number; xp: number } | null>(null);
	shopCatalog = $state<ShopCatalogItem[]>([]);
	shopZone = $state<string | null>(null);
	chat = $state<ChatMessage[]>([]);
	chatChannel = $state<ChatChannel>('Zone');
	whisperTarget = $state('');
	combat = $state<CombatState>({ active: false, enemy: null, log: [] });
	message = $state<string | null>(null);
	error = $state<string | null>(null);
	dead = $state(false);
	showShop = $state(false);
	showQuests = $state(false);
	showSettings = $state(false);
	showAbilities = $state(false);
	showAttributes = $state(false);
	showParty = $state(false);
	loadingMap = $state(false);
	/** Item shown in the detail dialog, or `null` when it is closed. */
	inspectedItem = $state<InventoryItem | null>(null);
	/** Result of the last area search, shown in the Local panel. */
	lastSearch = $state<{ rollValue: number; discovered: SearchFinding[] } | null>(null);

	private hub: HubConnection | null = null;
	private characterId: string | null = null;
	/** Previous journal snapshot, kept so objective completions can be announced without a per-tick event. */
	private previousQuests: QuestLogEntry[] = [];

	get selectedNode() {
		return this.map?.nodes.find((node) => node.id === this.selectedNodeId) ?? null;
	}

	get currentNode() {
		return this.map?.nodes.find((node) => node.id === this.currentNodeId) ?? null;
	}

	get adjacentNodeIds(): string[] {
		if (!this.map || !this.currentNodeId) return [];

		return this.map.edges
			.filter(
				(edge) => edge.fromNodeId === this.currentNodeId && edge.isTraversable && !edge.isHidden
			)
			.map((edge) => edge.toNodeId)
			.filter((nodeId, index, all) => all.indexOf(nodeId) === index);
	}

	get zoneLabel(): string {
		if (this.zone) return this.zone.name;
		if (this.map?.mapType === 'Zone') return this.map.name;
		return 'Region map';
	}

	/** True when the active character leads the party it belongs to. */
	get isPartyLeader(): boolean {
		if (!this.party || !this.character) return false;
		return this.party.leaderCharacterId === this.character.id;
	}

	get equippedEntries(): [string, InventoryItem][] {
		return Object.entries(this.equipped).filter(([, item]) => Boolean(item));
	}

	/** Every owned item, unequipped entries first and then alphabetically. */
	/**
	 * Active quests with their nearest incomplete objective, for the HUD tracker. Pure derivation: the journal is
	 * authoritative and the tracker is only a view of it.
	 */
	get trackedQuests(): TrackedQuest[] {
		return this.quests
			.filter((quest) => quest.status.toLowerCase() === 'active')
			.map((quest) => ({
				slug: quest.slug,
				title: quest.title,
				objective: quest.objectives.find((objective) => !objective.complete) ?? null
			}));
	}

	get bagItems(): InventoryItem[] {
		return [...this.inventory].sort((left, right) => {
			if (left.isEquipped !== right.isEquipped) return left.isEquipped ? 1 : -1;
			return itemLabel(left).localeCompare(itemLabel(right));
		});
	}

	/** Items currently equipped, flagged for the sheet and the bag badges. */
	get equippedItems(): InventoryItem[] {
		return this.equippedEntries.map(([, item]) => item);
	}

	/** Count of bag entries that are not equipped. */
	get carriedItemCount(): number {
		return this.inventory.filter((item) => !item.isEquipped).length;
	}

	get canRest(): boolean {
		return Boolean(this.zone?.hasInn && this.location);
	}

	get canShop(): boolean {
		return Boolean(this.zone?.hasMerchant);
	}

	get messagesForChannel(): ChatMessage[] {
		if (this.chatChannel === 'Whisper') {
			return this.chat.filter((entry) => entry.channel.toLowerCase() === 'whisper');
		}
		if (this.chatChannel === 'Global') {
			return this.chat.filter(
				(entry) =>
					entry.channel.toLowerCase() === 'global' || entry.channel.toLowerCase() === 'system'
			);
		}
		return this.chat.filter(
			(entry) => entry.channel.toLowerCase() === 'zone' || entry.channel.toLowerCase() === 'system'
		);
	}

	async connect(characterId: string): Promise<void> {
		this.characterId = characterId;
		this.phase = 'connecting';
		this.error = null;

		try {
			this.hub = createGameHub();
			this.registerHandlers();

			this.hub.onreconnecting(() => {
				this.phase = 'reconnecting';
			});

			this.hub.onreconnected(() => {
				this.phase = 'connected';
				void this.restoreSession();
			});

			this.hub.onclose(() => {
				if (this.phase !== 'disconnected') this.phase = 'failed';
			});

			await this.hub.start();
			this.phase = 'connected';
			await this.selectCharacter();
		} catch (error) {
			this.phase = 'failed';
			this.error = error instanceof Error ? error.message : 'Failed to connect to the game server.';
		}
	}

	async disconnect(): Promise<void> {
		this.phase = 'disconnected';
		const hub = this.hub;
		this.hub = null;
		if (hub) await hub.stop().catch(() => undefined);
	}

	private async restoreSession(): Promise<void> {
		this.addSystemMessage('Reconnected. Restoring session…');
		await this.selectCharacter();
	}

	async selectCharacter(): Promise<void> {
		if (!this.hub || !this.characterId) return;
		await this.hub.invoke('SelectCharacter', this.characterId);
	}

	async requestMapGraph(mapId: string | null = null): Promise<void> {
		if (!this.hub) return;
		this.loadingMap = true;
		await this.hub.invoke('GetMapGraph', mapId);
	}

	async travel(nodeId: string): Promise<void> {
		if (!this.hub || !this.map) return;
		this.error = null;
		await this.hub.invoke('TravelToNode', { mapId: this.map.id, nodeId });
	}

	async enterZone(zoneId: string): Promise<void> {
		if (!this.hub) return;
		await this.hub.invoke('EnterZone', zoneId);
	}

	async openInventory(): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('GetInventory');
	}

	/** Opens the item detail dialog for a bag or equipment entry. */
	inspectItem(item: InventoryItem): void {
		this.inspectedItem = item;
	}

	/** Closes the item detail dialog. */
	closeInspect(): void {
		this.inspectedItem = null;
	}

	/** Drops one unit of a bag item onto the ground at the current location. */
	async dropToGround(itemRef: string): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('DropItemToGround', itemRef);
	}

	/** Picks up a ground item at the current location. */
	async pickUpGroundItem(groundItemId: string): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('PickUpGroundItem', groundItemId);
	}

	/** Refreshes the list of items lying on the ground where the character is standing. */
	async requestGroundItems(): Promise<void> {
		if (!this.hub) return;
		await this.hub.invoke('GetGroundItems');
	}

	/** Consumes one unit of a usable item (potion, elixir). */
	async useItem(itemRef: string): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('UseItem', itemRef);
	}

	/** Spends unallocated attribute points. Keys are capitalised attribute names. */
	async allocateAttributePoints(allocations: Record<string, number>): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('AllocateAttributePoints', allocations);
	}

	/** Casts a learned ability outside combat. */
	async useAbility(abilityId: string): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('UseAbility', abilityId);
	}

	/** Accepts a catalog quest into the journal. */
	async acceptQuest(slug: string): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('AcceptQuest', slug);
	}

	/** Abandons an active quest. */
	async abandonQuest(slug: string): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('AbandonQuest', slug);
	}

	/** Turns in a quest whose objectives are complete. */
	async turnInQuest(slug: string): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('TurnInQuest', slug);
	}

	async loadQuests(): Promise<void> {
		if (!this.hub) return;
		this.showQuests = true;
		await this.hub.invoke('GetQuestLog');
	}

	async searchArea(): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('SearchArea');
	}

	async rest(cost = 10): Promise<void> {
		if (!this.hub || !this.location) return;
		this.error = null;
		await this.hub.invoke('RestAtLocation', {
			locationId: this.location.locationSlug,
			costInGold: cost
		});
	}

	async leaveZone(): Promise<void> {
		if (!this.hub) return;
		await this.hub.invoke('LeaveZone');
	}

	async visitShop(): Promise<void> {
		if (!this.hub || !this.zone) return;
		await this.hub.invoke('VisitShop', { zoneId: this.zone.id });
	}

	async buy(itemRef: string): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('BuyItem', itemRef);
	}

	async sell(itemRef: string): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('SellItem', itemRef);
	}

	/** Permanently discards one unit of an item (server-side `DropItem`). */
	async discard(itemRef: string): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('DropItem', itemRef);
	}

	/**
	 * Equips a bag item. The slot is omitted on purpose: the server derives it from the item's
	 * catalog type, so the client never has to guess.
	 */
	async equipItem(itemRef: string): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('EquipItem', { itemRef });
	}

	/** Clears a named equipment slot. */
	async unequip(slot: string): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('EquipItem', { slot, itemRef: null });
	}

	async engageEnemy(enemyId: string): Promise<void> {
		if (!this.hub || !this.location) return;
		this.error = null;
		await this.hub.invoke('EngageEnemy', {
			locationSlug: this.location.locationSlug,
			enemyId
		});
	}

	async attack(): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('AttackEnemy');
	}

	async defend(): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('DefendAction');
	}

	async flee(): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('FleeFromCombat');
	}

	async useCombatAbility(abilityId: string): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('UseAbilityInCombat', { abilityId });
	}

	async respawn(): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('Respawn');
	}

	/** Re-reads the party and its pending invites. Being solo is a normal answer, not an error. */
	async requestParty(): Promise<void> {
		if (!this.hub) return;
		await this.hub.invoke('GetParty');
	}

	async openParty(): Promise<void> {
		this.showParty = true;
		await this.requestParty();
	}

	async createParty(): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('CreateParty');
	}

	async leaveParty(): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('LeaveParty');
	}

	async kickPartyMember(characterId: string): Promise<void> {
		if (!this.hub || !characterId) return;
		this.error = null;
		await this.hub.invoke('KickPartyMember', characterId);
	}

	async inviteToParty(targetCharacterName: string): Promise<void> {
		if (!this.hub) return;
		const trimmed = targetCharacterName.trim();
		if (!trimmed) {
			this.error = 'Enter the name of the character to invite.';
			return;
		}
		this.error = null;
		await this.hub.invoke('InviteToParty', trimmed);
	}

	async acceptPartyInvite(partyId: string): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('AcceptPartyInvite', partyId);
	}

	async declinePartyInvite(partyId: string): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		// Drop it locally first so the toast disappears even before the server confirms.
		this.partyInvites = this.partyInvites.filter((invite) => invite.partyId !== partyId);
		await this.hub.invoke('DeclinePartyInvite', partyId);
	}

	async sendChat(text: string): Promise<void> {
		if (!this.hub) return;
		const trimmed = text.trim();
		if (!trimmed) return;

		if (this.chatChannel === 'Whisper') {
			const target = this.whisperTarget.trim();
			if (!target) {
				this.error = 'Enter a whisper target.';
				return;
			}
			await this.hub.invoke('SendWhisper', { targetCharacterName: target, message: trimmed });
			return;
		}

		if (this.chatChannel === 'Party') {
			await this.hub.invoke('SendPartyMessage', { message: trimmed });
			return;
		}

		if (this.chatChannel === 'Global') {
			await this.hub.invoke('SendGlobalMessage', { message: trimmed });
			return;
		}

		await this.hub.invoke('SendZoneMessage', { message: trimmed });
	}

	selectNode(nodeId: string): void {
		this.selectedNodeId = nodeId;
	}

	private registerHandlers(): void {
		const hub = this.hub;
		if (!hub) return;

		hub.on('ServerInfo', (payload: ServerInfoPayload) => {
			this.serverInfo = payload;
			this.addSystemMessage(`Connected to ${payload.serverVersion} as ${payload.connectionId}.`);
		});

		hub.on('CharacterSelected', (payload: CharacterSelectedPayload) => {
			void this.handleCharacterSelected(payload);
		});

		hub.on('CharacterAlreadyActive', () => {
			this.error = 'This character is already active in another session.';
		});

		hub.on('ZoneEntered', (payload: ZoneEnteredPayload) => {
			this.zone = payload;
			this.occupants = payload.occupants ?? [];
			this.addSystemMessage(`Entered ${payload.name}.`);
		});

		hub.on('MapGraphReceived', (payload: MapGraphReceivedPayload) => {
			this.loadingMap = false;
			if (!payload.success || !payload.map) {
				this.error = payload.errorMessage ?? 'Failed to load the map.';
				return;
			}

			this.map = payload.map;
			this.currentNodeId =
				payload.currentNodeId ?? payload.map.entryNodeId ?? payload.map.nodes[0]?.id ?? null;
			this.selectedNodeId = this.currentNodeId;
		});

		hub.on('NodeTraveled', (payload: NodeTraveledPayload) => {
			void this.handleNodeTraveled(payload);
		});

		hub.on('PlayerEntered', (payload: PlayerEnteredPayload) => {
			if (!this.occupants.some((occupant) => occupant.characterId === payload.characterId)) {
				this.occupants = [
					...this.occupants,
					{
						characterId: payload.characterId,
						characterName: payload.characterName,
						enteredAt: new SvelteDate().toISOString()
					}
				];
			}
		});

		hub.on('PlayerLeft', (payload: PlayerLeftPayload) => {
			this.occupants = this.occupants.filter(
				(occupant) => occupant.characterId !== payload.characterId
			);
		});

		hub.on('LocationEntered', (payload: LocationEnteredPayload) => {
			this.location = payload;
			// The enemy the player is already fighting is still on the roster, but it belongs to the combat panel,
			// not the location list. Filtering here also keeps a fight resumed after a reconnect from appearing twice.
			const engagedEnemyId = this.combat.enemy?.id;
			this.enemies = (payload.spawnedEnemies ?? []).filter((enemy) => enemy.id !== engagedEnemyId);
			// Ground items belong to the location, so refresh them whenever the player moves.
			void this.requestGroundItems();
		});

		hub.on('GroundItemsChanged', (payload: GroundItemsChangedPayload) => {
			this.groundItems = payload.items ?? [];
			if (payload.itemRef) this.addSystemMessage(`The ${payload.itemRef} is now on the ground.`);
		});

		hub.on('CombatStarted', (payload: CombatStartedPayload) => {
			this.combat = {
				active: true,
				enemy: {
					id: payload.enemyId,
					name: payload.enemyName,
					level: payload.enemyLevel,
					currentHealth: payload.enemyCurrentHealth,
					maxHealth: payload.enemyMaxHealth,
					abilities: payload.enemyAbilityNames ?? []
				},
				log: [`Combat started: ${payload.enemyName} (level ${payload.enemyLevel}).`]
			};
			this.enemies = this.enemies.filter((enemy) => enemy.id !== payload.enemyId);
		});

		hub.on('CombatTurn', (payload: CombatTurnPayload) => {
			this.stats = applyCombatTurnToStats(this.stats, payload);
			const line = describeCombatTurn(payload);
			if (this.combat.enemy) {
				this.combat = {
					...this.combat,
					enemy: {
						...this.combat.enemy,
						currentHealth: payload.enemyRemainingHealth ?? this.combat.enemy.currentHealth
					},
					log: [...this.combat.log, line].slice(-25)
				};
			}
			if (payload.playerDefeated) {
				this.dead = true;
				this.applyDeathPenalty(payload.goldLost ?? 0, payload.xpLost ?? 0);
			}
			if (payload.enemyDefeated) {
				this.combat = { ...this.combat, active: false };
			}
		});

		hub.on('CombatEnded', (payload: CombatEndedPayload) => {
			this.combat = {
				active: false,
				enemy: null,
				log: [...this.combat.log, `Combat ended: ${payload.reason}.`].slice(-25)
			};
			this.addSystemMessage(`Combat ended: ${payload.reason}.`);
		});

		hub.on('EnemyDefeated', () => {
			this.addSystemMessage('Enemy defeated.');
		});

		hub.on('CharacterRespawned', (payload?: CharacterRespawnedPayload) => {
			this.dead = false;

			// A2: death moves the character to the zone's safe location, so the view has to follow them.
			if (payload?.locationSlug) {
				this.location = {
					locationSlug: payload.locationSlug,
					locationDisplayName: payload.locationDisplayName ?? payload.locationSlug,
					typeKey: 'Location',
					spawnedEnemies: []
				};
				void this.requestGroundItems();
			}

			const sickness = payload?.sicknessExpiresAt
				? ` You feel weakened until ${new SvelteDate(payload.sicknessExpiresAt).toLocaleTimeString()}.`
				: '';
			this.addSystemMessage(
				payload?.locationDisplayName
					? `You have respawned at ${payload.locationDisplayName}.${sickness}`
					: `You have respawned.${sickness}`
			);
		});

		hub.on(
			'ExperienceGained',
			(payload: { newLevel: number; newXP: number; leveledUp: boolean; leveledUpTo: number }) => {
				this.stats = {
					...this.stats,
					level: payload.newLevel,
					experience: payload.newXP
				};
				if (payload.leveledUp)
					this.addSystemMessage(`Level up! You are now level ${payload.leveledUpTo}.`);
			}
		);

		hub.on('GoldChanged', (payload: { goldAdded: number; newGoldTotal: number }) => {
			this.stats = { ...this.stats, gold: payload.newGoldTotal };
		});

		hub.on(
			'DamageTaken',
			(payload: { damage: number; currentHP: number; maxHP: number; isDead: boolean }) => {
				this.stats = applyDamageToStats(this.stats, payload);
				if (payload.isDead) this.dead = true;
			}
		);

		hub.on('CharacterRested', (payload: CharacterRestedPayload) => {
			this.stats = applyRestToStats(this.stats, payload);
			this.addSystemMessage('You feel rested.');
		});

		hub.on('AbilityUsed', (payload: AbilityUsedPayload) => {
			this.stats = applyAbilityToStats(this.stats, payload);
		});

		hub.on(
			'AttributePointsAllocated',
			(payload: { remainingPoints: number; attrs: Record<string, number> }) => {
				this.stats = { ...this.stats, unspentAttributePoints: payload.remainingPoints };
				this.attributes = {
					strength: payload.attrs.Strength ?? this.attributes.strength,
					dexterity: payload.attrs.Dexterity ?? this.attributes.dexterity,
					constitution: payload.attrs.Constitution ?? this.attributes.constitution,
					intelligence: payload.attrs.Intelligence ?? this.attributes.intelligence,
					wisdom: payload.attrs.Wisdom ?? this.attributes.wisdom,
					charisma: payload.attrs.Charisma ?? this.attributes.charisma
				};
			}
		);

		hub.on(
			'ItemEquipped',
			(payload: {
				equipped?: Record<string, InventoryItem> | null;
				itemRef?: string | null;
				slot: string;
				clearedSlots?: string[] | null;
			}) => {
				if (payload.equipped) this.equipped = payload.equipped;
				this.addSystemMessage(
					payload.itemRef ? `Equipped ${payload.itemRef}.` : `Unequipped ${payload.slot}.`
				);
				for (const cleared of payload.clearedSlots ?? []) {
					this.addSystemMessage(`${cleared} freed (two-handed weapon).`);
				}
			}
		);

		hub.on(
			'ItemUsed',
			(payload: {
				itemRef: string;
				remainingQuantity: number;
				currentHealth: number;
				maxHealth: number;
				currentMana: number;
				maxMana: number;
				message: string;
			}) => {
				this.stats = {
					...this.stats,
					currentHealth: payload.currentHealth,
					maxHealth: payload.maxHealth,
					currentMana: payload.currentMana,
					maxMana: payload.maxMana
				};
				this.addSystemMessage(payload.message || `Used ${payload.itemRef}.`);
			}
		);

		hub.on('InventoryLoaded', (payload: InventoryLoadedPayload) => {
			// The bag lives in the character sheet now, so `openOverlay` no longer opens anything —
			// the snapshot just refreshes the sheet's item list and equipment rack.
			this.inventory = payload.items ?? [];
			this.equipped = payload.equipped ?? {};
		});

		hub.on('ShopVisited', (payload: { zoneId: string; zoneName: string }) => {
			this.shopZone = payload.zoneName;
			this.showShop = true;
			if (this.hub) void this.hub.invoke('GetShopCatalog');
		});

		hub.on('ShopCatalog', (payload: ShopCatalogPayload) => {
			this.shopCatalog = payload.catalog ?? [];
		});

		hub.on(
			'ItemPurchased',
			(payload: { itemRef: string; goldSpent: number; remainingGold: number }) => {
				this.stats = { ...this.stats, gold: payload.remainingGold };
				this.addSystemMessage(`Purchased ${payload.itemRef}.`);
			}
		);

		hub.on(
			'ItemSold',
			(payload: { itemRef: string; goldReceived: number; newGoldTotal: number }) => {
				this.stats = { ...this.stats, gold: payload.newGoldTotal };
				this.addSystemMessage(`Sold ${payload.itemRef} for ${payload.goldReceived} gold.`);
			}
		);

		hub.on('ItemDropped', (payload: { itemRef: string; remainingQuantity: number }) => {
			this.addSystemMessage(
				payload.remainingQuantity > 0
					? `Discarded ${payload.itemRef} (${payload.remainingQuantity} left).`
					: `Discarded ${payload.itemRef}.`
			);
		});

		hub.on('QuestLogReceived', (payload: QuestLogReceivedPayload | QuestLogEntry[]) => {
			if (Array.isArray(payload)) {
				this.quests = payload;
				this.announceQuestProgress(payload);
				return;
			}

			this.quests = payload.quests ?? this.quests;
			if (payload.available) this.availableQuests = payload.available;
			this.announceQuestProgress(this.quests);
		});

		hub.on('QuestTurnedIn', (payload: QuestTurnedInPayload) => {
			this.quests = payload.quests ?? this.quests;
			this.announceQuestProgress(this.quests);
			this.questTurnInToast = {
				slug: payload.slug,
				gold: payload.goldReward,
				xp: payload.xpReward
			};
			this.addSystemMessage(
				`Quest complete: ${payload.slug} (+${payload.goldReward} gold, +${payload.xpReward} XP).`
			);
		});

		hub.on('AreaSearched', (payload: AreaSearchedPayload) => {
			const discovered = payload.discovered ?? [];
			this.lastSearch = { rollValue: payload.rollValue ?? 0, discovered };
			this.addSystemMessage(
				`Search roll ${payload.rollValue ?? '?'}: ${discovered.length > 0 ? `discovered ${discovered.length} location(s).` : 'nothing found.'}`
			);
		});

		hub.on('PartyUpdated', (payload: PartySnapshot | null) => {
			const previousMemberCount = this.party?.members.length ?? 0;
			this.party = payload;

			if (payload === null) {
				this.partyInvites = [];
				this.endPartyChat();
				return;
			}

			// An invite to the party we just joined is no longer actionable.
			this.partyInvites = this.partyInvites.filter((invite) => invite.partyId !== payload.partyId);

			const leader = payload.members.find((member) => member.isLeader) ?? payload.members[0];
			if (previousMemberCount === 0) {
				if (leader) this.addSystemMessage(`You joined ${leader.name}'s party.`);
				return;
			}

			if (payload.members.length > previousMemberCount) {
				this.addSystemMessage('A new member joined the party.');
			} else if (payload.members.length < previousMemberCount) {
				this.addSystemMessage('A member left the party.');
			}
		});

		hub.on('PartyDisbanded', (payload: PartyDisbandedPayload) => {
			if (this.party?.partyId !== payload.partyId) return;
			this.party = null;
			this.showParty = false;
			this.endPartyChat();
			this.addSystemMessage('Your party disbanded.');
		});

		hub.on('PartyInvitesChanged', (payload: PartyInvitesChangedPayload) => {
			if (payload.partyId) {
				this.partyInvites = this.partyInvites.filter(
					(invite) => invite.partyId !== payload.partyId
				);
				return;
			}

			// The server only nudges us: invites are re-read, so a live invite and a login invite
			// follow exactly the same path.
			void this.requestParty();
		});

		hub.on('ReceiveChatMessage', (payload: ChatMessage) => {
			this.chat = appendChat(this.chat, payload);
		});

		hub.on('SystemMessage', (message: string) => {
			this.addSystemMessage(message);
		});

		hub.on('Error', (message: string) => {
			this.error = message;
		});

		hub.on('OnKicked', (payload: KickedPayload) => {
			this.error = payload.reason ?? 'You were disconnected by a moderator.';
			this.phase = 'disconnected';
		});
	}

	private async handleCharacterSelected(payload: CharacterSelectedPayload): Promise<void> {
		this.character = payload;
		this.stats = {
			currentHealth: payload.currentHealth,
			maxHealth: payload.maxHealth,
			currentMana: payload.currentMana,
			maxMana: payload.maxMana,
			gold: payload.gold,
			level: payload.level,
			experience: payload.experience,
			unspentAttributePoints: payload.unspentAttributePoints
		};
		this.currentNodeId = payload.currentNodeId ?? null;
		this.attributes = {
			strength: payload.strength,
			dexterity: payload.dexterity,
			constitution: payload.constitution,
			intelligence: payload.intelligence,
			wisdom: payload.wisdom,
			charisma: payload.charisma
		};

		// A party outlives a restart, so pull it (and any pending invites) as soon as the character
		// is selected instead of waiting for the panel to be opened.
		await this.requestParty();

		// The character sheet renders the bag and the equipment rack, so pull the snapshot as soon
		// as the character is selected instead of waiting for a manual refresh.
		await this.openInventory();

		if (payload.currentZoneId) await this.enterZone(payload.currentZoneId);
		await this.requestMapGraph(null);
	}

	private async handleNodeTraveled(payload: NodeTraveledPayload): Promise<void> {
		if (!payload.success) {
			this.error = payload.errorMessage ?? 'Travel failed.';
			return;
		}

		this.message = payload.message ?? null;
		if (payload.message) this.addSystemMessage(payload.message);

		if (payload.nodeId) {
			this.currentNodeId = payload.nodeId;
			this.selectedNodeId = payload.nodeId;
		}

		this.location = payload.locationSlug
			? {
					locationSlug: payload.locationSlug,
					locationDisplayName:
						payload.message?.replace(/^Travelled to /, '').replace(/\.$/, '') ??
						payload.locationSlug,
					typeKey: payload.nodeType ?? 'Location',
					spawnedEnemies: payload.spawnedEnemies ?? []
				}
			: null;
		this.enemies = payload.spawnedEnemies ?? [];

		if (payload.mapChanged) await this.requestMapGraph(payload.mapId ?? null);
	}

	/** Clears the turn-in toast once the player has seen it. */
	dismissQuestToast(): void {
		this.questTurnInToast = null;
	}

	/**
	 * Announces objectives that just became complete by diffing the new journal against the previous snapshot — the
	 * only way to surface progress without inventing a per-tick event. The first snapshot produces nothing: there is
	 * nothing to compare against and a reload must not spam the log.
	 */
	private announceQuestProgress(next: QuestLogEntry[]): void {
		const previous = this.previousQuests;
		this.previousQuests = next;
		if (previous.length === 0) return;

		for (const quest of next) {
			const before = previous.find((entry) => entry.slug === quest.slug);
			if (!before) continue;

			for (const objective of quest.objectives) {
				const was = before.objectives.find((entry) => entry.key === objective.key);
				if (!was?.complete && objective.complete) {
					this.addSystemMessage(`Objective complete: ${objective.key} (${quest.title}).`);
				}
			}
		}
	}

	/**
	 * The server says exactly what a death took, so the totals and the log are mirrored rather than guessed.
	 */
	private applyDeathPenalty(goldLost: number, xpLost: number): void {
		if (goldLost <= 0 && xpLost <= 0) return;

		this.stats = {
			...this.stats,
			gold: Math.max(0, this.stats.gold - goldLost),
			experience: Math.max(0, this.stats.experience - xpLost)
		};

		this.addSystemMessage(
			goldLost > 0 ? `You lost ${goldLost} gold.` : `You lost ${xpLost} experience.`
		);
	}

	/**
	 * Party chat is live-only: the moment membership ends the channel disappears and its history goes with it, so
	 * nothing implies a backlog the server does not keep.
	 */
	private endPartyChat(): void {
		this.chat = this.chat.filter((entry) => entry.channel.toLowerCase() !== 'party');
		if (this.chatChannel === 'Party') this.chatChannel = 'Zone';
	}

	private addSystemMessage(message: string): void {
		this.chat = appendChat(this.chat, {
			channel: 'System',
			sender: 'System',
			message,
			timestamp: new SvelteDate().toISOString()
		});
	}
}
