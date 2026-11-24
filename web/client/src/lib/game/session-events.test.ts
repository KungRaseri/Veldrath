import { beforeEach, describe, expect, it, vi } from 'vitest';
import type {
	InventoryItem,
	PartyMemberView,
	PartySnapshot,
	QuestLogEntry,
	SkillView,
	SkillXpGainedPayload
} from './types';

/**
 * Second-generation session suite. `session.test.ts` covers the documented happy paths;
 * this file targets the optional-field arms of the hub event handlers and the command
 * guards that only fire while no connection exists.
 */
class FakeHub {
	readonly handlers = new Map<string, (...args: unknown[]) => void>();
	readonly invocations: { method: string; args: unknown[] }[] = [];
	readonly results = new Map<string, unknown>();
	/** Methods whose `invoke` rejects, for the error paths of fire-and-forget loaders. */
	readonly rejecting = new Set<string>();
	stopped = false;
	private closed: (() => void) | null = null;

	on(event: string, handler: (...args: unknown[]) => void): void {
		this.handlers.set(event, handler);
	}

	onreconnecting(): void {
		// Not exercised here; the lifecycle suite owns the reconnect callbacks.
	}

	onreconnected(): void {
		// Not exercised here.
	}

	onclose(callback: () => void): void {
		this.closed = callback;
	}

	async start(): Promise<void> {
		// No negotiation in the fake.
	}

	async stop(): Promise<void> {
		this.stopped = true;
	}

	async invoke(method: string, ...args: unknown[]): Promise<unknown> {
		this.invocations.push({ method, args });
		if (this.rejecting.has(method)) throw new Error(`${method} failed`);
		return this.results.get(method);
	}

	emit(event: string, ...args: unknown[]): void {
		const handler = this.handlers.get(event);
		if (!handler) throw new Error(`No handler registered for ${event}.`);
		handler(...args);
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

function item(itemRef: string, overrides: Partial<InventoryItem> = {}): InventoryItem {
	return {
		itemRef,
		name: itemRef,
		rarity: 'Common',
		type: 'Misc',
		quantity: 1,
		stackSize: 1,
		isStackable: false,
		isEquipped: false,
		price: 1,
		weight: 1,
		effectPower: 0,
		isTwoHanded: false,
		requirementsLevel: 1,
		requirements: {},
		bonuses: {},
		...overrides
	};
}

function quest(slug: string, overrides: Partial<QuestLogEntry> = {}): QuestLogEntry {
	return {
		slug,
		title: slug,
		description: '',
		status: 'Active',
		goldReward: 0,
		xpReward: 0,
		canTurnIn: false,
		objectives: [{ key: 'kill-wolves', current: 0, required: 3, complete: false }],
		...overrides
	};
}

function member(characterId: string, overrides: Partial<PartyMemberView> = {}): PartyMemberView {
	return {
		characterId,
		name: characterId,
		level: 1,
		currentHealth: 10,
		maxHealth: 10,
		isLeader: false,
		...overrides
	};
}

function party(overrides: Partial<PartySnapshot> = {}): PartySnapshot {
	return {
		partyId: 'party-1',
		leaderCharacterId: 'char-1',
		lootRule: 'FreeForAll',
		members: [member('char-1', { name: 'Thornwarden', isLeader: true })],
		...overrides
	};
}

let hub: FakeHub;

async function connected(): Promise<InstanceType<typeof GameSession>> {
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

describe('GameSession command guards', () => {
	it('does nothing for the guarded commands before a connection exists', async () => {
		const session = new GameSession();

		await session.enterZone('zone-1');
		await session.dropToGround('bread');
		await session.requestGroundItems();
		await session.allocateAttributePoints({ Strength: 1 });
		await session.loadQuests();
		await session.discard('bread');
		await session.loadChatCommands();

		expect(hub.invocations).toEqual([]);
		expect(session.showQuests).toBe(false);
	});

	it('can be disconnected twice without touching a stale hub', async () => {
		const session = await connected();
		await session.disconnect();

		await expect(session.disconnect()).resolves.toBeUndefined();

		expect(session.phase).toBe('disconnected');
		expect(hub.stopped).toBe(true);
	});

	it('orders the bag with equipped entries last, then by label', async () => {
		const session = await connected();
		session.inventory = [
			item('zed', { name: 'Zed' }),
			item('axe', { name: 'Axe', isEquipped: true }),
			item('beta', { name: 'Beta' })
		];

		expect(session.bagItems.map((entry) => entry.itemRef)).toEqual(['beta', 'zed', 'axe']);
	});

	it('attacks through the hub once connected', async () => {
		const session = await connected();
		session.error = 'stale';

		await session.attack();

		expect(session.error).toBeNull();
		expect(hub.lastCall('AttackEnemy')).toEqual([]);
	});

	it('falls back to an empty slash-command list when the hub rejects', async () => {
		const session = await connected();
		hub.rejecting.add('GetChatCommands');

		await session.loadChatCommands();

		expect(session.chatCommands).toEqual([]);
	});

	it('fails cleanly when the hub cannot be created at all', async () => {
		hubState.current = null;
		const session = new GameSession();

		await session.connect('char-1');

		expect(session.phase).toBe('failed');
		expect(typeof session.error).toBe('string');
	});
});

describe('GameSession combat and stat events', () => {
	it('applies an out-of-combat ability to the stats', async () => {
		const session = await connected();
		session.stats = {
			...session.stats,
			currentHealth: 10,
			maxHealth: 20,
			currentMana: 30
		};

		hub.emit('AbilityUsed', {
			abilityId: 'mend',
			manaCost: 5,
			remainingMana: 25,
			healthRestored: 6
		});

		expect(session.stats.currentMana).toBe(25);
		expect(session.stats.currentHealth).toBe(16);
	});

	it('kills the character when a damage event says so', async () => {
		const session = await connected();

		hub.emit('DamageTaken', { damage: 90, currentHP: 0, maxHP: 100, isDead: true });

		expect(session.dead).toBe(true);
		expect(session.stats.currentHealth).toBe(0);
	});

	it('keeps the character standing for a survivable hit', async () => {
		const session = await connected();

		hub.emit('DamageTaken', { damage: 5, currentHP: 12, maxHP: 100, isDead: false });

		expect(session.dead).toBe(false);
		expect(session.stats.currentHealth).toBe(12);
	});

	it('keeps every attribute when the server sends an empty map', async () => {
		const session = await connected();
		session.attributes = {
			strength: 10,
			dexterity: 11,
			constitution: 12,
			intelligence: 13,
			wisdom: 14,
			charisma: 15
		};

		hub.emit('AttributePointsAllocated', { remainingPoints: 3, attrs: {} });

		expect(session.attributes).toEqual({
			strength: 10,
			dexterity: 11,
			constitution: 12,
			intelligence: 13,
			wisdom: 14,
			charisma: 15
		});
		expect(session.stats.unspentAttributePoints).toBe(3);
	});

	it('announces an experience-only death penalty', async () => {
		const session = await connected();
		session.stats = { ...session.stats, gold: 20, experience: 100 };

		hub.emit('CombatTurn', {
			action: 'attack',
			playerDefeated: true,
			goldLost: 0,
			xpLost: 30
		});

		expect(session.stats.experience).toBe(70);
		expect(session.chat.at(-1)?.message).toBe('You lost 30 experience.');
	});

	it('keeps the old attributes for keys the server omitted', async () => {
		const session = await connected();
		session.attributes = {
			strength: 10,
			dexterity: 11,
			constitution: 12,
			intelligence: 13,
			wisdom: 14,
			charisma: 15
		};

		hub.emit('AttributePointsAllocated', { remainingPoints: 1, attrs: { Strength: 16 } });

		expect(session.attributes.strength).toBe(16);
		expect(session.attributes.dexterity).toBe(11);
		expect(session.stats.unspentAttributePoints).toBe(1);
	});
});

describe('GameSession map and location events', () => {
	it('empties the occupants when a zone report omits them', async () => {
		const session = await connected();
		session.occupants = [
			{ characterId: 'stale', characterName: 'Stale', enteredAt: '2026-01-01T00:00:00Z' }
		];

		hub.emit('ZoneEntered', {
			id: 'zone-1',
			name: 'Aldenmere',
			description: '',
			zoneType: 'Town',
			hasInn: true,
			hasMerchant: true
		});

		expect(session.occupants).toEqual([]);
	});

	it('empties the ground when a change event omits the item list', async () => {
		const session = await connected();

		hub.emit('GroundItemsChanged', {});

		expect(session.groundItems).toEqual([]);
	});

	it('keeps the level gate the server worded and clears it on the next arrival', async () => {
		const session = await connected();

		hub.emit('NodeTraveled', {
			success: true,
			mapChanged: false,
			locationSlug: 'maw-descent',
			recommendedLevel: 26,
			yourLevel: 3,
			levelWarning: 'This place expects level 26; you are 3.'
		});

		expect(session.levelWarning).toBe('This place expects level 26; you are 3.');

		hub.emit('NodeTraveled', {
			success: true,
			mapChanged: false,
			locationSlug: 'crestfall-square'
		});

		expect(session.levelWarning).toBeNull();
	});

	it('remembers the run and the rooms it had already cleared', async () => {
		const session = await connected();

		hub.emit('NodeTraveled', {
			success: true,
			mapChanged: false,
			locationSlug: 'rootbound-cellars',
			zoneInstanceId: 'run-1',
			clearedLocations: ['halrow-threshold']
		});

		expect(session.instanceId).toBe('run-1');
		expect(session.clearedLocations).toEqual(['halrow-threshold']);
	});

	it('appends a room to the cleared list when the server announces one', async () => {
		const session = await connected();
		session.clearedLocations = ['halrow-threshold'];

		hub.emit('LocationCleared', {
			characterId: 'char-1',
			instanceId: 'run-1',
			locationSlug: 'collapsed-vault',
			firstClearOfRun: true,
			firstClearForCharacter: true
		});

		expect(session.clearedLocations).toEqual(['halrow-threshold', 'collapsed-vault']);
		expect(session.chat.some((message) => message.message.includes('collapsed-vault'))).toBe(true);
	});

	it('drops the level gate only on a successful arrival', async () => {
		const session = await connected();
		session.levelWarning = 'This place expects level 26; you are 3.';

		hub.emit('NodeTraveled', {
			success: false,
			mapChanged: false,
			errorMessage: 'No direct path.'
		});

		expect(session.levelWarning).toBe('This place expects level 26; you are 3.');
		expect(session.error).toBe('No direct path.');
	});

	it('starts a fight even when the enemy has no ability list', async () => {
		const session = await connected();

		hub.emit('CombatStarted', {
			characterId: 'char-1',
			enemyId: 'wolf',
			enemyName: 'Grey Wolf',
			enemyLevel: 2,
			enemyCurrentHealth: 30,
			enemyMaxHealth: 30
		});

		expect(session.combat.enemy?.abilities).toEqual([]);
	});

	it('respawns at a location that has no display name', async () => {
		const session = await connected();

		hub.emit('CharacterRespawned', {
			characterId: 'char-1',
			currentHealth: 10,
			currentMana: 5,
			locationSlug: 'aldenmere-inn'
		});

		expect(session.location?.locationDisplayName).toBe('aldenmere-inn');
		expect(session.chat.at(-1)?.message).toBe('You have respawned.');
	});

	it('reloads the map only when travel moved between maps', async () => {
		await connected();

		hub.emit('NodeTraveled', { success: true, mapChanged: false, nodeId: 'market' });
		expect(hub.lastCall('GetMapGraph')).toBeUndefined();

		hub.emit('NodeTraveled', { success: true, mapChanged: true, nodeId: 'dock' });
		await flush();
		expect(hub.lastCall('GetMapGraph')).toEqual([null]);
	});

	it('re-reads the map after a respawn so the node the server moved is the one drawn', async () => {
		await connected();

		hub.emit('CharacterRespawned', {
			characterId: 'char-1',
			currentHealth: 25,
			currentMana: 40,
			locationSlug: 'sunken-estate',
			locationDisplayName: 'The Sunken Estate'
		});

		await flush();
		// The respawn moves CurrentNodeId server-side as well as the location, so the graph on screen is stale and the
		// current-node marker has to come back from the server rather than be guessed here.
		expect(hub.lastCall('GetMapGraph')).toEqual([null]);
	});

	it('skips the shop catalog request when the hub is already gone', async () => {
		const session = await connected();
		await session.disconnect();

		hub.emit('ShopVisited', { zoneId: 'zone-1', zoneName: 'Aldenmere' });

		expect(hub.methods()).not.toContain('GetShopCatalog');
		expect(session.shopZone).toBe('Aldenmere');
		expect(session.showShop).toBe(true);
	});
});

describe('GameSession quest events', () => {
	it('keeps the journal when a turn-in omits the refreshed list', async () => {
		const session = await connected();
		const current = [quest('wolves')];

		hub.emit('QuestLogReceived', current);
		hub.emit('QuestTurnedIn', { slug: 'rats', goldReward: 5, xpReward: 10 });

		expect(session.quests).toEqual(current);
		expect(session.questTurnInToast).toEqual({ slug: 'rats', gold: 5, xp: 10 });
	});

	it('announces only quests and objectives that changed since the last snapshot', async () => {
		const session = await connected();

		hub.emit('QuestLogReceived', [quest('wolves')]);
		hub.emit('QuestLogReceived', [
			quest('wolves', {
				objectives: [{ key: 'kill-wolves', current: 3, required: 3, complete: true }]
			}),
			quest('rats')
		]);

		const messages = session.chat.map((entry) => entry.message);
		expect(messages).toContain('Objective complete: kill-wolves (wolves).');
		expect(messages.filter((line) => line.includes('rats'))).toEqual([]);
	});

	it('reports a search with no roll value using a placeholder', async () => {
		const session = await connected();

		hub.emit('AreaSearched', { anyFound: false });

		expect(session.lastSearch).toEqual({ rollValue: 0, discovered: [] });
		expect(session.chat.at(-1)?.message).toBe('Search roll ?: nothing found.');
	});
});

describe('GameSession party events', () => {
	it('falls back to the first member when nobody is flagged as leader', async () => {
		const session = await connected();

		hub.emit(
			'PartyUpdated',
			party({ members: [member('char-1', { name: 'Thornwarden' }), member('char-2')] })
		);

		expect(session.chat.at(-1)?.message).toBe("You joined Thornwarden's party.");
	});

	it('stays quiet when a fresh party snapshot has no members', async () => {
		const session = await connected();

		hub.emit('PartyUpdated', party({ members: [] }));

		const messages = session.chat.map((entry) => entry.message);
		expect(session.party?.members).toEqual([]);
		expect(messages.some((line) => line.includes('You joined'))).toBe(false);
	});

	it('announces a member leaving', async () => {
		const session = await connected();
		hub.emit('PartyUpdated', party({ members: [member('char-1'), member('char-2')] }));

		hub.emit('PartyUpdated', party({ members: [member('char-1')] }));

		expect(session.chat.at(-1)?.message).toBe('A member left the party.');
	});

	it('stays quiet when the roster size is unchanged', async () => {
		const session = await connected();
		hub.emit('PartyUpdated', party({ members: [member('char-1'), member('char-2')] }));
		const before = session.chat.length;

		hub.emit('PartyUpdated', party({ members: [member('char-1'), member('char-3')] }));

		expect(session.chat).toHaveLength(before);
	});
});

describe('GameSession moderation and social events', () => {
	it('warns with the reason and running total when supplied', async () => {
		const session = await connected();

		hub.emit('OnWarned', { reason: 'Spamming', newWarnCount: 2 });

		expect(session.error).toBe('A moderator warned you: Spamming (2 total).');
		expect(session.chat.at(-1)?.message).toBe('Warning received: Spamming (2 total).');
	});

	it('warns without details when the payload is bare', async () => {
		const session = await connected();

		hub.emit('OnWarned', {});

		expect(session.error).toBe('A moderator warned you.');
		expect(session.chat.at(-1)?.message).toBe('Warning received.');
	});

	it('reports a timed mute with its reason', async () => {
		const session = await connected();

		hub.emit('OnMuted', { until: '2026-01-01T00:00:00Z', reason: 'Spamming' });

		expect(session.chat.at(-1)?.message).toContain('You have been muted until ');
		expect(session.chat.at(-1)?.message).toContain(' Reason: Spamming');
	});

	it('reports a permanent mute without a reason', async () => {
		const session = await connected();

		hub.emit('OnMuted', {});

		expect(session.chat.at(-1)?.message).toBe('You have been muted permanently.');
	});

	it('renders an anonymous emote with a default action', async () => {
		const session = await connected();

		hub.emit('OnEmote', {});

		expect(session.chat.at(-1)).toMatchObject({ sender: 'Someone', message: '* gestures *' });
	});

	it('ignores an announcement with no message', async () => {
		const session = await connected();

		hub.emit('OnAnnouncement', {});

		expect(session.chat).toEqual([]);
	});

	it('defaults an announcement severity to info', async () => {
		const session = await connected();

		hub.emit('OnAnnouncement', { message: 'Server restart in five minutes.' });

		expect(session.chat.at(-1)?.message).toBe('[info] Server restart in five minutes.');
	});

	it('ignores a teleport without a zone', async () => {
		const session = await connected();

		hub.emit('OnTeleported', {});

		expect(session.chat).toEqual([]);
		expect(hub.methods()).not.toContain('EnterZone');
	});

	it('names the destination zone by its id when the name is missing', async () => {
		const session = await connected();

		hub.emit('OnTeleported', { zoneId: 'zone-2' });
		await flush();

		expect(session.chat.at(-1)?.message).toBe('You were moved to zone-2.');
		expect(hub.lastCall('EnterZone')).toEqual(['zone-2']);
	});

	it('ignores a summon without a zone', async () => {
		const session = await connected();

		hub.emit('OnSummoned', {});

		expect(session.chat).toEqual([]);
	});

	it('summons without naming the caller', async () => {
		const session = await connected();

		hub.emit('OnSummoned', { zoneId: 'zone-2' });
		await flush();

		expect(session.chat.at(-1)?.message).toBe('You were summoned.');
	});

	it('describes a bare item grant with defaults', async () => {
		const session = await connected();

		hub.emit('OnItemReceived', {});
		await flush();

		expect(session.chat.at(-1)?.message).toBe('Received 1x an item.');
		expect(hub.methods()).toContain('GetInventory');
	});

	it('ignores an ignore confirmation without a character id', async () => {
		const session = await connected();

		hub.emit('CharacterIgnored', {});

		expect(session.ignoredCharacterIds).toEqual([]);
		expect(session.chat).toEqual([]);
	});

	it('names an unnamed ignore target generically', async () => {
		const session = await connected();

		hub.emit('CharacterIgnored', { characterId: 'char-2' });

		expect(session.ignoredCharacterIds).toEqual(['char-2']);
		expect(session.chat.at(-1)?.message).toBe('That player is now ignored.');
	});
});

describe('skill progression', () => {
	function skill(overrides: Partial<SkillView> = {}): SkillView {
		return {
			skillId: 'swordsmanship',
			resolved: true,
			name: 'Swordsmanship',
			description: 'Skill with a blade.',
			category: 'combat',
			governingAttribute: 'strength',
			rank: 2,
			totalXp: 250,
			xpIntoRank: 50,
			xpToNextRank: 100,
			maxRank: 50,
			...overrides
		};
	}

	function award(overrides: Partial<SkillXpGainedPayload> = {}): SkillXpGainedPayload {
		return {
			characterId: 'char-1',
			skillId: 'swordsmanship',
			totalXp: 260,
			currentRank: 2,
			rankedUp: false,
			xpIntoRank: 60,
			xpToNextRank: 100,
			...overrides
		};
	}

	it('seeds the sheet from the character snapshot', async () => {
		const session = await connected();

		hub.emit('CharacterSelected', { id: 'char-1', skills: [skill(), { slug: 'ignored' }] });
		await flush();

		expect(session.skills).toHaveLength(1);
		expect(session.skills[0].name).toBe('Swordsmanship');
	});

	it('updates a known skill in place from an award', async () => {
		const session = await connected();
		session.skills = [skill()];

		hub.emit('SkillXpGained', award());

		expect(session.skills).toHaveLength(1);
		expect(session.skills[0]).toMatchObject({
			skillId: 'swordsmanship',
			name: 'Swordsmanship',
			totalXp: 260,
			xpIntoRank: 60
		});
		expect(session.skillRankUpToast).toBeNull();
	});

	it('announces a rank-up and keeps the catalog copy', async () => {
		const session = await connected();
		session.skills = [skill()];

		hub.emit(
			'SkillXpGained',
			award({ totalXp: 310, currentRank: 3, rankedUp: true, xpIntoRank: 10 })
		);

		expect(session.skillRankUpToast).toEqual({ name: 'Swordsmanship', rank: 3 });
		expect(session.skills[0].category).toBe('combat');

		session.dismissSkillToast();
		expect(session.skillRankUpToast).toBeNull();
	});

	it('appends an award for a skill the sheet has never seen', async () => {
		const session = await connected();
		session.skills = [];

		hub.emit('SkillXpGained', award({ skillId: 'herbalism', totalXp: 40, currentRank: 0 }));

		expect(session.skills).toHaveLength(1);
		expect(session.skills[0].resolved).toBe(false);
		expect(session.skills[0].skillId).toBe('herbalism');
		expect(session.skillRankUpToast).toBeNull();
	});
});

describe('crafting', () => {
	it('stores the recipe list the server sends, dropping entries without a slug', async () => {
		const session = await connected();

		hub.emit('CraftableRecipes', {
			recipes: [
				{
					slug: 'iron-longsword',
					name: 'Iron longsword',
					available: true,
					materials: [],
					outputItemRef: '@items/weapons/swords:iron-longsword',
					outputQuantity: 1
				},
				{ slug: '' }
			]
		});

		expect(session.craftableRecipes).toHaveLength(1);
		expect(session.craftableRecipes[0].slug).toBe('iron-longsword');
	});

	it('asks the server for the list when the panel opens', async () => {
		const session = await connected();

		await session.openCrafting();

		expect(session.showCrafting).toBe(true);
		expect(hub.methods()).toContain('GetCraftableRecipes');
	});

	it('announces a completed craft and refreshes the bag', async () => {
		const session = await connected();

		hub.emit('ItemCrafted', {
			characterId: 'char-1',
			recipeSlug: 'iron-longsword',
			recipeName: 'Iron longsword',
			itemRef: '@items/weapons/swords:iron-longsword',
			outputQuantity: 1,
			consumedMaterials: [{ itemRef: '@items/materials/ingots:iron-ingot', quantity: 2 }],
			skillId: 'blacksmithing',
			skillXp: 15
		});
		await flush();

		expect(session.chat.at(-1)?.message).toContain(
			'Crafted 1 × @items/weapons/swords:iron-longsword'
		);
		expect(hub.methods()).toContain('GetInventory');
	});

	it('does nothing for a craft before a connection exists', async () => {
		const session = new GameSession();

		await session.craft('iron-longsword');
		await session.loadCraftableRecipes();

		expect(hub.invocations).toEqual([]);
	});
});
