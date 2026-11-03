import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';
import CombatPanel from './CombatPanel.svelte';
import GameHeader from './GameHeader.svelte';
import ItemTooltip from './ItemTooltip.svelte';
import QuestTracker from './QuestTracker.svelte';
import { GameSession } from './session.svelte';
import type { CharacterAttributes, InventoryItem, QuestLogEntry } from './types';

// `#lib/hub` pulls in `$app/env/public`, whose runtime needs a SvelteKit dev global that Vitest
// does not provide. These components never connect, so a null factory is enough.
vi.mock('#lib/hub', () => ({ createGameHub: () => null }));

/**
 * The small game components around the shell: header, combat panel, quest tracker, and the item
 * tooltip.
 *
 * These files are short but branch-dense — almost every line is a conditional template row — so
 * each case below renders the component in a distinct state rather than trying to reach a
 * line. `svelte` template branches are what the `.svelte` branch floor measures.
 */

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

const attributes: CharacterAttributes = {
	strength: 12,
	dexterity: 8,
	constitution: 10,
	intelligence: 9,
	wisdom: 7,
	charisma: 6
};

describe('item tooltip', () => {
	it('pins to the top-left corner without an anchor', () => {
		render(ItemTooltip, { item: item() });

		const tooltip = screen.getByRole('tooltip');
		expect(tooltip.getAttribute('style')).toContain('left: 12px');
		expect(tooltip.getAttribute('style')).toContain('top: 12px');
	});

	it('flips to the left and clamps inside the viewport for a right-edge anchor', () => {
		// `anchor` is also a testing-library mount option, so the prop is passed explicitly through
		// `props` rather than as a top-level key.
		render(ItemTooltip, {
			props: {
				item: item(),
				anchor: { right: window.innerWidth - 4, left: 20, top: window.innerHeight - 20 } as DOMRect
			}
		});

		const style = screen.getByRole('tooltip').getAttribute('style') ?? '';
		// Flipping to `left - WIDTH - MARGIN` lands off-screen, so the clamp wins.
		expect(style).toContain('left: 12px');
		expect(style).toContain(`top: ${Math.max(12, window.innerHeight - 240)}px`);
	});

	it('renders every stat, bonus and requirement row for a rich item', () => {
		render(ItemTooltip, {
			item: item({
				durability: 80,
				effect: 'heal',
				effectPower: 25,
				isEquipped: true,
				quantity: 3,
				stackSize: 20,
				armorClass: 'Heavy',
				armorType: 'plate',
				requirementsLevel: 3,
				requirements: { Strength: 10, Wisdom: 99 },
				bonuses: { Strength: 2, Wisdom: -1 }
			}),
			level: 5,
			attributes
		});

		expect(screen.getByText('A blade of banked fire.')).toBeTruthy();
		expect(screen.getByText('4–9 damage')).toBeTruthy();
		expect(screen.getByText('Heavy · plate')).toBeTruthy();
		expect(screen.getByText('80%')).toBeTruthy();
		expect(screen.getByText('Restores 25 health')).toBeTruthy();
		expect(screen.getByText('Equipped')).toBeTruthy();
		expect(screen.getByText('Strength +2')).toBeTruthy();
		// A negative bonus keeps its own sign instead of gaining a `+`.
		expect(screen.getByText('Wisdom -1')).toBeTruthy();
		expect(screen.getByText('Level 3')).toBeTruthy();
		expect(screen.getByText('Strength 10')).toBeTruthy();
		expect(screen.getByText('Wisdom 99')).toBeTruthy();
		expect(screen.getByText('3 / 20')).toBeTruthy();
		// Level 5 meets the level-3 requirement but not Wisdom 99.
		expect(screen.getByText('Level 3').className).toBe('item-tooltip-met');
		expect(screen.getByText('Wisdom 99').className).toBe('item-tooltip-unmet');
	});

	it('omits every optional block for a bare material and treats a missing attribute as zero', () => {
		render(ItemTooltip, {
			item: item({
				name: '',
				description: null,
				type: '',
				slot: null,
				typeKey: null,
				damageMin: null,
				damageMax: null,
				durability: null,
				rarity: null as never,
				requirementsLevel: 2,
				requirements: { Strength: 5 }
			})
		});

		// Without a name the item ref is the label.
		expect(screen.getByText('item-1')).toBeTruthy();
		expect(screen.queryByText('Damage')).toBeNull();
		expect(screen.queryByText('Durability')).toBeNull();
		expect(screen.queryByText('Effect')).toBeNull();
		expect(screen.queryByText('State')).toBeNull();
		// No attributes prop means every requirement resolves against 0, so it is unmet.
		expect(screen.getByText('Strength 5').className).toBe('item-tooltip-unmet');
		expect(screen.getByText('Level 2').className).toBe('item-tooltip-unmet');
	});
});

describe('game header', () => {
	it('greets a connecting player without a character or location', () => {
		const game = new GameSession();
		game.phase = 'connecting';
		render(GameHeader, { session: game, onLeave: vi.fn() });

		expect(screen.getByRole('heading', { level: 1 })).toBeTruthy();
		expect(screen.getByText(/Entering Veldrath/)).toBeTruthy();
		expect(screen.getByText('Region map')).toBeTruthy();
		expect(screen.getByText('connecting')).toBeTruthy();
	});

	it('shows the character line and the location within the zone', () => {
		const game = new GameSession();
		game.phase = 'connected';
		game.character = { name: 'Kaleth', className: 'Ranger' } as never;
		game.stats = { ...game.stats, level: 7 };
		game.zone = { id: 'emberfall', name: 'Emberfall' } as never;
		game.location = { locationDisplayName: 'Market square' } as never;
		render(GameHeader, { session: game, onLeave: vi.fn() });

		expect(screen.getByText(/Kaleth/)).toBeTruthy();
		expect(screen.getByText(/Ranger · Lv 7/)).toBeTruthy();
		expect(screen.getByText(/Market square/)).toBeTruthy();
		expect(screen.getByText('connected')).toBeTruthy();
	});

	it('falls back to the current map node when there is no location', () => {
		const game = new GameSession();
		game.phase = 'reconnecting';
		game.map = {
			id: 'map-1',
			name: 'Aldenmere',
			mapType: 'Zone',
			nodes: [{ id: 'market', displayName: 'Market' }],
			edges: []
		} as never;
		game.currentNodeId = 'market';
		render(GameHeader, { session: game, onLeave: vi.fn() });

		expect(screen.getByText(/Market/)).toBeTruthy();
		expect(screen.getByText('reconnecting')).toBeTruthy();
	});

	it('leaves the world from the header button', async () => {
		const game = new GameSession();
		game.phase = 'disconnected';
		const onLeave = vi.fn();
		render(GameHeader, { session: game, onLeave });

		await fireEvent.click(screen.getByRole('button', { name: /Characters/ }));
		expect(onLeave).toHaveBeenCalled();
	});
});

describe('combat panel', () => {
	function session(): GameSession {
		const game = new GameSession();
		game.character = { learnedAbilities: ['arcane_bolt', 'heal'] } as never;
		return game;
	}

	it('shows the idle state with the action buttons disabled', () => {
		render(CombatPanel, { session: session() });

		expect(screen.getByText('No combat yet.')).toBeTruthy();
		expect((screen.getByRole('button', { name: /Attack/ }) as HTMLButtonElement).disabled).toBe(
			true
		);
		expect(screen.getByRole('button', { name: /arcane_bolt/ })).toBeTruthy();
	});

	it('renders the enemy, its health bar, the log and enabled actions', async () => {
		const game = session();
		game.combat = {
			active: true,
			enemy: { id: 'rat', name: 'Giant rat', level: 2, currentHealth: 5, maxHealth: 20 },
			log: ['You hit the rat for 4.']
		} as never;
		const attack = vi.spyOn(game, 'attack');
		const defend = vi.spyOn(game, 'defend');
		const flee = vi.spyOn(game, 'flee');
		const ability = vi.spyOn(game, 'useCombatAbility');
		render(CombatPanel, { session: game });

		expect(screen.getByText(/Giant rat/)).toBeTruthy();
		expect(screen.getByText('Level 2')).toBeTruthy();
		expect(screen.getByText('5 / 20')).toBeTruthy();
		expect(screen.getByText('You hit the rat for 4.')).toBeTruthy();

		await fireEvent.click(screen.getByRole('button', { name: /Attack/ }));
		await fireEvent.click(screen.getByRole('button', { name: /Defend/ }));
		await fireEvent.click(screen.getByRole('button', { name: /Flee/ }));
		await fireEvent.click(screen.getByRole('button', { name: /arcane_bolt/ }));

		expect(attack).toHaveBeenCalled();
		expect(defend).toHaveBeenCalled();
		expect(flee).toHaveBeenCalled();
		expect(ability).toHaveBeenCalledWith('arcane_bolt');
	});

	it('handles a character that has learned nothing', () => {
		const game = new GameSession();
		game.character = null;
		render(CombatPanel, { session: game });

		expect(screen.getByText('Combat')).toBeTruthy();
		expect(screen.queryByRole('button', { name: /arcane_bolt/ })).toBeNull();
	});
});

describe('quest tracker', () => {
	function quest(overrides: Partial<QuestLogEntry> = {}): QuestLogEntry {
		return {
			slug: 'market-rats',
			title: 'Rats in the market',
			description: 'Clear the grain stores.',
			status: 'Active',
			goldReward: 25,
			xpReward: 60,
			canTurnIn: false,
			objectives: [{ key: 'kill:rat', current: 1, required: 3, complete: false }],
			...overrides
		};
	}

	it('renders nothing while no quest is active', () => {
		const game = new GameSession();
		game.quests = [quest({ status: 'Completed' })];
		render(QuestTracker, { session: game });

		expect(screen.queryByLabelText('Quest tracker')).toBeNull();
	});

	it('tracks the next unfinished objective', () => {
		const game = new GameSession();
		game.quests = [quest()];
		render(QuestTracker, { session: game });

		expect(screen.getByText('Rats in the market')).toBeTruthy();
		expect(screen.getByText('kill:rat · 1/3')).toBeTruthy();
	});

	it('says a quest is ready to turn in once every objective is complete', () => {
		const game = new GameSession();
		game.quests = [
			quest({ objectives: [{ key: 'kill:rat', current: 3, required: 3, complete: true }] })
		];
		render(QuestTracker, { session: game });

		expect(screen.getByText('Ready to turn in')).toBeTruthy();
		expect(screen.queryByText(/kill:rat/)).toBeNull();
	});
});
