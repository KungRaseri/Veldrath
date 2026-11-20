#!/usr/bin/env node
/**
 * Enforce the e2e contract: every spec runs somewhere, and nothing runs that nothing declared.
 *
 * The coverage gate exists because a number can drop silently. For e2e the equivalent silent
 * failure is a spec that stops running: `tests/encounters.e2e.ts` was added to the local stack
 * script and never to the CI list, so the combat and death journey was green locally and absent
 * from every pull request. Pass/fail needs no gate — it is the job's own exit code — so this
 * checks only the things that do not fail a suite:
 *
 *   1. every `*.e2e.ts` on disk is claimed by exactly one run in the manifest;
 *   2. every spec a run claims exists on disk;
 *   3. for a run with results, the observed spec files equal the claimed ones — a spec that
 *      produced no result (crash, typo, filter) is a failure, not an absence;
 *   4. every skipped test is justified: its message matches an allow-listed reason, or the run
 *      is being executed without one of its declared gates (then the skip is the expected shape);
 *   5. the run does not shrink below its recorded floor in the baseline (ratchet, like coverage).
 *
 * Usage:
 *   node scripts/check-e2e-manifest.mjs [--manifest web/client/tests/e2e-manifest.json]
 *       [--run stack --results web/client/test-results/e2e-stack.json]
 *       [--baseline web/client/tests/e2e-baseline.json] [--suggest]
 *
 * Exit codes: 0 clean, 1 a contract violation, 2 a usage or parse error.
 */
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { basename, dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));

function arg(name, fallback) {
	const index = process.argv.indexOf(name);
	return index === -1 ? fallback : process.argv[index + 1];
}

function die(message, code) {
	console.error(`error: ${message}`);
	process.exit(code);
}

// Defaults are resolved from the script, not the working directory: the same command has to work
// from the repo root (CI), from `web/` and from `web/client/` (an npm script), and
// `scripts/coverage-floors.json` is looked up the same way.
const manifestPath = resolve(
	arg('--manifest', resolve(here, '..', 'web', 'client', 'tests', 'e2e-manifest.json'))
);
const baselinePath = resolve(
	arg('--baseline', resolve(here, '..', 'web', 'client', 'tests', 'e2e-baseline.json'))
);
const runName = arg('--run', '');
const resultsPath = arg('--results', '');
const suggest = process.argv.includes('--suggest');

if (!existsSync(manifestPath)) die(`no manifest at ${manifestPath}`, 2);

let manifest;
try {
	manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
} catch (error) {
	die(`${manifestPath} is not valid JSON: ${error.message}`, 2);
}

const clientRoot = resolve(dirname(manifestPath), '..');
const specRoot = resolve(clientRoot, manifest.specRoot ?? 'tests');
const runs = manifest.runs ?? {};
const failures = [];
const notes = [];

/** Walk the spec root for `*.e2e.ts`, skipping fixtures and helpers under `support/`. */
function discoverSpecs(dir) {
	const found = [];
	for (const entry of readdirSync(dir, { withFileTypes: true })) {
		const path = join(dir, entry.name);
		if (entry.isDirectory()) {
			if (entry.name === 'support') continue;
			found.push(...discoverSpecs(path));
		} else if (entry.name.endsWith('.e2e.ts')) {
			found.push(relative(clientRoot, path).split('\\').join('/'));
		}
	}
	return found;
}

function normalise(specPath) {
	return String(specPath ?? '')
		.replace(/\\/g, '/')
		.replace(/^\.\//, '');
}

// ---------------------------------------------------------------- 1 + 2: the manifest itself

const onDisk = discoverSpecs(specRoot);
const claims = new Map();
for (const [runNameKey, run] of Object.entries(runs)) {
	for (const spec of run.specs ?? []) {
		const key = normalise(spec);
		if (!claims.has(key)) claims.set(key, []);
		claims.get(key).push(runNameKey);
	}
}

for (const spec of onDisk) {
	const owners = claims.get(spec) ?? [];
	if (owners.length === 0) {
		failures.push(`${spec} exists but no run in the manifest claims it — it never runs in CI (this is how encounters.e2e.ts went missing)`);
	} else if (owners.length > 1) {
		failures.push(`${spec} is claimed by ${owners.length} runs (${owners.join(', ')}) — a spec runs in one place, or its results are counted twice`);
	}
}

for (const [spec, owners] of claims) {
	if (!existsSync(resolve(clientRoot, spec))) {
		failures.push(`${spec} is listed in run ${owners.join(', ')} but does not exist on disk`);
	}
	if (onDisk.includes(spec) === false && existsSync(resolve(clientRoot, spec))) {
		// exists but was not discovered (wrong suffix) — worth saying out loud
		notes.push(`${spec} is claimed but not discovered as *.e2e.ts under ${relative(clientRoot, specRoot)}`);
	}
}

// allowedSkips may only reference specs that exist, or the allow-list decays into noise.
for (const entry of manifest.allowedSkips ?? []) {
	if (entry.spec && !existsSync(resolve(clientRoot, normalise(entry.spec)))) {
		notes.push(`allowedSkips names ${entry.spec}, which does not exist — stale allow-list entry`);
	}
}

// ---------------------------------------------------------------- 3 + 4 + 5: one run's results

/**
 * Flatten the report into `{ file, spec }` entries.
 *
 * The suite itself is recorded too, not only its specs: a file that failed to load — a syntax
 * error, a missing import — produces a suite with zero specs, and only the suite carries the
 * file name. Collecting just specs would report that file as "produced no result" and hide the
 * reason, or worse, silently accept a run that skipped it entirely.
 */
function collectSpecs(suites, file, out) {
	for (const suite of suites ?? []) {
		const suiteFile = suite.file ?? file;
		if (suiteFile) out.push({ file: suiteFile, spec: null });
		for (const spec of suite.specs ?? []) out.push({ file: spec.file ?? suiteFile, spec });
		collectSpecs(suite.suites, suiteFile, out);
	}
	return out;
}

function observedFiles(entries) {
	return new Set(entries.map((entry) => normalise(entry.file)).filter(Boolean));
}

function isSkipped(test) {
	if (test.status === 'skipped' || test.expectedStatus === 'skipped') return true;
	const last = (test.results ?? [])[(test.results ?? []).length - 1];
	return last?.status === 'skipped';
}

function skipMessage(test) {
	const annotation = (test.annotations ?? []).find((a) => a.type === 'skip');
	return String(annotation?.description ?? '');
}

if (resultsPath) {
	if (!runName) die('--results needs --run', 2);
	const run = runs[runName];
	if (!run) die(`no run named "${runName}" in ${manifestPath}`, 2);

	const absolute = resolve(resultsPath);
	if (!existsSync(absolute)) {
		die(`${absolute} does not exist — the run produced no JSON report, so nothing can be verified`, 1);
	}

	let report;
	try {
		report = JSON.parse(readFileSync(absolute, 'utf8'));
	} catch (error) {
		die(`${absolute} is not valid JSON: ${error.message}`, 2);
	}

	const observed = collectSpecs(report.suites, undefined, []);
	const seen = observedFiles(observed);
	const claimed = new Set((run.specs ?? []).map(normalise));

	for (const spec of claimed) {
		if (!seen.has(spec)) {
			const reason = report.errors?.length
				? ` — the run reported: ${String(report.errors[0].message ?? '').split('\n')[0]}`
				: '';
			failures.push(`${spec} is claimed by run ${runName} but produced no result${reason}`);
		}
	}
	for (const spec of seen) {
		if (!claimed.has(spec)) {
			failures.push(`${spec} ran but is not claimed by run ${runName} — declare it in the manifest (or remove it from the invocation)`);
		}
	}

	const gatesSatisfied = (run.requires ?? []).every((gate) => process.env[gate] === '1');
	let skipped = 0;
	for (const { file, spec } of observed) {
		if (!spec) continue;
		for (const test of spec.tests ?? []) {
			if (!isSkipped(test)) continue;
			skipped += 1;
			const message = skipMessage(test);
			const specPath = normalise(file);

			// Skipping because a declared gate is off is the expected shape of a deliberate run.
			const gateJustifies = !gatesSatisfied && /E2E_(STACK|GAME)|needs a running|isolated compose stack/i.test(message);
			const allowed = (manifest.allowedSkips ?? []).some(
				(entry) => normalise(entry.spec) === specPath && (!entry.contains || message.includes(entry.contains))
			);
			if (!gateJustifies && !allowed) {
				failures.push(`${specPath} › ${spec.title} was skipped without an allow-listed reason: "${message || 'no reason given'}"`);
			}
		}
	}

	if (suggest) {
		notes.push(
			`run ${runName}: ${claimed.size} spec(s), ${observed.reduce((n, o) => n + (o.spec?.tests ?? []).length, 0)} test(s), ${skipped} skipped`
		);
	}
}

// ---------------------------------------------------------------- 5: the ratchet

let baseline = null;
if (existsSync(baselinePath)) {
	try {
		baseline = JSON.parse(readFileSync(baselinePath, 'utf8'));
	} catch (error) {
		die(`${baselinePath} is not valid JSON: ${error.message}`, 2);
	}
} else if (suggest) {
	notes.push(`no baseline at ${baselinePath} — the first green run should record its counts there`);
}

if (baseline && resultsPath) {
	const observedTests = (() => {
		const report = JSON.parse(readFileSync(resolve(resultsPath), 'utf8'));
		let count = 0;
		const walk = (suites) => {
			for (const suite of suites ?? []) {
				for (const spec of suite.specs ?? []) count += (spec.tests ?? []).length;
				walk(suite.suites);
			}
		};
		walk(report.suites);
		return count;
	})();

	const floor = baseline.runs?.[runName]?.tests;
	if (floor == null) {
		notes.push(`run ${runName} has no baseline floor; observed ${observedTests} test(s) — record {"runs":{"${runName}":{"tests":${observedTests}}}} to start the ratchet`);
	} else if (observedTests < floor) {
		failures.push(`run ${runName} ran ${observedTests} test(s), below its baseline floor of ${floor} — tests disappeared without an explicit decision`);
	} else if (suggest && observedTests > floor) {
		notes.push(`run ${runName} ran ${observedTests} test(s), above its floor of ${floor} — raise the floor to ${observedTests}`);
	}
}

// ---------------------------------------------------------------- output

if (notes.length > 0) {
	for (const note of notes) console.log(`note: ${note}`);
}

if (failures.length > 0) {
	console.error(`e2e manifest gate: ${failures.length} problem(s)`);
	for (const failure of failures) console.error(`  - ${failure}`);
	process.exit(1);
}

const checked = resultsPath ? `run ${runName} against ${basename(resultsPath)}` : `${onDisk.length} spec file(s)`;
console.log(`e2e manifest gate: clean (${checked})`);
