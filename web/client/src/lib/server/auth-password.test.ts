import { pbkdf2Sync } from 'node:crypto';
import { describe, expect, it } from 'vitest';
import { verifyIdentityPassword } from './auth-password';

/** Builds an ASP.NET Core Identity v3 hash for the supplied credentials. */
function buildIdentityHash(
	password: string,
	options: { prf?: number; iterations?: number; salt?: Buffer } = {}
): string {
	const prf = options.prf ?? 2; // 2 = HMAC-SHA512
	const iterations = options.iterations ?? 10_000;
	const salt = options.salt ?? Buffer.from('0123456789abcdef', 'utf8');
	const digest = prf === 0 ? 'sha1' : prf === 1 ? 'sha256' : 'sha512';

	const subkey = pbkdf2Sync(password, salt, iterations, 32, digest);

	const header = Buffer.alloc(13);
	header[0] = 0x01;
	header.writeUInt32BE(prf, 1);
	header.writeUInt32BE(iterations, 5);
	header.writeUInt32BE(salt.length, 9);

	return Buffer.concat([header, salt, subkey]).toString('base64');
}

describe('verifyIdentityPassword', () => {
	it('accepts the matching password', async () => {
		const hash = buildIdentityHash('LegacyP@ssw0rd!23');

		await expect(verifyIdentityPassword(hash, 'LegacyP@ssw0rd!23')).resolves.toBe(true);
	});

	it('rejects a different password', async () => {
		const hash = buildIdentityHash('LegacyP@ssw0rd!23');

		await expect(verifyIdentityPassword(hash, 'WrongP@ssw0rd!23')).resolves.toBe(false);
	});

	it('supports SHA256 and SHA1 PRFs used by older Identity deployments', async () => {
		const sha256Hash = buildIdentityHash('LegacyP@ssw0rd!23', { prf: 1 });
		const sha1Hash = buildIdentityHash('LegacyP@ssw0rd!23', { prf: 0 });

		await expect(verifyIdentityPassword(sha256Hash, 'LegacyP@ssw0rd!23')).resolves.toBe(true);
		await expect(verifyIdentityPassword(sha1Hash, 'LegacyP@ssw0rd!23')).resolves.toBe(true);
	});

	it('rejects Better Auth scrypt hashes and malformed values', async () => {
		await expect(verifyIdentityPassword('abcdef:0123456789', 'anything')).resolves.toBe(false);
		await expect(verifyIdentityPassword('not-base64!!', 'anything')).resolves.toBe(false);
		await expect(verifyIdentityPassword('', 'anything')).resolves.toBe(false);
	});
});
