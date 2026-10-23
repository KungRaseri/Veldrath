/**
 * No-op stub for `@pm2/io` (test-only).
 *
 * `@colyseus/core`'s Stats module imports `@pm2/io` and calls `io.default.metric(...)`
 * when the matchmaker initializes. In the Vitest worker pool, `@pm2/io`'s agent
 * calls `process.send()`, which is not allowed — so this stub satisfies the
 * module with no-op objects. Alias configured in `vitest.config.ts`.
 */

interface IoMetricOptions {
  id: string;
  name: string;
  value: () => number;
}

/** A no-op metric handle. */
function metric(_options: IoMetricOptions): void {
  void _options;
  // no-op
}

const io = {
  metric,
};

export default io;
