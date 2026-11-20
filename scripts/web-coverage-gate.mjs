#!/usr/bin/env node
/**
 * Quote the enforced coverage gate at the top of the web PR comment.
 *
 * The web coverage check is `npm run coverage --workspace client` (vitest + v8, thresholds in
 * `web/client/vite.config.ts`). The comment beside it is rendered by
 * `scripts/coverage-report.sh` (codecov/codecov-report) from the Cobertura XML vitest writes,
 * and that tool counts branches differently: it sums the per-line `condition-coverage="x% (c/t)"`
 * attributes, while v8 counts branch sites. istanbul writes a line-level branch count only for a
 * site it can attach to a source line, so the tool's denominator is smaller and its percentage
 * reads lower than the gate's for the very same run. On PR #88 the gate printed
 * `Branches : 90.33% ( 4428/4902 )` and the comment said `89.8% (3,752 of 4,180)`.
 *
 * This script prints the gate's own totals, taken from the coverage step's log (verbatim lines,
 * including vitest's `ERROR: Coverage for …` threshold messages, so per-glob floors surface too),
 * and cross-checks them against the json-summary v8 writes. It also states, in the comment, what
 * the Cobertura table underneath is counting, with both numbers for this run — so a reader can
 * never mistake one accounting for the other.
 *
 * Usage:
 *   node scripts/web-coverage-gate.mjs [--log <file>] [--summary <file>] [--cobertura <file>]
 *                                      [--out <file>] [--quiet]
 *
 * Defaults: --summary web/client/coverage/coverage-summary.json
 *           --cobertura web/client/coverage/cobertura-coverage.xml
 *           --out CoverageReport/GateSummary.md
 * Any source that is missing is skipped; the run fails only when no source can be read at all.
 */
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';

const METRICS = ['Statements', 'Branches', 'Functions', 'Lines'];

function arg(name, fallback) {
  const i = process.argv.indexOf(name);
  return i === -1 || i + 1 >= process.argv.length ? fallback : process.argv[i + 1];
}

const logPath = arg('--log', null);
const summaryPath = arg('--summary', 'web/client/coverage/coverage-summary.json');
const coberturaPath = arg('--cobertura', 'web/client/coverage/cobertura-coverage.xml');
const outPath = arg('--out', 'CoverageReport/GateSummary.md');
const quiet = process.argv.includes('--quiet');

/** Group thousands so 4902 reads as 4,902 in the table and in prose. */
function count(n) {
  return String(n).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
}

function pct(covered, total, digits = 2) {
  return total > 0 ? ((covered / total) * 100).toFixed(digits) : '0.00';
}

function readIfPresent(path) {
  if (!path) return null;
  const abs = resolve(path);
  return existsSync(abs) ? readFileSync(abs, 'utf8') : null;
}

/**
 * The gate's printed totals. vitest's text-summary reporter writes four lines such as
 * `Branches     : 90.33% ( 4428/4902 )`; the last block in the log wins, because a run that
 * retries or prints a per-file table first would otherwise be read from the wrong place.
 */
function parseGateLog(text) {
  if (!text) return { metrics: null, errors: [] };
  const clean = text.replace(/\u001B\[[0-9;]*[A-Za-z]/g, '');
  const metrics = {};
  const errors = [];
  for (const raw of clean.split(/\r?\n/)) {
    const line = raw.trim();
    const row = /^(Statements|Branches|Functions|Lines)\s*:\s*([\d.]+)%\s*\(\s*(\d+)\s*\/\s*(\d+)\s*\)$/.exec(
      line
    );
    if (row) {
      metrics[row[1]] = { pct: row[2], covered: Number(row[3]), total: Number(row[4]) };
      continue;
    }
    const failure = /^ERROR:\s*(Coverage for .*)$/.exec(line);
    if (failure && !errors.includes(failure[1])) errors.push(failure[1]);
  }
  const complete = METRICS.every((m) => metrics[m]);
  return { metrics: complete ? metrics : null, errors };
}

/** v8's own totals, written by the `json-summary` reporter in the same run. */
function parseSummary(text) {
  if (!text) return null;
  let data;
  try {
    data = JSON.parse(text);
  } catch {
    return null;
  }
  const total = data.total;
  if (!total) return null;
  const metrics = {};
  for (const m of METRICS) {
    const entry = total[m.toLowerCase()];
    if (!entry || typeof entry.total !== 'number') return null;
    metrics[m] = { pct: pct(entry.covered, entry.total), covered: entry.covered, total: entry.total };
  }
  return metrics;
}

/**
 * What the Cobertura report contains. Two things are read:
 *   - the root `<coverage branches-valid/branches-covered>` attributes, which istanbul computes
 *     from the branch map and therefore agree with v8 exactly (a cross-check on the gate numbers);
 *   - the per-line `condition-coverage` attributes, summed the way codecov-report sums them,
 *     which is the accounting the per-directory table in the comment is built on.
 * Each line appears once per class and again under each `<method>`; the highest counts win, which
 * mirrors `scripts/check-dotnet-coverage.mjs` and the reporter's `Math.Max` merge.
 */
function parseCobertura(text) {
  if (!text) return null;
  const rootAttrs = /<coverage\s([^>]*)>/.exec(text)?.[1];
  if (!rootAttrs) return null;
  const rootNum = (name) => {
    const m = new RegExp(`${name}="(\\d+)"`).exec(rootAttrs);
    return m ? Number(m[1]) : null;
  };
  let conditionCovered = 0;
  let conditionTotal = 0;
  for (const classMatch of text.matchAll(/<class\s[^>]*>([\s\S]*?)<\/class>/g)) {
    const perLine = new Map();
    for (const [, attrs] of classMatch[1].matchAll(/<line\s([^>]*?)\/?>/g)) {
      const number = /number="(\d+)"/.exec(attrs)?.[1];
      if (number == null) continue;
      const cc = /condition-coverage="[\d.]+%\s*\((\d+)\/(\d+)\)"/.exec(attrs);
      const previous = perLine.get(number) ?? { covered: 0, total: 0 };
      if (cc) {
        previous.covered = Math.max(previous.covered, Number(cc[1]));
        previous.total = Math.max(previous.total, Number(cc[2]));
      }
      perLine.set(number, previous);
    }
    for (const { covered, total } of perLine.values()) {
      conditionCovered += covered;
      conditionTotal += total;
    }
  }
  return {
    branchesCovered: rootNum('branches-covered'),
    branchesValid: rootNum('branches-valid'),
    conditionCovered,
    conditionTotal
  };
}

const gateLog = parseGateLog(readIfPresent(logPath));
const fromSummary = parseSummary(readIfPresent(summaryPath));
const cobertura = parseCobertura(readIfPresent(coberturaPath));

const gate = gateLog.metrics ?? fromSummary;
if (!gate) {
  console.error(
    'web-coverage-gate: no coverage totals found. The gate log and the json-summary are both ' +
      'missing or unreadable, so there is nothing to quote.'
  );
  process.exit(1);
}

const source = gateLog.metrics
  ? `the \`Coverage gate\` step's own output line for line`
  : '`coverage/coverage-summary.json`, the v8 totals from the same run (the gate log was not captured)';

const notes = [];

// Cross-check 1: the log and v8's json-summary are the same measurement; say so when they drift
// instead of silently preferring one.
if (gateLog.metrics && fromSummary) {
  const drift = METRICS.filter(
    (m) =>
      gateLog.metrics[m].covered !== fromSummary[m].covered ||
      gateLog.metrics[m].total !== fromSummary[m].total
  );
  if (drift.length) {
    notes.push(
      `the gate log and \`coverage-summary.json\` disagree on ${drift.join(', ')} ` +
        `(${drift.map((m) => `${m} ${gateLog.metrics[m].covered}/${gateLog.metrics[m].total} vs ${fromSummary[m].covered}/${fromSummary[m].total}`).join('; ')}) — one of the two reporters changed its accounting; the table quotes the gate log`
    );
  }
}

// Cross-check 2: the Cobertura root attributes are istanbul's branch-map totals and should equal
// v8's; if they do not, the file no longer describes the same run.
if (cobertura && cobertura.branchesValid != null && gate.Branches) {
  if (
    cobertura.branchesValid !== gate.Branches.total ||
    (cobertura.branchesCovered != null && cobertura.branchesCovered !== gate.Branches.covered)
  ) {
    notes.push(
      `the Cobertura root attributes report ${cobertura.branchesCovered ?? '?'} / ${cobertura.branchesValid} branches against the gate's ${gate.Branches.covered} / ${gate.Branches.total}; the file may not be from this run`
    );
  }
}

const lines = [];
lines.push('**Enforced coverage gate — vitest/v8**');
lines.push('');
lines.push(
  `The coverage check is \`npm run coverage --workspace client\`; these are the totals it enforces, ` +
    `quoted from ${source}. Compare them against the thresholds in \`web/client/vite.config.ts\` — ` +
    'nothing else on this page is the gate.'
);
lines.push('');
lines.push('| Metric | Gate | Covered / total |');
lines.push('|:--|--:|--:|');
for (const m of METRICS) {
  lines.push(`| ${m} | ${gate[m].pct}% | ${count(gate[m].covered)} / ${count(gate[m].total)} |`);
}
lines.push('');

if (gateLog.errors.length) {
  lines.push('Threshold failures reported in this run:');
  lines.push('');
  lines.push('```text');
  for (const error of gateLog.errors) lines.push(`ERROR: ${error}`);
  lines.push('```');
  lines.push('');
}

if (notes.length) {
  lines.push('> **Warning — accounting mismatch.** ' + notes.join('; ') + '.');
  lines.push('');
}

if (cobertura && cobertura.conditionTotal > 0 && gate.Branches) {
  const missing = gate.Branches.total - cobertura.conditionTotal;
  lines.push(
    `> The per-directory table below is the same run read back through **Cobertura** (istanbul ` +
      `line conditions), not the gate. It counts **${count(cobertura.conditionCovered)} of ` +
      `${count(cobertura.conditionTotal)}** branch conditions against the gate's ` +
      `**${count(gate.Branches.covered)} of ${count(gate.Branches.total)}** branch sites` +
      (missing > 0
        ? `: ${count(missing)} sites carry no line-level condition, so the Cobertura percentage is ` +
          `divided by a smaller denominator and reads lower. Compare branch coverage against the ` +
          `table above, never against the one below.`
        : '. The two accountings agree on this run.')
  );
  lines.push('');
}

// The block is concatenated ahead of the reporter's summary, so it has to end with a blank line
// or the next heading would be glued to the paragraph above it.
const markdown = lines.join('\n').replace(/\n*$/, '\n\n');
const out = resolve(outPath);
mkdirSync(dirname(out), { recursive: true });
writeFileSync(out, markdown, 'utf8');
if (!quiet) process.stdout.write(markdown + '\n');
