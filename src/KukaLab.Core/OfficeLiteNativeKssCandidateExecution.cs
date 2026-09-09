using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class NativeKssCandidateExecutionContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.native-kss-candidate-execution-receipt";
    public const int LegacyReceiptSchemaVersion = 1;
    public const int ReceiptSchemaVersion = 2;
    public const string EmbeddedScriptResource = "KukaLab.Probes.WorkVisual.KssCandidateExecution.csx";
    public const string EmbeddedScriptSha256 = "DF0E3F9B87145A760B2C641BF192892D5986ABA885F9D12F6889EE4E0028AF9E";
    public const string DefaultSnapshotName = "KLAB-EXACT-C01-ACTIVE";
    public const string DefaultExpectedProjectName = "deployment.analysis-copy";
}

public enum NativeKssCandidateExecutionDisposition
{
    InfrastructureFailed,
    RejectedByNativeKss,
    Executed
}

public sealed record NativeKssCandidateExecutionRequest
{
    public required OfficeLiteCycleRequest OfficeLite { get; init; }
    public required string RunnerPath { get; init; }
    public required string EvidenceDirectory { get; init; }
    public required NativeKssCandidateSubmissionRequest Submission { get; init; }
    public required string ExactProfileAcceptanceReceiptPath { get; init; }
    public string SnapshotName { get; init; } = NativeKssCandidateExecutionContract.DefaultSnapshotName;
    public string ExpectedProjectName { get; init; } = NativeKssCandidateExecutionContract.DefaultExpectedProjectName;
    public int RunnerTimeoutSeconds { get; init; } = 120;
}

public sealed record NativeKssCandidateObservedProfile
{
    public string RobotType { get; init; } = string.Empty;
    public string KssVersion { get; init; } = string.Empty;
    public string CurrentProjectName { get; init; } = string.Empty;
}

public sealed record NativeKssCandidateExecutionObservation
{
    public bool Attempted { get; init; }
    public bool ProfileMatched { get; init; }
    public bool UploadVerified { get; init; }
    public bool SelectSucceeded { get; init; }
    public List<NativeKssDiagnosticFinding> Diagnostics { get; init; } = [];
    public string Mode { get; init; } = string.Empty;
    public int StartCount { get; init; }
    public List<string> StartExceptions { get; init; } = [];
    public string FinalState { get; init; } = string.Empty;
    public bool Completed { get; init; }
    public bool Rejected { get; init; }
    public bool CleanupVerified { get; init; }
}

public sealed record NativeKssCandidateExecutionReceipt
{
    public string SchemaIdentity { get; init; } = NativeKssCandidateExecutionContract.ReceiptSchemaIdentity;
    public int SchemaVersion { get; init; } = NativeKssCandidateExecutionContract.ReceiptSchemaVersion;
    public required NativeKssCandidateExecutionPayload Payload { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record NativeKssCandidateExecutionPayload
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
    public NativeKssCandidateExecutionDisposition Disposition { get; init; }
    public NativeKssStatus NativeKssStatus { get; init; } = NativeKssStatus.NotRun;
    public OfficeLiteExactProfileAdmission ProfileAdmission { get; init; } = new();
    public required NativeKssCandidateSubmissionPlan Submission { get; init; }
    public NativeKssCandidateObservedProfile ObservedProfile { get; init; } = new();
    public NativeKssCandidateExecutionObservation Execution { get; init; } = new();
    public string RunnerPath { get; init; } = string.Empty;
    public string EvidenceDirectory { get; init; } = string.Empty;
    public string EmbeddedScriptSha256 { get; init; } = string.Empty;
    public string SnapshotName { get; init; } = string.Empty;
    public string ExpectedProjectName { get; init; } = string.Empty;
    public VmrunCommandObservation SnapshotInventoryCommand { get; init; } = new();
    public VmrunCommandObservation SnapshotRestoreCommand { get; init; } = new();
    public VmrunCommandObservation FinalVmListCommand { get; init; } = new();
    public bool SnapshotRestored { get; init; }
    public OfficeLiteCycleReceipt LifecycleReceipt { get; init; } = null!;
    public NativeKssRunnerCommandObservation LiveCommand { get; init; } = new();
    public bool CredentialsUsed { get; init; }
    public bool VirtualMotionRequested { get; init; }
    public bool PhysicalMotionRequested { get; init; }
    public bool ControllerMutationPerformed { get; init; }
    public bool ControllerStateRestored { get; init; }
    public bool EnvironmentReusable { get; init; }
    public List<EnvironmentCheck> Checks { get; init; } = [];
    public List<EnvironmentFileObservation> Files { get; init; } = [];
    public List<string> SideEffects { get; init; } = [];
}

public sealed record NativeKssCandidateExecutionOutcome(NativeKssCandidateExecutionReceipt Receipt)
{
    public bool Succeeded => Receipt.Payload.TerminalClassification == EnvironmentTerminalClassification.Ready;
    public int ExitCode => Receipt.Payload.TerminalClassification switch
    {
        EnvironmentTerminalClassification.Ready => 0,
        EnvironmentTerminalClassification.Failed => 2,
        _ => 3
    };
}

public sealed class OfficeLiteNativeKssCandidateExecutionRunner
{
    private readonly IOfficeLiteHostPlatform _officeLitePlatform;
    private readonly OfficeLiteCycleRunner _officeLiteRunner;
    private readonly IWorkVisualRunnerPlatform _workVisualPlatform;
    private readonly IOfficeLiteExactProfileAdmissionGate _exactProfileAdmissionGate;
    private readonly TimeProvider _timeProvider;

    public OfficeLiteNativeKssCandidateExecutionRunner()
        : this(
            new OfficeLiteHostPlatform(),
            new WorkVisualRunnerPlatform(),
            new OfficeLiteExactProfileAdmissionGate(),
            TimeProvider.System)
    {
    }

    internal OfficeLiteNativeKssCandidateExecutionRunner(
        IOfficeLiteHostPlatform officeLitePlatform,
        IWorkVisualRunnerPlatform workVisualPlatform,
        TimeProvider timeProvider)
        : this(
            officeLitePlatform,
            workVisualPlatform,
            new OfficeLiteExactProfileAdmissionGate(),
            timeProvider)
    {
    }

    internal OfficeLiteNativeKssCandidateExecutionRunner(
        IOfficeLiteHostPlatform officeLitePlatform,
        IWorkVisualRunnerPlatform workVisualPlatform,
        IOfficeLiteExactProfileAdmissionGate exactProfileAdmissionGate,
        TimeProvider timeProvider)
    {
        _officeLitePlatform = officeLitePlatform;
        _officeLiteRunner = new OfficeLiteCycleRunner(officeLitePlatform, timeProvider);
        _workVisualPlatform = workVisualPlatform;
        _exactProfileAdmissionGate = exactProfileAdmissionGate;
        _timeProvider = timeProvider;
    }

    public NativeKssCandidateExecutionOutcome Run(NativeKssCandidateExecutionRequest request, string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePortableId(attemptId, nameof(attemptId));
        ValidatePortableId(request.SnapshotName, nameof(request.SnapshotName));
        ValidatePortableId(request.ExpectedProjectName, nameof(request.ExpectedProjectName));
        if (request.RunnerTimeoutSeconds is < 1 or > 300)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "WorkVisual runner timeout must be from 1 through 300 seconds.");
        }

        var profileAdmission = _exactProfileAdmissionGate.Admit(new OfficeLiteExactProfileAdmissionRequest
        {
            AcceptanceReceiptPath = request.ExactProfileAcceptanceReceiptPath,
            ExpectedProjectName = request.ExpectedProjectName
        });

        var submission = new NativeKssCandidateSubmissionPlanner().Plan(request.Submission);
        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var runnerPath = Path.GetFullPath(request.RunnerPath);
        var evidenceDirectory = Path.GetFullPath(request.EvidenceDirectory);
        var checks = new List<EnvironmentCheck>();
        var files = new List<EnvironmentFileObservation>();
        var sideEffects = new List<string>();
        var live = new NativeKssRunnerCommandObservation { CleanupVerified = true };
        var observedProfile = new NativeKssCandidateObservedProfile();
        var execution = new NativeKssCandidateExecutionObservation();
        OfficeLiteCycleReceipt? lifecycleReceipt = null;
        var snapshotInventory = new VmrunCommandObservation();
        var snapshotRestore = new VmrunCommandObservation();
        var finalVmList = new VmrunCommandObservation();
        var snapshotRestored = false;
        var scriptHash = NativeKssCandidateExecutionContract.EmbeddedScriptSha256;
        checks.Add(Passed(
            "exact-profile-admission",
            $"Exact-C01 profile admission passed through receipt {profileAdmission.AcceptanceReceiptId}."));

        if (!OperatingSystem.IsWindows())
        {
            checks.Add(Blocked("host-os", "OfficeLite and WorkVisual Script Runner require Windows."));
            return Complete();
        }

        if (!ObserveFile("workvisual-script-runner", runnerPath, true, null)) return Complete();
        var sourcePath = ResolveCandidateFile(submission.CandidateRoot, submission.Source, "candidate-source");
        if (sourcePath is null) return Complete();
        var dataPath = ResolveCandidateFile(submission.CandidateRoot, submission.Data, "candidate-data");
        if (dataPath is null) return Complete();

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
            var scriptBytes = ReadEmbeddedScript();
            scriptHash = Convert.ToHexString(SHA256.HashData(scriptBytes));
            if (!string.Equals(scriptHash, NativeKssCandidateExecutionContract.EmbeddedScriptSha256, StringComparison.Ordinal))
            {
                checks.Add(Failed("candidate-execution-script", "Embedded candidate-execution script hash is not pinned."));
                return Complete();
            }

            scriptPath = Path.Combine(evidenceDirectory, $"{attemptId}.kss-candidate-execution.csx");
            WriteNew(scriptPath, scriptBytes);
            files.Add(ObserveExistingFile("workvisual-candidate-execution-script", scriptPath, false));
            sideEffects.Add($"CreateProbeScript:{scriptPath}");
            checks.Add(Passed("candidate-execution-script", "Pinned candidate-execution script was materialized with create-new semantics."));
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
            checks.Add(Blocked("named-snapshot", "The required exact-C01 snapshot was not enumerated; OfficeLite was not started."));
            return Complete();
        }

        checks.Add(Passed("named-snapshot", "The required exact-C01 snapshot was enumerated before startup."));
        var lifecycle = _officeLiteRunner.Run(
            request.OfficeLite,
            $"{attemptId}-lifecycle",
            context => Execute(context.GuestAddress, scriptPath, sourcePath, dataPath));
        lifecycleReceipt = lifecycle.Receipt;

        snapshotRestore = RunVmrun(
            "restore-named-snapshot",
            request.OfficeLite,
            ["-T", "ws", "revertToSnapshot", request.OfficeLite.VmxPath, request.SnapshotName]);
        finalVmList = RunVmrun("list-after-snapshot-restore", request.OfficeLite, ["-T", "ws", "list"]);
        snapshotRestored = CommandSucceeded(snapshotRestore)
            && CommandSucceeded(finalVmList)
            && !ContainsRunningVmx(finalVmList.StandardOutput, request.OfficeLite.VmxPath);
        checks.Add(snapshotRestored
            ? Passed("snapshot-restore", "The isolated VM returned to the exact named snapshot and remained stopped.")
            : Failed("snapshot-restore", "Named snapshot restoration or final stopped-state verification failed."));

        var acceptedDisposition = ClassifyDisposition(execution);
        if (lifecycleReceipt.Payload.TerminalClassification == EnvironmentTerminalClassification.Ready
            && execution.ProfileMatched
            && execution.CleanupVerified
            && acceptedDisposition is NativeKssCandidateExecutionDisposition.Executed or NativeKssCandidateExecutionDisposition.RejectedByNativeKss
            && snapshotRestored)
        {
            checks.Add(Passed("native-kss-candidate", acceptedDisposition == NativeKssCandidateExecutionDisposition.Executed
                ? "The verified candidate completed under exact-C01 native KSS within the Start bound."
                : "Exact-C01 native KSS rejected the verified candidate with correlated diagnostics and no Start command."));
        }
        else if (!execution.Attempted)
        {
            checks.Add(Blocked("native-kss-candidate", "The candidate was not submitted because OfficeLite readiness was not established."));
        }
        else
        {
            checks.Add(Failed("native-kss-candidate", "The exact-C01 candidate transaction did not satisfy profile, bounded execution/rejection, or cleanup requirements."));
        }

        return Complete();

        void Execute(string address, string materializedScriptPath, string materializedSourcePath, string materializedDataPath)
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
                        $"-src={materializedSourcePath}",
                        $"-dat={materializedDataPath}",
                        $"-transactionroot={submission.ControllerTransactionRoot}",
                        $"-maxstarts={submission.MaximumStartCommands}",
                        $"-expectedrobotb64={EncodeBase64(submission.RequiredProfile.MachineDataIdentity)}",
                        $"-expectedkssb64={EncodeBase64(profileAdmission.RuntimeKssVersion)}",
                        $"-expectedprojectb64={EncodeBase64(request.ExpectedProjectName)}"
                    ],
                    evidenceDirectory,
                    TimeSpan.FromSeconds(request.RunnerTimeoutSeconds));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
            {
                result = new WorkVisualProcessResult(-1, false, true, 0, string.Empty, $"{exception.GetType().Name}: {exception.Message}");
            }

            live = ToObservation(result);
            observedProfile = ParseProfile(result.StandardOutput);
            execution = ParseExecution(
                result.StandardOutput,
                submission,
                observedProfile,
                request.ExpectedProjectName,
                profileAdmission.RuntimeKssVersion);
            PreserveRawEvidence(result);
            sideEffects.Add($"CreateControllerDirectory:{submission.ControllerTransactionRoot}");
            sideEffects.Add("UploadVerifiedCandidateFiles:2");
            sideEffects.Add($"InvokeHighLevelInterpreterStart:{execution.StartCount}");
            sideEffects.Add($"DeleteControllerDirectory:{submission.ControllerTransactionRoot}");
            if (!result.CleanupVerified || !execution.CleanupVerified)
            {
                checks.Add(Failed("controller-transaction-cleanup", "The controller transaction or WorkVisual runner cleanup was not verified."));
            }
            else if (result.TimedOut)
            {
                checks.Add(Blocked("live-candidate-execution", "The candidate execution exceeded its bounded timeout."));
            }
            else if (result.ExitCode != 0)
            {
                checks.Add(Failed("live-candidate-execution", $"The candidate execution returned exit code {result.ExitCode}."));
            }
            else
            {
                checks.Add(Passed("live-candidate-execution", "The candidate transaction returned a parseable native KSS disposition."));
            }
        }

        void PreserveRawEvidence(WorkVisualProcessResult result)
        {
            var stdoutPath = Path.Combine(evidenceDirectory, $"{attemptId}.live.stdout.log");
            var stderrPath = Path.Combine(evidenceDirectory, $"{attemptId}.live.stderr.log");
            WriteNew(stdoutPath, Encoding.UTF8.GetBytes(result.StandardOutput));
            WriteNew(stderrPath, Encoding.UTF8.GetBytes(result.StandardError));
            files.Add(ObserveExistingFile("workvisual-live-stdout", stdoutPath, false));
            files.Add(ObserveExistingFile("workvisual-live-stderr", stderrPath, false));
            sideEffects.Add($"CreateRawStdout:{stdoutPath}");
            sideEffects.Add($"CreateRawStderr:{stderrPath}");
        }

        NativeKssCandidateExecutionOutcome Complete()
        {
            stopwatch.Stop();
            lifecycleReceipt ??= CreateUnavailableLifecycleReceipt(request.OfficeLite, attemptId, startedAt);
            var disposition = ClassifyDisposition(execution);
            var ready = checks.All(check => check.Status is not EnvironmentCheckStatus.Failed and not EnvironmentCheckStatus.Blocked)
                && lifecycleReceipt.Payload.TerminalClassification == EnvironmentTerminalClassification.Ready
                && disposition is NativeKssCandidateExecutionDisposition.Executed or NativeKssCandidateExecutionDisposition.RejectedByNativeKss
                && snapshotRestored;
            var terminal = ready
                ? EnvironmentTerminalClassification.Ready
                : checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
                    ? EnvironmentTerminalClassification.Failed
                    : EnvironmentTerminalClassification.Blocked;
            var nativeStatus = ready && disposition == NativeKssCandidateExecutionDisposition.Executed
                ? NativeKssStatus.BoundedExecutionValidated
                : ready && disposition == NativeKssCandidateExecutionDisposition.RejectedByNativeKss
                    ? NativeKssStatus.SyntaxSelectionValidated
                    : NativeKssStatus.NotRun;
            var payload = new NativeKssCandidateExecutionPayload
            {
                ReceiptId = $"native-kss-candidate-execution-{attemptId}",
                AttemptId = attemptId,
                CoreAssemblySha256 = ComputeFileSha256(typeof(OfficeLiteNativeKssCandidateExecutionRunner).Assembly.Location),
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
                Disposition = disposition,
                NativeKssStatus = nativeStatus,
                ProfileAdmission = profileAdmission,
                Submission = submission,
                ObservedProfile = observedProfile,
                Execution = execution,
                RunnerPath = runnerPath,
                EvidenceDirectory = evidenceDirectory,
                EmbeddedScriptSha256 = scriptHash,
                SnapshotName = request.SnapshotName,
                ExpectedProjectName = request.ExpectedProjectName,
                SnapshotInventoryCommand = snapshotInventory,
                SnapshotRestoreCommand = snapshotRestore,
                FinalVmListCommand = finalVmList,
                SnapshotRestored = snapshotRestored,
                LifecycleReceipt = lifecycleReceipt,
                LiveCommand = live,
                CredentialsUsed = false,
                VirtualMotionRequested = execution.StartCount > 0,
                PhysicalMotionRequested = false,
                ControllerMutationPerformed = execution.Attempted,
                ControllerStateRestored = execution.CleanupVerified && snapshotRestored,
                EnvironmentReusable = live.CleanupVerified && execution.CleanupVerified
                    && lifecycleReceipt.Payload.CleanShutdownVerified && snapshotRestored,
                Checks = checks,
                Files = files,
                SideEffects = sideEffects
            };
            var receipt = new NativeKssCandidateExecutionReceipt
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
            };
            return new NativeKssCandidateExecutionOutcome(receipt);
        }

        string? ResolveCandidateFile(string root, RawKrlCandidateFile file, string id)
        {
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(Path.Combine(root, file.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
                || !ObserveFile(id, fullPath, false, file.Sha256))
            {
                return null;
            }

            return fullPath;
        }

        bool ObserveFile(string id, string path, bool includeVersion, string? expectedSha256)
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
                checks.Add(Failed(id, $"Pinned candidate file SHA-256 mismatch: {path}"));
                return false;
            }

            checks.Add(Passed(id, expectedSha256 is null ? $"Observed required file: {path}" : $"Pinned candidate file accepted: {path}"));
            return true;
        }
    }

    internal static NativeKssCandidateObservedProfile ParseProfile(string output) => new()
    {
        RobotType = DecodeBase64(ReadMarker(output, "PROFILE_ROBOT")),
        KssVersion = DecodeBase64(ReadMarker(output, "PROFILE_KSS")),
        CurrentProjectName = DecodeBase64(ReadMarker(output, "PROFILE_PROJECT"))
    };

    internal static NativeKssCandidateExecutionObservation ParseExecution(
        string output,
        NativeKssCandidateSubmissionPlan submission,
        NativeKssCandidateObservedProfile profile,
        string expectedProjectName,
        string expectedKssVersion)
    {
        var diagnostics = ReadErrors(output).ToList();
        var completed = ReadBoolMarker(output, "COMPLETED");
        var rejected = ReadBoolMarker(output, "REJECTED")
            || string.Equals(ReadMarker(output, "DISPOSITION"), "RejectedByNativeKss", StringComparison.Ordinal);
        var profileMatched = string.Equals(profile.RobotType, submission.RequiredProfile.MachineDataIdentity, StringComparison.Ordinal)
            && string.Equals(profile.KssVersion, expectedKssVersion, StringComparison.Ordinal)
            && string.Equals(profile.CurrentProjectName, expectedProjectName, StringComparison.Ordinal);
        return new NativeKssCandidateExecutionObservation
        {
            Attempted = output.Contains("KSS_CANDIDATE_EXECUTION_BEGIN=True", StringComparison.Ordinal),
            ProfileMatched = profileMatched,
            UploadVerified = ReadBoolMarker(output, "UPLOAD_VERIFIED"),
            SelectSucceeded = ReadBoolMarker(output, "SELECT_SUCCEEDED"),
            Diagnostics = diagnostics,
            Mode = ReadMarker(output, "MODE") ?? string.Empty,
            StartCount = int.TryParse(ReadMarker(output, "START_COUNT"), out var startCount) ? startCount : 0,
            StartExceptions = ReadMarkerValues(output, "START_EXCEPTION").Select(DecodeBase64).ToList(),
            FinalState = ReadMarker(output, "FINAL_STATE") ?? string.Empty,
            Completed = completed,
            Rejected = rejected,
            CleanupVerified = ReadBoolMarker(output, "CLEANUP_VERIFIED")
        };
    }

    internal static NativeKssCandidateExecutionDisposition ClassifyDisposition(NativeKssCandidateExecutionObservation execution)
    {
        if (!execution.Attempted || !execution.ProfileMatched || !execution.UploadVerified || !execution.CleanupVerified
            || execution.StartExceptions.Count != 0)
        {
            return NativeKssCandidateExecutionDisposition.InfrastructureFailed;
        }

        if (execution.Rejected && !execution.Completed && execution.StartCount == 0 && execution.Diagnostics.Count > 0)
        {
            return NativeKssCandidateExecutionDisposition.RejectedByNativeKss;
        }

        if (!execution.Rejected && execution.SelectSucceeded && execution.Diagnostics.Count == 0
            && execution.Completed && string.Equals(execution.Mode, "Go", StringComparison.Ordinal)
            && string.Equals(execution.FinalState, "End", StringComparison.Ordinal))
        {
            return NativeKssCandidateExecutionDisposition.Executed;
        }

        return NativeKssCandidateExecutionDisposition.InfrastructureFailed;
    }

    private static IEnumerable<NativeKssDiagnosticFinding> ReadErrors(string output)
    {
        foreach (var value in ReadMarkerValues(output, "DIAGNOSTIC"))
        {
            var parts = value.Split('|');
            if (parts.Length != 6 || !int.TryParse(parts[1], out var number)
                || !int.TryParse(parts[2], out var line) || !int.TryParse(parts[3], out var column)) continue;
            yield return new NativeKssDiagnosticFinding
            {
                Module = DecodeBase64(parts[0]),
                ErrorNumber = number,
                Line = line,
                Column = column,
                Description = DecodeBase64(parts[4]),
                Parameter = DecodeBase64(parts[5])
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

    private static string? ReadMarker(string output, string marker) => ReadMarkerValues(output, marker).LastOrDefault();
    private static bool ReadBoolMarker(string output, string marker) =>
        bool.TryParse(ReadMarker(output, marker), out var value) && value;

    private static string DecodeBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
        catch (FormatException) { return string.Empty; }
    }

    private static string EncodeBase64(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static NativeKssRunnerCommandObservation ToObservation(WorkVisualProcessResult result) => new()
    {
        Attempted = true,
        ExitCode = result.ExitCode,
        TimedOut = result.TimedOut,
        CleanupVerified = result.CleanupVerified,
        DurationMilliseconds = result.DurationMilliseconds,
        StandardOutputSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.StandardOutput))),
        StandardErrorSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.StandardError)))
    };

    private VmrunCommandObservation RunVmrun(string name, OfficeLiteCycleRequest request, IReadOnlyList<string> arguments)
    {
        try
        {
            var result = _officeLitePlatform.RunVmrun(request.VmrunPath, arguments, TimeSpan.FromSeconds(request.VmrunTimeoutSeconds));
            return new VmrunCommandObservation
            {
                Name = name,
                ExitCode = result.ExitCode,
                TimedOut = result.TimedOut,
                DurationMilliseconds = result.DurationMilliseconds,
                StandardOutput = result.StandardOutput,
                StandardError = result.StandardError
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            return new VmrunCommandObservation { Name = name, ExitCode = -1, StandardError = $"{exception.GetType().Name}: {exception.Message}" };
        }
    }

    private static byte[] ReadEmbeddedScript()
    {
        using var stream = typeof(OfficeLiteNativeKssCandidateExecutionRunner).Assembly.GetManifestResourceStream(
            NativeKssCandidateExecutionContract.EmbeddedScriptResource)
            ?? throw new InvalidOperationException("Embedded native-KSS candidate-execution script was not found.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static OfficeLiteCycleReceipt CreateUnavailableLifecycleReceipt(
        OfficeLiteCycleRequest request,
        string attemptId,
        DateTimeOffset observedAt)
    {
        var payload = new OfficeLiteCyclePayload
        {
            ReceiptId = $"officelite-cycle-{attemptId}-not-run",
            AttemptId = $"{attemptId}-not-run",
            CoreAssemblySha256 = ComputeFileSha256(typeof(OfficeLiteCycleRunner).Assembly.Location),
            Runtime = new RuntimeEnvironment
            {
                OsDescription = RuntimeInformation.OSDescription,
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
            },
            StartedAtUtc = observedAt,
            CompletedAtUtc = observedAt,
            TerminalClassification = EnvironmentTerminalClassification.Blocked,
            VmrunPath = Path.GetFullPath(request.VmrunPath),
            Checks = [Blocked("lifecycle", "OfficeLite lifecycle was not started because host-side preconditions failed.")],
            UnsupportedGaps = ["Native KSS candidate execution was not invoked."]
        };
        return new OfficeLiteCycleReceipt
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
    }

    private static EnvironmentFileObservation ObserveExistingFile(string id, string path, bool includeVersion)
    {
        var info = new FileInfo(path);
        var version = includeVersion ? FileVersionInfo.GetVersionInfo(info.FullName) : null;
        return new EnvironmentFileObservation
        {
            Id = id,
            Path = info.FullName,
            Exists = true,
            Bytes = info.Length,
            Sha256 = ComputeFileSha256(info.FullName),
            FileVersion = version?.FileVersion,
            ProductVersion = version?.ProductVersion
        };
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

    private static void ValidatePortableId(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || !Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9._-]{0,95}$"))
        {
            throw new ArgumentException("Value must be a 1-96 character portable identifier.", parameterName);
        }
    }

    private static void WriteNew(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
    }

    private static EnvironmentCheck Passed(string id, string detail) => new() { Id = id, Status = EnvironmentCheckStatus.Passed, Detail = detail };
    private static EnvironmentCheck Blocked(string id, string detail) => new() { Id = id, Status = EnvironmentCheckStatus.Blocked, Detail = detail };
    private static EnvironmentCheck Failed(string id, string detail) => new() { Id = id, Status = EnvironmentCheckStatus.Failed, Detail = detail };
    private static string ComputeFileSha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}

public sealed record NativeKssCandidateExecutionVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;
    public bool Succeeded { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
    public List<string> Errors { get; init; } = [];
}

public static class OfficeLiteNativeKssCandidateExecutionReceiptVerifier
{
    private static readonly string[] RequiredFileIds =
    [
        "workvisual-script-runner",
        "candidate-source",
        "candidate-data",
        "workvisual-candidate-execution-script",
        "workvisual-live-stdout",
        "workvisual-live-stderr"
    ];

    public static NativeKssCandidateExecutionVerificationResult Verify(
        NativeKssCandidateExecutionReceipt receipt) =>
        Verify(receipt, new OfficeLiteExactProfileAdmissionGate());

    internal static NativeKssCandidateExecutionVerificationResult Verify(
        NativeKssCandidateExecutionReceipt receipt,
        IOfficeLiteExactProfileAdmissionGate exactProfileAdmissionGate)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(exactProfileAdmissionGate);
        var errors = new List<string>();
        if (receipt.SchemaIdentity != NativeKssCandidateExecutionContract.ReceiptSchemaIdentity
            || receipt.SchemaVersion is not (
                NativeKssCandidateExecutionContract.LegacyReceiptSchemaVersion
                or NativeKssCandidateExecutionContract.ReceiptSchemaVersion))
        {
            errors.Add("candidate-execution receipt schema identity/version is unsupported");
        }

        var payload = receipt.Payload;
        if (!string.Equals(ReceiptSerialization.ComputeCanonicalSha256(payload), receipt.PayloadSha256, StringComparison.Ordinal))
        {
            errors.Add("payload SHA-256 mismatch");
        }

        if (receipt.SchemaVersion == NativeKssCandidateExecutionContract.LegacyReceiptSchemaVersion)
        {
            if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready)
            {
                errors.Add("legacy schema v1 cannot establish admitted exact-C01 execution");
            }
        }
        else if (receipt.SchemaVersion == NativeKssCandidateExecutionContract.ReceiptSchemaVersion)
        {
            try
            {
                var currentAdmission = exactProfileAdmissionGate.Admit(
                    new OfficeLiteExactProfileAdmissionRequest
                    {
                        AcceptanceReceiptPath = payload.ProfileAdmission.AcceptanceReceiptPath,
                        ExpectedProjectName = payload.ExpectedProjectName
                    });
                if (!string.Equals(
                        ReceiptSerialization.ComputeCanonicalSha256(currentAdmission),
                        ReceiptSerialization.ComputeCanonicalSha256(payload.ProfileAdmission),
                        StringComparison.Ordinal))
                {
                    errors.Add("current exact-profile admission no longer matches the execution receipt");
                }
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or InvalidDataException
                or InvalidOperationException)
            {
                errors.Add($"current exact-profile admission failed: {exception.Message}");
            }

            if (!payload.ProfileAdmission.Accepted
                || payload.Checks.Count(check => check.Id == "exact-profile-admission"
                    && check.Status == EnvironmentCheckStatus.Passed) != 1)
            {
                errors.Add("schema v2 requires one accepted exact-profile admission check");
            }
        }

        try
        {
            var current = new NativeKssCandidateSubmissionPlanner().Plan(new NativeKssCandidateSubmissionRequest
            {
                CandidateRoot = payload.Submission.CandidateRoot,
                CandidateReceiptPath = payload.Submission.CandidateReceiptPath,
                ProgramRelativeStem = payload.Submission.ProgramRelativeStem,
                MaximumStartCommands = payload.Submission.MaximumStartCommands
            });
            if (!string.Equals(ReceiptSerialization.ComputeCanonicalSha256(current),
                    ReceiptSerialization.ComputeCanonicalSha256(payload.Submission), StringComparison.Ordinal))
            {
                errors.Add("current verified candidate no longer matches the submitted candidate plan");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
        {
            errors.Add($"current candidate verification failed: {exception.Message}");
        }

        var classified = OfficeLiteNativeKssCandidateExecutionRunner.ClassifyDisposition(payload.Execution);
        if (classified != payload.Disposition) errors.Add("execution disposition does not match parsed execution evidence");
        if (payload.Execution.StartCount < 0 || payload.Execution.StartCount > payload.Submission.MaximumStartCommands)
        {
            errors.Add("Start count exceeded the submission bound");
        }

        if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready)
        {
            var expectedStatus = payload.Disposition == NativeKssCandidateExecutionDisposition.Executed
                ? NativeKssStatus.BoundedExecutionValidated
                : payload.Disposition == NativeKssCandidateExecutionDisposition.RejectedByNativeKss
                    ? NativeKssStatus.SyntaxSelectionValidated
                    : NativeKssStatus.NotRun;
            if (payload.NativeKssStatus != expectedStatus) errors.Add("Ready receipt has the wrong native KSS status");
            if (!payload.SnapshotRestored || !payload.ControllerStateRestored || !payload.EnvironmentReusable)
            {
                errors.Add("Ready receipt requires full controller and snapshot restoration");
            }
        }

        if (payload.CredentialsUsed || payload.PhysicalMotionRequested)
        {
            errors.Add("candidate execution may not use credentials or request physical motion");
        }

        if (!string.Equals(payload.EmbeddedScriptSha256, NativeKssCandidateExecutionContract.EmbeddedScriptSha256, StringComparison.Ordinal))
        {
            errors.Add("embedded script SHA-256 is not the pinned contract value");
        }

        if (payload.LifecycleReceipt is null)
        {
            errors.Add("lifecycle receipt is required");
        }
        else if (!OfficeLiteCycleReceiptVerifier.Verify(payload.LifecycleReceipt).Succeeded)
        {
            errors.Add("lifecycle receipt integrity failed");
        }

        var observed = payload.Files.GroupBy(file => file.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        foreach (var duplicate in observed.Where(item => item.Value.Length != 1))
        {
            errors.Add($"evidence identity must occur at most once: {duplicate.Key}");
        }

        if (payload.LiveCommand.Attempted)
        {
            foreach (var id in RequiredFileIds)
            {
                if (!observed.TryGetValue(id, out var matches) || matches.Length != 1 || !matches[0].Exists)
                {
                    errors.Add($"live execution evidence must occur exactly once: {id}");
                }
            }
        }
        else if (observed.ContainsKey("workvisual-live-stdout") || observed.ContainsKey("workvisual-live-stderr"))
        {
            errors.Add("live stdout/stderr evidence is inconsistent with an unattempted command");
        }

        VerifyRawEvidence(payload, observed, errors);
        return new NativeKssCandidateExecutionVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }

    private static void VerifyRawEvidence(
        NativeKssCandidateExecutionPayload payload,
        IReadOnlyDictionary<string, EnvironmentFileObservation[]> observed,
        List<string> errors)
    {
        if (!payload.LiveCommand.Attempted)
        {
            if (observed.TryGetValue("workvisual-candidate-execution-script", out var scripts)
                && scripts.Length == 1 && scripts[0].Exists
                && !string.Equals(scripts[0].Sha256, payload.EmbeddedScriptSha256, StringComparison.Ordinal))
            {
                errors.Add("materialized script SHA-256 is not bound to the pinned contract");
            }
            return;
        }

        if (RequiredFileIds.Any(id => !observed.TryGetValue(id, out var matches) || matches.Length != 1)) return;
        var script = observed["workvisual-candidate-execution-script"][0];
        var stdout = observed["workvisual-live-stdout"][0];
        var stderr = observed["workvisual-live-stderr"][0];
        if (!string.Equals(script.Sha256, payload.EmbeddedScriptSha256, StringComparison.Ordinal)
            || !string.Equals(stdout.Sha256, payload.LiveCommand.StandardOutputSha256, StringComparison.Ordinal)
            || !string.Equals(stderr.Sha256, payload.LiveCommand.StandardErrorSha256, StringComparison.Ordinal))
        {
            errors.Add("raw script/output hashes are not bound to the command observation");
        }

        if (File.Exists(stdout.Path))
        {
            var output = File.ReadAllText(stdout.Path);
            var profile = OfficeLiteNativeKssCandidateExecutionRunner.ParseProfile(output);
            var execution = OfficeLiteNativeKssCandidateExecutionRunner.ParseExecution(
                output,
                payload.Submission,
                profile,
                payload.ExpectedProjectName,
                payload.ProfileAdmission.RuntimeKssVersion);
            if (!string.Equals(ReceiptSerialization.ComputeCanonicalSha256(profile),
                    ReceiptSerialization.ComputeCanonicalSha256(payload.ObservedProfile), StringComparison.Ordinal)
                || !string.Equals(ReceiptSerialization.ComputeCanonicalSha256(execution),
                    ReceiptSerialization.ComputeCanonicalSha256(payload.Execution), StringComparison.Ordinal))
            {
                errors.Add("raw output no longer corroborates parsed profile/execution evidence");
            }
        }
    }
}

public static class NativeKssCandidateExecutionReceiptWriter
{
    public static string WriteNew(string outputPath, NativeKssCandidateExecutionReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!OfficeLiteNativeKssCandidateExecutionReceiptVerifier.Verify(receipt).Succeeded)
        {
            throw new InvalidOperationException("Native KSS candidate-execution receipt integrity is invalid.");
        }

        var fullPath = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Receipt output must use the .json extension.", nameof(outputPath));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath)));
        using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(ReceiptSerialization.ToJson(receipt));
        writer.WriteLine();
        writer.Flush();
        stream.Flush(flushToDisk: true);
        return fullPath;
    }
}
