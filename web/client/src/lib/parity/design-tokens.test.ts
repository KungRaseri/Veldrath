import { describe, expect, it } from 'vitest';
import { readRepoFile } from '#lib/testing/source';

/**
 * Design-token parity.
 *
 * The design system used to be prose that nobody diffed against the CSS, so `--vds-gold*` — the
 * metal the whole identity is built from — ran for months undocumented while the document described
 * a component class vocabulary that did not exist. A reader cannot tell a stale table from a live
 * one, which makes a design document worse than none.
 *
 * Three artefacts describe the same tokens and all three must agree:
 *
 * - `web/client/src/routes/theme.css` — the `:root` block, the only real definition
 * - `docs/design/tokens.json` — the machine-readable export for design tools
 * - `docs/design-system.md` — the prose that designers and developers read
 *
 * The CSS is the source of truth. Add a token there and this suite fails until the export and the
 * document catch up.
 */

const THEME_CSS = 'web/client/src/routes/theme.css';
const TOKENS_JSON = 'docs/design/tokens.json';
const DESIGN_DOC = 'docs/design-system.md';

/** Token values are compared loosely: whitespace collapsed, case folded. */
const normalise = (value: string) => value.replace(/\s+/g, ' ').trim().toLowerCase();

/** The `:root` block, which ends at the first line that closes it. */
function rootBlock(): string {
	const css = readRepoFile(THEME_CSS);
	const start = css.indexOf(':root');

	expect(start, `${THEME_CSS} has no :root block`).toBeGreaterThanOrEqual(0);

	const end = css.indexOf('\n}', start);
	expect(end, `${THEME_CSS} :root block is never closed`).toBeGreaterThan(start);

	return css.slice(start, end);
}

/** Every `--vds-*` custom property declared in `:root`. */
function cssTokens(): Map<string, string> {
	const tokens = new Map<string, string>();

	for (const [, name, value] of rootBlock().matchAll(/--vds-([a-z0-9-]+):\s*([^;]+);/g)) {
		tokens.set(`--vds-${name}`, normalise(value));
	}

	return tokens;
}

/** Every token in the published export, keyed the same way as the CSS. */
function exportTokens(): Map<string, string> {
	const parsed = JSON.parse(readRepoFile(TOKENS_JSON)) as {
		groups: Record<string, Record<string, string>>;
	};
	const tokens = new Map<string, string>();

	for (const entries of Object.values(parsed.groups)) {
		for (const [name, value] of Object.entries(entries)) {
			tokens.set(`--vds-${name}`, normalise(value));
		}
	}

	return tokens;
}

const CSS_TOKENS = cssTokens();
const EXPORT_TOKENS = exportTokens();
const DESIGN_DOC_TEXT = readRepoFile(DESIGN_DOC);

describe('design token parity', () => {
	it('exports every token that theme.css declares', () => {
		const missing = [...CSS_TOKENS.keys()].filter((name) => !EXPORT_TOKENS.has(name)).sort();

		expect(
			missing,
			`${TOKENS_JSON} is missing ${missing.length} token(s). Add them to the matching group.`
		).toEqual([]);
	});

	it('declares nothing in the export that theme.css does not define', () => {
		const extra = [...EXPORT_TOKENS.keys()].filter((name) => !CSS_TOKENS.has(name)).sort();

		expect(
			extra,
			`${TOKENS_JSON} documents ${extra.length} token(s) that do not exist in ${THEME_CSS}. A stale export is how the document drifted last time.`
		).toEqual([]);
	});

	it('agrees on every token value', () => {
		const mismatched = [...CSS_TOKENS.entries()]
			.filter(([name, value]) => EXPORT_TOKENS.get(name) !== value)
			.map(
				([name, value]) =>
					`${name}: theme.css has "${value}", ${TOKENS_JSON} has "${EXPORT_TOKENS.get(name) ?? 'nothing'}"`
			)
			.sort();

		expect(mismatched, 'Token values differ between the CSS and the export.').toEqual([]);
	});

	it('documents every token in the design system', () => {
		const undocumented = [...CSS_TOKENS.keys()]
			.filter((name) => !DESIGN_DOC_TEXT.includes(`\`${name}\``))
			.sort();

		expect(
			undocumented,
			`${DESIGN_DOC} does not mention ${undocumented.length} token(s) in a code span. A token nobody can read about is a token nobody uses correctly.`
		).toEqual([]);
	});
});
