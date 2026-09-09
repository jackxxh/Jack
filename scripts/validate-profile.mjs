#!/usr/bin/env node
import { loadProfile } from './profile-lib.mjs';
try {
  if (process.argv.length !== 3) throw new Error('Usage: node scripts/validate-profile.mjs <profile.json>');
  const { profile, profileSha256 } = loadProfile(process.argv[2]);
  console.log(JSON.stringify({ valid: true, id: profile.id, profileSha256, enabledAdapters: Object.keys(profile.software).filter(k => profile.software[k].enabled) }, null, 2));
} catch (error) { console.error(JSON.stringify({ valid: false, error: error.message })); process.exitCode = 1; }