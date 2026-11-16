/**
 * Pure helpers behind the Foundry content editor.
 *
 * The editor renders a form from the server's `ContentTypeSchema` and writes back a JSON payload addressed by
 * dot-paths (`stats.cooldown`). Everything here is data-in/data-out so it can be unit-tested without a browser.
 */

/** Field types the server's `ContentFieldType` enum can carry. */
export type ContentFieldKind =
	'Text' | 'Slug' | 'LongText' | 'Integer' | 'Decimal' | 'Boolean' | 'EnumString';

/** One editable field from the content schema. */
export interface ContentField {
	name: string;
	label: string;
	fieldType: ContentFieldKind;
	required: boolean;
	default: string | null;
	min: number | null;
	max: number | null;
	enumValues: string[] | null;
	hint: string | null;
}

/** A named group of fields (`Identity`, `Stats`, `Traits`, …). */
export interface ContentFieldGroup {
	label: string;
	fields: ContentField[];
}

/** The full schema for one content type. */
export interface ContentTypeSchema {
	contentType: string;
	displayLabel: string;
	description: string;
	groups: ContentFieldGroup[];
}

/** One validation issue returned by the write path. */
export interface ContentIssue {
	rule: string;
	message: string;
	severity: 'Warning' | 'Error';
	field: string | null;
	identifier: string | null;
}

/** Every field of every group, in schema order. */
export function allFields(schema: ContentTypeSchema | null): ContentField[] {
	if (!schema) return [];
	return schema.groups.flatMap((group) => group.fields);
}

/** Reads a dot-path out of a payload, returning `undefined` when any segment is missing. */
export function readPath(payload: Record<string, unknown>, path: string): unknown {
	return path.split('.').reduce<unknown>((current, segment) => {
		if (current === null || typeof current !== 'object') return undefined;
		return (current as Record<string, unknown>)[segment];
	}, payload);
}

/** Returns a copy of `payload` with the dot-path set to `value`, creating intermediate objects. */
export function writePath(
	payload: Record<string, unknown>,
	path: string,
	value: unknown
): Record<string, unknown> {
	const [head, ...rest] = path.split('.');
	const clone: Record<string, unknown> = { ...payload };

	if (rest.length === 0) {
		clone[head] = value;
		return clone;
	}

	const child = clone[head];
	const childObject =
		child !== null && typeof child === 'object' ? (child as Record<string, unknown>) : {};
	clone[head] = writePath(childObject, rest.join('.'), value);
	return clone;
}

/** Converts a form input's string value to the shape the payload expects. */
export function coerceFieldValue(field: ContentField, raw: string | boolean): unknown {
	if (field.fieldType === 'Boolean') {
		return typeof raw === 'boolean' ? raw : raw === 'true';
	}

	const text = String(raw).trim();

	if (field.fieldType === 'Integer' || field.fieldType === 'Decimal') {
		if (text === '') return null;
		const parsed = Number(text);
		return Number.isFinite(parsed) ? parsed : null;
	}

	if (text === '') return field.fieldType === 'Text' || field.fieldType === 'LongText' ? '' : null;
	return text;
}

/** Renders a payload value as the string a form input binds to. */
export function toInputValue(value: unknown): string {
	if (value === null || value === undefined) return '';
	if (typeof value === 'boolean') return value ? 'true' : 'false';
	return String(value);
}

/**
 * Groups validation issues by the field they point at.
 *
 * Issues without a field (or whose field is not a form field, e.g. `objectives[0]`) go under the empty-string key so
 * the page can still show them instead of dropping them.
 */
export function issuesByField(issues: ContentIssue[]): Map<string, ContentIssue[]> {
	const grouped = new Map<string, ContentIssue[]>();

	for (const issue of issues) {
		const key = issue.field ?? '';
		const existing = grouped.get(key) ?? [];
		existing.push(issue);
		grouped.set(key, existing);
	}

	return grouped;
}

/** `true` when any issue blocks the save. */
export function hasBlockingIssues(issues: ContentIssue[]): boolean {
	return issues.some((issue) => issue.severity === 'Error');
}

/**
 * Builds the payload to send to the write path: the loaded entity plus every field the form rendered.
 *
 * Fields the author left at their loaded value are included unchanged, so a save never silently drops a value the
 * form did not show.
 */
export function buildPayload(
	original: Record<string, unknown>,
	schema: ContentTypeSchema | null,
	values: Record<string, string | boolean>
): Record<string, unknown> {
	let payload = { ...original };

	for (const field of allFields(schema)) {
		if (!(field.name in values)) continue;
		payload = writePath(payload, field.name, coerceFieldValue(field, values[field.name]));
	}

	return payload;
}

/** Seeds the form state from a payload, falling back to the schema default for empty fields. */
export function initialValues(
	payload: Record<string, unknown>,
	schema: ContentTypeSchema | null
): Record<string, string | boolean> {
	const values: Record<string, string | boolean> = {};

	for (const field of allFields(schema)) {
		const current = readPath(payload, field.name);
		if (current === undefined || current === null) {
			values[field.name] = field.default ?? '';
			continue;
		}

		values[field.name] = field.fieldType === 'Boolean' ? current === true : toInputValue(current);
	}

	return values;
}

/**
 * Lowercase, hyphenated slug derived from a display name.
 *
 * Accents are folded rather than dropped, so `Café Noir` becomes `cafe-noir` instead of `caf-noir`. The
 * server validates the slug with the same shape, which is why the editor derives it instead of letting the
 * author discover the rule from a rejection.
 *
 * @param text The display name.
 * @returns The slug, or `''` when nothing alphanumeric survives.
 */
export function slugify(text: string): string {
	return text
		.normalize('NFKD')
		.replace(/[\u0300-\u036f]/g, '')
		.toLowerCase()
		.replace(/[^a-z0-9]+/g, '-')
		.replace(/^-+|-+$/g, '');
}

/**
 * A URL-safe anchor id for a group heading, so the form can link to its own sections.
 *
 * @param label The group label.
 * @returns The anchor, never empty.
 */
export function anchorId(label: string): string {
	return slugify(label) || 'section';
}

/**
 * Whether the form holds edits that would be lost.
 *
 * @param seed The values the form was seeded with.
 * @param values The values it holds now.
 * @returns `true` when any field differs, including one the seed did not have.
 */
export function isDirty(
	seed: Record<string, string | boolean>,
	values: Record<string, string | boolean>
): boolean {
	const keys = new Set([...Object.keys(seed), ...Object.keys(values)]);

	for (const key of keys) {
		if (seed[key] !== values[key]) return true;
	}

	return false;
}

/**
 * The required fields the form left blank.
 *
 * A boolean is never blank — unchecked means `false`, which is a value — and a number field left empty is
 * blank rather than `0`, because `coerceFieldValue` turns it into `null`.
 *
 * @param schema The content type schema.
 * @param values The form values.
 * @returns The field names, in schema order.
 */
export function missingRequired(
	schema: ContentTypeSchema | null,
	values: Record<string, string | boolean>
): string[] {
	return allFields(schema)
		.filter((field) => field.required && field.fieldType !== 'Boolean')
		.filter((field) => {
			const value = values[field.name];
			return value === undefined || String(value).trim() === '';
		})
		.map((field) => field.name);
}

/** Hrefs a preview link may use. Anything else stays literal text. */
const SAFE_HREF = /^(https?:\/\/|\/|#)/i;

/** One run of text, with at most one emphasis and an optional link target. */
export interface MarkdownSpan {
	/** The literal text, already the thing to render — never markup. */
	text: string;
	/** Set when the run came from `` `code` ``. */
	code?: boolean;
	/** Set when the run came from `**strong**`. */
	strong?: boolean;
	/** Set when the run came from `_emphasis_`. */
	em?: boolean;
	/** Set when the run came from `[label](href)` and the href is safe. */
	href?: string;
}

/** One blank-line-separated block. */
export type MarkdownBlock =
	| { kind: 'heading'; level: number; spans: MarkdownSpan[] }
	| { kind: 'list'; items: MarkdownSpan[][] }
	| { kind: 'paragraph'; lines: MarkdownSpan[][] };

/** The inline subset, scanned left to right so each construct becomes one span. */
const INLINE = /`([^`]+)`|\*\*([^*]+)\*\*|\[([^\]]+)\]\(([^)\s]+)\)|_([^_]+)_/g;

/** Splits one line into spans. Nested emphasis is not supported and simply reads as text. */
function parseInline(text: string): MarkdownSpan[] {
	const spans: MarkdownSpan[] = [];
	let cursor = 0;

	INLINE.lastIndex = 0;

	for (let match = INLINE.exec(text); match !== null; match = INLINE.exec(text)) {
		if (match.index > cursor) {
			spans.push({ text: text.slice(cursor, match.index) });
		}

		if (match[1] !== undefined) {
			spans.push({ text: match[1], code: true });
		} else if (match[2] !== undefined) {
			spans.push({ text: match[2], strong: true });
		} else if (match[3] !== undefined) {
			// An unsafe href keeps the whole construct as text rather than rendering a link nobody should click.
			spans.push(
				SAFE_HREF.test(match[4]) ? { text: match[3], href: match[4] } : { text: match[0] }
			);
		} else if (match[5] !== undefined) {
			spans.push({ text: match[5], em: true });
		}

		cursor = match.index + match[0].length;
	}

	if (cursor < text.length) {
		spans.push({ text: text.slice(cursor) });
	}

	return spans;
}

/**
 * Parses the `LongText` preview.
 *
 * Deliberately a small subset — headings, lists, paragraphs, bold, italic, inline code and links — rather
 * than a markdown library, and deliberately a *structure* rather than an HTML string: the caller renders
 * spans with Svelte markup, so author input is escaped by the template compiler and there is no `{@html}`
 * on a path an author controls. Links carry an `href` only when it is `http(s)`, a path or an anchor.
 *
 * @param markdown The field's value.
 * @returns The blocks to render, or an empty list for blank input.
 */
export function parseMarkdown(markdown: string): MarkdownBlock[] {
	const source = markdown.trim();
	if (source === '') return [];

	return source.split(/\n{2,}/).flatMap((chunk): MarkdownBlock[] => {
		const lines = chunk
			.split('\n')
			.map((line) => line.trim())
			.filter(Boolean);

		if (lines.length === 0) return [];

		const heading = /^(#{1,6})\s+(.*)$/.exec(lines[0]);
		if (heading && lines.length === 1) {
			// The preview sits inside an `<h2>` section heading, so the source's `#` maps to `h3` at the smallest.
			return [
				{
					kind: 'heading',
					level: Math.min(heading[1].length + 2, 6),
					spans: parseInline(heading[2])
				}
			];
		}

		if (lines.every((line) => line.startsWith('- ') || line.startsWith('* '))) {
			return [{ kind: 'list', items: lines.map((line) => parseInline(line.slice(2))) }];
		}

		return [{ kind: 'paragraph', lines: lines.map(parseInline) }];
	});
}
