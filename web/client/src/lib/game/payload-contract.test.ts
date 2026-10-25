import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, expect, it } from 'vitest';

/**
 * Hub payload contract check.
 *
 * The wire is the contract: [`GameHub.cs`](../../../../../Veldrath.Server/Hubs/GameHub.cs) sends
 * anonymous objects, and `types.ts` describes them for the browser. Nothing generates one from the
 * other, so a renamed field silently breaks the game client at runtime while TypeScript stays happy.
 *
 * This parses every `SendAsync("<Event>", <payload>)` in the hub, extracts the top-level property
 * names of the anonymous object it sends (including `var payload = new { … }` locals used a few
 * lines earlier), and compares them with the matching `<Event>Payload` interface in `types.ts`.
 *
 * Payloads that are primitives, arrays, or have no TypeScript counterpart are ignored — the client
 * only mirrors what it consumes.
 */

/** Vitest runs with cwd inside the workspace, so walk up until the repo root is recognisable. */
function findRepoRoot(): string {
	let current = process.cwd();

	for (let depth = 0; depth < 6; depth += 1) {
		if (
			existsSync(join(current, 'Veldrath.Server')) &&
			existsSync(join(current, 'web', 'client'))
		) {
			return current;
		}
		current = dirname(current);
	}

	throw new Error(`Could not locate the repository root from ${process.cwd()}.`);
}

const repoRoot = findRepoRoot();
const hubFile = join(repoRoot, 'Veldrath.Server', 'Hubs', 'GameHub.cs');
const typesFile = join(repoRoot, 'web', 'client', 'src', 'lib', 'game', 'types.ts');

/** Removes line and block comments so brace/comma scanning is not confused by prose. */
function stripComments(source: string): string {
	return source.replace(/\/\*[\s\S]*?\*\//g, '').replace(/\/\/[^\n]*/g, '');
}

/** Returns the body between the `{` at `openIndex` and its matching `}`. */
function extractBalancedBlock(source: string, openIndex: number): string | null {
	let depth = 0;

	for (let index = openIndex; index < source.length; index += 1) {
		const character = source[index];
		if (character === '{') depth += 1;
		else if (character === '}') {
			depth -= 1;
			if (depth === 0) return source.slice(openIndex + 1, index);
		}
	}

	return null;
}

/** Property names assigned at the top level of an anonymous-object body. */
function topLevelProperties(body: string): string[] {
	const parts: string[] = [];
	let depth = 0;
	let current = '';

	for (const character of body) {
		if ('{(['.includes(character)) depth += 1;
		else if ('})]'.includes(character)) depth -= 1;

		if (character === ',' && depth === 0) {
			parts.push(current);
			current = '';
			continue;
		}

		current += character;
	}

	if (current.trim()) parts.push(current);

	return parts
		.map((part) => part.split('=')[0].trim())
		.filter(Boolean)
		.map((name) => {
			// Shorthand projections (`result.CurrentHealth`, `e.Id`) take the final identifier.
			const segments = name.split('.');
			return segments[segments.length - 1].replace(/\?$/, '').trim();
		})
		.filter((name) => /^[A-Za-z_]\w*$/.test(name));
}

/** Every event the hub broadcasts with an anonymous-object payload, keyed by event name. */
function readWirePayloads(): Map<string, string[]> {
	const source = stripComments(readFileSync(hubFile, 'utf8'));
	const shapes = new Map<string, string[]>();

	for (const match of source.matchAll(/SendAsync\(\s*"(\w+)"\s*,\s*([\w.]+)/g)) {
		const eventName = match[1];
		const argument = match[2];
		const payloadStart = (match.index ?? 0) + match[0].length - argument.length;
		const lookbehindStart = Math.max(0, (match.index ?? 0) - 8000);
		const lookbehind = source.slice(lookbehindStart, match.index);

		let braceIndex = -1;

		if (argument === 'new') {
			// Inline: SendAsync("Event", new { ... })
			const brace = source.indexOf('{', payloadStart);
			if (brace >= 0 && brace - payloadStart < 40) braceIndex = brace;
		} else if (/^\w+$/.test(argument)) {
			// Local: var payload = new { ... }; … SendAsync("Event", payload)
			const declaration = new RegExp(`\\b${argument}\\s*=\\s*new`).exec(
				lookbehind.split(';').pop() ?? ''
			);
			if (declaration) {
				const statementStart = lookbehind.lastIndexOf(';') + 1;
				const brace = lookbehind.indexOf('{', statementStart + declaration.index);
				if (brace >= 0) braceIndex = lookbehindStart + brace;
			}
		}
		// Anything else (method call, array, string, Task) is not an object payload.

		if (braceIndex < 0) continue;

		const body = extractBalancedBlock(source, braceIndex);
		if (body === null) continue;

		const properties = topLevelProperties(body);
		if (properties.length === 0) continue;

		// One event can be broadcast with different shapes (CombatTurn sends one payload per
		// action); the client must be able to describe every variant, so union them.
		const existing = shapes.get(eventName) ?? [];
		shapes.set(eventName, [...new Set([...existing, ...properties])]);
	}

	return shapes;
}

function readTypescriptInterfaces(): Map<string, string[]> {
	const source = readFileSync(typesFile, 'utf8');
	const interfaces = new Map<string, string[]>();

	for (const match of source.matchAll(/export interface (\w+)\s*\{([\s\S]*?)\n\}/g)) {
		const [, name, body] = match;
		const properties = [...body.matchAll(/^\s*(\w+)\??\s*:/gm)].map(([, property]) => property);
		interfaces.set(name, properties);
	}

	return interfaces;
}

const normalize = (name: string) => name.replace(/_/g, '').toLowerCase();

describe('hub payload contract', () => {
	it('finds the hub and the payload types', () => {
		expect(existsSync(hubFile), `missing ${hubFile}`).toBe(true);
		expect(existsSync(typesFile), `missing ${typesFile}`).toBe(true);
		expect(readWirePayloads().size).toBeGreaterThan(0);
	});

	it('keeps every mirrored payload property-aligned with the wire', () => {
		const interfaces = readTypescriptInterfaces();
		const mismatches: string[] = [];

		for (const [eventName, wireProperties] of readWirePayloads()) {
			const mirrored = interfaces.get(`${eventName}Payload`);
			if (!mirrored) continue;

			const wireNames = new Set(wireProperties.map(normalize));
			const typescriptNames = new Set(mirrored.map(normalize));

			const missingInTs = [...wireNames].filter((name) => !typescriptNames.has(name));
			const extraInTs = [...typescriptNames].filter((name) => !wireNames.has(name));

			if (missingInTs.length > 0 || extraInTs.length > 0) {
				mismatches.push(
					`${eventName}Payload: missing in types.ts [${missingInTs.join(', ')}]` +
						`; not on the wire [${extraInTs.join(', ')}]`
				);
			}
		}

		expect(
			mismatches,
			'Update web/client/src/lib/game/types.ts to match the payloads GameHub.cs broadcasts.'
		).toEqual([]);
	});

	it('mirrors the payloads the game shell consumes', () => {
		const interfaces = readTypescriptInterfaces();
		const expected = [
			'CharacterSelectedPayload',
			'ZoneEnteredPayload',
			'MapGraphReceivedPayload',
			'NodeTraveledPayload',
			'InventoryLoadedPayload',
			'CombatStartedPayload',
			'CombatTurnPayload',
			'LocationEnteredPayload',
			'AreaSearchedPayload',
			'CharacterRestedPayload',
			'AbilityUsedPayload',
			'ShopCatalogPayload'
		];

		expect(expected.filter((name) => !interfaces.has(name))).toEqual([]);
	});
});
