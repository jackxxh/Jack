#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';

const file = process.argv[2];
if (!file) { console.error('Usage: node scripts/probe-profile.mjs <profile.json>'); process.exit(2); }
const bytes = fs.readFileSync(file);
const profile = JSON.parse(bytes);
const profileSha256 = crypto.createHash('sha256').update(bytes).digest('hex').toUpperCase();
const checks = Object.entries(profile.software ?? {}).map(([name, spec]) => {
  const configured = Boolean(spec?.enabled);
  const root = spec?.pathKey ? profile.paths?.[spec.pathKey] : undefined;
  const exists = typeof root === 'string' && fs.existsSync(path.resolve(root));
  return { adapter: name, declaredVersion: spec?.version ?? null, configured, pathKey: spec?.pathKey ?? null, pathExists: exists, status: !configured ? 'NotRun' : exists ? 'Ready' : 'Missing' };
});
console.log(JSON.stringify({ profileId: profile.id, profileSha256, checks }, null, 2));
process.exit(checks.some(x => x.status === 'Missing') ? 1 : 0);
