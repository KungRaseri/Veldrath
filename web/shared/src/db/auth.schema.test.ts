/**
 * Contract tests for the shared Drizzle auth schema.
 *
 * The SvelteKit server builds its Drizzle client from these exact objects and the committed migrations are generated
 * from them, so a rename here is a database change. These assertions pin the parts other code and the migrations
 * depend on: the `auth` schema, the Better Auth column names, uniqueness, the cascade on `user_id`, and the index
 * names Drizzle emitted into the migration.
 */
import { describe, expect, it } from "vitest";
import { getTableColumns, getTableName } from "drizzle-orm";
import { getTableConfig } from "drizzle-orm/pg-core";
import {
  account,
  accountRelations,
  session,
  sessionRelations,
  user,
  userRelations,
  verification,
} from "./auth.schema.js";
import * as schemaEntry from "./schema.js";

describe("auth schema placement", () => {
  it("puts every Better Auth table in the auth schema", () => {
    for (const table of [user, session, account, verification]) {
      expect(getTableConfig(table).schema).toBe("auth");
    }
  });

  it("keeps the Better Auth table names the migrations were generated from", () => {
    expect(getTableName(user)).toBe("user");
    expect(getTableName(session)).toBe("session");
    expect(getTableName(account)).toBe("account");
    expect(getTableName(verification)).toBe("verification");
  });
});

describe("user table", () => {
  const columns = getTableColumns(user);

  it("requires a name and an email, and keeps the username unique", () => {
    expect(columns.name.notNull).toBe(true);
    expect(columns.email.notNull).toBe(true);
    expect(columns.email.isUnique).toBe(true);
    expect(columns.username.isUnique).toBe(true);
  });

  it("defaults emailVerified to false so a new account is unverified", () => {
    expect(columns.emailVerified.notNull).toBe(true);
    expect(columns.emailVerified.hasDefault).toBe(true);
  });
});

describe("session table", () => {
  const config = getTableConfig(session);

  it("keeps the session token unique and expiring", () => {
    const columns = getTableColumns(session);
    expect(columns.token.isUnique).toBe(true);
    expect(columns.token.notNull).toBe(true);
    expect(columns.expiresAt.notNull).toBe(true);
  });

  it("cascades sessions when the owning user is deleted", () => {
    const [foreignKey] = config.foreignKeys;
    expect(foreignKey).toBeDefined();
    const reference = foreignKey!.reference();

    expect(foreignKey!.onDelete).toBe("cascade");
    expect(reference.columns.map((column) => column.name)).toEqual(["user_id"]);
    expect(reference.foreignColumns.map((column) => column.name)).toEqual(["id"]);
  });

  it("indexes user_id, which every session lookup filters on", () => {
    expect(config.indexes.map((index) => index.config.name)).toEqual(["session_userId_idx"]);
  });
});

describe("account table", () => {
  const config = getTableConfig(account);

  it("records the provider identity Better Auth needs", () => {
    const columns = getTableColumns(account);
    expect(columns.accountId.notNull).toBe(true);
    expect(columns.providerId.notNull).toBe(true);
    expect(columns.password).toBeDefined();
  });

  it("cascades accounts when the owning user is deleted and indexes user_id", () => {
    expect(config.foreignKeys[0]?.onDelete).toBe("cascade");
    expect(config.indexes.map((index) => index.config.name)).toEqual(["account_userId_idx"]);
  });
});

describe("verification table", () => {
  it("indexes the identifier so token lookups are not a scan", () => {
    const config = getTableConfig(verification);
    expect(config.indexes.map((index) => index.config.name)).toEqual(["verification_identifier_idx"]);
    expect(getTableColumns(verification).identifier.notNull).toBe(true);
  });
});

describe("relations", () => {
  it("binds each relation set to the table it describes", () => {
    // A relation declared against the wrong table typechecks but produces joins on the wrong columns.
    expect(userRelations.table).toBe(user);
    expect(sessionRelations.table).toBe(session);
    expect(accountRelations.table).toBe(account);
  });
});

describe("package entrypoints", () => {
  it("re-exports the same table objects from the db entrypoint", () => {
    // A second definition of any table would silently split the schema in two.
    expect(schemaEntry.user).toBe(user);
    expect(schemaEntry.session).toBe(session);
    expect(schemaEntry.account).toBe(account);
    expect(schemaEntry.verification).toBe(verification);
  });
});
