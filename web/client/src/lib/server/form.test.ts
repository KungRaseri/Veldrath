import { describe, expect, it } from 'vitest';
import { boolField, listField, numberField, stringField } from '#lib/server/form';

/**
 * Form helpers are tiny, but every editorial and admin action reads its input through them; a
 * silent `Number('')` or `getAll` mistake changes what staff submit.
 */
describe('form field readers', () => {
	it('trims strings and falls back when the field is missing', () => {
		const form = new FormData();
		form.set('title', '  The Emberfall  ');

		expect(stringField(form, 'title')).toBe('The Emberfall');
		expect(stringField(form, 'missing')).toBe('');
		expect(stringField(form, 'missing', 'fallback')).toBe('fallback');
	});

	it('parses numbers, treating empty and non-numeric values as null', () => {
		const form = new FormData();
		form.set('page', '3');
		form.set('empty', '');
		form.set('text', 'many');

		expect(numberField(form, 'page')).toBe(3);
		expect(numberField(form, 'empty')).toBeNull();
		expect(numberField(form, 'text')).toBeNull();
		expect(numberField(form, 'absent')).toBeNull();
	});

	it('accepts the usual checkbox encodings only', () => {
		const form = new FormData();
		form.set('on', 'on');
		form.set('true', 'true');
		form.set('one', '1');
		form.set('off', 'off');

		expect(boolField(form, 'on')).toBe(true);
		expect(boolField(form, 'true')).toBe(true);
		expect(boolField(form, 'one')).toBe(true);
		expect(boolField(form, 'off')).toBe(false);
		expect(boolField(form, 'absent')).toBe(false);
	});

	it('collects repeated values and drops blanks', () => {
		const form = new FormData();
		form.append('tags', 'lore');
		form.append('tags', '  ');
		form.append('tags', ' history ');

		expect(listField(form, 'tags')).toEqual(['lore', 'history']);
		expect(listField(form, 'absent')).toEqual([]);
	});
});
