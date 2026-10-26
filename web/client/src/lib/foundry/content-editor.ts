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
