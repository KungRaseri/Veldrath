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
	CharacterRestedPayload,
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
	LocationEnteredPayload,
	MapGraph,
	MapGraphReceivedPayload,
	NodeTraveledPayload,
	OccupantEntry,
	PlayerEnteredPayload,
	PlayerLeftPayload,
	QuestLogEntry,
	ServerInfoPayload,
	ShopCatalogItem,
	ShopCatalogPayload,
	SpawnedEnemy,
	ZoneEnteredPayload
} from './types';

export type HubPhase =
	'idle' | 'connecting' | 'connected' | 'reconnecting' | 'failed' | 'disconnected';
export type ChatChannel = 'Zone' | 'Global' | 'Whisper';

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
	zone = $state<ZoneEnteredPayload | null>(null);
	occupants = $state<OccupantEntry[]>([]);
	map = $state<MapGraph | null>(null);
	currentNodeId = $state<string | null>(null);
	selectedNodeId = $state<string | null>(null);
	location = $state<LocationEnteredPayload | null>(null);
	enemies = $state<SpawnedEnemy[]>([]);
	inventory = $state<InventoryItem[]>([]);
	equipped = $state<Record<string, InventoryItem>>({});
	quests = $state<QuestLogEntry[]>([]);
	shopCatalog = $state<ShopCatalogItem[]>([]);
	shopZone = $state<string | null>(null);
	chat = $state<ChatMessage[]>([]);
	chatChannel = $state<ChatChannel>('Zone');
	whisperTarget = $state('');
	combat = $state<CombatState>({ active: false, enemy: null, log: [] });
	message = $state<string | null>(null);
	error = $state<string | null>(null);
	dead = $state(false);
	showInventory = $state(false);
	showShop = $state(false);
	showQuests = $state(false);
	showSettings = $state(false);
	loadingMap = $state(false);

	private hub: HubConnection | null = null;
	private characterId: string | null = null;

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

	get equippedEntries(): [string, InventoryItem][] {
		return Object.entries(this.equipped).filter(([, item]) => Boolean(item));
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

	async drop(itemRef: string): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('DropItem', itemRef);
	}

	async equip(slot: string, itemRef: string | null): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('EquipItem', { slot, itemRef });
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
			this.enemies = payload.spawnedEnemies ?? [];
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
			if (payload.playerDefeated) this.dead = true;
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

		hub.on('CharacterRespawned', () => {
			this.dead = false;
			this.addSystemMessage('You have respawned.');
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
			}
		);

		hub.on(
			'ItemEquipped',
			(payload: {
				equipped?: Record<string, InventoryItem> | null;
				itemRef?: string | null;
				slot: string;
			}) => {
				if (payload.equipped) this.equipped = payload.equipped;
				this.addSystemMessage(
					payload.itemRef ? `Equipped ${payload.itemRef}.` : `Unequipped ${payload.slot}.`
				);
			}
		);

		hub.on('InventoryLoaded', (payload: InventoryLoadedPayload) => {
			this.inventory = payload.itemDetails ?? [];
			this.equipped = payload.equippedItems ?? {};
			if (payload.openOverlay !== false) this.showInventory = true;
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
					? `Dropped ${payload.itemRef} (${payload.remainingQuantity} left).`
					: `Dropped ${payload.itemRef}.`
			);
		});

		hub.on('QuestLogReceived', (payload: QuestLogEntry[] | { quests?: QuestLogEntry[] }) => {
			this.quests = Array.isArray(payload) ? payload : (payload.quests ?? []);
		});

		hub.on('AreaSearched', (payload: AreaSearchedPayload) => {
			const found = payload.discovered?.length ?? 0;
			this.addSystemMessage(
				`Search roll ${payload.rollValue ?? '?'}: ${found > 0 ? `discovered ${found} location(s).` : 'nothing found.'}`
			);
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

	private addSystemMessage(message: string): void {
		this.chat = appendChat(this.chat, {
			channel: 'System',
			sender: 'System',
			message,
			timestamp: new SvelteDate().toISOString()
		});
	}
}
