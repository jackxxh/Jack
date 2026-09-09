#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { loadProfile, probeProfile, createBinding } from './profile-lib.mjs';

const args = process.argv.slice(2);
const p = args.indexOf('--profile');
if (p < 0 || !args[p + 1]) { console.error('Usage: node scripts/kuka-profile-run.mjs --profile <profile.json> <kuka-lab CLI args...>'); process.exit(2); }
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
let snapshot;
try { snapshot = loadProfile(args[p + 1]); } catch (error) { console.error(`Profile invalid: ${error.message}`); process.exit(2); }
const probe = probeProfile(snapshot);
console.log(JSON.stringify(probe, null, 2));
if (probe.checks.some(check => check.configured && check.status !== 'Unverified')) { console.error('Profile capability probe did not pass. No CLI operation was started.'); process.exit(1); }
const forwarded = args.filter((_, i) => i !== p && i !== p + 1);
const cli = spawnSync('dotnet', ['run', '--project', path.join(root, 'src', 'KukaLab.Cli'), '--', ...forwarded], { stdio: 'inherit', cwd: root });
const outputFlag = forwarded.indexOf('--output');
if (outputFlag >= 0 && forwarded[outputFlag + 1]) {
  const receiptPath = path.resolve(root, forwarded[outputFlag + 1]);
  if (fs.existsSync(receiptPath)) {
    const receiptBytes = fs.readFileSync(receiptPath);
    const binding = createBinding(snapshot, probe, receiptBytes, forwarded, cli.status ?? 1);
    fs.writeFileSync(`${receiptPath}.profile.json`, JSON.stringify(binding, null, 2) + '\n');
  }
}
process.exit(cli.status ?? 1);