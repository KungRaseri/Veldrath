import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, expect, it } from 'vitest';

/**
 * Payload contract drift check (W9 in `plans/blazor-decommission-and-cleanup.md`).
 *
 * The hub payloads exist twice: as C# positional records in
 * `Veldrath.GameClient.Core/Payloads/*.cs` (what the server broadcasts) and as TypeScript
 * interfaces in `web/client/src/lib/game/types.ts` (what the browser reads). Nothing generates one
 * from the other, so a renamed field silently breaks the web client at runtime.
 *
 * For every payload that has a counterpart in both places this asserts the property sets match
 * (C# PascalCase vs TS camelCase). Payloads with no TS counterpart are reported, not failed — the
 * web client only mirrors what it consumes so far.
 */

/** Vitest runs with cwd inside the workspace, so walk up until the repo root is recognisable. */
function findRepoRoot(): string {
	let current = process.cwd();

	for (let depth = 0; depth < 6; depth += 1) {
		if (
			existsSync(join(current, 'Veldrath.GameClient.Core')) &&
			existsSync(join(current, 'web', 'client'))
		) {
			return current;
		}
		current = dirname(current);
	}

	throw new Error(`Could not locate the repository root from ${process.cwd()}.`);
}

const repoRoot = findRepoRoot();
const payloadsDir = join(repoRoot, 'Veldrath.GameClient.Core', 'Payloads');
const typesFile = join(repoRoot, 'web', 'client', 'src', 'lib', 'game', 'types.ts');

interface PayloadShape {
	name: string;
	properties: string[];
}

/** Splits a positional-parameter list on top-level commas (ignores commas inside `<>`). */
function splitParameters(parameters: string): string[] {
	const parts: string[] = [];
	let depth = 0;
	let current = '';

	for (const character of parameters) {
		if (character === '<') depth += 1;
		else if (character === '>') depth -= 1;

		if (character === ',' && depth === 0) {
			parts.push(current);
			current = '';
			continue;
		}

		current += character;
	}

	if (current.trim()) parts.push(current);
	return parts;
}

function readCsharpPayloads(): PayloadShape[] {
	const shapes: PayloadShape[] = [];

	for (const file of readdirSync(payloadsDir)) {
		if (!file.endsWith('.cs')) continue;
		const source = readFileSync(join(payloadsDir, file), 'utf8');

		for (const match of source.matchAll(/record\s+(\w+)\s*\(([\s\S]*?)\)\s*(?::|;)/g)) {
			const [, name, parameterList] = match;
			const properties = splitParameters(parameterList)
				.map((parameter) => parameter.split('=')[0].trim())
				.map((parameter) => parameter.split(/\s+/).at(-1) ?? '')
				.filter(Boolean);

			shapes.push({ name, properties });
		}
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
	it('finds the payload sources', () => {
		expect(existsSync(payloadsDir), `missing ${payloadsDir}`).toBe(true);
		expect(existsSync(typesFile), `missing ${typesFile}`).toBe(true);
		expect(readCsharpPayloads().length).toBeGreaterThan(0);
	});

	it('keeps every mirrored payload property-aligned with its C# record', () => {
		const interfaces = readTypescriptInterfaces();
		const mismatches: string[] = [];

		for (const payload of readCsharpPayloads()) {
			const mirrored = interfaces.get(payload.name);
			if (!mirrored) continue;

			const csharpNames = new Set(payload.properties.map(normalize));
			const typescriptNames = new Set(mirrored.map(normalize));

			const missingInTs = [...csharpNames].filter((name) => !typescriptNames.has(name));
			const extraInTs = [...typescriptNames].filter((name) => !csharpNames.has(name));

			if (missingInTs.length > 0 || extraInTs.length > 0) {
				mismatches.push(
					`${payload.name}: missing in types.ts [${missingInTs.join(', ')}]` +
						`; not in the C# record [${extraInTs.join(', ')}]`
				);
			}
		}

		expect(
			mismatches,
			'Update web/client/src/lib/game/types.ts to match Veldrath.GameClient.Core/Payloads.'
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
