import { describe, expect, it } from 'vitest';
import {
	blockedLine,
	consequenceLine,
	emptyConversationLine,
	optionClickable,
	speakerSubtitle
} from './dialogue';
import type { NpcDialogueOption, NpcDialoguePayload } from './types';

function option(overrides: Partial<NpcDialogueOption> = {}): NpcDialogueOption {
	return {
		key: 'ask-about-woods',
		label: 'What happened in the wood?',
		enabled: true,
		blockedReason: null,
		consequences: [],
		...overrides
	};
}

function dialogue(overrides: Partial<NpcDialoguePayload> = {}): NpcDialoguePayload {
	return {
		speakerSlug: 'wayfarer-elder',
		displayName: 'Wayfarer Elder',
		occupation: 'townspeople',
		entrySlug: 'elder-greeting',
		prompt: 'The wood remembers.',
		hasConversation: true,
		options: [option()],
		...overrides
	};
}

describe('dialogue display rules', () => {
	it('joins the consequences the server worded', () => {
		expect(consequenceLine(option({ consequences: ['1 × healing-draught', '25 gold'] }))).toBe(
			'1 × healing-draught · 25 gold'
		);
	});

	it('says nothing about an option that grants nothing', () => {
		expect(consequenceLine(option())).toBe('');
	});

	it('survives a payload that omits the consequences entirely', () => {
		// Older or partially-rolled-out payloads have no list at all; the panel must render rather than throw.
		const bare = { key: 'x', label: 'x', enabled: true } as unknown as NpcDialogueOption;

		expect(consequenceLine(bare)).toBe('');
	});

	it('passes the server reason through when an option is blocked', () => {
		expect(blockedLine(option({ enabled: false, blockedReason: 'You would need level 3.' }))).toBe(
			'You would need level 3.'
		);
	});

	it('never renders an inert button without a reason', () => {
		// The server always explains; a disabled option with no reason is a defect, so it is not silently blank.
		expect(blockedLine(option({ enabled: false, blockedReason: '   ' }))).toBe(
			'Not available right now.'
		);
	});

	it('has no blocked line for an available option', () => {
		expect(blockedLine(option())).toBeNull();
	});

	it('refuses a click while another choice is in flight', () => {
		expect(optionClickable(option(), null)).toBe(true);
		expect(optionClickable(option(), 'another-option')).toBe(false);
		expect(optionClickable(option({ enabled: false }), null)).toBe(false);
	});

	it('tells the player a spoken entry has nothing to answer', () => {
		expect(emptyConversationLine(dialogue({ hasConversation: false, options: [] }))).toBe(
			'There is nothing more to say just now.'
		);
		expect(emptyConversationLine(dialogue())).toBe('');
	});

	it('shows the occupation as a subtitle, and nothing when there is none', () => {
		expect(speakerSubtitle(dialogue())).toBe('townspeople');
		expect(speakerSubtitle(dialogue({ occupation: null }))).toBe('');
		expect(speakerSubtitle(dialogue({ occupation: '  ' }))).toBe('');
	});
});
