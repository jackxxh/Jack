import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { validateProfile, loadProfile, probeProfile, createBinding, verifyBinding } from './profile-lib.mjs';

const local = path.resolve('.local');
fs.mkdirSync(local, { recursive: true });
const root = fs.mkdtempSync(path.join(local, 'profile-tests-'));
const file = path.join(root, 'profile.json');
const profile = { id: 'synthetic', platform: 'windows', software: Object.fromEntries(['kukaSim', 'officeLite', 'workVisual'].map(name => [name, { enabled: true, version: 'synthetic', pathKey: 'root' }])), paths: { root: '.' } };
fs.writeFileSync(file, JSON.stringify(profile));
test.after(() => { if (!root.startsWith(local + path.sep)) throw new Error('Unsafe cleanup path'); fs.rmSync(root, { recursive: true, force: true }); });

test('profile validator rejects unknown fields and unresolved paths', () => {
  assert.deepEqual(validateProfile(profile), []);
  assert.ok(validateProfile({ ...profile, unexpected: true }).some(x => x.includes('unknown')));
  assert.ok(validateProfile({ ...profile, paths: {} }).some(x => x.includes('does not resolve')));
});

test('presence probe never attests vendor execution or observed version', () => {
  const probe = probeProfile(loadProfile(file));
  assert.equal(probe.scope, 'filesystem-presence-only');
  for (const check of probe.checks) {
    assert.equal(check.status, 'Unverified');
    assert.equal(check.executionVerified, false);
    assert.equal(check.observedVersion, null);
  }
});

test('binding detects receipt and profile tampering', () => {
  const snapshot = loadProfile(file);
  const receipt = Buffer.from('{"synthetic":true}');
  const binding = createBinding(snapshot, probeProfile(snapshot), receipt, ['synthetic'], 0);
  assert.equal(verifyBinding(binding, snapshot.bytes, receipt).succeeded, true);
  assert.equal(verifyBinding(binding, Buffer.from('{}'), receipt).succeeded, false);
  assert.equal(verifyBinding(binding, snapshot.bytes, Buffer.from('{}')).succeeded, false);
  binding.payload.exitCode = 1;
  assert.equal(verifyBinding(binding, snapshot.bytes, receipt).succeeded, false);
});

test('three configured adapters produce all seven non-empty combinations', () => {
  const result = spawnSync(process.execPath, ['scripts/profile-capability-matrix.mjs', file], { encoding: 'utf8' });
  assert.equal(result.status, 0, result.stderr);
  const matrix = JSON.parse(result.stdout);
  assert.equal(matrix.combinations.length, 7);
  assert.deepEqual(matrix.combinations.map(x => x.adapters.length).sort(), [1, 1, 1, 2, 2, 2, 3]);
  assert.ok(matrix.combinations.every(x => x.executionVerified === false));
});

test('generated MCP server exposes probe, plan and guarded run tools', async () => {
  const { createKukaLabServer } = await import('../plugins/kuka-virtual-validation/mcp-server/src/server.mjs');
  const server = createKukaLabServer();
  for (const name of ['kuka_lab_profile_probe', 'kuka_lab_profile_plan', 'kuka_lab_profile_run']) assert.ok(server._registeredTools[name]);
});
