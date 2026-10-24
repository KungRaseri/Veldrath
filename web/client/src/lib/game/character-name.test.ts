import { describe, expect, it } from 'vitest';
import { validateCharacterName } from './character-name';

describe('validateCharacterName', () => {
	it.each(['Aragorn', 'Mary Jane', 'Bo', 'A'.repeat(20)])('accepts %s', (name) => {
		expect(validateCharacterName(name)).toEqual({ valid: true, error: null });
	});

	it.each([
		['', 'Name is required.'],
		['   ', 'Name is required.'],
		['A', 'Name must be at least 2 characters.'],
		['A'.repeat(21), 'Name must be at most 20 characters.'],
		['Aragorn2', 'Name may only contain letters and spaces.'],
		['Aragorn-Elf', 'Name may only contain letters and spaces.']
	])('rejects %j', (name, error) => {
		expect(validateCharacterName(name)).toEqual({ valid: false, error });
	});

	it('ignores surrounding whitespace', () => {
		expect(validateCharacterName('  Aragorn  ')).toEqual({ valid: true, error: null });
	});
});
