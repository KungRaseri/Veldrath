#!/usr/bin/env node
/**
 * Enforce the .NET coverage contract from a merged Cobertura report.
 *
 * Coverage is a property of an assembly, not of the job that happened to run a test
 * against it, so the only honest number is the merged one: `scripts/coverage-report.sh`
 * (codecov/codecov-report) over the six test projects' Cobertura output. This script compares
 * that with `coverage-floors.json` and exits non-zero on a regression.
 *
 * Two integrity checks beyond the floors, both there to stop the gate passing by
 * measuring nothing:
 *   - an assembly named in the floor table must appear in the report (a dead test project
 *     would otherwise just remove it from the report);
 *   - an assembly in the report with no floor must fail (a new shipped project has to get
 *     a floor in the same change that adds it).
 *
 * Usage: node scripts/check-dotnet-coverage.mjs --report <Cobertura.xml> [--floors <json>] [--suggest]
 */
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));

function arg(name, fallback) {
  const i = process.argv.indexOf(name);
  return i === -1 ? fallback : process.argv[i + 1];
}

const reportPath = resolve(arg('--report', 'CoverageReport/Cobertura.xml'));
const floorsPath = resolve(arg('--floors', resolve(here, 'coverage-floors.json')));
const suggest = process.argv.includes('--suggest');

const floors = JSON.parse(readFileSync(floorsPath, 'utf8'));
const xml = readFileSync(reportPath, 'utf8');
const minBranchSites = floors.branchFloorMinSites ?? 100;

/**
 * Rates come from the <package> attributes; counts are re-derived from the class-level
 * <lines> blocks. Each line appears twice in the XML — once under <methods>/<method> and
 * again in the class-level <lines> — so they are deduplicated by line number per class.
 */
function parsePackages(source) {
  const packages = new Map();
  for (const chunk of source.split(/<package\s+/).slice(1)) {
    const name = /^name="([^"]+)"/.exec(chunk)?.[1];
    if (!name) continue;
    let lineSites = 0;
    let covered = 0;
    let branchSites = 0;
    for (const classMatch of chunk.matchAll(/<class\s[^>]*>([\s\S]*?)<\/class>/g)) {
      const seen = new Map();
      for (const [, attrs] of classMatch[1].matchAll(/<line\s([^>]*)\/?>/g)) {
        const number = /number="(\d+)"/.exec(attrs)?.[1];
        if (number == null) continue;
        const hits = Number(/hits="(\d+)"/.exec(attrs)?.[1] ?? '0');
        const cc = /condition-coverage="[\d.]+%\s*\((\d+)\/(\d+)\)"/.exec(attrs);
        const previous = seen.get(number);
        if (!previous || hits > previous.hits) seen.set(number, { hits, cc });
        else if (previous && !previous.cc && cc) previous.cc = cc;
      }
      for (const { hits, cc } of seen.values()) {
        lineSites += 1;
        if (hits > 0) covered += 1;
        if (cc) branchSites += Number(cc[2]);
      }
    }
    packages.set(name, {
      lineRate: Number(/line-rate="([\d.]+)"/.exec(chunk)?.[1]),
      branchRate: Number(/branch-rate="([\d.]+)"/.exec(chunk)?.[1]),
      lineSites,
      covered,
      branchSites,
    });
  }
  return packages;
}

const rootAttrs = /<coverage\s([^>]*)>/.exec(xml)?.[1] ?? '';
const rootNum = (name) => Number(new RegExp(`${name}="([\\d.]+)"`).exec(rootAttrs)?.[1]);
const totals = { lineRate: rootNum('line-rate'), branchRate: rootNum('branch-rate') };

const packages = parsePackages(xml);
const failures = [];
const notes = [];
const rows = [];
const pct = (rate) => `${(rate * 100).toFixed(1)}%`;
const pad = (v, w) => String(v).padStart(w);

if (packages.size === 0) {
  console.error(`error: no <package> elements found in ${reportPath} — the report is empty or not Cobertura`);
  process.exit(1);
}

for (const [assembly, floor] of Object.entries(floors.assemblies)) {
  const pkg = packages.get(assembly);
  if (!pkg) {
    failures.push(`${assembly}: missing from the report — a test project did not run, so this assembly is not measured`);
    rows.push([assembly, '—', floor.line, '—', floor.branch ?? '—', 'MISSING']);
    continue;
  }
  const linePct = pkg.lineRate * 100;
  const branchPct = pkg.branchRate * 100;
  const branchGated = floor.branch != null && pkg.branchSites >= minBranchSites;

  if (linePct + 1e-9 < floor.line) {
    failures.push(`${assembly}: line ${pct(pkg.lineRate)} is below its floor of ${floor.line}% (regression: ${(floor.line - linePct).toFixed(2)} points)`);
  }
  if (branchGated && branchPct + 1e-9 < floor.branch) {
    failures.push(`${assembly}: branch ${pct(pkg.branchRate)} is below its floor of ${floor.branch}% (regression: ${(floor.branch - branchPct).toFixed(2)} points)`);
  }
  if (floor.branch != null && !branchGated) {
    notes.push(`${assembly}: branch floor not applied — ${pkg.branchSites} branch sites is under the ${minBranchSites} minimum for a meaningful branch rate (measured ${pct(pkg.branchRate)})`);
  }
  if (suggest) {
    if (linePct >= floor.line + 3) notes.push(`${assembly}: line is ${(linePct - floor.line).toFixed(1)} points above its floor — raise it to ${Math.floor(linePct)}`);
    if (branchGated && branchPct >= floor.branch + 3) notes.push(`${assembly}: branch is ${(branchPct - floor.branch).toFixed(1)} points above its floor — raise it to ${Math.floor(branchPct)}`);
  }
  rows.push([assembly, pct(pkg.lineRate), floor.line, branchGated ? pct(pkg.branchRate) : 'n/a', branchGated ? floor.branch : 'n/a', '']);
}

for (const assembly of packages.keys()) {
  if (!(assembly in floors.assemblies)) {
    failures.push(`${assembly}: in the report but has no floor — add it to coverage-floors.json in this change`);
  }
}

if (totals.lineRate * 100 + 1e-9 < floors.global.line) {
  failures.push(`TOTAL: line ${pct(totals.lineRate)} is below the global floor of ${floors.global.line}%`);
}
if (totals.branchRate * 100 + 1e-9 < floors.global.branch) {
  failures.push(`TOTAL: branch ${pct(totals.branchRate)} is below the global floor of ${floors.global.branch}%`);
}

const width = Math.max(...Object.keys(floors.assemblies).map((n) => n.length), 16);
console.log(`${'assembly'.padEnd(width)}  measured line / floor   measured branch / floor`);
for (const [name, line, lineFloor, branch, branchFloor, flag] of rows) {
  console.log(`${name.padEnd(width)}  ${pad(line, 8)} / ${pad(lineFloor, 5)}   ${pad(branch, 8)} / ${pad(branchFloor, 5)}  ${flag}`);
}
console.log(`${'TOTAL'.padEnd(width)}  ${pad(pct(totals.lineRate), 8)} / ${pad(floors.global.line, 5)}   ${pad(pct(totals.branchRate), 8)} / ${pad(floors.global.branch, 5)}`);
console.log(`\n(${[...packages.values()].reduce((a, p) => a + p.lineSites, 0)} lines, ${[...packages.values()].reduce((a, p) => a + p.branchSites, 0)} branches, ${packages.size} assemblies)`);

for (const note of notes) console.log(`note: ${note}`);

if (failures.length > 0) {
  console.error(`\nCoverage contract failed (${failures.length}):`);
  for (const f of failures) console.error(`  - ${f}`);
  console.error('\nFloors live in scripts/coverage-floors.json. Never lower one to make this pass — add tests.');
  process.exit(1);
}
console.log('\nCoverage contract satisfied.');
