#!/usr/bin/env node
/**
 * Tests for the e2e reporting tooling: `scripts/e2e-summary.mjs` and
 * `scripts/check-e2e-manifest.mjs`.
 *
 * Same discipline as `scripts/tests/forgejo-pr-comment.test.sh`: run the real script against a
 * fixture and assert what it did, no mocking of the thing under test. The fixtures live in
 * `scripts/tests/fixtures/`, and the checker's failure paths are exercised by mutating a copy of
 * the fixture tree, so a negative control is a real one.
 *
 * Run with: node --test scripts/tests/e2e-reporting.test.mjs
 */
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { after, before, describe, test } from 'node:test';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, '..', '..');
const summaryScript = join(repoRoot, 'scripts', 'e2e-summary.mjs');
const checkScript = join(repoRoot, 'scripts', 'check-e2e-manifest.mjs');
const fixtureTree = join(here, 'fixtures', 'e2e-project');
const fixtureResults = join(here, 'fixtures', 'e2e-playwright.json');

let work;

before(() => {
	work = mkdtempSync(join(tmpdir(), 'e2e-reporting-'));
});

after(() => {
	rmSync(work, { recursive: true, force: true });
});

function run(script, args) {
	const result = spawnSync(process.execPath, [script, ...args], {
		cwd: repoRoot,
		encoding: 'utf8'
	});
	return { code: result.status, out: `${result.stdout ?? ''}${result.stderr ?? ''}` };
}

/** Copy the fixture project into the work dir so a test can break one thing at a time. */
function copyProject(name) {
	const target = join(work, name);
	cpSync(fixtureTree, target, { recursive: true });
	return {
		root: target,
		manifest: join(target, 'tests', 'e2e-manifest.json'),
		spec: (file) => join(target, 'tests', file)
	};
}

/** Read the fixture report, let a test mutate it, and write it back under `work`. */
function mutateResults(name, mutate) {
	const report = JSON.parse(readFileSync(fixtureResults, 'utf8'));
	mutate(report);
	const path = join(work, name);
	writeFileSync(path, JSON.stringify(report, null, '\t'), 'utf8');
	return path;
}

function summaryArgs(results, out) {
	return ['--results', results, '--out', out];
}

/** Read the totals table back so a test asserts the numbers a reviewer actually reads. */
function totalsFrom(body) {
	const value = (label) => Number(body.match(new RegExp(`\\| ${label} \\| (\\d+) \\|`))?.[1]);
	return {
		specs: value('Specs'),
		tests: value('Tests'),
		passed: value('Passed'),
		failed: value('Failed'),
		flaky: value('Flaky'),
		skipped: value('Skipped')
	};
}

/** `passed + failed + flaky + skipped === tests` — the invariant that must never regress. */
function consistent(counts) {
	return (
		counts.specs >= 0 &&
		counts.passed + counts.failed + counts.flaky + counts.skipped === counts.tests
	);
}

describe('e2e-summary', () => {
	test('renders totals, the failing test and its trace path', () => {
		const out = join(work, 'Summary.md');
		const { code, out: log } = run(summaryScript, summaryArgs(fixtureResults, out));

		assert.equal(code, 0, log);
		const body = readFileSync(out, 'utf8');

		assert.match(body, /\| Tests \| 3 \|/);
		assert.match(body, /\| Passed \| 2 \|/);
		assert.match(body, /\| Failed \| 1 \|/);
		assert.match(body, /#### Not green/);
		assert.match(body, /a journey that fails/);
		// The headline is the first line of the error, not the whole stack.
		assert.match(body, /toBeVisible/);
		assert.match(body, /trace: `test-results\/one-a-journey-that-fails\/trace\.zip`/);
		assert.match(body, /Specs, slowest first/);
	});

	test('a flaky test is reported as flaky, not as a failure', () => {
		const results = mutateResults('flaky.json', (report) => {
			const spec = report.suites[0].specs[1];
			spec.tests[0].status = 'flaky';
			report.stats.unexpected = 0;
			report.stats.flaky = 1;
		});
		const out = join(work, 'flaky.md');
		const { code } = run(summaryScript, summaryArgs(results, out));
		assert.equal(code, 0);

		const body = readFileSync(out, 'utf8');
		assert.match(body, /\| Flaky \| 1 \|/);
		assert.match(body, /\| Failed \| 0 \|/);
		assert.match(body, /marker: flaky|— flaky:/);
	});

	test('a skipped attempt that passed on retry is passed, not flaky', () => {
		// Run 2194: encounters.e2e.ts:90 timed out inside a serial group, so the group's next test
		// (:134) was marked `skipped` on attempt 1; the group retry then passed both. Playwright
		// counts that as 1 flaky / 20 passed, and the summary must agree instead of saying 2 flaky.
		const results = mutateResults('skipped-then-passed.json', (report) => {
			const test = report.suites[0].specs[0].tests[0];
			test.results = [
				{
					workerIndex: 0,
					status: 'skipped',
					duration: 0,
					errors: [],
					stdout: [],
					stderr: [],
					retry: 0,
					startTime: '2026-11-20T00:00:00.000Z',
					attachments: []
				},
				{ ...test.results[0], retry: 1, duration: 2100 }
			];
			// Playwright's own status for this shape is 'expected': a skipped attempt is not a failure.
			test.status = 'expected';
		});
		const out = join(work, 'skipped-then-passed.md');
		const { code, out: log } = run(summaryScript, summaryArgs(results, out));
		assert.equal(code, 0, log);

		const counts = totalsFrom(readFileSync(out, 'utf8'));
		assert.equal(counts.tests, 3);
		assert.equal(counts.passed, 2);
		assert.equal(counts.failed, 1);
		assert.equal(counts.flaky, 0);
		assert.equal(counts.skipped, 0);
		assert.deepEqual(consistent(counts), true);
		assert.match(log, /e2e summary: FAILED — 2\/3 passed, 1 failed, 0 flaky, 0 skipped/);
	});

	test('a failed attempt followed by a pass is still flaky', () => {
		const results = mutateResults('failed-then-passed.json', (report) => {
			const test = report.suites[0].specs[1].tests[0];
			test.results = [{ ...test.results[0], retry: 0 }, { ...test.results[0], status: 'passed', retry: 1 }];
			test.status = 'flaky';
			report.stats.unexpected = 0;
			report.stats.flaky = 1;
		});
		const out = join(work, 'failed-then-passed.md');
		const { code } = run(summaryScript, summaryArgs(results, out));
		assert.equal(code, 0);

		const counts = totalsFrom(readFileSync(out, 'utf8'));
		assert.equal(counts.flaky, 1);
		assert.equal(counts.failed, 0);
		assert.equal(counts.passed, 2);
		assert.deepEqual(consistent(counts), true);

		// The same attempts with no Playwright `status` field still read as flaky: the fallback
		// classifies from the attempts, and only a real failure is a flake.
		const noStatus = mutateResults('failed-then-passed-no-status.json', (report) => {
			const test = report.suites[0].specs[1].tests[0];
			test.results = [{ ...test.results[0], retry: 0 }, { ...test.results[0], status: 'passed', retry: 1 }];
			delete test.status;
		});
		const fallback = join(work, 'failed-then-passed-no-status.md');
		assert.equal(run(summaryScript, summaryArgs(noStatus, fallback)).code, 0);
		assert.equal(totalsFrom(readFileSync(fallback, 'utf8')).flaky, 1);
	});

	test('a timed-out attempt followed by a pass is flaky', () => {
		// The attempt status `timedOut` is a genuine failure too — the very failure that started
		// run 2194's serial skip in the first place.
		const results = mutateResults('timed-out-then-passed.json', (report) => {
			const test = report.suites[0].specs[1].tests[0];
			test.results = [
				{ ...test.results[0], status: 'timedOut', retry: 0 },
				{ ...test.results[0], status: 'passed', retry: 1 }
			];
			// No `status` field, so the classification comes from the attempts alone.
			delete test.status;
		});
		const out = join(work, 'timed-out-then-passed.md');
		assert.equal(run(summaryScript, summaryArgs(results, out)).code, 0);
		assert.equal(totalsFrom(readFileSync(out, 'utf8')).flaky, 1);
	});

	test('two result files merge into one section with a run breakdown', () => {
		const first = join(work, 'e2e-stack-free.json');
		const second = join(work, 'e2e-stack.json');
		cpSync(fixtureResults, first);
		cpSync(fixtureResults, second);
		const out = join(work, 'merged.md');

		const { code } = run(summaryScript, ['--results', first, '--results', second, '--out', out]);
		assert.equal(code, 0);

		const body = readFileSync(out, 'utf8');
		assert.match(body, /#### Runs/);
		assert.match(body, /`stack-free`/);
		assert.match(body, /`stack`/);
		// Totals double, because the same run was summarised twice on purpose.
		assert.match(body, /\| Tests \| 6 \|/);
	});

	test('a missing report file is an error, not an empty summary', () => {
		const { code, out } = run(summaryScript, summaryArgs(join(work, 'nope.json'), join(work, 'x.md')));
		assert.equal(code, 1, out);
		assert.match(out, /no Playwright JSON report/);
	});

	test('a run that executed nothing says so instead of reading as green', () => {
		const results = mutateResults('empty.json', (report) => {
			report.suites = [];
			report.errors = [{ message: 'Error: No tests found.' }];
			report.stats = {
				startTime: '2026-11-20T00:00:00.000Z',
				duration: 0,
				expected: 0,
				skipped: 0,
				unexpected: 0,
				flaky: 0
			};
		});
		const out = join(work, 'empty.md');

		const { code, out: log } = run(summaryScript, summaryArgs(results, out));
		assert.equal(code, 0, log);
		assert.match(log, /NO TESTS/);

		const body = readFileSync(out, 'utf8');
		assert.match(body, /No tests ran\./);
	});
});

describe('check-e2e-manifest', () => {
	test('the fixture manifest is clean', () => {
		const { code, out } = run(checkScript, ['--manifest', join(fixtureTree, 'tests', 'e2e-manifest.json')]);
		assert.equal(code, 0, out);
		assert.match(out, /clean \(2 spec file\(s\)\)/);
	});

	test('a spec file no run claims fails — the encounters.e2e.ts defect', () => {
		const project = copyProject('unclaimed');
		writeFileSync(project.spec('three.e2e.ts'), '// orphan spec\nexport const spec = 3;\n');

		const { code, out } = run(checkScript, ['--manifest', project.manifest]);
		assert.equal(code, 1, out);
		assert.match(out, /tests\/three\.e2e\.ts exists but no run in the manifest claims it/);
	});

	test('a claimed spec missing from disk fails', () => {
		const project = copyProject('missing-on-disk');
		rmSync(project.spec('two.e2e.ts'));

		const { code, out } = run(checkScript, ['--manifest', project.manifest]);
		assert.equal(code, 1, out);
		assert.match(out, /tests\/two\.e2e\.ts is listed in run main but does not exist on disk/);
	});

	test('a claimed spec that produced no result fails', () => {
		const project = copyProject('no-result');
		const results = mutateResults('missing-spec.json', (report) => {
			report.suites = report.suites.filter((suite) => suite.file !== 'tests/two.e2e.ts');
		});

		const { code, out } = run(checkScript, [
			'--manifest',
			project.manifest,
			'--run',
			'main',
			'--results',
			results
		]);
		assert.equal(code, 1, out);
		assert.match(out, /tests\/two\.e2e\.ts is claimed by run main but produced no result/);
	});

	test('a spec that ran but is not claimed by the run fails', () => {
		const project = copyProject('unexpected');
		const results = mutateResults('unexpected-spec.json', (report) => {
			report.suites.push({
				title: 'three.e2e.ts',
				file: 'tests/three.e2e.ts',
				column: 0,
				line: 1,
				specs: [],
				suites: []
			});
		});

		const { code, out } = run(checkScript, [
			'--manifest',
			project.manifest,
			'--run',
			'main',
			'--results',
			results
		]);
		assert.equal(code, 1, out);
		assert.match(out, /tests\/three\.e2e\.ts ran but is not claimed by run main/);
	});

	test('a skip with no allow-listed reason fails; the allow-listed one passes', () => {
		const project = copyProject('skips');

		const unjustified = mutateResults('skip-unjustified.json', (report) => {
			const test = report.suites[0].specs[0].tests[0];
			test.status = 'skipped';
			test.results = [{ workerIndex: 0, status: 'skipped', duration: 0, errors: [], attachments: [] }];
			test.annotations = [{ type: 'skip', description: 'because' }];
		});

		const failing = run(checkScript, ['--manifest', project.manifest, '--run', 'main', '--results', unjustified]);
		assert.equal(failing.code, 1, failing.out);
		assert.match(failing.out, /was skipped without an allow-listed reason: "because"/);

		const justified = mutateResults('skip-allowed.json', (report) => {
			const test = report.suites[0].specs[0].tests[0];
			test.status = 'skipped';
			test.results = [{ workerIndex: 0, status: 'skipped', duration: 0, errors: [], attachments: [] }];
			test.annotations = [
				{ type: 'skip', description: 'An earlier run emptied both authored chests; start the stack with --fresh.' }
			];
		});

		const passing = run(checkScript, ['--manifest', project.manifest, '--run', 'main', '--results', justified]);
		assert.equal(passing.code, 0, passing.out);
	});

	test('failures alone never fail the gate — pass/fail is the job exit code', () => {
		const project = copyProject('failures-are-not-the-gates-business');
		const { code, out } = run(checkScript, [
			'--manifest',
			project.manifest,
			'--run',
			'main',
			'--results',
			fixtureResults
		]);
		assert.equal(code, 0, out);
	});

	test('a run below its baseline floor fails', () => {
		const project = copyProject('floor');
		const baseline = join(project.root, 'tests', 'e2e-baseline.json');
		writeFileSync(baseline, JSON.stringify({ runs: { main: { tests: 10 } } }), 'utf8');

		const { code, out } = run(checkScript, [
			'--manifest',
			project.manifest,
			'--baseline',
			baseline,
			'--run',
			'main',
			'--results',
			fixtureResults
		]);
		assert.equal(code, 1, out);
		assert.match(out, /ran 3 test\(s\), below its baseline floor of 10/);
	});

	test('the real manifest claims every spec on disk exactly once', () => {
		const manifest = join(repoRoot, 'web', 'client', 'tests', 'e2e-manifest.json');
		assert.ok(existsSync(manifest), 'web/client/tests/e2e-manifest.json should exist');

		const { code, out } = run(checkScript, ['--manifest', manifest]);
		assert.equal(code, 0, out);
	});
});

describe('run-e2e', () => {
	const runner = join(repoRoot, 'web', 'client', 'tests', 'run-e2e.mjs');

	test('--list prints the manifest spec list, not a hand-copied one', () => {
		const { code, out } = run(runner, ['stack', '--list']);
		assert.equal(code, 0, out);
		assert.match(out, /tests\/encounters\.e2e\.ts/);
		assert.match(out, /tests\/admin-console\.e2e\.ts/);
		// The stack run must not claim the stack-free specs; that is what the split is for.
		assert.doesNotMatch(out, /tests\/home\.e2e\.ts/);
	});

	test('an unknown run name is a usage error, distinct from a test failure', () => {
		const { code, out } = run(runner, ['nope']);
		assert.equal(code, 2, out);
		assert.match(out, /no run named "nope"/);
	});

	test('a run whose gates are unmet refuses to produce an all-skipped report', () => {
		const env = { ...process.env };
		delete env.E2E_STACK;
		delete env.E2E_GAME;
		const result = spawnSync(process.execPath, [runner, 'stack'], { cwd: repoRoot, encoding: 'utf8', env });
		assert.equal(result.status, 2);
		assert.match(`${result.stdout}${result.stderr}`, /refusing to produce an all-skipped report/i);
	});

	test('--only restricts the run to one of its own specs', () => {
		const { code, out } = run(runner, ['stack', '--list', '--only', 'tests/encounters.e2e.ts']);
		assert.equal(code, 0, out);
		assert.equal(out.trim(), 'tests/encounters.e2e.ts');
	});

	test('--only refuses a spec that belongs to another run', () => {
		const { code, out } = run(runner, ['stack', '--only', 'tests/home.e2e.ts']);
		assert.equal(code, 2, out);
		assert.match(out, /is not part of run "stack"/);
	});
});

// Keep the temp dir used by summary tests referenced so a failure names the path it wrote to.
mkdirSync(work, { recursive: true });
