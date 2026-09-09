using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class OfficeLiteActiveProjectDownloadContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.officelite-active-project-download-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const string EmbeddedScriptResource = "KukaLab.Probes.WorkVisual.ActiveProjectDownload.csx";
    public const string EmbeddedScriptSha256 = "F3EDBE4277E0AFEEDB4B0BFF66EE8A7D682AC6B087D9F479E8EE7A468B022ED7";
    public const string FailureMarker = "ACTIVE_PROJECT_DOWNLOAD_FAILED:";

    internal static readonly IReadOnlyList<string> RequiredCheckIds =
    [
        "workvisual-script-runner",
        "fixed-download-script",
        "negative-control",
        "negative-no-file",
        "live-download",
        "downloaded-wvs",
        "environment-cleanup"
    ];

    internal static readonly IReadOnlyList<string> RequiredUnsupportedGaps =
    [
        "This receipt proves a controller-to-PC download from the isolated OfficeLite fixture only; it is not physical-controller evidence.",
        "The live address is resolved only from the exact OfficeLite VMX MAC and VMware DHCP lease; caller-supplied guest addresses are prohibited.",
        "The downloaded WVS is byte-bound but its contents, robot profile, machine data, technology packages and Tool/Base/Load are not inspected by this operation.",
        "No project is uploaded, deployed, activated, pinned, merged, opened, saved or written back to a controller.",
        "Native KSS execution and KUKA.Sim robot-profile matching remain separate validation steps."
    ];
}

public sealed record OfficeLiteActiveProjectDownloadRequest
{
    public required OfficeLiteCycleRequest OfficeLite { get; init; }

    public required string RunnerPath { get; init; }

    public required string EvidenceDirectory { get; init; }

    public int RunnerTimeoutSeconds { get; init; } = 120;

    public static OfficeLiteActiveProjectDownloadRequest CreateDefault(
        string assetRoot,
        string evidenceDirectory,
        string? vmrunPath = null,
        string? runnerPath = null,
        int readinessTimeoutSeconds = 240,
        int serviceObservationSeconds = 180,
        int runnerTimeoutSeconds = 120)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceDirectory);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return new OfficeLiteActiveProjectDownloadRequest
        {
            OfficeLite = OfficeLiteCycleRequest.CreateDefault(
                assetRoot,
                vmrunPath,
                guestIpAddress: null,
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

public sealed record OfficeLiteActiveProjectDownloadReceipt
{
    public string SchemaIdentity { get; init; } = OfficeLiteActiveProjectDownloadContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = OfficeLiteActiveProjectDownloadContract.ReceiptSchemaVersion;

    public required OfficeLiteActiveProjectDownloadPayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record OfficeLiteActiveProjectDownloadPayload
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

    public string ActiveProject { get; init; } = string.Empty;

    public int? ProjectCount { get; init; }

    public EnvironmentFileObservation DownloadedProject { get; init; } = new();

    public bool ControllerToPcDownloadPerformed { get; init; }

    public bool ReadOnlyControllerOperation { get; init; } = true;

    public bool PhysicalControllerContacted { get; init; }

    public bool GuestIpOverrideUsed { get; init; }

    public bool CredentialsUsed { get; init; }

    public bool ControllerProjectModified { get; init; }

    public bool ProjectOpenedOrSaved { get; init; }

    public NativeKssStatus NativeKssStatus { get; init; } = NativeKssStatus.NotRun;

    public List<EnvironmentCheck> Checks { get; init; } = [];

    public List<EnvironmentFileObservation> Files { get; init; } = [];

    public List<string> SideEffects { get; init; } = [];

    public bool EnvironmentReusable { get; init; }

    public List<string> UnsupportedGaps { get; init; } = [];
}

public sealed record OfficeLiteActiveProjectDownloadOutcome(OfficeLiteActiveProjectDownloadReceipt Receipt)
{
    public int ExitCode => Receipt.Payload.TerminalClassification switch
    {
        EnvironmentTerminalClassification.Ready => 0,
        EnvironmentTerminalClassification.Failed => 2,
        _ => 3
    };
}

public sealed class OfficeLiteActiveProjectDownloadRunner
{
    private readonly OfficeLiteCycleRunner _officeLiteRunner;
    private readonly IWorkVisualRunnerPlatform _workVisualPlatform;
    private readonly TimeProvider _timeProvider;

    public OfficeLiteActiveProjectDownloadRunner()
        : this(new OfficeLiteCycleRunner(), new WorkVisualRunnerPlatform(), TimeProvider.System)
    {
    }

    internal OfficeLiteActiveProjectDownloadRunner(
        OfficeLiteCycleRunner officeLiteRunner,
        IWorkVisualRunnerPlatform workVisualPlatform,
        TimeProvider timeProvider)
    {
        _officeLiteRunner = officeLiteRunner;
        _workVisualPlatform = workVisualPlatform;
        _timeProvider = timeProvider;
    }

    public OfficeLiteActiveProjectDownloadOutcome Run(
        OfficeLiteActiveProjectDownloadRequest request,
        string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAttemptId(attemptId);
        if (request.RunnerTimeoutSeconds is < 1 or > 300)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "WorkVisual runner timeout must be from 1 through 300 seconds.");
        }

        if (!string.IsNullOrWhiteSpace(request.OfficeLite.GuestIpAddress))
        {
            throw new ArgumentException(
                "Active-project download forbids caller-supplied guest addresses; the exact OfficeLite VMX MAC and VMware DHCP lease must resolve the endpoint.",
                nameof(request));
        }

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var checks = new List<EnvironmentCheck>();
        var files = new List<EnvironmentFileObservation>();
        var sideEffects = new List<string>();
        var runnerPath = Path.GetFullPath(request.RunnerPath);
        var evidenceDirectory = Path.GetFullPath(request.EvidenceDirectory);
        var downloadDirectory = Path.Combine(evidenceDirectory, "download");
        var scriptHash = string.Empty;
        var negative = new WorkVisualRunnerCommandObservation { CleanupVerified = true };
        var live = new WorkVisualRunnerCommandObservation { CleanupVerified = true };
        var activeProject = string.Empty;
        int? projectCount = null;
        var downloadedProject = new EnvironmentFileObservation { Id = "downloaded-active-project" };
        var downloadPerformed = false;
        var runnerCleanupVerified = true;
        OfficeLiteCycleReceipt? lifecycleReceipt = null;

        if (!OperatingSystem.IsWindows())
        {
            checks.Add(Blocked("workvisual-script-runner", "OfficeLite and WorkVisual Script Runner require Windows."));
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
                checks.Add(Failed("fixed-download-script", "Evidence directory already exists; create-new semantics refused reuse."));
                return Complete();
            }

            Directory.CreateDirectory(downloadDirectory);
            sideEffects.Add($"CreateEvidenceDirectory:{evidenceDirectory}");
            sideEffects.Add($"CreateDownloadDirectory:{downloadDirectory}");
            scriptPath = Path.Combine(evidenceDirectory, $"{attemptId}.active-project-download.csx");
            var scriptBytes = ReadEmbeddedScript();
            scriptHash = Convert.ToHexString(SHA256.HashData(scriptBytes));
            if (!string.Equals(
                    scriptHash,
                    OfficeLiteActiveProjectDownloadContract.EmbeddedScriptSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                checks.Add(Failed("fixed-download-script", "Embedded active-project download script hash is not the pinned value."));
                return Complete();
            }

            WriteNew(scriptPath, scriptBytes);
            sideEffects.Add($"CreateProbeScript:{scriptPath}");
            files.Add(ObserveExistingFile("workvisual-active-download-script", scriptPath, false));
            checks.Add(Passed("fixed-download-script", "Pinned active-project download script was materialized as create-new evidence."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            checks.Add(Failed("fixed-download-script", $"Evidence preparation failed safely: {exception.Message}"));
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
            context => ExecuteDownload(context.GuestAddress, scriptPath));
        lifecycleReceipt = lifecycle.Receipt;

        if (lifecycleReceipt.Payload.TerminalClassification == EnvironmentTerminalClassification.Ready
            && downloadPerformed
            && checks.All(check => check.Status is not EnvironmentCheckStatus.Failed and not EnvironmentCheckStatus.Blocked))
        {
            checks.Add(Passed("live-download", "The isolated controller returned its active project through the pinned controller-to-PC WorkVisual API."));
        }
        else if (!checks.Any(check => check.Id == "live-download"))
        {
            checks.Add(Blocked("live-download", "The active-project download did not complete because OfficeLite DeviceInfo readiness or a preceding guard was not established."));
        }

        checks.Add(lifecycleReceipt.Payload.CleanShutdownVerified
            ? Passed("environment-cleanup", "OfficeLite soft shutdown and WorkVisual runner cleanup were verified.")
            : Failed("environment-cleanup", "OfficeLite soft shutdown was not verified."));
        return Complete();

        void ExecuteDownload(string address, string materializedScriptPath)
        {
            var negativeResult = RunWorkVisual("127.0.0.1", materializedScriptPath);
            negative = ToObservation(negativeResult);
            runnerCleanupVerified &= negativeResult.CleanupVerified;
            PreserveRawEvidence("negative", negativeResult);

            if (negativeResult.TimedOut
                || negativeResult.ExitCode != 42
                || !negativeResult.StandardOutput.Contains(
                    OfficeLiteActiveProjectDownloadContract.FailureMarker,
                    StringComparison.Ordinal))
            {
                checks.Add(Failed("negative-control", "The unreachable-address control did not produce the pinned typed failure and exit code 42."));
                return;
            }

            checks.Add(Passed("negative-control", "The unreachable-address control produced the expected typed failure and exit code 42."));
            if (Directory.EnumerateFileSystemEntries(downloadDirectory).Any())
            {
                checks.Add(Failed("negative-no-file", "The unreachable-address control unexpectedly created a download artifact."));
                return;
            }

            checks.Add(Passed("negative-no-file", "The unreachable-address control created no download artifact."));
            var liveResult = RunWorkVisual(address, materializedScriptPath);
            live = ToObservation(liveResult);
            runnerCleanupVerified &= liveResult.CleanupVerified;
            PreserveRawEvidence("live", liveResult);

            if (!liveResult.CleanupVerified)
            {
                checks.Add(Failed("live-download", "WorkVisual Script Runner cleanup could not be verified."));
                return;
            }

            if (liveResult.TimedOut)
            {
                checks.Add(Blocked("live-download", "The live active-project download exceeded its bounded timeout."));
                return;
            }

            if (liveResult.ExitCode == 42
                && liveResult.StandardOutput.Contains(
                    OfficeLiteActiveProjectDownloadContract.FailureMarker,
                    StringComparison.Ordinal))
            {
                checks.Add(Blocked("live-download", "The WorkVisual client reached its typed online-controller failure path for the live OfficeLite address."));
                return;
            }

            if (liveResult.ExitCode != 0)
            {
                checks.Add(Failed("live-download", $"The live active-project download returned unexpected exit code {liveResult.ExitCode}."));
                return;
            }

            activeProject = DecodeMarker(liveResult.StandardOutput, "ActiveProjectBase64") ?? string.Empty;
            projectCount = int.TryParse(ReadMarker(liveResult.StandardOutput, "ProjectCount"), out var count) ? count : null;
            var returnedPath = DecodeMarker(liveResult.StandardOutput, "DownloadedProjectPathBase64");
            if (string.IsNullOrWhiteSpace(activeProject)
                || projectCount is null or < 1
                || string.IsNullOrWhiteSpace(returnedPath))
            {
                checks.Add(Failed("live-download", "The live call exited successfully but did not emit the complete download contract."));
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(returnedPath);
                EnsureDescendant(downloadDirectory, fullPath);
                if (!string.Equals(Path.GetExtension(fullPath), ".wvs", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The returned controller project does not use the WVS extension.");
                }

                downloadedProject = ObserveExistingFile("downloaded-active-project", fullPath, false);
                if (downloadedProject.Bytes <= 0)
                {
                    throw new InvalidDataException("The returned controller project is empty.");
                }

                files.Add(downloadedProject);
                sideEffects.Add($"CreateDownloadedProject:{fullPath}");
                downloadPerformed = true;
                checks.Add(Passed("downloaded-wvs", "The returned WVS exists below the create-new download directory and has a non-empty SHA-256-bound identity."));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
            {
                checks.Add(Failed("downloaded-wvs", $"Downloaded project validation failed: {exception.Message}"));
            }
        }

        WorkVisualProcessResult RunWorkVisual(string address, string materializedScriptPath)
        {
            try
            {
                return _workVisualPlatform.Run(
                    runnerPath,
                    [
                        "-executescript",
                        $"-scriptpath={materializedScriptPath}",
                        $"-address={address}",
                        $"-destinationfolder={downloadDirectory}"
                    ],
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

        OfficeLiteActiveProjectDownloadOutcome Complete()
        {
            stopwatch.Stop();
            lifecycleReceipt ??= CreateUnavailableLifecycleReceipt(request.OfficeLite, attemptId, startedAt);
            var terminal = checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
                ? EnvironmentTerminalClassification.Failed
                : checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked)
                    || lifecycleReceipt.Payload.TerminalClassification != EnvironmentTerminalClassification.Ready
                    ? EnvironmentTerminalClassification.Blocked
                    : EnvironmentTerminalClassification.Ready;
            var payload = new OfficeLiteActiveProjectDownloadPayload
            {
                ReceiptId = $"officelite-active-project-download-{attemptId}",
                AttemptId = attemptId,
                CoreAssemblySha256 = ComputeFileSha256(typeof(OfficeLiteActiveProjectDownloadRunner).Assembly.Location),
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
                ActiveProject = activeProject,
                ProjectCount = projectCount,
                DownloadedProject = downloadedProject,
                ControllerToPcDownloadPerformed = downloadPerformed,
                ReadOnlyControllerOperation = true,
                PhysicalControllerContacted = false,
                GuestIpOverrideUsed = false,
                CredentialsUsed = false,
                ControllerProjectModified = false,
                ProjectOpenedOrSaved = false,
                NativeKssStatus = NativeKssStatus.NotRun,
                Checks = checks,
                Files = files,
                SideEffects = sideEffects,
                EnvironmentReusable = runnerCleanupVerified && lifecycleReceipt.Payload.CleanShutdownVerified,
                UnsupportedGaps = OfficeLiteActiveProjectDownloadContract.RequiredUnsupportedGaps.ToList()
            };
            var receipt = new OfficeLiteActiveProjectDownloadReceipt
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
            };
            return new OfficeLiteActiveProjectDownloadOutcome(receipt);
        }
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

    private static string? DecodeMarker(string output, string name)
    {
        var encoded = ReadMarker(output, name);
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? ReadMarker(string output, string name)
    {
        var match = Regex.Match(
            output,
            $"(?m)^{Regex.Escape(name)}=(?<value>.*)\\r?$",
            RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
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
        using var stream = typeof(OfficeLiteActiveProjectDownloadRunner).Assembly.GetManifestResourceStream(
            OfficeLiteActiveProjectDownloadContract.EmbeddedScriptResource)
            ?? throw new InvalidOperationException("Pinned WorkVisual active-project download resource is missing.");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true);
        var canonicalText = reader.ReadToEnd()
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return Encoding.UTF8.GetBytes(canonicalText);
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

    private static void EnsureDescendant(string root, string path)
    {
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var pathFull = Path.GetFullPath(path);
        if (!pathFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The returned project path is outside the create-new download directory.");
        }
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

public sealed record OfficeLiteActiveProjectDownloadVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}

public static class OfficeLiteActiveProjectDownloadReceiptVerifier
{
    private static readonly string[] RequiredFileIds =
    [
        "workvisual-script-runner",
        "workvisual-active-download-script",
        "workvisual-negative-stdout",
        "workvisual-negative-stderr",
        "workvisual-live-stdout",
        "workvisual-live-stderr",
        "downloaded-active-project"
    ];

    public static OfficeLiteActiveProjectDownloadVerificationResult Verify(
        OfficeLiteActiveProjectDownloadReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (ReferenceEquals(receipt.Payload, null))
        {
            return new OfficeLiteActiveProjectDownloadVerificationResult
            {
                Succeeded = false,
                PayloadSha256 = receipt.PayloadSha256,
                Errors = ["payload is required"]
            };
        }

        if (receipt.SchemaIdentity != OfficeLiteActiveProjectDownloadContract.ReceiptSchemaIdentity
            || receipt.SchemaVersion != OfficeLiteActiveProjectDownloadContract.ReceiptSchemaVersion)
        {
            errors.Add("schema identity/version is unsupported");
        }

        var payload = receipt.Payload;
        var checks = payload.Checks ?? [];
        var files = payload.Files ?? [];
        var unsupportedGaps = payload.UnsupportedGaps ?? [];
        var canonicalHash = ReceiptSerialization.ComputeCanonicalSha256(payload);
        if (!string.Equals(canonicalHash, receipt.PayloadSha256, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("payload SHA-256 does not match canonical payload");
        }

        if (string.IsNullOrWhiteSpace(payload.ReceiptId)
            || string.IsNullOrWhiteSpace(payload.AttemptId)
            || !string.Equals(
                payload.ReceiptId,
                $"officelite-active-project-download-{payload.AttemptId}",
                StringComparison.Ordinal)
            || payload.CoreAssemblySha256.Length != 64
            || payload.CoreAssemblySha256.Any(character => !Uri.IsHexDigit(character))
            || payload.Runtime is null
            || string.IsNullOrWhiteSpace(payload.Runtime.OsDescription)
            || string.IsNullOrWhiteSpace(payload.Runtime.FrameworkDescription)
            || string.IsNullOrWhiteSpace(payload.Runtime.ProcessArchitecture)
            || payload.StartedAtUtc > payload.CompletedAtUtc
            || payload.DurationMilliseconds < 0)
        {
            errors.Add("receipt, attempt, core, runtime or timing identity is invalid");
        }

        if (!string.Equals(
                payload.EmbeddedScriptSha256,
                OfficeLiteActiveProjectDownloadContract.EmbeddedScriptSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("embedded script SHA-256 is not pinned");
        }

        if (!payload.ReadOnlyControllerOperation
            || payload.PhysicalControllerContacted
            || payload.GuestIpOverrideUsed
            || payload.CredentialsUsed
            || payload.ControllerProjectModified
            || payload.ProjectOpenedOrSaved
            || payload.NativeKssStatus != NativeKssStatus.NotRun)
        {
            errors.Add("controller safety claims are invalid");
        }

        if (payload.LifecycleReceipt is null
            || !OfficeLiteCycleReceiptVerifier.Verify(payload.LifecycleReceipt).Succeeded)
        {
            errors.Add("nested OfficeLite lifecycle receipt is invalid");
        }

        var checkIds = checks.Select(check => check.Id).ToArray();
        if (checkIds.Distinct(StringComparer.Ordinal).Count() != checkIds.Length
            || checkIds.Any(id => !OfficeLiteActiveProjectDownloadContract.RequiredCheckIds.Contains(id, StringComparer.Ordinal))
            || (payload.TerminalClassification == EnvironmentTerminalClassification.Ready
                && !OfficeLiteActiveProjectDownloadContract.RequiredCheckIds.All(
                    id => checkIds.Contains(id, StringComparer.Ordinal))))
        {
            errors.Add("download checks are duplicated, unknown or incomplete for a Ready receipt");
        }

        if (!unsupportedGaps.SequenceEqual(
                OfficeLiteActiveProjectDownloadContract.RequiredUnsupportedGaps,
                StringComparer.Ordinal))
        {
            errors.Add("unsupported gaps do not preserve the exact boundary");
        }

        if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready)
        {
            if (!payload.ControllerToPcDownloadPerformed
                || !payload.EnvironmentReusable
                || payload.LifecycleReceipt is null
                || !payload.LifecycleReceipt.Payload.CleanShutdownVerified
                || payload.NegativeControlCommand.ExitCode != 42
                || payload.NegativeControlCommand.TimedOut
                || !payload.NegativeControlCommand.CleanupVerified
                || payload.LiveCommand.ExitCode != 0
                || payload.LiveCommand.TimedOut
                || !payload.LiveCommand.CleanupVerified
                || string.IsNullOrWhiteSpace(payload.ActiveProject)
                || payload.ProjectCount is null or < 1
                || !payload.DownloadedProject.Exists
                || payload.DownloadedProject.Bytes <= 0
                || !string.Equals(Path.GetExtension(payload.DownloadedProject.Path), ".wvs", StringComparison.OrdinalIgnoreCase)
                || !IsDescendant(Path.Combine(payload.EvidenceDirectory, "download"), payload.DownloadedProject.Path)
                || string.IsNullOrWhiteSpace(payload.LifecycleReceipt.Payload.GuestEndpoint.Address)
                || checks.Any(check => check.Status != EnvironmentCheckStatus.Passed))
            {
                errors.Add("Ready receipt lacks complete accepted download/negative-control/cleanup evidence");
            }
        }

        var fileIds = files.Select(file => file.Id).ToArray();
        if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready
            && (fileIds.Distinct(StringComparer.Ordinal).Count() != fileIds.Length
                || !RequiredFileIds.All(id => fileIds.Contains(id, StringComparer.Ordinal))))
        {
            errors.Add("Ready receipt lacks the exact material evidence set");
        }

        foreach (var file in files.Where(file => file.Exists))
        {
            if (!File.Exists(file.Path))
            {
                errors.Add($"evidence file is missing: {file.Id}");
                continue;
            }

            var info = new FileInfo(file.Path);
            using var stream = File.OpenRead(file.Path);
            var hash = Convert.ToHexString(SHA256.HashData(stream));
            if (info.Length != file.Bytes || !string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"evidence file drifted: {file.Id}");
            }
        }

        if (payload.DownloadedProject.Exists
            && !files.Any(file => file.Id == payload.DownloadedProject.Id
                && string.Equals(file.Sha256, payload.DownloadedProject.Sha256, StringComparison.OrdinalIgnoreCase)
                && file.Bytes == payload.DownloadedProject.Bytes))
        {
            errors.Add("downloaded project identity is not bound into the material evidence set");
        }

        return new OfficeLiteActiveProjectDownloadVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = canonicalHash,
            Errors = errors
        };
    }

    private static bool IsDescendant(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(rootFull, StringComparison.OrdinalIgnoreCase);
    }
}

public static class OfficeLiteActiveProjectDownloadReceiptWriter
{
    public static string WriteNew(string outputPath, OfficeLiteActiveProjectDownloadReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!OfficeLiteActiveProjectDownloadReceiptVerifier.Verify(receipt).Succeeded)
        {
            throw new InvalidOperationException("OfficeLite active-project download receipt integrity is invalid.");
        }

        var fullPath = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Receipt output must use the .json extension.", nameof(outputPath));
        }

        if (!receipt.Payload.SideEffects.Contains($"CreateNewReceiptFile:{fullPath}", StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Receipt output does not match the recorded create-new side effect.");
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
