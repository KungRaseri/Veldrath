/**
 * Vitest config for @your-scope/shared.
 *
 * COVERAGE GATE:
 * - Soft threshold: 90% per file (lines/statements/functions/branches) via
 *   `coverage.thresholds.perFile: true` + global 90 values. This is the CI gate.
 *
 * WINDOWS/ESM V8 DOUBLE-LISTING ARTIFACT:
 * Same Windows drive-letter case artifact as `engine/vitest.config.ts` — with
 * `coverage.all: true` (default) every covered file is reported twice (real
 * counters + an all-zeros "(empty-report)" entry), halving the printed
 * aggregate. GLOBAL thresholds are therefore unreliable (do not set them). We
 * set `coverage.all: false` so `getUntestedFiles` never runs; every source file
 * here is imported by at least one test, so per-file thresholds are faithful.
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
        perFile: true,
        lines: 90,
        statements: 90,
        functions: 90,
        branches: 90,
      },
    },
  },
});
