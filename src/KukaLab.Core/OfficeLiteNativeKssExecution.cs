using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class OfficeLiteNativeKssExecutionContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.officelite-native-kss-execution-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const string EmbeddedScriptResource = "KukaLab.Probes.WorkVisual.KssBoundedExecution.csx";
    public const string EmbeddedScriptSha256 = "D1BCDFAB2BA8BA591FDF5BC385D0791F07CA4D0513E58BDECBAF476C8F1B0740";
    public const string TransactionRoot = @"KRC:\R1\Program\KLAB_W4AG";
    public const string DefaultSnapshotName = "KLAB-ITEM9-CLEAN";
    public const string FailureMarker = "KSS_BOUNDED_EXECUTION_FAILED=";
    public const int ExpectedStartCount = 2;
    public const int ExpectedInvalidErrorNumber = 2137;
    public const int ExpectedInvalidErrorLine = 11;
    public const int ExpectedInvalidErrorColumn = 6;
    public const double ExpectedRelativeXMillimeters = 10.0;

    public static readonly IReadOnlyDictionary<string, string> FixtureRelativePaths =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["valid-src"] = "fixtures/minimal-officelite-kr3-execution-valid/controller-files/LAB_OL_KR3.src",
            ["valid-dat"] = "fixtures/minimal-officelite-kr3-execution-valid/controller-files/LAB_OL_KR3.dat",
            ["invalid-src"] = "fixtures/minimal-missing-target-invalid/controller-files/LAB_MISSING_TARGET.src",
            ["invalid-dat"] = "fixtures/minimal-missing-target-invalid/controller-files/LAB_MISSING_TARGET.dat"
        };

    public static readonly IReadOnlyDictionary<string, string> FixtureSha256 =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["valid-src"] = "D274FC1C27E94021F2FA557104DAC526D4378CC91B89CABFC101FE49CC47B300",
            ["valid-dat"] = "114791B043C7AE196A12E2816CC39BBA4BDC0658A92AEF1109265BBA2E9BE8B2",
            ["invalid-src"] = "C39DAAA3C5FC40CAF978771C31913AF25C1C5A7C3E1F3D4197D09C95F9C3CA80",
            ["invalid-dat"] = "140B95C2EE526284E532373EBFC5701A705F50612F6CBD27726036DF1EA2B431"
        };

    internal static readonly IReadOnlyList<string> RequiredUnsupportedGaps =
    [
        "The bounded execution is exact for the isolated OfficeLite #KR3R540 C4SR profile only; it is not KR 210 R2700-2 workcell evidence.",
        "KUKA.Sim axes, TCP path, collision and cycle-time equivalence remain outside this OfficeLite-only receipt.",
        "No physical controller connection, physical motion, safety qualification, load calibration or license operation is performed."
    ];
}

public sealed record OfficeLiteNativeKssExecutionRequest
{
    public required OfficeLiteCycleRequest OfficeLite { get; init; }
    public required string RunnerPath { get; init; }
    public required string EvidenceDirectory { get; init; }
    public required string LabRoot { get; init; }
    public string SnapshotName { get; init; } = OfficeLiteNativeKssExecutionContract.DefaultSnapshotName;
    public int RunnerTimeoutSeconds { get; init; } = 120;

    public static OfficeLiteNativeKssExecutionRequest CreateDefault(
        string assetRoot,
        string labRoot,
        string evidenceDirectory,
        string? vmrunPath = null,
        string? vmxPath = null,
        string? runnerPath = null,
        string? guestIpAddress = null,
        string? snapshotName = null,
        int readinessTimeoutSeconds = 240,
        int serviceObservationSeconds = 180,
        int runnerTimeoutSeconds = 120)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(labRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceDirectory);
        var fullAssetRoot = Path.GetFullPath(assetRoot);
        var defaultVmx = Path.Combine(
            fullAssetRoot,
            "OfficeLite-Work",
            "8.7.8-build04",
            "runs",
            "kr210-c01-item9",
            "KR210-C01-Item9.vmx");
        var officeLite = OfficeLiteCycleRequest.CreateDefault(
            fullAssetRoot,
            vmrunPath,
            guestIpAddress,
            readinessTimeoutSeconds: readinessTimeoutSeconds) with
        {
            VmxPath = Path.GetFullPath(vmxPath ?? defaultVmx),
            DiagnoseWorkVisualServices = true,
            ServiceObservationSeconds = serviceObservationSeconds
        };
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return new OfficeLiteNativeKssExecutionRequest
        {
            OfficeLite = officeLite,
            RunnerPath = Path.GetFullPath(runnerPath
                ?? Path.Combine(programFilesX86, "KUKA", "WorkVisual 6.0", "wvsr.exe")),
            EvidenceDirectory = Path.GetFullPath(evidenceDirectory),
            LabRoot = Path.GetFullPath(labRoot),
            SnapshotName = string.IsNullOrWhiteSpace(snapshotName)
                ? OfficeLiteNativeKssExecutionContract.DefaultSnapshotName
                : snapshotName,
            RunnerTimeoutSeconds = runnerTimeoutSeconds
        };
    }
}

public sealed record NativeKssExecutionTraceSample
{
    public int Sequence { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string Line { get; init; } = string.Empty;
    public string Axis { get; init; } = string.Empty;
    public string Pose { get; init; } = string.Empty;
    public string ProgramState { get; init; } = string.Empty;
    public string PeriReady { get; init; } = string.Empty;
    public string CouldStartMotion { get; init; } = string.Empty;
}

public sealed record NativeKssCartesianPose
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
    public double A { get; init; }
    public double B { get; init; }
    public double C { get; init; }
}

public sealed record NativeKssExecutionObservation
{
    public bool Attempted { get; init; }
    public string RobotStateBefore { get; init; } = string.Empty;
    public string RobotSelectedBefore { get; init; } = string.Empty;
    public bool UploadVerified { get; init; }
    public bool ValidSelectSucceeded { get; init; }
    public bool ValidAccepted { get; init; }
    public List<NativeKssDiagnosticFinding> ValidErrors { get; init; } = [];
    public bool GoConfirmed { get; init; }
    public int StartCount { get; init; }
    public List<string> StartExceptions { get; init; } = [];
    public bool ValidCompleted { get; init; }
    public string ValidFinalMode { get; init; } = string.Empty;
    public string ValidFinalState { get; init; } = string.Empty;
    public List<NativeKssExecutionTraceSample> Trace { get; init; } = [];
    public NativeKssCartesianPose? InitialPose { get; init; }
    public NativeKssCartesianPose? FinalPose { get; init; }
    public bool InvalidRejected { get; init; }
    public List<NativeKssDiagnosticFinding> InvalidErrors { get; init; } = [];
    public bool CleanupVerified { get; init; }
}

public sealed record OfficeLiteNativeKssExecutionReceipt
{
    public string SchemaIdentity { get; init; } = OfficeLiteNativeKssExecutionContract.ReceiptSchemaIdentity;
    public int SchemaVersion { get; init; } = OfficeLiteNativeKssExecutionContract.ReceiptSchemaVersion;
    public required OfficeLiteNativeKssExecutionPayload Payload { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record OfficeLiteNativeKssExecutionPayload
{
    public string ReceiptId { get; init; } = string.Empty;
    public string AttemptId { get; init; } = string.Empty;
    public string CoreVersion { get; init; } = FixtureContract.CoreVersion;
    public string CoreAssemblySha256 { get; init; } = string.Empty;
    public RuntimeEnvironment Runtime { get; init; } = new();
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset CompletedAtUtc { get; init; }
    public long DurationMilliseconds { get; init; }
    public EnvironmentTerminalClassification TerminalClassification { get; init; }
    public string RunnerPath { get; init; } = string.Empty;
    public string EvidenceDirectory { get; init; } = string.Empty;
    public string EmbeddedScriptSha256 { get; init; } = string.Empty;
    public string TransactionRoot { get; init; } = OfficeLiteNativeKssExecutionContract.TransactionRoot;
    public string SnapshotName { get; init; } = OfficeLiteNativeKssExecutionContract.DefaultSnapshotName;
    public VmrunCommandObservation SnapshotInventoryCommand { get; init; } = new();
    public VmrunCommandObservation SnapshotRestoreCommand { get; init; } = new();
    public VmrunCommandObservation FinalVmListCommand { get; init; } = new();
    public bool SnapshotRestored { get; init; }
    public OfficeLiteCycleReceipt LifecycleReceipt { get; init; } = null!;
    public NativeKssRunnerCommandObservation LiveCommand { get; init; } = new();
    public NativeKssExecutionObservation Execution { get; init; } = new();
    public NativeKssStatus NativeKssStatus { get; init; } = NativeKssStatus.NotRun;
    public bool CredentialsUsed { get; init; }
    public bool ProgramStartRequested { get; init; }
    public bool ProgramRunRequested { get; init; }
    public bool VirtualMotionRequested { get; init; }
    public bool PhysicalMotionRequested { get; init; }
    public bool ControllerMutationPerformed { get; init; }
    public bool ControllerStateRestored { get; init; }
    public bool EnvironmentReusable { get; init; }
    public List<EnvironmentCheck> Checks { get; init; } = [];
    public List<EnvironmentFileObservation> Files { get; init; } = [];
    public List<string> SideEffects { get; init; } = [];
    public List<string> UnsupportedGaps { get; init; } = [];
}

public sealed record OfficeLiteNativeKssExecutionOutcome(OfficeLiteNativeKssExecutionReceipt Receipt)
{
    public bool Succeeded => Receipt.Payload.TerminalClassification == EnvironmentTerminalClassification.Ready;
    public int ExitCode => Receipt.Payload.TerminalClassification switch
    {
        EnvironmentTerminalClassification.Ready => 0,
        EnvironmentTerminalClassification.Failed => 2,
        _ => 3
    };
}

public sealed class OfficeLiteNativeKssExecutionRunner
{
    private readonly IOfficeLiteHostPlatform _officeLitePlatform;
    private readonly OfficeLiteCycleRunner _officeLiteRunner;
    private readonly IWorkVisualRunnerPlatform _workVisualPlatform;
    private readonly TimeProvider _timeProvider;

    public OfficeLiteNativeKssExecutionRunner()
    {
        _officeLitePlatform = new OfficeLiteHostPlatform();
        _timeProvider = TimeProvider.System;
        _officeLiteRunner = new OfficeLiteCycleRunner(_officeLitePlatform, _timeProvider);
        _workVisualPlatform = new WorkVisualRunnerPlatform();
    }

    internal OfficeLiteNativeKssExecutionRunner(
        IOfficeLiteHostPlatform officeLitePlatform,
        IWorkVisualRunnerPlatform workVisualPlatform,
        TimeProvider timeProvider)
    {
        _officeLitePlatform = officeLitePlatform;
        _officeLiteRunner = new OfficeLiteCycleRunner(officeLitePlatform, timeProvider);
        _workVisualPlatform = workVisualPlatform;
        _timeProvider = timeProvider;
    }

    public OfficeLiteNativeKssExecutionOutcome Run(OfficeLiteNativeKssExecutionRequest request, string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAttemptId(attemptId);
        if (request.RunnerTimeoutSeconds is < 1 or > 300)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "WorkVisual runner timeout must be from 1 through 300 seconds.");
        }

        if (string.IsNullOrWhiteSpace(request.SnapshotName)
            || !Regex.IsMatch(request.SnapshotName, "^[A-Za-z0-9][A-Za-z0-9._-]{0,95}$"))
        {
            throw new ArgumentException("Snapshot name must be a portable 1-96 character identifier.", nameof(request));
        }

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var checks = new List<EnvironmentCheck>();
        var files = new List<EnvironmentFileObservation>();
        var sideEffects = new List<string>();
        var runnerPath = Path.GetFullPath(request.RunnerPath);
        var evidenceDirectory = Path.GetFullPath(request.EvidenceDirectory);
        var labRoot = Path.GetFullPath(request.LabRoot);
        var scriptHash = OfficeLiteNativeKssExecutionContract.EmbeddedScriptSha256;
        var live = new NativeKssRunnerCommandObservation { CleanupVerified = true };
        var execution = new NativeKssExecutionObservation();
        OfficeLiteCycleReceipt? lifecycleReceipt = null;
        var fixturePaths = new Dictionary<string, string>(StringComparer.Ordinal);
        var snapshotInventory = new VmrunCommandObservation();
        var snapshotRestore = new VmrunCommandObservation();
        var finalVmList = new VmrunCommandObservation();
        var snapshotRestored = false;
        var runnerCleanupVerified = true;

        if (!OperatingSystem.IsWindows())
        {
            checks.Add(Blocked("host-os", "OfficeLite and WorkVisual Script Runner require Windows."));
            return Complete();
        }

        if (!ObserveFile("workvisual-script-runner", runnerPath, true, null, files, checks))
        {
            return Complete();
        }

        foreach (var declaration in OfficeLiteNativeKssExecutionContract.FixtureRelativePaths)
        {
            var fullPath = Path.GetFullPath(Path.Combine(labRoot, declaration.Value.Replace('/', Path.DirectorySeparatorChar)));
            var expectedRoot = labRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase)
                || !ObserveFile($"fixture-{declaration.Key}", fullPath, false,
                    OfficeLiteNativeKssExecutionContract.FixtureSha256[declaration.Key], files, checks))
            {
                return Complete();
            }

            fixturePaths[declaration.Key] = fullPath;
        }

        string scriptPath;
        try
        {
            if (Directory.Exists(evidenceDirectory))
            {
                checks.Add(Failed("evidence-directory", "Evidence directory already exists; create-new semantics refused reuse."));
                return Complete();
            }

            Directory.CreateDirectory(evidenceDirectory);
            sideEffects.Add($"CreateEvidenceDirectory:{evidenceDirectory}");
            scriptPath = Path.Combine(evidenceDirectory, $"{attemptId}.kss-bounded-execution.csx");
            var scriptBytes = ReadEmbeddedScript();
            scriptHash = Convert.ToHexString(SHA256.HashData(scriptBytes));
            if (!string.Equals(scriptHash, OfficeLiteNativeKssExecutionContract.EmbeddedScriptSha256, StringComparison.Ordinal))
            {
                checks.Add(Failed("native-kss-script", "Embedded bounded-execution script hash is not the pinned value."));
                return Complete();
            }

            WriteNew(scriptPath, scriptBytes);
            sideEffects.Add($"CreateProbeScript:{scriptPath}");
            files.Add(ObserveExistingFile("workvisual-kss-bounded-execution-script", scriptPath, false));
            checks.Add(Passed("native-kss-script", "Pinned bounded-execution script was materialized as create-new evidence."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            checks.Add(Failed("evidence-directory", $"Evidence preparation failed safely: {exception.Message}"));
            return Complete();
        }

        snapshotInventory = RunVmrun(
            "list-snapshots-before",
            request.OfficeLite,
            ["-T", "ws", "listSnapshots", request.OfficeLite.VmxPath]);
        if (!CommandSucceeded(snapshotInventory)
            || !snapshotInventory.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(request.SnapshotName, StringComparer.Ordinal))
        {
            checks.Add(Blocked("named-snapshot", "The required exact named snapshot was not enumerated; OfficeLite was not started."));
            return Complete();
        }

        checks.Add(Passed("named-snapshot", "The required exact named snapshot was enumerated before OfficeLite startup."));
        var lifecycle = _officeLiteRunner.Run(
            request.OfficeLite,
            $"{attemptId}-lifecycle",
            context => Execute(context.GuestAddress, scriptPath));
        lifecycleReceipt = lifecycle.Receipt;

        snapshotRestore = RunVmrun(
            "restore-named-snapshot",
            request.OfficeLite,
            ["-T", "ws", "revertToSnapshot", request.OfficeLite.VmxPath, request.SnapshotName]);
        finalVmList = RunVmrun(
            "list-after-snapshot-restore",
            request.OfficeLite,
            ["-T", "ws", "list"]);
        snapshotRestored = CommandSucceeded(snapshotRestore)
            && CommandSucceeded(finalVmList)
            && !ContainsRunningVmx(finalVmList.StandardOutput, request.OfficeLite.VmxPath);
        if (snapshotRestored)
        {
            sideEffects.Add($"RestoreNamedSnapshot:{request.SnapshotName}");
            checks.Add(Passed("snapshot-restore", "The isolated VM returned to the exact named snapshot and remained stopped."));
        }
        else
        {
            checks.Add(Failed("snapshot-restore", "Named snapshot restoration or final stopped-state verification failed."));
        }

        if (lifecycleReceipt.Payload.TerminalClassification == EnvironmentTerminalClassification.Ready
            && IsAcceptedExecution(execution)
            && snapshotRestored
            && checks.All(check => check.Status is not EnvironmentCheckStatus.Failed and not EnvironmentCheckStatus.Blocked))
        {
            checks.Add(Passed("native-kss-execution", "Native KSS completed the fixed KR3 PTP plus 10 mm LIN_REL path and rejected the fixed invalid program."));
        }
        else if (!execution.Attempted)
        {
            checks.Add(Blocked("native-kss-execution", "The bounded execution did not run because OfficeLite readiness was not established."));
        }
        else
        {
            checks.Add(Failed("native-kss-execution", "The bounded execution did not satisfy all deterministic positive/negative assertions."));
        }

        checks.Add(lifecycleReceipt.Payload.CleanShutdownVerified && execution.CleanupVerified && snapshotRestored
            ? Passed("environment-cleanup", "Controller transaction deletion, soft shutdown, snapshot restore and zero running VM state were verified.")
            : Failed("environment-cleanup", "The controller, VM or snapshot cleanup chain was incomplete."));
        return Complete();

        void Execute(string address, string materializedScriptPath)
        {
            WorkVisualProcessResult result;
            try
            {
                result = _workVisualPlatform.Run(
                    runnerPath,
                    [
                        "-executescript",
                        $"-scriptpath={materializedScriptPath}",
                        $"-address={address}",
                        $"-validsrc={fixturePaths["valid-src"]}",
                        $"-validdat={fixturePaths["valid-dat"]}",
                        $"-invalidsrc={fixturePaths["invalid-src"]}",
                        $"-invaliddat={fixturePaths["invalid-dat"]}",
                        $"-transactionroot={OfficeLiteNativeKssExecutionContract.TransactionRoot}"
                    ],
                    evidenceDirectory,
                    TimeSpan.FromSeconds(request.RunnerTimeoutSeconds));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
            {
                result = new WorkVisualProcessResult(-1, false, true, 0, string.Empty, $"{exception.GetType().Name}: {exception.Message}");
            }

            live = ToObservation(result);
            runnerCleanupVerified &= result.CleanupVerified;
            PreserveRawEvidence(result);
            execution = ParseExecution(result.StandardOutput);
            sideEffects.Add($"CreateControllerDirectory:{OfficeLiteNativeKssExecutionContract.TransactionRoot}");
            sideEffects.Add("UploadFixedFixtureFiles:4");
            sideEffects.Add($"InvokeHighLevelInterpreterStart:{execution.StartCount}");
            sideEffects.Add($"DeleteControllerDirectory:{OfficeLiteNativeKssExecutionContract.TransactionRoot}");

            if (!result.CleanupVerified)
            {
                checks.Add(Failed("workvisual-runner-cleanup", "WorkVisual Script Runner cleanup could not be verified."));
            }
            else if (result.ExitCode == 51 || !execution.CleanupVerified)
            {
                checks.Add(Failed("controller-transaction-cleanup", "The isolated controller transaction could not be proven deleted."));
            }
            else if (result.TimedOut)
            {
                checks.Add(Blocked("live-execution", "The native-KSS execution exceeded its bounded timeout."));
            }
            else if (result.ExitCode != 0)
            {
                checks.Add(Failed("live-execution", $"The native-KSS execution returned exit code {result.ExitCode}."));
            }
            else if (!IsAcceptedExecution(execution))
            {
                checks.Add(Failed("live-execution", "The live execution did not satisfy completion, TCP delta and invalid-rejection assertions."));
            }
            else
            {
                checks.Add(Passed("live-execution", "The native-KSS positive execution and invalid differential completed."));
            }
        }

        void PreserveRawEvidence(WorkVisualProcessResult result)
        {
            var stdoutPath = Path.Combine(evidenceDirectory, $"{attemptId}.live.stdout.log");
            var stderrPath = Path.Combine(evidenceDirectory, $"{attemptId}.live.stderr.log");
            WriteNew(stdoutPath, Encoding.UTF8.GetBytes(result.StandardOutput));
            WriteNew(stderrPath, Encoding.UTF8.GetBytes(result.StandardError));
            sideEffects.Add($"CreateRawStdout:{stdoutPath}");
            sideEffects.Add($"CreateRawStderr:{stderrPath}");
            files.Add(ObserveExistingFile("workvisual-live-stdout", stdoutPath, false));
            files.Add(ObserveExistingFile("workvisual-live-stderr", stderrPath, false));
        }

        OfficeLiteNativeKssExecutionOutcome Complete()
        {
            stopwatch.Stop();
            lifecycleReceipt ??= CreateUnavailableLifecycleReceipt(request.OfficeLite, attemptId, startedAt);
            var terminal = checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
                ? EnvironmentTerminalClassification.Failed
                : checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked)
                    || lifecycleReceipt.Payload.TerminalClassification != EnvironmentTerminalClassification.Ready
                    ? EnvironmentTerminalClassification.Blocked
                    : EnvironmentTerminalClassification.Ready;
            var accepted = terminal == EnvironmentTerminalClassification.Ready
                && IsAcceptedExecution(execution)
                && snapshotRestored;
            var payload = new OfficeLiteNativeKssExecutionPayload
            {
                ReceiptId = $"officelite-native-kss-execution-{attemptId}",
                AttemptId = attemptId,
                CoreAssemblySha256 = ComputeFileSha256(typeof(OfficeLiteNativeKssExecutionRunner).Assembly.Location),
                Runtime = new RuntimeEnvironment
                {
                    OsDescription = RuntimeInformation.OSDescription,
                    FrameworkDescription = RuntimeInformation.FrameworkDescription,
                    ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
                },
                StartedAtUtc = startedAt,
                CompletedAtUtc = _timeProvider.GetUtcNow(),
                DurationMilliseconds = Math.Max(0, stopwatch.ElapsedMilliseconds),
                TerminalClassification = terminal,
                RunnerPath = runnerPath,
                EvidenceDirectory = evidenceDirectory,
                EmbeddedScriptSha256 = scriptHash,
                TransactionRoot = OfficeLiteNativeKssExecutionContract.TransactionRoot,
                SnapshotName = request.SnapshotName,
                SnapshotInventoryCommand = snapshotInventory,
                SnapshotRestoreCommand = snapshotRestore,
                FinalVmListCommand = finalVmList,
                SnapshotRestored = snapshotRestored,
                LifecycleReceipt = lifecycleReceipt,
                LiveCommand = live,
                Execution = execution,
                NativeKssStatus = accepted ? NativeKssStatus.BoundedExecutionValidated : NativeKssStatus.NotRun,
                CredentialsUsed = false,
                ProgramStartRequested = execution.StartCount > 0,
                ProgramRunRequested = execution.Attempted,
                VirtualMotionRequested = execution.StartCount > 0,
                PhysicalMotionRequested = false,
                ControllerMutationPerformed = execution.Attempted,
                ControllerStateRestored = execution.CleanupVerified && snapshotRestored,
                EnvironmentReusable = runnerCleanupVerified && execution.CleanupVerified
                    && lifecycleReceipt.Payload.CleanShutdownVerified && snapshotRestored,
                Checks = checks,
                Files = files,
                SideEffects = sideEffects,
                UnsupportedGaps = OfficeLiteNativeKssExecutionContract.RequiredUnsupportedGaps.ToList()
            };
            var receipt = new OfficeLiteNativeKssExecutionReceipt
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
            };
            return new OfficeLiteNativeKssExecutionOutcome(receipt);
        }
    }

    internal static NativeKssExecutionObservation ParseExecution(string output)
    {
        var trace = ReadTrace(output).OrderBy(item => item.Sequence).ToList();
        var poses = trace
            .Select(item => TryParsePose(item.Pose, out var pose) ? pose : null)
            .Where(item => item is not null)
            .Cast<NativeKssCartesianPose>()
            .ToList();
        return new NativeKssExecutionObservation
        {
            Attempted = output.Contains("KSS_BOUNDED_EXECUTION_BEGIN=True", StringComparison.Ordinal),
            RobotStateBefore = ReadMarker(output, "ROBOT_STATE_BEFORE") ?? string.Empty,
            RobotSelectedBefore = DecodeBase64(ReadMarker(output, "ROBOT_SELECTED_BEFORE")),
            UploadVerified = ReadBoolMarker(output, "UPLOAD_VERIFIED"),
            ValidSelectSucceeded = ReadBoolMarker(output, "VALID_SELECT_SUCCEEDED"),
            ValidAccepted = ReadBoolMarker(output, "VALID_ACCEPTED"),
            ValidErrors = ReadErrors(output, "VALID_ERROR").ToList(),
            GoConfirmed = ReadBoolMarker(output, "GO_CONFIRMED"),
            StartCount = int.TryParse(ReadMarker(output, "START_COUNT"), out var startCount) ? startCount : 0,
            StartExceptions = ReadMarkerValues(output, "START_EXCEPTION").Select(DecodeBase64).ToList(),
            ValidCompleted = ReadBoolMarker(output, "VALID_COMPLETED_FINAL"),
            ValidFinalMode = ReadMarker(output, "VALID_FINAL_MODE") ?? string.Empty,
            ValidFinalState = ReadMarker(output, "VALID_FINAL_STATE") ?? string.Empty,
            Trace = trace,
            InitialPose = poses.FirstOrDefault(),
            FinalPose = poses.LastOrDefault(),
            InvalidRejected = ReadBoolMarker(output, "INVALID_REJECTED_FINAL"),
            InvalidErrors = ReadErrors(output, "INVALID_ERROR").ToList(),
            CleanupVerified = ReadBoolMarker(output, "CLEANUP_VERIFIED")
        };
    }

    internal static bool IsAcceptedExecution(NativeKssExecutionObservation observation)
    {
        if (!observation.Attempted || !observation.UploadVerified || !observation.ValidSelectSucceeded
            || !observation.ValidAccepted || observation.ValidErrors.Count != 0 || !observation.GoConfirmed
            || observation.StartCount != OfficeLiteNativeKssExecutionContract.ExpectedStartCount
            || observation.StartExceptions.Count != 0 || !observation.ValidCompleted
            || !string.Equals(observation.ValidFinalMode, "Go", StringComparison.Ordinal)
            || !string.Equals(observation.ValidFinalState, "End", StringComparison.Ordinal)
            || !observation.InvalidRejected || !observation.CleanupVerified
            || observation.InitialPose is null || observation.FinalPose is null)
        {
            return false;
        }

        var initial = observation.InitialPose;
        var final = observation.FinalPose;
        const double tolerance = 0.001;
        return Math.Abs((final.X - initial.X) - OfficeLiteNativeKssExecutionContract.ExpectedRelativeXMillimeters) <= tolerance
            && Math.Abs(final.Y - initial.Y) <= tolerance
            && Math.Abs(final.Z - initial.Z) <= tolerance
            && Math.Abs(final.A - initial.A) <= tolerance
            && Math.Abs(final.B - initial.B) <= tolerance
            && Math.Abs(final.C - initial.C) <= tolerance
            && observation.Trace.Any(item => string.Equals(item.State, "Stop", StringComparison.Ordinal))
            && observation.Trace.Last().State == "End"
            && observation.InvalidErrors.Any(error => error.ErrorNumber == OfficeLiteNativeKssExecutionContract.ExpectedInvalidErrorNumber
                && error.Line == OfficeLiteNativeKssExecutionContract.ExpectedInvalidErrorLine
                && error.Column == OfficeLiteNativeKssExecutionContract.ExpectedInvalidErrorColumn);
    }

    private static IEnumerable<NativeKssExecutionTraceSample> ReadTrace(string output)
    {
        foreach (var value in ReadMarkerValues(output, "TRACE"))
        {
            var parts = value.Split('|');
            if (parts.Length != 10 || !int.TryParse(parts[0], out var sequence)) continue;
            yield return new NativeKssExecutionTraceSample
            {
                Sequence = sequence,
                Label = parts[1],
                Mode = parts[2],
                State = parts[3],
                Line = DecodeBase64(parts[4]),
                Axis = DecodeBase64(parts[5]),
                Pose = DecodeBase64(parts[6]),
                ProgramState = DecodeBase64(parts[7]),
                PeriReady = DecodeBase64(parts[8]),
                CouldStartMotion = DecodeBase64(parts[9])
            };
        }
    }

    private static bool TryParsePose(string value, out NativeKssCartesianPose? pose)
    {
        pose = null;
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("<", StringComparison.Ordinal)) return false;
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(value, @"\b([XYZABC])\s+(-?\d+(?:\.\d+)?)", RegexOptions.CultureInvariant))
        {
            if (double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                values[match.Groups[1].Value] = number;
            }
        }

        if (new[] { "X", "Y", "Z", "A", "B", "C" }.Any(key => !values.ContainsKey(key))) return false;
        pose = new NativeKssCartesianPose
        {
            X = values["X"], Y = values["Y"], Z = values["Z"],
            A = values["A"], B = values["B"], C = values["C"]
        };
        return true;
    }

    private static IEnumerable<NativeKssDiagnosticFinding> ReadErrors(string output, string marker)
    {
        foreach (var value in ReadMarkerValues(output, marker))
        {
            var parts = value.Split('|');
            if (parts.Length != 6 || !int.TryParse(parts[1], out var number)
                || !int.TryParse(parts[2], out var line) || !int.TryParse(parts[3], out var column)) continue;
            yield return new NativeKssDiagnosticFinding
            {
                Module = DecodeBase64(parts[0]), ErrorNumber = number, Line = line, Column = column,
                Description = DecodeBase64(parts[4]), Parameter = DecodeBase64(parts[5])
            };
        }
    }

    private static IEnumerable<string> ReadMarkerValues(string output, string marker)
    {
        var prefix = marker + "=";
        foreach (var line in output.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal)) yield return trimmed[prefix.Length..];
        }
    }

    private static string? ReadMarker(string output, string name) => ReadMarkerValues(output, name).LastOrDefault();
    private static bool ReadBoolMarker(string output, string name) =>
        bool.TryParse(ReadMarker(output, name), out var value) && value;

    private static string DecodeBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
        catch (FormatException) { return string.Empty; }
    }

    private VmrunCommandObservation RunVmrun(string name, OfficeLiteCycleRequest request, IReadOnlyList<string> arguments)
    {
        try
        {
            var result = _officeLitePlatform.RunVmrun(
                request.VmrunPath,
                arguments,
                TimeSpan.FromSeconds(request.VmrunTimeoutSeconds));
            return new VmrunCommandObservation
            {
                Name = name, ExitCode = result.ExitCode, TimedOut = result.TimedOut,
                DurationMilliseconds = result.DurationMilliseconds,
                StandardOutput = result.StandardOutput, StandardError = result.StandardError
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            return new VmrunCommandObservation
            {
                Name = name, ExitCode = -1, StandardError = $"{exception.GetType().Name}: {exception.Message}"
            };
        }
    }

    private static bool CommandSucceeded(VmrunCommandObservation command) => !command.TimedOut && command.ExitCode == 0;

    private static bool ContainsRunningVmx(string output, string vmxPath)
    {
        var expected = Path.GetFullPath(vmxPath);
        return output.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(line => line.EndsWith(".vmx", StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetFullPath(line), expected, StringComparison.OrdinalIgnoreCase));
    }

    private static NativeKssRunnerCommandObservation ToObservation(WorkVisualProcessResult result) => new()
    {
        Attempted = true, ExitCode = result.ExitCode, TimedOut = result.TimedOut,
        CleanupVerified = result.CleanupVerified, DurationMilliseconds = result.DurationMilliseconds,
        StandardOutputSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.StandardOutput))),
        StandardErrorSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.StandardError)))
    };

    private static byte[] ReadEmbeddedScript()
    {
        using var stream = typeof(OfficeLiteNativeKssExecutionRunner).Assembly.GetManifestResourceStream(
            OfficeLiteNativeKssExecutionContract.EmbeddedScriptResource)
            ?? throw new InvalidOperationException("Embedded native-KSS bounded-execution script was not found.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static void ValidateAttemptId(string attemptId)
    {
        if (string.IsNullOrWhiteSpace(attemptId) || !Regex.IsMatch(attemptId, "^[A-Za-z0-9][A-Za-z0-9._-]{0,95}$"))
        {
            throw new ArgumentException("Attempt ID must be 1-96 portable identifier characters.", nameof(attemptId));
        }
    }

    private static bool ObserveFile(string id, string path, bool includeVersion, string? expectedSha256,
        List<EnvironmentFileObservation> files, List<EnvironmentCheck> checks)
    {
        if (!File.Exists(path))
        {
            files.Add(new EnvironmentFileObservation { Id = id, Path = Path.GetFullPath(path), Exists = false });
            checks.Add(Blocked(id, $"Required file is missing: {path}"));
            return false;
        }

        var observation = ObserveExistingFile(id, path, includeVersion);
        files.Add(observation);
        if (expectedSha256 is not null && !string.Equals(observation.Sha256, expectedSha256, StringComparison.Ordinal))
        {
            checks.Add(Failed(id, $"Pinned file SHA-256 mismatch: {path}"));
            return false;
        }

        checks.Add(Passed(id, expectedSha256 is null ? $"Observed required file: {path}" : $"Pinned file hash accepted: {path}"));
        return true;
    }

    private static EnvironmentFileObservation ObserveExistingFile(string id, string path, bool includeVersion)
    {
        var info = new FileInfo(path);
        return new EnvironmentFileObservation
        {
            Id = id, Path = info.FullName, Exists = true, Bytes = info.Length,
            Sha256 = ComputeFileSha256(info.FullName),
            FileVersion = includeVersion ? FileVersionInfo.GetVersionInfo(info.FullName).FileVersion : null,
            ProductVersion = includeVersion ? FileVersionInfo.GetVersionInfo(info.FullName).ProductVersion : null
        };
    }

    private static void WriteNew(string path, byte[] content)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(content);
    }

    private static OfficeLiteCycleReceipt CreateUnavailableLifecycleReceipt(
        OfficeLiteCycleRequest request, string attemptId, DateTimeOffset observedAt)
    {
        var payload = new OfficeLiteCyclePayload
        {
            ReceiptId = $"officelite-cycle-{attemptId}-not-run", AttemptId = $"{attemptId}-not-run",
            CoreAssemblySha256 = ComputeFileSha256(typeof(OfficeLiteCycleRunner).Assembly.Location),
            Runtime = new RuntimeEnvironment
            {
                OsDescription = RuntimeInformation.OSDescription,
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
            },
            StartedAtUtc = observedAt, CompletedAtUtc = observedAt, DurationMilliseconds = 0,
            TerminalClassification = EnvironmentTerminalClassification.Blocked,
            VmrunPath = Path.GetFullPath(request.VmrunPath),
            Checks = [Blocked("lifecycle", "OfficeLite lifecycle was not started because host-side preconditions failed.")],
            UnsupportedGaps = ["Native KSS bounded execution was not invoked."]
        };
        return new OfficeLiteCycleReceipt
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
    }

    private static EnvironmentCheck Passed(string id, string detail) => new() { Id = id, Status = EnvironmentCheckStatus.Passed, Detail = detail };
    private static EnvironmentCheck Blocked(string id, string detail) => new() { Id = id, Status = EnvironmentCheckStatus.Blocked, Detail = detail };
    private static EnvironmentCheck Failed(string id, string detail) => new() { Id = id, Status = EnvironmentCheckStatus.Failed, Detail = detail };
    private static string ComputeFileSha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}

public sealed record OfficeLiteNativeKssExecutionVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;
    public bool Succeeded { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
    public List<string> Errors { get; init; } = [];
}

public static class OfficeLiteNativeKssExecutionReceiptVerifier
{
    private static readonly Regex Sha256Pattern = new("^[0-9A-F]{64}$", RegexOptions.CultureInvariant);
    private static readonly string[] RequiredFileIds =
    [
        "workvisual-script-runner", "workvisual-kss-bounded-execution-script",
        "workvisual-live-stdout", "workvisual-live-stderr"
    ];

    public static OfficeLiteNativeKssExecutionVerificationResult Verify(OfficeLiteNativeKssExecutionReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (receipt.SchemaIdentity != OfficeLiteNativeKssExecutionContract.ReceiptSchemaIdentity
            || receipt.SchemaVersion != OfficeLiteNativeKssExecutionContract.ReceiptSchemaVersion)
        {
            errors.Add("native-KSS execution receipt schema identity/version is unsupported");
        }

        var payload = receipt.Payload;
        if (!string.Equals(ReceiptSerialization.ComputeCanonicalSha256(payload), receipt.PayloadSha256, StringComparison.Ordinal))
        {
            errors.Add("payload SHA-256 mismatch");
        }

        var accepted = payload.TerminalClassification == EnvironmentTerminalClassification.Ready;
        if (accepted && payload.NativeKssStatus != NativeKssStatus.BoundedExecutionValidated)
        {
            errors.Add("Ready receipt must have BoundedExecutionValidated status");
        }
        else if (!accepted && payload.NativeKssStatus != NativeKssStatus.NotRun)
        {
            errors.Add("non-Ready receipt must retain native KSS status NotRun");
        }

        if (payload.CredentialsUsed || payload.PhysicalMotionRequested)
        {
            errors.Add("credentials and physical motion must remain false");
        }

        if (accepted && (!payload.ProgramStartRequested || !payload.ProgramRunRequested
            || !payload.VirtualMotionRequested || !payload.ControllerMutationPerformed
            || !payload.ControllerStateRestored || !payload.EnvironmentReusable || !payload.SnapshotRestored))
        {
            errors.Add("accepted receipt must bind virtual execution, restoration and reusable environment");
        }

        if (!string.Equals(payload.TransactionRoot, OfficeLiteNativeKssExecutionContract.TransactionRoot, StringComparison.Ordinal)
            || !string.Equals(payload.SnapshotName, OfficeLiteNativeKssExecutionContract.DefaultSnapshotName, StringComparison.Ordinal)
            || !string.Equals(payload.EmbeddedScriptSha256, OfficeLiteNativeKssExecutionContract.EmbeddedScriptSha256, StringComparison.Ordinal))
        {
            errors.Add("fixed transaction root, snapshot name or embedded script hash mismatch");
        }

        if (!Sha256Pattern.IsMatch(payload.CoreAssemblySha256)
            || payload.CompletedAtUtc < payload.StartedAtUtc || payload.DurationMilliseconds < 0)
        {
            errors.Add("core assembly identity or timing is invalid");
        }

        if (accepted && (!payload.LiveCommand.Attempted || payload.LiveCommand.ExitCode != 0
            || payload.LiveCommand.TimedOut || !payload.LiveCommand.CleanupVerified))
        {
            errors.Add("live command must exit zero without timeout or runner residue");
        }

        if (accepted && !OfficeLiteNativeKssExecutionRunner.IsAcceptedExecution(payload.Execution))
        {
            errors.Add("execution observation does not satisfy deterministic PTP/LIN_REL and invalid-rejection assertions");
        }

        if (accepted && payload.Execution.InvalidErrors.Any(error =>
                !error.Module.StartsWith(OfficeLiteNativeKssExecutionContract.TransactionRoot + "\\", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("invalid fixture diagnostic escaped the fixed controller transaction root");
        }

        if (accepted && (!CommandSucceeded(payload.SnapshotInventoryCommand)
            || !CommandSucceeded(payload.SnapshotRestoreCommand)
            || !CommandSucceeded(payload.FinalVmListCommand)
            || (payload.LifecycleReceipt is not null
                && ContainsRunningVmx(payload.FinalVmListCommand.StandardOutput, payload.LifecycleReceipt.Payload.VmxPath))))
        {
            errors.Add("snapshot inventory, restoration or final stopped-state evidence is invalid");
        }

        if (payload.LifecycleReceipt is null)
        {
            errors.Add("OfficeLite lifecycle receipt is required");
        }
        else
        {
            var lifecycle = OfficeLiteCycleReceiptVerifier.Verify(payload.LifecycleReceipt);
            if (!lifecycle.Succeeded || (accepted && !payload.LifecycleReceipt.Payload.CleanShutdownVerified))
            {
                errors.Add("nested OfficeLite lifecycle receipt or clean shutdown is invalid");
            }
        }

        foreach (var id in OfficeLiteNativeKssExecutionContract.FixtureRelativePaths.Keys)
        {
            var observed = payload.Files.Where(file => file.Id == $"fixture-{id}").ToArray();
            if (accepted && (observed.Length != 1 || !observed[0].Exists || !File.Exists(observed[0].Path)
                || !string.Equals(observed[0].Sha256, OfficeLiteNativeKssExecutionContract.FixtureSha256[id], StringComparison.Ordinal)
                || !string.Equals(ComputeFileSha256(observed[0].Path), observed[0].Sha256, StringComparison.Ordinal)))
            {
                errors.Add($"fixture-{id} current bytes do not match the pinned receipt identity");
            }
        }

        foreach (var file in payload.Files.Where(file => file.Exists))
        {
            if (!File.Exists(file.Path) || file.Sha256 is null || !Sha256Pattern.IsMatch(file.Sha256)
                || !string.Equals(ComputeFileSha256(file.Path), file.Sha256, StringComparison.Ordinal))
            {
                errors.Add($"current evidence drifted: {file.Id}");
            }
        }

        if (accepted)
        {
            var evidence = new Dictionary<string, EnvironmentFileObservation>(StringComparer.Ordinal);
            foreach (var id in RequiredFileIds)
            {
                var observed = payload.Files.Where(file => file.Id == id).ToArray();
                if (observed.Length != 1 || !observed[0].Exists) errors.Add($"required evidence must occur exactly once: {id}");
                else evidence[id] = observed[0];
            }

            VerifyRawBindings(payload, evidence, errors);
        }

        if (payload.UnsupportedGaps is null
            || !payload.UnsupportedGaps.SequenceEqual(OfficeLiteNativeKssExecutionContract.RequiredUnsupportedGaps, StringComparer.Ordinal))
        {
            errors.Add("unsupported-gap declarations are incomplete or reordered");
        }

        return new OfficeLiteNativeKssExecutionVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }

    private static void VerifyRawBindings(OfficeLiteNativeKssExecutionPayload payload,
        IReadOnlyDictionary<string, EnvironmentFileObservation> files, List<string> errors)
    {
        if (files.Count != RequiredFileIds.Length) return;
        var script = files["workvisual-kss-bounded-execution-script"];
        var stdout = files["workvisual-live-stdout"];
        var stderr = files["workvisual-live-stderr"];
        if (!string.Equals(script.Sha256, payload.EmbeddedScriptSha256, StringComparison.Ordinal)
            || !string.Equals(stdout.Sha256, payload.LiveCommand.StandardOutputSha256, StringComparison.Ordinal)
            || !string.Equals(stderr.Sha256, payload.LiveCommand.StandardErrorSha256, StringComparison.Ordinal))
        {
            errors.Add("command output hashes are not bound to the observed raw evidence");
        }

        var evidenceRoot = Path.GetFullPath(payload.EvidenceDirectory).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        foreach (var file in files.Values.Where(file => file.Id != "workvisual-script-runner"))
        {
            if (!Path.GetFullPath(file.Path).StartsWith(evidenceRoot, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"raw evidence escaped the declared evidence directory: {file.Id}");
            }
        }

        if (File.Exists(stdout.Path))
        {
            var parsed = OfficeLiteNativeKssExecutionRunner.ParseExecution(File.ReadAllText(stdout.Path));
            if (!string.Equals(ReceiptSerialization.ComputeCanonicalSha256(parsed),
                ReceiptSerialization.ComputeCanonicalSha256(payload.Execution), StringComparison.Ordinal))
            {
                errors.Add("live raw output does not corroborate the execution payload");
            }
        }
    }

    private static bool CommandSucceeded(VmrunCommandObservation command) => !command.TimedOut && command.ExitCode == 0;

    private static bool ContainsRunningVmx(string output, string vmxPath)
    {
        if (string.IsNullOrWhiteSpace(vmxPath)) return true;
        var expected = Path.GetFullPath(vmxPath);
        return output.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(line => line.EndsWith(".vmx", StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetFullPath(line), expected, StringComparison.OrdinalIgnoreCase));
    }

    private static string ComputeFileSha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}

public static class OfficeLiteNativeKssExecutionReceiptWriter
{
    public static string WriteNew(string outputPath, OfficeLiteNativeKssExecutionReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!OfficeLiteNativeKssExecutionReceiptVerifier.Verify(receipt).Succeeded)
        {
            throw new InvalidOperationException("OfficeLite native-KSS execution receipt integrity is invalid.");
        }

        var fullPath = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Receipt output must use the .json extension.", nameof(outputPath));
        }

        var parent = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        Directory.CreateDirectory(parent);
        using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(ReceiptSerialization.ToJson(receipt));
        writer.WriteLine();
        writer.Flush();
        stream.Flush(flushToDisk: true);
        return fullPath;
    }
}
