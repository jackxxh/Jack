using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class OfficeLiteControllerProfileReadbackContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.officelite-controller-profile-readback-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const string EmbeddedScriptResource = "KukaLab.Probes.WorkVisual.ControllerProfileReadback.csx";
    public const string EmbeddedScriptSha256 = "0D139E678661BA7F3A75644B34DE647F37E9A5EE59D386738205E82D6D09B095";
    public const string BeginMarker = "CONTROLLER_PROFILE_READBACK_BEGIN=True";
    public const string FailureMarker = "CONTROLLER_PROFILE_READBACK_FAILED:";

    internal static readonly IReadOnlyList<string> RequiredUnsupportedGaps =
    [
        "This readback proves runtime robot, KSS and project identities only; cabinet, machine-data and frame/load acceptance require the downloaded active project.",
        "This readback does not download, upload, activate, deploy, select or execute a controller program.",
        "KUKA.Sim profile matching and virtual motion remain separate validation steps."
    ];
}

public sealed record OfficeLiteControllerProfileReadbackRequest
{
    public required OfficeLiteCycleRequest OfficeLite { get; init; }

    public required string RunnerPath { get; init; }

    public required string EvidenceDirectory { get; init; }

    public int RunnerTimeoutSeconds { get; init; } = 30;

    public static OfficeLiteControllerProfileReadbackRequest CreateDefault(
        string assetRoot,
        string evidenceDirectory,
        string? vmrunPath = null,
        string? runnerPath = null,
        string? guestIpAddress = null,
        int readinessTimeoutSeconds = 240,
        int serviceObservationSeconds = 180,
        int runnerTimeoutSeconds = 30)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceDirectory);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return new OfficeLiteControllerProfileReadbackRequest
        {
            OfficeLite = OfficeLiteCycleRequest.CreateDefault(
                assetRoot,
                vmrunPath,
                guestIpAddress,
                readinessTimeoutSeconds: readinessTimeoutSeconds) with
            {
                DiagnoseWorkVisualServices = true,
                ServiceObservationSeconds = serviceObservationSeconds
            },
            RunnerPath = Path.GetFullPath(runnerPath
                ?? Path.Combine(programFilesX86, "KUKA", "WorkVisual 6.0", "wvsr.exe")),
            EvidenceDirectory = Path.GetFullPath(evidenceDirectory),
            RunnerTimeoutSeconds = runnerTimeoutSeconds
        };
    }
}

public sealed record OfficeLiteControllerProfileReadbackReceipt
{
    public string SchemaIdentity { get; init; } = OfficeLiteControllerProfileReadbackContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = OfficeLiteControllerProfileReadbackContract.ReceiptSchemaVersion;

    public required OfficeLiteControllerProfileReadbackPayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record OfficeLiteControllerProfileReadbackPayload
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

    public required OfficeLiteCycleReceipt LifecycleReceipt { get; init; }

    public WorkVisualRunnerCommandObservation NegativeControlCommand { get; init; } = new();

    public WorkVisualRunnerCommandObservation LiveCommand { get; init; } = new();

    public OfficeLiteControllerProfileObservation Profile { get; init; } = new();

    public bool ReadOnlyOperation { get; init; } = true;

    public bool CredentialsUsed { get; init; }

    public bool ControllerMutationPerformed { get; init; }

    public NativeKssStatus NativeKssStatus { get; init; } = NativeKssStatus.NotRun;

    public List<EnvironmentCheck> Checks { get; init; } = [];

    public List<EnvironmentFileObservation> Files { get; init; } = [];

    public List<string> SideEffects { get; init; } = [];

    public bool EnvironmentReusable { get; init; }

    public List<string> UnsupportedGaps { get; init; } = [];
}

public sealed record OfficeLiteControllerProfileObservation
{
    public bool Attempted { get; init; }

    public string? ControllerAddress { get; init; }

    public string? RobotType { get; init; }

    public string? KssVersion { get; init; }

    public string? CurrentProjectName { get; init; }

    public string? ActiveProject { get; init; }

    public string? BaseProject { get; init; }

    public string? InitialProject { get; init; }

    public int? ProjectCount { get; init; }
}

public sealed record OfficeLiteControllerProfileReadbackOutcome(OfficeLiteControllerProfileReadbackReceipt Receipt)
{
    public int ExitCode => Receipt.Payload.TerminalClassification switch
    {
        EnvironmentTerminalClassification.Ready => 0,
        EnvironmentTerminalClassification.Failed => 2,
        _ => 3
    };
}

public sealed class OfficeLiteControllerProfileReadbackRunner
{
    private readonly OfficeLiteCycleRunner _officeLiteRunner;
    private readonly IWorkVisualRunnerPlatform _workVisualPlatform;
    private readonly TimeProvider _timeProvider;

    public OfficeLiteControllerProfileReadbackRunner()
        : this(new OfficeLiteCycleRunner(), new WorkVisualRunnerPlatform(), TimeProvider.System)
    {
    }

    internal OfficeLiteControllerProfileReadbackRunner(
        OfficeLiteCycleRunner officeLiteRunner,
        IWorkVisualRunnerPlatform workVisualPlatform,
        TimeProvider timeProvider)
    {
        _officeLiteRunner = officeLiteRunner;
        _workVisualPlatform = workVisualPlatform;
        _timeProvider = timeProvider;
    }

    public OfficeLiteControllerProfileReadbackOutcome Run(
        OfficeLiteControllerProfileReadbackRequest request,
        string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAttemptId(attemptId);
        if (request.RunnerTimeoutSeconds is < 1 or > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "WorkVisual runner timeout must be from 1 through 120 seconds.");
        }

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var checks = new List<EnvironmentCheck>();
        var files = new List<EnvironmentFileObservation>();
        var sideEffects = new List<string>();
        var runnerPath = Path.GetFullPath(request.RunnerPath);
        var evidenceDirectory = Path.GetFullPath(request.EvidenceDirectory);
        var scriptHash = string.Empty;
        var negative = new WorkVisualRunnerCommandObservation { CleanupVerified = true };
        var live = new WorkVisualRunnerCommandObservation { CleanupVerified = true };
        var profile = new OfficeLiteControllerProfileObservation();
        var runnerCleanupVerified = true;
        OfficeLiteCycleReceipt? lifecycleReceipt = null;

        if (!OperatingSystem.IsWindows())
        {
            checks.Add(Blocked("host-os", "OfficeLite and WorkVisual Script Runner require Windows."));
            return Complete();
        }

        if (!ObserveFile("workvisual-script-runner", runnerPath, true, files, checks))
        {
            return Complete();
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
            scriptPath = Path.Combine(evidenceDirectory, $"{attemptId}.controller-profile-readback.csx");
            var scriptBytes = ReadEmbeddedScript();
            scriptHash = Convert.ToHexString(SHA256.HashData(scriptBytes));
            if (!string.Equals(
                    scriptHash,
                    OfficeLiteControllerProfileReadbackContract.EmbeddedScriptSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                checks.Add(Failed("read-only-script", "Embedded controller-profile script hash is not the pinned read-only value."));
                return Complete();
            }

            WriteNew(scriptPath, scriptBytes);
            sideEffects.Add($"CreateProbeScript:{scriptPath}");
            files.Add(ObserveExistingFile("workvisual-controller-profile-script", scriptPath, false));
            checks.Add(Passed("read-only-script", "Pinned read-only controller-profile script was materialized as create-new evidence."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            checks.Add(Failed("evidence-directory", $"Evidence preparation failed safely: {exception.Message}"));
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
            context => ExecuteReadOnlyProfile(context.GuestAddress, scriptPath));
        lifecycleReceipt = lifecycle.Receipt;

        if (lifecycleReceipt.Payload.TerminalClassification == EnvironmentTerminalClassification.Ready
            && HasCompleteProfile(profile)
            && checks.All(check => check.Status is not EnvironmentCheckStatus.Failed and not EnvironmentCheckStatus.Blocked))
        {
            checks.Add(Passed("controller-profile-readback", "The controller returned complete runtime profile and project identities through the pinned read-only WorkVisual API."));
        }
        else if (!profile.Attempted)
        {
            checks.Add(Blocked("controller-profile-readback", "The readback did not run because OfficeLite DeviceInfo readiness was not established."));
        }

        checks.Add(lifecycleReceipt.Payload.CleanShutdownVerified
            ? Passed("environment-cleanup", "OfficeLite soft shutdown and WorkVisual runner cleanup were verified.")
            : Failed("environment-cleanup", "OfficeLite soft shutdown was not verified."));
        return Complete();

        void ExecuteReadOnlyProfile(string address, string materializedScriptPath)
        {
            var negativeResult = RunWorkVisual("127.0.0.1", materializedScriptPath);
            negative = ToObservation(negativeResult);
            runnerCleanupVerified &= negativeResult.CleanupVerified;
            PreserveRawEvidence("negative", negativeResult);
            if (negativeResult.TimedOut
                || negativeResult.ExitCode != 42
                || !negativeResult.StandardOutput.Contains(
                    OfficeLiteControllerProfileReadbackContract.FailureMarker,
                    StringComparison.Ordinal))
            {
                checks.Add(Failed("negative-control", "The unreachable-address control did not produce the pinned typed failure and exit code 42."));
                return;
            }

            checks.Add(Passed("negative-control", "The unreachable-address control produced the expected typed failure and exit code 42."));
            var liveResult = RunWorkVisual(address, materializedScriptPath);
            live = ToObservation(liveResult);
            runnerCleanupVerified &= liveResult.CleanupVerified;
            PreserveRawEvidence("live", liveResult);
            profile = ParseProfile(address, liveResult.StandardOutput);

            if (!liveResult.CleanupVerified)
            {
                checks.Add(Failed("workvisual-runner-cleanup", "WorkVisual Script Runner cleanup could not be verified."));
            }
            else if (liveResult.TimedOut)
            {
                checks.Add(Blocked("live-query", "The live read-only WorkVisual query exceeded its bounded timeout."));
            }
            else if (liveResult.ExitCode == 42
                && liveResult.StandardOutput.Contains(
                    OfficeLiteControllerProfileReadbackContract.FailureMarker,
                    StringComparison.Ordinal))
            {
                checks.Add(Blocked("live-query", "The WorkVisual client reached its typed online-controller failure path for the live OfficeLite address."));
            }
            else if (liveResult.ExitCode != 0)
            {
                checks.Add(Failed("live-query", $"The live read-only WorkVisual query returned unexpected exit code {liveResult.ExitCode}."));
            }
            else if (!HasCompleteProfile(profile))
            {
                checks.Add(Failed("live-query", "The live query exited successfully but did not emit the complete controller-profile contract."));
            }
            else
            {
                checks.Add(Passed("live-query", "The live read-only WorkVisual query returned the complete controller-profile contract."));
            }
        }

        WorkVisualProcessResult RunWorkVisual(string address, string materializedScriptPath)
        {
            try
            {
                return _workVisualPlatform.Run(
                    runnerPath,
                    ["-executescript", $"-scriptpath={materializedScriptPath}", $"-address={address}"],
                    evidenceDirectory,
                    TimeSpan.FromSeconds(request.RunnerTimeoutSeconds));
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or Win32Exception)
            {
                return new WorkVisualProcessResult(
                    -1,
                    false,
                    true,
                    0,
                    string.Empty,
                    $"{exception.GetType().Name}: {exception.Message}");
            }
        }

        void PreserveRawEvidence(string prefix, WorkVisualProcessResult result)
        {
            var stdoutPath = Path.Combine(evidenceDirectory, $"{attemptId}.{prefix}.stdout.log");
            var stderrPath = Path.Combine(evidenceDirectory, $"{attemptId}.{prefix}.stderr.log");
            WriteNew(stdoutPath, Encoding.UTF8.GetBytes(result.StandardOutput));
            WriteNew(stderrPath, Encoding.UTF8.GetBytes(result.StandardError));
            sideEffects.Add($"CreateRawStdout:{stdoutPath}");
            sideEffects.Add($"CreateRawStderr:{stderrPath}");
            files.Add(ObserveExistingFile($"workvisual-{prefix}-stdout", stdoutPath, false));
            files.Add(ObserveExistingFile($"workvisual-{prefix}-stderr", stderrPath, false));
        }

        OfficeLiteControllerProfileReadbackOutcome Complete()
        {
            stopwatch.Stop();
            lifecycleReceipt ??= CreateUnavailableLifecycleReceipt(request.OfficeLite, attemptId, startedAt);
            var terminal = checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
                ? EnvironmentTerminalClassification.Failed
                : checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked)
                    || lifecycleReceipt.Payload.TerminalClassification != EnvironmentTerminalClassification.Ready
                    ? EnvironmentTerminalClassification.Blocked
                    : EnvironmentTerminalClassification.Ready;
            var payload = new OfficeLiteControllerProfileReadbackPayload
            {
                ReceiptId = $"officelite-controller-profile-readback-{attemptId}",
                AttemptId = attemptId,
                CoreAssemblySha256 = ComputeFileSha256(typeof(OfficeLiteControllerProfileReadbackRunner).Assembly.Location),
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
                LifecycleReceipt = lifecycleReceipt,
                NegativeControlCommand = negative,
                LiveCommand = live,
                Profile = profile,
                ReadOnlyOperation = true,
                CredentialsUsed = false,
                ControllerMutationPerformed = false,
                NativeKssStatus = NativeKssStatus.NotRun,
                Checks = checks,
                Files = files,
                SideEffects = sideEffects,
                EnvironmentReusable = runnerCleanupVerified && lifecycleReceipt.Payload.CleanShutdownVerified,
                UnsupportedGaps = OfficeLiteControllerProfileReadbackContract.RequiredUnsupportedGaps.ToList()
            };
            var receipt = new OfficeLiteControllerProfileReadbackReceipt
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
            };
            return new OfficeLiteControllerProfileReadbackOutcome(receipt);
        }
    }

    internal static OfficeLiteControllerProfileObservation ParseProfile(string address, string output) => new()
    {
        Attempted = output.Contains(OfficeLiteControllerProfileReadbackContract.BeginMarker, StringComparison.Ordinal),
        ControllerAddress = address,
        RobotType = DecodeBase64(ReadMarker(output, "PROFILE_ROBOT_B64")),
        KssVersion = DecodeBase64(ReadMarker(output, "PROFILE_KSS_B64")),
        CurrentProjectName = DecodeBase64(ReadMarker(output, "PROFILE_PROJECT_B64")),
        ActiveProject = DecodeBase64(ReadMarker(output, "ACTIVE_PROJECT_B64")),
        BaseProject = DecodeBase64(ReadMarker(output, "BASE_PROJECT_B64")),
        InitialProject = DecodeBase64(ReadMarker(output, "INITIAL_PROJECT_B64")),
        ProjectCount = int.TryParse(ReadMarker(output, "PROJECT_COUNT"), out var count) ? count : null
    };

    internal static bool HasCompleteProfile(OfficeLiteControllerProfileObservation observation) =>
        observation.Attempted
        && !string.IsNullOrWhiteSpace(observation.RobotType)
        && !string.IsNullOrWhiteSpace(observation.KssVersion)
        && !string.IsNullOrWhiteSpace(observation.CurrentProjectName)
        && observation.ActiveProject is not null
        && observation.BaseProject is not null
        && observation.InitialProject is not null
        && observation.ProjectCount is >= 0;

    private static string? ReadMarker(string output, string name)
    {
        var match = Regex.Match(
            output,
            $"(?m)^{Regex.Escape(name)}=(?<value>.*)\\r?$",
            RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static string? DecodeBase64(string? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static WorkVisualRunnerCommandObservation ToObservation(WorkVisualProcessResult result) =>
        new()
        {
            ExitCode = result.ExitCode,
            TimedOut = result.TimedOut,
            CleanupVerified = result.CleanupVerified,
            DurationMilliseconds = Math.Max(0, result.DurationMilliseconds)
        };

    private static byte[] ReadEmbeddedScript()
    {
        using var stream = typeof(OfficeLiteControllerProfileReadbackRunner).Assembly.GetManifestResourceStream(
            OfficeLiteControllerProfileReadbackContract.EmbeddedScriptResource)
            ?? throw new InvalidOperationException("Pinned WorkVisual controller-profile resource is missing.");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        var canonicalText = reader.ReadToEnd()
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
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
            UnsupportedGaps =
            [
                "Native KSS compilation was not invoked by this VM lifecycle attempt.",
                "The runtime KSS build and controller archive still require controller-native evidence."
            ]
        };
        return new OfficeLiteCycleReceipt
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
    }

    private static bool ObserveFile(
        string id,
        string path,
        bool includeVersion,
        List<EnvironmentFileObservation> files,
        List<EnvironmentCheck> checks)
    {
        if (!File.Exists(path))
        {
            files.Add(new EnvironmentFileObservation { Id = id, Path = path, Exists = false });
            checks.Add(Blocked(id, $"Required file is missing: {path}"));
            return false;
        }

        files.Add(ObserveExistingFile(id, path, includeVersion));
        checks.Add(Passed(id, "Required file exists and was hashed."));
        return true;
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

    private static EnvironmentCheck Passed(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Passed, Detail = detail };

    private static EnvironmentCheck Blocked(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Blocked, Detail = detail };

    private static EnvironmentCheck Failed(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Failed, Detail = detail };
}

public sealed record OfficeLiteControllerProfileReadbackVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}

public static class OfficeLiteControllerProfileReadbackReceiptVerifier
{
    private static readonly string[] RequiredFileIds =
    [
        "workvisual-script-runner",
        "workvisual-controller-profile-script",
        "workvisual-negative-stdout",
        "workvisual-negative-stderr",
        "workvisual-live-stdout",
        "workvisual-live-stderr"
    ];

    public static OfficeLiteControllerProfileReadbackVerificationResult Verify(
        OfficeLiteControllerProfileReadbackReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        var payload = receipt.Payload;
        if (!string.Equals(receipt.SchemaIdentity, OfficeLiteControllerProfileReadbackContract.ReceiptSchemaIdentity, StringComparison.Ordinal))
        {
            errors.Add("schemaIdentity is invalid");
        }

        if (receipt.SchemaVersion != OfficeLiteControllerProfileReadbackContract.ReceiptSchemaVersion)
        {
            errors.Add("schemaVersion is invalid");
        }

        if (!string.Equals(receipt.PayloadSha256, ReceiptSerialization.ComputeCanonicalSha256(payload), StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("payloadSha256 does not match the canonical payload");
        }

        if (!OfficeLiteCycleReceiptVerifier.Verify(payload.LifecycleReceipt).Succeeded)
        {
            errors.Add("lifecycleReceipt integrity is invalid");
        }

        if (!payload.ReadOnlyOperation || payload.CredentialsUsed || payload.ControllerMutationPerformed)
        {
            errors.Add("read-only safety assertions are invalid");
        }

        if (payload.NativeKssStatus != NativeKssStatus.NotRun)
        {
            errors.Add("nativeKssStatus must remain NotRun");
        }

        if (!string.Equals(payload.EmbeddedScriptSha256, OfficeLiteControllerProfileReadbackContract.EmbeddedScriptSha256, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("embeddedScriptSha256 is not the pinned value");
        }

        if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready)
        {
            if (payload.LifecycleReceipt.Payload.TerminalClassification != EnvironmentTerminalClassification.Ready
                || !payload.LifecycleReceipt.Payload.WorkVisualServices.DeviceInfoEverOpen
                || !payload.LifecycleReceipt.Payload.CleanShutdownVerified)
            {
                errors.Add("ready receipt lacks a ready and clean OfficeLite lifecycle");
            }

            if (payload.NegativeControlCommand.ExitCode != 42
                || payload.NegativeControlCommand.TimedOut
                || payload.LiveCommand.ExitCode != 0
                || payload.LiveCommand.TimedOut
                || !HasCompleteProfile(payload.Profile))
            {
                errors.Add("ready receipt lacks successful differential controller-profile evidence");
            }
        }

        foreach (var file in payload.Files.Where(file => file.Exists))
        {
            if (string.IsNullOrWhiteSpace(file.Sha256) || !File.Exists(file.Path))
            {
                errors.Add($"observed file is unavailable: {file.Id}");
                continue;
            }

            if (!string.Equals(file.Sha256, ComputeFileSha256(file.Path), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"observed file hash changed: {file.Id}");
            }
        }

        if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready)
        {
            var liveStdout = payload.Files.SingleOrDefault(file =>
                string.Equals(file.Id, "workvisual-live-stdout", StringComparison.Ordinal));
            if (liveStdout is null || !liveStdout.Exists || !File.Exists(liveStdout.Path))
            {
                errors.Add("raw WorkVisual output is unavailable for profile reparse");
            }
            else
            {
                try
                {
                    var reparsed = OfficeLiteControllerProfileReadbackRunner.ParseProfile(
                        payload.LifecycleReceipt.Payload.GuestEndpoint.Address ?? string.Empty,
                        File.ReadAllText(liveStdout.Path, Encoding.UTF8));
                    if (reparsed != payload.Profile)
                    {
                        errors.Add("controller profile does not match the raw WorkVisual output");
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    errors.Add($"raw WorkVisual output could not be reparsed: {exception.Message}");
                }
            }
        }

        if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready)
        {
            foreach (var id in RequiredFileIds)
            {
                if (!payload.Files.Any(file => string.Equals(file.Id, id, StringComparison.Ordinal) && file.Exists))
                {
                    errors.Add($"required observed file is missing: {id}");
                }
            }
        }

        if (!OfficeLiteControllerProfileReadbackContract.RequiredUnsupportedGaps.SequenceEqual(payload.UnsupportedGaps, StringComparer.Ordinal))
        {
            errors.Add("unsupportedGaps do not match the contract");
        }

        return new OfficeLiteControllerProfileReadbackVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }

    private static bool HasCompleteProfile(OfficeLiteControllerProfileObservation observation) =>
        observation.Attempted
        && !string.IsNullOrWhiteSpace(observation.RobotType)
        && !string.IsNullOrWhiteSpace(observation.KssVersion)
        && !string.IsNullOrWhiteSpace(observation.CurrentProjectName)
        && observation.ActiveProject is not null
        && observation.BaseProject is not null
        && observation.InitialProject is not null
        && observation.ProjectCount is >= 0;

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}

public static class OfficeLiteControllerProfileReadbackReceiptWriter
{
    public static string WriteNew(string outputPath, OfficeLiteControllerProfileReadbackReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!OfficeLiteControllerProfileReadbackReceiptVerifier.Verify(receipt).Succeeded)
        {
            throw new InvalidOperationException("Cannot write an invalid controller-profile readback receipt.");
        }

        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(ReceiptSerialization.ToJson(receipt));
        writer.Flush();
        stream.Flush(flushToDisk: true);
        return fullPath;
    }
}
