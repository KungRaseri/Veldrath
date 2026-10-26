import { describe, expect, it } from 'vitest';
import {
	allFields,
	buildPayload,
	coerceFieldValue,
	hasBlockingIssues,
	initialValues,
	issuesByField,
	readPath,
	toInputValue,
	writePath,
	type ContentField,
	type ContentTypeSchema
} from './content-editor';

function field(overrides: Partial<ContentField> = {}): ContentField {
	return {
		name: 'displayName',
		label: 'Display Name',
		fieldType: 'Text',
		required: true,
		default: null,
		min: null,
		max: null,
		enumValues: null,
		hint: null,
		...overrides
	};
}

function schema(overrides: Partial<ContentTypeSchema> = {}): ContentTypeSchema {
	return {
		contentType: 'item',
		displayLabel: 'Items',
		description: 'Anything a player can hold.',
		groups: [
			{
				label: 'Identity',
				fields: [field(), field({ name: 'slug', label: 'Slug', fieldType: 'Slug' })]
			},
			{
				label: 'Stats',
				fields: [field({ name: 'stats.value', label: 'Value', fieldType: 'Integer' })]
			}
		],
		...overrides
	};
}

describe('allFields', () => {
	it('flattens every group in schema order', () => {
		expect(allFields(schema()).map((f) => f.name)).toEqual(['displayName', 'slug', 'stats.value']);
	});

	it('returns nothing for a missing schema', () => {
		expect(allFields(null)).toEqual([]);
	});
});

describe('readPath / writePath', () => {
	it('reads nested dot-paths', () => {
		expect(readPath({ stats: { value: 12 } }, 'stats.value')).toBe(12);
	});

	it('returns undefined for a missing segment', () => {
		expect(readPath({ stats: {} }, 'stats.value')).toBeUndefined();
		expect(readPath({}, 'stats.value')).toBeUndefined();
	});

	it('writes nested dot-paths without mutating the source', () => {
		const original = { slug: 'x', stats: { value: 1 } };
		const updated = writePath(original, 'stats.value', 5);

		expect(readPath(updated, 'stats.value')).toBe(5);
		expect(readPath(original, 'stats.value')).toBe(1);
	});

	it('creates intermediate objects', () => {
		expect(readPath(writePath({}, 'stats.value', 3), 'stats.value')).toBe(3);
	});
});

describe('coerceFieldValue', () => {
	it('parses integers and blanks them to null', () => {
		expect(coerceFieldValue(field({ fieldType: 'Integer' }), '14')).toBe(14);
		expect(coerceFieldValue(field({ fieldType: 'Integer' }), '')).toBeNull();
		expect(coerceFieldValue(field({ fieldType: 'Integer' }), 'abc')).toBeNull();
	});

	it('parses decimals', () => {
		expect(coerceFieldValue(field({ fieldType: 'Decimal' }), '1.5')).toBe(1.5);
	});

	it('keeps booleans boolean', () => {
		expect(coerceFieldValue(field({ fieldType: 'Boolean' }), true)).toBe(true);
		expect(coerceFieldValue(field({ fieldType: 'Boolean' }), 'false')).toBe(false);
	});

	it('trims text and keeps empty text as an empty string', () => {
		expect(coerceFieldValue(field(), '  Iron Sword  ')).toBe('Iron Sword');
		expect(coerceFieldValue(field(), '   ')).toBe('');
	});

	it('blanks an empty slug to null so the server can reject it', () => {
		expect(coerceFieldValue(field({ fieldType: 'Slug' }), '')).toBeNull();
	});
});

describe('initialValues', () => {
	it('seeds inputs from the payload, including nested values', () => {
		const values = initialValues(
			{ displayName: 'Iron Sword', slug: 'iron-sword', stats: { value: 25 } },
			schema()
		);

		expect(values).toEqual({ displayName: 'Iron Sword', slug: 'iron-sword', 'stats.value': '25' });
	});

	it('falls back to the schema default for a missing value', () => {
		const withDefault = schema({
			groups: [{ label: 'Identity', fields: [field({ name: 'rarityWeight', default: '50' })] }]
		});

		expect(initialValues({}, withDefault)).toEqual({ rarityWeight: '50' });
	});

	it('keeps booleans as booleans for checkboxes', () => {
		const boolSchema = schema({
			groups: [{ label: 'Traits', fields: [field({ name: 'isActive', fieldType: 'Boolean' })] }]
		});

		expect(initialValues({ isActive: false }, boolSchema)).toEqual({ isActive: false });
	});
});

describe('buildPayload', () => {
	it('applies coerced field values onto the loaded entity', () => {
		const payload = buildPayload({ slug: 'iron-sword', stats: { value: 25 } }, schema(), {
			displayName: 'Iron Sword',
			slug: 'iron-sword',
			'stats.value': '30'
		});

		expect(payload.displayName).toBe('Iron Sword');
		expect(readPath(payload, 'stats.value')).toBe(30);
	});

	it('leaves fields the form did not render untouched', () => {
		const payload = buildPayload({ slug: 'iron-sword', typeKey: 'heavy-blades' }, schema(), {
			displayName: 'Iron Sword'
		});

		expect(payload.typeKey).toBe('heavy-blades');
	});
});

describe('issuesByField', () => {
	it('groups issues by field and keeps unfielded ones under the empty key', () => {
		const grouped = issuesByField([
			{ rule: 'slug_format', message: 'bad', severity: 'Error', field: 'slug', identifier: 'x' },
			{
				rule: 'quest_without_objectives',
				message: 'empty',
				severity: 'Error',
				field: null,
				identifier: 'q'
			}
		]);

		expect(grouped.get('slug')?.map((issue) => issue.rule)).toEqual(['slug_format']);
		expect(grouped.get('')?.map((issue) => issue.rule)).toEqual(['quest_without_objectives']);
	});
});

describe('hasBlockingIssues', () => {
	it('is true only for error-severity issues', () => {
		expect(
			hasBlockingIssues([
				{ rule: 'w', message: 'warn', severity: 'Warning', field: null, identifier: null }
			])
		).toBe(false);
		expect(
			hasBlockingIssues([
				{ rule: 'e', message: 'err', severity: 'Error', field: null, identifier: null }
			])
		).toBe(true);
	});
});

describe('toInputValue', () => {
	it('renders null, boolean and number values for inputs', () => {
		expect(toInputValue(null)).toBe('');
		expect(toInputValue(true)).toBe('true');
		expect(toInputValue(7)).toBe('7');
	});
});
