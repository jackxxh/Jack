using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public enum KukaSimCandidateExecutionDisposition
{
    InfrastructureFailed,
    RejectedByIntegratedInterpreter,
    Executed
}

public static class KukaSimCandidateExecutionClassifier
{
    public static KukaSimCandidateExecutionDisposition Classify(
        NativeKssCandidateSubmissionPlan submission,
        KukaSimIntegratedAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(attempt);
        var result = attempt.Result;
        if (result is null
            || !attempt.Command.ProcessStarted
            || attempt.Command.TimedOut
            || !attempt.Command.CleanupVerified
            || !string.IsNullOrEmpty(attempt.Command.PlatformError)
            || !result.ApplicationInitialized
            || !result.ApplicationReady
            || !result.ValidLicenseExists
            || !string.Equals(result.ComponentName, "KR 210 R2700-2 C01", StringComparison.Ordinal)
            || !result.IsComponent
            || result.LoadedObjectCount < 1
            || !string.Equals(result.TcpNodeName, "mountplate", StringComparison.Ordinal)
            || !string.Equals(result.MotionExecution, "Integrated", StringComparison.Ordinal)
            || !string.Equals(Path.GetFileNameWithoutExtension(result.ProgramName), submission.ProgramName, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(result.ProgramMode, "Go", StringComparison.Ordinal)
            || !result.TraceDataRecording
            || !string.Equals(result.SynchronizationAfter, "Synchronized", StringComparison.Ordinal)
            || !string.Equals(result.SynchronizationMode, "Automatic", StringComparison.Ordinal)
            || result.SimulationRunningAfter)
        {
            return KukaSimCandidateExecutionDisposition.InfrastructureFailed;
        }

        if (attempt.Command.ExitCode == 0
            && result.ProgramFinished
            && result.SuccessMarker
            && result.SawNonIdleInterpreterState
            && string.Equals(result.InterpreterModeAfter, "Go", StringComparison.Ordinal)
            && (string.Equals(result.InterpreterStateAfter, "End", StringComparison.Ordinal)
                || string.Equals(result.InterpreterStateAfter, "Active", StringComparison.Ordinal))
            && result.CompletedMotions.Count > 0
            && result.Samples.Count(sample => sample.Axes.Count == 6
                && double.IsFinite(sample.X) && double.IsFinite(sample.Y) && double.IsFinite(sample.Z)) >= 2)
        {
            return KukaSimCandidateExecutionDisposition.Executed;
        }

        if (attempt.Command.ExitCode is 0 or 4
            && !result.ProgramFinished
            && !result.SuccessMarker
            && result.CompletedMotions.Count == 0
            && result.Messages.Count > 0)
        {
            return KukaSimCandidateExecutionDisposition.RejectedByIntegratedInterpreter;
        }

        return KukaSimCandidateExecutionDisposition.InfrastructureFailed;
    }
}

public static class KukaSimCandidateExecutionContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.kukasim-candidate-execution-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const string EmbeddedScriptResource = KukaSimIntegratedValidationContract.EmbeddedScriptResource;
    public const string EmbeddedScriptSha256 = KukaSimIntegratedValidationContract.EmbeddedScriptSha256;
    public const string ExactComponentSha256 = KukaSimComponentSmokeContract.ExactKr210R2700Component410Sha256;
}

public sealed record KukaSimCandidateExecutionRequest
{
    public required string EnginePath { get; init; }
    public required string LauncherPath { get; init; }
    public required string BootstrapPluginPath { get; init; }
    public required string Create3DSharedPath { get; init; }
    public required string ComponentPath { get; init; }
    public string SimulationLayoutPath { get; init; } = string.Empty;
    public required string EvidenceDirectory { get; init; }
    public required NativeKssCandidateSubmissionRequest Submission { get; init; }
    public int TimeoutSeconds { get; init; } = 180;
    public bool GuiExecutionAuthorized { get; init; }
    public string AuthorizationReference { get; init; } = string.Empty;

    public static KukaSimCandidateExecutionRequest CreateDefault(
        string evidenceDirectory,
        NativeKssCandidateSubmissionRequest submission,
        bool guiExecutionAuthorized,
        string authorizationReference,
        string? enginePath = null,
        string? componentPath = null,
        int timeoutSeconds = 180,
        string? simulationLayoutPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceDirectory);
        ArgumentNullException.ThrowIfNull(submission);
        var kukaSim = KukaSimInstallationDiscovery.ResolveFromEngine(enginePath, componentPath);
        return new KukaSimCandidateExecutionRequest
        {
            EnginePath = kukaSim.EnginePath,
            LauncherPath = kukaSim.LauncherPath,
            BootstrapPluginPath = kukaSim.BootstrapPluginPath,
            Create3DSharedPath = kukaSim.Create3DSharedPath,
            ComponentPath = kukaSim.ComponentPath,
            SimulationLayoutPath = Path.GetFullPath(simulationLayoutPath ?? kukaSim.ComponentPath),
            EvidenceDirectory = Path.GetFullPath(evidenceDirectory),
            Submission = submission,
            TimeoutSeconds = timeoutSeconds,
            GuiExecutionAuthorized = guiExecutionAuthorized,
            AuthorizationReference = authorizationReference
        };
    }
}

public sealed record KukaSimCandidateExecutionReceipt
{
    public string SchemaIdentity { get; init; } = KukaSimCandidateExecutionContract.ReceiptSchemaIdentity;
    public int SchemaVersion { get; init; } = KukaSimCandidateExecutionContract.ReceiptSchemaVersion;
    public required KukaSimCandidateExecutionPayload Payload { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record KukaSimCandidateExecutionPayload
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
    public KukaSimCandidateExecutionDisposition Disposition { get; init; }
    public required NativeKssCandidateSubmissionPlan Submission { get; init; }
    public string Provider { get; init; } = "Integrated";
    public string EnginePath { get; init; } = string.Empty;
    public string ComponentPath { get; init; } = string.Empty;
    public string SimulationLayoutPath { get; init; } = string.Empty;
    public string EvidenceDirectory { get; init; } = string.Empty;
    public string EmbeddedScriptSha256 { get; init; } = string.Empty;
    public bool GuiExecutionAuthorized { get; init; }
    public string AuthorizationReference { get; init; } = string.Empty;
    public bool SimulationPerformed { get; init; }
    public bool OfficeLiteConnectionPerformed { get; init; }
    public bool RcsProviderUsed { get; init; }
    public bool PhysicalControllerContacted { get; init; }
    public NativeKssStatus NativeKssStatus { get; init; } = NativeKssStatus.NotRun;
    public List<KukaSimProcessObservation> PreExistingProcesses { get; init; } = [];
    public required KukaSimIntegratedAttempt Attempt { get; init; }
    public List<EnvironmentCheck> Checks { get; init; } = [];
    public List<EnvironmentFileObservation> Files { get; init; } = [];
    public List<string> SideEffects { get; init; } = [];
    public bool EnvironmentReusable { get; init; }
}

public sealed record KukaSimCandidateExecutionOutcome(KukaSimCandidateExecutionReceipt Receipt)
{
    public bool Succeeded => Receipt.Payload.TerminalClassification == EnvironmentTerminalClassification.Ready;
    public int ExitCode => Receipt.Payload.TerminalClassification switch
    {
        EnvironmentTerminalClassification.Ready => 0,
        EnvironmentTerminalClassification.Failed => 2,
        _ => 3
    };
}

public sealed class KukaSimCandidateExecutionRunner
{
    private readonly IKukaSimCandidateExecutionPlatform _platform;
    private readonly TimeProvider _timeProvider;
    private readonly Func<string, string> _fileSha256;

    public KukaSimCandidateExecutionRunner()
        : this(new KukaSimCandidateExecutionPlatform(), TimeProvider.System, ComputeFileSha256)
    {
    }

    internal KukaSimCandidateExecutionRunner(
        IKukaSimCandidateExecutionPlatform platform,
        TimeProvider timeProvider,
        Func<string, string> fileSha256)
    {
        _platform = platform;
        _timeProvider = timeProvider;
        _fileSha256 = fileSha256;
    }

    public KukaSimCandidateExecutionOutcome Run(KukaSimCandidateExecutionRequest request, string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateIdentifier(attemptId, nameof(attemptId));
        if (request.TimeoutSeconds is < 10 or > 600)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Timeout must be from 10 through 600 seconds.");
        }
        if (request.GuiExecutionAuthorized) ValidateIdentifier(request.AuthorizationReference, nameof(request.AuthorizationReference));

        var submission = new NativeKssCandidateSubmissionPlanner().Plan(request.Submission);
        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var checks = new List<EnvironmentCheck>();
        var files = new List<EnvironmentFileObservation>();
        var sideEffects = new List<string>();
        var preExisting = new List<KukaSimProcessObservation>();
        var attempt = EmptyAttempt(submission);
        var enginePath = Path.GetFullPath(request.EnginePath);
        var launcherPath = Path.GetFullPath(request.LauncherPath);
        var componentPath = Path.GetFullPath(request.ComponentPath);
        var simulationLayoutPath = Path.GetFullPath(string.IsNullOrWhiteSpace(request.SimulationLayoutPath)
            ? request.ComponentPath
            : request.SimulationLayoutPath);
        var evidenceDirectory = Path.GetFullPath(request.EvidenceDirectory);
        var installRoot = Path.GetDirectoryName(enginePath)
            ?? throw new ArgumentException("KUKA.Sim engine path has no parent directory.", nameof(request));
        var programmingCorePath = Path.Combine(installRoot, "KUKA", "Kuka.Sim.ProgrammingCore.dll");
        var compilerPath = KukaSimComponentSmokeRunner.ResolveFrameworkCompilerPath();
        var sourcePath = ResolveCandidateFile(submission.CandidateRoot, submission.Source);
        var dataPath = ResolveCandidateFile(submission.CandidateRoot, submission.Data);
        var scriptSha = string.Empty;
        var environmentReusable = true;

        if (!OperatingSystem.IsWindows())
        {
            checks.Add(Blocked("host-os", "KUKA.Sim candidate execution requires Windows."));
            return Complete();
        }

        var required = new[]
        {
            ("kukasim-engine", enginePath, string.Empty),
            ("kukasim-launcher", launcherPath, string.Empty),
            ("kukasim-bootstrap-plugin", Path.GetFullPath(request.BootstrapPluginPath), string.Empty),
            ("kukasim-create3d-api", Path.GetFullPath(request.Create3DSharedPath), string.Empty),
            ("kukasim-ux-shared", Path.Combine(installRoot, "UX.Shared.dll"), string.Empty),
            ("kukasim-caliburn", Path.Combine(installRoot, "Caliburn.Micro.dll"), string.Empty),
            ("kukasim-programming-core", programmingCorePath, string.Empty),
            ("netfx-csharp-compiler", compilerPath, string.Empty),
            ("exact-c01-component", componentPath, KukaSimCandidateExecutionContract.ExactComponentSha256),
            ("candidate-source", sourcePath, submission.Source.Sha256),
            ("candidate-data", dataPath, submission.Data.Sha256)
        };
        foreach (var item in required)
        {
            if (!ObserveRequiredFile(item.Item1, item.Item2, item.Item3, files, checks)) return Complete();
        }
        if (!string.Equals(simulationLayoutPath, componentPath, StringComparison.OrdinalIgnoreCase)
            && !ObserveRequiredFile("simulation-layout", simulationLayoutPath, string.Empty, files, checks))
        {
            return Complete();
        }

        if (!request.GuiExecutionAuthorized)
        {
            checks.Add(Blocked("gui-authorization", "KUKA.Sim is a GUI-subsystem process and this attempt has no recorded authorization."));
            return Complete();
        }
        checks.Add(Passed("gui-authorization", $"Authorized by {request.AuthorizationReference}."));

        try
        {
            preExisting = _platform.FindRunningProcesses(launcherPath, enginePath).ToList();
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            checks.Add(Failed("process-ownership", $"Process state could not be read: {exception.Message}"));
            return Complete();
        }
        if (preExisting.Count > 0)
        {
            checks.Add(Blocked("process-ownership", "A KUKA.Sim process already exists; the Lab will not attach to or close it."));
            return Complete();
        }
        checks.Add(Passed("process-ownership", "No pre-existing KUKA.Sim process exists."));

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
            scriptSha = Convert.ToHexString(SHA256.HashData(scriptBytes));
            if (!string.Equals(scriptSha, KukaSimCandidateExecutionContract.EmbeddedScriptSha256, StringComparison.Ordinal))
            {
                checks.Add(Failed("pinned-script", "Embedded Integrated script hash does not match the pinned contract."));
                return Complete();
            }
            scriptPath = Path.Combine(evidenceDirectory, $"{attemptId}.integrated-candidate.cs");
            WriteNew(scriptPath, scriptBytes);
            files.Add(ObserveExistingFile("kukasim-candidate-script", scriptPath));
            sideEffects.Add($"CreateProbeScript:{scriptPath}");
            checks.Add(Passed("pinned-script", "The fixed Integrated script was materialized create-new."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            checks.Add(Failed("evidence-directory", exception.Message));
            return Complete();
        }

        var resultPath = Path.Combine(evidenceDirectory, $"{attemptId}.result.tsv");
        var tracePath = Path.Combine(evidenceDirectory, $"{attemptId}.trace.tsv");
        var probeAssemblyPath = Path.Combine(evidenceDirectory, $"{attemptId}.integrated-candidate.dll");
        var bridgeTracePath = Path.Combine(evidenceDirectory, $"{attemptId}.bootstrap-trace.log");
        KukaSimProcessResult process;
        try
        {
            process = _platform.Run(new KukaSimCandidateExecutionProcessRequest(
                launcherPath,
                enginePath,
                compilerPath,
                scriptPath,
                probeAssemblyPath,
                simulationLayoutPath,
                sourcePath,
                dataPath,
                submission.ProgramName,
                resultPath,
                tracePath,
                bridgeTracePath,
                TimeSpan.FromSeconds(request.TimeoutSeconds)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            process = new KukaSimProcessResult(-1, false, false, false, true, 0, [], $"{exception.GetType().Name}: {exception.Message}");
        }
        if (process.ProcessStarted)
        {
            sideEffects.Add($"StartOwnedKukaSim:{launcherPath}");
            sideEffects.Add("RequestOwnedKukaSimExit:PinnedInProcessScript");
        }
        if (File.Exists(probeAssemblyPath))
        {
            files.Add(ObserveExistingFile("kukasim-compiled-probe", probeAssemblyPath));
            sideEffects.Add($"CompileProbeAssembly:{probeAssemblyPath}");
        }
        if (File.Exists(bridgeTracePath))
        {
            files.Add(ObserveExistingFile("kukasim-bootstrap-trace", bridgeTracePath));
        }
        var command = new KukaSimCommandObservation
        {
            ProcessStarted = process.ProcessStarted,
            ExitCode = process.ExitCode,
            TimedOut = process.TimedOut,
            ForcedTerminationUsed = process.ForcedTerminationUsed,
            CleanupVerified = process.CleanupVerified,
            DurationMilliseconds = Math.Max(0, process.DurationMilliseconds),
            OwnedProcessIds = process.OwnedProcessIds.Distinct().Order().ToList(),
            PlatformError = process.PlatformError
        };
        KukaSimIntegratedRawResult? result = null;
        if (File.Exists(resultPath))
        {
            files.Add(ObserveExistingFile("kukasim-candidate-result", resultPath));
            try { result = KukaSimIntegratedRawParser.Parse(resultPath); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException or FormatException)
            {
                checks.Add(Failed("candidate-result-parse", exception.Message));
            }
        }
        else
        {
            checks.Add(process.TimedOut
                ? Blocked("candidate-result-parse", "No result was produced before timeout.")
                : Failed("candidate-result-parse", "KUKA.Sim exited without the required result."));
        }
        if (File.Exists(tracePath)) files.Add(ObserveExistingFile("kukasim-candidate-trace", tracePath));
        attempt = new KukaSimIntegratedAttempt
        {
            Role = "Candidate",
            ProgramName = submission.ProgramName,
            SourcePath = sourcePath,
            DataPath = dataPath,
            RawResultPath = resultPath,
            TracePath = tracePath,
            Command = command,
            Result = result,
            ResultCanonicalSha256 = result is null ? string.Empty : ReceiptSerialization.ComputeCanonicalSha256(result)
        };

        var remaining = _platform.FindRunningProcesses(launcherPath, enginePath);
        environmentReusable = process.CleanupVerified && remaining.Count == 0;
        checks.Add(environmentReusable
            ? Passed(
                "process-cleanup",
                process.ForcedTerminationUsed
                    ? "The owned KUKA.Sim process required bounded PID-specific termination after durable evidence was written; cleanup was verified and no related process remains."
                    : "The owned KUKA.Sim process exited naturally and no related process remains.")
            : Failed("process-cleanup", "KUKA.Sim cleanup is unverified or a related process remains."));
        var disposition = KukaSimCandidateExecutionClassifier.Classify(submission, attempt);
        checks.Add(disposition is KukaSimCandidateExecutionDisposition.Executed or KukaSimCandidateExecutionDisposition.RejectedByIntegratedInterpreter
            ? Passed("candidate-integrated-run", disposition == KukaSimCandidateExecutionDisposition.Executed
                ? "The arbitrary candidate completed under exact-C01 Integrated/Go with joint/TCP evidence."
                : "The arbitrary candidate was rejected before completed motion with KUKA.Sim messages.")
            : Failed("candidate-integrated-run", "The arbitrary candidate did not produce an accepted Integrated execution or rejection."));
        return Complete();

        KukaSimCandidateExecutionOutcome Complete()
        {
            stopwatch.Stop();
            var disposition = KukaSimCandidateExecutionClassifier.Classify(submission, attempt);
            var terminal = checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
                ? EnvironmentTerminalClassification.Failed
                : checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked)
                    ? EnvironmentTerminalClassification.Blocked
                    : EnvironmentTerminalClassification.Ready;
            var accepted = terminal == EnvironmentTerminalClassification.Ready
                && disposition is KukaSimCandidateExecutionDisposition.Executed or KukaSimCandidateExecutionDisposition.RejectedByIntegratedInterpreter
                && environmentReusable;
            var payload = new KukaSimCandidateExecutionPayload
            {
                ReceiptId = $"kukasim-candidate-execution-{attemptId}",
                AttemptId = attemptId,
                CoreAssemblySha256 = ComputeFileSha256(typeof(KukaSimCandidateExecutionRunner).Assembly.Location),
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
                Submission = submission,
                EnginePath = enginePath,
                ComponentPath = componentPath,
                SimulationLayoutPath = simulationLayoutPath,
                EvidenceDirectory = evidenceDirectory,
                EmbeddedScriptSha256 = scriptSha,
                GuiExecutionAuthorized = request.GuiExecutionAuthorized,
                AuthorizationReference = request.AuthorizationReference,
                SimulationPerformed = accepted && disposition == KukaSimCandidateExecutionDisposition.Executed,
                OfficeLiteConnectionPerformed = false,
                RcsProviderUsed = false,
                PhysicalControllerContacted = false,
                NativeKssStatus = NativeKssStatus.NotRun,
                PreExistingProcesses = preExisting,
                Attempt = attempt,
                Checks = checks,
                Files = files,
                SideEffects = sideEffects,
                EnvironmentReusable = environmentReusable
            };
            return new KukaSimCandidateExecutionOutcome(new KukaSimCandidateExecutionReceipt
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
            });
        }
    }

    private static KukaSimIntegratedAttempt EmptyAttempt(NativeKssCandidateSubmissionPlan submission) => new()
    {
        Role = "Candidate",
        ProgramName = submission.ProgramName,
        SourcePath = ResolveCandidateFile(submission.CandidateRoot, submission.Source),
        DataPath = ResolveCandidateFile(submission.CandidateRoot, submission.Data)
    };

    private bool ObserveRequiredFile(
        string id,
        string path,
        string expectedSha256,
        List<EnvironmentFileObservation> files,
        List<EnvironmentCheck> checks)
    {
        if (!File.Exists(path))
        {
            files.Add(new EnvironmentFileObservation { Id = id, Path = Path.GetFullPath(path), Exists = false });
            checks.Add(Blocked(id, $"Required file is missing: {path}"));
            return false;
        }
        var observation = ObserveExistingFile(id, path);
        files.Add(observation);
        if (!string.IsNullOrEmpty(expectedSha256)
            && !string.Equals(observation.Sha256, expectedSha256, StringComparison.Ordinal))
        {
            checks.Add(Failed(id, "Required file does not match the pinned SHA-256."));
            return false;
        }
        checks.Add(Passed(id, string.IsNullOrEmpty(expectedSha256)
            ? "Required file exists and was hashed."
            : "Required file matches the pinned SHA-256."));
        return true;
    }

    private EnvironmentFileObservation ObserveExistingFile(string id, string path)
    {
        var info = new FileInfo(path);
        return new EnvironmentFileObservation
        {
            Id = id,
            Path = info.FullName,
            Exists = true,
            Bytes = info.Length,
            Sha256 = _fileSha256(info.FullName)
        };
    }

    private static string ResolveCandidateFile(string root, RawKrlCandidateFile file)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, file.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Candidate file escaped the verified source root.");
        }
        return fullPath;
    }

    private static byte[] ReadEmbeddedScript()
    {
        using var stream = typeof(KukaSimCandidateExecutionRunner).Assembly.GetManifestResourceStream(
            KukaSimCandidateExecutionContract.EmbeddedScriptResource)
            ?? throw new InvalidOperationException("Pinned KUKA.Sim Integrated script resource is missing.");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true);
        var canonical = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return Encoding.UTF8.GetBytes(canonical);
    }

    private static void WriteNew(string path, byte[] content)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(content);
        stream.Flush(true);
    }

    private static void ValidateIdentifier(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (!Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("Identifier must match [A-Za-z0-9][A-Za-z0-9._-]{2,127}.", parameterName);
        }
    }

    private static EnvironmentCheck Passed(string id, string detail) => new() { Id = id, Status = EnvironmentCheckStatus.Passed, Detail = detail };
    private static EnvironmentCheck Blocked(string id, string detail) => new() { Id = id, Status = EnvironmentCheckStatus.Blocked, Detail = detail };
    private static EnvironmentCheck Failed(string id, string detail) => new() { Id = id, Status = EnvironmentCheckStatus.Failed, Detail = detail };
    private static string ComputeFileSha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}

internal sealed record KukaSimCandidateExecutionProcessRequest(
    string LauncherPath,
    string EnginePath,
    string CompilerPath,
    string ScriptPath,
    string ProbeAssemblyPath,
    string SimulationLayoutPath,
    string SourcePath,
    string DataPath,
    string ProgramName,
    string ResultPath,
    string TracePath,
    string BridgeTracePath,
    TimeSpan Timeout);

internal interface IKukaSimCandidateExecutionPlatform
{
    IReadOnlyList<KukaSimProcessObservation> FindRunningProcesses(string launcherPath, string enginePath);
    KukaSimProcessResult Run(KukaSimCandidateExecutionProcessRequest request);
}

// KUKA.Sim 4.10 ignores the legacy /csscript route used by 4.3. Execute the
// same pinned probe through the already accepted 4.10 vendor-plugin bootstrap.
internal sealed class KukaSimCandidateExecutionPlatform : IKukaSimCandidateExecutionPlatform
{
    private readonly KukaSimComponentPlatform _platform = new();

    public IReadOnlyList<KukaSimProcessObservation> FindRunningProcesses(string launcherPath, string enginePath) =>
        _platform.FindRunningProcesses(launcherPath, enginePath);

    public KukaSimProcessResult Run(KukaSimCandidateExecutionProcessRequest request) =>
        _platform.RunProbe(
            request.LauncherPath,
            request.EnginePath,
            request.CompilerPath,
            request.ScriptPath,
            request.ProbeAssemblyPath,
            "KukaLabIntegratedLiveProbe",
            request.BridgeTracePath,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["KUKA_LAB_RESULT_PATH"] = request.ResultPath,
                ["KUKA_LAB_TRACE_PATH"] = request.TracePath,
                ["KUKA_LAB_COMPONENT_PATH"] = request.SimulationLayoutPath,
                ["KUKA_LAB_SOURCE_PATH"] = request.SourcePath,
                ["KUKA_LAB_DATA_PATH"] = request.DataPath,
                ["KUKA_LAB_PROGRAM_NAME"] = request.ProgramName
            },
            request.Timeout,
            [
                Path.Combine(Path.GetDirectoryName(request.EnginePath)!, "KUKA", "Kuka.Sim.ProgrammingCore.dll"),
                Path.Combine(Path.GetDirectoryName(request.EnginePath)!, "KUKA", "Kuka.Sim.Progress.dll"),
                Path.Combine(Path.GetDirectoryName(request.EnginePath)!, "KUKA", "KukaRoboter.KrlController.Core.dll")
            ]);
}

public sealed record KukaSimCandidateExecutionVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;
    public bool Succeeded { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
    public List<string> Errors { get; init; } = [];
}

public static class KukaSimCandidateExecutionReceiptVerifier
{
    public static KukaSimCandidateExecutionVerificationResult Verify(KukaSimCandidateExecutionReceipt receipt) =>
        Verify(receipt, rehashCurrentFiles: true);

    internal static KukaSimCandidateExecutionVerificationResult Verify(
        KukaSimCandidateExecutionReceipt receipt,
        bool rehashCurrentFiles)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (receipt.SchemaIdentity != KukaSimCandidateExecutionContract.ReceiptSchemaIdentity
            || receipt.SchemaVersion != KukaSimCandidateExecutionContract.ReceiptSchemaVersion)
        {
            errors.Add("KUKA.Sim candidate receipt schema identity/version is unsupported");
        }
        var payload = receipt.Payload;
        if (!string.Equals(ReceiptSerialization.ComputeCanonicalSha256(payload), receipt.PayloadSha256, StringComparison.Ordinal))
        {
            errors.Add("payload SHA-256 mismatch");
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
                errors.Add("current candidate no longer matches the simulated submission");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
        {
            errors.Add($"current candidate verification failed: {exception.Message}");
        }
        var disposition = KukaSimCandidateExecutionClassifier.Classify(payload.Submission, payload.Attempt);
        if (disposition != payload.Disposition) errors.Add("stored KUKA.Sim disposition does not match the attempt evidence");
        if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready
            && (disposition == KukaSimCandidateExecutionDisposition.InfrastructureFailed || !payload.EnvironmentReusable))
        {
            errors.Add("Ready receipt requires an accepted disposition and reusable environment");
        }
        if (payload.OfficeLiteConnectionPerformed || payload.RcsProviderUsed || payload.PhysicalControllerContacted
            || payload.NativeKssStatus != NativeKssStatus.NotRun)
        {
            errors.Add("Integrated candidate receipt may not claim OfficeLite, RCS, physical-controller or native-KSS activity");
        }
        if (!string.Equals(payload.Provider, "Integrated", StringComparison.Ordinal)
            || !string.Equals(payload.EmbeddedScriptSha256, KukaSimCandidateExecutionContract.EmbeddedScriptSha256, StringComparison.Ordinal))
        {
            errors.Add("provider or pinned script identity is invalid");
        }
        if (payload.PreExistingProcesses.Count != 0) errors.Add("accepted receipt cannot own a pre-existing KUKA.Sim process");
        if (payload.Attempt.Result is not null
            && !string.Equals(payload.Attempt.ResultCanonicalSha256,
                ReceiptSerialization.ComputeCanonicalSha256(payload.Attempt.Result), StringComparison.Ordinal))
        {
            errors.Add("stored result canonical SHA-256 is invalid");
        }
        if (File.Exists(payload.Attempt.RawResultPath))
        {
            try
            {
                var reparsed = KukaSimIntegratedRawParser.Parse(payload.Attempt.RawResultPath);
                if (!string.Equals(ReceiptSerialization.ComputeCanonicalSha256(reparsed),
                    payload.Attempt.ResultCanonicalSha256, StringComparison.Ordinal))
                {
                    errors.Add("raw KUKA.Sim result no longer corroborates the parsed result");
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException or FormatException)
            {
                errors.Add($"raw KUKA.Sim result verification failed: {exception.Message}");
            }
        }
        else if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready)
        {
            errors.Add("accepted receipt raw result is missing");
        }
        if (rehashCurrentFiles)
        {
            var component = payload.Files.SingleOrDefault(file => file.Id == "exact-c01-component");
            if (component is null || !File.Exists(component.Path)
                || !string.Equals(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(component.Path))),
                    KukaSimCandidateExecutionContract.ExactComponentSha256, StringComparison.Ordinal))
            {
                errors.Add("current exact-C01 component hash is invalid");
            }
        }
        return new KukaSimCandidateExecutionVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }
}

public static class KukaSimCandidateExecutionReceiptWriter
{
    public static string WriteNew(string outputPath, KukaSimCandidateExecutionReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        var verification = KukaSimCandidateExecutionReceiptVerifier.Verify(receipt);
        if (!verification.Succeeded)
        {
            throw new InvalidOperationException(
                "KUKA.Sim candidate-execution receipt is invalid: " + string.Join("; ", verification.Errors));
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
