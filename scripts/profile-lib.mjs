import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';

export const sha256 = bytes => crypto.createHash('sha256').update(bytes).digest('hex').toUpperCase();
const object = x => x !== null && typeof x === 'object' && !Array.isArray(x);
const text = x => typeof x === 'string' && x.trim().length > 0;
const adapters = ['kukaSim', 'officeLite', 'workVisual'];
function keys(value, allowed, label, errors) {
  if (!object(value)) { errors.push(`${label} must be an object`); return false; }
  for (const key of Object.keys(value)) if (!allowed.includes(key)) errors.push(`${label}.${key} is unknown`);
  return true;
}

export function validateProfile(profile) {
  const errors = [];
  if (!keys(profile, ['id', 'platform', 'software', 'paths', 'capabilities', 'metadata'], 'profile', errors)) return errors;
  if (!text(profile.id) || !/^[A-Za-z0-9._-]+$/.test(profile.id)) errors.push('Invalid profile id');
  if (profile.platform !== 'windows') errors.push('platform must be windows');
  const paths = profile.paths ?? {};
  if (!object(paths)) errors.push('paths must be an object');
  else for (const [key, value] of Object.entries(paths)) if (!text(value)) errors.push(`paths.${key} must be a nonempty string`);
  if (keys(profile.software, adapters, 'software', errors)) {
    for (const [name, spec] of Object.entries(profile.software)) {
      if (!keys(spec, ['enabled', 'version', 'build', 'pathKey'], `software.${name}`, errors)) continue;
      if (typeof spec.enabled !== 'boolean') errors.push(`${name}.enabled must be boolean`);
      if (!text(spec.version)) errors.push(`${name}.version must be nonempty`);
      if (spec.build !== undefined && typeof spec.build !== 'string') errors.push(`${name}.build must be a string`);
      if (spec.pathKey !== undefined && !text(spec.pathKey)) errors.push(`${name}.pathKey must be nonempty`);
      if (spec.enabled && !text(spec.pathKey)) errors.push(`${name}.pathKey is required when enabled`);
      if (text(spec.pathKey) && (!object(paths) || !Object.hasOwn(paths, spec.pathKey) || !text(paths[spec.pathKey]))) errors.push(`${name}.pathKey does not resolve in paths`);
    }
  }
  if (profile.capabilities !== undefined && (!Array.isArray(profile.capabilities) || profile.capabilities.some(x => !text(x)))) errors.push('capabilities must be an array of nonempty strings');
  if (profile.metadata !== undefined && (!object(profile.metadata) || Object.values(profile.metadata).some(x => typeof x !== 'string'))) errors.push('metadata must contain string values');
  return errors;
}

export function loadProfile(file) {
  const fullPath = path.resolve(file);
  const bytes = fs.readFileSync(fullPath);
  const profile = JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(bytes));
  const errors = validateProfile(profile);
  if (errors.length) throw new Error(errors.join('; '));
  return { profile, fullPath, bytes, profileSha256: sha256(bytes) };
}

export function probeProfile(snapshot) {
  const { profile, fullPath, profileSha256 } = snapshot;
  const checks = adapters.map(adapter => {
    const spec = profile.software[adapter];
    const configured = spec?.enabled === true;
    const result = { adapter, configured, declaredVersion: spec?.version ?? null,
      observedVersion: null, pathKey: spec?.pathKey ?? null, pathExists: false,
      status: 'NotRun', executionVerified: false };
    if (!configured) return result;
    const raw = profile.paths[spec.pathKey];
    if (process.platform !== 'win32' && path.win32.isAbsolute(raw)) return { ...result, status: 'UnsupportedPlatform' };
    const location = path.resolve(path.dirname(fullPath), raw);
    try {
      const directory = fs.statSync(location).isDirectory();
      return { ...result, pathExists: directory, status: directory ? 'Unverified' : 'Missing',
        reason: directory ? 'Directory exists; executable identity, version, license and execution were not checked.' : 'Configured root is not a directory.' };
    } catch (error) { return { ...result, status: error.code === 'ENOENT' ? 'Missing' : 'Inaccessible' }; }
  });
  return { profileId: profile.id, profileSha256, scope: 'filesystem-presence-only', checks };
}

const SIDECAR_SCHEMA = 'kuka.lab.profile-invocation';
export function createBinding(snapshot, probe, receiptBytes, cliArgs, exitCode) {
  const payload = {
    profileId: snapshot.profile.id, profileSha256: snapshot.profileSha256,
    receiptSha256: sha256(receiptBytes), cliArgs, exitCode, probe,
    scope: 'Profile supplied to wrapper; vendor execution compatibility is not attested.'
  };
  return { schemaIdentity: SIDECAR_SCHEMA, schemaVersion: 1, payload, payloadSha256: sha256(JSON.stringify(payload)) };
}

export function verifyBinding(binding, profileBytes, receiptBytes) {
  const errors = [];
  if (!object(binding) || binding.schemaIdentity !== SIDECAR_SCHEMA || binding.schemaVersion !== 1 || !object(binding.payload)) return { succeeded: false, errors: ['Unsupported profile binding schema'] };
  if (binding.payloadSha256 !== sha256(JSON.stringify(binding.payload))) errors.push('Binding payload hash mismatch');
  if (binding.payload.profileSha256 !== sha256(profileBytes)) errors.push('Profile bytes changed');
  if (binding.payload.receiptSha256 !== sha256(receiptBytes)) errors.push('Receipt bytes changed');
  if (binding.payload.probe?.profileSha256 !== binding.payload.profileSha256) errors.push('Probe/profile identity mismatch');
  return { succeeded: errors.length === 0, errors, scope: 'Byte binding only; run the Core receipt verifier separately.' };
}
