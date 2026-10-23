-- Local development bootstrap for the SvelteKit auth database.
--
-- The auth tables and Drizzle's migration table live in the `auth` schema so
-- they cannot collide with the EF Core tables in `public`.

CREATE SCHEMA IF NOT EXISTS auth;

-- Applies to new connections to this database. In production, prefer setting
-- the dedicated role's search_path instead:
--   ALTER ROLE veldrath_auth SET search_path = auth, public;
ALTER DATABASE veldrath SET search_path TO auth, public;
