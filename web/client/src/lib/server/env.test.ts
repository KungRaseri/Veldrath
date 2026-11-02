// @vitest-environment node
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterEach, describe, expect, it, vi } from 'vitest';

/**
 * `#lib/server/env` reads `process.env` and layers a local `.env` on top, only for keys that are
 * not already set. That precedence is what keeps container configuration authoritative over a
 * stray `.env` in the image, so the parser is pinned here.
 */

const originalCwd = process.cwd();
const directories: string[] = [];

afterEach(() => {
	process.chdir(originalCwd);
	for (const directory of directories.splice(0))
		rmSync(directory, { recursive: true, force: true });
	delete process.env.VELDRATH_ENV_DOUBLE;
	delete process.env.VELDRATH_ENV_SINGLE;
	delete process.env.VELDRATH_ENV_PRESET;
	delete process.env.VELDRATH_ENV_BARE;
	vi.resetModules();
});

describe('environment loading', () => {
	it('parses .env, strips quotes and comments, and keeps process values', async () => {
		const directory = mkdtempSync(join(tmpdir(), 'veldrath-env-'));
		directories.push(directory);
		writeFileSync(
			join(directory, '.env'),
			[
				'# a comment',
				'',
				'VELDRATH_ENV_DOUBLE="quoted value"',
				"VELDRATH_ENV_SINGLE='single value'",
				'VELDRATH_ENV_PRESET=from-file',
				'VELDRATH_ENV_BARE=bare',
				'IGNORED LINE WITHOUT EQUALS'
			].join('\n')
		);
		process.env.VELDRATH_ENV_PRESET = 'from-process';
		process.chdir(directory);
		vi.resetModules();

		const { env } = await import('#lib/server/env');

		expect(env.VELDRATH_ENV_DOUBLE).toBe('quoted value');
		expect(env.VELDRATH_ENV_SINGLE).toBe('single value');
		// The process wins: a baked-in container value must not be overridden by the file.
		expect(env.VELDRATH_ENV_PRESET).toBe('from-process');
		expect(env.VELDRATH_ENV_BARE).toBe('bare');
	});

	it('does nothing when there is no .env in the working directory', async () => {
		const directory = mkdtempSync(join(tmpdir(), 'veldrath-env-empty-'));
		directories.push(directory);
		process.chdir(directory);
		vi.resetModules();

		const { env } = await import('#lib/server/env');

		expect(env).toBe(process.env);
	});
});
