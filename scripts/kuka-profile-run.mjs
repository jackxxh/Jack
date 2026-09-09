#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { loadProfile, probeProfile, createBinding } from './profile-lib.mjs';
import { planProfileCommand } from './profile-commands.mjs';

const args = process.argv.slice(2);
const p = args.indexOf('--profile');
if (p < 0 || !args[p + 1]) { console.error('Usage: node scripts/kuka-profile-run.mjs --profile <profile.json> <kuka-lab CLI args...>'); process.exit(2); }
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
let snapshot;
try { snapshot = loadProfile(args[p + 1]); } catch (error) { console.error(`Profile invalid: ${error.message}`); process.exit(2); }
const probe = probeProfile(snapshot);
const requested = args.filter((value, i) => i !== p && i !== p + 1 && value !== '--plan');
let plan;
try { plan = planProfileCommand(snapshot, requested, probe); } catch (error) { console.error(error.message); process.exit(2); }
if (args.includes('--plan')) { console.log(JSON.stringify(plan, null, 2)); process.exit(0); }
if (!plan.canInvoke) { console.error(JSON.stringify(plan, null, 2)); process.exit(1); }
const forwarded = plan.cliArgs;
const outputFlag = forwarded.indexOf('--output');
if (outputFlag >= 0 && fs.existsSync(path.resolve(root, forwarded[outputFlag + 1]))) { console.error('Receipt output already exists; no operation was started.'); process.exit(1); }
const cli = spawnSync('dotnet', ['run', '--project', path.join(root, 'src', 'KukaLab.Cli'), '--', ...forwarded], { stdio: 'inherit', cwd: root });
if (outputFlag >= 0 && forwarded[outputFlag + 1]) {
  const receiptPath = path.resolve(root, forwarded[outputFlag + 1]);
  if (fs.existsSync(receiptPath)) {
    const receiptBytes = fs.readFileSync(receiptPath);
    const binding = createBinding(snapshot, probe, receiptBytes, forwarded, cli.status ?? 1);
    fs.writeFileSync(`${receiptPath}.profile.json`, JSON.stringify(binding, null, 2) + '\n');
  }
}
process.exit(cli.status ?? 1);
