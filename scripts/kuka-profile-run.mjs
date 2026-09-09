#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const args = process.argv.slice(2);
const p = args.indexOf('--profile');
if (p < 0 || !args[p + 1]) {
  console.error('Usage: node scripts/kuka-profile-run.mjs --profile <profile.json> <kuka-lab CLI args...>');
  process.exit(2);
}
const profile = path.resolve(args[p + 1]);
if (!fs.existsSync(profile)) { console.error(`Profile not found: ${profile}`); process.exit(2); }
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const check = spawnSync(process.execPath, [path.join(root, 'scripts', 'validate-profile.mjs'), profile], { stdio: 'inherit' });
if (check.status !== 0) process.exit(check.status ?? 1);
const probe = spawnSync(process.execPath, [path.join(root, 'scripts', 'probe-profile.mjs'), profile], { stdio: 'inherit' });
if (probe.status !== 0) process.exit(probe.status ?? 1);
const forwarded = args.filter((_, i) => i !== p && i !== p + 1);
const cli = spawnSync('dotnet', ['run', '--project', path.join(root, 'src', 'KukaLab.Cli'), '--', ...forwarded], { stdio: 'inherit', cwd: root });
const outputFlag = forwarded.indexOf('--output');
if (cli.status === 0 && outputFlag >= 0 && forwarded[outputFlag + 1]) {
  const receiptPath = path.resolve(root, forwarded[outputFlag + 1]);
  if (fs.existsSync(receiptPath)) {
    const receipt = JSON.parse(fs.readFileSync(receiptPath, 'utf8'));
    const profileData = JSON.parse(fs.readFileSync(profile, 'utf8'));
    const profileSha256 = (await import('node:crypto')).default.createHash('sha256').update(fs.readFileSync(profile)).digest('hex').toUpperCase();
    fs.writeFileSync(`${receiptPath}.profile.json`, JSON.stringify({ id: profileData.id, sha256: profileSha256, software: profileData.software, capabilityProbe: 'passed' }, null, 2) + '\n');
  }
}
process.exit(cli.status ?? 1);
