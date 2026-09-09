#!/usr/bin/env node
import fs from 'node:fs';
import crypto from 'node:crypto';

const file = process.argv[2];
if (!file) {
  console.error('Usage: node scripts/validate-profile.mjs <profile.json>');
  process.exit(2);
}
let profile;
try { profile = JSON.parse(fs.readFileSync(file, 'utf8')); }
catch (error) { console.error(`Invalid JSON: ${error.message}`); process.exit(2); }
const errors = [];
if (!/^[A-Za-z0-9._-]+$/.test(profile.id ?? '')) errors.push('id must contain only letters, digits, dot, underscore or hyphen');
if (profile.platform !== 'windows') errors.push('platform must be windows');
const software = profile.software;
if (!software || typeof software !== 'object' || !Object.keys(software).length) errors.push('software must enable at least one adapter');
for (const [name, value] of Object.entries(software ?? {})) {
  if (!['kukaSim', 'officeLite', 'workVisual'].includes(name)) errors.push(`unsupported software block: ${name}`);
  if (typeof value !== 'object' || typeof value.enabled !== 'boolean' || typeof value.version !== 'string' || !value.version) errors.push(`${name} requires enabled and version`);
  if (value?.pathKey && typeof profile.paths?.[value.pathKey] !== 'string') errors.push(`${name}.pathKey does not resolve in paths`);
}
const digest = crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex').toUpperCase();
if (errors.length) { for (const error of errors) console.error(`profile: ${error}`); process.exit(1); }
console.log(JSON.stringify({ valid: true, id: profile.id, profileSha256: digest, enabledAdapters: Object.entries(software).filter(([, v]) => v.enabled).map(([k]) => k) }, null, 2));
