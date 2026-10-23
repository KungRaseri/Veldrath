declare global {
	interface AuthUser {
		id: string;
		email: string;
		name: string;
		username?: string | null;
		emailVerified: boolean;
		image?: string | null;
		createdAt: Date;
		updatedAt: Date;
	}

	interface AuthSession {
		id: string;
		userId: string;
		expiresAt: Date;
		token: string;
		createdAt: Date;
		updatedAt: Date;
		ipAddress?: string | null;
		userAgent?: string | null;
	}

	namespace App {
		interface Locals {
			session?: AuthSession;
			user?: AuthUser;
		}
	}
}

export {};
