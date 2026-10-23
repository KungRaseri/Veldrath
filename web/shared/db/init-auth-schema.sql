-- Local development bootstrap for the SvelteKit auth database.
--
-- Better Auth tables and Drizzle's migration table live in the `auth` schema.
-- The SvelteKit process connects as a dedicated role that can only see that
-- schema, so it cannot accidentally touch the EF Core tables in `public`.

CREATE SCHEMA IF NOT EXISTS auth;

DO $$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'veldrath_auth') THEN
    CREATE ROLE veldrath_auth LOGIN PASSWORD 'veldrath_auth_dev';
  END IF;
END
$$;

GRANT CONNECT ON DATABASE veldrath TO veldrath_auth;
-- Drizzle runs CREATE SCHEMA IF NOT EXISTS auth before migrating, which
-- requires CREATE on the database even when the schema already exists.
GRANT CREATE ON DATABASE veldrath TO veldrath_auth;
GRANT USAGE, CREATE ON SCHEMA auth TO veldrath_auth;

-- Keep the auth role inside the auth schema. Do NOT set this on the database
-- or on the shared `veldrath` role: EF Core and the Blazor-era tables still
-- live in `public`.
ALTER ROLE veldrath_auth SET search_path = auth;
