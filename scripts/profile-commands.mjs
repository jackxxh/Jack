import path from 'node:path';

// Installation-specific flags supported by the pinned CLI. No authorization,
// controller address, snapshot, project identity or candidate is defaulted here.
export const profileCommands = {
  'krl candidate-intake': { adapters: [], paths: {} },
  'receipt verify': { adapters: [], paths: {} },
  'workvisual runner-smoke': { adapters: ['workVisual'], paths: { '--runner': 'workVisualRunner' } },
  'workvisual interface-inventory': { adapters: ['workVisual'], paths: { '--install-root': '@workVisual' } },
  'workvisual project-extract': { adapters: ['workVisual'], paths: { '--extractor': 'workVisualExtractor' } },
  'kukasim component-smoke': { adapters: ['kukaSim'], paths: { '--launcher': 'simLauncher', '--component': 'component' } },
  'kukasim integrated-validate': { adapters: ['kukaSim'], paths: { '--engine': 'simEngine', '--component': 'component' } },
  'kukasim candidate-execute': { adapters: ['kukaSim'], paths: { '--engine': 'simEngine', '--component': 'component', '--layout': 'layout' } },
  'officelite boot-verify': { adapters: ['officeLite'], paths: { '--asset-root': '@officeLite', '--vmrun': 'vmrun', '--vmx': 'vmx' } },
  'officelite project-inventory': { adapters: ['officeLite', 'workVisual'], paths: { '--asset-root': '@officeLite', '--vmrun': 'vmrun', '--vmx': 'vmx', '--runner': 'workVisualRunner' } },
  'officelite controller-profile-readback': { adapters: ['officeLite', 'workVisual'], paths: { '--asset-root': '@officeLite', '--vmrun': 'vmrun', '--vmx': 'vmx', '--runner': 'workVisualRunner' } },
  'officelite kss-candidate-execute': { adapters: ['officeLite', 'workVisual'], paths: { '--asset-root': '@officeLite', '--vmrun': 'vmrun', '--vmx': 'vmx', '--runner': 'workVisualRunner' } },
  'kukasim officelite-loop': { adapters: ['kukaSim', 'officeLite', 'workVisual'], paths: { '--asset-root': '@officeLite', '--vmrun': 'vmrun', '--vmx': 'vmx', '--runner': 'workVisualRunner', '--engine': 'simEngine', '--component': 'component', '--layout': 'layout' } }
};

export function planProfileCommand(snapshot, args, probe) {
  const command = args.slice(0, 2).join(' ');
  const contract = profileCommands[command];
  if (!contract) throw new Error(`No profile route for ${command}; use the Core CLI directly with explicit arguments.`);
  const cliArgs = [...args];
  const applied = [];
  for (const [flag, key] of Object.entries(contract.paths)) {
    if (cliArgs.includes(flag)) continue;
    const pathKey = key.startsWith('@') ? snapshot.profile.software[key.slice(1)]?.pathKey : key;
    const raw = snapshot.profile.paths?.[pathKey];
    if (!raw) continue;
    const value = path.resolve(path.dirname(snapshot.fullPath), raw);
    cliArgs.push(flag, value);
    applied.push({ flag, pathKey });
  }
  const checks = probe.checks.filter(x => contract.adapters.includes(x.adapter));
  const blocked = checks.filter(x => !x.configured || x.status !== 'Unverified');
  return { command, requiredAdapters: contract.adapters, cliArgs, applied, checks,
    canInvoke: blocked.length === 0, executionVerified: false,
    reason: blocked.length ? 'Required adapter is disabled, missing or inaccessible.' : 'Arguments resolved; vendor compatibility and execution remain unverified.' };
}
