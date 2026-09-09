using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class WorkVisualRunnerSmokeContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.workvisual-runner-smoke-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const string EmbeddedScriptResource = "KukaLab.Probes.WorkVisual.ReadOnlySmoke.csx";
    public const string EmbeddedScriptSha256 = "B0DBEC5D9E58E761D673202FDB24FF89C1355B384FA0AE9FC90CA8CA3D63E450";
    public const string SuccessMarker = "Application=WorkVisual";

    internal static readonly IReadOnlyList<string> RequiredUnsupportedGaps =
    [
        "This smoke does not inspect an OfficeLite controller project.",
        "This smoke does not upload, activate, deploy or save a WorkVisual project.",
        "Native KSS compilation is not invoked by this smoke."
    ];
}

public sealed record WorkVisualRunnerSmokeRequest
{
    public required string RunnerPath { get; init; }

    public required string EvidenceDirectory { get; init; }

    public int TimeoutSeconds { get; init; } = 30;

    public static WorkVisualRunnerSmokeRequest CreateDefault(
        string evidenceDirectory,
        string? runnerPath = null,
        int timeoutSeconds = 30)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceDirectory);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return new WorkVisualRunnerSmokeRequest
        {
            RunnerPath = Path.GetFullPath(runnerPath
                ?? Path.Combine(programFilesX86, "KUKA", "WorkVisual 6.0", "wvsr.exe")),
            EvidenceDirectory = Path.GetFullPath(evidenceDirectory),
            TimeoutSeconds = timeoutSeconds
        };
    }
}

public sealed record WorkVisualRunnerSmokeReceipt
{
    public string SchemaIdentity { get; init; } = WorkVisualRunnerSmokeContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = WorkVisualRunnerSmokeContract.ReceiptSchemaVersion;

    public required WorkVisualRunnerSmokePayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record WorkVisualRunnerSmokePayload
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

    public bool SuccessMarkerObserved { get; init; }

    public bool ProjectInspectionPerformed { get; init; }

    public NativeKssStatus NativeKssStatus { get; init; } = NativeKssStatus.NotRun;

    public WorkVisualRunnerCommandObservation Command { get; init; } = new();

    public List<EnvironmentCheck> Checks { get; init; } = [];

    public List<EnvironmentFileObservation> Files { get; init; } = [];

    public List<string> SideEffects { get; init; } = [];

    public bool EnvironmentReusable { get; init; }

    public List<string> UnsupportedGaps { get; init; } = [];
}

public sealed record WorkVisualRunnerCommandObservation
{
    public int ExitCode { get; init; }

    public bool TimedOut { get; init; }

    public bool CleanupVerified { get; init; }

    public long DurationMilliseconds { get; init; }
}

public sealed record WorkVisualRunnerSmokeOutcome(WorkVisualRunnerSmokeReceipt Receipt)
{
    public int ExitCode => Receipt.Payload.TerminalClassification switch
    {
        EnvironmentTerminalClassification.Ready => 0,
        EnvironmentTerminalClassification.Failed => 2,
        _ => 3
    };
}

public sealed class WorkVisualRunnerSmokeRunner
{
    private readonly IWorkVisualRunnerPlatform _platform;
    private readonly TimeProvider _timeProvider;

    public WorkVisualRunnerSmokeRunner()
        : this(new WorkVisualRunnerPlatform(), TimeProvider.System)
    {
    }

    internal WorkVisualRunnerSmokeRunner(IWorkVisualRunnerPlatform platform, TimeProvider timeProvider)
    {
        _platform = platform;
        _timeProvider = timeProvider;
    }

    public WorkVisualRunnerSmokeOutcome Run(WorkVisualRunnerSmokeRequest request, string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAttemptId(attemptId);
        if (request.TimeoutSeconds is < 1 or > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "WorkVisual smoke timeout must be from 1 through 120 seconds.");
        }

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var checks = new List<EnvironmentCheck>();
        var files = new List<EnvironmentFileObservation>();
        var sideEffects = new List<string>();
        var unsupportedGaps = WorkVisualRunnerSmokeContract.RequiredUnsupportedGaps.ToList();
        var runnerPath = Path.GetFullPath(request.RunnerPath);
        var evidenceDirectory = Path.GetFullPath(request.EvidenceDirectory);
        var scriptSha256 = string.Empty;
        var successMarkerObserved = false;
        var command = new WorkVisualRunnerCommandObservation { CleanupVerified = true };
        var environmentReusable = true;

        if (!OperatingSystem.IsWindows())
        {
            checks.Add(Blocked("host-os", "WorkVisual Script Runner requires Windows."));
            return Complete();
        }

        if (!ObserveFile("workvisual-script-runner", runnerPath, includeVersion: true, files, checks))
        {
            return Complete();
        }

        try
        {
            if (Directory.Exists(evidenceDirectory))
            {
                if ((File.GetAttributes(evidenceDirectory) & FileAttributes.ReparsePoint) != 0)
                {
                    checks.Add(Failed("evidence-directory", "Evidence directory cannot be a reparse point."));
                    return Complete();
                }
            }
            else
            {
                Directory.CreateDirectory(evidenceDirectory);
                sideEffects.Add($"CreateEvidenceDirectory:{evidenceDirectory}");
            }

            checks.Add(Passed("evidence-directory", "Evidence directory is local and not a reparse point."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            checks.Add(Failed("evidence-directory", $"Evidence directory could not be prepared: {exception.Message}"));
            return Complete();
        }

        var scriptPath = Path.Combine(evidenceDirectory, $"{attemptId}.read-only-smoke.csx");
        var stdoutPath = Path.Combine(evidenceDirectory, $"{attemptId}.stdout.log");
        var stderrPath = Path.Combine(evidenceDirectory, $"{attemptId}.stderr.log");
        try
        {
            var scriptBytes = ReadEmbeddedScript();
            scriptSha256 = Convert.ToHexString(SHA256.HashData(scriptBytes));
            if (!string.Equals(
                    scriptSha256,
                    WorkVisualRunnerSmokeContract.EmbeddedScriptSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                checks.Add(Failed("safe-smoke-script", "Embedded WorkVisual smoke script hash is not the pinned safe value."));
                return Complete();
            }

            WriteNew(scriptPath, scriptBytes);
            sideEffects.Add($"CreateProbeScript:{scriptPath}");
            files.Add(ObserveExistingFile("workvisual-safe-smoke-script", scriptPath, includeVersion: false));
            checks.Add(Passed("safe-smoke-script", "Pinned read-only WorkVisual smoke script was materialized as create-new evidence."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            checks.Add(Failed("safe-smoke-script", $"Safe smoke script could not be materialized: {exception.Message}"));
            return Complete();
        }

        WorkVisualProcessResult result;
        try
        {
            result = _platform.Run(
                runnerPath,
                ["-executescript", $"-scriptpath={scriptPath}", "-scriptmode=Release"],
                evidenceDirectory,
                TimeSpan.FromSeconds(request.TimeoutSeconds));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or Win32Exception)
        {
            result = new WorkVisualProcessResult(
                -1,
                false,
                true,
                0,
                string.Empty,
                $"{exception.GetType().Name}: {exception.Message}");
        }

        command = new WorkVisualRunnerCommandObservation
        {
            ExitCode = result.ExitCode,
            TimedOut = result.TimedOut,
            CleanupVerified = result.CleanupVerified,
            DurationMilliseconds = Math.Max(0, result.DurationMilliseconds)
        };
        environmentReusable = result.CleanupVerified;

        try
        {
            WriteNew(stdoutPath, Encoding.UTF8.GetBytes(result.StandardOutput));
            WriteNew(stderrPath, Encoding.UTF8.GetBytes(result.StandardError));
            sideEffects.Add($"CreateRawStdout:{stdoutPath}");
            sideEffects.Add($"CreateRawStderr:{stderrPath}");
            files.Add(ObserveExistingFile("workvisual-smoke-stdout", stdoutPath, includeVersion: false));
            files.Add(ObserveExistingFile("workvisual-smoke-stderr", stderrPath, includeVersion: false));
            checks.Add(Passed("raw-evidence", "Runner stdout and stderr were preserved as create-new hash-bound evidence."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            checks.Add(Failed("raw-evidence", $"Runner output could not be preserved: {exception.Message}"));
        }

        successMarkerObserved = result.StandardOutput.Contains(
            WorkVisualRunnerSmokeContract.SuccessMarker,
            StringComparison.Ordinal);
        if (!result.CleanupVerified)
        {
            checks.Add(Failed("runner-cleanup", "WorkVisual Script Runner termination could not be verified."));
        }
        else
        {
            checks.Add(Passed("runner-cleanup", "WorkVisual Script Runner process exit was verified."));
        }

        if (result.TimedOut)
        {
            checks.Add(Blocked("runner-execution", "WorkVisual Script Runner exceeded the bounded timeout and was stopped."));
        }
        else if (result.ExitCode != 0)
        {
            checks.Add(Failed("runner-execution", $"WorkVisual Script Runner returned exit code {result.ExitCode}."));
        }
        else if (!successMarkerObserved)
        {
            checks.Add(Failed("runner-execution", "WorkVisual Script Runner exited successfully without the expected application marker."));
        }
        else
        {
            checks.Add(Passed("runner-execution", "WorkVisual Script Runner executed the pinned read-only script and returned the expected marker."));
        }

        return Complete();

        WorkVisualRunnerSmokeOutcome Complete()
        {
            stopwatch.Stop();
            var completedAt = _timeProvider.GetUtcNow();
            var terminal = checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
                ? EnvironmentTerminalClassification.Failed
                : checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked)
                    ? EnvironmentTerminalClassification.Blocked
                    : EnvironmentTerminalClassification.Ready;
            var payload = new WorkVisualRunnerSmokePayload
            {
                ReceiptId = $"workvisual-runner-smoke-{attemptId}",
                AttemptId = attemptId,
                CoreAssemblySha256 = ComputeFileSha256(typeof(WorkVisualRunnerSmokeRunner).Assembly.Location),
                Runtime = new RuntimeEnvironment
                {
                    OsDescription = RuntimeInformation.OSDescription,
                    FrameworkDescription = RuntimeInformation.FrameworkDescription,
                    ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
                },
                StartedAtUtc = startedAt,
                CompletedAtUtc = completedAt,
                DurationMilliseconds = Math.Max(0, stopwatch.ElapsedMilliseconds),
                TerminalClassification = terminal,
                RunnerPath = runnerPath,
                EvidenceDirectory = evidenceDirectory,
                EmbeddedScriptSha256 = scriptSha256,
                SuccessMarkerObserved = successMarkerObserved,
                ProjectInspectionPerformed = false,
                NativeKssStatus = NativeKssStatus.NotRun,
                Command = command,
                Checks = checks,
                Files = files,
                SideEffects = sideEffects,
                EnvironmentReusable = environmentReusable,
                UnsupportedGaps = unsupportedGaps
            };
            var receipt = new WorkVisualRunnerSmokeReceipt
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
            };
            return new WorkVisualRunnerSmokeOutcome(receipt);
        }
    }

    private static byte[] ReadEmbeddedScript()
    {
        using var stream = typeof(WorkVisualRunnerSmokeRunner).Assembly.GetManifestResourceStream(
            WorkVisualRunnerSmokeContract.EmbeddedScriptResource)
            ?? throw new InvalidOperationException("Pinned WorkVisual smoke resource is missing.");
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true);
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

        try
        {
            files.Add(ObserveExistingFile(id, path, includeVersion));
            checks.Add(Passed(id, "Required file exists and was hashed."));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            checks.Add(Failed(id, $"Required file could not be inventoried: {exception.Message}"));
            return false;
        }
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
        if (!Regex.IsMatch(
                attemptId,
                "^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$",
                RegexOptions.CultureInvariant))
        {
            throw new ArgumentException(
                "Attempt ID must match [A-Za-z0-9][A-Za-z0-9._-]{2,127}.",
                nameof(attemptId));
        }
    }

    private static EnvironmentCheck Passed(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Passed, Detail = detail };

    private static EnvironmentCheck Blocked(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Blocked, Detail = detail };

    private static EnvironmentCheck Failed(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Failed, Detail = detail };
}

public sealed record WorkVisualRunnerSmokeReceiptVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}

public static class WorkVisualRunnerSmokeReceiptVerifier
{
    public static WorkVisualRunnerSmokeReceiptVerificationResult Verify(WorkVisualRunnerSmokeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (!string.Equals(
                receipt.SchemaIdentity,
                WorkVisualRunnerSmokeContract.ReceiptSchemaIdentity,
                StringComparison.Ordinal))
        {
            errors.Add($"schemaIdentity must be {WorkVisualRunnerSmokeContract.ReceiptSchemaIdentity}");
        }

        if (receipt.SchemaVersion != WorkVisualRunnerSmokeContract.ReceiptSchemaVersion)
        {
            errors.Add($"schemaVersion must be {WorkVisualRunnerSmokeContract.ReceiptSchemaVersion}");
        }

        if (ReferenceEquals(receipt.Payload, null))
        {
            return new WorkVisualRunnerSmokeReceiptVerificationResult
            {
                Succeeded = false,
                PayloadSha256 = receipt.PayloadSha256,
                Errors = ["payload is required"]
            };
        }

        var payload = receipt.Payload;
        if (string.IsNullOrWhiteSpace(payload.ReceiptId)
            || string.IsNullOrWhiteSpace(payload.AttemptId))
        {
            errors.Add("receiptId and attemptId are required");
        }

        if (!IsSha256(payload.CoreAssemblySha256)
            || (!string.IsNullOrEmpty(payload.EmbeddedScriptSha256)
                && !IsSha256(payload.EmbeddedScriptSha256)))
        {
            errors.Add("payload hash identities are invalid");
        }

        if (payload.Runtime is null
            || string.IsNullOrWhiteSpace(payload.Runtime.OsDescription)
            || string.IsNullOrWhiteSpace(payload.Runtime.FrameworkDescription)
            || string.IsNullOrWhiteSpace(payload.Runtime.ProcessArchitecture))
        {
            errors.Add("payload.runtime identity is incomplete");
        }

        if (payload.Command is null)
        {
            errors.Add("payload.command is required");
        }
        else if (payload.StartedAtUtc > payload.CompletedAtUtc
            || payload.DurationMilliseconds < 0
            || payload.Command.DurationMilliseconds < 0)
        {
            errors.Add("payload timing is invalid");
        }

        if (payload.Checks is null || payload.Checks.Count == 0
            || payload.Checks.Any(check => check is null || string.IsNullOrWhiteSpace(check.Id))
            || payload.Checks.GroupBy(check => check.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            errors.Add("payload.checks must contain evidence with unique non-empty IDs");
        }

        if (payload.Files is null
            || payload.SideEffects is null
            || payload.UnsupportedGaps is null)
        {
            errors.Add("payload evidence collections cannot be null");
        }
        else if (payload.Files.Any(file => file is null
                || string.IsNullOrWhiteSpace(file.Id)
                || string.IsNullOrWhiteSpace(file.Path)
                || !Path.IsPathFullyQualified(file.Path))
            || payload.Files.GroupBy(
                    file => file.Id,
                    StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1))
        {
            errors.Add("payload.files must have unique non-empty IDs and absolute paths");
        }
        else
        {
            VerifyObservedFiles(payload.Files, errors);
        }

        if (payload.NativeKssStatus != NativeKssStatus.NotRun || payload.ProjectInspectionPerformed)
        {
            errors.Add("runner smoke cannot claim controller project or native KSS evidence");
        }

        if (payload.UnsupportedGaps is null
            || WorkVisualRunnerSmokeContract.RequiredUnsupportedGaps.Any(
                required => !payload.UnsupportedGaps.Contains(required, StringComparer.Ordinal)))
        {
            errors.Add("runner smoke must preserve its controller, deployment and native KSS limitations");
        }

        if (payload.Checks is not null && payload.Checks.All(check => check is not null))
        {
            var expectedTerminal = payload.Checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
                ? EnvironmentTerminalClassification.Failed
                : payload.Checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked)
                    ? EnvironmentTerminalClassification.Blocked
                    : EnvironmentTerminalClassification.Ready;
            if (payload.TerminalClassification != expectedTerminal)
            {
                errors.Add("payload.terminalClassification does not agree with check statuses");
            }
        }

        if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready
            && (payload.Command is null
                || payload.Command.ExitCode != 0
                || payload.Command.TimedOut
                || !payload.Command.CleanupVerified
                || !payload.SuccessMarkerObserved
                || !payload.EnvironmentReusable
                || !string.Equals(
                    payload.EmbeddedScriptSha256,
                    WorkVisualRunnerSmokeContract.EmbeddedScriptSha256,
                    StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("ready runner smoke lacks pinned-script, process, marker or cleanup evidence");
        }


        if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready)
        {
            VerifyReadyEvidence(payload, errors);
        }

        if (payload.Command is not null
            && !payload.Command.CleanupVerified
            && payload.EnvironmentReusable)
        {
            errors.Add("environment cannot be reusable when process cleanup is unverified");
        }

        if (!string.Equals(
                receipt.PayloadSha256,
                ReceiptSerialization.ComputeCanonicalSha256(payload),
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("payloadSha256 does not match the canonical payload");
        }

        return new WorkVisualRunnerSmokeReceiptVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static void VerifyReadyEvidence(
        WorkVisualRunnerSmokePayload payload,
        List<string> errors)
    {
        var checks = payload.Checks?.Where(check => check is not null).ToList() ?? [];
        var files = payload.Files?.Where(file => file is not null).ToList() ?? [];
        var requiredCheckIds = new[]
        {
            "workvisual-script-runner",
            "evidence-directory",
            "safe-smoke-script",
            "raw-evidence",
            "runner-cleanup",
            "runner-execution"
        };
        foreach (var checkId in requiredCheckIds)
        {
            if (!checks.Any(check => check.Id == checkId
                    && check.Status == EnvironmentCheckStatus.Passed))
            {
                errors.Add($"ready runner smoke lacks passed check: {checkId}");
            }
        }

        var requiredFileIds = new[]
        {
            "workvisual-script-runner",
            "workvisual-safe-smoke-script",
            "workvisual-smoke-stdout",
            "workvisual-smoke-stderr"
        };
        foreach (var fileId in requiredFileIds)
        {
            if (!files.Any(file => file.Id == fileId && file.Exists))
            {
                errors.Add($"ready runner smoke lacks observed file: {fileId}");
            }
        }

        var runner = files.SingleOrDefault(file => file.Id == "workvisual-script-runner");
        if (runner is not null && !PathsEqual(runner.Path, payload.RunnerPath))
        {
            errors.Add("runner file evidence does not match payload.runnerPath");
        }

        var script = files.SingleOrDefault(file => file.Id == "workvisual-safe-smoke-script");
        if (script is not null
            && !string.Equals(
                script.Sha256,
                WorkVisualRunnerSmokeContract.EmbeddedScriptSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("script file evidence does not match the pinned safe script");
        }

        var requiredSideEffectPrefixes = new[]
        {
            "CreateProbeScript:",
            "CreateRawStdout:",
            "CreateRawStderr:"
        };
        foreach (var prefix in requiredSideEffectPrefixes)
        {
            if (payload.SideEffects is null
                || !payload.SideEffects.Any(effect => effect is not null
                    && effect.StartsWith(prefix, StringComparison.Ordinal)))
            {
                errors.Add($"ready runner smoke lacks declared side effect: {prefix}");
            }
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return Path.IsPathFullyQualified(left)
                && Path.IsPathFullyQualified(right)
                && string.Equals(
                    Path.GetFullPath(left),
                    Path.GetFullPath(right),
                    StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static void VerifyObservedFiles(
        IReadOnlyList<EnvironmentFileObservation> files,
        List<string> errors)
    {
        foreach (var file in files.Where(file => file.Exists))
        {
            if (file.Bytes is null || file.Bytes < 0 || file.Sha256 is null || !IsSha256(file.Sha256))
            {
                errors.Add($"file evidence is incomplete: {file.Id}");
                continue;
            }

            try
            {
                var info = new FileInfo(file.Path);
                if (!info.Exists)
                {
                    errors.Add($"observed file is missing: {file.Id}");
                    continue;
                }

                if (info.Length != file.Bytes)
                {
                    errors.Add($"observed file length changed: {file.Id}");
                    continue;
                }

                using var stream = File.OpenRead(file.Path);
                var currentSha256 = Convert.ToHexString(SHA256.HashData(stream));
                if (!string.Equals(currentSha256, file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"observed file hash changed: {file.Id}");
                }
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
            {
                errors.Add($"observed file could not be verified: {file.Id}: {exception.Message}");
            }
        }
    }
}

public static class WorkVisualRunnerSmokeReceiptWriter
{
    public static string WriteNew(string outputPath, WorkVisualRunnerSmokeReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!WorkVisualRunnerSmokeReceiptVerifier.Verify(receipt).Succeeded)
        {
            throw new InvalidOperationException("WorkVisual runner smoke receipt integrity is invalid.");
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
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(ReceiptSerialization.ToJson(receipt));
        writer.WriteLine();
        writer.Flush();
        stream.Flush(flushToDisk: true);
        return fullPath;
    }
}

internal interface IWorkVisualRunnerPlatform
{
    WorkVisualProcessResult Run(
        string executablePath,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout);
}

internal sealed record WorkVisualProcessResult(
    int ExitCode,
    bool TimedOut,
    bool CleanupVerified,
    long DurationMilliseconds,
    string StandardOutput,
    string StandardError);

internal sealed class WorkVisualRunnerPlatform : IWorkVisualRunnerPlatform
{
    public WorkVisualProcessResult Run(
        string executablePath,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var timedOut = !process.WaitForExit(checked((int)timeout.TotalMilliseconds));
        var cleanupVerified = true;
        if (timedOut)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                cleanupVerified = process.WaitForExit(10000);
            }
            catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
            {
                cleanupVerified = process.HasExited;
            }
        }
        else
        {
            process.WaitForExit();
        }

        if (cleanupVerified)
        {
            Task.WaitAll([stdoutTask, stderrTask], TimeSpan.FromSeconds(10));
        }

        stopwatch.Stop();
        return new WorkVisualProcessResult(
            timedOut ? -1 : process.ExitCode,
            timedOut,
            cleanupVerified,
            stopwatch.ElapsedMilliseconds,
            stdoutTask.IsCompletedSuccessfully ? stdoutTask.Result : string.Empty,
            stderrTask.IsCompletedSuccessfully ? stderrTask.Result : string.Empty);
    }
}
