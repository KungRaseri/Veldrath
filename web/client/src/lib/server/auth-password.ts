import { pbkdf2 as pbkdf2Callback, timingSafeEqual } from 'node:crypto';
import { promisify } from 'node:util';

const pbkdf2 = promisify(pbkdf2Callback);

const IDENTITY_V3_MARKER = 0x01;
const HEADER_LENGTH = 13;

const PrfAlgorithms: Record<number, string> = {
	0: 'sha1',
	1: 'sha256',
	2: 'sha512'
};

/**
 * Verifies an ASP.NET Core Identity password hash (v3 PBKDF2 format).
 *
 * Better Auth normally stores `${saltHex}:${keyHex}` scrypt hashes. Accounts migrated from
 * the legacy `AspNetUsers` table keep their Identity hash so existing players can sign in
 * without a forced password reset. New passwords are written in Better Auth's scrypt format.
 *
 * Identity v3 layout: `0x01 | prf (uint32 BE) | iterations (uint32 BE) | saltLength (uint32 BE) | salt | subkey`.
 */
export async function verifyIdentityPassword(hash: string, password: string): Promise<boolean> {
	if (!hash || !password) {
		return false;
	}

	let decoded: Buffer;
	try {
		decoded = Buffer.from(hash, 'base64');
	} catch {
		return false;
	}

	if (decoded.length <= HEADER_LENGTH || decoded[0] !== IDENTITY_V3_MARKER) {
		return false;
	}

	const prf = decoded.readUInt32BE(1);
	const iterations = decoded.readUInt32BE(5);
	const saltLength = decoded.readUInt32BE(9);

	if (iterations <= 0 || saltLength <= 0 || decoded.length <= HEADER_LENGTH + saltLength) {
		return false;
	}

	const algorithm = PrfAlgorithms[prf];
	if (!algorithm) {
		return false;
	}

	const salt = decoded.subarray(HEADER_LENGTH, HEADER_LENGTH + saltLength);
	const expected = decoded.subarray(HEADER_LENGTH + saltLength);

	const actual = (await pbkdf2(password, salt, iterations, expected.length, algorithm)) as Buffer;
	return actual.length === expected.length && timingSafeEqual(actual, expected);
}
