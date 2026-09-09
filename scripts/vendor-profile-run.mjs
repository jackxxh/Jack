#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { loadProfile, probeProfile, createBinding } from './profile-lib.mjs';

const args = process.argv.slice(2);
const profileIndex = args.indexOf('--profile');
if (profileIndex < 0 || !args[profileIndex + 1]) {
  console.error('Usage: node scripts/vendor-profile-run.mjs --profile <profile.json> [PowerShell arguments]');
  process.exit(2);
}
if (process.platform !== 'win32') { console.error('Vendor adapters require Windows.'); process.exit(2); }
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const snapshot = loadProfile(args[profileIndex + 1]);
const probe = probeProfile(snapshot);
const enabled = Object.entries(snapshot.profile.software).filter(([, value]) => value.enabled).map(([name]) => name);
if (!enabled.includes('kukaSim')) { console.error('The exact-C01 PowerShell adapter requires an enabled kukaSim profile.'); process.exit(1); }
if (probe.checks.some(check => check.configured && check.status === 'Missing')) {
  console.error(JSON.stringify(probe, null, 2)); process.exit(1);
}

const source = path.join(root, 'tools', 'exact-c01-official-loop.ps1');
let script = fs.readFileSync(source, 'utf8');
const paths = snapshot.profile.paths ?? {};
const kukaSimRoot = paths[snapshot.profile.software.kukaSim.pathKey];
const officeLite = snapshot.profile.software.officeLite?.pathKey ? paths[snapshot.profile.software.officeLite.pathKey] : undefined;
const replacements = [
  ["$labRoot = Join-Path $workspaceRoot '04_kuka_lab'", '$labRoot = $workspaceRoot'],
  ['C:\\Program Files\\KUKA\\KUKA.Sim 4.10', kukaSimRoot],
  ['C:/kuka-validation-lab/vendor/KUKA Simulation', officeLite]
];
for (const [from, to] of replacements) if (to) script = script.split(from).join(String(to).replaceAll('/', '\\'));
const temp = path.join(root, '.local', `vendor-profile-${Date.now()}.ps1`);
fs.mkdirSync(path.dirname(temp), { recursive: true });
fs.writeFileSync(temp, script);
try {
  const forwarded = args.filter((_, index) => index !== profileIndex && index !== profileIndex + 1);
  const result = spawnSync('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', temp, ...forwarded], { cwd: root, stdio: 'inherit' });
  const receiptIndex = forwarded.findIndex(value => value.toLowerCase() === '-receipt');
  if (result.status === 0 && receiptIndex >= 0 && forwarded[receiptIndex + 1]) {
    const receiptPath = path.resolve(root, forwarded[receiptIndex + 1]);
    if (fs.existsSync(receiptPath)) {
      const binding = createBinding(snapshot, probe, fs.readFileSync(receiptPath), forwarded, result.status);
      fs.writeFileSync(`${receiptPath}.profile.json`, JSON.stringify(binding, null, 2) + '\n');
    }
  }
  process.exitCode = result.status ?? 1;
} finally { fs.rmSync(temp, { force: true }); }
