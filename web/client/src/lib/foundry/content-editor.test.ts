import { describe, expect, it } from 'vitest';
import {
	allFields,
	anchorId,
	buildPayload,
	coerceFieldValue,
	diffFields,
	hasBlockingIssues,
	initialValues,
	isDirty,
	issuesByField,
	missingRequired,
	appendTo,
	moveAt,
	pairsToObject,
	parseMarkdown,
	readList,
	readPairs,
	readRows,
	readPath,
	removeAt,
	rowFromItems,
	setRowValue,
	slugify,
	submissionPayload,
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
		// A checkbox with no `value` submits `on`, which means checked. Reading it as false is how a saved
		// entry became inactive and disappeared from the catalog.
		expect(coerceFieldValue(field({ fieldType: 'Boolean' }), 'on')).toBe(true);
		expect(coerceFieldValue(field({ fieldType: 'Boolean' }), 'true')).toBe(true);
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

describe('submissionPayload', () => {
	// The form renders every scalar field and posts them all, so an untouched one arrives as the value the form
	// displayed — blank for a field with no default. Writing those over the stored row is what blanked
	// `stats.cooldown` while the seeded `traits.hasCooldown` stayed behind.
	const stored = {
		displayName: 'Bite',
		slug: 'bite',
		rarityWeight: 60,
		stats: { cooldown: 4, manaCost: 0 },
		traits: { hasCooldown: true }
	};
	const correctionSchema = schema({
		groups: [
			{
				label: 'Identity',
				fields: [field(), field({ name: 'slug', label: 'Slug', fieldType: 'Slug' })]
			},
			{
				label: 'Stats',
				fields: [
					field({
						name: 'stats.cooldown',
						label: 'Cooldown (s)',
						fieldType: 'Decimal',
						required: false
					}),
					field({
						name: 'rarityWeight',
						label: 'Rarity Weight',
						fieldType: 'Integer',
						required: false,
						default: '50'
					})
				]
			}
		]
	});

	it('keeps the stored values the form posted unchanged, so no sibling is blanked', () => {
		const payload = submissionPayload(stored, correctionSchema, {
			displayName: 'Bite Revised',
			slug: 'bite',
			'stats.cooldown': '',
			rarityWeight: '60'
		});

		expect(payload.displayName).toBe('Bite Revised');
		expect(readPath(payload, 'stats.cooldown')).toBe(4);
		// The trait the cooldown depends on keeps a declared cooldown beside it.
		expect(readPath(payload, 'traits.hasCooldown')).toBe(true);
		expect(payload.rarityWeight).toBe(60);
	});

	it('writes the fields the author actually edited', () => {
		const payload = submissionPayload(stored, correctionSchema, {
			displayName: 'Bite Revised',
			'stats.cooldown': '6',
			rarityWeight: '70'
		});

		expect(payload.displayName).toBe('Bite Revised');
		expect(readPath(payload, 'stats.cooldown')).toBe(6);
		expect(payload.rarityWeight).toBe(70);
	});

	it('writes every submitted field when the catalog holds no row yet', () => {
		const payload = submissionPayload({}, correctionSchema, {
			displayName: 'New Bite',
			'stats.cooldown': '3',
			rarityWeight: '50'
		});

		expect(payload).toEqual({ displayName: 'New Bite', stats: { cooldown: 3 }, rarityWeight: 50 });
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

	it('renders nothing when a collection reaches a scalar input', () => {
		expect(toInputValue(['a', 'b'])).toBe('');
	});
});

describe('slugify', () => {
	it('folds accents and hyphenates', () => {
		expect(slugify('Ember Blade')).toBe('ember-blade');
		expect(slugify('  Café  Noir!! ')).toBe('cafe-noir');
		expect(slugify('Aelthir_Prime')).toBe('aelthir-prime');
	});

	it('returns an empty string when nothing alphanumeric survives', () => {
		expect(slugify('***')).toBe('');
	});
});

describe('anchorId', () => {
	it('derives a usable anchor from a group label', () => {
		expect(anchorId('Identity')).toBe('identity');
		expect(anchorId('Stats & Traits')).toBe('stats-traits');
	});

	it('never returns an empty anchor', () => {
		expect(anchorId('!!!')).toBe('section');
	});
});

describe('isDirty', () => {
	it('compares both directions and notices a key the seed did not have', () => {
		expect(isDirty({ a: '1' }, { a: '1' })).toBe(false);
		expect(isDirty({ a: '1' }, { a: '2' })).toBe(true);
		expect(isDirty({ a: '1' }, { a: '1', b: '' })).toBe(true);
	});
});

const requiredField = (name: string, fieldType: string, required = true) => ({
	name,
	label: name,
	fieldType,
	required,
	default: null,
	min: null,
	max: null,
	enumValues: null,
	hint: null
});

const formSchema = {
	contentType: 'item',
	displayLabel: 'Item',
	description: '',
	groups: [
		{
			label: 'Identity',
			fields: [
				requiredField('displayName', 'Text'),
				requiredField('slug', 'Slug'),
				requiredField('isActive', 'Boolean')
			]
		},
		{
			label: 'Stats',
			fields: [requiredField('weight', 'Decimal')]
		}
	]
} as never;

describe('missingRequired', () => {
	it('names the blank required fields and ignores a boolean', () => {
		expect(
			missingRequired(formSchema, {
				displayName: 'Ember blade',
				slug: '  ',
				isActive: false,
				weight: '1.5'
			})
		).toEqual(['slug']);
	});

	it('treats an empty number as blank rather than zero', () => {
		expect(missingRequired(formSchema, { displayName: 'x', slug: 'x', weight: '' })).toEqual([
			'weight'
		]);
		expect(missingRequired(formSchema, { displayName: 'x', slug: 'x', weight: '0' })).toEqual([]);
	});

	it('accepts a null schema', () => {
		expect(missingRequired(null, {})).toEqual([]);
	});
});

describe('parseMarkdown', () => {
	it('parses headings, lists and paragraphs', () => {
		expect(parseMarkdown('# Title')).toEqual([
			{ kind: 'heading', level: 3, spans: [{ text: 'Title' }] }
		]);
		expect(parseMarkdown('- one\n- two')).toEqual([
			{ kind: 'list', items: [[{ text: 'one' }], [{ text: 'two' }]] }
		]);
		expect(parseMarkdown('first\nsecond')).toEqual([
			{ kind: 'paragraph', lines: [[{ text: 'first' }], [{ text: 'second' }]] }
		]);
	});

	it('marks bold, italic and code runs', () => {
		expect(parseMarkdown('**bold** and `code`')).toEqual([
			{
				kind: 'paragraph',
				lines: [[{ text: 'bold', strong: true }, { text: ' and ' }, { text: 'code', code: true }]]
			}
		]);
		expect(parseMarkdown('A _quiet_ word.')).toEqual([
			{
				kind: 'paragraph',
				lines: [[{ text: 'A ' }, { text: 'quiet', em: true }, { text: ' word.' }]]
			}
		]);
	});

	it('leaves author input as text, so nothing can become markup', () => {
		expect(parseMarkdown('<script>alert(1)</script>')).toEqual([
			{ kind: 'paragraph', lines: [[{ text: '<script>alert(1)</script>' }]] }
		]);
	});

	it('links a safe href and leaves an unsafe one as text', () => {
		expect(parseMarkdown('[docs](/lore)')).toEqual([
			{ kind: 'paragraph', lines: [[{ text: 'docs', href: '/lore' }]] }
		]);

		const spans = parseMarkdown('[x](javascript:alert(1))').flatMap((block) =>
			block.kind === 'paragraph'
				? block.lines.flat()
				: block.kind === 'list'
					? block.items.flat()
					: block.spans
		);

		expect(spans.some((span) => span.href !== undefined)).toBe(false);
		expect(spans.map((span) => span.text).join('')).toBe('[x](javascript:alert(1))');
	});

	it('returns no blocks for blank input', () => {
		expect(parseMarkdown('   ')).toEqual([]);
	});
});

describe('collection helpers', () => {
	const payload = {
		tags: ['fire', 'ice'],
		stats: { fire: '10' },
		objectives: [{ type: 'kill', quantity: 5 }, 'nonsense', null]
	};

	it('reads lists and rows defensively', () => {
		expect(readList(payload, 'tags')).toEqual(['fire', 'ice']);
		expect(readList(payload, 'missing')).toEqual([]);
		expect(readList({ tags: 'nope' }, 'tags')).toEqual([]);
		expect(readRows(payload, 'objectives')).toEqual([{ type: 'kill', quantity: 5 }]);
		expect(readRows(payload, 'tags')).toEqual([]);
	});

	it('reads an object as ordered rows and writes it back', () => {
		expect(readPairs(payload, 'stats')).toEqual([{ key: 'fire', value: '10' }]);
		expect(readPairs({ stats: ['no'] }, 'stats')).toEqual([]);
		expect(
			pairsToObject([
				{ key: 'fire', value: 10 },
				{ key: ' ', value: 'dropped' }
			])
		).toEqual({ fire: '10' });
	});

	it('seeds a new row from the descriptors', () => {
		expect(
			rowFromItems([
				requiredField('a', 'Text'),
				{ ...requiredField('b', 'Boolean'), default: 'true' }
			] as never)
		).toEqual({ a: '', b: true });
		expect(rowFromItems(null)).toEqual({});
	});

	it('keeps list edits immutable and ignores out-of-range moves', () => {
		const list = ['a'];
		expect(appendTo(list, 'b')).toEqual(['a', 'b']);
		expect(list).toEqual(['a']);
		expect(removeAt(list, 5)).toEqual(['a']);
		expect(moveAt(list, 0, -1)).toEqual(['a']);
		expect(moveAt(['a', 'b'], 1, -1)).toEqual(['b', 'a']);
	});

	it('replaces one property of one row only', () => {
		const rows = [{ a: 1 }, { a: 2 }];
		expect(setRowValue(rows, 1, 'a', 3)).toEqual([{ a: 1 }, { a: 3 }]);
		expect(rows[1]).toEqual({ a: 2 });
	});
});

describe('collections in the form lifecycle', () => {
	const collectionSchema = {
		contentType: 'quest',
		displayLabel: 'Quests',
		description: '',
		groups: [
			{
				label: 'Identity',
				fields: [
					{ ...requiredField('tags', 'StringList') },
					{
						name: 'objectives',
						label: 'objectives',
						fieldType: 'ObjectList',
						required: false,
						default: null,
						min: null,
						max: null,
						enumValues: null,
						hint: null,
						items: [requiredField('type', 'Text')]
					},
					{
						...requiredField('stats', 'KeyValue'),
						required: false
					}
				]
			}
		]
	} as never;

	const collectionPayload = {
		tags: ['a'],
		objectives: [{ type: 'kill' }],
		stats: { fire: '10' }
	};

	it('seeds collections as editable values', () => {
		expect(initialValues(collectionPayload, collectionSchema)).toEqual({
			tags: ['a'],
			objectives: [{ type: 'kill' }],
			stats: [{ key: 'fire', value: '10' }]
		});
	});

	it('writes them back in payload shape', () => {
		expect(
			buildPayload({}, collectionSchema, {
				tags: ['a', 'b'],
				objectives: [{ type: 'visit' }],
				stats: [
					{ key: 'fire', value: '10' },
					{ key: ' ', value: 'dropped' }
				]
			})
		).toEqual({ tags: ['a', 'b'], objectives: [{ type: 'visit' }], stats: { fire: '10' } });
	});

	it('compares collections by content when deciding the form is dirty', () => {
		expect(isDirty({ tags: ['a'] }, { tags: ['a'] })).toBe(false);
		expect(isDirty({ tags: ['a'] }, { tags: [] })).toBe(true);
	});

	it('counts an empty required collection as missing', () => {
		expect(missingRequired(collectionSchema, { tags: [], objectives: [], stats: [] })).toEqual([
			'tags'
		]);
		expect(missingRequired(collectionSchema, { tags: ['a'] })).toEqual([]);
	});

	it('diffs a proposed payload against the stored one, field by field', () => {
		const diffSchema = {
			contentType: 'item',
			displayLabel: 'Items',
			description: '',
			groups: [
				{
					label: 'Identity',
					fields: [
						requiredField('displayName', 'Text'),
						requiredField('weight', 'Decimal'),
						requiredField('tags', 'StringList'),
						requiredField('isActive', 'Boolean')
					]
				}
			]
		} as never;

		expect(
			diffFields(
				diffSchema,
				{ displayName: 'Blade', weight: 1, tags: ['a'] },
				{ displayName: 'Blade', weight: 1, tags: ['a'] }
			)
		).toEqual([]);

		expect(
			diffFields(
				diffSchema,
				{ displayName: 'Blade', weight: 1, tags: ['a'], isActive: true },
				{ displayName: 'Ember blade', weight: 1, tags: ['a', 'b'], isActive: false }
			)
		).toEqual([
			{
				field: 'displayName',
				label: 'displayName',
				before: 'Blade',
				after: 'Ember blade',
				kind: 'changed'
			},
			{ field: 'tags', label: 'tags', before: '["a"]', after: '["a","b"]', kind: 'changed' },
			{ field: 'isActive', label: 'isActive', before: 'Yes', after: 'No', kind: 'changed' }
		]);

		// No stored entity: every value the proposal carries is new.
		expect(diffFields(diffSchema, null, { displayName: 'Blade' })).toEqual([
			{ field: 'displayName', label: 'displayName', before: '—', after: 'Blade', kind: 'added' }
		]);
	});

	it('treats a key/value field holding a scalar as empty rather than crashing', () => {
		const keyValue = {
			...requiredField('stats', 'KeyValue'),
			required: false
		} as never;

		expect(coerceFieldValue(keyValue, 'not rows')).toEqual({});
	});
});
