#!/usr/bin/env node
/**
 * Migrates ASP.NET Core Identity accounts into Better Auth.
 *
 * Copies:
 * - `AspNetUsers` → `auth.user` with the same GUID (Better Auth user ID)
 * - `AspNetUsers.PasswordHash` → `auth.account` (`provider_id = 'credential'`)
 * - `AspNetUserLogins` → `auth.account` (discord / google / microsoft)
 *
 * Password hashes are copied verbatim. Better Auth verifies them through the legacy
 * PBKDF2 verifier in `web/client/src/lib/server/auth-password.ts`, and new password
 * changes are written as Better Auth scrypt hashes.
 *
 * The script is idempotent: existing users and account rows are skipped.
 *
 * Usage:
 *   node web/scripts/migrate-identity-accounts.mjs --dry-run
 *   node web/scripts/migrate-identity-accounts.mjs
 *
 * The migration needs a role that can read `public."AspNetUsers"` and write `auth.*`.
 * Set `MIGRATION_DATABASE_URL` (owner/admin role) when `DATABASE_URL` uses the restricted
 * `veldrath_auth` role. Local fallback: `postgres://veldrath:veldrath_dev@localhost:5433/veldrath`.
 */

import { randomUUID } from "node:crypto";
import { existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { loadEnvFile } from "node:process";
import postgres from "postgres";

const dryRun = process.argv.includes("--dry-run");

if (!process.env.DATABASE_URL) {
  const envPath = fileURLToPath(new URL("../client/.env", import.meta.url));
  if (existsSync(envPath)) {
    loadEnvFile(envPath);
  }
}

const databaseUrl = process.env.MIGRATION_DATABASE_URL ?? process.env.DATABASE_URL;
if (!databaseUrl) {
  console.error("MIGRATION_DATABASE_URL or DATABASE_URL is not configured. Set it or create web/client/.env.");
  process.exit(1);
}

const providerMap = {
  Discord: "discord",
  Google: "google",
  Microsoft: "microsoft",
};

class DryRunRollback extends Error {}

function sanitizeUsername(raw, fallbackId) {
  const clean = (raw ?? "").replace(/[^A-Za-z0-9_]/g, "").slice(0, 32);
  if (clean.length >= 3) {
    return clean;
  }
  return `player${fallbackId.replace(/-/g, "").slice(0, 8)}`;
}

const sql = postgres(databaseUrl, { max: 1 });

try {
  await sql.begin(async (tx) => {
    const identityUsers = await tx`
   select "Id" as id,
          "UserName" as username,
          "Email" as email,
          "EmailConfirmed" as email_verified,
          "PasswordHash" as password_hash
   from public."AspNetUsers"
  `;

    const identityLogins = await tx`
   select "LoginProvider" as provider,
          "ProviderKey" as provider_key,
          "UserId" as user_id
   from public."AspNetUserLogins"
  `;

    const existingUsers = await tx`select id, email, username from auth.user`;

    const knownIds = new Set(existingUsers.map((row) => row.id));
    const knownUsernames = new Map(
      existingUsers.filter((row) => row.username).map((row) => [row.username.toLowerCase(), row.id]),
    );
    const knownEmails = new Map(
      existingUsers.filter((row) => row.email).map((row) => [row.email.toLowerCase(), row.id]),
    );

    const stats = {
      dryRun,
      usersInserted: 0,
      usersSkipped: 0,
      credentialsInserted: 0,
      credentialsSkipped: 0,
      loginsInserted: 0,
      loginsSkipped: 0,
    };

    for (const row of identityUsers) {
      const id = row.id;

      if (knownIds.has(id)) {
        stats.usersSkipped++;
      } else {
        let username = sanitizeUsername(row.username, id);
        let suffix = 1;
        while (knownUsernames.has(username.toLowerCase())) {
          username = `${sanitizeUsername(row.username, id).slice(0, 28)}${suffix++}`;
        }

        let email = row.email?.trim() || `${id}@external.veldrath.local`;
        const owner = knownEmails.get(email.toLowerCase());
        if (owner && owner !== id) {
          email = `${id}@external.veldrath.local`;
        }

        await tx`
					insert into auth.user
						(id, name, username, display_username, email, email_verified, image, created_at, updated_at)
					values
						(${id}, ${username}, ${username}, ${username}, ${email}, ${row.email_verified === true}, null, now(), now())
				`;

        knownIds.add(id);
        knownUsernames.set(username.toLowerCase(), id);
        knownEmails.set(email.toLowerCase(), id);
        stats.usersInserted++;
      }

      if (row.password_hash) {
        const [existingCredential] = await tx`
     select id from auth.account
     where provider_id = 'credential' and user_id = ${id}
     limit 1
    `;

        if (existingCredential) {
          // Better Auth 1.7+ matches credential accounts by issuer as well as provider.
          await tx`
      update auth.account set issuer = 'local:credential'
      where id = ${existingCredential.id} and issuer is null
     `;
          stats.credentialsSkipped++;
        } else {
          await tx`
      insert into auth.account
      	(id, account_id, provider_id, issuer, user_id, password, created_at, updated_at)
      values
      	(${randomUUID()}, ${id}, 'credential', 'local:credential', ${id}, ${row.password_hash}, now(), now())
     `;
          stats.credentialsInserted++;
        }
      }
    }

    for (const login of identityLogins) {
      const provider = providerMap[login.provider] ?? login.provider.toLowerCase();
      const issuer = `local:oauth:${provider}`;
      const [existingAccount] = await tx`
    select id from auth.account
    where provider_id = ${provider} and account_id = ${login.provider_key}
    limit 1
   `;

      if (existingAccount) {
        await tx`
    	update auth.account set issuer = ${issuer}
    	where id = ${existingAccount.id} and issuer is null
    `;
        stats.loginsSkipped++;
        continue;
      }

      if (!knownIds.has(login.user_id)) {
        stats.loginsSkipped++;
        continue;
      }

      await tx`
    insert into auth.account
    	(id, account_id, provider_id, issuer, user_id, password, created_at, updated_at)
    values
    	(${randomUUID()}, ${login.provider_key}, ${provider}, ${issuer}, ${login.user_id}, null, now(), now())
   `;
      stats.loginsInserted++;
    }

    console.log(JSON.stringify(stats, null, 2));

    if (dryRun) {
      throw new DryRunRollback("dry-run rollback");
    }
  });
} catch (error) {
  if (error instanceof DryRunRollback) {
    console.log("Dry run complete — transaction rolled back.");
  } else {
    console.error("Identity account migration failed:", error);
    process.exitCode = 1;
  }
} finally {
  await sql.end();
}
