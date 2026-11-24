import type { HubConnection } from '@microsoft/signalr';
import { SvelteDate } from 'svelte/reactivity';
import { createGameHub } from '#lib/hub';
import {
	appendChat,
	applyAbilityToStats,
	applyCombatTurnToStats,
	applyDamageToStats,
	applyRestToStats,
	describeCombatTurn,
	outcomeFrom
} from './reducers';
import { craftedSummary } from './crafting';
import { toArchetypeTier } from './archetype-portraits';
import { resolvePowers, type PowerContext } from './powers';
import { applySkillProgress, resolveSkills, skillLabel } from './skills';
import type {
	AbilityUsedPayload,
	AnnouncementPayload,
	AreaSearchedPayload,
	AvailableQuest,
	CharacterAttributes,
	CharacterIgnoredPayload,
	CharacterRestedPayload,
	CharacterRespawnedPayload,
	CharacterSelectedPayload,
	CharacterStats,
	ChatCommandInfo,
	ChatMessage,
	CombatEndedPayload,
	CombatParticipantsPayload,
	CombatStartedPayload,
	CombatState,
	CombatTurnPayload,
	CraftableRecipe,
	CraftableRecipesPayload,
	ChestOpenedPayload,
	EmotePayload,
	ExperienceGainedPayload,
	GoldChangedPayload,
	GroundItem,
	InventoryItem,
	InventoryLoadedPayload,
	ItemCraftedPayload,
	ItemReceivedPayload,
	KickedPayload,
	LearnedPower,
	HarvestedPayload,
	LocationClearedPayload,
	LocationChest,
	LocationChestsPayload,
	LocationNpc,
	LocationNpcsPayload,
	NpcDialoguePayload,
	NpcOptionChosenPayload,
	NpcTalkedPayload,
	LocationEnteredPayload,
	LocationNode,
	LocationNodesPayload,
	MapGraph,
	MapGraphReceivedPayload,
	MutedPayload,
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
	SkillView,
	SkillXpGainedPayload,
	SpawnedEnemy,
	SummonedPayload,
	TeleportedPayload,
	TrackedQuest,
	WarnedPayload,
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
	/** Practised skills, seeded from the character snapshot and kept current by `SkillXpGained`. */
	skills = $state<SkillView[]>([]);
	/** Rank-up announcement shown as a toast, or `null` when there is nothing to announce. */
	skillRankUpToast = $state<{ name: string; rank: number } | null>(null);
	/**
	 * Advisory level-gate wording for the location just entered, or `null` when the arrival set no
	 * expectation. The server words it; the client only shows and dismisses it.
	 */
	levelWarning = $state<string | null>(null);
	/** The instance run the character is inside, or `null` in the shared zone. */
	instanceId = $state<string | null>(null);
	/** Rooms this run has already cleared, as the server last described them. */
	clearedLocations = $state<string[]>([]);
	/** Recipes the character could craft, as the server last described them. */
	craftableRecipes = $state<CraftableRecipe[]>([]);
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
	showSkills = $state(false);
	showCrafting = $state(false);
	showAttributes = $state(false);
	showParty = $state(false);
	loadingMap = $state(false);
	/** Item shown in the detail dialog, or `null` when it is closed. */
	inspectedItem = $state<InventoryItem | null>(null);
	/** Result of the last area search, shown in the Local panel. */
	lastSearch = $state<{ rollValue: number; discovered: SearchFinding[] } | null>(null);
	/** Resource nodes standing in this location, refreshed by the server after every attempt. */
	nodes = $state<LocationNode[]>([]);
	/** Unopened chests standing in this location, refreshed by the server after every open. */
	chests = $state<LocationChest[]>([]);
	/** People standing in this location, refreshed whenever the location changes. */
	npcs = $state<LocationNpc[]>([]);
	/** The conversation currently open, or null when the panel is closed. */
	dialogue = $state<NpcDialoguePayload | null>(null);
	/** The option key in flight, so one choice cannot be submitted twice. */
	dialogueBusy = $state<string | null>(null);
	/** Slash commands the caller may run, already filtered by permission server-side. */
	chatCommands = $state<ChatCommandInfo[]>([]);
	/** Characters muted locally with `/ignore`; their chat is hidden in every channel. */
	ignoredCharacterIds = $state<string[]>([]);

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

	/**
	 * Learned powers with their catalog details, ready for the ability panels.
	 *
	 * Falls back to placeholders built from the raw slug list when the snapshot carries no resolved
	 * powers, so the panels still list what the character knows instead of showing an empty state.
	 */
	get powers(): LearnedPower[] {
		return resolvePowers(this.character);
	}

	/** The character state the power availability rules read. */
	get powerContext(): PowerContext {
		return {
			mana: this.stats.currentMana,
			level: this.stats.level,
			inCombat: this.combat.active,
			dead: this.dead
		};
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
			// Commands are permission-filtered per character, so load them after selection.
			void this.loadChatCommands();
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

	/**
	 * Enters a named location through the server's one entry path. The refusal (for example "travel
	 * to the zone first") comes back as an `Error` broadcast, so nothing is moved optimistically.
	 */
	async enterLocation(zoneId: string, locationSlug: string): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('EnterLocation', { zoneId, locationSlug });
	}

	async openInventory(): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('GetInventory');
	}

	/** Asks for the nodes standing here; the server answers with `LocationNodes`. */
	async requestLocationNodes(): Promise<void> {
		if (!this.hub) return;
		await this.hub.invoke('GetLocationNodes');
	}

	/** Harvests one node. The yield, the refreshed list and the log line all arrive as broadcasts. */
	async harvest(nodeId: string): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('HarvestNode', { nodeId });
	}

	/** Asks for the chests standing here; the server answers with `LocationChests`. */
	async requestLocationChests(): Promise<void> {
		if (!this.hub) return;
		await this.hub.invoke('GetLocationChests');
	}

	/** Opens one chest. The loot, the refreshed list and the log line all arrive as broadcasts. */
	async openChest(chestId: string): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('OpenChest', { chestId });
	}

	/** Asks who is standing here; the server answers with `LocationNpcs`. */
	async requestLocationNpcs(): Promise<void> {
		if (!this.hub) return;
		await this.hub.invoke('GetLocationNpcs');
	}

	/** Speaks to one of them. Their line arrives as `NpcTalked`. */
	async talkToNpc(npcSlug: string): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('TalkToNpc', npcSlug);
	}

	/**
	 * Opens a conversation: the greeting (which still advances `talk:` objectives) and then the choices.
	 *
	 * Both calls on purpose. `TalkToNpc` is what quests count as "having spoken to them", and the conversation is
	 * what the player actually answers; dropping either would break something that works today.
	 */
	async startConversation(npcSlug: string): Promise<void> {
		if (!this.hub) return;
		await this.talkToNpc(npcSlug);
		await this.hub.invoke('GetNpcDialogue', { npcSlug, entrySlug: null });
	}

	/** Takes one choice. The server re-validates it and answers with `NpcOptionChosen`. */
	async chooseNpcOption(optionKey: string): Promise<void> {
		if (!this.hub || !this.dialogue) return;
		// One choice at a time: a second click while the first is in flight would be a second claim.
		if (this.dialogueBusy) return;

		this.dialogueBusy = optionKey;
		try {
			await this.hub.invoke('ChooseNpcOption', {
				npcSlug: this.dialogue.speakerSlug,
				optionKey,
				entrySlug: this.dialogue.entrySlug || null
			});
		} finally {
			this.dialogueBusy = null;
		}
	}

	/** Closes the panel without choosing anything. */
	closeDialogue(): void {
		this.dialogue = null;
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

	/**
	 * Loads the slash commands the caller may run.
	 *
	 * The hub filters by permission, so the list is authoritative: a Moderator sees `/mute`,
	 * a regular player does not.
	 */
	async loadChatCommands(): Promise<void> {
		if (!this.hub) return;

		try {
			this.chatCommands = (await this.hub.invoke<ChatCommandInfo[]>('GetChatCommands')) ?? [];
		} catch {
			// A hub that cannot list commands must not break chat; the player can still talk.
			this.chatCommands = [];
		}
	}

	async sendChat(text: string): Promise<void> {
		if (!this.hub) return;
		const trimmed = text.trim();
		if (!trimmed) return;

		// Slash commands are parsed server-side by SendChatMessage, which also handles mute checks
		// and permission gating. Plain messages keep their channel-specific paths below.
		if (trimmed.startsWith('/')) {
			await this.hub.invoke('SendChatMessage', { message: trimmed });
			return;
		}

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
			// Ground items, nodes and chests belong to the location. Their lists are cleared first so a stale
			// forest or a chest from the last room cannot follow the player, and they are pulled the same way
			// ground items are.
			this.nodes = [];
			this.chests = [];
			this.npcs = [];
			void this.requestGroundItems();
			void this.requestLocationNodes();
			void this.requestLocationChests();
			void this.requestLocationNpcs();
		});

		hub.on('LocationNodes', (payload: LocationNodesPayload) => {
			this.nodes = payload.nodes ?? [];
		});

		hub.on('LocationChests', (payload: LocationChestsPayload) => {
			this.chests = payload.chests ?? [];
		});

		hub.on('LocationNpcs', (payload: LocationNpcsPayload) => {
			this.npcs = payload.npcs ?? [];
		});

		hub.on('NpcTalked', (payload: NpcTalkedPayload) => {
			// The server words the line; the client only shows it, so the same line reads the same everywhere.
			if (payload.line) this.addSystemMessage(`${payload.displayName}: “${payload.line}”`);
		});

		hub.on('NpcDialogue', (payload: NpcDialoguePayload) => {
			this.dialogue = payload;
		});

		hub.on('NpcOptionChosen', (payload: NpcOptionChosenPayload) => {
			// The reply goes in the log as well as the panel: the panel closes, and what was said should survive it.
			if (payload.response) this.addSystemMessage(`${payload.displayName}: “${payload.response}”`);
			if (payload.applied.length > 0) this.addSystemMessage(payload.applied.join(', '));

			// A chained entry replaces the panel's contents; an ending closes it, which is how the conversation ends.
			this.dialogue = payload.next ?? null;
		});

		hub.on('ChestOpened', (payload: ChestOpenedPayload) => {
			// One player opening a shared chest empties it for everyone standing there, so the broadcast is
			// what keeps a party's list honest — not just the opener's own reply.
			this.chests = payload.remainingChests ?? [];
			if (payload.message) this.addSystemMessage(payload.message);
		});

		hub.on('Harvested', (payload: HarvestedPayload) => {
			// The node moved, so keep the list honest without waiting for the refresh that follows.
			this.nodes = this.nodes.map((node) =>
				node.nodeId === payload.nodeId
					? {
							...node,
							healthPercent: payload.nodeHealthPercent,
							state: payload.nodeState,
							canHarvest: payload.nodeHealthPercent >= 10
						}
					: node
			);
			this.addSystemMessage(payload.message);
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
					abilities: payload.enemyAbilityNames ?? [],
					archetypeSlug: payload.enemyArchetypeSlug ?? null,
					typeKey: payload.enemyArchetypeTypeKey ?? null,
					tier: toArchetypeTier(payload.enemyTier, payload.enemyArchetypeTypeKey)
				},
				log: [`Combat started: ${payload.enemyName} (level ${payload.enemyLevel}).`],
				participants: [],
				outcome: null
			};
			this.enemies = this.enemies.filter((enemy) => enemy.id !== payload.enemyId);
		});

		hub.on('CombatParticipants', (payload: CombatParticipantsPayload) => {
			// The shared snapshot is authoritative for the enemy's health and for who is fighting:
			// a party member's turn changes the enemy too, and this is how everyone learns it.
			this.combat = {
				...this.combat,
				enemy: {
					id: payload.enemyId,
					name: payload.enemyName,
					level: payload.enemyLevel,
					currentHealth: payload.enemyCurrentHealth,
					maxHealth: payload.enemyMaxHealth,
					abilities: payload.enemyAbilityNames ?? [],
					archetypeSlug: payload.enemyArchetypeSlug ?? null,
					typeKey: payload.enemyArchetypeTypeKey ?? null,
					tier: toArchetypeTier(payload.enemyTier, payload.enemyArchetypeTypeKey)
				},
				participants: payload.participants ?? []
			};
		});

		hub.on('CombatTurn', (payload: CombatTurnPayload) => {
			this.stats = applyCombatTurnToStats(this.stats, payload);
			const line = describeCombatTurn(payload);

			this.combat = {
				...this.combat,
				enemy: this.combat.enemy
					? {
							...this.combat.enemy,
							currentHealth: payload.enemyRemainingHealth ?? this.combat.enemy.currentHealth
						}
					: this.combat.enemy,
				log: [...this.combat.log, line].slice(-200),
				outcome: this.combat.outcome ?? outcomeFrom(payload)
			};

			if (payload.playerDefeated) {
				this.dead = true;
				this.applyDeathPenalty(payload.goldLost ?? 0, payload.xpLost ?? 0);
			}
			if (payload.enemyDefeated) {
				this.combat = { ...this.combat, active: false };

				// Loot landed in the bag server-side, so the sheet has to re-read it or the drop is invisible.
				if ((payload.itemsDropped?.length ?? 0) > 0) void this.openInventory();
			}
		});

		hub.on('CombatEnded', (payload: CombatEndedPayload) => {
			this.combat = {
				...this.combat,
				active: false,
				log: [...this.combat.log, `Combat ended: ${payload.reason}.`].slice(-200),
				outcome: this.combat.outcome ?? {
					kind: payload.reason.toLowerCase() === 'fled' ? 'fled' : 'defeat',
					xpEarned: 0,
					goldEarned: 0,
					xpLost: 0,
					goldLost: 0,
					hardcore: false
				}
			};
			this.addSystemMessage(`Combat ended: ${payload.reason}.`);
		});

		hub.on('EnemyDefeated', () => {
			this.addSystemMessage('Enemy defeated.');
		});

		hub.on('CharacterRespawned', (payload?: CharacterRespawnedPayload) => {
			this.dead = false;

			// The server restores the bar and removes the combat session before it answers, so the shell
			// takes both facts from this payload. Without them the sheet keeps the 0 HP the death left and
			// a fight the server no longer owns stays on screen (the death itself already sends
			// `CombatEnded`; this is the belt for a respawn that arrives first).
			if (payload) {
				this.stats = {
					...this.stats,
					currentHealth: payload.currentHealth,
					currentMana: payload.currentMana
				};
			}
			this.combat = { ...this.combat, active: false };

			// A2: death moves the character to the zone's safe location, so the view has to follow them.
			if (payload?.locationSlug) {
				this.location = {
					locationSlug: payload.locationSlug,
					locationDisplayName: payload.locationDisplayName ?? payload.locationSlug,
					typeKey: 'Location',
					spawnedEnemies: []
				};
				void this.requestGroundItems();

				// The server moves the map node as well as the location, so the graph the shell is already drawing is
				// one move out of date. Re-read it from the server rather than guessing the node: the current-node
				// marker and the travel panel both come back in the same answer.
				void this.requestMapGraph(null);
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

		hub.on('ExperienceGained', (payload: ExperienceGainedPayload) => {
			// A level-up rewrites the derived pools on the server, so the payload carries them: without this the
			// sheet would keep drawing the old bar next to the new level until the next character load. The banked
			// attribute points come along for the same reason — the allocation panel budgets from them, so a level
			// that banks points must enable it without a re-select. A payload without the field (an older server)
			// leaves the balance alone; 0 is a real balance, so the check is undefined, not truthiness.
			this.stats = {
				...this.stats,
				level: payload.newLevel,
				experience: payload.newExperience,
				...(payload.maxHealth ? { maxHealth: payload.maxHealth } : {}),
				...(payload.currentHealth !== undefined ? { currentHealth: payload.currentHealth } : {}),
				...(payload.maxMana ? { maxMana: payload.maxMana } : {}),
				...(payload.currentMana !== undefined ? { currentMana: payload.currentMana } : {}),
				...(payload.unspentAttributePoints !== undefined
					? { unspentAttributePoints: payload.unspentAttributePoints }
					: {})
			};
			if (payload.leveledUp)
				this.addSystemMessage(`Level up! You are now level ${payload.leveledUpTo}.`);
		});

		hub.on('GoldChanged', (payload: GoldChangedPayload) => {
			this.stats = { ...this.stats, gold: payload.newGoldTotal };
		});

		hub.on(
			'DamageTaken',
			(payload: { damage: number; currentHP: number; maxHP: number; isDead: boolean }) => {
				this.stats = applyDamageToStats(this.stats, payload);
				// The flag follows the server on both edges: a GM heal through /sethealth sends isDead false with the
				// restored pool, and without this the death banner would stay up until a reload.
				this.dead = payload.isDead;
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

		hub.on('SkillXpGained', (payload: SkillXpGainedPayload) => this.applySkillXp(payload));

		hub.on('CraftableRecipes', (payload: CraftableRecipesPayload) => {
			this.craftableRecipes = (payload.recipes ?? []).filter((recipe) => Boolean(recipe?.slug));
		});

		hub.on('ItemCrafted', (payload: ItemCraftedPayload) => {
			// The server has already applied the craft; this is the announcement plus a bag refresh,
			// so the sheet never shows materials that have been consumed.
			this.addSystemMessage(craftedSummary(payload));
			void this.openInventory();
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

		hub.on('LocationCleared', (payload: LocationClearedPayload) => {
			if (!this.clearedLocations.includes(payload.locationSlug)) {
				this.clearedLocations = [...this.clearedLocations, payload.locationSlug];
			}

			if (payload.firstClearOfRun) {
				this.addSystemMessage(
					payload.firstClearForCharacter
						? `Room cleared for the first time: ${payload.locationSlug}.`
						: `Room cleared: ${payload.locationSlug}.`
				);
			}
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
			// `/ignore` is a client-side filter by design: the server still delivers the message so
			// the ignore list can be changed without a round trip.
			if (payload.characterId && this.ignoredCharacterIds.includes(payload.characterId)) return;
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

		// Moderation outcomes: these were broadcast by the hub but never subscribed, so a warned,
		// muted or moved player used to see nothing at all.
		hub.on('OnWarned', (payload: WarnedPayload) => {
			const reason = payload.reason ? `: ${payload.reason}` : '';
			const count = payload.newWarnCount ? ` (${payload.newWarnCount} total)` : '';
			this.error = `A moderator warned you${reason}${count}.`;
			this.addSystemMessage(`Warning received${reason}${count}.`);
		});

		hub.on('OnMuted', (payload: MutedPayload) => {
			const until = payload.until
				? ` until ${new SvelteDate(payload.until).toLocaleString()}`
				: ' permanently';
			const reason = payload.reason ? ` Reason: ${payload.reason}` : '';
			this.addSystemMessage(`You have been muted${until}.${reason}`);
		});

		// Social broadcasts. Emotes and announcements render as chat so they land where the player
		// is already reading instead of in a modal nobody opens.
		hub.on('OnEmote', (payload: EmotePayload) => {
			this.chat = appendChat(this.chat, {
				channel: 'Zone',
				sender: payload.characterName ?? 'Someone',
				message: `* ${payload.action ?? 'gestures'} *`,
				timestamp: new SvelteDate().toISOString()
			});
		});

		hub.on('OnAnnouncement', (payload: AnnouncementPayload) => {
			if (!payload.message) return;
			this.addSystemMessage(`[${payload.severity ?? 'info'}] ${payload.message}`);
		});

		// Staff movement and grants change server-side state without the client asking, so the shell
		// has to resync rather than keep showing the old zone or bag.
		hub.on('OnTeleported', (payload: TeleportedPayload) => {
			if (!payload.zoneId) return;
			this.addSystemMessage(`You were moved to ${payload.zoneName ?? payload.zoneId}.`);
			void this.enterZone(payload.zoneId);
		});

		hub.on('OnSummoned', (payload: SummonedPayload) => {
			if (!payload.zoneId) return;
			const by = payload.byCharacterName ? ` by ${payload.byCharacterName}` : '';
			this.addSystemMessage(`You were summoned${by}.`);
			void this.enterZone(payload.zoneId);
		});

		hub.on('OnItemReceived', (payload: ItemReceivedPayload) => {
			const quantity = payload.quantity ?? 1;
			const from = payload.givenByUsername ? ` from ${payload.givenByUsername}` : '';
			this.addSystemMessage(`Received ${quantity}x ${payload.itemSlug ?? 'an item'}${from}.`);
			void this.openInventory();
		});

		hub.on('CharacterIgnored', (payload: CharacterIgnoredPayload) => {
			if (!payload.characterId) return;
			const name = payload.characterName ?? 'That player';

			if (this.ignoredCharacterIds.includes(payload.characterId)) {
				this.ignoredCharacterIds = this.ignoredCharacterIds.filter(
					(id) => id !== payload.characterId
				);
				this.addSystemMessage(`${name} is no longer ignored.`);
				return;
			}

			this.ignoredCharacterIds = [...this.ignoredCharacterIds, payload.characterId];
			this.addSystemMessage(`${name} is now ignored.`);
		});

		hub.on('ZoneLeft', () => {
			this.zone = null;
			this.location = null;
			this.occupants = [];
			this.enemies = [];
			this.map = null;
			this.currentNodeId = null;
			this.addSystemMessage('You left the zone.');
		});
	}

	private async handleCharacterSelected(payload: CharacterSelectedPayload): Promise<void> {
		this.character = payload;
		this.skills = resolveSkills(payload.skills);
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
		// A character that died before the last disconnect is still dead: the server keeps CurrentHealth at 0 until
		// a respawn, so the shell has to offer the Respawn button from the snapshot as well as from a combat event.
		this.dead = payload.currentHealth <= 0;

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

		// A resumed character already stands somewhere, and the location lists are only pushed on a *move*: ask
		// for them once here so the panel is right on arrival instead of after the player walks to a neighbour.
		void this.requestGroundItems();
		void this.requestLocationNodes();
		void this.requestLocationChests();
		void this.requestLocationNpcs();
	}

	private async handleNodeTraveled(payload: NodeTraveledPayload): Promise<void> {
		if (!payload.success) {
			this.error = payload.errorMessage ?? 'Travel failed.';
			return;
		}

		this.message = payload.message ?? null;
		if (payload.message) this.addSystemMessage(payload.message);

		// An advisory level gate, worded by the server. Arriving somewhere that sets none clears the banner.
		this.levelWarning = payload.levelWarning ?? null;
		this.instanceId = payload.zoneInstanceId ?? null;
		this.clearedLocations = [...(payload.clearedLocations ?? [])];

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

		// Arriving inside a zone, the server sends the `ZoneEntered` snapshot a walk needs — name, inn and
		// merchant flags, occupants. Arriving on a region map there is no zone at all, so the previous zone's
		// banner and occupants are dropped rather than left standing over the new map.
		if (!payload.zoneId && this.zone) {
			this.zone = null;
			this.occupants = [];
		}

		// Nodes, chests and people belong to the location, and travelling to a Location node is how most
		// locations are entered — that path emits `NodeTraveled`, not `LocationEntered`, so the lists are
		// refreshed here too.
		this.nodes = [];
		this.chests = [];
		this.npcs = [];
		void this.requestLocationNodes();
		void this.requestLocationChests();
		void this.requestLocationNpcs();

		if (payload.mapChanged) await this.requestMapGraph(payload.mapId ?? null);
	}

	/** Clears the turn-in toast once the player has seen it. */
	dismissQuestToast(): void {
		this.questTurnInToast = null;
	}

	/** Clears the rank-up toast once the player has seen it. */
	dismissSkillToast(): void {
		this.skillRankUpToast = null;
	}

	/** Clears the level-gate banner once the player has acknowledged it. */
	dismissLevelWarning(): void {
		this.levelWarning = null;
	}

	/** Opens the crafting panel and asks the server what can be made right now. */
	async openCrafting(): Promise<void> {
		this.showCrafting = true;
		await this.loadCraftableRecipes();
	}

	/** Re-reads the craftable recipe list from the server. */
	async loadCraftableRecipes(): Promise<void> {
		if (!this.hub) return;
		await this.hub.invoke('GetCraftableRecipes');
	}

	/**
	 * Crafts one recipe. Every check belongs to the server — level, skill rank and materials — and
	 * the outcome arrives as an `ItemCrafted` broadcast, so nothing is optimistically applied here.
	 */
	async craft(recipeSlug: string): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('CraftItem', recipeSlug);
	}

	/**
	 * Folds a skill XP award into the sheet.
	 *
	 * The award carries the server's own progress figures, so the bar is redrawn from them rather
	 * than from arithmetic here. An award for a skill the snapshot never resolved is appended as an
	 * unresolved entry: practising it is real even when the catalog row is missing.
	 */
	private applySkillXp(payload: SkillXpGainedPayload): void {
		const index = this.skills.findIndex((skill) => skill.skillId === payload.skillId);
		const updated = applySkillProgress(index >= 0 ? this.skills[index] : null, payload);

		if (index >= 0) this.skills[index] = updated;
		else this.skills = [...this.skills, updated];

		if (payload.rankedUp) {
			this.skillRankUpToast = { name: skillLabel(updated), rank: payload.currentRank };
		}
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
