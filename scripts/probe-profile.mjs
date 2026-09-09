#!/usr/bin/env node
import { loadProfile, probeProfile } from './profile-lib.mjs';
try {
  if (process.argv.length !== 3) throw new Error('Usage: node scripts/probe-profile.mjs <profile.json>');
  const result = probeProfile(loadProfile(process.argv[2]));
  console.log(JSON.stringify(result, null, 2));
  process.exitCode = result.checks.some(c => c.configured && c.status !== 'Unverified') ? 1 : 0;
} catch (error) { console.error(JSON.stringify({ error: error.message })); process.exitCode = 1; }