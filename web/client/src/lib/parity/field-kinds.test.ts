import { describe, expect, it } from 'vitest';
import { CONTENT_FIELD_KINDS } from '#lib/foundry/content-editor';
import { readRepoFile } from '#lib/testing/source';

/**
 * Content field kinds are declared twice: `ContentFieldType` on the server decides what the registry may
 * describe, and `ContentFieldKind` on the client decides what the editor can render. Adding a kind to one
 * side and forgetting the other used to be silent — the form rendered a text input for a kind it did not
 * know. This reads the server enum and compares it with the client list, the same way the hub and route
 * parity suites read source files.
 *
 * The server file is read through `readRepoFile`, which resolves it from the repository root. It used to
 * be a local `resolve(process.cwd(), '..', '..')`, which resolved differently depending on whether the
 * suite ran from `web/client` or `web/` — from `web/` it left the repository and the suite died with an
 * ENOENT that looked like a broken harness.
 */

/** The members of `ContentFieldType`, in declaration order. */
function serverFieldTypes(): string[] {
	const source = readRepoFile('Veldrath.Contracts/Content/ContentSchema.cs');

	const block = /public enum ContentFieldType\s*\{([\s\S]*?)\n\}/.exec(source);
	if (!block) throw new Error('ContentFieldType was not found in ContentSchema.cs');

	return (
		block[1]
			.split('\n')
			.map((line) => line.trim())
			// Drop the doc comments, then the trailing comma a member may carry.
			.filter((line) => line !== '' && !line.startsWith('///') && !line.startsWith('//'))
			.map((line) => line.replace(/,$/, ''))
	);
}

describe('content field kind parity', () => {
	it('finds the server enum', () => {
		expect(serverFieldTypes().length).toBeGreaterThan(0);
	});

	it('declares the same kinds as the server, in the same order', () => {
		expect([...CONTENT_FIELD_KINDS]).toEqual(serverFieldTypes());
	});
});
