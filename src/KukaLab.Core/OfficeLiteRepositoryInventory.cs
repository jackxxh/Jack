using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class OfficeLiteRepositoryInventoryContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.officelite-repository-inventory-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const string EmbeddedScriptResource = "KukaLab.Probes.WorkVisual.RepositoryInventory.csx";
    public const string EmbeddedScriptSha256 = "03C1B5B42A513A0998076AA141B90147791245AD6943AEDE4DA10DC9638664EC";
    public const string DefaultRepositoryPath = @"KRC:\R1\Program";
    public const string FailureMarker = "REPOSITORY_INVENTORY_FAILED:";
    public const string MissingPathMarker = "REPOSITORY_INVENTORY_PATH_MISSING:";

    internal static readonly IReadOnlyList<string> RequiredUnsupportedGaps =
    [
        "The repository inventory lists immediate child paths only; it does not download or read controller file contents.",
        "The inventory does not create, upload, move, delete, select, start or reset a controller program.",
        "Native KSS compilation remains NotRun, and KUKA.Sim robot-profile matching remains a separate gate."
    ];
}

public sealed record OfficeLiteRepositoryInventoryRequest
{
    public required OfficeLiteCycleRequest OfficeLite { get; init; }

    public required string RunnerPath { get; init; }

    public required string EvidenceDirectory { get; init; }

    public string RepositoryPath { get; init; } = OfficeLiteRepositoryInventoryContract.DefaultRepositoryPath;

    public int RunnerTimeoutSeconds { get; init; } = 30;

    public static OfficeLiteRepositoryInventoryRequest CreateDefault(
        string assetRoot,
        string evidenceDirectory,
        string? vmrunPath = null,
        string? runnerPath = null,
        string? guestIpAddress = null,
        string? repositoryPath = null,
        int readinessTimeoutSeconds = 240,
        int serviceObservationSeconds = 180,
        int runnerTimeoutSeconds = 30)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceDirectory);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return new OfficeLiteRepositoryInventoryRequest
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
            RepositoryPath = string.IsNullOrWhiteSpace(repositoryPath)
                ? OfficeLiteRepositoryInventoryContract.DefaultRepositoryPath
                : repositoryPath,
            RunnerTimeoutSeconds = runnerTimeoutSeconds
        };
    }
}

public sealed record OfficeLiteRepositoryInventoryReceipt
{
    public string SchemaIdentity { get; init; } = OfficeLiteRepositoryInventoryContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = OfficeLiteRepositoryInventoryContract.ReceiptSchemaVersion;

    public required OfficeLiteRepositoryInventoryPayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record OfficeLiteRepositoryInventoryPayload
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

    public ControllerRepositoryObservation Repository { get; init; } = new();

    public bool ReadOnlyOperation { get; init; } = true;

    public bool CredentialsUsed { get; init; }

    public bool FileContentsRead { get; init; }

    public bool RepositoryMutationPerformed { get; init; }

    public NativeKssStatus NativeKssStatus { get; init; } = NativeKssStatus.NotRun;

    public List<EnvironmentCheck> Checks { get; init; } = [];

    public List<EnvironmentFileObservation> Files { get; init; } = [];

    public List<string> SideEffects { get; init; } = [];

    public bool EnvironmentReusable { get; init; }

    public List<string> UnsupportedGaps { get; init; } = [];
}

public sealed record ControllerRepositoryObservation
{
    public bool Attempted { get; init; }

    public string? ControllerAddress { get; init; }

    public string? RepositoryPath { get; init; }

    public bool? DirectoryExists { get; init; }

    public int? DirectoryCount { get; init; }

    public int? FileCount { get; init; }

    public List<string> Directories { get; init; } = [];

    public List<string> Files { get; init; } = [];
}

public sealed record OfficeLiteRepositoryInventoryOutcome(OfficeLiteRepositoryInventoryReceipt Receipt)
{
    public int ExitCode => Receipt.Payload.TerminalClassification switch
    {
        EnvironmentTerminalClassification.Ready => 0,
        EnvironmentTerminalClassification.Failed => 2,
        _ => 3
    };
}

public sealed class OfficeLiteRepositoryInventoryRunner
{
    private readonly OfficeLiteCycleRunner _officeLiteRunner;
    private readonly IWorkVisualRunnerPlatform _workVisualPlatform;
    private readonly TimeProvider _timeProvider;

    public OfficeLiteRepositoryInventoryRunner()
        : this(new OfficeLiteCycleRunner(), new WorkVisualRunnerPlatform(), TimeProvider.System)
    {
    }

    internal OfficeLiteRepositoryInventoryRunner(
        OfficeLiteCycleRunner officeLiteRunner,
        IWorkVisualRunnerPlatform workVisualPlatform,
        TimeProvider timeProvider)
    {
        _officeLiteRunner = officeLiteRunner;
        _workVisualPlatform = workVisualPlatform;
        _timeProvider = timeProvider;
    }

    public OfficeLiteRepositoryInventoryOutcome Run(
        OfficeLiteRepositoryInventoryRequest request,
        string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAttemptId(attemptId);
        ValidateRepositoryPath(request.RepositoryPath);
        if (request.RunnerTimeoutSeconds is < 1 or > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "WorkVisual runner timeout must be from 1 through 120 seconds.");
        }

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var checks = new List<EnvironmentCheck>();
        var files = new List<EnvironmentFileObservation>();
        var sideEffects = new List<string>();
        var unsupportedGaps = OfficeLiteRepositoryInventoryContract.RequiredUnsupportedGaps.ToList();
        var runnerPath = Path.GetFullPath(request.RunnerPath);
        var evidenceDirectory = Path.GetFullPath(request.EvidenceDirectory);
        var scriptHash = string.Empty;
        var negative = new WorkVisualRunnerCommandObservation { CleanupVerified = true };
        var live = new WorkVisualRunnerCommandObservation { CleanupVerified = true };
        var repository = new ControllerRepositoryObservation();
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
            scriptPath = Path.Combine(evidenceDirectory, $"{attemptId}.repository-inventory.csx");
            var scriptBytes = ReadEmbeddedScript();
            scriptHash = Convert.ToHexString(SHA256.HashData(scriptBytes));
            if (!string.Equals(
                    scriptHash,
                    OfficeLiteRepositoryInventoryContract.EmbeddedScriptSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                checks.Add(Failed("read-only-script", "Embedded repository inventory script hash is not the pinned read-only value."));
                return Complete();
            }

            WriteNew(scriptPath, scriptBytes);
            sideEffects.Add($"CreateProbeScript:{scriptPath}");
            files.Add(ObserveExistingFile("workvisual-repository-inventory-script", scriptPath, false));
            checks.Add(Passed("read-only-script", "Pinned read-only repository script was materialized as create-new evidence."));
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
            context => ExecuteReadOnlyInventory(context.GuestAddress, scriptPath));
        lifecycleReceipt = lifecycle.Receipt;

        if (lifecycleReceipt.Payload.TerminalClassification == EnvironmentTerminalClassification.Ready
            && HasCompleteInventory(repository)
            && checks.All(check => check.Status is not EnvironmentCheckStatus.Failed and not EnvironmentCheckStatus.Blocked))
        {
            checks.Add(Passed("repository-inventory", "The controller returned a complete immediate-child repository inventory through WorkVisual."));
        }
        else if (!repository.Attempted)
        {
            checks.Add(Blocked("repository-inventory", "The repository query did not run because OfficeLite DeviceInfo readiness was not established."));
        }

        checks.Add(lifecycleReceipt.Payload.CleanShutdownVerified
            ? Passed("environment-cleanup", "OfficeLite soft shutdown and WorkVisual runner cleanup were verified.")
            : Failed("environment-cleanup", "OfficeLite soft shutdown was not verified."));
        return Complete();

        void ExecuteReadOnlyInventory(string address, string materializedScriptPath)
        {
            var negativeResult = RunWorkVisual("127.0.0.1", materializedScriptPath);
            negative = ToObservation(negativeResult);
            runnerCleanupVerified &= negativeResult.CleanupVerified;
            PreserveRawEvidence("negative", negativeResult);

            if (negativeResult.TimedOut
                || negativeResult.ExitCode != 42
                || !negativeResult.StandardOutput.Contains(
                    OfficeLiteRepositoryInventoryContract.FailureMarker,
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
            repository = ParseInventory(address, request.RepositoryPath, liveResult.StandardOutput);

            if (!liveResult.CleanupVerified)
            {
                checks.Add(Failed("workvisual-runner-cleanup", "WorkVisual Script Runner cleanup could not be verified."));
            }
            else if (liveResult.TimedOut)
            {
                checks.Add(Blocked("live-query", "The live read-only repository query exceeded its bounded timeout."));
            }
            else if (liveResult.ExitCode == 42
                && liveResult.StandardOutput.Contains(OfficeLiteRepositoryInventoryContract.FailureMarker, StringComparison.Ordinal))
            {
                checks.Add(Blocked("live-query", "The WorkVisual client reached its typed online-controller failure path."));
            }
            else if (liveResult.ExitCode == 43
                && liveResult.StandardOutput.Contains(OfficeLiteRepositoryInventoryContract.MissingPathMarker, StringComparison.Ordinal))
            {
                checks.Add(Blocked("live-query", "The controller was reached but the requested repository path is absent."));
            }
            else if (liveResult.ExitCode != 0)
            {
                checks.Add(Failed("live-query", $"The live repository query returned unexpected exit code {liveResult.ExitCode}."));
            }
            else if (!HasCompleteInventory(repository))
            {
                checks.Add(Failed("live-query", "The live query exited successfully but did not emit the complete repository inventory contract."));
            }
            else
            {
                checks.Add(Passed("live-query", "The live read-only repository query returned the complete path inventory contract."));
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
                        $"-repositorypath={request.RepositoryPath}"
                    ],
                    evidenceDirectory,
                    TimeSpan.FromSeconds(request.RunnerTimeoutSeconds));
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or Win32Exception)
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
            sideEffects.Add($"CreateRawStdout:{stdoutPath}");
            sideEffects.Add($"CreateRawStderr:{stderrPath}");
            files.Add(ObserveExistingFile($"workvisual-{prefix}-stdout", stdoutPath, false));
            files.Add(ObserveExistingFile($"workvisual-{prefix}-stderr", stderrPath, false));
        }

        OfficeLiteRepositoryInventoryOutcome Complete()
        {
            stopwatch.Stop();
            lifecycleReceipt ??= CreateUnavailableLifecycleReceipt(request.OfficeLite, attemptId, startedAt);
            var terminal = checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
                ? EnvironmentTerminalClassification.Failed
                : checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked)
                    || lifecycleReceipt.Payload.TerminalClassification != EnvironmentTerminalClassification.Ready
                    ? EnvironmentTerminalClassification.Blocked
                    : EnvironmentTerminalClassification.Ready;
            var payload = new OfficeLiteRepositoryInventoryPayload
            {
                ReceiptId = $"officelite-repository-inventory-{attemptId}",
                AttemptId = attemptId,
                CoreAssemblySha256 = ComputeFileSha256(typeof(OfficeLiteRepositoryInventoryRunner).Assembly.Location),
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
                Repository = repository,
                ReadOnlyOperation = true,
                CredentialsUsed = false,
                FileContentsRead = false,
                RepositoryMutationPerformed = false,
                NativeKssStatus = NativeKssStatus.NotRun,
                Checks = checks,
                Files = files,
                SideEffects = sideEffects,
                EnvironmentReusable = runnerCleanupVerified && lifecycleReceipt.Payload.CleanShutdownVerified,
                UnsupportedGaps = unsupportedGaps
            };
            var receipt = new OfficeLiteRepositoryInventoryReceipt
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
            };
            return new OfficeLiteRepositoryInventoryOutcome(receipt);
        }
    }

    private static ControllerRepositoryObservation ParseInventory(string address, string repositoryPath, string output)
    {
        var directories = ReadBase64Markers(output, "DirectoryBase64");
        var files = ReadBase64Markers(output, "FileBase64");
        return new ControllerRepositoryObservation
        {
            Attempted = true,
            ControllerAddress = address,
            RepositoryPath = repositoryPath,
            DirectoryExists = bool.TryParse(ReadMarker(output, "DirectoryExists"), out var exists) ? exists : null,
            DirectoryCount = int.TryParse(ReadMarker(output, "DirectoryCount"), out var directoryCount) ? directoryCount : null,
            FileCount = int.TryParse(ReadMarker(output, "FileCount"), out var fileCount) ? fileCount : null,
            Directories = directories,
            Files = files
        };
    }

    private static List<string> ReadBase64Markers(string output, string name)
    {
        var values = new List<string>();
        foreach (Match match in Regex.Matches(
                     output,
                     $"(?m)^{Regex.Escape(name)}=(?<value>[A-Za-z0-9+/=]+)\\r?$",
                     RegexOptions.CultureInvariant))
        {
            try
            {
                values.Add(Encoding.UTF8.GetString(Convert.FromBase64String(match.Groups["value"].Value)));
            }
            catch (FormatException)
            {
                return [];
            }
        }

        return values;
    }

    private static string? ReadMarker(string output, string name)
    {
        var match = Regex.Match(
            output,
            $"(?m)^{Regex.Escape(name)}=(?<value>.*)\\r?$",
            RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static bool HasCompleteInventory(ControllerRepositoryObservation observation) =>
        observation.Attempted
        && observation.DirectoryExists == true
        && observation.DirectoryCount is >= 0
        && observation.FileCount is >= 0
        && observation.Directories.Count == observation.DirectoryCount
        && observation.Files.Count == observation.FileCount;

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
        using var stream = typeof(OfficeLiteRepositoryInventoryRunner).Assembly.GetManifestResourceStream(
            OfficeLiteRepositoryInventoryContract.EmbeddedScriptResource)
            ?? throw new InvalidOperationException("Pinned WorkVisual repository inventory resource is missing.");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        var canonicalText = reader.ReadToEnd()
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return Encoding.UTF8.GetBytes(canonicalText);
    }

    private static void ValidateAttemptId(string attemptId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptId);
        if (!Regex.IsMatch(attemptId, "^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("Attempt id must use only letters, digits, dot, underscore or hyphen.", nameof(attemptId));
        }
    }

    private static void ValidateRepositoryPath(string repositoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        if (!repositoryPath.StartsWith(@"KRC:\", StringComparison.OrdinalIgnoreCase)
            || repositoryPath.Contains("..", StringComparison.Ordinal)
            || repositoryPath.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            throw new ArgumentException("Repository path must be an absolute KRC path without traversal or control characters.", nameof(repositoryPath));
        }
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

    private static EnvironmentCheck Passed(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Passed, Detail = detail };

    private static EnvironmentCheck Blocked(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Blocked, Detail = detail };

    private static EnvironmentCheck Failed(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Failed, Detail = detail };

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}

public sealed record OfficeLiteRepositoryInventoryVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}

public static class OfficeLiteRepositoryInventoryReceiptVerifier
{
    private static readonly Regex Sha256Pattern = new("^[0-9A-F]{64}$", RegexOptions.CultureInvariant);

    private static readonly string[] RequiredFileIds =
    [
        "workvisual-script-runner",
        "workvisual-repository-inventory-script",
        "workvisual-negative-stdout",
        "workvisual-negative-stderr",
        "workvisual-live-stdout",
        "workvisual-live-stderr"
    ];

    public static OfficeLiteRepositoryInventoryVerificationResult Verify(OfficeLiteRepositoryInventoryReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        var payload = receipt.Payload;
        if (!string.Equals(receipt.SchemaIdentity, OfficeLiteRepositoryInventoryContract.ReceiptSchemaIdentity, StringComparison.Ordinal)
            || receipt.SchemaVersion != OfficeLiteRepositoryInventoryContract.ReceiptSchemaVersion)
        {
            errors.Add("receipt schema identity or version is invalid");
        }

        var canonicalHash = ReceiptSerialization.ComputeCanonicalSha256(payload);
        if (!string.Equals(receipt.PayloadSha256, canonicalHash, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("payloadSha256 mismatch");
        }

        if (!Sha256Pattern.IsMatch(payload.CoreAssemblySha256)
            || !string.Equals(payload.EmbeddedScriptSha256, OfficeLiteRepositoryInventoryContract.EmbeddedScriptSha256, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("core or embedded script hash is invalid");
        }

        if (!payload.ReadOnlyOperation
            || payload.CredentialsUsed
            || payload.FileContentsRead
            || payload.RepositoryMutationPerformed
            || payload.NativeKssStatus != NativeKssStatus.NotRun)
        {
            errors.Add("read-only safety claims are invalid");
        }

        if (!OfficeLiteCycleReceiptVerifier.Verify(payload.LifecycleReceipt).Succeeded)
        {
            errors.Add("nested OfficeLite lifecycle receipt is invalid");
        }

        if (payload.CompletedAtUtc < payload.StartedAtUtc || payload.DurationMilliseconds < 0)
        {
            errors.Add("receipt timing is invalid");
        }

        if (payload.Repository.Attempted)
        {
            VerifyRepository(payload.Repository, errors);
        }

        if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready)
        {
            if (!payload.EnvironmentReusable
                || payload.LifecycleReceipt.Payload.TerminalClassification != EnvironmentTerminalClassification.Ready
                || !payload.LifecycleReceipt.Payload.WorkVisualServices.DeviceInfoEverOpen
                || !payload.LifecycleReceipt.Payload.CleanShutdownVerified)
            {
                errors.Add("ready receipt lacks a ready, reusable and clean OfficeLite lifecycle");
            }

            if (payload.NegativeControlCommand.ExitCode != 42
                || payload.NegativeControlCommand.TimedOut
                || payload.LiveCommand.ExitCode != 0
                || payload.LiveCommand.TimedOut
                || !HasCompleteInventory(payload.Repository))
            {
                errors.Add("ready receipt lacks successful differential repository evidence");
            }

            foreach (var id in RequiredFileIds)
            {
                if (!payload.Files.Any(file => string.Equals(file.Id, id, StringComparison.Ordinal) && file.Exists))
                {
                    errors.Add($"required observed file is missing: {id}");
                }
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

        if (!OfficeLiteRepositoryInventoryContract.RequiredUnsupportedGaps.SequenceEqual(payload.UnsupportedGaps, StringComparer.Ordinal))
        {
            errors.Add("unsupportedGaps do not match the contract");
        }

        return new OfficeLiteRepositoryInventoryVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }

    private static void VerifyRepository(ControllerRepositoryObservation repository, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(repository.ControllerAddress)
            || string.IsNullOrWhiteSpace(repository.RepositoryPath)
            || !repository.RepositoryPath.StartsWith(@"KRC:\", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("repository address or path is invalid");
            return;
        }

        if (repository.DirectoryExists == false)
        {
            if (repository.DirectoryCount is not null
                || repository.FileCount is not null
                || repository.Directories.Count != 0
                || repository.Files.Count != 0)
            {
                errors.Add("missing repository path contains child inventory evidence");
            }

            return;
        }

        if (repository.DirectoryExists != true
            || repository.DirectoryCount is < 0
            || repository.FileCount is < 0
            || repository.Directories.Count != repository.DirectoryCount
            || repository.Files.Count != repository.FileCount)
        {
            errors.Add("repository counts are inconsistent");
        }

        var prefix = repository.RepositoryPath.TrimEnd('\\') + "\\";
        var allPaths = repository.Directories.Concat(repository.Files).ToList();
        if (allPaths.Any(path => !path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || path[prefix.Length..].Contains('\\'))
            || allPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != allPaths.Count)
        {
            errors.Add("repository child paths are outside the requested immediate-child boundary");
        }
    }

    private static bool HasCompleteInventory(ControllerRepositoryObservation observation) =>
        observation.Attempted
        && observation.DirectoryExists == true
        && observation.DirectoryCount is >= 0
        && observation.FileCount is >= 0
        && observation.Directories.Count == observation.DirectoryCount
        && observation.Files.Count == observation.FileCount;

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}

public static class OfficeLiteRepositoryInventoryReceiptWriter
{
    public static string WriteNew(string outputPath, OfficeLiteRepositoryInventoryReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!OfficeLiteRepositoryInventoryReceiptVerifier.Verify(receipt).Succeeded)
        {
            throw new InvalidOperationException("OfficeLite repository inventory receipt integrity is invalid.");
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
