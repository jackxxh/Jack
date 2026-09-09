import { loadProfile, probeProfile } from './profile-lib.mjs';

const file = process.argv[2];
if (!file) {
  console.error('Usage: node scripts/profile-capability-matrix.mjs <profile.json>');
  process.exit(2);
}

const snapshot = loadProfile(file);
const probe = probeProfile(snapshot);
const configured = probe.checks.filter(x => x.configured).map(x => x.adapter);
const rows = [];
for (let mask = 1; mask < (1 << configured.length); mask++) {
  const selected = configured.filter((_, i) => (mask & (1 << i)) !== 0);
  const checks = probe.checks.filter(x => selected.includes(x.adapter));
  rows.push({ adapters: selected, statuses: Object.fromEntries(checks.map(x => [x.adapter, x.status])), executionVerified: false });
}
console.log(JSON.stringify({ profileId: snapshot.profile.id, profileSha256: snapshot.profileSha256, configured, combinations: rows,
  note: 'Combinations describe available profile inputs; executionVerified remains false until a vendor receipt is produced and verified.' }, null, 2));
