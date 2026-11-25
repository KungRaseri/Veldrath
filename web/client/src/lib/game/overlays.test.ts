import { fireEvent, render, screen, within } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import GameOverlays from './GameOverlays.svelte';
import { GameSession } from './session.svelte';
import type { InventoryItem, QuestLogEntry, SkillView, ZoneEnteredPayload } from './types';

// `#lib/hub` imports `$app/env/public`, whose runtime needs a SvelteKit dev global that does not
// exist under Vitest. Every session method guards on `this.hub`, so a null factory keeps the
// components renderable while the button handlers stay harmless.
vi.mock('#lib/hub', () => ({ createGameHub: () => null }));

/**
 * The modal overlays: quest log, shop, abilities, attributes, item details, party, settings, and
 * the two toast strips.
 *
 * Each dialog is rendered on its own — one `show*` flag open at a time — so a query like
 * `getByRole('button', { name: 'Use' })` cannot be satisfied by a neighbouring dialog. That is
 * also why the component is rendered fresh per case: a shared instance would accumulate state.
 *
 * The empty branch and the populated branch are asserted for every list because a shop that
 * renders nothing and a shop that failed to load look the same to a player.
 */

function session(): GameSession {
	return new GameSession();
}

function item(overrides: Partial<InventoryItem> = {}): InventoryItem {
	return {
		itemRef: 'item-1',
		name: 'Ember blade',
		description: 'A blade of banked fire.',
		rarity: 'Rare',
		type: 'Weapon',
		typeKey: 'Sword',
		slot: 'MainHand',
		quantity: 1,
		stackSize: 1,
		isStackable: false,
		isEquipped: false,
		price: 120,
		weight: 3,
		durability: null,
		effect: null,
		effectPower: 0,
		isTwoHanded: false,
		armorClass: null,
		armorType: null,
		weaponType: 'sword',
		damageMin: 4,
		damageMax: 9,
		requirementsLevel: 1,
		requirements: {},
		bonuses: {},
		...overrides
	};
}

function quest(overrides: Partial<QuestLogEntry> = {}): QuestLogEntry {
	return {
		slug: 'market-rats',
		title: 'Rats in the market',
		description: 'The grain stores are overrun.',
		status: 'Active',
		goldReward: 25,
		xpReward: 60,
		canTurnIn: true,
		objectives: [{ key: 'kill:rat', current: 3, required: 3, complete: true }],
		...overrides
	};
}

const zone: ZoneEnteredPayload = {
	id: 'emberfall',
	name: 'Emberfall',
	description: 'A market town.',
	zoneType: 'Town',
	occupants: [],
	hasInn: true,
	hasMerchant: true
};

beforeEach(() => {
	document.body.innerHTML = '';
});

describe('quest log', () => {
	it('shows the empty state when the journal is empty', () => {
		const game = session();
		game.showQuests = true;
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Quest log' });
		expect(within(dialog).getByText('No quests yet.')).toBeTruthy();
		expect(within(dialog).queryByRole('button', { name: 'Accept' })).toBeNull();
	});

	it('renders an active quest with objectives and both actions', async () => {
		const game = session();
		game.showQuests = true;
		game.quests = [quest()];
		const turnIn = vi.spyOn(game, 'turnInQuest');
		const abandon = vi.spyOn(game, 'abandonQuest');
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Quest log' });
		expect(within(dialog).getByText('Rats in the market')).toBeTruthy();
		expect(within(dialog).getByText('The grain stores are overrun.')).toBeTruthy();
		// Objective keys are humanised: `kill:rat` becomes `kill · rat`.
		expect(within(dialog).getByText('kill · rat')).toBeTruthy();
		expect(within(dialog).getByText('3/3')).toBeTruthy();
		expect(within(dialog).getByText(/25 g · 60 XP/)).toBeTruthy();

		await fireEvent.click(within(dialog).getByRole('button', { name: 'Turn in' }));
		await fireEvent.click(within(dialog).getByRole('button', { name: 'Abandon' }));

		expect(turnIn).toHaveBeenCalledWith('market-rats');
		expect(abandon).toHaveBeenCalledWith('market-rats');
	});

	it('disables Turn in until the objectives are complete and hides actions on a finished quest', () => {
		const game = session();
		game.showQuests = true;
		game.quests = [
			quest({ canTurnIn: false, description: '', objectives: [] }),
			quest({ slug: 'done', title: 'A finished errand', status: 'Completed' })
		];
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Quest log' });
		expect(
			(within(dialog).getByRole('button', { name: 'Turn in' }) as HTMLButtonElement).disabled
		).toBe(true);
		expect(within(dialog).getAllByRole('button', { name: 'Abandon' })).toHaveLength(1);
	});

	it('offers available quests for acceptance', async () => {
		const game = session();
		game.showQuests = true;
		game.quests = [];
		game.availableQuests = [
			{
				slug: 'survey-the-road',
				title: 'Survey the road',
				description: 'Walk the north road.',
				goldReward: 10,
				xpReward: 20,
				objectives: []
			}
		];
		const accept = vi.spyOn(game, 'acceptQuest');
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Quest log' });
		expect(within(dialog).getByRole('heading', { name: 'Available' })).toBeTruthy();
		expect(within(dialog).getByText('Survey the road')).toBeTruthy();

		await fireEvent.click(within(dialog).getByRole('button', { name: 'Accept' }));
		expect(accept).toHaveBeenCalledWith('survey-the-road');
	});
});

describe('shop', () => {
	it('shows the empty state with the zone in the title', () => {
		const game = session();
		game.showShop = true;
		game.shopZone = 'Emberfall';
		render(GameOverlays, { session: game });

		// The dialog's accessible name is its title, which carries the zone.
		const dialog = screen.getByRole('dialog', { name: /Shop/ });
		expect(within(dialog).getByText('No wares available.')).toBeTruthy();
		expect(within(dialog).getByText(/Shop · Emberfall/)).toBeTruthy();
	});

	it('lists wares with prices and disables what the player cannot afford', async () => {
		const game = session();
		game.showShop = true;
		game.shopZone = null;
		game.stats = { ...game.stats, gold: 50 };
		game.shopCatalog = [
			{ slug: 'potion', name: 'Healing potion', rarity: 'Common', price: 10 },
			{ slug: 'blade', name: 'Ember blade', price: 500 }
		];
		const buy = vi.spyOn(game, 'buy');
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Shop' });
		expect(within(dialog).getByText('Healing potion')).toBeTruthy();
		expect(within(dialog).getByText('Common')).toBeTruthy();
		// A catalog entry with no rarity falls back to the word `Item`.
		expect(within(dialog).getByText('Item')).toBeTruthy();
		expect(within(dialog).queryByText(/Shop ·/)).toBeNull();

		const [cheap, dear] = within(dialog).getAllByRole('button', { name: /Buy/ });
		expect((cheap as HTMLButtonElement).disabled).toBe(false);
		expect((dear as HTMLButtonElement).disabled).toBe(true);

		await fireEvent.click(cheap);
		expect(buy).toHaveBeenCalledWith('potion');
	});
});

/** A resolved power, as the server sends it once the catalog lookup succeeds. */
function arcaneBolt() {
	return {
		slug: 'arcane_bolt',
		resolved: true,
		displayName: 'Arcane bolt',
		description: 'Hurls a shard of force.',
		school: 'arcane',
		powerType: 'Spell',
		effectType: 'Damage',
		manaCost: 5,
		cooldown: 2,
		baseDamage: '2d6',
		damageType: 'arcane',
		tier: 1,
		requiredLevel: 1
	};
}

describe('abilities', () => {
	it('shows the empty state before a character is selected', () => {
		const game = session();
		game.showAbilities = true;
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Abilities' });
		expect(within(dialog).getByText('No abilities learned yet.')).toBeTruthy();
	});

	it('names the ability, lists what it costs and uses it outside combat', async () => {
		const game = session();
		game.showAbilities = true;
		game.stats = { ...game.stats, currentMana: 20, maxMana: 20, level: 5 };
		game.character = { learnedAbilities: ['arcane_bolt'], learnedPowers: [arcaneBolt()] } as never;
		const use = vi.spyOn(game, 'useAbility');
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Abilities' });
		expect(within(dialog).getByText('Arcane bolt')).toBeTruthy();
		expect(within(dialog).getByText('Hurls a shard of force.')).toBeTruthy();
		expect(within(dialog).getByText('5')).toBeTruthy();
		expect(within(dialog).getByText('2 turns')).toBeTruthy();
		expect(within(dialog).getByText('2d6 arcane')).toBeTruthy();

		const button = within(dialog).getByRole('button', {
			name: 'Use Arcane bolt'
		}) as HTMLButtonElement;
		expect(button.disabled).toBe(false);

		await fireEvent.click(button);
		expect(use).toHaveBeenCalledWith('arcane_bolt');
	});

	it('explains why the ability cannot be used during combat or while dead', async () => {
		const game = session();
		game.showAbilities = true;
		game.stats = { ...game.stats, currentMana: 20, maxMana: 20 };
		game.character = { learnedAbilities: ['arcane_bolt'], learnedPowers: [arcaneBolt()] } as never;
		game.combat = { ...game.combat, active: true };
		const { unmount } = render(GameOverlays, { session: game });

		expect(
			(screen.getByRole('button', { name: 'Use Arcane bolt' }) as HTMLButtonElement).disabled
		).toBe(true);
		expect(screen.getByText('Finish the fight first — use it from the combat panel.')).toBeTruthy();
		unmount();

		document.body.innerHTML = '';
		const dead = session();
		dead.showAbilities = true;
		dead.stats = { ...dead.stats, currentMana: 20, maxMana: 20 };
		dead.character = { learnedAbilities: ['arcane_bolt'], learnedPowers: [arcaneBolt()] } as never;
		dead.dead = true;
		render(GameOverlays, { session: dead });

		expect(
			(screen.getByRole('button', { name: 'Use Arcane bolt' }) as HTMLButtonElement).disabled
		).toBe(true);
		expect(screen.getByText('You cannot use abilities while dead.')).toBeTruthy();
	});

	it('names an ability the catalog could not resolve and refuses it', () => {
		const game = session();
		game.showAbilities = true;
		game.character = { learnedAbilities: ['ghost_power'] } as never;
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Abilities' });
		expect(within(dialog).getByText('Unknown ability (ghost_power)')).toBeTruthy();
		expect(within(dialog).getByText('The catalog has no entry for this ability.')).toBeTruthy();
		expect(
			(
				within(dialog).getByRole('button', {
					name: 'Use Unknown ability (ghost_power)'
				}) as HTMLButtonElement
			).disabled
		).toBe(true);
	});
});

describe('attribute allocation', () => {
	it('counts pending points and sends capitalised allocations', async () => {
		const game = session();
		game.showAttributes = true;
		game.stats = { ...game.stats, unspentAttributePoints: 2 };
		const allocate = vi.spyOn(game, 'allocateAttributePoints').mockResolvedValue(undefined);
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Attributes' });
		expect(within(dialog).getByText('2 of 2 unspent points left.')).toBeTruthy();
		expect(
			(within(dialog).getByRole('button', { name: /^Spend/ }) as HTMLButtonElement).disabled
		).toBe(true);

		await fireEvent.click(within(dialog).getByRole('button', { name: 'Increase Strength' }));
		expect(within(dialog).getByText('1 of 2 unspent points left.')).toBeTruthy();
		expect(within(dialog).getByText('+1')).toBeTruthy();

		// Two pending points on one attribute exercises the "do not delete the key" branch.
		await fireEvent.click(within(dialog).getByRole('button', { name: 'Increase Strength' }));
		expect(
			(within(dialog).getByRole('button', { name: 'Increase Strength' }) as HTMLButtonElement)
				.disabled
		).toBe(true);
		expect(within(dialog).getByText('+2')).toBeTruthy();

		await fireEvent.click(within(dialog).getByRole('button', { name: 'Decrease Strength' }));
		expect(within(dialog).getByRole('button', { name: 'Spend 1 point' })).toBeTruthy();
		expect(within(dialog).getByText('+1')).toBeTruthy();

		await fireEvent.click(within(dialog).getByRole('button', { name: /^Spend/ }));
		expect(allocate).toHaveBeenCalledWith({ Strength: 1 });
		expect(game.showAttributes).toBe(false);
	});

	it('decrements the last pending point back to zero', async () => {
		const game = session();
		game.showAttributes = true;
		game.stats = { ...game.stats, unspentAttributePoints: 1 };
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Attributes' });
		await fireEvent.click(within(dialog).getByRole('button', { name: 'Increase Intelligence' }));
		expect(within(dialog).getByText('+1')).toBeTruthy();
		await fireEvent.click(within(dialog).getByRole('button', { name: 'Decrease Intelligence' }));

		expect(within(dialog).queryByText('+1')).toBeNull();
		expect(
			(within(dialog).getByRole('button', { name: 'Decrease Intelligence' }) as HTMLButtonElement)
				.disabled
		).toBe(true);
	});

	it('closes without spending when cancelled', async () => {
		const game = session();
		game.showAttributes = true;
		game.stats = { ...game.stats, unspentAttributePoints: 1 };
		const allocate = vi.spyOn(game, 'allocateAttributePoints');
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Attributes' });
		await fireEvent.click(within(dialog).getByRole('button', { name: 'Increase Strength' }));
		await fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }));

		expect(game.showAttributes).toBe(false);
		expect(allocate).not.toHaveBeenCalled();
	});
});

describe('item details', () => {
	it('renders every optional stat row for an owned weapon', async () => {
		const game = session();
		game.zone = zone;
		game.inspectedItem = item({
			durability: 80,
			isTwoHanded: true,
			requirementsLevel: 3,
			requirements: { Strength: 12 },
			bonuses: { Strength: 2 },
			quantity: 3,
			stackSize: 20,
			damageMin: 4,
			damageMax: 9
		});
		const equip = vi.spyOn(game, 'equipItem');
		const sell = vi.spyOn(game, 'sell');
		render(GameOverlays, { session: game });

		// The dialog is named after the item, not the literal "Item details" aria-label.
		const dialog = screen.getByRole('dialog', { name: 'Ember blade' });
		expect(within(dialog).getByText('A blade of banked fire.')).toBeTruthy();
		expect(within(dialog).getByText('4–9 damage · two-handed')).toBeTruthy();
		expect(within(dialog).getByText('80%')).toBeTruthy();
		expect(within(dialog).getByText('Level 3 · Strength 12')).toBeTruthy();
		expect(within(dialog).getByText('Strength +2')).toBeTruthy();
		expect(within(dialog).getByText('3 / 20')).toBeTruthy();
		expect(within(dialog).getByText('120 g')).toBeTruthy();

		await fireEvent.click(within(dialog).getByRole('button', { name: /Equip/ }));
		await fireEvent.click(within(dialog).getByRole('button', { name: /Sell/ }));

		expect(equip).toHaveBeenCalledWith('item-1');
		expect(sell).toHaveBeenCalledWith('item-1');
	});

	it('offers Unequip, Use, and no Sell, for an equipped consumable', async () => {
		const game = session();
		game.zone = zone;
		game.inspectedItem = item({
			name: '',
			description: null,
			slot: null,
			isEquipped: true,
			effect: 'heal',
			effectPower: 40,
			durability: null,
			rarity: 'Unknown',
			typeKey: null,
			damageMin: null,
			damageMax: null
		});
		const unequip = vi.spyOn(game, 'unequip');
		const use = vi.spyOn(game, 'useItem');
		render(GameOverlays, { session: game });

		// No name falls back to the item ref, which also names the dialog.
		const dialog = screen.getByRole('dialog', { name: 'item-1' });
		expect(within(dialog).getByText('item-1')).toBeTruthy();
		expect(within(dialog).getByText('Restores 40 health')).toBeTruthy();
		expect(within(dialog).queryByText('Durability')).toBeNull();
		expect(within(dialog).queryByText('Damage')).toBeNull();

		const useButton = within(dialog).getByRole('button', { name: /Use/ }) as HTMLButtonElement;
		expect(useButton.disabled).toBe(false);
		await fireEvent.click(useButton);
		await fireEvent.click(within(dialog).getByRole('button', { name: /Unequip/ }));

		expect(use).toHaveBeenCalledWith('item-1');
		expect(unequip).toHaveBeenCalledWith('');
		expect(within(dialog).queryByRole('button', { name: /Sell/ })).toBeNull();
	});

	it('disables Use while the character is dead, even out of combat', () => {
		const game = session();
		game.zone = null;
		game.dead = true;
		game.combat = { active: false, enemy: null, log: [] };
		game.inspectedItem = item({
			slot: null,
			isEquipped: false,
			effect: 'heal',
			effectPower: 40,
			durability: null,
			damageMin: null,
			damageMax: null
		});
		render(GameOverlays, { session: game });

		// Death now ends the fight, so `dead && !combat.active` is the normal post-death state; the
		// dialog's Use must not stay live there or a dead character heals and Respawn refuses them.
		const dialog = screen.getByRole('dialog', { name: 'Ember blade' });
		const useButton = within(dialog).getByRole('button', { name: /Use/ }) as HTMLButtonElement;
		expect(useButton.disabled).toBe(true);
	});

	it('shows neither Use nor Equip for a plain material', () => {
		const game = session();
		game.zone = null;
		game.inspectedItem = item({
			slot: null,
			effect: null,
			damageMin: null,
			damageMax: null,
			requirementsLevel: 1,
			description: null
		});
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Ember blade' });
		expect(within(dialog).queryByRole('button', { name: /Use/ })).toBeNull();
		expect(within(dialog).queryByRole('button', { name: /Equip/ })).toBeNull();
		expect(within(dialog).queryByRole('button', { name: /Sell/ })).toBeNull();
		expect(within(dialog).getByRole('button', { name: 'Close' })).toBeTruthy();
	});

	it('closes the dialog through the Close button', async () => {
		const game = session();
		game.zone = null;
		game.inspectedItem = item();
		const close = vi.spyOn(game, 'closeInspect');
		render(GameOverlays, { session: game });

		await fireEvent.click(
			within(screen.getByRole('dialog', { name: 'Ember blade' })).getByRole('button', {
				name: 'Close'
			})
		);

		expect(close).toHaveBeenCalled();
	});
});

describe('party', () => {
	it('invites the player to create a party when alone', async () => {
		const game = session();
		game.showParty = true;
		const create = vi.spyOn(game, 'createParty');
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Party' });
		expect(within(dialog).getByText('You are adventuring alone.')).toBeTruthy();

		await fireEvent.click(within(dialog).getByRole('button', { name: /Create party/ }));
		expect(create).toHaveBeenCalled();
	});

	it('lists members, marks the leader, and lets the leader remove others', async () => {
		const game = session();
		game.showParty = true;
		game.character = { id: 'char-1' } as never;
		game.party = {
			partyId: 'p1',
			leaderCharacterId: 'char-1',
			lootRule: 'RoundRobin',
			members: [
				{
					characterId: 'char-1',
					name: 'Kaleth',
					level: 7,
					currentHealth: 40,
					maxHealth: 50,
					isLeader: true
				},
				{
					characterId: 'char-2',
					name: 'Brann',
					level: 5,
					currentHealth: 30,
					maxHealth: 40,
					isLeader: false
				}
			]
		};
		const kick = vi.spyOn(game, 'kickPartyMember');
		const leave = vi.spyOn(game, 'leaveParty');
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Party' });
		expect(within(dialog).getByText('Leader')).toBeTruthy();
		expect(within(dialog).getByText('Level 5 · 30 / 40 health')).toBeTruthy();
		expect(within(dialog).getAllByRole('button', { name: 'Remove' })).toHaveLength(1);

		await fireEvent.click(within(dialog).getByRole('button', { name: 'Remove' }));
		expect(kick).toHaveBeenCalledWith('char-2');

		const leaveButton = within(dialog).getByRole('button', { name: /Disband or leave/ });
		await fireEvent.click(leaveButton);
		expect(leave).toHaveBeenCalled();
	});

	it('sends an invite by name, ignoring a blank one', async () => {
		const game = session();
		game.showParty = true;
		game.character = { id: 'char-2' } as never;
		game.party = {
			partyId: 'p1',
			leaderCharacterId: 'char-1',
			lootRule: 'RoundRobin',
			members: [
				{
					characterId: 'char-1',
					name: 'Kaleth',
					level: 7,
					currentHealth: 40,
					maxHealth: 50,
					isLeader: true
				}
			]
		};
		const invite = vi.spyOn(game, 'inviteToParty').mockResolvedValue(undefined);
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Party' });
		// A non-leader sees no Remove control and the plain "Leave party" label.
		expect(within(dialog).queryByRole('button', { name: 'Remove' })).toBeNull();
		expect(within(dialog).getByRole('button', { name: /Leave party/ })).toBeTruthy();

		const inviteButton = within(dialog).getByRole('button', { name: /Invite/ });
		await fireEvent.click(inviteButton);
		expect(invite).not.toHaveBeenCalled();

		await fireEvent.input(within(dialog).getByPlaceholderText('Character name'), {
			target: { value: '  Brann  ' }
		});
		await fireEvent.click(inviteButton);

		expect(invite).toHaveBeenCalledWith('Brann');
	});
});

describe('settings', () => {
	it('reports the connection phase and refreshes both caches', async () => {
		const game = session();
		game.showSettings = true;
		game.serverInfo = { serverVersion: '1.4.0', connectionId: 'conn-1' } as never;
		game.character = { id: 'char-1' } as never;
		const map = vi.spyOn(game, 'requestMapGraph');
		const inventory = vi.spyOn(game, 'openInventory');
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Settings' });
		expect(within(dialog).getByText('1.4.0')).toBeTruthy();
		expect(within(dialog).getByText('conn-1')).toBeTruthy();
		expect(within(dialog).getByText('char-1')).toBeTruthy();

		await fireEvent.click(within(dialog).getByRole('button', { name: /Refresh map/ }));
		await fireEvent.click(within(dialog).getByRole('button', { name: /Refresh inventory/ }));

		expect(map).toHaveBeenCalledWith(null);
		expect(inventory).toHaveBeenCalled();
	});

	it('omits the server and character rows when nothing has loaded yet', () => {
		const game = session();
		game.showSettings = true;
		game.serverInfo = null;
		game.character = null;
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Settings' });
		expect(within(dialog).queryByText('Server')).toBeNull();
		expect(within(dialog).queryByText('Character ID')).toBeNull();
	});
});

describe('toasts', () => {
	it('announces a turned-in quest and dismisses it', async () => {
		const game = session();
		game.questTurnInToast = { slug: 'market-rats', gold: 25, xp: 60 } as never;
		const dismiss = vi.spyOn(game, 'dismissQuestToast');
		render(GameOverlays, { session: game });

		expect(screen.getByText(/Quest complete/)).toBeTruthy();
		expect(screen.getByText(/\+25 gold · \+60 XP/)).toBeTruthy();

		await fireEvent.click(screen.getByRole('button', { name: /Dismiss/ }));
		expect(dismiss).toHaveBeenCalled();
	});

	it('offers accept and decline for a pending party invite', async () => {
		const game = session();
		game.partyInvites = [
			{
				partyId: 'p1',
				partyLeaderName: 'Kaleth',
				invitedByName: 'Kaleth',
				expiresAt: '2026-02-01T00:00:00.000Z'
			}
		] as never;
		const accept = vi.spyOn(game, 'acceptPartyInvite');
		const decline = vi.spyOn(game, 'declinePartyInvite');
		render(GameOverlays, { session: game });

		expect(screen.getByText(/invited you to/)).toBeTruthy();

		await fireEvent.click(screen.getByRole('button', { name: /Accept/ }));
		await fireEvent.click(screen.getByRole('button', { name: /Decline/ }));

		expect(accept).toHaveBeenCalledWith('p1');
		expect(decline).toHaveBeenCalledWith('p1');
	});
});

describe('skills', () => {
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

	it('shows the empty state before anything is practised', () => {
		const game = session();
		game.showSkills = true;
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Skills' });
		expect(within(dialog).getByText('No skills practised yet.')).toBeTruthy();
	});

	it('groups the sheet by category and shows rank and progress', () => {
		const game = session();
		game.showSkills = true;
		game.skills = [
			skill(),
			skill({
				skillId: 'herbalism',
				name: 'Herbalism',
				category: 'profession',
				governingAttribute: 'wisdom',
				rank: 0,
				totalXp: 40,
				xpIntoRank: 40,
				maxRank: 30
			})
		];
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Skills' });
		expect(within(dialog).getByRole('heading', { name: 'Combat' })).toBeTruthy();
		expect(within(dialog).getByRole('heading', { name: 'Profession' })).toBeTruthy();
		expect(within(dialog).getByText('Swordsmanship')).toBeTruthy();
		expect(within(dialog).getByText('Rank 2 / 50')).toBeTruthy();
		expect(within(dialog).getByText('50 / 100 XP to the next rank · 250 total')).toBeTruthy();
		expect(within(dialog).getByText('Rank 0 / 30')).toBeTruthy();
		expect(within(dialog).getByText('40 / 100 XP to the next rank · 40 total')).toBeTruthy();
		expect(within(dialog).getByText('Governed by strength')).toBeTruthy();
		expect(within(dialog).getByText('Governed by wisdom')).toBeTruthy();
	});

	it('names a skill the catalog could not resolve and still shows its XP', () => {
		const game = session();
		game.showSkills = true;
		game.skills = [skill({ resolved: false, name: null, category: null })];
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Skills' });
		expect(within(dialog).getByText('Unknown skill (swordsmanship)')).toBeTruthy();
		expect(within(dialog).getByText('The catalog has no entry for this skill.')).toBeTruthy();
		expect(within(dialog).getByText('250 XP recorded')).toBeTruthy();
	});

	it('announces a rank-up and dismisses it', async () => {
		const game = session();
		game.skillRankUpToast = { name: 'Swordsmanship', rank: 3 };
		render(GameOverlays, { session: game });

		expect(screen.getByText('Now rank 3')).toBeTruthy();

		await fireEvent.click(screen.getByRole('button', { name: /Dismiss/ }));

		expect(game.skillRankUpToast).toBeNull();
	});
});
