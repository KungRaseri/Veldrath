#!/usr/bin/env node
/**
 * CI summary generator for the GitHub Actions workflow (`.github/workflows/node.yml`).
 *
 * Reads per-workspace Vitest JSON results (`ci-results/<workspace>.json`), the coverage
 * text log (`ci-results/coverage.log`), and the Playwright e2e log (`ci-results/e2e.log`),
 * combines them with the per-step outcomes passed as `name=value` CLI args, and writes a
 * markdown summary to `$GITHUB_STEP_SUMMARY` (GitHub Actions step summary — the per-run
 * "CI/CD dashboard").
 *
 * Exits non-zero when any gated step failed (so the job turns red), even though the check
 * steps themselves use `continue-on-error: true` to let every result be collected first.
 *
 * Usage (from repo root, run by the workflow):
 *   node scripts/ci-summary.mjs build=success typecheck=failure ... coverage=success
 */
import { readFileSync, appendFileSync } from "node:fs";
import { join } from "node:path";

const RESULTS_DIR = "ci-results";
const WORKSPACES = ["shared", "engine", "server", "client"];

function readJson(name) {
  try {
    return JSON.parse(readFileSync(join(RESULTS_DIR, `${name}.json`), "utf8"));
  } catch {
    return null;
  }
}

function readText(name) {
  try {
    return readFileSync(join(RESULTS_DIR, name), "utf8");
  } catch {
    return null;
  }
}

function fmt(outcome) {
  if (outcome === "success") return "✅";
  if (outcome === "failure") return "❌";
  return "⏭️"; // skipped / unknown
}

/** Removes ANSI SGR/CSI escape codes (Vitest keeps colors under some redirects). */
function stripAnsi(text) {
  return text.replace(/\u001B\[[0-9;]*[a-zA-Z]/g, "");
}

/**
 * Splits combined Vitest output (coverage log) into per-workspace blocks by the
 * ` RUN  v3.2.7 <cwd>` header; keys are the basename of the run's working directory
 * (the workspace name).
 */
function splitByWorkspace(text) {
  const blocks = new Map();
  if (!text) return blocks;
  let current;
  for (const line of stripAnsi(text).split(/\r?\n/)) {
    const m = line.match(/\bRUN\s+v\d+\.\d+\.\d+\s+(.+?)\s*$/);
    if (m) {
      current = m[1].trim().split(/[\\/]/).pop();
      blocks.set(current, []);
    } else if (current) {
      blocks.get(current).push(line);
    }
  }
  return blocks;
}

/** Extracts the v8 text-table "All files" row: [stmts, branch, funcs, lines]. */
function allFilesRow(lines) {
  for (const line of lines) {
    const m = line.match(/All files\s*\|/i);
    if (!m) continue;
    const cols = line.split("|").map((s) => s.trim());
    if (cols.length >= 5) {
      return [cols[1], cols[2], cols[3], cols[4]];
    }
  }
  return null;
}

/** Vitest JSON counts. */
function testCounts(json) {
  if (!json) return null;
  return {
    tests: json.numTotalTests ?? 0,
    passed: json.numPassedTests ?? 0,
    failed: json.numFailedTests ?? 0,
    skipped: json.numSkippedTests ?? 0,
  };
}

/** Playwright summary counts ("9 passed, 1 skipped, 1 failed …"). */
function e2eCounts(log) {
  if (!log) return null;
  log = stripAnsi(log);
  const grab = (re) => {
    let value = 0;
    for (const m of log.matchAll(re)) value = Number(m[1]);
    return value;
  };
  return {
    passed: grab(/(\d+)\s+passed/g),
    skipped: grab(/(\d+)\s+skipped/g),
    failed: grab(/(\d+)\s+failed/g),
  };
}

const args = Object.fromEntries(
  process.argv.slice(2).map((a) => {
    const i = a.indexOf("=");
    return i === -1 ? [a, "success"] : [a.slice(0, i), a.slice(i + 1)];
  }),
);

const coverageBlocks = splitByWorkspace(readText("coverage.log"));
const e2e = e2eCounts(readText("e2e.log"));

const lines = [];
lines.push("## ⚙️ CI Results");
lines.push("");
lines.push("| Component | Result |");
lines.push("| --- | --- |");
const components = [
  ["Build (tsc --noEmit + vite build)", "build"],
  ["Typecheck (incl. svelte-check)", "typecheck"],
  ["Lint (ESLint + Prettier)", "lint"],
  ["DB migrations", "migrate"],
  ["Tests — shared", "test-shared"],
  ["Tests — engine", "test-engine"],
  ["Tests — server", "test-server"],
  ["Tests — client (unit)", "test-client"],
  ["Tests — e2e (Playwright)", "test-e2e"],
  ["Coverage gate (shared/engine/server)", "coverage"],
];
for (const [label, key] of components) {
  lines.push(`| ${label} | ${fmt(args[key] ?? "skipped")} |`);
}
lines.push("");

lines.push("### 🧪 Test counts");
lines.push("");
lines.push("| Suite | Result |");
lines.push("| --- | --- |");
const unitKeys = { shared: "test-shared", engine: "test-engine", server: "test-server", client: "test-client" };
for (const ws of WORKSPACES) {
  const c = testCounts(readJson(ws));
  const ok = (args[unitKeys[ws]] ?? "skipped") === "success";
  const label = ws === "client" ? "client (unit)" : ws;
  lines.push(
    `| ${label} | ${ok ? "✅" : "❌"} ${c ? `${c.tests} tests (${c.passed} passed, ${c.skipped} skipped${c.failed ? `, ${c.failed} failed` : ""})` : "n/a"} |`,
  );
}
if (e2e) {
  const ok = (args["test-e2e"] ?? "skipped") === "success";
  lines.push(
    `| e2e (Playwright) | ${ok ? "✅" : "❌"} ${e2e.passed} passed, ${e2e.skipped} skipped${e2e.failed ? `, ${e2e.failed} failed` : ""} |`,
  );
} else {
  lines.push(`| e2e (Playwright) | ${fmt(args["test-e2e"] ?? "skipped")} (log not parsed) |`);
}
lines.push("");

lines.push("### 📊 Coverage");
lines.push("");
lines.push("| Workspace | Stmts | Branch | Funcs | Lines |");
lines.push("| --- | --- | --- | --- | --- |");
for (const ws of WORKSPACES) {
  const row = allFilesRow(coverageBlocks.get(ws) ?? []);
  if (row) {
    lines.push(`| ${ws} | ${row[0]} | ${row[1]} | ${row[2]} | ${row[3]} |`);
  } else if (ws === "client") {
    lines.push("| client | — (no coverage gate; unit coverage ≥80%) | — | — | — |");
  } else {
    lines.push(`| ${ws} | — (no coverage output captured) | — | — | — |`);
  }
}
lines.push("");
lines.push("_Artifacts on this run: coverage reports, client build, Playwright report, CI logs._");

const summaryPath = process.env.GITHUB_STEP_SUMMARY;
if (summaryPath) {
  appendFileSync(summaryPath, lines.join("\n") + "\n");
}

// Surface the summary on stdout too (useful when run outside Actions).
console.log(lines.join("\n"));

const failed = Object.values(args).some((v) => v === "failure");
process.exit(failed ? 1 : 0);
