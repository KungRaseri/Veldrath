import type { NpcDialogueOption, NpcDialoguePayload } from './types';

/**
 * Display rules for a conversation.
 *
 * The server decides everything that matters — which options are available, why one is not, and what taking it
 * changes — so this module never evaluates a requirement. It only turns the payload into the sentences the panel
 * shows, which keeps the wording in one place and testable without a browser.
 */

/** Separator between the consequence fragments on one option. */
const CONSEQUENCE_SEPARATOR = ' · ';

/**
 * What taking an option changes, as one line: `25 gold · 1 × healing-draught`.
 *
 * An option that grants nothing returns an empty string rather than a placeholder: "no effect" is not worth a line,
 * and an empty string lets the panel skip the row entirely.
 */
export function consequenceLine(option: NpcDialogueOption): string {
	return (option.consequences ?? []).filter(Boolean).join(CONSEQUENCE_SEPARATOR);
}

/**
 * Why an option cannot be taken, or null when it can.
 *
 * A disabled option with no server reason is a defect rather than a state — the server always explains — so it says
 * so instead of rendering a silently inert button.
 */
export function blockedLine(option: NpcDialogueOption): string | null {
	if (option.enabled) return null;

	return option.blockedReason?.trim() || 'Not available right now.';
}

/** Whether an option can be clicked: enabled by the server, and no other choice already in flight. */
export function optionClickable(option: NpcDialogueOption, busy: string | null): boolean {
	return option.enabled && busy === null;
}

/**
 * The panel's closing line for a conversation with nothing to choose from.
 *
 * A speaker with lines but no authored options still greets you; saying "nothing more to say" is honest, whereas an
 * empty list looks like a failed load.
 */
export function emptyConversationLine(dialogue: NpcDialoguePayload): string {
	return dialogue.hasConversation ? '' : 'There is nothing more to say just now.';
}

/** The speaker's subtitle: occupation when the archetype has one, blank otherwise. */
export function speakerSubtitle(dialogue: NpcDialoguePayload): string {
	return dialogue.occupation?.trim() ?? '';
}
