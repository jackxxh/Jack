# WorkVisual non-UI probes

These scripts exercise the installed WorkVisual Script Runner (`wvsr.exe`) without opening WorkVisual UI. They contain no credentials. Unless a section below explicitly labels an activation diagnostic as state-changing, they do not upload, activate, deploy, save or modify a controller project.

The activation-diagnostic scripts described below are retained solely to reproduce the exact-C01 activation diagnosis and are not general-purpose public commands. The next activation path begins only after the isolated OfficeLite clone completes its local `ExpertModeTransition`; prefer a supported deterministic controller channel, with smartHMI retained as a fallback interaction surface.

## Read-only runner smoke

```powershell
& 'C:\Program Files (x86)\KUKA\WorkVisual 6.0\wvsr.exe' `
  -executescript `
  "-scriptpath=$((Resolve-Path '.\04_kuka_lab\probes\workvisual\read-only-smoke.csx').Path)"
```

Accepted local behavior on 2026-08-26: exit `0`, application identity `WorkVisual`.

## Read-only controller project inventory

```powershell
& 'C:\Program Files (x86)\KUKA\WorkVisual 6.0\wvsr.exe' `
  -executescript `
  "-scriptpath=$((Resolve-Path '.\04_kuka_lab\probes\workvisual\online-project-inventory.csx').Path)" `
  '-address=203.0.113.128'
```

The address is an explicit per-run input. The script calls only `GetOnlineController(address).GetProjects()` and returns `42` with the vendor exception type when the endpoint is unavailable. It must first be exercised against an unreachable local address as the negative control. A live query may run only inside a Lab-owned bounded OfficeLite lifecycle after KUKA HTTPS readiness; while TCP 49003 is unreachable it is diagnostic evidence, not a connection attempt that can pass. Non-GUI lifecycle/query execution does not grant UI authority: observing or changing smartHMI Secure Remote Access still requires its own exact operation-scoped authorization, and the Lab never changes that setting.

Do not add username/password arguments, use WorkVisual deployment APIs or weaken OfficeLite secure remote-access policy in these probes. If controller access requires KukaUser authentication, the owner performs or separately authorizes that configuration; secrets never enter source, command logs or receipts.

## Active project download to an isolated PC directory

`active-project-download.csx` obtains the fixed online controller, calls `GetProjects()` and then only `DownloadProject(SpecialProjectType.ActiveProject, destinationFolder, false)`. The destination must already be an empty create-new directory supplied by Core/CLI; the script emits active-project and absolute downloaded-WVS paths as UTF-8 Base64 markers and refuses zero/multiple/non-WVS results.

Core/CLI owns the required unreachable-address negative control, OfficeLite lifecycle, raw evidence, WVS hashing and soft shutdown. The command does not accept a caller guest address; it resolves the isolated endpoint only from the exact VMX MAC and VMware DHCP lease. The script does not open/save the project, upload, deploy, activate, pin, merge, use credentials, run KSS or command motion. Formal isolated evidence is `WP4L-20260828-ISOLATED-DOWNLOAD-02`; physical-controller use remains attended and is not exposed by this OfficeLite lifecycle command.

## Read-only controller repository inventory

`repository-inventory.csx` uses the installed public `FileHandlingFacade` only to test whether one explicit controller repository path exists and to list its immediate child directories and files. Names are emitted as UTF-8 Base64 markers so receipts can parse them without locale ambiguity. The script never creates, uploads, moves or deletes controller content and never accepts credentials.

The fixed first candidate for OfficeLite is `KRC:\R1\Program`. It must pass an unreachable-address negative control before a live bounded OfficeLite lifecycle may query it. Exit `42` is a typed connection failure; exit `43` means the controller was reached but the requested repository path is absent. Neither result is native KSS compilation evidence.

## Isolated native KSS selection diagnostics

`krl-native-diagnostic.csx` is the fixed, no-credential transaction used only inside a Lab-owned OfficeLite lifecycle. It refuses a pre-existing transaction directory or a selected Robot interpreter, uploads the fixed valid/intentional-failure fixture pairs into one new isolated controller directory, calls only Robot-interpreter `Select`/`Deselect`, reads `RuntimeManagerFacade` errors and then deletes the four files plus directory in `finally`.

The script never calls `Start`, `Run`, `Reset`, `Stop`, motion, project deployment or activation. A valid selection with zero related KSS errors and an invalid selection with an attributable error are syntax/selection evidence only; execution and motion remain outside this operation. Exit `47` means cleanup could not be verified and requires the exact protected recovery procedure before any retry.

## Local WVS option inventory

`project-option-inventory.csx` is a hash-pinned, no-controller probe for one existing WVS source. WorkVisual's `automaticUpdate=true` path rewrites the file it opens even when the solution is closed with `Close(false)`, so the probe first creates a uniquely named temporary WVS, opens and inventories only that copy, closes it without saving, deletes it, and verifies that the caller-pinned source hash is unchanged. It records project options separately from the runner's locally installed option packages. It never calls deployment, target-change, save or activation APIs. Exit `43` rejects source drift, `47` rejects source mutation and `48` rejects temporary-copy cleanup failure.

`exact-c01-virtual-motion-profile.csx` is the fixed offline builder for the 2026-09-07 exact C01 virtual-motion profile. It accepts only the pinned protected physical WVS, clean OfficeLite `KRC_IO.xml` and audited STEU MADA hashes, requires a create-new output, removes exactly the two out-of-scope LDD/PROFINET options plus five PNIO files, and preserves the source WVS hash. The only accepted motion-enable mapping in its MADA input is `$MOVE_ENABLE=$IN[1025]`; `$IN[505]` is rejected. The script has no controller, deployment, activation, VM, credential or license surface. Its logged in-process WVS hash is diagnostic only; acceptance must hash the output after `wvsr` exits and independently extract/compare the project.

Accepted local evidence `WP5K-20260831-PROJECT-OPTION-INVENTORY-02` observes zero options in the current OfficeLite KR3 project and exactly `KUKA.PROFINET S 6.0.0` in the controller-targeted minimal C01 project, with no residual temporary files or vendor process. This is a target-reconciliation observation, not permission to install a KOP or accept a different option set.

## Exact-profile activation diagnosis

`project-activation-status.csx` is read-only. It checks whether an activation is in progress, independently resolves the named staged project when there is no active transaction, and reports the vendor activation state/log without installing or activating anything.

`runtime-message-inventory.csx` is read-only. It snapshots the current KSS/runtime message window so a WorkVisual error can be correlated with controller messages without selecting or starting a program.

`project-activation-prepare-diagnostic.csx` is a closed diagnostic artifact, not an activation mechanism. It uses reflection against one private WorkVisual field to disable only the client-side keep-alive monitor, calls `PrepareInstallation()` and attempts `Rollback()` in `finally`; it never calls install or activate. Its completed experiment proved that bypassing the client watchdog still produced the controller communication fault. Do not rerun it as a workaround and do not expose it through CLI, MCP, Skill or plugin interfaces.

`project-activation-phased.csx` is a state-changing, authorization-gated diagnostic implementation retained for reproducibility. It requires an explicit authorization reference and separates prepare/install/activate evidence, but it must not be run before OfficeLite completes the local `ExpertModeTransition`. Prefer the supported WorkVisual activation wizard for the next attempt; never use this script to bypass smartHMI user rights or confirmation prompts.

`project-activation-rollback.csx` is an authorization-gated recovery probe. It is idempotent when no transaction exists, refuses to roll back a differently named pending project, invokes only `ActivationFacade.Rollback()` for the exact authorized staged project and polls until `IsActivationInProgress` is false. It never prepares, installs or activates a project and remains an internal recovery boundary rather than a plugin operation.

The attributable supported-path result is recorded in `../../docs/audits/OFFICELITE_EXACT_C01_ACTIVATION_DIAGNOSIS_20260829.md`: WorkVisual reaches the isolated controller and rejects activation because the local OfficeLite user must be at least `Expert`. Current program-scoped WorkVisual firewall rules are inbound `Allow`; firewall mutation and physical-controller reconnection are not on the activation critical path.

## Read-only expert-mode transition readback

`expert-mode-readback.csx` reads only `$MODE_OP`, `$USER_LEVEL`, `$DRIVES_ON`, `$PRO_STATE1` and `$PRO_MODE1` from a caller-supplied isolated OfficeLite endpoint. It does not write variables, change modes, enter a transition input, invoke KSS, upload files or activate projects. It also emits a semantic role and an `AT_LEAST_EXPERT` decision using the hash-bound installed KSS role values: `5=Operator`, `10=Programmer`, `20=ExpertProgrammer`, `27=SafetyRecovery`, `29=SafetyMaintenance`, `30=KrcAdministrator`. A successful process exit proves only that current values were read; activation may proceed only when `USER_LEVEL_AT_LEAST_EXPERT=True` in the same live session.
