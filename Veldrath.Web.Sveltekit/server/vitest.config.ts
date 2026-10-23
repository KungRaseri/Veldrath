/**
 * Vitest config for @your-scope/server.
 *
 * 1. `@colyseus/core`'s Stats module dynamically imports `@pm2/io` on
 *    matchmaker setup; `@pm2/io`'s agent calls `process.send()`, which is
 *    illegal inside the Vitest worker pool. We force-inline `@colyseus/core` +
 *    `@pm2/io` so the `@pm2/io` alias below applies (externalized node_modules
 *    bypass Vite's resolver), replacing pm2 with a no-op stub for tests.
 * 2. `@colyseus/ws-transport` must be inlined alongside `@colyseus/core`.
 *    Otherwise Vitest externalizes the transport (native Node), which resolves
 *    `@colyseus/core` through its `require` condition (build/index.cjs) while
 *    our inlined copy uses the `import` condition (build/index.mjs). That dual
 *    package hazard yields TWO `matchMaker` singletons — rooms created by our
 *    server live in one copy, but the transport's `getLocalRoomById` reads the
 *    other, so every join fails with "seat reservation expired".
 *
 * These inlining + alias rules are mandatory for any Colyseus server under
 * Vitest.
 *
 * Coverage: `coverage.all: false` avoids the Windows/ESM v8 double-listing
 * artifact. Start with a simple global threshold (80%) and raise it as your
 * suite grows.
 */
import { defineConfig } from "vitest/config";
import { fileURLToPath } from "node:url";

export default defineConfig({
  test: {
    environment: "node",
    passWithNoTests: true,
    server: {
      deps: {
        inline: ["@colyseus/core", "@colyseus/ws-transport", "@pm2/io"],
      },
    },
    alias: {
      "@pm2/io": fileURLToPath(new URL("./tests/stubs/pm2-io.ts", import.meta.url)),
    },
    coverage: {
      provider: "v8",
      all: false,
      reporter: ["text", "html"],
      include: ["src/**/*.ts"],
      thresholds: {
        lines: 80,
        statements: 80,
        functions: 80,
        branches: 80,
      },
    },
  },
});
