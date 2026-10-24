import { env } from '#lib/server/env';
import { PUBLIC_API_URL } from '$app/env/public';

/** Transactional messages that Better Auth can ask the game server to deliver. */
export type TransactionalEmailKind = 'password-reset' | 'verification';

/** Payload for a transactional email. */
export interface TransactionalEmail {
	/** Recipient address. */
	to: string;
	/** Optional username shown in the greeting. */
	userName?: string | null;
	/** Absolute link the recipient must open. */
	url: string;
}

const REQUEST_TIMEOUT_MS = 10_000;

/**
 * Sends a transactional email through Veldrath.Server's SMTP transport.
 *
 * The .NET server owns the SMTP configuration, so the SvelteKit server forwards the
 * message over the internal bridge instead of running its own mailer. When the bridge is
 * not configured (local development without the game server) the link is written to the
 * server console so the flow can still be exercised.
 */
export async function sendTransactionalEmail(
	kind: TransactionalEmailKind,
	email: TransactionalEmail
): Promise<boolean> {
	const baseUrl = env.INTERNAL_BRIDGE_URL ?? PUBLIC_API_URL;
	const token = env.INTERNAL_BRIDGE_TOKEN;

	if (!baseUrl || !token) {
		console.info(`[email:${kind}] bridge not configured; link for ${email.to}: ${email.url}`);
		return false;
	}

	const body =
		kind === 'password-reset'
			? { to: email.to, userName: email.userName ?? null, resetUrl: email.url }
			: { to: email.to, userName: email.userName ?? null, verifyUrl: email.url };

	try {
		const response = await fetch(`${baseUrl.replace(/\/+$/, '')}/api/internal/email/${kind}`, {
			method: 'POST',
			headers: {
				'content-type': 'application/json',
				'x-internal-bridge-token': token
			},
			body: JSON.stringify(body),
			signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS)
		});

		if (!response.ok) {
			console.error(`[email:${kind}] server rejected the message with HTTP ${response.status}.`);
			return false;
		}

		return true;
	} catch (error) {
		console.error(`[email:${kind}] request failed.`, error);
		return false;
	}
}
