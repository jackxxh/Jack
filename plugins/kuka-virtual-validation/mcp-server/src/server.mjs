import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { McpServer } from '@modelcontextprotocol/server';
import { serveStdio } from '@modelcontextprotocol/server/stdio';
import * as z from 'zod/v4';

const SERVER_VERSION = '0.12.0';
const MAX_CAPTURE_BYTES = 8 * 1024 * 1024;
const DEFAULT_TIMEOUT_MS = 120_000;
const SERVER_DIRECTORY = path.dirname(fileURLToPath(import.meta.url));
const PROTECTED_LICENSE_PATH = path.resolve(
  'C:\\ProgramData\\Visual Components\\Visual Components License Server 2.0\\lservrc.dat'
);

const workspaceSchema = z.string().min(1).describe(
  'Absolute path to the contracts development workspace that contains 04_kuka_lab.'
);
const pathSchema = z.string().min(1).describe('Absolute or workspace-relative filesystem path.');

export function resolveLabRoot(workspaceRoot) {
  const root = path.resolve(workspaceRoot);
  const candidates = [root, path.join(root, '04_kuka_lab')];
  const labRoot = candidates.find(candidate =>
    fs.existsSync(path.join(candidate, 'AGENTS.md'))
    && fs.existsSync(path.join(candidate, 'src', 'KukaLab.Cli', 'KukaLab.Cli.csproj'))
  );

  if (!labRoot) {
    throw new Error(
      `Cannot locate 04_kuka_lab from workspaceRoot: ${root}. `
      + 'Pass the workspace root or the 04_kuka_lab directory itself.'
    );
  }

  return labRoot;
}

export function assertAllowedPath(candidatePath, label) {
  const resolved = path.resolve(candidatePath);
  const identities = [resolved];
  if (fs.existsSync(resolved)) identities.push(fs.realpathSync.native(resolved));
  if (identities.some(identity => identity.toLowerCase() === PROTECTED_LICENSE_PATH.toLowerCase())) {
    throw new Error(`${label} is the protected Visual Components license-control file and cannot be read.`);
  }

  return resolved;
}

export function assertWorkspacePath(workspaceRoot, candidatePath, label) {
  const root = path.resolve(workspaceRoot);
  const candidate = assertAllowedPath(
    path.isAbsolute(candidatePath) ? candidatePath : path.join(root, candidatePath),
    label
  );
  const isDescendant = (ancestor, descendant) => {
    const relative = path.relative(ancestor, descendant);
    return relative !== ''
      && !relative.startsWith(`..${path.sep}`)
      && relative !== '..'
      && !path.isAbsolute(relative);
  };
  if (!isDescendant(root, candidate)) {
    throw new Error(`${label} must be a descendant of workspaceRoot: ${root}.`);
  }
  const realRoot = fs.realpathSync.native(root);
  let existingAncestor = candidate;
  while (!fs.existsSync(existingAncestor)) {
    const parent = path.dirname(existingAncestor);
    if (parent === existingAncestor) {
      throw new Error(`${label} has no existing ancestor inside workspaceRoot.`);
    }
    existingAncestor = parent;
  }
  const realAncestor = fs.realpathSync.native(existingAncestor);
  if (realAncestor !== realRoot && !isDescendant(realRoot, realAncestor)) {
    throw new Error(`${label} crosses a reparse/symlink boundary outside workspaceRoot.`);
  }
  return candidate;
}

export function assertWorkspaceBoundPath(workspaceRoot, candidatePath, label) {
  const labRoot = resolveLabRoot(workspaceRoot);
  return assertWorkspacePath(path.dirname(labRoot), candidatePath, label);
}

export function resolveCliCommand(labRoot, serverDirectory = SERVER_DIRECTORY) {
  const pluginRoot = path.resolve(serverDirectory, '..', '..');
  const canonicalPluginRoot = path.join(labRoot, 'plugins', 'kuka-virtual-validation');
  const sourceMode = fs.existsSync(pluginRoot)
    && fs.existsSync(canonicalPluginRoot)
    && fs.realpathSync.native(pluginRoot).toLowerCase()
      === fs.realpathSync.native(canonicalPluginRoot).toLowerCase();
  if (sourceMode) {
    const cliDll = path.join(
      labRoot,
      'src',
      'KukaLab.Cli',
      'bin',
      'Release',
      'net8.0',
      'kuka-lab.dll'
    );
    const coreDll = path.join(path.dirname(cliDll), 'KukaLab.Core.dll');
    if (!fs.existsSync(cliDll) || !fs.existsSync(coreDll)) {
      throw new Error(
        `Built KUKA Lab CLI/Core not found beside: ${cliDll}. `
        + 'Run the affected KUKA Lab build before using the plugin.'
      );
    }
    return { command: 'dotnet', argsPrefix: [cliDll], runtimeMode: 'CanonicalSource' };
  }

  const bundledRuntimeRoot = path.resolve(serverDirectory, '..', 'runtime');
  const bundledCliDll = path.join(bundledRuntimeRoot, 'kuka-lab.dll');
  const bundledCoreDll = path.join(bundledRuntimeRoot, 'KukaLab.Core.dll');
  if (fs.existsSync(bundledCliDll) && fs.existsSync(bundledCoreDll)) {
    return { command: 'dotnet', argsPrefix: [bundledCliDll], runtimeMode: 'Bundled' };
  }

  throw new Error(
    `Installed plugin runtime is missing from ${bundledRuntimeRoot}. `
    + 'Reinstall the cache-busted plugin package; caller-selected workspace binaries are never executed.'
  );
}

function appendCapture(current, chunk, streamName, child) {
  const next = current + chunk.toString('utf8');
  if (Buffer.byteLength(next, 'utf8') > MAX_CAPTURE_BYTES) {
    child.kill();
    throw new Error(`${streamName} exceeded the ${MAX_CAPTURE_BYTES}-byte capture limit.`);
  }

  return next;
}

export function invokeCliProcess({
  labRoot,
  cliArgs,
  timeoutMs = DEFAULT_TIMEOUT_MS,
  signal,
  terminateOnAbort = true
}) {
  const { command, argsPrefix } = resolveCliCommand(labRoot);

  return new Promise((resolve, reject) => {
    const child = spawn(command, [...argsPrefix, ...cliArgs], {
      cwd: labRoot,
      windowsHide: true,
      stdio: ['ignore', 'pipe', 'pipe']
    });
    let stdout = '';
    let stderr = '';
    let settled = false;

    const finish = (callback, value) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      signal?.removeEventListener('abort', abort);
      callback(value);
    };
    const abort = () => {
      if (terminateOnAbort) child.kill();
      finish(
        reject,
        new Error(terminateOnAbort
          ? 'KUKA Lab CLI call was cancelled.'
          : 'KUKA Lab CLI call was cancelled by the client; the bounded lifecycle continues only to complete its mandatory cleanup.')
      );
    };
    const timer = setTimeout(() => {
      if (terminateOnAbort) child.kill();
      finish(
        reject,
        new Error(terminateOnAbort
          ? `KUKA Lab CLI timed out after ${timeoutMs} ms.`
          : `KUKA Lab CLI exceeded ${timeoutMs} ms; its bounded lifecycle continues only to complete mandatory cleanup.`)
      );
    }, timeoutMs);

    signal?.addEventListener('abort', abort, { once: true });
    child.stdout.on('data', chunk => {
      if (settled) return;
      try {
        stdout = appendCapture(stdout, chunk, 'stdout', child);
      } catch (error) {
        finish(reject, error);
      }
    });
    child.stderr.on('data', chunk => {
      if (settled) return;
      try {
        stderr = appendCapture(stderr, chunk, 'stderr', child);
      } catch (error) {
        finish(reject, error);
      }
    });
    child.on('error', error => finish(reject, error));
    child.on('close', code => finish(resolve, {
      exitCode: code ?? 73,
      stdout: stdout.trim(),
      stderr: stderr.trim()
    }));
  });
}

function parseJsonOutput(stdout) {
  if (!stdout) return null;
  try {
    return JSON.parse(stdout);
  } catch (error) {
    throw new Error(`KUKA Lab CLI returned non-JSON stdout: ${error.message}`);
  }
}

function buildResult(execution, acceptedExitCodes) {
  const accepted = acceptedExitCodes.includes(execution.exitCode);
  const result = parseJsonOutput(execution.stdout);
  const structuredContent = {
    status: accepted ? (execution.exitCode === 4 ? 'DifferencesObserved' : 'Completed') : 'Failed',
    exitCode: execution.exitCode,
    result,
    stderr: execution.stderr
  };

  return {
    content: [{
      type: 'text',
      text: JSON.stringify(structuredContent, null, 2)
    }],
    structuredContent,
    ...(accepted ? {} : { isError: true })
  };
}

function errorResult(error) {
  return {
    content: [{ type: 'text', text: error instanceof Error ? error.message : String(error) }],
    isError: true
  };
}

export function buildOfficeLiteProjectInventoryCliArgs(args) {
  const labRoot = resolveLabRoot(args.workspaceRoot);
  const repositoryRoot = path.dirname(labRoot);
  const cliArgs = [
    'officelite', 'project-inventory',
    '--asset-root', assertAllowedPath(args.assetRoot, 'assetRoot'),
    '--output', assertWorkspacePath(repositoryRoot, args.outputReceipt, 'outputReceipt'),
    '--timeout-seconds', String(args.timeoutSeconds),
    '--observation-seconds', String(args.observationSeconds),
    '--runner-timeout-seconds', String(args.runnerTimeoutSeconds)
  ];
  if (args.vmrun) cliArgs.push('--vmrun', assertAllowedPath(args.vmrun, 'vmrun'));
  if (args.runner) cliArgs.push('--runner', assertAllowedPath(args.runner, 'runner'));
  if (args.guestIp) cliArgs.push('--guest-ip', args.guestIp);
  if (args.attemptId) cliArgs.push('--attempt-id', args.attemptId);
  return cliArgs;
}

export function buildKukaSimCandidateExecutionCliArgs(args) {
  const labRoot = resolveLabRoot(args.workspaceRoot);
  const repositoryRoot = path.dirname(labRoot);
  const cliArgs = [
    'kukasim', 'candidate-execute',
    '--candidate-root', assertWorkspacePath(repositoryRoot, args.candidateRoot, 'candidateRoot'),
    '--candidate-receipt', assertWorkspacePath(repositoryRoot, args.candidateReceipt, 'candidateReceipt'),
    '--program', args.programRelativeStem,
    '--evidence-dir', assertWorkspacePath(repositoryRoot, args.evidenceDirectory, 'evidenceDirectory'),
    '--output', assertWorkspacePath(repositoryRoot, args.outputReceipt, 'outputReceipt'),
    '--allow-gui', 'true',
    '--authorization-ref', args.authorizationReference,
    '--timeout-seconds', String(args.timeoutSeconds),
    '--maximum-start-commands', String(args.maximumStartCommands)
  ];
  if (args.enginePath) cliArgs.push('--engine', assertAllowedPath(args.enginePath, 'enginePath'));
  if (args.componentPath) cliArgs.push('--component', assertAllowedPath(args.componentPath, 'componentPath'));
  if (args.simulationLayoutPath) {
    cliArgs.push(
      '--layout',
      assertWorkspacePath(repositoryRoot, args.simulationLayoutPath, 'simulationLayoutPath')
    );
  }
  if (args.attemptId) cliArgs.push('--attempt-id', args.attemptId);
  return cliArgs;
}

export function buildOfficeLiteNativeCandidateExecutionCliArgs(args) {
  const labRoot = resolveLabRoot(args.workspaceRoot);
  const repositoryRoot = path.dirname(labRoot);
  const cliArgs = [
    'officelite', 'kss-candidate-execute',
    '--asset-root', assertAllowedPath(args.assetRoot, 'assetRoot'),
    '--candidate-root', assertWorkspacePath(repositoryRoot, args.candidateRoot, 'candidateRoot'),
    '--candidate-receipt', assertWorkspacePath(repositoryRoot, args.candidateReceipt, 'candidateReceipt'),
    '--program', args.programRelativeStem,
    '--profile-acceptance', assertWorkspacePath(repositoryRoot, args.profileAcceptance, 'profileAcceptance'),
    '--snapshot-name', args.snapshotName,
    '--expected-project', args.expectedProject,
    '--vmx', assertAllowedPath(args.vmxPath, 'vmxPath'),
    '--output', assertWorkspacePath(repositoryRoot, args.outputReceipt, 'outputReceipt'),
    '--timeout-seconds', String(args.timeoutSeconds),
    '--observation-seconds', String(args.observationSeconds),
    '--runner-timeout-seconds', String(args.runnerTimeoutSeconds),
    '--maximum-start-commands', String(args.maximumStartCommands)
  ];
  if (args.vmrunPath) cliArgs.push('--vmrun', assertAllowedPath(args.vmrunPath, 'vmrunPath'));
  if (args.runnerPath) cliArgs.push('--runner', assertAllowedPath(args.runnerPath, 'runnerPath'));
  if (args.guestIp) cliArgs.push('--guest-ip', args.guestIp);
  if (args.dhcpLeasesPath) {
    cliArgs.push('--dhcp-leases', assertAllowedPath(args.dhcpLeasesPath, 'dhcpLeasesPath'));
  }
  if (args.attemptId) cliArgs.push('--attempt-id', args.attemptId);
  return cliArgs;
}

async function callCli(
  args,
  cliArgs,
  acceptedExitCodes,
  context,
  invokeCli,
  timeoutMs = DEFAULT_TIMEOUT_MS,
  terminateOnAbort = true
) {
  try {
    const labRoot = resolveLabRoot(args.workspaceRoot);
    resolveCliCommand(labRoot);
    const execution = await invokeCli({
      labRoot,
      cliArgs,
      timeoutMs,
      terminateOnAbort,
      signal: context?.mcpReq?.signal
    });
    return buildResult(execution, acceptedExitCodes);
  } catch (error) {
    return errorResult(error);
  }
}

export function createKukaLabServer({ invokeCli = invokeCliProcess } = {}) {
  const server = new McpServer(
    { name: 'kuka-virtual-validation', version: SERVER_VERSION },
    {
      instructions:
        'This developer-preview server wraps only accepted KUKA Lab Core/CLI operations. '
        + 'It starts OfficeLite only through the bounded project-inventory tool and runs KUKA.Sim candidates only through the exact-C01 bounded execution tool with an explicit authorization reference; it never connects a physical controller, '
        + 'read license material, or promote a receipt beyond its recorded evidence level. '
        + 'It accepts only file-based KRL or ValidationPackage inputs and never invokes Rhino. '
        + 'Controller observation intake binds owner-declared facts only and performs no controller access. '
        + 'Stage-one composition remains file-only. Native OfficeLite/KSS candidate execution is exposed only through a current exact-C01 profile-acceptance receipt, a named disposable-clone snapshot and mandatory cleanup.'
    }
  );

  server.registerTool(
    'kuka_lab_controller_observation_intake',
    {
      title: 'Bind Sanitized Controller Observation',
      description:
        'Creates an immutable receipt for sanitized facts already observed by the owner. '
        + 'It explicitly retains robot MADA, technology packages and Tool/Base/Load as pending and performs no network or vendor action.',
      inputSchema: z.object({
        workspaceRoot: workspaceSchema,
        controllerFamily: z.string().min(1).max(128),
        cabinetModel: z.string().min(1).max(128),
        kssVersion: z.string().regex(/^[0-9]+\.[0-9]+\.[0-9]+$/),
        kssBuild: z.string().regex(/^B[0-9]+$/i),
        kliAddress: z.string().min(7).max(45),
        prefixLength: z.number().int().min(1).max(30),
        observedOnLocalDate: z.string().regex(/^[0-9]{4}-[0-9]{2}-[0-9]{2}$/),
        evidenceReferences: z.array(z.string().regex(/^[A-Za-z0-9][A-Za-z0-9._:-]{2,127}$/)).min(1).max(16),
        outputReceipt: pathSchema,
        attemptId: z.string().min(3).max(128).optional()
      }),
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false, openWorldHint: false }
    },
    async (args, context) => {
      try {
        const cliArgs = [
          'controller', 'observation-intake',
          '--controller-family', args.controllerFamily,
          '--cabinet-model', args.cabinetModel,
          '--kss-version', args.kssVersion,
          '--kss-build', args.kssBuild,
          '--kli-address', args.kliAddress,
          '--prefix-length', String(args.prefixLength),
          '--observed-on', args.observedOnLocalDate,
          '--evidence-references', args.evidenceReferences.join(','),
          '--output', assertWorkspaceBoundPath(args.workspaceRoot, args.outputReceipt, 'outputReceipt')
        ];
        if (args.attemptId) cliArgs.push('--attempt-id', args.attemptId);
        return await callCli(args, cliArgs, [0], context, invokeCli);
      } catch (error) {
        return errorResult(error);
      }
    }
  );

  server.registerTool(
    'kuka_lab_environment_inventory',
    {
      title: 'Inventory KUKA Lab Environment',
      description:
        'Creates a new immutable environment receipt using the workspace KUKA Lab CLI. '
        + 'This inventories files and configuration; it does not start OfficeLite or KUKA.Sim.',
      inputSchema: z.object({
        workspaceRoot: workspaceSchema,
        assetRoot: pathSchema,
        outputReceipt: pathSchema,
        attemptId: z.string().min(1).optional(),
        kukaSim: pathSchema.optional(),
        workVisual: pathSchema.optional(),
        vmrun: pathSchema.optional()
      }),
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false, openWorldHint: false }
    },
    async (args, context) => {
      try {
        const cliArgs = [
          'environment', 'inventory',
          '--asset-root', assertAllowedPath(args.assetRoot, 'assetRoot'),
          '--output', assertWorkspaceBoundPath(args.workspaceRoot, args.outputReceipt, 'outputReceipt')
        ];
        if (args.attemptId) cliArgs.push('--attempt-id', args.attemptId);
        if (args.kukaSim) cliArgs.push('--kuka-sim', assertAllowedPath(args.kukaSim, 'kukaSim'));
        if (args.workVisual) cliArgs.push('--workvisual', assertAllowedPath(args.workVisual, 'workVisual'));
        if (args.vmrun) cliArgs.push('--vmrun', assertAllowedPath(args.vmrun, 'vmrun'));
        return await callCli(args, cliArgs, [0, 2], context, invokeCli);
      } catch (error) {
        return errorResult(error);
      }
    }
  );

  server.registerTool(
    'kuka_lab_workvisual_interface_inventory',
    {
      title: 'Inventory WorkVisual Automation Interfaces',
      description:
        'Creates a hash-bound receipt for the installed WorkVisual scripting, file-transfer, program-control and '
        + 'runtime-message interfaces by reading managed metadata and configuration only. It does not start '
        + 'WorkVisual or OfficeLite, contact a controller, invoke upload/program-control methods, or run native KSS.',
      inputSchema: z.object({
        workspaceRoot: workspaceSchema,
        outputReceipt: pathSchema,
        installRoot: pathSchema.optional(),
        attemptId: z.string().min(3).max(128).optional()
      }),
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false, openWorldHint: false }
    },
    async (args, context) => {
      try {
        const cliArgs = [
          'workvisual', 'interface-inventory',
          '--output', assertWorkspaceBoundPath(args.workspaceRoot, args.outputReceipt, 'outputReceipt')
        ];
        if (args.installRoot) {
          cliArgs.push('--install-root', assertAllowedPath(args.installRoot, 'installRoot'));
        }
        if (args.attemptId) cliArgs.push('--attempt-id', args.attemptId);
        return await callCli(args, cliArgs, [0, 3], context, invokeCli);
      } catch (error) {
        return errorResult(error);
      }
    }
  );

  server.registerTool(
    'kuka_lab_officelite_project_inventory',
    {
      title: 'Read OfficeLite Project Identities',
      description:
        'Starts only the isolated OfficeLite VMware VM, waits for the accepted readiness gates, calls the pinned '
        + 'credential-free WorkVisual GetProjects() probe, creates an immutable receipt and always requests a soft VM stop in cleanup. '
        + 'The caller must permit the declared bounded lifecycle to complete; cancellation never force-kills cleanup. '
        + 'It cannot download, upload, activate, deploy, save or mutate a project; it does not run native KSS or contact a physical controller.',
      inputSchema: z.object({
        workspaceRoot: workspaceSchema,
        assetRoot: pathSchema,
        outputReceipt: pathSchema,
        vmrun: pathSchema.optional(),
        runner: pathSchema.optional(),
        guestIp: z.string().regex(/^(?:[0-9]{1,3}\.){3}[0-9]{1,3}$/).optional(),
        timeoutSeconds: z.number().int().min(1).max(900).default(240),
        observationSeconds: z.number().int().min(10).max(600).default(180),
        runnerTimeoutSeconds: z.number().int().min(1).max(120).default(30),
        attemptId: z.string().min(3).max(128).optional()
      }),
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false, openWorldHint: false }
    },
    async (args, context) => {
      try {
        const cliArgs = buildOfficeLiteProjectInventoryCliArgs(args);
        const operationTimeoutMs = (
          args.timeoutSeconds + args.observationSeconds + args.runnerTimeoutSeconds + 120
        ) * 1000;
        return await callCli(args, cliArgs, [0], context, invokeCli, operationTimeoutMs, false);
      } catch (error) {
        return errorResult(error);
      }
    }
  );

  server.registerTool(
    'kuka_lab_officelite_project_inventory_receipt_verify',
    {
      title: 'Verify OfficeLite Project Inventory Receipt',
      description:
        'Verifies the immutable OfficeLite/WorkVisual project-inventory receipt, including lifecycle cleanup evidence. '
        + 'It starts no VM, sends no network traffic and performs no vendor action.',
      inputSchema: z.object({
        workspaceRoot: workspaceSchema,
        receiptPath: pathSchema
      }),
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false }
    },
    async (args, context) => {
      try {
        const cliArgs = [
          'officelite', 'project-inventory-receipt', 'verify',
          '--receipt', assertWorkspaceBoundPath(args.workspaceRoot, args.receiptPath, 'receiptPath')
        ];
        return await callCli(args, cliArgs, [0], context, invokeCli);
      } catch (error) {
        return errorResult(error);
      }
    }
  );

  server.registerTool(
    'kuka_lab_kukasim_candidate_execute',
    {
      title: 'Execute KRL Candidate in Exact-C01 KUKA.Sim',
      description:
        'Runs one previously hash-bound SRC/DAT program through the accepted KUKA.Sim Integrated/Go runner using the pinned '
        + 'KR 210 R2700-2 C01 component. It records joint/TCP samples or an interpreter rejection, creates an immutable receipt, '
        + 'refuses pre-existing KUKA.Sim processes and verifies owned-process cleanup. It does not start OfficeLite, use RCS, invoke Rhino, '
        + 'or contact a physical controller. An explicit authorization reference is required for the bounded GUI-subsystem lifecycle.',
      inputSchema: z.object({
        workspaceRoot: workspaceSchema,
        candidateRoot: pathSchema,
        candidateReceipt: pathSchema,
        programRelativeStem: z.string().regex(/^[A-Za-z0-9][A-Za-z0-9_./-]{0,159}$/),
        evidenceDirectory: pathSchema,
        outputReceipt: pathSchema,
        authorizationReference: z.string().regex(/^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$/),
        enginePath: pathSchema.optional(),
        componentPath: pathSchema.optional(),
        simulationLayoutPath: pathSchema.optional().describe(
          'Optional exact-C01 KUKA.Sim layout under the workspace, such as the accepted controller-synchronized layout.'
        ),
        timeoutSeconds: z.number().int().min(10).max(600).default(180),
        maximumStartCommands: z.number().int().min(1).max(64).default(16),
        attemptId: z.string().min(3).max(128).optional()
      }),
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false, openWorldHint: false }
    },
    async (args, context) => {
      try {
        const cliArgs = buildKukaSimCandidateExecutionCliArgs(args);
        return await callCli(
          args,
          cliArgs,
          [0],
          context,
          invokeCli,
          (args.timeoutSeconds + 180) * 1000,
          false
        );
      } catch (error) {
        return errorResult(error);
      }
    }
  );

  server.registerTool(
    'kuka_lab_officelite_native_candidate_execute',
    {
      title: 'Execute KRL Candidate in Exact-C01 Native KSS',
      description:
        'Runs one hash-bound SRC/DAT pair through the disposable OfficeLite KSS only after a current exact-C01 profile receipt is admitted. '
        + 'It switches the virtual controller to T1 through the bounded WorkVisual data API with readback, uploads to a candidate-derived isolated directory, '
        + 'captures file/line/column KSS diagnostics or bounded execution state, then stops/resets/deselects, deletes the transaction and restores the named snapshot. '
        + 'It never contacts a physical controller, uses credentials, changes safety or reads license content.',
      inputSchema: z.object({
        workspaceRoot: workspaceSchema,
        assetRoot: pathSchema,
        candidateRoot: pathSchema,
        candidateReceipt: pathSchema,
        programRelativeStem: z.string().regex(/^[A-Za-z0-9][A-Za-z0-9_./-]{0,159}$/),
        profileAcceptance: pathSchema,
        snapshotName: z.string().regex(/^[A-Za-z0-9][A-Za-z0-9._ -]{2,95}$/),
        expectedProject: z.string().min(3).max(96),
        vmxPath: pathSchema,
        outputReceipt: pathSchema,
        vmrunPath: pathSchema.optional(),
        runnerPath: pathSchema.optional(),
        guestIp: z.string().regex(/^(?:[0-9]{1,3}\.){3}[0-9]{1,3}$/).optional(),
        dhcpLeasesPath: pathSchema.optional(),
        timeoutSeconds: z.number().int().min(1).max(900).default(300),
        observationSeconds: z.number().int().min(10).max(600).default(180),
        runnerTimeoutSeconds: z.number().int().min(1).max(300).default(180),
        maximumStartCommands: z.number().int().min(1).max(64).default(16),
        attemptId: z.string().min(3).max(96).optional()
      }),
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false, openWorldHint: false }
    },
    async (args, context) => {
      try {
        const cliArgs = buildOfficeLiteNativeCandidateExecutionCliArgs(args);
        const operationTimeoutMs = (
          args.timeoutSeconds + args.observationSeconds + args.runnerTimeoutSeconds + 180
        ) * 1000;
        return await callCli(args, cliArgs, [0], context, invokeCli, operationTimeoutMs, false);
      } catch (error) {
        return errorResult(error);
      }
    }
  );

  server.registerTool(
    'kuka_lab_officelite_native_candidate_receipt_verify',
    {
      title: 'Verify Exact-C01 Native-KSS Candidate Receipt',
      description:
        'Re-admits the current exact-C01 profile receipt, re-hashes the current SRC/DAT candidate and raw WorkVisual evidence, '
        + 'and verifies native disposition, Start bound, transaction cleanup and snapshot restoration without starting a VM or vendor process.',
      inputSchema: z.object({
        workspaceRoot: workspaceSchema,
        receiptPath: pathSchema
      }),
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false }
    },
    async (args, context) => {
      try {
        const labRoot = resolveLabRoot(args.workspaceRoot);
        const repositoryRoot = path.dirname(labRoot);
        const cliArgs = [
          'officelite', 'kss-candidate-receipt', 'verify',
          '--receipt', assertWorkspacePath(repositoryRoot, args.receiptPath, 'receiptPath')
        ];
        return await callCli(args, cliArgs, [0], context, invokeCli);
      } catch (error) {
        return errorResult(error);
      }
    }
  );

  server.registerTool(
    'kuka_lab_officelite_exact_profile_receipt_verify',
    {
      title: 'Verify Exact-C01 OfficeLite Profile Receipt',
      description:
        'Re-hashes and re-verifies the accepted runtime/project/MADA/axis/Tool/Base/Load and exact-model evidence without starting OfficeLite, KUKA.Sim or WorkVisual.',
      inputSchema: z.object({
        workspaceRoot: workspaceSchema,
        receiptPath: pathSchema
      }),
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false }
    },
    async (args, context) => {
      try {
        const labRoot = resolveLabRoot(args.workspaceRoot);
        const repositoryRoot = path.dirname(labRoot);
        const cliArgs = [
          'officelite', 'exact-profile-acceptance-receipt', 'verify',
          '--receipt', assertWorkspacePath(repositoryRoot, args.receiptPath, 'receiptPath')
        ];
        return await callCli(args, cliArgs, [0], context, invokeCli);
      } catch (error) {
        return errorResult(error);
      }
    }
  );

  server.registerTool(
    'kuka_lab_kukasim_candidate_receipt_verify',
    {
      title: 'Verify Exact-C01 KUKA.Sim Candidate Receipt',
      description:
        'Re-hashes the current SRC/DAT candidate, pinned exact-C01 component, raw KUKA.Sim result and immutable candidate-execution receipt. '
        + 'It starts no vendor application or VM and performs no network or physical-controller action.',
      inputSchema: z.object({
        workspaceRoot: workspaceSchema,
        receiptPath: pathSchema
      }),
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false }
    },
    async (args, context) => {
      try {
        const labRoot = resolveLabRoot(args.workspaceRoot);
        const repositoryRoot = path.dirname(labRoot);
        const cliArgs = [
          'kukasim', 'candidate-receipt', 'verify',
          '--receipt', assertWorkspacePath(repositoryRoot, args.receiptPath, 'receiptPath')
        ];
        return await callCli(args, cliArgs, [0], context, invokeCli);
      } catch (error) {
        return errorResult(error);
      }
    }
  );

  server.registerTool(
    'kuka_lab_fixture_verify',
    {
      title: 'Verify KUKA Lab Fixture',
      description:
        'Verifies tracked fixture structure, byte hashes and Rhino-to-KRL correlations, then creates a new receipt. '
        + 'Native KSS remains NotRun.',
      inputSchema: z.object({
        workspaceRoot: workspaceSchema,
        fixtureDirectory: pathSchema,
        outputReceipt: pathSchema,
        attemptId: z.string().min(1).optional()
      }),
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false, openWorldHint: false }
    },
    async (args, context) => {
      try {
        const cliArgs = [
          'fixture', 'verify',
          '--fixture', assertAllowedPath(args.fixtureDirectory, 'fixtureDirectory'),
          '--output', assertWorkspaceBoundPath(args.workspaceRoot, args.outputReceipt, 'outputReceipt')
        ];
        if (args.attemptId) cliArgs.push('--attempt-id', args.attemptId);
        return await callCli(args, cliArgs, [0, 2], context, invokeCli);
      } catch (error) {
        return errorResult(error);
      }
    }
  );

  server.registerTool(
    'kuka_lab_raw_krl_candidate_intake',
    {
      title: 'Intake Raw KRL Candidate',
      description:
        'Hashes and pairs a directory containing only SRC/DAT files, then creates a black-box candidate receipt. '
        + 'It does not invoke or modify Rhino, invent Workcell/MotionPlan/frame facts, start vendor software, contact '
        + 'a controller or run native KSS. Extra files are rejected without reading their content.',
      inputSchema: z.object({
        workspaceRoot: workspaceSchema,
        sourceDirectory: pathSchema,
        outputReceipt: pathSchema,
        attemptId: z.string().min(3).max(128).optional()
      }),
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false, openWorldHint: false }
    },
    async (args, context) => {
      try {
        const cliArgs = [
          'krl', 'candidate-intake',
          '--source', assertAllowedPath(args.sourceDirectory, 'sourceDirectory'),
          '--output', assertWorkspaceBoundPath(args.workspaceRoot, args.outputReceipt, 'outputReceipt')
        ];
        if (args.attemptId) cliArgs.push('--attempt-id', args.attemptId);
        return await callCli(args, cliArgs, [0, 2], context, invokeCli);
      } catch (error) {
        return errorResult(error);
      }
    }
  );

  server.registerTool(
    'kuka_lab_validation_package_verify',
    {
      title: 'Verify KUKA ValidationPackage',
      description:
        'Validates a producer-owned standard ValidationPackage, creates an immutable receipt outside the package, '
        + 'and leaves native KSS and all vendor applications NotRun.',
      inputSchema: z.object({
        workspaceRoot: workspaceSchema,
        packageDirectory: pathSchema,
        outputReceipt: pathSchema,
        attemptId: z.string().min(1).optional()
      }),
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false, openWorldHint: false }
    },
    async (args, context) => {
      try {
        const cliArgs = [
          'package', 'verify',
          '--package', assertAllowedPath(args.packageDirectory, 'packageDirectory'),
          '--output', assertWorkspaceBoundPath(args.workspaceRoot, args.outputReceipt, 'outputReceipt')
        ];
        if (args.attemptId) cliArgs.push('--attempt-id', args.attemptId);
        return await callCli(args, cliArgs, [0, 2], context, invokeCli);
      } catch (error) {
        return errorResult(error);
      }
    }
  );

  server.registerTool(
    'kuka_lab_validation_package_receipt_verify',
    {
      title: 'Verify ValidationPackage Receipt and Current Package',
      description:
        'Strictly verifies a ValidationPackage receipt and rehashes an explicitly supplied current package copy. '
        + 'It performs no vendor application, VM, network, UI or native KSS action.',
      inputSchema: z.object({
        workspaceRoot: workspaceSchema,
        receiptPath: pathSchema,
        packageDirectory: pathSchema
      }),
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false }
    },
    async (args, context) => {
      try {
        const cliArgs = [
          'package', 'receipt', 'verify',
          '--receipt', assertWorkspaceBoundPath(args.workspaceRoot, args.receiptPath, 'receiptPath'),
          '--package', assertAllowedPath(args.packageDirectory, 'packageDirectory')
        ];
        return await callCli(args, cliArgs, [0], context, invokeCli);
      } catch (error) {
        return errorResult(error);
      }
    }
  );

  server.registerTool(
    'kuka_lab_krl_static_preflight',
    {
      title: 'Run Static KRL Preflight',
      description:
        'Reads the exact SRC/DAT files from a currently verified ValidationPackage, checks program envelopes, '
        + 'SRC/DAT pairing, BAS initialization and motion-target declarations, then creates an immutable receipt. '
        + 'Native KSS remains NotRun and a pass is only readiness for later OfficeLite/KSS submission.',
      inputSchema: z.object({
        workspaceRoot: workspaceSchema,
        packageDirectory: pathSchema,
        packageReceiptPath: pathSchema,
        outputReceipt: pathSchema,
        attemptId: z.string().min(3).max(128).optional()
      }),
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false, openWorldHint: false }
    },
    async (args, context) => {
      try {
        const labRoot = resolveLabRoot(args.workspaceRoot);
        const repositoryRoot = path.dirname(labRoot);
        const cliArgs = [
          'package', 'krl-preflight',
          '--package', assertWorkspacePath(repositoryRoot, args.packageDirectory, 'packageDirectory'),
          '--package-receipt', assertWorkspacePath(repositoryRoot, args.packageReceiptPath, 'packageReceiptPath'),
          '--output', assertWorkspacePath(repositoryRoot, args.outputReceipt, 'outputReceipt')
        ];
        if (args.attemptId) cliArgs.push('--attempt-id', args.attemptId);
        return await callCli(args, cliArgs, [0, 2], context, invokeCli);
      } catch (error) {
        return errorResult(error);
      }
    }
  );

  server.registerTool(
    'kuka_lab_validation_package_controller_compare',
    {
      title: 'Compare ValidationPackage with Controller Evidence',
      description:
        'Compares a verified ValidationPackage compatibility target with a verified sanitized controller-observation receipt. '
        + 'Inconclusive is a completed comparison with pending evidence, not a compatibility pass.',
      inputSchema: z.object({
        workspaceRoot: workspaceSchema,
        packageReceiptPath: pathSchema,
        controllerObservationReceiptPath: pathSchema
      }),
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false }
    },
    async (args, context) => {
      try {
        const cliArgs = [
          'package', 'controller-compare',
          '--package-receipt', assertAllowedPath(args.packageReceiptPath, 'packageReceiptPath'),
          '--controller-observation-receipt', assertAllowedPath(
            args.controllerObservationReceiptPath,
            'controllerObservationReceiptPath'
          )
        ];
        return await callCli(args, cliArgs, [0], context, invokeCli);
      } catch (error) {
        return errorResult(error);
      }
    }
  );

  server.registerTool(
    'kuka_lab_receipt_verify',
    {
      title: 'Verify KUKA Lab Receipt',
      description:
        'Re-reads a supported KUKA Lab receipt and its bound evidence using the canonical CLI verifier. '
        + 'It performs no vendor application or VM lifecycle action.',
      inputSchema: z.object({
        workspaceRoot: workspaceSchema,
        receiptPath: pathSchema
      }),
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false }
    },
    async (args, context) => {
      try {
        const cliArgs = [
          'receipt', 'verify',
          '--receipt', assertWorkspaceBoundPath(args.workspaceRoot, args.receiptPath, 'receiptPath')
        ];
        return await callCli(args, cliArgs, [0], context, invokeCli);
      } catch (error) {
        return errorResult(error);
      }
    }
  );

  server.registerTool(
    'kuka_lab_stage_one_compose',
    {
      title: 'Compose Stage-One Evidence Bundle',
      description:
        'Creates one immutable file-only receipt that binds a current raw-KRL identity receipt or '
        + 'ValidationPackage static-preflight receipt to the exact accepted item-9D comparison and item-9E virtual-loop baselines. '
        + 'It starts no vendor software, VM, network connection, Rhino process or physical controller, and it never promotes '
        + 'the current candidate to native-KSS or KUKA.Sim validated.',
      inputSchema: z.object({
        workspaceRoot: workspaceSchema,
        candidateKind: z.enum(['raw-krl', 'validation-package']),
        candidateRoot: pathSchema,
        candidateReceiptPath: pathSchema,
        packageReceiptPath: pathSchema.optional(),
        comparisonReceiptPath: pathSchema,
        virtualLoopReceiptPath: pathSchema,
        outputReceipt: pathSchema,
        attemptId: z.string().min(3).max(128).optional()
      }).superRefine((value, context) => {
        if (value.candidateKind === 'validation-package' && !value.packageReceiptPath) {
          context.addIssue({
            code: 'custom',
            path: ['packageReceiptPath'],
            message: 'packageReceiptPath is required for validation-package candidates.'
          });
        }
        if (value.candidateKind === 'raw-krl' && value.packageReceiptPath) {
          context.addIssue({
            code: 'custom',
            path: ['packageReceiptPath'],
            message: 'packageReceiptPath is not allowed for raw-krl candidates.'
          });
        }
      }),
      annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false, openWorldHint: false }
    },
    async (args, context) => {
      try {
        const labRoot = resolveLabRoot(args.workspaceRoot);
        const repositoryRoot = path.dirname(labRoot);
        const cliArgs = [
          'stage-one', 'compose',
          '--candidate-kind', args.candidateKind,
          '--candidate-root', assertWorkspacePath(repositoryRoot, args.candidateRoot, 'candidateRoot'),
          '--candidate-receipt', assertWorkspacePath(
            repositoryRoot,
            args.candidateReceiptPath,
            'candidateReceiptPath'
          ),
          '--comparison-receipt', assertWorkspacePath(
            repositoryRoot,
            args.comparisonReceiptPath,
            'comparisonReceiptPath'
          ),
          '--virtual-loop-receipt', assertWorkspacePath(
            repositoryRoot,
            args.virtualLoopReceiptPath,
            'virtualLoopReceiptPath'
          ),
          '--output', assertWorkspacePath(repositoryRoot, args.outputReceipt, 'outputReceipt')
        ];
        if (args.packageReceiptPath) {
          cliArgs.push(
            '--package-receipt',
            assertWorkspacePath(repositoryRoot, args.packageReceiptPath, 'packageReceiptPath')
          );
        }
        if (args.attemptId) cliArgs.push('--attempt-id', args.attemptId);
        return await callCli(args, cliArgs, [0], context, invokeCli);
      } catch (error) {
        return errorResult(error);
      }
    }
  );

  server.registerTool(
    'kuka_lab_stage_one_receipt_verify',
    {
      title: 'Verify Stage-One Evidence Bundle',
      description:
        'Re-hashes the current candidate, the exact item-9D/item-9E inputs and the stage-one receipt through the dedicated CLI verifier. '
        + 'It performs no vendor application, VM, network, Rhino or physical-controller action.',
      inputSchema: z.object({
        workspaceRoot: workspaceSchema,
        receiptPath: pathSchema
      }),
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false }
    },
    async (args, context) => {
      try {
        const labRoot = resolveLabRoot(args.workspaceRoot);
        const repositoryRoot = path.dirname(labRoot);
        const cliArgs = [
          'stage-one', 'receipt', 'verify',
          '--receipt', assertWorkspacePath(repositoryRoot, args.receiptPath, 'receiptPath')
        ];
        return await callCli(args, cliArgs, [0], context, invokeCli);
      } catch (error) {
        return errorResult(error);
      }
    }
  );

  server.registerTool(
    'kuka_lab_virtual_loop_receipt_verify',
    {
      title: 'Verify Accepted KUKA.Sim/OfficeLite Virtual-Loop Receipt',
      description:
        'Re-verifies an existing item-9E KUKA.Sim/OfficeLite virtual-loop receipt and its bound raw evidence. '
        + 'This tool is read-only and deliberately does not expose the stateful VM or vendor-software execution command.',
      inputSchema: z.object({
        workspaceRoot: workspaceSchema,
        receiptPath: pathSchema
      }),
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false }
    },
    async (args, context) => {
      try {
        const labRoot = resolveLabRoot(args.workspaceRoot);
        const repositoryRoot = path.dirname(labRoot);
        const cliArgs = [
          'kukasim', 'officelite-loop-receipt', 'verify',
          '--receipt', assertWorkspacePath(repositoryRoot, args.receiptPath, 'receiptPath')
        ];
        return await callCli(args, cliArgs, [0], context, invokeCli);
      } catch (error) {
        return errorResult(error);
      }
    }
  );

  server.registerTool(
    'kuka_lab_environment_receipt_diff',
    {
      title: 'Compare KUKA Lab Environment Receipts',
      description:
        'Compares two verified environment receipts and returns localized drift. Exit code 4 means differences '
        + 'were observed successfully, not that the MCP transport failed.',
      inputSchema: z.object({
        workspaceRoot: workspaceSchema,
        baselineReceipt: pathSchema,
        currentReceipt: pathSchema
      }),
      annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false }
    },
    async (args, context) => {
      try {
        const cliArgs = [
          'environment', 'receipt', 'diff',
          '--baseline', assertAllowedPath(args.baselineReceipt, 'baselineReceipt'),
          '--current', assertAllowedPath(args.currentReceipt, 'currentReceipt')
        ];
        return await callCli(args, cliArgs, [0, 4], context, invokeCli);
      } catch (error) {
        return errorResult(error);
      }
    }
  );

  return server;
}

const mainPath = process.argv[1] ? path.resolve(process.argv[1]) : '';
if (mainPath === fileURLToPath(import.meta.url)) {
  void serveStdio(() => createKukaLabServer());
  console.error('kuka-virtual-validation MCP server listening on stdio');
}
