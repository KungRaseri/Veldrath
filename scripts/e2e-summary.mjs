#!/usr/bin/env node
/**
 * Render Playwright's JSON report into a Markdown summary for the pull request.
 *
 * The counterpart of `scripts/coverage-report.sh` for the e2e half of CI: coverage has a
 * rendered summary that a sticky comment can carry, and Playwright had nothing but console
 * output. This reads the `json` reporter's output (see `web/client/playwright.config.ts`) and
 * writes the same kind of Markdown the coverage reporter produces, so
 * `scripts/forgejo-pr-comment.sh --marker '<!-- ci-e2e -->' --section …` can post it.
 *
 * One invocation may take several result files — the stack run and the outage run are two
 * Playwright invocations in one job — and they are merged into a single section, because a
 * pull request wants one summary, not one per command.
 *
 * Usage:
 *   node scripts/e2e-summary.mjs --results web/client/test-results/e2e-stack.json \
 *       [--results …] [--out E2EReport/Summary.md] [--run stack]
 *
 * Exit codes: 0 when the summary was written, 1 when a result file is missing or unreadable,
 * 2 for a usage error. It never fails on the *content* of a run: a red run still produces its
 * summary, which is the whole point — a report that only exists when everything passed is
 * missing exactly when it is read.
 */
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { basename, dirname, resolve } from 'node:path';

function arg(name, fallback) {
	const index = process.argv.indexOf(name);
	return index === -1 ? fallback : process.argv[index + 1];
}

function allArgs(name) {
	const out = [];
	for (let index = 0; index < process.argv.length; index += 1) {
		if (process.argv[index] === name && process.argv[index + 1]) out.push(process.argv[index + 1]);
	}
	return out;
}

function die(message, code = 2) {
	console.error(`error: ${message}`);
	process.exit(code);
}

const outPath = resolve(arg('--out', 'E2EReport/Summary.md'));
const explicitRun = arg('--run', '');
const resultArgs = allArgs('--results');
const dirs = allArgs('--results-dir');

for (const dir of dirs) {
	if (!existsSync(dir)) continue;
	for (const entry of readdirSync(dir)) {
		if (/^e2e-.*\.json$/.test(entry)) resultArgs.push(resolve(dir, entry));
	}
}

if (resultArgs.length === 0) {
	die('no results: pass --results <json> (repeatable) or --results-dir <dir>');
}

const files = resultArgs.map((file) => resolve(file));
const missing = files.filter((file) => !existsSync(file));
if (missing.length > 0) {
	die(`no Playwright JSON report at ${missing.join(', ')} — did the run reach the reporters step?`, 1);
}

const ANSI = /\u001b\[[0-9;]*m/g;

function clean(text) {
	return String(text ?? '')
		.replace(ANSI, '')
		.replace(/\r/g, '')
		.trim();
}

/** The first line of an error message is the headline; the rest is stack noise in a summary. */
function headline(message) {
	const first = clean(message).split('\n').find((line) => line.trim().length > 0) ?? 'failed';
	return first.length > 220 ? `${first.slice(0, 217)}…` : first;
}

/**
 * Playwright nests suites. A spec's file lives on the nearest enclosing suite, and its real
 * name is every suite title from the root down plus its own title.
 */
function collectSpecs(suites, trail, file, out) {
	for (const suite of suites ?? []) {
		const suiteFile = suite.file ?? file;
		const nextTrail = suite.title ? [...trail, suite.title] : trail;
		for (const spec of suite.specs ?? []) {
			out.push({ file: spec.file ?? suiteFile, trail: nextTrail, spec });
		}
		collectSpecs(suite.suites, nextTrail, suiteFile, out);
	}
	return out;
}

/**
 * Result statuses that make an attempt a genuine failure. Playwright does not treat `skipped` or
 * `interrupted` as one: when a serial group's test fails, the rest of that group is marked
 * `skipped` and the whole group is retried, so `[skipped, passed]` is a test that passed — not a
 * flake. Classifying it as flaky is how the `<!-- ci-e2e -->` comment said 2 flaky where
 * Playwright's own tally said 1 (run 2194, `encounters.e2e.ts:134`).
 */
const FAILED_RESULT_STATUSES = new Set(['failed', 'timedOut']);

function outcome(test) {
	if (test.status === 'flaky') return 'flaky';
	if (test.status === 'unexpected') return 'failed';
	if (test.status === 'skipped') return 'skipped';
	if (test.expectedStatus === 'skipped') return 'skipped';

	const attempts = test.results ?? [];
	const last = attempts[attempts.length - 1];
	if (!last) return 'skipped';
	const failedAttempt = attempts.some((attempt) => FAILED_RESULT_STATUSES.has(attempt.status));
	if (last.status === 'passed') return failedAttempt ? 'flaky' : 'passed';
	return failedAttempt ? 'failed' : 'skipped';
}

function durationOf(test) {
	// Sum of every attempt, retries included — not wall clock. With several workers the per-spec
	// numbers still answer "which file is slow", but they add up to more than the run took.
	return (test.results ?? []).reduce((total, result) => total + (result.duration ?? 0), 0);
}

function skipReason(test) {
	const annotation = (test.annotations ?? []).find((a) => a.type === 'skip');
	return clean(annotation?.description) || 'skipped without a reason';
}

function traceOf(test) {
	for (const result of test.results ?? []) {
		for (const attachment of result.attachments ?? []) {
			if (/trace/i.test(attachment.name ?? '') || /trace/i.test(attachment.contentType ?? '')) {
				return attachment.path ?? attachment.name ?? '';
			}
		}
	}
	return '';
}

function runLabelFor(file) {
	const name = basename(file).replace(/^e2e-/, '').replace(/\.json$/, '');
	return name || basename(dirname(file));
}

function formatDuration(ms) {
	const total = Math.round(ms / 1000);
	if (total < 60) return `${total}s`;
	const minutes = Math.floor(total / 60);
	const seconds = total % 60;
	return `${minutes}m ${String(seconds).padStart(2, '0')}s`;
}

const rows = [];
const failures = [];
const skips = [];
let totals = { specs: 0, tests: 0, passed: 0, failed: 0, flaky: 0, skipped: 0, duration: 0 };
const perRun = [];

for (const file of files) {
	let report;
	try {
		report = JSON.parse(readFileSync(file, 'utf8'));
	} catch (error) {
		die(`${file} is not valid JSON: ${error.message}`, 1);
	}

	const runLabel = explicitRun || runLabelFor(file);
	const specs = collectSpecs(report.suites, [], undefined, []);
	const counts = { specs: 0, tests: 0, passed: 0, failed: 0, flaky: 0, skipped: 0, duration: 0 };
	const fileStats = new Map();

	for (const { file: specFile, trail, spec } of specs) {
		const tests = spec.tests ?? [];
		// Rows are per spec *file*, not per describe block: the header says "Specs" and the
		// question a reader asks is "which file is slow or failing", so several tests from one
		// file are summed into one row.
		const key = specFile ?? '(unknown file)';
		const stat = fileStats.get(key) ?? { passed: 0, failed: 0, flaky: 0, skipped: 0, duration: 0, tests: 0 };
		fileStats.set(key, stat);
		for (const test of tests) {
			const state = outcome(test);
			stat[state] += 1;
			stat.duration += durationOf(test);
			stat.tests += 1;
			counts.tests += 1;
			counts[state] += 1;
			counts.duration += durationOf(test);

			const name = [...trail, spec.title].filter(Boolean).join(' › ');
			if (state === 'failed') {
				const message = (test.results ?? []).map((r) => r.error?.message).filter(Boolean).pop();
				failures.push({ run: runLabel, name, message: headline(message), trace: traceOf(test), file: specFile });
			}
			if (state === 'flaky') {
				failures.push({
					run: runLabel,
					name,
					message: `passed on retry (${(test.results ?? []).length} attempts)`,
					trace: traceOf(test),
					file: specFile,
					flaky: true
				});
			}
			if (state === 'skipped') {
				skips.push({ run: runLabel, name, reason: skipReason(test) });
			}
		}

	}

	counts.specs = fileStats.size;
	for (const [file, stat] of fileStats) rows.push({ run: runLabel, file, counts: stat, total: stat.tests });

	perRun.push({ run: runLabel, counts });
	for (const key of Object.keys(totals)) totals[key] += counts[key];
}

const sha = process.env.GITHUB_SHA ?? process.env.CI_COMMIT_SHA ?? '';
const ref = process.env.GITHUB_REF_NAME ?? process.env.GITHUB_HEAD_REF ?? '';

const lines = [];
lines.push(`**${perRun.map((r) => `\`${r.run}\``).join(', ')}** · generated ${new Date().toISOString().replace('T', ' ').slice(0, 16)} UTC`);
if (sha || ref) {
	lines.push('');
	lines.push(`Commit \`${sha.slice(0, 7) || 'unknown'}\` on \`${ref || 'unknown'}\` — a summary from a reused preview or a stale artifact can be spotted here.`);
}
if (totals.tests === 0) {
	// A summary that says "0/0 passed" is the coverage-gate failure mode in test form: green by
	// measuring nothing. Say so in the body, because this is what a wrongly filtered run looks like.
	lines.push('');
	lines.push('> **No tests ran.** The filter matched nothing, or the run stopped before a test started — read the job log, not this summary.');
}

lines.push('');
lines.push('| | |');
lines.push('|---|---|');
lines.push(`| Specs | ${totals.specs} |`);
lines.push(`| Tests | ${totals.tests} |`);
lines.push(`| Passed | ${totals.passed} |`);
lines.push(`| Failed | ${totals.failed} |`);
lines.push(`| Flaky | ${totals.flaky} |`);
lines.push(`| Skipped | ${totals.skipped} |`);
lines.push(`| Duration | ${formatDuration(totals.duration)} |`);
lines.push('');
lines.push(
	'_Duration is the sum of test attempts (retries included) across every worker, not the wall clock of the run — that is why it can exceed the time the job took._'
);

if (perRun.length > 1) {
	lines.push('');
	lines.push('#### Runs');
	lines.push('');
	lines.push('| Run | Tests | Passed | Failed | Flaky | Skipped | Duration |');
	lines.push('|---|---|---|---|---|---|---|');
	for (const { run, counts } of perRun) {
		lines.push(
			`| \`${run}\` | ${counts.tests} | ${counts.passed} | ${counts.failed} | ${counts.flaky} | ${counts.skipped} | ${formatDuration(counts.duration)} |`
		);
	}
}

if (rows.length > 0) {
	lines.push('');
	lines.push('#### Specs, slowest first');
	lines.push('');
	lines.push('| Spec | Tests | Passed | Failed | Flaky | Skipped | Duration |');
	lines.push('|---|---|---|---|---|---|---|');
	const sorted = [...rows].sort((a, b) => b.counts.duration - a.counts.duration);
	for (const row of sorted) {
		const name = row.file ? `\`${row.file}\`` : '`unknown`';
		lines.push(
			`| ${name} | ${row.total} | ${row.counts.passed} | ${row.counts.failed} | ${row.counts.flaky} | ${row.counts.skipped} | ${formatDuration(row.counts.duration)} |`
		);
	}
}

if (failures.length > 0) {
	lines.push('');
	lines.push('#### Not green');
	lines.push('');
	for (const failure of failures) {
		const marker = failure.flaky ? 'flaky' : 'failed';
		lines.push(`- **${failure.name}** — ${marker}: \`${failure.message}\``);
		if (failure.trace) lines.push(`  - trace: \`${failure.trace}\``);
	}
}

if (skips.length > 0) {
	lines.push('');
	lines.push('#### Skipped');
	lines.push('');
	for (const skip of skips) {
		lines.push(`- ${skip.name} — ${skip.reason}`);
	}
}

lines.push('');

mkdirSync(dirname(outPath), { recursive: true });
writeFileSync(outPath, `${lines.join('\n')}`, 'utf8');

const verdict = totals.tests === 0 ? 'NO TESTS' : totals.failed > 0 ? 'FAILED' : 'PASSED';
console.log(
	`e2e summary: ${verdict} — ${totals.passed}/${totals.tests} passed, ${totals.failed} failed, ${totals.flaky} flaky, ${totals.skipped} skipped across ${totals.specs} spec(s)`
);
console.log(`written to ${outPath}`);
