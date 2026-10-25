import { beforeEach, describe, expect, it, vi } from 'vitest';
import type {
	CharacterSelectedPayload,
	ChatMessage,
	CombatStartedPayload,
	MapEdge,
	MapGraph,
	MapNode,
	SpawnedEnemy,
	ZoneEnteredPayload
} from './types';

/**
 * In-memory stand-in for the SignalR `HubConnection`: records every `invoke` and lets
 * tests raise server events through the handlers the session registered with `on`.
 */
class FakeHub {
	readonly handlers = new Map<string, (...args: unknown[]) => void>();
	readonly invocations: { method: string; args: unknown[] }[] = [];
	startError: unknown = null;
	stopError: unknown = null;
	stopped = false;
	private reconnecting: (() => void) | null = null;
	private reconnected: (() => void) | null = null;
	private closed: (() => void) | null = null;

	on(event: string, handler: (...args: unknown[]) => void): void {
		this.handlers.set(event, handler);
	}

	onreconnecting(callback: () => void): void {
		this.reconnecting = callback;
	}

	onreconnected(callback: () => void): void {
		this.reconnected = callback;
	}

	onclose(callback: () => void): void {
		this.closed = callback;
	}

	async start(): Promise<void> {
		if (this.startError) throw this.startError;
	}

	async stop(): Promise<void> {
		this.stopped = true;
		if (this.stopError) throw this.stopError;
	}

	async invoke(method: string, ...args: unknown[]): Promise<void> {
		this.invocations.push({ method, args });
	}

	emit(event: string, ...args: unknown[]): void {
		const handler = this.handlers.get(event);
		if (!handler) throw new Error(`No handler registered for ${event}.`);
		handler(...args);
	}

	raiseReconnecting(): void {
		this.reconnecting?.();
	}

	raiseReconnected(): void {
		this.reconnected?.();
	}

	raiseClose(): void {
		this.closed?.();
	}

	methods(): string[] {
		return this.invocations.map((call) => call.method);
	}

	lastCall(method: string): unknown[] | undefined {
		return this.invocations.findLast((call) => call.method === method)?.args;
	}
}

const hubState = vi.hoisted(() => ({ current: null as unknown }));

vi.mock('#lib/hub', () => ({
	createGameHub: () => hubState.current
}));

const { GameSession } = await import('./session.svelte');

/** Lets fire-and-forget handlers (`void this.handleX(...)`) finish their awaits. */
const flush = () => new Promise((resolve) => setTimeout(resolve, 0));

function node(id: string, overrides: Partial<MapNode> = {}): MapNode {
	return {
		id,
		mapId: 'map-1',
		nodeType: 'Location',
		displayName: id,
		description: '',
		isEntryPoint: false,
		isExitPoint: false,
		isHidden: false,
		sortOrder: 0,
		...overrides
	};
}

function edge(fromNodeId: string, toNodeId: string, overrides: Partial<MapEdge> = {}): MapEdge {
	return {
		id: `${fromNodeId}->${toNodeId}`,
		mapId: 'map-1',
		fromNodeId,
		toNodeId,
		direction: 'North',
		edgeType: 'Path',
		isTraversable: true,
		isHidden: false,
		...overrides
	};
}

function map(overrides: Partial<MapGraph> = {}): MapGraph {
	return {
		id: 'map-1',
		mapType: 'Zone',
		ownerId: 'zone-1',
		name: 'Aldenmere Vale',
		description: '',
		entryNodeId: 'gate',
		nodes: [node('gate'), node('market'), node('woods')],
		edges: [],
		...overrides
	};
}

function zone(overrides: Partial<ZoneEnteredPayload> = {}): ZoneEnteredPayload {
	return {
		id: 'zone-1',
		name: 'Aldenmere',
		description: '',
		zoneType: 'Town',
		occupants: [],
		hasInn: true,
		hasMerchant: true,
		...overrides
	};
}

function selected(overrides: Partial<CharacterSelectedPayload> = {}): CharacterSelectedPayload {
	return {
		id: 'char-1',
		name: 'Thornwarden',
		className: 'Warrior',
		level: 3,
		experience: 250,
		currentZoneId: 'zone-1',
		regionId: 'region-1',
		currentNodeId: 'market',
		currentHealth: 80,
		maxHealth: 100,
		currentMana: 25,
		maxMana: 40,
		gold: 42,
		unspentAttributePoints: 2,
		strength: 10,
		dexterity: 10,
		constitution: 10,
		intelligence: 10,
		wisdom: 10,
		charisma: 10,
		learnedAbilities: [],
		...overrides
	} as CharacterSelectedPayload;
}

function enemy(id: string, overrides: Partial<SpawnedEnemy> = {}): SpawnedEnemy {
	return { id, name: `Enemy ${id}`, level: 1, currentHealth: 20, maxHealth: 20, ...overrides };
}

function combatStarted(overrides: Partial<CombatStartedPayload> = {}): CombatStartedPayload {
	return {
		characterId: 'char-1',
		enemyId: 'wolf',
		enemyName: 'Grey Wolf',
		enemyLevel: 2,
		enemyCurrentHealth: 30,
		enemyMaxHealth: 30,
		enemyAbilityNames: ['bite'],
		...overrides
	};
}

function chat(channel: string, message: string): ChatMessage {
	return { channel, sender: 'Tester', message, timestamp: '2026-01-01T00:00:00Z' };
}

let hub: FakeHub;

/** Creates a session already connected to the fake hub, with the connect-time calls cleared. */
async function connected() {
	const session = new GameSession();
	await session.connect('char-1');
	hub.invocations.length = 0;
	session.chat = [];
	return session;
}

beforeEach(() => {
	hub = new FakeHub();
	hubState.current = hub;
});

describe('GameSession connection lifecycle', () => {
	it('connects, then selects the character', async () => {
		const session = new GameSession();
		const pending = session.connect('char-1');
		expect(session.phase).toBe('connecting');

		await pending;

		expect(session.phase).toBe('connected');
		expect(session.error).toBeNull();
		expect(hub.invocations).toEqual([{ method: 'SelectCharacter', args: ['char-1'] }]);
	});

	it('reports a failed start with the error message', async () => {
		hub.startError = new Error('Negotiation failed');
		const session = new GameSession();

		await session.connect('char-1');

		expect(session.phase).toBe('failed');
		expect(session.error).toBe('Negotiation failed');
		expect(hub.invocations).toEqual([]);
	});

	it('falls back to a generic message when the failure is not an Error', async () => {
		hub.startError = 'boom';
		const session = new GameSession();

		await session.connect('char-1');

		expect(session.error).toBe('Failed to connect to the game server.');
	});

	it('tracks reconnects and re-selects the character afterwards', async () => {
		const session = await connected();

		hub.raiseReconnecting();
		expect(session.phase).toBe('reconnecting');

		hub.raiseReconnected();
		await flush();

		expect(session.phase).toBe('connected');
		expect(hub.methods()).toEqual(['SelectCharacter']);
		expect(session.chat.at(-1)?.message).toBe('Reconnected. Restoring session…');
	});

	it('marks an unexpected close as failed', async () => {
		const session = await connected();
		hub.raiseClose();
		expect(session.phase).toBe('failed');
	});

	it('keeps the disconnected phase when the close was requested', async () => {
		const session = await connected();

		await session.disconnect();
		hub.raiseClose();

		expect(session.phase).toBe('disconnected');
		expect(hub.stopped).toBe(true);
	});

	it('swallows errors from stopping the connection', async () => {
		const session = await connected();
		hub.stopError = new Error('already stopped');

		await expect(session.disconnect()).resolves.toBeUndefined();
		expect(session.phase).toBe('disconnected');
	});

	it('ignores commands once disconnected', async () => {
		const session = await connected();
		await session.disconnect();

		await session.attack();
		await session.openInventory();
		await session.sendChat('hello');

		expect(hub.invocations).toEqual([]);
	});

	it('treats a moderator kick as a disconnect', async () => {
		const session = await connected();

		hub.emit('OnKicked', { reason: 'Spamming' });
		hub.raiseClose();

		expect(session.phase).toBe('disconnected');
		expect(session.error).toBe('Spamming');
	});

	it('uses a default kick reason', async () => {
		const session = await connected();
		hub.emit('OnKicked', {});
		expect(session.error).toBe('You were disconnected by a moderator.');
	});
});

describe('GameSession commands', () => {
	it('does nothing before a connection exists', async () => {
		const session = new GameSession();

		await session.selectCharacter();
		await session.requestMapGraph();
		await session.attack();

		expect(hub.invocations).toEqual([]);
	});

	it('travels on the current map', async () => {
		const session = await connected();
		session.map = map();
		session.error = 'stale';

		await session.travel('woods');

		expect(session.error).toBeNull();
		expect(hub.lastCall('TravelToNode')).toEqual([{ mapId: 'map-1', nodeId: 'woods' }]);
	});

	it('cannot travel without a map', async () => {
		const session = await connected();
		await session.travel('woods');
		expect(hub.invocations).toEqual([]);
	});

	it('flags the map as loading while it is requested', async () => {
		const session = await connected();

		await session.requestMapGraph('map-2');

		expect(session.loadingMap).toBe(true);
		expect(hub.lastCall('GetMapGraph')).toEqual(['map-2']);
	});

	it('rests at the current location with the default cost', async () => {
		const session = await connected();
		session.location = { locationSlug: 'inn', locationDisplayName: 'Inn', typeKey: 'Inn' };

		await session.rest();

		expect(hub.lastCall('RestAtLocation')).toEqual([{ locationId: 'inn', costInGold: 10 }]);
	});

	it('cannot rest or engage without a location', async () => {
		const session = await connected();

		await session.rest();
		await session.engageEnemy('wolf');

		expect(hub.invocations).toEqual([]);
	});

	it('engages an enemy at the current location', async () => {
		const session = await connected();
		session.location = { locationSlug: 'woods', locationDisplayName: 'Woods', typeKey: 'Wild' };

		await session.engageEnemy('wolf');

		expect(hub.lastCall('EngageEnemy')).toEqual([{ locationSlug: 'woods', enemyId: 'wolf' }]);
	});

	it('visits the shop only inside a zone', async () => {
		const session = await connected();

		await session.visitShop();
		expect(hub.invocations).toEqual([]);

		session.zone = zone();
		await session.visitShop();
		expect(hub.lastCall('VisitShop')).toEqual([{ zoneId: 'zone-1' }]);
	});

	it('opens the quest log and requests it', async () => {
		const session = await connected();

		await session.loadQuests();

		expect(session.showQuests).toBe(true);
		expect(hub.methods()).toEqual(['GetQuestLog']);
	});

	it('maps item and combat commands to hub methods', async () => {
		const session = await connected();

		await session.buy('potion');
		await session.sell('dagger');
		await session.drop('rock');
		await session.equip('MainHand', null);
		await session.defend();
		await session.flee();
		await session.useCombatAbility('cleave');
		await session.respawn();
		await session.searchArea();
		await session.leaveZone();
		await session.enterZone('zone-2');

		expect(hub.invocations).toEqual([
			{ method: 'BuyItem', args: ['potion'] },
			{ method: 'SellItem', args: ['dagger'] },
			{ method: 'DropItem', args: ['rock'] },
			{ method: 'EquipItem', args: [{ slot: 'MainHand', itemRef: null }] },
			{ method: 'DefendAction', args: [] },
			{ method: 'FleeFromCombat', args: [] },
			{ method: 'UseAbilityInCombat', args: [{ abilityId: 'cleave' }] },
			{ method: 'Respawn', args: [] },
			{ method: 'SearchArea', args: [] },
			{ method: 'LeaveZone', args: [] },
			{ method: 'EnterZone', args: ['zone-2'] }
		]);
	});
});

describe('GameSession chat', () => {
	it('ignores blank messages', async () => {
		const session = await connected();
		await session.sendChat('   ');
		expect(hub.invocations).toEqual([]);
	});

	it('sends trimmed zone messages by default', async () => {
		const session = await connected();
		await session.sendChat('  hello  ');
		expect(hub.invocations).toEqual([{ method: 'SendZoneMessage', args: [{ message: 'hello' }] }]);
	});

	it('sends global messages on the global channel', async () => {
		const session = await connected();
		session.chatChannel = 'Global';

		await session.sendChat('hi all');

		expect(hub.lastCall('SendGlobalMessage')).toEqual([{ message: 'hi all' }]);
	});

	it('requires a whisper target', async () => {
		const session = await connected();
		session.chatChannel = 'Whisper';
		session.whisperTarget = '  ';

		await session.sendChat('psst');

		expect(session.error).toBe('Enter a whisper target.');
		expect(hub.invocations).toEqual([]);
	});

	it('whispers to the trimmed target', async () => {
		const session = await connected();
		session.chatChannel = 'Whisper';
		session.whisperTarget = ' Mira ';

		await session.sendChat('psst');

		expect(hub.lastCall('SendWhisper')).toEqual([{ targetCharacterName: 'Mira', message: 'psst' }]);
	});

	it('filters messages for the active channel, case-insensitively', async () => {
		const session = await connected();
		for (const entry of [
			chat('zone', 'z'),
			chat('GLOBAL', 'g'),
			chat('Whisper', 'w'),
			chat('System', 's')
		]) {
			hub.emit('ReceiveChatMessage', entry);
		}

		const messages = () => session.messagesForChannel.map((entry) => entry.message);

		expect(messages()).toEqual(['z', 's']);
		session.chatChannel = 'Global';
		expect(messages()).toEqual(['g', 's']);
		session.chatChannel = 'Whisper';
		expect(messages()).toEqual(['w']);
	});

	it('records system messages and errors from the server', async () => {
		const session = await connected();

		hub.emit('SystemMessage', 'Server restarting soon.');
		hub.emit('Error', 'Not enough gold.');

		expect(session.chat.at(-1)).toMatchObject({
			channel: 'System',
			sender: 'System',
			message: 'Server restarting soon.'
		});
		expect(session.error).toBe('Not enough gold.');
	});
});

describe('GameSession character and world events', () => {
	it('announces the server on ServerInfo', async () => {
		const session = await connected();

		hub.emit('ServerInfo', { serverVersion: '1.2.3', connectionId: 'abc' });

		expect(session.serverInfo).toEqual({ serverVersion: '1.2.3', connectionId: 'abc' });
		expect(session.chat.at(-1)?.message).toBe('Connected to 1.2.3 as abc.');
	});

	it('loads the selected character, enters its zone and requests the map', async () => {
		const session = await connected();

		hub.emit('CharacterSelected', selected());
		await flush();

		expect(session.character?.name).toBe('Thornwarden');
		expect(session.stats).toEqual({
			currentHealth: 80,
			maxHealth: 100,
			currentMana: 25,
			maxMana: 40,
			gold: 42,
			level: 3,
			experience: 250,
			unspentAttributePoints: 2
		});
		expect(session.currentNodeId).toBe('market');
		expect(hub.invocations).toEqual([
			{ method: 'EnterZone', args: ['zone-1'] },
			{ method: 'GetMapGraph', args: [null] }
		]);
	});

	it('skips entering a zone when the character is on the region map', async () => {
		const session = await connected();

		hub.emit('CharacterSelected', selected({ currentZoneId: null, currentNodeId: undefined }));
		await flush();

		expect(session.currentNodeId).toBeNull();
		expect(hub.methods()).toEqual(['GetMapGraph']);
	});

	it('reports a character that is active elsewhere', async () => {
		const session = await connected();
		hub.emit('CharacterAlreadyActive');
		expect(session.error).toBe('This character is already active in another session.');
	});

	it('enters a zone with its occupants', async () => {
		const session = await connected();
		const occupants = [{ characterId: 'c2', characterName: 'Mira', enteredAt: 'now' }];

		hub.emit('ZoneEntered', zone({ occupants }));

		expect(session.zone?.name).toBe('Aldenmere');
		expect(session.occupants).toEqual(occupants);
		expect(session.chat.at(-1)?.message).toBe('Entered Aldenmere.');
	});

	it('adds arriving players once and removes leaving players', async () => {
		const session = await connected();

		hub.emit('PlayerEntered', { characterId: 'c2', characterName: 'Mira' });
		hub.emit('PlayerEntered', { characterId: 'c2', characterName: 'Mira' });
		hub.emit('PlayerEntered', { characterId: 'c3', characterName: 'Oren' });
		expect(session.occupants.map((entry) => entry.characterName)).toEqual(['Mira', 'Oren']);
		expect(session.occupants[0].enteredAt).toMatch(/^\d{4}-\d{2}-\d{2}T/);

		hub.emit('PlayerLeft', { characterId: 'c2' });
		expect(session.occupants.map((entry) => entry.characterName)).toEqual(['Oren']);
	});

	it('replaces the visible enemies when a location is entered', async () => {
		const session = await connected();
		const payload = {
			locationSlug: 'woods',
			locationDisplayName: 'Woods',
			typeKey: 'Wild',
			spawnedEnemies: [enemy('wolf')]
		};

		hub.emit('LocationEntered', payload);
		expect(session.location).toEqual(payload);
		expect(session.enemies).toEqual([enemy('wolf')]);

		hub.emit('LocationEntered', { ...payload, spawnedEnemies: null });
		expect(session.enemies).toEqual([]);
	});

	it('derives rest and shop availability from the zone', async () => {
		const session = await connected();
		expect(session.canRest).toBe(false);
		expect(session.canShop).toBe(false);

		session.zone = zone({ hasInn: true, hasMerchant: false });
		expect(session.canRest).toBe(false);

		session.location = { locationSlug: 'inn', locationDisplayName: 'Inn', typeKey: 'Inn' };
		expect(session.canRest).toBe(true);
		expect(session.canShop).toBe(false);
	});

	it('labels the zone, falling back to the map name and then the region', async () => {
		const session = await connected();
		expect(session.zoneLabel).toBe('Region map');

		session.map = map({ mapType: 'Region', name: 'Westreach' });
		expect(session.zoneLabel).toBe('Region map');

		session.map = map({ mapType: 'Zone', name: 'Aldenmere Vale' });
		expect(session.zoneLabel).toBe('Aldenmere Vale');

		session.zone = zone({ name: 'Aldenmere' });
		expect(session.zoneLabel).toBe('Aldenmere');
	});
});

describe('GameSession map', () => {
	it('stores a received map and starts on the reported node', async () => {
		const session = await connected();
		session.loadingMap = true;

		hub.emit('MapGraphReceived', { success: true, map: map(), currentNodeId: 'market' });

		expect(session.loadingMap).toBe(false);
		expect(session.map?.id).toBe('map-1');
		expect(session.currentNodeId).toBe('market');
		expect(session.selectedNodeId).toBe('market');
		expect(session.currentNode?.id).toBe('market');
	});

	it('falls back to the entry node, then the first node, then nothing', async () => {
		const session = await connected();

		hub.emit('MapGraphReceived', { success: true, map: map() });
		expect(session.currentNodeId).toBe('gate');

		hub.emit('MapGraphReceived', { success: true, map: map({ entryNodeId: null }) });
		expect(session.currentNodeId).toBe('gate');

		hub.emit('MapGraphReceived', {
			success: true,
			map: map({ entryNodeId: null, nodes: [node('woods'), node('gate')] })
		});
		expect(session.currentNodeId).toBe('woods');

		hub.emit('MapGraphReceived', { success: true, map: map({ entryNodeId: null, nodes: [] }) });
		expect(session.currentNodeId).toBeNull();
		expect(session.currentNode).toBeNull();
	});

	it('reports a failed map load and keeps the previous map', async () => {
		const session = await connected();
		session.map = map();
		session.loadingMap = true;

		hub.emit('MapGraphReceived', { success: false, errorMessage: 'No such map.' });
		expect(session.loadingMap).toBe(false);
		expect(session.error).toBe('No such map.');
		expect(session.map?.id).toBe('map-1');

		hub.emit('MapGraphReceived', { success: true, map: null });
		expect(session.error).toBe('Failed to load the map.');
	});

	it('lists unique, visible, traversable neighbours of the current node', async () => {
		const session = await connected();
		expect(session.adjacentNodeIds).toEqual([]);

		session.map = map({
			edges: [
				edge('gate', 'market'),
				edge('gate', 'market', { id: 'duplicate', direction: 'East' }),
				edge('gate', 'woods', { isHidden: true }),
				edge('gate', 'cave', { isTraversable: false }),
				edge('market', 'woods')
			]
		});
		expect(session.adjacentNodeIds).toEqual([]);

		session.currentNodeId = 'gate';
		expect(session.adjacentNodeIds).toEqual(['market']);
	});

	it('selects a node', async () => {
		const session = await connected();
		session.map = map();

		session.selectNode('woods');

		expect(session.selectedNodeId).toBe('woods');
		expect(session.selectedNode?.id).toBe('woods');
	});

	it('moves to the travelled node and builds the location from the result', async () => {
		const session = await connected();

		hub.emit('NodeTraveled', {
			success: true,
			mapChanged: false,
			nodeId: 'woods',
			nodeType: 'Wilderness',
			locationSlug: 'dark-woods',
			message: 'Travelled to Dark Woods.',
			spawnedEnemies: [enemy('wolf')]
		});
		await flush();

		expect(session.currentNodeId).toBe('woods');
		expect(session.selectedNodeId).toBe('woods');
		expect(session.message).toBe('Travelled to Dark Woods.');
		expect(session.chat.at(-1)?.message).toBe('Travelled to Dark Woods.');
		expect(session.location).toEqual({
			locationSlug: 'dark-woods',
			locationDisplayName: 'Dark Woods',
			typeKey: 'Wilderness',
			spawnedEnemies: [enemy('wolf')]
		});
		expect(session.enemies).toEqual([enemy('wolf')]);
		expect(hub.invocations).toEqual([]);
	});

	it('names the location by its slug when the result has no message', async () => {
		const session = await connected();

		hub.emit('NodeTraveled', { success: true, mapChanged: false, locationSlug: 'dark-woods' });
		await flush();

		expect(session.message).toBeNull();
		expect(session.location).toMatchObject({
			locationDisplayName: 'dark-woods',
			typeKey: 'Location',
			spawnedEnemies: []
		});
	});

	it('clears the location when travelling to a node without one', async () => {
		const session = await connected();
		session.location = { locationSlug: 'inn', locationDisplayName: 'Inn', typeKey: 'Inn' };
		session.enemies = [enemy('wolf')];

		hub.emit('NodeTraveled', { success: true, mapChanged: false, nodeId: 'crossroads' });
		await flush();

		expect(session.location).toBeNull();
		expect(session.enemies).toEqual([]);
	});

	it('requests the new map when travel changes maps', async () => {
		const session = await connected();

		hub.emit('NodeTraveled', { success: true, mapChanged: true, mapId: 'map-2', nodeId: 'dock' });
		await flush();

		expect(session.loadingMap).toBe(true);
		expect(hub.invocations).toEqual([{ method: 'GetMapGraph', args: ['map-2'] }]);
	});

	it('reports failed travel without moving', async () => {
		const session = await connected();
		session.currentNodeId = 'gate';

		hub.emit('NodeTraveled', { success: false, mapChanged: false, nodeId: 'woods' });
		await flush();

		expect(session.error).toBe('Travel failed.');
		expect(session.currentNodeId).toBe('gate');
	});

	it('reports the server reason when travel fails', async () => {
		const session = await connected();

		hub.emit('NodeTraveled', { success: false, mapChanged: false, errorMessage: 'Path blocked.' });
		await flush();

		expect(session.error).toBe('Path blocked.');
	});

	it('announces search results', async () => {
		const session = await connected();

		hub.emit('AreaSearched', {
			rollValue: 17,
			anyFound: true,
			discovered: [{ slug: 'cave', displayName: 'Cave', typeKey: 'Dungeon' }]
		});
		expect(session.chat.at(-1)?.message).toBe('Search roll 17: discovered 1 location(s).');

		hub.emit('AreaSearched', { rollValue: 3, anyFound: false });
		expect(session.chat.at(-1)?.message).toBe('Search roll 3: nothing found.');
	});
});

describe('GameSession combat', () => {
	it('starts combat and removes the engaged enemy from the location', async () => {
		const session = await connected();
		session.enemies = [enemy('wolf'), enemy('boar')];

		hub.emit('CombatStarted', combatStarted());

		expect(session.combat).toEqual({
			active: true,
			enemy: {
				id: 'wolf',
				name: 'Grey Wolf',
				level: 2,
				currentHealth: 30,
				maxHealth: 30,
				abilities: ['bite']
			},
			log: ['Combat started: Grey Wolf (level 2).']
		});
		expect(session.enemies.map((entry) => entry.id)).toEqual(['boar']);
	});

	it('applies combat turns to the enemy and the log', async () => {
		const session = await connected();
		hub.emit('CombatStarted', combatStarted());

		hub.emit('CombatTurn', { action: 'attack', playerDamage: 12, enemyRemainingHealth: 18 });

		expect(session.combat.active).toBe(true);
		expect(session.combat.enemy?.currentHealth).toBe(18);
		expect(session.combat.log).toHaveLength(2);
	});

	it('keeps the enemy health when a turn does not report it', async () => {
		const session = await connected();
		hub.emit('CombatStarted', combatStarted());

		hub.emit('CombatTurn', { action: 'defend' });

		expect(session.combat.enemy?.currentHealth).toBe(30);
	});

	it('keeps only the latest 25 combat log lines', async () => {
		const session = await connected();
		hub.emit('CombatStarted', combatStarted());

		for (let turn = 0; turn < 30; turn++) hub.emit('CombatTurn', { action: 'defend' });

		expect(session.combat.log).toHaveLength(25);
		expect(session.combat.log[0]).not.toContain('Combat started');
	});

	it('ends combat when the enemy is defeated', async () => {
		const session = await connected();
		hub.emit('CombatStarted', combatStarted());

		hub.emit('CombatTurn', { action: 'attack', enemyRemainingHealth: 0, enemyDefeated: true });

		expect(session.combat.active).toBe(false);
		expect(session.combat.enemy?.currentHealth).toBe(0);
	});

	it('marks the player dead when a turn defeats them', async () => {
		const session = await connected();
		hub.emit('CombatStarted', combatStarted());

		hub.emit('CombatTurn', { action: 'attack', playerRemainingHealth: 0, playerDefeated: true });

		expect(session.dead).toBe(true);
	});

	it('closes combat with the reason', async () => {
		const session = await connected();
		hub.emit('CombatStarted', combatStarted());

		hub.emit('CombatEnded', { characterId: 'char-1', reason: 'Fled' });

		expect(session.combat.active).toBe(false);
		expect(session.combat.enemy).toBeNull();
		expect(session.combat.log.at(-1)).toBe('Combat ended: Fled.');
		expect(session.chat.at(-1)?.message).toBe('Combat ended: Fled.');
	});

	it('marks death from damage and clears it on respawn', async () => {
		const session = await connected();

		hub.emit('DamageTaken', { damage: 50, currentHP: 0, maxHP: 100, isDead: true });
		expect(session.dead).toBe(true);
		expect(session.stats.currentHealth).toBe(0);

		hub.emit('CharacterRespawned');
		expect(session.dead).toBe(false);
		expect(session.chat.at(-1)?.message).toBe('You have respawned.');
	});

	it('announces defeated enemies', async () => {
		const session = await connected();
		hub.emit('EnemyDefeated');
		expect(session.chat.at(-1)?.message).toBe('Enemy defeated.');
	});
});

describe('GameSession progression and economy', () => {
	it('updates level and experience, announcing level ups', async () => {
		const session = await connected();

		hub.emit('ExperienceGained', { newLevel: 3, newXP: 280, leveledUp: false, leveledUpTo: 0 });
		expect(session.stats).toMatchObject({ level: 3, experience: 280 });
		expect(session.chat).toEqual([]);

		hub.emit('ExperienceGained', { newLevel: 4, newXP: 10, leveledUp: true, leveledUpTo: 4 });
		expect(session.stats.level).toBe(4);
		expect(session.chat.at(-1)?.message).toBe('Level up! You are now level 4.');
	});

	it('tracks gold from every gold-changing event', async () => {
		const session = await connected();

		hub.emit('GoldChanged', { goldAdded: 5, newGoldTotal: 47 });
		expect(session.stats.gold).toBe(47);

		hub.emit('ItemPurchased', { itemRef: 'potion', goldSpent: 7, remainingGold: 40 });
		expect(session.stats.gold).toBe(40);
		expect(session.chat.at(-1)?.message).toBe('Purchased potion.');

		hub.emit('ItemSold', { itemRef: 'dagger', goldReceived: 12, newGoldTotal: 52 });
		expect(session.stats.gold).toBe(52);
		expect(session.chat.at(-1)?.message).toBe('Sold dagger for 12 gold.');
	});

	it('updates unspent attribute points', async () => {
		const session = await connected();
		hub.emit('AttributePointsAllocated', { remainingPoints: 0, attrs: { strength: 12 } });
		expect(session.stats.unspentAttributePoints).toBe(0);
	});

	it('restores stats after resting', async () => {
		const session = await connected();

		hub.emit('CharacterRested', {
			locationId: 'inn',
			currentHealth: 100,
			maxHealth: 100,
			currentMana: 40,
			maxMana: 40,
			goldRemaining: 32
		});

		expect(session.stats).toMatchObject({ currentHealth: 100, currentMana: 40, gold: 32 });
		expect(session.chat.at(-1)?.message).toBe('You feel rested.');
	});

	it('loads the inventory and opens the overlay unless told not to', async () => {
		const session = await connected();
		const sword = { itemRef: 'sword', name: 'Sword' };

		hub.emit('InventoryLoaded', { itemDetails: [sword], equippedItems: { MainHand: sword } });
		expect(session.inventory).toEqual([sword]);
		expect(session.equippedEntries).toEqual([['MainHand', sword]]);
		expect(session.showInventory).toBe(true);

		session.showInventory = false;
		hub.emit('InventoryLoaded', { itemDetails: null, equippedItems: null, openOverlay: false });
		expect(session.inventory).toEqual([]);
		expect(session.equipped).toEqual({});
		expect(session.showInventory).toBe(false);
	});

	it('updates equipment and announces equips and unequips', async () => {
		const session = await connected();
		const sword = { itemRef: 'sword' };

		hub.emit('ItemEquipped', { slot: 'MainHand', itemRef: 'sword', equipped: { MainHand: sword } });
		expect(session.equipped).toEqual({ MainHand: sword });
		expect(session.chat.at(-1)?.message).toBe('Equipped sword.');

		hub.emit('ItemEquipped', { slot: 'MainHand', itemRef: null });
		expect(session.equipped).toEqual({ MainHand: sword });
		expect(session.chat.at(-1)?.message).toBe('Unequipped MainHand.');
	});

	it('announces dropped items with the remaining quantity', async () => {
		const session = await connected();

		hub.emit('ItemDropped', { itemRef: 'arrow', remainingQuantity: 9 });
		expect(session.chat.at(-1)?.message).toBe('Dropped arrow (9 left).');

		hub.emit('ItemDropped', { itemRef: 'rock', remainingQuantity: 0 });
		expect(session.chat.at(-1)?.message).toBe('Dropped rock.');
	});

	it('opens the shop and requests its catalog', async () => {
		const session = await connected();

		hub.emit('ShopVisited', { zoneId: 'zone-1', zoneName: 'Aldenmere' });

		expect(session.shopZone).toBe('Aldenmere');
		expect(session.showShop).toBe(true);
		expect(hub.methods()).toEqual(['GetShopCatalog']);
	});

	it('stores the shop catalog', async () => {
		const session = await connected();
		const catalog = [{ slug: 'potion', name: 'Potion', price: 7 }];

		hub.emit('ShopCatalog', { zoneId: 'zone-1', catalog });
		expect(session.shopCatalog).toEqual(catalog);

		hub.emit('ShopCatalog', { zoneId: 'zone-1', catalog: null });
		expect(session.shopCatalog).toEqual([]);
	});

	it('accepts the quest log as an array or a wrapped object', async () => {
		const session = await connected();
		const quest = { slug: 'rats', title: 'Rat Problem' };

		hub.emit('QuestLogReceived', [quest]);
		expect(session.quests).toEqual([quest]);

		hub.emit('QuestLogReceived', { quests: [] });
		expect(session.quests).toEqual([]);

		hub.emit('QuestLogReceived', {});
		expect(session.quests).toEqual([]);
	});
});
