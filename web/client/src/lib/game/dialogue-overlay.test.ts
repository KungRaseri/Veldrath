import { fireEvent, render, screen, within } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';
import DialogueOverlay from './DialogueOverlay.svelte';
import { GameSession } from './session.svelte';
import type { NpcDialogueOption, NpcDialoguePayload } from './types';

vi.mock('#lib/hub', () => ({ createGameHub: () => null }));

function option(overrides: Partial<NpcDialogueOption> = {}): NpcDialogueOption {
	return {
		key: 'ask-about-woods',
		label: 'What happened in the wood?',
		enabled: true,
		blockedReason: null,
		consequences: ['25 gold'],
		...overrides
	};
}

function open(overrides: Partial<NpcDialoguePayload> = {}): GameSession {
	const session = new GameSession();
	session.dialogue = {
		speakerSlug: 'wayfarer-elder',
		displayName: 'Wayfarer Elder',
		occupation: 'townspeople',
		entrySlug: 'elder-greeting',
		prompt: 'The wood remembers.',
		hasConversation: true,
		options: [option()],
		...overrides
	};
	return session;
}

describe('dialogue overlay', () => {
	it('names the speaker, their trade and what they say', () => {
		render(DialogueOverlay, { session: open() });

		// The dialog's accessible name is its title — the speaker — which is what a screen reader announces.
		const dialog = screen.getByRole('dialog', { name: 'Wayfarer Elder' });
		expect(within(dialog).getByText('Wayfarer Elder')).toBeTruthy();
		expect(within(dialog).getByText('townspeople')).toBeTruthy();
		expect(within(dialog).getByText('“The wood remembers.”')).toBeTruthy();
	});

	it('shows each choice with what it will change before it is committed', () => {
		render(DialogueOverlay, { session: open() });

		const dialog = screen.getByRole('dialog', { name: 'Wayfarer Elder' });
		expect(within(dialog).getByText('What happened in the wood?')).toBeTruthy();
		expect(within(dialog).getByText('25 gold')).toBeTruthy();
	});

	it('disables a blocked choice and shows the server reason instead of an inert button', () => {
		const session = open({
			options: [option({ enabled: false, blockedReason: 'You would need level 3.' })]
		});

		render(DialogueOverlay, { session });

		const button = screen.getByRole('button', { name: 'Say What happened in the wood?' });
		expect((button as HTMLButtonElement).disabled).toBe(true);
		expect(screen.getByText('You would need level 3.')).toBeTruthy();
	});

	it('says there is nothing to answer when the entry is only spoken', () => {
		render(DialogueOverlay, { session: open({ hasConversation: false, options: [] }) });

		expect(screen.getByText('There is nothing more to say just now.')).toBeTruthy();
	});

	it('takes the choice the player picks', async () => {
		const session = open();
		const choose = vi.spyOn(session, 'chooseNpcOption').mockResolvedValue(undefined);

		render(DialogueOverlay, { session });
		await fireEvent.click(screen.getByRole('button', { name: 'Say What happened in the wood?' }));

		expect(choose).toHaveBeenCalledWith('ask-about-woods');
	});

	it('closes the conversation when dismissed', async () => {
		const session = open();

		render(DialogueOverlay, { session });
		await fireEvent.click(screen.getByRole('button', { name: 'Close conversation' }));

		expect(session.dialogue).toBeNull();
	});

	it('renders nothing while no conversation is open', () => {
		const session = new GameSession();

		render(DialogueOverlay, { session });

		expect(screen.queryByRole('dialog')).toBeNull();
	});
});
