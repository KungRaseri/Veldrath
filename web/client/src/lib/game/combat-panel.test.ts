import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';
import CombatPanel from './CombatPanel.svelte';
import { GameSession } from './session.svelte';

// `#lib/hub` imports `$app/env/public`; every session method guards on `this.hub`, so a null
// factory keeps the panel renderable while the button handlers stay harmless.
vi.mock('#lib/hub', () => ({ createGameHub: () => null }));

/** A session mid-fight against a shared enemy, with one other character in the party. */
function fighting(): GameSession {
	const game = new GameSession();
	game.character = { id: 'char-1', name: 'Thornwarden', learnedAbilities: [] } as never;
	game.stats = { ...game.stats, currentMana: 20, maxMana: 20, level: 5 };
	game.combat = {
		active: true,
		enemy: {
			id: 'wolf',
			name: 'Grey Wolf',
			level: 2,
			currentHealth: 12,
			maxHealth: 30,
			abilities: ['Bite']
		},
		log: ['Combat started: Grey Wolf (level 2).'],
		participants: [
			{
				characterId: 'char-1',
				name: 'Thornwarden',
				currentHealth: 18,
				maxHealth: 30,
				damageDealt: 12,
				contributionPercent: 60
			},
			{
				characterId: 'char-2',
				name: 'Ash',
				currentHealth: 9,
				maxHealth: 20,
				damageDealt: 8,
				contributionPercent: 40
			}
		],
		outcome: null
	};
	return game;
}

describe('combat panel participants', () => {
	it('lists everyone in the fight with their health and share', () => {
		render(CombatPanel, { session: fighting() });

		expect(screen.getByText('Thornwarden')).toBeTruthy();
		expect(screen.getByText('(you)')).toBeTruthy();
		expect(screen.getByText('Ash')).toBeTruthy();
		expect(screen.getByText('18 / 30 HP · 60%')).toBeTruthy();
		expect(screen.getByText('9 / 20 HP · 40%')).toBeTruthy();
	});

	it('names a participant the server could not resolve instead of printing nothing', () => {
		const game = fighting();
		game.combat = {
			...game.combat,
			participants: [
				{
					characterId: 'char-3',
					name: null,
					currentHealth: 4,
					maxHealth: 10,
					damageDealt: 1,
					contributionPercent: 100
				}
			]
		};

		render(CombatPanel, { session: game });

		expect(screen.getByText('Unknown fighter')).toBeTruthy();
	});

	it('shows the enemy abilities it can use and the turn state', () => {
		render(CombatPanel, { session: fighting() });

		expect(screen.getByText('Can use: Bite')).toBeTruthy();
		expect(screen.getByText('Your turn')).toBeTruthy();
	});
});

describe('combat panel outcome', () => {
	it('summarises a victory with the rewards the server sent', () => {
		const game = fighting();
		game.combat = {
			...game.combat,
			active: false,
			enemy: { ...game.combat.enemy!, currentHealth: 0 },
			outcome: {
				kind: 'victory',
				xpEarned: 25,
				goldEarned: 7,
				xpLost: 0,
				goldLost: 0,
				hardcore: false
			}
		};

		render(CombatPanel, { session: game });

		expect(screen.getByText('Victory')).toBeTruthy();
		expect(screen.getByText('+25 XP · +7 gold')).toBeTruthy();
		expect((screen.getByRole('button', { name: /Attack/ }) as HTMLButtonElement).disabled).toBe(
			true
		);
	});

	it('summarises a defeat with the losses and the hardcore note', () => {
		const game = fighting();
		game.combat = {
			...game.combat,
			active: false,
			outcome: {
				kind: 'defeat',
				xpEarned: 0,
				goldEarned: 0,
				xpLost: 40,
				goldLost: 12,
				hardcore: true
			}
		};

		render(CombatPanel, { session: game });

		expect(screen.getByText('Defeated')).toBeTruthy();
		expect(screen.getByText(/-40 XP · -12 gold/)).toBeTruthy();
		expect(screen.getByText(/this character did not survive/)).toBeTruthy();
	});

	it('summarises a flight without inventing rewards', () => {
		const game = fighting();
		game.combat = {
			...game.combat,
			active: false,
			outcome: {
				kind: 'fled',
				xpEarned: 0,
				goldEarned: 0,
				xpLost: 0,
				goldLost: 0,
				hardcore: false
			}
		};

		render(CombatPanel, { session: game });

		expect(screen.getByText('You escaped')).toBeTruthy();
		expect(screen.queryByText(/XP/)).toBeNull();
	});
});

describe('combat panel log', () => {
	it('renders every exchange of the fight rather than the last few', () => {
		const game = fighting();
		game.combat = {
			...game.combat,
			log: Array.from({ length: 40 }, (_, index) => `Exchange ${index + 1}.`)
		};

		render(CombatPanel, { session: game });

		expect(screen.getByText('Exchange 1.')).toBeTruthy();
		expect(screen.getByText('Exchange 40.')).toBeTruthy();
	});

	it('does not offer an attack while no fight is running', async () => {
		const game = fighting();
		game.combat = { active: false, enemy: null, log: [] };
		const attack = vi.spyOn(game, 'attack');

		render(CombatPanel, { session: game });
		await fireEvent.click(screen.getByRole('button', { name: /Attack/ }));

		expect(attack).not.toHaveBeenCalled();
	});
});
