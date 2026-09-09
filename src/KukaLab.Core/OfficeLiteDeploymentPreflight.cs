using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class OfficeLiteDeploymentPreflightContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.officelite-workvisual-deployment-preflight-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const string EmbeddedScriptResource = "KukaLab.Probes.WorkVisual.DeploymentPreflight.csx";
    public const string EmbeddedScriptSha256 = "82132FC984C618C7488BBD898FB0EA5DAA6400256EBE9A4BD4A810B733B9BE01";
    public const string FailureMarker = "DEPLOYMENT_PREFLIGHT_FAILED:";

    internal static readonly IReadOnlyList<string> RequiredUnsupportedGaps =
    [
        "Ready means the differential deployment preflight completed; it does not mean the project is deployable.",
        "The adapter never calls deployment Execute, conflict Resolve, project save, activation, program selection, program start or motion.",
        "Only isolated analysis copies may be rewritten by WorkVisual; the protected source WVS must remain byte-identical.",
        "Missing WorkVisual option packages must be obtained from vendor-authorized media or by an attended controller-to-PC download; they are never synthesized from extracted controller files.",
        "A conflict-free preflight is still not native KSS execution, KUKA.Sim VRC equivalence, safety validation or physical qualification."
    ];
}

public sealed record OfficeLiteDeploymentPreflightRequest
{
    public required OfficeLiteCycleRequest OfficeLite { get; init; }
    public required string RunnerPath { get; init; }
    public required string ProjectPath { get; init; }
    public required string EvidenceDirectory { get; init; }
    public int RunnerTimeoutSeconds { get; init; } = 120;

    public static OfficeLiteDeploymentPreflightRequest CreateDefault(
        string assetRoot,
        string projectPath,
        string evidenceDirectory,
        string? vmxPath = null,
        string? vmrunPath = null,
        string? runnerPath = null,
        string? guestIpAddress = null,
        int readinessTimeoutSeconds = 240,
        int serviceObservationSeconds = 180,
        int runnerTimeoutSeconds = 120)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceDirectory);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var cycle = OfficeLiteCycleRequest.CreateDefault(
            assetRoot,
            vmrunPath,
            guestIpAddress,
            readinessTimeoutSeconds: readinessTimeoutSeconds) with
        {
            DiagnoseWorkVisualServices = true,
            ServiceObservationSeconds = serviceObservationSeconds
        };
        if (!string.IsNullOrWhiteSpace(vmxPath))
        {
            cycle = cycle with { VmxPath = Path.GetFullPath(vmxPath) };
        }

        return new OfficeLiteDeploymentPreflightRequest
        {
            OfficeLite = cycle,
            RunnerPath = Path.GetFullPath(runnerPath
                ?? Path.Combine(programFilesX86, "KUKA", "WorkVisual 6.0", "wvsr.exe")),
            ProjectPath = Path.GetFullPath(projectPath),
            EvidenceDirectory = Path.GetFullPath(evidenceDirectory),
            RunnerTimeoutSeconds = runnerTimeoutSeconds
        };
    }
}

public sealed record WorkVisualProjectOptionObservation
{
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
}

public sealed record WorkVisualDeploymentConflictObservation
{
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int ResolutionCount { get; init; }
    public bool HasDefaultResolution { get; init; }
}

public sealed record WorkVisualDeploymentPreflightObservation
{
    public bool Attempted { get; init; }
    public string TargetAddress { get; init; } = string.Empty;
    public string SourceController { get; init; } = string.Empty;
    public string SourceAddress { get; init; } = string.Empty;
    public string SourceFirmware { get; init; } = string.Empty;
    public string SourceRobot { get; init; } = string.Empty;
    public string ActiveOptionProfile { get; init; } = string.Empty;
    public string DownloadedOptionsDirectory { get; init; } = string.Empty;
    public IReadOnlyList<WorkVisualProjectOptionObservation> ProjectOptions { get; init; } = [];
    public IReadOnlyList<WorkVisualProjectOptionObservation> InstalledOptionPackages { get; init; } = [];
    public bool HasConflicts { get; init; }
    public bool CanExecute { get; init; }
    public IReadOnlyList<WorkVisualDeploymentConflictObservation> Conflicts { get; init; } = [];
    public bool DeploymentExecuted { get; init; }
    public bool DeploymentReady => Attempted && CanExecute && !HasConflicts && Conflicts.Count == 0 && !DeploymentExecuted;
}

public sealed record OfficeLiteDeploymentPreflightPayload
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
    public string ProjectPath { get; init; } = string.Empty;
    public string EvidenceDirectory { get; init; } = string.Empty;
    public string EmbeddedScriptSha256 { get; init; } = string.Empty;
    public string SourceProjectInitialSha256 { get; init; } = string.Empty;
    public string NegativeCopyInitialSha256 { get; init; } = string.Empty;
    public string LiveCopyInitialSha256 { get; init; } = string.Empty;
    public bool SourceProjectUnchanged { get; init; }
    public required OfficeLiteCycleReceipt LifecycleReceipt { get; init; }
    public WorkVisualRunnerCommandObservation NegativeControlCommand { get; init; } = new();
    public WorkVisualRunnerCommandObservation LiveCommand { get; init; } = new();
    public WorkVisualDeploymentPreflightObservation Preflight { get; init; } = new();
    public bool ReadOnlyControllerOperation { get; init; } = true;
    public bool CredentialsUsed { get; init; }
    public bool SourceProjectMutationPerformed { get; init; }
    public bool AnalysisCopyMutationPermitted { get; init; } = true;
    public bool ControllerMutationPerformed { get; init; }
    public bool ConflictResolutionPerformed { get; init; }
    public bool DeploymentExecuted { get; init; }
    public NativeKssStatus NativeKssStatus { get; init; } = NativeKssStatus.NotRun;
    public IReadOnlyList<EnvironmentCheck> Checks { get; init; } = [];
    public IReadOnlyList<EnvironmentFileObservation> Files { get; init; } = [];
    public IReadOnlyList<string> SideEffects { get; init; } = [];
    public bool EnvironmentReusable { get; init; }
    public IReadOnlyList<string> UnsupportedGaps { get; init; } = [];
}

public sealed record OfficeLiteDeploymentPreflightReceipt
{
    public string SchemaIdentity { get; init; } = OfficeLiteDeploymentPreflightContract.ReceiptSchemaIdentity;
    public int SchemaVersion { get; init; } = OfficeLiteDeploymentPreflightContract.ReceiptSchemaVersion;
    public required OfficeLiteDeploymentPreflightPayload Payload { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record OfficeLiteDeploymentPreflightOutcome(OfficeLiteDeploymentPreflightReceipt Receipt)
{
    public int ExitCode => Receipt.Payload.TerminalClassification switch
    {
        EnvironmentTerminalClassification.Ready => 0,
        EnvironmentTerminalClassification.Failed => 2,
        _ => 3
    };
}

public sealed class OfficeLiteDeploymentPreflightRunner
{
    private readonly OfficeLiteCycleRunner _officeLiteRunner;
    private readonly IWorkVisualRunnerPlatform _workVisualPlatform;
    private readonly TimeProvider _timeProvider;

    public OfficeLiteDeploymentPreflightRunner()
        : this(new OfficeLiteCycleRunner(), new WorkVisualRunnerPlatform(), TimeProvider.System)
    {
    }

    internal OfficeLiteDeploymentPreflightRunner(
        OfficeLiteCycleRunner officeLiteRunner,
        IWorkVisualRunnerPlatform workVisualPlatform,
        TimeProvider timeProvider)
    {
        _officeLiteRunner = officeLiteRunner;
        _workVisualPlatform = workVisualPlatform;
        _timeProvider = timeProvider;
    }

    public OfficeLiteDeploymentPreflightOutcome Run(OfficeLiteDeploymentPreflightRequest request, string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAttemptId(attemptId);
        if (request.RunnerTimeoutSeconds is < 1 or > 300)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "WorkVisual runner timeout must be from 1 through 300 seconds.");
        }

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var checks = new List<EnvironmentCheck>();
        var files = new List<EnvironmentFileObservation>();
        var sideEffects = new List<string>();
        var runnerPath = Path.GetFullPath(request.RunnerPath);
        var projectPath = Path.GetFullPath(request.ProjectPath);
        var evidenceDirectory = Path.GetFullPath(request.EvidenceDirectory);
        var scriptHash = string.Empty;
        var sourceInitialHash = string.Empty;
        var negativeInitialHash = string.Empty;
        var liveInitialHash = string.Empty;
        var sourceUnchanged = false;
        var negative = new WorkVisualRunnerCommandObservation { CleanupVerified = true };
        var live = new WorkVisualRunnerCommandObservation { CleanupVerified = true };
        var preflight = new WorkVisualDeploymentPreflightObservation();
        var runnerCleanupVerified = true;
        OfficeLiteCycleReceipt? lifecycleReceipt = null;
        string scriptPath = string.Empty;
        string negativeProjectPath = string.Empty;
        string liveProjectPath = string.Empty;

        if (!OperatingSystem.IsWindows())
        {
            checks.Add(Blocked("host-os", "OfficeLite and WorkVisual Script Runner require Windows."));
            return Complete();
        }
        if (!File.Exists(runnerPath) || !File.Exists(projectPath))
        {
            checks.Add(Blocked("prerequisites", "The WorkVisual runner or protected source WVS is missing."));
            return Complete();
        }
        if (!string.Equals(Path.GetExtension(projectPath), ".wvs", StringComparison.OrdinalIgnoreCase)
            || (File.GetAttributes(projectPath) & FileAttributes.ReparsePoint) != 0)
        {
            checks.Add(Failed("source-project", "The source must be a non-reparse .wvs file."));
            return Complete();
        }

        try
        {
            if (Directory.Exists(evidenceDirectory))
            {
                checks.Add(Failed("evidence-directory", "Evidence directory already exists; create-new semantics refused reuse."));
                return Complete();
            }
            Directory.CreateDirectory(evidenceDirectory);
            sideEffects.Add($"CreateEvidenceDirectory:{evidenceDirectory}");

            sourceInitialHash = ComputeFileSha256(projectPath);
            scriptPath = Path.Combine(evidenceDirectory, $"{attemptId}.deployment-preflight.csx");
            negativeProjectPath = Path.Combine(evidenceDirectory, $"{attemptId}.negative.analysis-copy.wvs");
            liveProjectPath = Path.Combine(evidenceDirectory, $"{attemptId}.live.analysis-copy.wvs");
            var scriptBytes = ReadEmbeddedScript();
            scriptHash = Convert.ToHexString(SHA256.HashData(scriptBytes));
            if (!string.Equals(scriptHash, OfficeLiteDeploymentPreflightContract.EmbeddedScriptSha256, StringComparison.OrdinalIgnoreCase))
            {
                checks.Add(Failed("pinned-script", "Embedded deployment-preflight script does not match its pinned hash."));
                return Complete();
            }

            WriteNew(scriptPath, scriptBytes);
            File.Copy(projectPath, negativeProjectPath, overwrite: false);
            File.Copy(projectPath, liveProjectPath, overwrite: false);
            negativeInitialHash = ComputeFileSha256(negativeProjectPath);
            liveInitialHash = ComputeFileSha256(liveProjectPath);
            if (!string.Equals(sourceInitialHash, negativeInitialHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(sourceInitialHash, liveInitialHash, StringComparison.OrdinalIgnoreCase))
            {
                checks.Add(Failed("analysis-copies", "The isolated analysis copies are not initially byte-identical to the source WVS."));
                return Complete();
            }

            sideEffects.Add($"CreateProbeScript:{scriptPath}");
            sideEffects.Add($"CreateAnalysisCopy:{negativeProjectPath}");
            sideEffects.Add($"CreateAnalysisCopy:{liveProjectPath}");
            checks.Add(Passed("source-project", "The protected source WVS was hashed and two byte-identical isolated analysis copies were created."));
            checks.Add(Passed("pinned-script", "The fixed no-deploy WorkVisual deployment-preflight script was materialized."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            checks.Add(Failed("evidence-preparation", $"Evidence preparation failed safely: {exception.Message}"));
            return Complete();
        }

        var cycleRequest = request.OfficeLite with
        {
            DiagnoseWorkVisualServices = true,
            ServiceObservationSeconds = request.OfficeLite.ServiceObservationSeconds > 0
                ? request.OfficeLite.ServiceObservationSeconds
                : 180
        };
        var lifecycle = _officeLiteRunner.Run(
            cycleRequest,
            $"{attemptId}-lifecycle",
            context => ExecuteDifferential(context.GuestAddress));
        lifecycleReceipt = lifecycle.Receipt;

        try
        {
            sourceUnchanged = string.Equals(sourceInitialHash, ComputeFileSha256(projectPath), StringComparison.OrdinalIgnoreCase);
            files.Add(ObserveExistingFile("workvisual-script-runner", runnerPath, true));
            files.Add(ObserveExistingFile("source-project", projectPath, false));
            files.Add(ObserveExistingFile("deployment-preflight-script", scriptPath, false));
            files.Add(ObserveExistingFile("negative-analysis-copy", negativeProjectPath, false));
            files.Add(ObserveExistingFile("live-analysis-copy", liveProjectPath, false));
            checks.Add(sourceUnchanged
                ? Passed("source-immutability", "The protected source WVS remained byte-identical after both WorkVisual runs.")
                : Failed("source-immutability", "The protected source WVS changed during preflight."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            checks.Add(Failed("source-immutability", $"Source rehash failed: {exception.Message}"));
        }

        if (lifecycleReceipt.Payload.TerminalClassification == EnvironmentTerminalClassification.Ready
            && HasCompleteObservation(preflight)
            && checks.All(check => check.Status is not EnvironmentCheckStatus.Failed and not EnvironmentCheckStatus.Blocked))
        {
            checks.Add(Passed("deployment-preflight", preflight.DeploymentReady
                ? "The source project is conflict-free for the isolated target; deployment remains deliberately unexecuted."
                : $"The deployment preflight completed and preserved {preflight.Conflicts.Count} exact blocker(s); deployment remains deliberately unexecuted."));
        }
        else if (!preflight.Attempted)
        {
            checks.Add(Blocked("deployment-preflight", "The live preflight did not run because OfficeLite DeviceInfo readiness was not established."));
        }

        checks.Add(lifecycleReceipt.Payload.CleanShutdownVerified && runnerCleanupVerified
            ? Passed("environment-cleanup", "OfficeLite soft shutdown and WorkVisual runner cleanup were verified.")
            : Failed("environment-cleanup", "OfficeLite or WorkVisual runner cleanup was not verified."));
        return Complete();

        void ExecuteDifferential(string address)
        {
            var negativeResult = RunWorkVisual("127.0.0.1", negativeProjectPath);
            negative = ToObservation(negativeResult);
            runnerCleanupVerified &= negativeResult.CleanupVerified;
            PreserveRawEvidence("negative", negativeResult);
            var negativeCombined = negativeResult.StandardOutput + Environment.NewLine + negativeResult.StandardError;
            if (negativeResult.TimedOut
                || negativeResult.ExitCode != 42
                || !negativeCombined.Contains(OfficeLiteDeploymentPreflightContract.FailureMarker, StringComparison.Ordinal)
                || !negativeCombined.Contains("DEPLOYMENT_EXECUTED=false", StringComparison.Ordinal))
            {
                checks.Add(Failed("negative-control", "The unreachable-address control did not prove typed failure plus no deployment."));
                return;
            }
            checks.Add(Passed("negative-control", "The unreachable-address control returned exit 42 and explicitly reported no deployment."));

            var liveResult = RunWorkVisual(address, liveProjectPath);
            live = ToObservation(liveResult);
            runnerCleanupVerified &= liveResult.CleanupVerified;
            PreserveRawEvidence("live", liveResult);
            preflight = ParsePreflight(address, liveResult.StandardOutput + Environment.NewLine + liveResult.StandardError);
            if (liveResult.TimedOut)
            {
                checks.Add(Blocked("live-preflight", "The live WorkVisual preflight exceeded its bounded timeout."));
            }
            else if (liveResult.ExitCode == 42)
            {
                checks.Add(Blocked("live-preflight", "The WorkVisual client reached its typed online-controller failure path for the isolated OfficeLite address."));
            }
            else if (liveResult.ExitCode != 0)
            {
                checks.Add(Failed("live-preflight", $"The live preflight returned unexpected exit code {liveResult.ExitCode}."));
            }
            else if (!HasCompleteObservation(preflight))
            {
                checks.Add(Failed("live-preflight", "The live preflight did not emit the complete fixed observation contract."));
            }
            else
            {
                checks.Add(Passed("live-preflight", "The live WorkVisual preflight returned the complete identity, option and conflict contract without deployment."));
            }
        }

        WorkVisualProcessResult RunWorkVisual(string address, string analysisProjectPath)
        {
            try
            {
                return _workVisualPlatform.Run(
                    runnerPath,
                    ["-executescript", $"-scriptpath={scriptPath}", $"-project={analysisProjectPath}", $"-address={address}"],
                    evidenceDirectory,
                    TimeSpan.FromSeconds(request.RunnerTimeoutSeconds));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
            {
                return new WorkVisualProcessResult(-1, false, true, 0, string.Empty, $"{exception.GetType().Name}: {exception.Message}");
            }
        }

        void PreserveRawEvidence(string prefix, WorkVisualProcessResult result)
        {
            var stdoutPath = Path.Combine(evidenceDirectory, $"{attemptId}.{prefix}.stdout.log");
            var stderrPath = Path.Combine(evidenceDirectory, $"{attemptId}.{prefix}.stderr.log");
            WriteNew(stdoutPath, Encoding.UTF8.GetBytes(result.StandardOutput));
            WriteNew(stderrPath, Encoding.UTF8.GetBytes(result.StandardError));
            files.Add(ObserveExistingFile($"workvisual-{prefix}-stdout", stdoutPath, false));
            files.Add(ObserveExistingFile($"workvisual-{prefix}-stderr", stderrPath, false));
            sideEffects.Add($"CreateRawStdout:{stdoutPath}");
            sideEffects.Add($"CreateRawStderr:{stderrPath}");
        }

        OfficeLiteDeploymentPreflightOutcome Complete()
        {
            stopwatch.Stop();
            lifecycleReceipt ??= CreateUnavailableLifecycleReceipt(request.OfficeLite, attemptId, startedAt);
            var terminal = checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
                ? EnvironmentTerminalClassification.Failed
                : checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked)
                    || lifecycleReceipt.Payload.TerminalClassification != EnvironmentTerminalClassification.Ready
                    ? EnvironmentTerminalClassification.Blocked
                    : EnvironmentTerminalClassification.Ready;
            var payload = new OfficeLiteDeploymentPreflightPayload
            {
                ReceiptId = $"officelite-workvisual-deployment-preflight-{attemptId}",
                AttemptId = attemptId,
                CoreAssemblySha256 = ComputeFileSha256(typeof(OfficeLiteDeploymentPreflightRunner).Assembly.Location),
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
                ProjectPath = projectPath,
                EvidenceDirectory = evidenceDirectory,
                EmbeddedScriptSha256 = scriptHash,
                SourceProjectInitialSha256 = sourceInitialHash,
                NegativeCopyInitialSha256 = negativeInitialHash,
                LiveCopyInitialSha256 = liveInitialHash,
                SourceProjectUnchanged = sourceUnchanged,
                LifecycleReceipt = lifecycleReceipt,
                NegativeControlCommand = negative,
                LiveCommand = live,
                Preflight = preflight,
                ReadOnlyControllerOperation = true,
                CredentialsUsed = false,
                SourceProjectMutationPerformed = false,
                AnalysisCopyMutationPermitted = true,
                ControllerMutationPerformed = false,
                ConflictResolutionPerformed = false,
                DeploymentExecuted = false,
                NativeKssStatus = NativeKssStatus.NotRun,
                Checks = checks,
                Files = files,
                SideEffects = sideEffects,
                EnvironmentReusable = sourceUnchanged && runnerCleanupVerified && lifecycleReceipt.Payload.CleanShutdownVerified,
                UnsupportedGaps = OfficeLiteDeploymentPreflightContract.RequiredUnsupportedGaps
            };
            var receipt = new OfficeLiteDeploymentPreflightReceipt
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
            };
            return new OfficeLiteDeploymentPreflightOutcome(receipt);
        }
    }

    private static WorkVisualDeploymentPreflightObservation ParsePreflight(string address, string output)
    {
        var options = ReadPairs(output, "PROJECT_OPTION", ReadInt(output, "PROJECT_OPTION_COUNT"));
        var packages = ReadPairs(output, "INSTALLED_OPTION_PACKAGE", ReadInt(output, "INSTALLED_OPTION_PACKAGE_COUNT"));
        var conflictCount = ReadInt(output, "CONFLICT_COUNT");
        var conflicts = new List<WorkVisualDeploymentConflictObservation>();
        for (var index = 0; index < conflictCount; index++)
        {
            conflicts.Add(new WorkVisualDeploymentConflictObservation
            {
                Type = ReadMarker(output, $"CONFLICT_{index}_TYPE") ?? string.Empty,
                Title = ReadMarker(output, $"CONFLICT_{index}_TITLE") ?? string.Empty,
                Description = ReadMarker(output, $"CONFLICT_{index}_DESCRIPTION") ?? string.Empty,
                ResolutionCount = ReadInt(output, $"CONFLICT_{index}_RESOLUTION_COUNT"),
                HasDefaultResolution = ReadBool(output, $"CONFLICT_{index}_HAS_DEFAULT")
            });
        }

        return new WorkVisualDeploymentPreflightObservation
        {
            Attempted = output.Contains("DEPLOYMENT_EXECUTED=", StringComparison.Ordinal),
            TargetAddress = ReadMarker(output, "TARGET_ADDRESS") ?? address,
            SourceController = ReadMarker(output, "SOURCE_CONTROLLER") ?? string.Empty,
            SourceAddress = ReadMarker(output, "SOURCE_ADDRESS") ?? string.Empty,
            SourceFirmware = ReadMarker(output, "SOURCE_FIRMWARE") ?? string.Empty,
            SourceRobot = ReadMarker(output, "SOURCE_ROBOT") ?? string.Empty,
            ActiveOptionProfile = ReadMarker(output, "ACTIVE_OPTION_PROFILE") ?? string.Empty,
            DownloadedOptionsDirectory = ReadMarker(output, "DOWNLOADED_OPTIONS_DIRECTORY") ?? string.Empty,
            ProjectOptions = options,
            InstalledOptionPackages = packages,
            HasConflicts = ReadBool(output, "HAS_CONFLICTS"),
            CanExecute = ReadBool(output, "CAN_EXECUTE"),
            Conflicts = conflicts,
            DeploymentExecuted = ReadBool(output, "DEPLOYMENT_EXECUTED")
        };
    }

    private static IReadOnlyList<WorkVisualProjectOptionObservation> ReadPairs(string output, string prefix, int count)
    {
        var result = new List<WorkVisualProjectOptionObservation>();
        for (var index = 0; index < count; index++)
        {
            var value = ReadMarker(output, $"{prefix}_{index}") ?? string.Empty;
            var parts = value.Split('|', 2);
            result.Add(new WorkVisualProjectOptionObservation
            {
                Name = parts.ElementAtOrDefault(0) ?? string.Empty,
                Version = parts.ElementAtOrDefault(1) ?? string.Empty
            });
        }
        return result;
    }

    private static string? ReadMarker(string output, string name)
    {
        var match = Regex.Match(output, $"(?m)^{Regex.Escape(name)}=(?<value>.*)\\r?$", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static int ReadInt(string output, string name) =>
        int.TryParse(ReadMarker(output, name), out var value) && value >= 0 ? value : 0;

    private static bool ReadBool(string output, string name) =>
        bool.TryParse(ReadMarker(output, name), out var value) && value;

    private static bool HasCompleteObservation(WorkVisualDeploymentPreflightObservation observation) =>
        observation.Attempted
        && !string.IsNullOrWhiteSpace(observation.TargetAddress)
        && !string.IsNullOrWhiteSpace(observation.SourceController)
        && !string.IsNullOrWhiteSpace(observation.SourceFirmware)
        && !string.IsNullOrWhiteSpace(observation.SourceRobot)
        && observation.ProjectOptions.All(option => !string.IsNullOrWhiteSpace(option.Name) && !string.IsNullOrWhiteSpace(option.Version))
        && observation.Conflicts.All(conflict => !string.IsNullOrWhiteSpace(conflict.Type) && !string.IsNullOrWhiteSpace(conflict.Title))
        && !observation.DeploymentExecuted;

    private static WorkVisualRunnerCommandObservation ToObservation(WorkVisualProcessResult result) => new()
    {
        ExitCode = result.ExitCode,
        TimedOut = result.TimedOut,
        CleanupVerified = result.CleanupVerified,
        DurationMilliseconds = Math.Max(0, result.DurationMilliseconds)
    };

    private static byte[] ReadEmbeddedScript()
    {
        using var stream = typeof(OfficeLiteDeploymentPreflightRunner).Assembly.GetManifestResourceStream(
            OfficeLiteDeploymentPreflightContract.EmbeddedScriptResource)
            ?? throw new InvalidOperationException("Pinned WorkVisual deployment-preflight resource is missing.");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        var canonicalText = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return Encoding.UTF8.GetBytes(canonicalText);
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
            AssetRoot = Path.GetFullPath(request.AssetRoot),
            VmxPath = Path.GetFullPath(request.VmxPath),
            VmrunPath = Path.GetFullPath(request.VmrunPath),
            Checks = [Blocked("lifecycle", "OfficeLite lifecycle was not started because host-side preconditions failed.")],
            UnsupportedGaps = ["Native KSS, project deployment and motion were not invoked."]
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
        var version = includeVersion ? FileVersionInfo.GetVersionInfo(path) : null;
        return new EnvironmentFileObservation
        {
            Id = id,
            Path = Path.GetFullPath(path),
            Exists = true,
            Bytes = info.Length,
            Sha256 = ComputeFileSha256(path),
            FileVersion = string.IsNullOrWhiteSpace(version?.FileVersion) ? null : version.FileVersion,
            ProductVersion = string.IsNullOrWhiteSpace(version?.ProductVersion) ? null : version.ProductVersion
        };
    }

    private static void WriteNew(string path, byte[] content)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(content);
        stream.Flush(flushToDisk: true);
    }

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void ValidateAttemptId(string attemptId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptId);
        if (!Regex.IsMatch(attemptId, "^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("Attempt ID must match [A-Za-z0-9][A-Za-z0-9._-]{2,127}.", nameof(attemptId));
        }
    }

    private static EnvironmentCheck Passed(string id, string detail) => new() { Id = id, Status = EnvironmentCheckStatus.Passed, Detail = detail };
    private static EnvironmentCheck Blocked(string id, string detail) => new() { Id = id, Status = EnvironmentCheckStatus.Blocked, Detail = detail };
    private static EnvironmentCheck Failed(string id, string detail) => new() { Id = id, Status = EnvironmentCheckStatus.Failed, Detail = detail };
}

public sealed record OfficeLiteDeploymentPreflightVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;
    public bool Succeeded { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
    public IReadOnlyList<string> Errors { get; init; } = [];
}

public static class OfficeLiteDeploymentPreflightReceiptVerifier
{
    private static readonly string[] RequiredFileIds =
    [
        "workvisual-script-runner",
        "source-project",
        "deployment-preflight-script",
        "negative-analysis-copy",
        "live-analysis-copy",
        "workvisual-negative-stdout",
        "workvisual-negative-stderr",
        "workvisual-live-stdout",
        "workvisual-live-stderr"
    ];

    public static OfficeLiteDeploymentPreflightVerificationResult Verify(OfficeLiteDeploymentPreflightReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var payload = receipt.Payload;
        var errors = new List<string>();
        if (!string.Equals(receipt.SchemaIdentity, OfficeLiteDeploymentPreflightContract.ReceiptSchemaIdentity, StringComparison.Ordinal)
            || receipt.SchemaVersion != OfficeLiteDeploymentPreflightContract.ReceiptSchemaVersion)
        {
            errors.Add("receipt schema is invalid");
        }
        if (!string.Equals(receipt.PayloadSha256, ReceiptSerialization.ComputeCanonicalSha256(payload), StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("payloadSha256 does not match the canonical payload");
        }
        if (!OfficeLiteCycleReceiptVerifier.Verify(payload.LifecycleReceipt).Succeeded)
        {
            errors.Add("lifecycleReceipt integrity is invalid");
        }
        if (!payload.ReadOnlyControllerOperation
            || payload.CredentialsUsed
            || payload.SourceProjectMutationPerformed
            || !payload.AnalysisCopyMutationPermitted
            || payload.ControllerMutationPerformed
            || payload.ConflictResolutionPerformed
            || payload.DeploymentExecuted
            || payload.Preflight.DeploymentExecuted
            || payload.NativeKssStatus != NativeKssStatus.NotRun)
        {
            errors.Add("no-deploy safety assertions are invalid");
        }
        if ((!string.IsNullOrWhiteSpace(payload.EmbeddedScriptSha256)
                || payload.TerminalClassification == EnvironmentTerminalClassification.Ready)
            && !string.Equals(payload.EmbeddedScriptSha256, OfficeLiteDeploymentPreflightContract.EmbeddedScriptSha256, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("embeddedScriptSha256 is not the pinned value");
        }
        if ((!string.IsNullOrWhiteSpace(payload.SourceProjectInitialSha256)
                || payload.TerminalClassification == EnvironmentTerminalClassification.Ready)
            && (!string.Equals(payload.SourceProjectInitialSha256, payload.NegativeCopyInitialSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(payload.SourceProjectInitialSha256, payload.LiveCopyInitialSha256, StringComparison.OrdinalIgnoreCase)
                || !payload.SourceProjectUnchanged))
        {
            errors.Add("source/copy identity assertions are invalid");
        }
        if (!OfficeLiteDeploymentPreflightContract.RequiredUnsupportedGaps.SequenceEqual(payload.UnsupportedGaps, StringComparer.Ordinal))
        {
            errors.Add("unsupportedGaps do not match the contract");
        }
        foreach (var file in payload.Files.Where(file => file.Exists))
        {
            if (!File.Exists(file.Path) || !string.Equals(file.Sha256, ComputeFileSha256(file.Path), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"observed file is unavailable or changed: {file.Id}");
            }
        }
        if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready)
        {
            if (payload.LifecycleReceipt.Payload.TerminalClassification != EnvironmentTerminalClassification.Ready
                || !payload.LifecycleReceipt.Payload.CleanShutdownVerified
                || payload.NegativeControlCommand.ExitCode != 42
                || payload.NegativeControlCommand.TimedOut
                || payload.LiveCommand.ExitCode != 0
                || payload.LiveCommand.TimedOut
                || !payload.Preflight.Attempted)
            {
                errors.Add("ready receipt lacks complete differential preflight evidence");
            }
            foreach (var id in RequiredFileIds)
            {
                if (!payload.Files.Any(file => string.Equals(file.Id, id, StringComparison.Ordinal) && file.Exists))
                {
                    errors.Add($"required observed file is missing: {id}");
                }
            }
        }

        return new OfficeLiteDeploymentPreflightVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}

public static class OfficeLiteDeploymentPreflightReceiptWriter
{
    public static string WriteNew(string outputPath, OfficeLiteDeploymentPreflightReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        var verification = OfficeLiteDeploymentPreflightReceiptVerifier.Verify(receipt);
        if (!verification.Succeeded)
        {
            throw new InvalidOperationException("OfficeLite WorkVisual deployment-preflight receipt integrity is invalid.");
        }
        var fullPath = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Receipt output must use the .json extension.", nameof(outputPath));
        }
        var parent = Path.GetDirectoryName(fullPath) ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
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
