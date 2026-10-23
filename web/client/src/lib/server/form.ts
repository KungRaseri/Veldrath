/** Reads a trimmed string field from a submitted form. */
export function stringField(form: FormData, key: string, fallback = ''): string {
	const value = form.get(key);
	return typeof value === 'string' ? value.trim() : fallback;
}

/** Reads a numeric field from a submitted form, or `null` when empty/invalid. */
export function numberField(form: FormData, key: string): number | null {
	const value = stringField(form, key);
	if (!value) return null;
	const parsed = Number(value);
	return Number.isFinite(parsed) ? parsed : null;
}

/** Reads a checkbox/boolean field from a submitted form. */
export function boolField(form: FormData, key: string): boolean {
	const value = form.get(key);
	return value === 'on' || value === 'true' || value === '1';
}

/** Reads a repeated multi-value field from a submitted form. */
export function listField(form: FormData, key: string): string[] {
	return form
		.getAll(key)
		.filter((value): value is string => typeof value === 'string' && value.trim().length > 0)
		.map((value) => value.trim());
}
