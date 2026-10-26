/**
 * Vitest config for @veldrath/shared.
 *
 * THERE IS NO COVERAGE GATE HERE, ON PURPOSE.
 *
 * This package is currently all declarations: `src/db/auth.schema.ts` defines the Better Auth tables and
 * `src/index.ts` is a placeholder for shared protocol types. There is no executable logic to measure, so a
 * `coverage` script could only report a number that means nothing - and for months it "passed" while the suite was
 * empty (`passWithNoTests`), which is exactly the dishonesty this milestone exists to remove. The script is gone
 * from package.json; `npm run test` still runs the contract suite.
 *
 * WHAT THE TESTS DO COVER:
 * `src/db/auth.schema.test.ts` pins the parts other code and the committed migrations depend on: the `auth` schema,
 * table names, uniqueness of `email`/`username`/`token`, the cascade on `user_id`, the index names Drizzle emitted,
 * and the relation bindings. Those are assertions about declarations, which is the only behaviour this package has.
 *
 * WHEN THE GATE COMES BACK:
 * The moment this package owns real logic (shared validators, protocol helpers, generated contracts with transforms),
 * add `"coverage": "vitest run --coverage"` back plus a per-file threshold block. Keep `coverage.all: false` when you
 * do: with `all: true` Windows reports every covered file twice (real counters + an all-zeros "(empty-report)" entry),
 * which halves the printed aggregate and makes global thresholds unreliable.
 */
import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    environment: "node",
    exclude: ["src/schema/**", "src/ticket.ts", "src/ticket.test.ts"],
  },
});
