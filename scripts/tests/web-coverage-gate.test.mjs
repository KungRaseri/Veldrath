#!/usr/bin/env node
/**
 * Tests for `scripts/web-coverage-gate.mjs`, the block that heads the web coverage comment.
 *
 * The defect these lock down: the comment reported 89.8% branches (3,752 of 4,180) beside a gate
 * line that said 90.33% (4,428 of 4,902) for the same run, so a reviewer concluded the PR was red
 * when it was green. The script must therefore quote the gate's own totals, say which accounting
 * the Cobertura table uses, and never present the two as the same number.
 *
 * Same discipline as the sibling suites: run the real script against fixture files and assert what
 * it wrote. No mocking of the thing under test, no network, no vitest.
 *
 * Run with: node --test scripts/tests/web-coverage-gate.test.mjs
 */
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { after, before, describe, test } from 'node:test';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, '..', '..');
const script = join(repoRoot, 'scripts', 'web-coverage-gate.mjs');

let work;
let caseId = 0;

before(() => {
  work = mkdtempSync(join(tmpdir(), 'web-coverage-gate-'));
});

after(() => {
  rmSync(work, { recursive: true, force: true });
});

/**
 * A vitest run with a failing global branch threshold and one failing per-glob floor, the shape
 * reported on PR #88: the gate printed 4,428 of 4,902 branches while the Cobertura report carried
 * line conditions for only 4,180 sites.
 */
const gateLog = [
  '       Coverage enabled with v8',
  ' ✓ src/lib/game/session.test.ts (58 tests) 412ms',
  ' Test Files  101 passed (101)',
  '      Tests  1481 passed (1481)',
  '',
  ' % Stmts | % Branch | % Funcs | % Lines | Uncovered Line #s',
  '   96.68 |    89.41 |   95.08 |   96.57 |',
  '',
  'Statements   : 96.68% ( 10891/11264 )',
  'Branches     : 89.41% ( 4383/4902 )',
  'Functions    : 95.08% ( 2478/2606 )',
  'Lines        : 96.57% ( 6618/6853 )',
  '',
  'ERROR: Coverage for branches (89.41%) does not meet global threshold (90%)',
  'ERROR: Coverage for branches (89.41%) does not meet "src/lib/game/**/*.ts" threshold (92%)',
  ''
].join('\n');

const summary = {
  total: {
    lines: { total: 6853, covered: 6618, skipped: 0, pct: 96.57 },
    statements: { total: 11264, covered: 10891, skipped: 0, pct: 96.68 },
    functions: { total: 2606, covered: 2478, skipped: 0, pct: 95.08 },
    branches: { total: 4902, covered: 4383, skipped: 0, pct: 89.41 }
  },
  'src/lib/game/session.svelte.ts': {
    lines: { total: 100, covered: 100, skipped: 0, pct: 100 },
    statements: { total: 100, covered: 100, skipped: 0, pct: 100 },
    functions: { total: 10, covered: 10, skipped: 0, pct: 100 },
    branches: { total: 347, covered: 347, skipped: 0, pct: 100 }
  }
};

/**
 * Cobertura as vitest/istanbul writes it: the root attributes carry istanbul's branch-map totals
 * (equal to v8's — 4,383 of 4,902 in `gateLog`), while the per-line conditions drop the sites that
 * cannot be attached to a source line. The line appears twice, once under <method> and once at
 * class level — the script must count it once, at the higher of the two counts. `root: false`
 * writes a file with no root branch counters at all, the shape a non-istanbul producer gives.
 */
function cobertura({
  rootValid = 4902,
  rootCovered = 4383,
  root = true,
  condition = '89.76% (3752/4180)'
} = {}) {
  const rootBranches = root ? ` branches-valid="${rootValid}" branches-covered="${rootCovered}"` : '';
  return `<?xml version="1.0" ?>
<!DOCTYPE coverage SYSTEM "http://cobertura.sourceforge.net/xml/coverage-04.dtd">
<coverage lines-valid="6853" lines-covered="6618" line-rate="0.9657"${rootBranches} branch-rate="0.9033" timestamp="1795145238000" complexity="0" version="0.1">
  <sources><source>web/client</source></sources>
  <packages>
    <package name="src" line-rate="0.96" branch-rate="0.89">
      <classes>
        <class name="session.svelte.ts" filename="src/lib/game/session.svelte.ts" line-rate="1" branch-rate="0.89">
          <methods>
            <method name="reduce" hits="4" signature="()V">
              <lines><line number="42" hits="4" branch="true" condition-coverage="50% (1/2)"/></lines>
            </method>
          </methods>
          <lines>
            <line number="42" hits="4" branch="true" condition-coverage="${condition}"/>
          </lines>
        </class>
      </classes>
    </package>
  </packages>
</coverage>
`;
}

/** Write one case's inputs into its own directory and run the script over them. */
const EXTENSIONS = { log: 'log', summary: 'json', cobertura: 'xml' };

function runCase({ log = null, summary: summaryValue = null, cobertura: coberturaValue = null } = {}) {
  const dir = join(work, `case-${++caseId}`);
  mkdirSync(dir, { recursive: true });
  const paths = {};
  for (const [key, content] of Object.entries({ log, summary: summaryValue, cobertura: coberturaValue })) {
    if (content == null) continue;
    paths[key] = join(dir, `${key}.${EXTENSIONS[key]}`);
    writeFileSync(paths[key], typeof content === 'string' ? content : JSON.stringify(content, null, 2));
  }
  const out = join(dir, 'GateSummary.md');
  const args = [script, '--out', out];
  if (paths.log) args.push('--log', paths.log);
  args.push('--summary', paths.summary ?? join(dir, 'absent-summary.json'));
  args.push('--cobertura', paths.cobertura ?? join(dir, 'absent-cobertura.xml'));
  const result = spawnSync(process.execPath, args, { cwd: repoRoot, encoding: 'utf8' });
  const markdown = paths.log || paths.summary ? readFileSync(out, 'utf8') : null;
  return { ...result, markdown, out };
}

describe('gate totals', () => {
  test('quotes the coverage step log, including the threshold messages', () => {
    const { status, markdown } = runCase({ log: gateLog, summary, cobertura: cobertura() });
    assert.equal(status, 0);

    assert.match(markdown, /\*\*Enforced coverage gate — vitest\/v8\*\*/);
    assert.match(markdown, /\| Branches \| 89\.41% \| 4,383 \/ 4,902 \|/);
    assert.match(markdown, /\| Statements \| 96\.68% \| 10,891 \/ 11,264 \|/);
    assert.match(markdown, /\| Lines \| 96\.57% \| 6,618 \/ 6,853 \|/);
    assert.match(markdown, /\| Functions \| 95\.08% \| 2,478 \/ 2,606 \|/);

    // Both threshold messages survive: the global one and the per-glob floor, which no
    // machine-readable artifact carries.
    assert.match(markdown, /ERROR: Coverage for branches \(89\.41%\) does not meet global threshold \(90%\)/);
    assert.match(
      markdown,
      /ERROR: Coverage for branches \(89\.41%\) does not meet "src\/lib\/game\/\*\*\/\*\.ts" threshold \(92%\)/
    );

    // The table precedes the errors, and the gate block names its source as the log.
    assert.ok(markdown.indexOf('| Metric | Gate |') < markdown.indexOf('ERROR: Coverage'));
    assert.match(markdown, /quoted from the `Coverage gate` step's own output line for line/);

    // It is concatenated ahead of the reporter's summary: a trailing blank line keeps the next
    // heading from being glued to the paragraph above it.
    assert.ok(markdown.endsWith('\n\n'), 'the block must end with a blank line');
  });

  test('strips ANSI colour from the log before reading it', () => {
    const coloured = gateLog.replace(/^(Branches\s+:)/m, '\u001B[31m$1\u001B[0m');
    const { status, markdown } = runCase({ log: coloured, summary, cobertura: cobertura() });
    assert.equal(status, 0);
    assert.match(markdown, /\| Branches \| 89\.41% \| 4,383 \/ 4,902 \|/);
  });

  test('falls back to the json-summary when the log was not captured, and says so', () => {
    const { status, markdown } = runCase({ summary, cobertura: cobertura() });
    assert.equal(status, 0);
    assert.match(markdown, /\| Branches \| 89\.41% \| 4,383 \/ 4,902 \|/);
    assert.match(markdown, /coverage\/coverage-summary\.json`, the v8 totals from the same run/);
    assert.match(markdown, /the gate log was not captured/);
  });

  test('fails loudly when there is nothing to quote', () => {
    const { status, stderr } = runCase({});
    assert.equal(status, 1);
    assert.match(stderr, /no coverage totals found/);
  });
});

describe('the other accounting', () => {
  test('labels the Cobertura table with both numbers from the same run', () => {
    const { markdown } = runCase({ log: gateLog, summary, cobertura: cobertura() });

    // The report's Total row is the file's root counters, i.e. the gate's own 4,383 of 4,902;
    // the per-directory rows below it are still the 4,180 line conditions istanbul could place.
    assert.match(markdown, /Its Total row is the report's root counters/);
    assert.match(markdown, /\*\*4,383 of 4,902\*\* branch sites/);
    assert.match(markdown, /the two accountings agree on this run/);
    assert.match(markdown, /\*\*3,752 of 4,180\*\*/);
    assert.match(markdown, /722 sites have no row of their own/);
    assert.match(markdown, /do not sum to that Total/);
    assert.match(markdown, /not the gate/);

    // The old paragraph explained a gap the report's Total row no longer shows; it must be gone.
    assert.doesNotMatch(markdown, /carry no line-level condition/);
  });

  test('drops the contrast paragraph when no Cobertura file was written', () => {
    const { status, markdown } = runCase({ log: gateLog, summary });
    assert.equal(status, 0);
    assert.match(markdown, /\| Branches \| 89\.41%/);
    assert.doesNotMatch(markdown, /read back through \*\*Cobertura\*\*/);
  });

  test('warns when the log and the json-summary disagree instead of picking one silently', () => {
    const drifted = JSON.parse(JSON.stringify(summary));
    drifted.total.branches = { total: 4180, covered: 3752, skipped: 0, pct: 89.76 };
    const { markdown } = runCase({ log: gateLog, summary: drifted, cobertura: cobertura() });
    assert.match(markdown, /Warning — accounting mismatch/);
    assert.match(markdown, /Branches 4383\/4902 vs 3752\/4180/);
    // The gate log still wins the table itself.
    assert.match(markdown, /\| Branches \| 89\.41% \| 4,383 \/ 4,902 \|/);
  });

  test('warns when the Cobertura file is from another run', () => {
    const stale = cobertura({ rootValid: 4180, rootCovered: 3752 });
    const { markdown } = runCase({ log: gateLog, summary, cobertura: stale });
    assert.match(markdown, /the Cobertura root attributes report 3752 \/ 4180 branches/);
    // The paragraph must say the file is not this run's, and never present its numbers as the gate's.
    assert.match(
      markdown,
      /report \*\*3,752 of 4,180\*\* branch sites against the gate's \*\*4,383 of 4,902\*\*/
    );
    assert.match(markdown, /a Cobertura file that does not describe this run/);
    assert.match(markdown, /Compare branch coverage against the table above/);
  });

  test('keeps the per-line explanation when the file has no root counters', () => {
    // A producer that writes no root branches-valid/branches-covered attributes: the script cannot
    // tell whether the Total row is this run's, so it explains the per-line accounting instead.
    const { status, markdown } = runCase({
      log: gateLog,
      summary,
      cobertura: cobertura({ root: false })
    });
    assert.equal(status, 0);
    assert.doesNotMatch(markdown, /the Cobertura root attributes report/);
    assert.match(markdown, /It counts \*\*3,752 of 4,180\*\* branch conditions/);
    assert.match(markdown, /against the gate's \*\*4,383 of 4,902\*\* branch sites/);
    assert.match(markdown, /722 sites carry no line-level condition/);
    assert.match(markdown, /divided by a smaller denominator/);
    assert.match(markdown, /Compare branch coverage against the table above/);
  });

  test('restores the agreement sentence when the per-line sum already equals the gate', () => {
    // No root counters, and the per-line sum lands on the gate's own pair: the pre-v1.0.4 script
    // closed the paragraph with the agreement sentence rather than a bare full stop.
    const { status, markdown } = runCase({
      log: gateLog,
      summary,
      cobertura: cobertura({ root: false, condition: '89.41% (4383/4902)' })
    });
    assert.equal(status, 0);
    assert.match(markdown, /It counts \*\*4,383 of 4,902\*\* branch conditions/);
    assert.match(
      markdown,
      /against the gate's \*\*4,383 of 4,902\*\* branch sites\. The two accountings agree on this run\./
    );
    assert.doesNotMatch(markdown, /carry no line-level condition/);
  });
});
