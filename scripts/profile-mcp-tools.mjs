// The importer embeds this registration and the shared profile helpers in the
// generated server, so a plugin bundle does not depend on a checkout's scripts/.
export function registerProfileTools(server, api) {
  const { z, fs, path, loadProfile, probeProfile, createBinding, planProfileCommand,
    assertAllowedPath, assertWorkspacePath, resolveLabRoot, invokeCli } = api;
  const result = value => ({ content: [{ type: 'text', text: JSON.stringify(value, null, 2) }], structuredContent: value });
  const fail = error => ({ isError: true, content: [{ type: 'text', text: error.message }] });
  const load = profilePath => loadProfile(assertAllowedPath(profilePath, 'profilePath'));
  server.registerTool('kuka_lab_profile_probe', {
    title: 'Probe Environment Profile', description: 'Validate a profile and check directory presence. Does not start vendor software or attest compatibility.',
    inputSchema: z.object({ profilePath: z.string().min(1) }),
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false }
  }, async args => { try { return result(probeProfile(load(args.profilePath))); } catch (error) { return fail(error); } });

  server.registerTool('kuka_lab_profile_plan', {
    title: 'Plan Profile CLI Invocation', description: 'Resolve installation paths and required software for a CLI operation without running it.',
    inputSchema: z.object({ profilePath: z.string().min(1), cliArgs: z.array(z.string()).min(2) }),
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false }
  }, async args => { try { const snapshot = load(args.profilePath); return result(planProfileCommand(snapshot, args.cliArgs, probeProfile(snapshot))); } catch (error) { return fail(error); } });

  server.registerTool('kuka_lab_profile_run', {
    title: 'Run Profile CLI Operation', description: 'Run an explicitly selected profile route. Vendor operations retain Core authorization and isolation checks. Never supplies GUI consent, project identity or controller addresses automatically.',
    inputSchema: z.object({ workspaceRoot: z.string().min(1), profilePath: z.string().min(1), cliArgs: z.array(z.string()).min(2) }),
    annotations: { readOnlyHint: false, destructiveHint: true, idempotentHint: false, openWorldHint: true }
  }, async (args, context) => {
    try {
      const labRoot = resolveLabRoot(args.workspaceRoot);
      const snapshot = load(args.profilePath);
      const probe = probeProfile(snapshot);
      const plan = planProfileCommand(snapshot, args.cliArgs, probe);
      if (!plan.canInvoke) return { ...result(plan), isError: true };
      const outputIndex = plan.cliArgs.indexOf('--output');
      let receiptPath;
      if (outputIndex >= 0) {
        const output = plan.cliArgs[outputIndex + 1];
        if (!output) throw new Error('--output needs a path');
        receiptPath = assertWorkspacePath(labRoot, path.resolve(labRoot, output), 'output');
        if (fs.existsSync(receiptPath) || fs.existsSync(receiptPath + '.profile.json')) throw new Error('Receipt or profile sidecar already exists; operation was not started.');
        plan.cliArgs[outputIndex + 1] = receiptPath;
      }
      const execution = await invokeCli({ labRoot, cliArgs: plan.cliArgs, timeoutMs: 900000, signal: context?.signal });
      let sidecarWritten = false;
      if (receiptPath && fs.existsSync(receiptPath)) {
        const binding = createBinding(snapshot, probe, fs.readFileSync(receiptPath), plan.cliArgs, execution.exitCode);
        fs.writeFileSync(receiptPath + '.profile.json', JSON.stringify(binding, null, 2) + '\n', { flag: 'wx' });
        sidecarWritten = true;
      }
      const reply = result({ exitCode: execution.exitCode, stdout: execution.stdout, stderr: execution.stderr, sidecarWritten, profileSha256: snapshot.profileSha256 });
      return execution.exitCode === 0 ? reply : { ...reply, isError: true };
    } catch (error) { return fail(error); }
  });
}
