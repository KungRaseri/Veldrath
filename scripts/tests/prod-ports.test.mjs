#!/usr/bin/env node
/**
 * Tests for scripts/check-prod-ports.mjs — the gate that keeps production from publishing a
 * host port.
 *
 * The gate's whole job is to disagree with a plausible-looking compose file, so the cases are
 * mostly negative controls: an overlay that says `ports: []` must FAIL, only `ports: !reset []`
 * may pass. The last test renders the real files with Docker Compose when it is available and
 * asserts that the static merge and Compose's own verdict name the same ports — the same
 * check that caught the original bug (`ports: []` still publishing six ports).
 *
 * Run: node --test scripts/tests/prod-ports.test.mjs
 */

import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

import { findingsFor, mergePublications, readServicePublications } from '../check-prod-ports.mjs';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const script = join(repoRoot, 'scripts/check-prod-ports.mjs');

const SERVICES = ['discord', 'grafana', 'postgres', 'prometheus', 'seq', 'server', 'web'];

/** Fixture directories are removed when this process exits. */
const tempDirs = [];
process.on('exit', () => {
	for (const dir of tempDirs) rmSync(dir, { recursive: true, force: true });
});

/** A temporary directory that is cleaned up on exit. */
function tempDir(prefix) {
	const dir = mkdtempSync(join(tmpdir(), prefix));
	tempDirs.push(dir);
	return dir;
}

/** Runs the CLI and returns its status plus output. */
function run(args, options = {}) {
	const result = spawnSync(process.execPath, [script, ...args], {
		cwd: options.cwd ?? repoRoot,
		encoding: 'utf8'
	});
	return { status: result.status, stdout: result.stdout, stderr: result.stderr };
}

/** Writes a fixture pair into a fresh directory and returns its paths. */
function fixture(services, overlayServices) {
	const dir = tempDir('prod-ports-');
	const base = join(dir, 'base.yml');
	const overlay = join(dir, 'overlay.yml');
	writeFileSync(base, services);
	writeFileSync(overlay, overlayServices);
	return { base, overlay };
}

/** A base file that defines all seven production services, with `ports` spliced in. */
function baseWith(servicePorts) {
	const blocks = SERVICES.map((service) => {
		const ports = servicePorts[service];
		const lines = [`  ${service}:`, `    image: example/${service}:latest`];
		if (ports) lines.push(...ports.map((line) => `    ${line}`));
		else lines.push('    restart: unless-stopped');
		return lines.join('\n');
	});
	return `services:\n${blocks.join('\n')}\n`;
}

/** An overlay that restarts every service and may reset some ports. */
function overlayWith(servicePorts) {
	const blocks = SERVICES.map((service) => {
		const lines = [`  ${service}:`];
		if (servicePorts[service]) lines.push(...servicePorts[service].map((line) => `    ${line}`));
		lines.push('    restart: unless-stopped');
		return lines.join('\n');
	});
	return `services:\n${blocks.join('\n')}\n`;
}

/** A rendered `config --format json` services map with every expected service present. */
function renderedServices(overrides = {}) {
	const services = {};
	for (const service of SERVICES) services[service] = overrides[service] ?? { expose: [] };
	return services;
}

function check(files) {
	return run(['--base', files.base, '--overlay', files.overlay, '--no-extra']);
}

test('the checked-in production stack publishes nothing', () => {
	const result = run([]);
	assert.equal(result.status, 0, result.stderr);
	assert.match(result.stdout, /0 host ports published/);
});

test('an inherited publication survives `ports: []` and fails the gate', () => {
	const files = fixture(
		baseWith({ server: ['ports:', '  - "9000:8080"'] }),
		overlayWith({ server: ['ports: []'] })
	);
	const result = check(files);
	assert.equal(result.status, 1);
	assert.match(result.stderr, /server: publishes host port 9000/);
	assert.match(result.stderr, /inherited from .*base\.yml/);
});

test('`ports: !reset []` is the value that drops an inherited publication', () => {
	const files = fixture(
		baseWith({ server: ['ports:', '  - "9000:8080"'] }),
		overlayWith({ server: ['ports: !reset []'] })
	);
	const result = check(files);
	assert.equal(result.status, 0, result.stderr);
});

test('a bare container port is a publication, not a private port', () => {
	const files = fixture(
		baseWith({ web: ['ports:', '  - "8081"'] }),
		overlayWith({ web: ['ports: !reset []'] })
	);
	// Even reset in the overlay, the base file still lists `ports:` as a publication; the
	// interesting case is the overlay-less read of that file.
	const services = readServicePublications(baseWith({ web: ['ports:', '  - "8081"'] }));
	assert.deepEqual(services.get('web').publications[0].published, ['random']);
	assert.equal(check(files).status, 0, 'reset clears it, so the pair passes');
});

test('long syntax is read as a publication', () => {
	const base = baseWith({
		postgres: ['ports:', '  - target: 5432', '    published: "5433"', '    protocol: tcp']
	});
	const files = fixture(base, overlayWith({}));
	const result = check(files);
	assert.equal(result.status, 1);
	assert.match(result.stderr, /postgres: publishes host port 5433/);

	const services = readServicePublications(base);
	assert.deepEqual(services.get('postgres').publications[0].published, ['5433']);
	assert.equal(services.get('postgres').publications[0].target, '5432');
});

test('a port added by the overlay is reported against the overlay', () => {
	const files = fixture(
		baseWith({}),
		overlayWith({ grafana: ['ports:', '  - "9002:3000"'] })
	);
	const result = check(files);
	assert.equal(result.status, 1);
	assert.match(result.stderr, /grafana: publishes host port 9002 \(.*overlay\.yml\)/);
});

test('network_mode: host publishes everything and fails', () => {
	const files = fixture(
		baseWith({}),
		overlayWith({ seq: ['network_mode: host'] })
	);
	const result = check(files);
	assert.equal(result.status, 1);
	assert.match(result.stderr, /seq: network_mode: host exposes every container port/);
});

test('a port range is expanded, so one allowed member cannot hide the rest', () => {
	const services = readServicePublications(
		baseWith({ postgres: ['ports:', '  - "9000-9002:5432"'] })
	);
	assert.deepEqual(services.get('postgres').publications[0].published, [
		'9000',
		'9001',
		'9002'
	]);
});

test('a lost service fails instead of passing quietly', () => {
	const base = 'services:\n  server:\n    image: example/server:latest\n';
	const overlay = 'services:\n  server:\n    restart: unless-stopped\n';
	const files = fixture(base, overlay);
	const result = check(files);
	assert.equal(result.status, 1);
	assert.match(result.stderr, /service is missing from the .*merge/);
});

test('the allowlist is the only way a publication passes, and it is per service', () => {
	const base = readServicePublications(baseWith({ postgres: ['ports:', '  - "5433:5432"'] }));
	const overlay = readServicePublications(overlayWith({}));
	const merged = mergePublications(base, overlay, 'base.yml', 'overlay.yml');

	assert.equal(findingsFor(merged, {}, 'overlay.yml').length, 1);
	assert.equal(findingsFor(merged, { postgres: [5433] }, 'overlay.yml').length, 0);
	assert.equal(
		findingsFor(merged, { server: [5433] }, 'overlay.yml').length,
		1,
		'an allowlist entry does not travel to another service'
	);
});

test('the render mode agrees with the static mode on a rendered config', () => {
	const dir = tempDir('prod-ports-render-');
	const clean = join(dir, 'clean.json');
	writeFileSync(clean, JSON.stringify({ services: renderedServices() }));
	assert.equal(run(['--render', clean]).status, 0);

	const dirty = join(dir, 'dirty.json');
	writeFileSync(
		dirty,
		JSON.stringify({
			services: renderedServices({
				postgres: { ports: [{ mode: 'ingress', target: 5432, published: '5433' }] }
			})
		})
	);
	const result = run(['--render', dirty]);
	assert.equal(result.status, 1);
	assert.match(result.stderr, /postgres: publishes host port 5433/);
});

test('the render mode fails when a rendered config lost a service', () => {
	const dir = tempDir('prod-ports-render-missing-');
	const services = renderedServices();
	delete services.web;
	const missing = join(dir, 'missing.json');
	writeFileSync(missing, JSON.stringify({ services }));
	const result = run(['--render', missing]);
	assert.equal(result.status, 1);
	assert.match(result.stderr, /web: service is missing from .*missing\.json/);
});

test('parity: Docker Compose renders the same publications the static merge computes', () => {
	const version = spawnSync('docker', ['compose', 'version'], { encoding: 'utf8' });
	if (version.status !== 0) {
		console.log('skip: docker compose is not available here');
		return;
	}

	const dir = tempDir('prod-ports-parity-');
	const render = (overlayPath, out) => {
		const result = spawnSync(
			'docker',
			[
				'compose',
				'-f',
				join(repoRoot, 'docker-compose.yml'),
				'-f',
				overlayPath,
				'config',
				'--format',
				'json'
			],
			{ cwd: repoRoot, encoding: 'utf8' }
		);
		assert.equal(result.status, 0, result.stderr);
		writeFileSync(out, result.stdout);
		return out;
	};

	const clean = render(join(repoRoot, 'docker-compose.prod.yml'), join(dir, 'clean.json'));
	assert.equal(run(['--render', clean]).status, 0, 'the checked-in overlay publishes nothing');

	// The negative control: the same overlay with `!reset []` reverted to `ports: []` and the
	// two observability services left alone — exactly the shape that shipped six published
	// ports. Compose must render six, and the static merge must report the same six.
	const mutated = join(dir, 'overlay-before.yml');
	writeFileSync(
		mutated,
		[
			'services:',
			'  server:',
			'    ports: []',
			'  web:',
			'    ports: []',
			'  postgres:',
			'    ports: []',
			'  seq:',
			'    restart: unless-stopped',
			'  prometheus:',
			'    restart: unless-stopped',
			'  grafana:',
			'    restart: unless-stopped',
			''
		].join('\n')
	);
	const dirty = render(mutated, join(dir, 'dirty.json'));
	const rendered = run(['--render', dirty]);
	assert.equal(rendered.status, 1);

	const publishedFromRender = new Set(
		[...rendered.stderr.matchAll(/publishes host port (\d+)/g)].map((match) => match[1])
	);
	const staticResult = run([
		'--base',
		'docker-compose.yml',
		'--overlay',
		mutated,
		'--no-extra'
	]);
	const publishedFromStatic = new Set(
		[...staticResult.stderr.matchAll(/publishes host port (\d+)/g)].map((match) => match[1])
	);
	assert.deepEqual(
		[...publishedFromStatic].sort(),
		[...publishedFromRender].sort(),
		'the static merge and Compose disagree'
	);
	assert.deepEqual([...publishedFromRender].sort(), [
		'5433',
		'8081',
		'9000',
		'9001',
		'9002',
		'9003'
	]);
});
