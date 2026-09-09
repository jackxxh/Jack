#!/usr/bin/env node
import fs from 'node:fs';
import { verifyBinding } from './profile-lib.mjs';
try {
  const [bindingPath, profilePath, receiptPath] = process.argv.slice(2);
  if (process.argv.length !== 5) throw new Error('Usage: node scripts/verify-profile-binding.mjs <binding.json> <profile.json> <receipt.json>');
  const result = verifyBinding(JSON.parse(fs.readFileSync(bindingPath, 'utf8')), fs.readFileSync(profilePath), fs.readFileSync(receiptPath));
  console.log(JSON.stringify(result, null, 2));
  process.exitCode = result.succeeded ? 0 : 1;
} catch (error) { console.error(JSON.stringify({ succeeded: false, error: error.message })); process.exitCode = 1; }
