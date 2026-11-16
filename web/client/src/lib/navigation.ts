/**
 * Accepts a `redirectTo` query value only when it is a local path.
 *
 * Without this, `?redirectTo=https://elsewhere.example` would turn the login page into an open redirect,
 * and `//elsewhere.example` would do the same with a protocol-relative URL.
 *
 * @param value The raw query value, or `null` when the parameter is absent.
 * @returns The path when it is local, otherwise `null` so the caller can fall back to its default.
 */
export function localPathFrom(value: string | null): string | null {
	return value && value.startsWith('/') && !value.startsWith('//') ? value : null;
}
