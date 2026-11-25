import { fireEvent, render, screen, within } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import GameOverlays from './GameOverlays.svelte';
import { GameSession } from './session.svelte';
import type {
	AvailableQuest,
	CharacterStats,
	InventoryItem,
	PartyMemberView,
	QuestLogEntry,
	QuestObjective
} from './types';

// `#lib/hub` reaches for `$app/env/public`, which needs the SvelteKit dev global that only exists
// inside a running app. Each session method guards on `this.hub`, so a null factory keeps the
// dialogs renderable while the button handlers stay harmless.
vi.mock('#lib/hub', () => ({ createGameHub: () => null }));

/**
 * Conditional arms of the overlay dialogs.
 *
 * Each case opens exactly one dialog so a query such as `getByRole('button', { name: 'Discard' })`
 * cannot be satisfied by a neighbouring panel, and renders fresh because the component owns the
 * pending-allocation state. Derived label helpers (`damageLabel`, `armorLabel`, `isUsable`) take
 * the `null` path for non-weapons and non-armour, so items are built from a complete shape and
 * then degraded one field at a time.
 */

function inventoryItem(overrides: Partial<InventoryItem> = {}): InventoryItem {
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
		canTurnIn: false,
		objectives: [{ key: 'kill:rat', current: 1, required: 3, complete: false }],
		...overrides
	};
}

beforeEach(() => {
	document.body.innerHTML = '';
});

describe('attribute allocation guards', () => {
	it('ignores a click on a disabled increase or decrease', async () => {
		const game = new GameSession();
		game.showAttributes = true;
		game.stats = { ...game.stats, unspentAttributePoints: 0 };
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Attributes' });
		// Both controls are disabled, but a keyboard or synthetic activation still reaches the
		// handler; neither may invent a pending point.
		await fireEvent.click(within(dialog).getByRole('button', { name: 'Increase Strength' }));
		await fireEvent.click(within(dialog).getByRole('button', { name: 'Decrease Strength' }));

		expect(within(dialog).getByText('0 of 0 unspent points left.')).toBeTruthy();
		expect(within(dialog).queryByText('+1')).toBeNull();
		expect(
			(within(dialog).getByRole('button', { name: 'Spend 0 points' }) as HTMLButtonElement).disabled
		).toBe(true);
	});

	it('shows Dexterity, Wisdom and Charisma read-only and keeps the rest spendable', async () => {
		const game = new GameSession();
		game.showAttributes = true;
		game.stats = { ...game.stats, unspentAttributePoints: 3 };
		game.attributes = {
			strength: 10,
			dexterity: 12,
			constitution: 14,
			intelligence: 16,
			wisdom: 18,
			charisma: 20
		};
		const allocate = vi.spyOn(game, 'allocateAttributePoints').mockResolvedValue(undefined);
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Attributes' });
		// Opening the panel spends nothing, and the bank is untouched until a point is banked.
		expect(within(dialog).getByText('3 of 3 unspent points left.')).toBeTruthy();
		expect(allocate).not.toHaveBeenCalled();

		// The three stats with no regular-play spending rule keep their name, their saved value and
		// an explanation, and offer no control that would bank a point the server would not use.
		for (const [label, value] of [
			['Dexterity', '12'],
			['Wisdom', '18'],
			['Charisma', '20']
		] as const) {
			const row = within(dialog).getByText(label).closest('li') as HTMLElement;
			expect(within(row).getByText(value)).toBeTruthy();
			expect(within(row).getByText('Not available for point spending yet.')).toBeTruthy();
			expect(within(row).queryByRole('button', { name: `Increase ${label}` })).toBeNull();
			expect(within(row).queryByRole('button', { name: `Decrease ${label}` })).toBeNull();
		}

		// Strength, Constitution and Intelligence still consume points where combat reads them.
		for (const label of ['Strength', 'Constitution', 'Intelligence']) {
			expect(within(dialog).getByRole('button', { name: `Increase ${label}` })).toBeTruthy();
		}
	});

	it('sends every pending point with the plural spend label', async () => {
		const game = new GameSession();
		game.showAttributes = true;
		game.stats = { ...game.stats, unspentAttributePoints: 2 };
		const allocate = vi.spyOn(game, 'allocateAttributePoints').mockResolvedValue(undefined);
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Attributes' });
		await fireEvent.click(within(dialog).getByRole('button', { name: 'Increase Strength' }));
		await fireEvent.click(within(dialog).getByRole('button', { name: 'Increase Constitution' }));

		await fireEvent.click(within(dialog).getByRole('button', { name: 'Spend 2 points' }));

		expect(allocate).toHaveBeenCalledWith({ Strength: 1, Constitution: 1 });
		expect(game.showAttributes).toBe(false);
	});

	it('does not call the server when Spend is activated with nothing pending', async () => {
		const game = new GameSession();
		game.showAttributes = true;
		game.stats = { ...game.stats, unspentAttributePoints: 1 };
		const allocate = vi.spyOn(game, 'allocateAttributePoints').mockResolvedValue(undefined);
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Attributes' });
		await fireEvent.click(within(dialog).getByRole('button', { name: 'Spend 0 points' }));

		expect(allocate).not.toHaveBeenCalled();
		expect(game.showAttributes).toBe(true);
	});
});

describe('quest log objective arms', () => {
	it('renders an incomplete objective with its own counter and styling', () => {
		const game = new GameSession();
		game.showQuests = true;
		game.quests = [
			quest({
				objectives: [
					{ key: 'kill:rat', current: 3, required: 3, complete: true },
					{ key: 'gather-herbs', current: 0, required: 2, complete: false }
				]
			})
		];
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Quest log' });
		const pending = within(dialog).getByText('gather herbs');
		expect(pending.className).toContain('text-surface-600-400');
		const met = within(dialog).getByText('kill · rat');
		expect(met.className).toContain('text-success-500');
		expect(within(dialog).getByText('0/2')).toBeTruthy();
		expect(within(dialog).getByText('3/3')).toBeTruthy();
	});
});

describe('item details arms', () => {
	it('renders the armour row for a piece of armour', async () => {
		const game = new GameSession();
		game.showParty = false;
		game.inspectedItem = inventoryItem({
			name: 'Iron plate',
			type: 'Armour',
			typeKey: 'Chestplate',
			slot: 'Chest',
			armorClass: 'Heavy',
			armorType: 'Plate',
			damageMin: null,
			damageMax: null,
			durability: 62,
			requirementsLevel: 3,
			requirements: { Strength: 12 },
			bonuses: { Constitution: 2 }
		});
		const equip = vi.spyOn(game, 'equipItem');
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Iron plate' });
		expect(within(dialog).getByText('Heavy · Plate armour')).toBeTruthy();
		expect(within(dialog).getByText('62%')).toBeTruthy();
		expect(within(dialog).queryByText('Damage')).toBeNull();

		await fireEvent.click(within(dialog).getByRole('button', { name: /Equip/ }));
		expect(equip).toHaveBeenCalledWith('item-1');
	});

	it('falls back to the item ref and empty labels for a skeleton item', () => {
		const game = new GameSession();
		game.inspectedItem = inventoryItem({
			itemRef: 'mystery-relic',
			name: undefined,
			description: undefined,
			rarity: undefined,
			type: undefined,
			typeKey: undefined,
			slot: undefined,
			durability: undefined,
			quantity: undefined,
			requirements: undefined as unknown as Record<string, number>,
			bonuses: undefined as unknown as Record<string, number>,
			damageMin: null,
			damageMax: null
		} as unknown as InventoryItem);
		const { container } = render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'mystery-relic' });
		expect(container.textContent).not.toContain('undefined');
		expect(within(dialog).queryByText('Stack')).toBeNull();
		expect(within(dialog).queryByText('Requires')).toBeNull();
		expect(within(dialog).queryByText('Bonuses')).toBeNull();
		expect(within(dialog).getByText('120 g')).toBeTruthy();
	});

	it('shows the stack row for a bundle', () => {
		const game = new GameSession();
		game.inspectedItem = inventoryItem({ quantity: 2, stackSize: 20, isStackable: true });
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Ember blade' });
		expect(within(dialog).getByText('2 / 20')).toBeTruthy();
		expect(within(dialog).getByText('Stack')).toBeTruthy();
	});

	it('closes the dialog through its own close control', async () => {
		const game = new GameSession();
		game.inspectedItem = inventoryItem();
		const close = vi.spyOn(game, 'closeInspect');
		render(GameOverlays, { session: game });

		await fireEvent.click(screen.getByRole('button', { name: 'Close item details' }));

		expect(close).toHaveBeenCalled();
		expect(game.inspectedItem).toBeNull();
	});
});

describe('shop and abilities arms', () => {
	it('renders the shop header with and without a zone name', async () => {
		const game = new GameSession();
		game.showShop = true;
		game.shopZone = null;
		game.stats = { ...game.stats, gold: 7 };
		game.shopCatalog = [
			{ slug: 'potion', name: 'Healing potion', price: 5 },
			{ slug: 'blade', name: 'Ember blade', rarity: 'Rare', price: 500 }
		];
		const buy = vi.spyOn(game, 'buy');
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Shop' });
		expect(within(dialog).getByText('7 gold available.')).toBeTruthy();
		// A catalog entry that carries no rarity is labelled with the generic word.
		expect(within(dialog).getByText('Item')).toBeTruthy();
		expect(within(dialog).getByText('5 g')).toBeTruthy();
		expect(within(dialog).queryByText(/Shop ·/)).toBeNull();

		await fireEvent.click(within(dialog).getAllByRole('button', { name: /Buy/ })[0]);
		expect(buy).toHaveBeenCalledWith('potion');
	});

	it('names the shop after the zone it opened in', () => {
		const game = new GameSession();
		game.showShop = true;
		game.shopZone = 'Emberfall';
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Shop · Emberfall' });
		expect(within(dialog).getByText('No wares available.')).toBeTruthy();
	});

	it('disables the ability action while fighting and says why', async () => {
		const game = new GameSession();
		game.showAbilities = true;
		game.stats = { ...game.stats, currentMana: 20, maxMana: 20 };
		game.character = {
			learnedAbilities: ['arcane_bolt'],
			learnedPowers: [
				{
					slug: 'arcane_bolt',
					resolved: true,
					displayName: 'Arcane bolt',
					powerType: 'Spell',
					effectType: 'Damage',
					manaCost: 5,
					cooldown: 2,
					tier: 1,
					requiredLevel: 1
				}
			]
		} as never;
		game.combat = { active: true, enemy: null, log: [] };
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Abilities' });
		const button = within(dialog).getByRole('button', {
			name: 'Use Arcane bolt'
		}) as HTMLButtonElement;

		// `fireEvent.click` dispatches past the disabled attribute, so the assertion is on the
		// button state and the visible reason, not on the absence of a callback.
		expect(button.disabled).toBe(true);
		expect(
			within(dialog).getByText('Finish the fight first — use it from the combat panel.')
		).toBeTruthy();
	});
});

describe('available quests and quest descriptions', () => {
	it('omits the description line and lists an offer to accept', async () => {
		const game = new GameSession();
		game.showQuests = true;
		game.quests = [quest({ description: undefined as unknown as string, status: 'Completed' })];
		game.availableQuests = [
			{
				slug: 'survey-the-road',
				title: 'Survey the road',
				description: 'Walk the north road.',
				goldReward: 0,
				xpReward: 0,
				objectives: []
			}
		];
		const accept = vi.spyOn(game, 'acceptQuest');
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Quest log' });
		expect(within(dialog).getByRole('heading', { name: 'Available' })).toBeTruthy();
		expect(within(dialog).getByText('0 g · 0 XP')).toBeTruthy();
		// A finished quest shows no Abandon or Turn in controls.
		expect(within(dialog).queryByRole('button', { name: 'Abandon' })).toBeNull();

		await fireEvent.click(within(dialog).getByRole('button', { name: 'Accept' }));
		expect(accept).toHaveBeenCalledWith('survey-the-road');
	});
});

describe('payloads that omit optional numbers', () => {
	it('drops the reward totals and objective counters the server did not send', () => {
		const game = new GameSession();
		game.showQuests = true;
		game.quests = [
			quest({
				goldReward: undefined as unknown as number,
				xpReward: undefined as unknown as number,
				objectives: [
					{ key: 'kill:rat', complete: false } as unknown as QuestObjective,
					{ key: 'gather-herbs', current: 1, required: 2, complete: true }
				]
			})
		];
		game.availableQuests = [
			{
				slug: 'survey-the-road',
				title: 'Survey the road',
				description: 'Walk the north road.',
				goldReward: undefined,
				xpReward: undefined,
				objectives: []
			} as unknown as AvailableQuest
		];
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Quest log' });
		expect(dialog.textContent).not.toContain('undefined');
		// Both reward lines lose their totals rather than printing the missing value.
		expect(dialog.textContent).toMatch(/Reward:\s*g\s*·\s*XP/);
		// An objective with no counter still renders its row.
		expect(within(dialog).getByText('/')).toBeTruthy();
	});

	it('renders an unpriced ware and an unnumbered toast', () => {
		const game = new GameSession();
		game.showShop = true;
		game.shopCatalog = [{ slug: 'potion', name: 'Healing potion', price: undefined as never }];
		game.questTurnInToast = { slug: 'market-rats', gold: undefined, xp: undefined } as never;
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Shop' });
		expect(dialog.textContent).not.toContain('undefined');
		expect(within(dialog).getAllByText('g').length).toBeGreaterThan(0);
		expect(screen.getByText(/Quest complete/).parentElement?.textContent).toMatch(
			/\+ gold · \+ XP/
		);
	});

	it('counts down from a bank the server did not report', () => {
		const game = new GameSession();
		game.showAttributes = true;
		game.stats = { ...game.stats } as CharacterStats;
		(game.stats as { unspentAttributePoints?: number }).unspentAttributePoints = undefined;
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Attributes' });
		// The missing bank reads as an empty value, not as the word undefined.
		expect(dialog.textContent).not.toContain('undefined');
		expect(dialog.textContent).toMatch(/of\s+unspent points left\./);
	});

	it('shows a stack row and value for an item with partial numbers', () => {
		const game = new GameSession();
		game.inspectedItem = inventoryItem({
			quantity: 2,
			stackSize: undefined as unknown as number,
			price: undefined as unknown as number,
			requirementsLevel: 1
		});
		render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Ember blade' });
		expect(dialog.textContent).not.toContain('undefined');
		expect(within(dialog).getByText('Stack')).toBeTruthy();
		expect(within(dialog).getByText(/^2 \/$/)).toBeTruthy();
	});
});

describe('party member arms', () => {
	it('renders members whose profile fields are missing', () => {
		const game = new GameSession();
		game.showParty = true;
		game.character = { id: 'char-1' } as never;
		game.party = {
			partyId: 'p1',
			leaderCharacterId: 'char-1',
			lootRule: 'RoundRobin',
			members: [
				{
					characterId: 'char-1',
					name: undefined,
					level: undefined,
					currentHealth: undefined,
					maxHealth: undefined,
					isLeader: true
				} as unknown as PartyMemberView
			]
		};
		const { container } = render(GameOverlays, { session: game });

		const dialog = screen.getByRole('dialog', { name: 'Party' });
		expect(within(dialog).getByText('Leader')).toBeTruthy();
		expect(container.textContent).not.toContain('undefined');
	});
});
