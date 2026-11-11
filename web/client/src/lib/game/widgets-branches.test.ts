import { render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';
import CombatPanel from './CombatPanel.svelte';
import GameHeader from './GameHeader.svelte';
import ItemTooltip from './ItemTooltip.svelte';
import QuestTracker from './QuestTracker.svelte';
import { GameSession } from './session.svelte';
import type { CharacterAttributes, InventoryItem, QuestLogEntry } from './types';

// `#lib/hub` pulls in `$app/env/public`, whose runtime needs a SvelteKit dev global that Vitest
// does not provide. None of these components connect, so a null factory is enough.
vi.mock('#lib/hub', () => ({ createGameHub: () => null }));

/**
 * Branch arms that the existing widget suite leaves dark.
 *
 * Almost every missed branch in these templates is a `?? ''` fallback that Svelte emits next to a
 * text interpolation, so each case below drives one interpolated value to `null`/`undefined`
 * (or takes the untaken arm of a real conditional) rather than adding another happy-path render.
 * Assertions stay on rendered text so a passing test also proves the arm actually ran.
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

describe('item tooltip fallbacks', () => {
	it('treats a missing requirement map, bonus map and price as empty', () => {
		render(ItemTooltip, {
			item: item({
				requirements: null as never,
				bonuses: null as never,
				price: null as never
			}),
			attributes
		});

		// No requirement rows and no bonus list, but the footer still prints the gold suffix.
		expect(screen.queryByText('Requires')).toBeNull();
		expect(screen.queryByRole('list')).toBeNull();
		expect(screen.getByRole('tooltip').textContent).toContain(' g');
	});

	it('renders a stack count whose stack size is unknown', () => {
		render(ItemTooltip, { item: item({ quantity: 3, stackSize: null as never }) });

		// `{quantity} / {stackSize}` with a null stack size keeps the separator and drops the size.
		expect(screen.getByText('3 /')).toBeTruthy();
		expect(screen.getByText('3 /').textContent).toBe('3 / ');
	});

	it('hides the stack line when the quantity itself is unknown', () => {
		render(ItemTooltip, { item: item({ quantity: null as never, stackSize: 20 }) });

		// `(quantity ?? 1) > 1` resolves to the single-item default, so no count row is rendered.
		expect(screen.getByRole('tooltip').textContent).not.toContain(' / ');
	});

	it('uses the built-in viewport when the window global is absent', () => {
		vi.stubGlobal('window', undefined);
		try {
			render(ItemTooltip, {
				props: {
					item: item(),
					anchor: { right: 20, left: 5, top: 300 } as DOMRect
				}
			});

			// left = anchor.right + MARGIN, top = anchor.top - 4: neither needs the viewport.
			const tooltip = document.querySelector('.item-tooltip');
			expect(tooltip?.getAttribute('style')).toContain('left: 32px');
			expect(tooltip?.getAttribute('style')).toContain('top: 296px');
		} finally {
			vi.unstubAllGlobals();
		}
	});
});

describe('combat panel fallbacks', () => {
	it('renders an enemy whose name, level and health are unknown', () => {
		const game = new GameSession();
		game.character = { learnedAbilities: [] } as never;
		game.combat = {
			active: true,
			enemy: { id: 'rat', name: null, level: null, currentHealth: null, maxHealth: null },
			log: []
		} as never;

		render(CombatPanel, { session: game });

		expect(screen.getByRole('heading', { name: /Combat/ })).toBeTruthy();
		// `Level {level}` and `{health} / {max}` both fall back to an empty string.
		expect(screen.getByText('Level')).toBeTruthy();
		expect(screen.getByText('/')).toBeTruthy();
	});

	it('names an ability the catalog could not resolve instead of printing its slug as a name', () => {
		const game = new GameSession();
		game.character = { learnedAbilities: [null, 'heal'] } as never;

		render(CombatPanel, { session: game });

		// Attack, Defend, Flee plus one button for the one usable slug; the null entry is dropped.
		expect(screen.getAllByRole('button')).toHaveLength(4);
		expect(screen.getByText('Unknown ability (heal)')).toBeTruthy();
		expect(screen.getByText('The catalog has no entry for this ability.')).toBeTruthy();
		expect(
			(screen.getByRole('button', { name: 'Use Unknown ability (heal)' }) as HTMLButtonElement)
				.disabled
		).toBe(true);
	});
});

describe('quest tracker fallbacks', () => {
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

	it('renders an objective whose key and counters are unknown', () => {
		const game = new GameSession();
		game.quests = [
			quest({
				objectives: [{ key: null, current: null, required: null, complete: false }]
			} as never)
		];

		render(QuestTracker, { session: game });

		expect(screen.getByText('Rats in the market')).toBeTruthy();
		// `{key} / {current}/{required}` with every part missing collapses to the separator only.
		expect(screen.getByText('· /')).toBeTruthy();
	});
});

describe('game header fallbacks', () => {
	it('renders a character line with no class and no level', () => {
		const game = new GameSession();
		game.phase = 'connected';
		game.character = { name: 'Kaleth', className: null } as never;
		game.stats = { ...game.stats, level: null as never };

		render(GameHeader, { session: game, onLeave: vi.fn() });

		expect(screen.getByText(/Kaleth/)).toBeTruthy();
		expect(screen.getByRole('heading', { level: 1 }).textContent).toContain('Lv');
	});

	it('renders a location whose display name is unknown', () => {
		const game = new GameSession();
		game.zone = { id: 'emberfall', name: 'Emberfall' } as never;
		game.location = { locationDisplayName: null } as never;

		render(GameHeader, { session: game, onLeave: vi.fn() });

		expect(screen.getByText(/Emberfall/)).toBeTruthy();
	});

	it('renders a map node whose display name is unknown', () => {
		const game = new GameSession();
		game.map = {
			id: 'map-1',
			name: 'Aldenmere',
			mapType: 'Zone',
			nodes: [{ id: 'market', displayName: null }],
			edges: []
		} as never;
		game.currentNodeId = 'market';

		render(GameHeader, { session: game, onLeave: vi.fn() });

		expect(screen.getByText(/Aldenmere/)).toBeTruthy();
	});

	it('renders a header whose connection phase is unknown', () => {
		const game = new GameSession();
		game.phase = null as never;

		const { container } = render(GameHeader, { session: game, onLeave: vi.fn() });

		expect(screen.getByText('Region map')).toBeTruthy();
		expect(container.querySelector('.status-dot')).toBeTruthy();
	});
});
