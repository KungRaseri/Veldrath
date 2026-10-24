import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

/**
 * Server-side environment access.
 *
 * `$env/dynamic/private` is snapshotted by the SvelteKit 3 + Vite 8 (Rolldown) build and
 * constant-folded away when the value is absent at build time, which breaks runtime
 * configuration in the adapter-node image. Read `process.env` directly instead and load
 * `.env` for local dev/preview when the variable is not already provided.
 */
function loadDotEnv(): void {
	const path = resolve(process.cwd(), '.env');
	if (!existsSync(path)) {
		return;
	}

	for (const rawLine of readFileSync(path, 'utf8').split('\n')) {
		const line = rawLine.trim();
		if (!line || line.startsWith('#') || !line.includes('=')) {
			continue;
		}

		const index = line.indexOf('=');
		const key = line.slice(0, index).trim();
		let value = line.slice(index + 1).trim();
		if (
			(value.startsWith('"') && value.endsWith('"')) ||
			(value.startsWith("'") && value.endsWith("'"))
		) {
			value = value.slice(1, -1);
		}

		if (key && process.env[key] === undefined) {
			process.env[key] = value;
		}
	}
}

loadDotEnv();

/** Runtime server environment (populated from the process and local `.env`). */
export const env: Record<string, string | undefined> = process.env;
