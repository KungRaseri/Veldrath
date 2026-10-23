/**
 * Vitest config for @your-scope/engine.
 *
 * - Node environment.
 * - `coverage.all: false` avoids the Windows/ESM v8 double-listing artifact
 *   (every file is imported by at least one test, so real counters are
 *   reported and the global threshold is a faithful gate). If you add a file
 *   that no test imports, add a test for it.
 * - Start with a simple global threshold (80%) and raise it as your suite
 *   grows; the original project used a per-file gate with 90–100% targets.
 */
import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    environment: "node",
    passWithNoTests: true,
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
