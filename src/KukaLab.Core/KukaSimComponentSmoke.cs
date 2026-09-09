using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class KukaSimComponentSmokeContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.kukasim-component-smoke-receipt";
    public const int ReceiptSchemaVersion = 3;
    public const string ResultSchemaIdentity = "kuka.lab.kukasim-component-smoke-result";
    public const int ResultSchemaVersion = 1;
    public const string EmbeddedScriptResource = "KukaLab.Probes.KukaSim.ComponentLoadSmoke.cs";
    public const string EmbeddedScriptSha256 = "6DFB338A2AEC482827C5A61A8142A323BAD52F79B89E24B9F7432C8F9F079757";
    public const string ExactKr210R2700ComponentSha256 = "9717CF4C0A1FD74E8D293E4D5B0E3D90D54406181D686E69AB47D3FD84DF9C65";
    public const string ExactKr210R2700Component410Sha256 = "9307A04CC52BF70CC7D357676B80ABC1F3F3D5D452AA2AD64EC48E4A37543F33";

    internal static readonly IReadOnlyList<string> RequiredUnsupportedGaps =
    [
        "This smoke does not connect KUKA.Sim to OfficeLite or perform VRC synchronization.",
        "This smoke does not load, compile or run a KRL program and native KSS remains NotRun.",
        "A loaded component and saved layout do not prove controller-generation, machine-data or real-cell compatibility."
    ];

    internal static readonly IReadOnlySet<int> SupportedReceiptSchemaVersions =
        new HashSet<int> { 1, 2, ReceiptSchemaVersion };
}

public sealed record KukaSimComponentSmokeRequest
{
    public required string LauncherPath { get; init; }

    public required string EnginePath { get; init; }

    public required string BootstrapPluginPath { get; init; }

    public required string Create3DSharedPath { get; init; }

    public required string ComponentPath { get; init; }

    public required string ExpectedComponentSha256 { get; init; }

    public required string EvidenceDirectory { get; init; }

    public int TimeoutSeconds { get; init; } = 180;

    public bool GuiExecutionAuthorized { get; init; }

    public string AuthorizationReference { get; init; } = string.Empty;

    public static KukaSimComponentSmokeRequest CreateDefault(
        string evidenceDirectory,
        bool guiExecutionAuthorized,
        string authorizationReference,
        string? launcherPath = null,
        string? componentPath = null,
        int timeoutSeconds = 180)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceDirectory);
        var kukaSim = KukaSimInstallationDiscovery.ResolveFromLauncher(launcherPath, componentPath);
        return new KukaSimComponentSmokeRequest
        {
            LauncherPath = kukaSim.LauncherPath,
            EnginePath = kukaSim.EnginePath,
            BootstrapPluginPath = kukaSim.BootstrapPluginPath,
            Create3DSharedPath = kukaSim.Create3DSharedPath,
            ComponentPath = kukaSim.ComponentPath,
            ExpectedComponentSha256 = KukaSimComponentSmokeContract.ExactKr210R2700Component410Sha256,
            EvidenceDirectory = Path.GetFullPath(evidenceDirectory),
            TimeoutSeconds = timeoutSeconds,
            GuiExecutionAuthorized = guiExecutionAuthorized,
            AuthorizationReference = authorizationReference
        };
    }
}

public sealed record KukaSimComponentSmokeReceipt
{
    public string SchemaIdentity { get; init; } = KukaSimComponentSmokeContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = KukaSimComponentSmokeContract.ReceiptSchemaVersion;

    public required KukaSimComponentSmokePayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record KukaSimComponentSmokePayload
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

    public string LauncherPath { get; init; } = string.Empty;

    public string EnginePath { get; init; } = string.Empty;

    public string ComponentPath { get; init; } = string.Empty;

    public string ExpectedComponentSha256 { get; init; } = string.Empty;

    public string EvidenceDirectory { get; init; } = string.Empty;

    public string EmbeddedScriptSha256 { get; init; } = string.Empty;

    public bool GuiExecutionAuthorized { get; init; }

    public string AuthorizationReference { get; init; } = string.Empty;

    public bool VrcConnectionPerformed { get; init; }

    public bool SimulationPerformed { get; init; }

    public NativeKssStatus NativeKssStatus { get; init; } = NativeKssStatus.NotRun;

    public KukaSimCommandObservation Command { get; init; } = new();

    public List<KukaSimProcessObservation> PreExistingProcesses { get; init; } = [];

    public KukaSimInProcessResult? InProcessResult { get; init; }

    public List<EnvironmentCheck> Checks { get; init; } = [];

    public List<EnvironmentFileObservation> Files { get; init; } = [];

    public List<string> SideEffects { get; init; } = [];

    public bool EnvironmentReusable { get; init; }

    public List<string> UnsupportedGaps { get; init; } = [];
}

public sealed record KukaSimCommandObservation
{
    public bool ProcessStarted { get; init; }

    public int ExitCode { get; init; }

    public bool TimedOut { get; init; }

    public bool ForcedTerminationUsed { get; init; }

    public bool CleanupVerified { get; init; }

    public long DurationMilliseconds { get; init; }

    public List<int> OwnedProcessIds { get; init; } = [];

    public string PlatformError { get; init; } = string.Empty;
}

public sealed record KukaSimProcessObservation
{
    public int ProcessId { get; init; }

    public string ProcessName { get; init; } = string.Empty;

    public string ExecutablePath { get; init; } = string.Empty;

    public DateTimeOffset? StartedAtUtc { get; init; }
}

public sealed record KukaSimInProcessResult
{
    public string SchemaIdentity { get; init; } = string.Empty;

    public int SchemaVersion { get; init; }

    public string Status { get; init; } = string.Empty;

    public string ComponentPath { get; init; } = string.Empty;

    public string ComponentSha256 { get; init; } = string.Empty;

    public string LayoutPath { get; init; } = string.Empty;

    public bool LayoutSaved { get; init; }

    public bool IsComponent { get; init; }

    public int LoadedComponentCount { get; init; }

    public List<string> LoadedComponentNames { get; init; } = [];

    public bool ApplicationInitialized { get; init; }

    public bool ApplicationReady { get; init; }

    public bool ValidLicenseExists { get; init; }

    public string EngineVersion { get; init; } = string.Empty;

    public bool ExitRequested { get; init; }

    public string ErrorType { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed record KukaSimComponentSmokeOutcome(KukaSimComponentSmokeReceipt Receipt)
{
    public int ExitCode => Receipt.Payload.TerminalClassification switch
    {
        EnvironmentTerminalClassification.Ready => 0,
        EnvironmentTerminalClassification.Failed => 2,
        _ => 3
    };
}

public sealed class KukaSimComponentSmokeRunner
{
    private readonly IKukaSimComponentPlatform _platform;
    private readonly TimeProvider _timeProvider;

    public KukaSimComponentSmokeRunner()
        : this(new KukaSimComponentPlatform(), TimeProvider.System)
    {
    }

    internal KukaSimComponentSmokeRunner(
        IKukaSimComponentPlatform platform,
        TimeProvider timeProvider)
    {
        _platform = platform;
        _timeProvider = timeProvider;
    }

    public KukaSimComponentSmokeOutcome Run(KukaSimComponentSmokeRequest request, string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateIdentifier(attemptId, nameof(attemptId));
        if (request.TimeoutSeconds is < 10 or > 600)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "KUKA.Sim component-smoke timeout must be from 10 through 600 seconds.");
        }

        if (!IsSha256(request.ExpectedComponentSha256))
        {
            throw new ArgumentException("Expected component SHA-256 is invalid.", nameof(request));
        }

        if (request.GuiExecutionAuthorized)
        {
            ValidateIdentifier(request.AuthorizationReference, nameof(request.AuthorizationReference));
        }

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var checks = new List<EnvironmentCheck>();
        var files = new List<EnvironmentFileObservation>();
        var sideEffects = new List<string>();
        var unsupportedGaps = KukaSimComponentSmokeContract.RequiredUnsupportedGaps.ToList();
        var launcherPath = Path.GetFullPath(request.LauncherPath);
        var enginePath = Path.GetFullPath(request.EnginePath);
        var installRoot = Path.GetDirectoryName(enginePath)
            ?? throw new ArgumentException("KUKA.Sim engine path has no parent directory.", nameof(request));
        var bootstrapPluginPath = Path.GetFullPath(request.BootstrapPluginPath);
        var create3DSharedPath = Path.GetFullPath(request.Create3DSharedPath);
        var uxSharedPath = Path.Combine(installRoot, "UX.Shared.dll");
        var caliburnMicroPath = Path.Combine(installRoot, "Caliburn.Micro.dll");
        var frameworkCompilerPath = ResolveFrameworkCompilerPath();
        var componentPath = Path.GetFullPath(request.ComponentPath);
        var evidenceDirectory = Path.GetFullPath(request.EvidenceDirectory);
        var scriptSha256 = string.Empty;
        var preExistingProcesses = new List<KukaSimProcessObservation>();
        var command = new KukaSimCommandObservation { CleanupVerified = true };
        KukaSimInProcessResult? inProcessResult = null;
        var environmentReusable = true;

        if (!OperatingSystem.IsWindows())
        {
            checks.Add(Blocked("host-os", "KUKA.Sim component smoke requires Windows."));
            return Complete();
        }

        if (!ObserveFile("kukasim-launcher", launcherPath, true, files, checks)
            || !ObserveFile("kukasim-engine", enginePath, true, files, checks)
            || !ObserveFile("kukasim-bootstrap-plugin", bootstrapPluginPath, true, files, checks)
            || !ObserveFile("kukasim-create3d-api", create3DSharedPath, true, files, checks)
            || !ObserveFile("kukasim-ux-shared", uxSharedPath, true, files, checks)
            || !ObserveFile("kukasim-caliburn", caliburnMicroPath, true, files, checks)
            || !ObserveFile("netfx-csharp-compiler", frameworkCompilerPath, true, files, checks)
            || !ObserveFile("kukasim-component-source", componentPath, false, files, checks))
        {
            return Complete();
        }

        var componentObservation = files.Single(file => file.Id == "kukasim-component-source");
        if (!string.Equals(
                componentObservation.Sha256,
                request.ExpectedComponentSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            checks.Add(Failed(
                "component-identity",
                "The component source does not match the explicitly expected SHA-256."));
            return Complete();
        }

        checks.Add(Passed(
            "component-identity",
            "The component source matches the explicitly expected SHA-256."));

        if (!request.GuiExecutionAuthorized)
        {
            checks.Add(Blocked(
                "gui-authorization",
                "KUKA.Sim is a GUI-subsystem process and this attempt has no operation-scoped GUI execution authorization."));
            return Complete();
        }

        checks.Add(Passed(
            "gui-authorization",
            $"Operation-scoped GUI execution authorization reference was supplied: {request.AuthorizationReference}."));

        try
        {
            preExistingProcesses = _platform.FindRunningProcesses(launcherPath, enginePath).ToList();
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            checks.Add(Failed(
                "process-ownership",
                $"Pre-existing KUKA.Sim process state could not be determined: {exception.Message}"));
            return Complete();
        }

        if (preExistingProcesses.Count > 0)
        {
            checks.Add(Blocked(
                "process-ownership",
                "A KUKA.Sim launcher/engine process already exists. The Lab will not attach to, close or reuse it."));
            return Complete();
        }

        checks.Add(Passed(
            "process-ownership",
            "No pre-existing KUKA.Sim launcher/engine process exists; a new process can be owned by this attempt."));

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
            checks.Add(Failed(
                "evidence-directory",
                $"Evidence directory could not be prepared: {exception.Message}"));
            return Complete();
        }

        var scriptPath = Path.Combine(evidenceDirectory, $"{attemptId}.component-load-smoke.cs");
        var probeAssemblyPath = Path.Combine(evidenceDirectory, $"{attemptId}.component-load-smoke.dll");
        var resultPath = Path.Combine(evidenceDirectory, $"{attemptId}.component-load-result.json");
        var layoutPath = Path.Combine(evidenceDirectory, $"{attemptId}.loaded-layout.vcmx");
        var bridgeTracePath = Path.Combine(evidenceDirectory, $"{attemptId}.bootstrap-trace.log");
        try
        {
            var scriptBytes = ReadEmbeddedScript();
            scriptSha256 = Convert.ToHexString(SHA256.HashData(scriptBytes));
            if (!string.Equals(
                    scriptSha256,
                    KukaSimComponentSmokeContract.EmbeddedScriptSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                checks.Add(Failed("safe-smoke-script", "Embedded KUKA.Sim smoke script hash is not the pinned safe value."));
                return Complete();
            }

            WriteNew(scriptPath, scriptBytes);
            sideEffects.Add($"CreateProbeScript:{scriptPath}");
            files.Add(ObserveExistingFile("kukasim-safe-smoke-script", scriptPath, false));
            checks.Add(Passed(
                "safe-smoke-script",
                "Pinned component-load script was materialized as create-new evidence; callers cannot supply script text."));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            checks.Add(Failed(
                "safe-smoke-script",
                $"Safe KUKA.Sim smoke script could not be materialized: {exception.Message}"));
            return Complete();
        }

        KukaSimProcessResult processResult;
        try
        {
            processResult = _platform.Run(
                launcherPath,
                enginePath,
                bootstrapPluginPath,
                frameworkCompilerPath,
                scriptPath,
                probeAssemblyPath,
                componentPath,
                resultPath,
                layoutPath,
                bridgeTracePath,
                TimeSpan.FromSeconds(request.TimeoutSeconds));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or Win32Exception)
        {
            processResult = new KukaSimProcessResult(
                -1,
                false,
                false,
                false,
                true,
                0,
                [],
                $"{exception.GetType().Name}: {exception.Message}");
        }

        command = new KukaSimCommandObservation
        {
            ProcessStarted = processResult.ProcessStarted,
            ExitCode = processResult.ExitCode,
            TimedOut = processResult.TimedOut,
            ForcedTerminationUsed = processResult.ForcedTerminationUsed,
            CleanupVerified = processResult.CleanupVerified,
            DurationMilliseconds = Math.Max(0, processResult.DurationMilliseconds),
            OwnedProcessIds = processResult.OwnedProcessIds.Distinct().Order().ToList(),
            PlatformError = processResult.PlatformError
        };
        environmentReusable = processResult.CleanupVerified;
        if (File.Exists(probeAssemblyPath))
        {
            files.Add(ObserveExistingFile("kukasim-compiled-probe", probeAssemblyPath, false));
            checks.Add(Passed("compiled-probe", "The pinned probe source compiled into an attempt-local assembly."));
            sideEffects.Add($"CompileProbeAssembly:{probeAssemblyPath}");
        }
        else
        {
            checks.Add(Failed(
                "compiled-probe",
                string.IsNullOrWhiteSpace(processResult.PlatformError)
                    ? "The attempt-local probe assembly was not produced."
                    : processResult.PlatformError));
        }

        if (File.Exists(bridgeTracePath))
        {
            files.Add(ObserveExistingFile("kukasim-bootstrap-trace", bridgeTracePath, false));
        }
        if (processResult.ProcessStarted)
        {
            sideEffects.Add($"StartOwnedKukaSim:{launcherPath}");
            sideEffects.Add("RequestOwnedKukaSimExit:PinnedInProcessScript");
        }

        if (processResult.ForcedTerminationUsed)
        {
            sideEffects.Add("ForceTerminateOwnedKukaSimAfterBoundedGracefulClose");
        }

        if (File.Exists(resultPath))
        {
            try
            {
                files.Add(ObserveExistingFile("kukasim-in-process-result", resultPath, false));
                inProcessResult = ReceiptSerialization.KukaSimInProcessResultFromJson(
                    File.ReadAllText(resultPath, new UTF8Encoding(false, true)));
                checks.Add(Passed(
                    "in-process-result",
                    "The pinned in-process script produced strict JSON result evidence."));
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or JsonException
                or DecoderFallbackException)
            {
                checks.Add(Failed(
                    "in-process-result",
                    $"KUKA.Sim result evidence could not be parsed and bound: {exception.Message}"));
            }
        }
        else
        {
            checks.Add(processResult.TimedOut
                ? Blocked("in-process-result", "No in-process result was produced before the bounded timeout.")
                : Failed("in-process-result", "KUKA.Sim exited without producing the required in-process result."));
        }

        if (File.Exists(layoutPath))
        {
            try
            {
                files.Add(ObserveExistingFile("kukasim-saved-layout", layoutPath, false));
                checks.Add(Passed("saved-layout", "The loaded world was saved as hash-bound layout evidence."));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                checks.Add(Failed("saved-layout", $"Saved layout evidence could not be hashed: {exception.Message}"));
            }
        }
        else if (!processResult.TimedOut)
        {
            checks.Add(Failed("saved-layout", "KUKA.Sim did not create the required saved-layout evidence."));
        }

        if (!processResult.CleanupVerified)
        {
            checks.Add(Failed(
                "process-cleanup",
                "One or more Lab-owned KUKA.Sim launcher/engine processes remained after cleanup."));
        }
        else if (processResult.ForcedTerminationUsed)
        {
            checks.Add(Passed(
                "process-cleanup",
                "The isolated Lab-owned KUKA.Sim process required bounded forced termination after evidence was flushed; cleanup was verified."));
        }
        else
        {
            checks.Add(Passed(
                "process-cleanup",
                "All Lab-owned KUKA.Sim launcher/engine processes exited without forced termination."));
        }

        if (!string.IsNullOrWhiteSpace(processResult.PlatformError) || !processResult.ProcessStarted)
        {
            checks.Add(Failed(
                "component-load",
                string.IsNullOrWhiteSpace(processResult.PlatformError)
                    ? "KUKA.Sim was not started."
                    : $"KUKA.Sim launch failed: {processResult.PlatformError}"));
        }
        else if (processResult.TimedOut)
        {
            checks.Add(Blocked("component-load", "KUKA.Sim component smoke exceeded its bounded timeout."));
        }
        else
        {
            var resultErrors = ValidateReadyInProcessResult(
                inProcessResult,
                componentPath,
                request.ExpectedComponentSha256,
                layoutPath);
            if (resultErrors.Count > 0)
            {
                checks.Add(Failed(
                    "component-load",
                    $"In-process component-load assertions failed: {string.Join("; ", resultErrors)}"));
            }
            else if (processResult.ExitCode != 0)
            {
                checks.Add(Failed(
                    "component-load",
                    $"KUKA.Sim returned exit code {processResult.ExitCode} after the in-process smoke."));
            }
            else
            {
                checks.Add(Passed(
                    "component-load",
                    "The exact hash-bound component loaded, was classified as a component, and produced a saved layout."));
            }
        }

        return Complete();

        KukaSimComponentSmokeOutcome Complete()
        {
            stopwatch.Stop();
            var completedAt = _timeProvider.GetUtcNow();
            var terminal = checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
                ? EnvironmentTerminalClassification.Failed
                : checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked)
                    ? EnvironmentTerminalClassification.Blocked
                    : EnvironmentTerminalClassification.Ready;
            var payload = new KukaSimComponentSmokePayload
            {
                ReceiptId = $"kukasim-component-smoke-{attemptId}",
                AttemptId = attemptId,
                CoreAssemblySha256 = ComputeFileSha256(typeof(KukaSimComponentSmokeRunner).Assembly.Location),
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
                LauncherPath = launcherPath,
                EnginePath = enginePath,
                ComponentPath = componentPath,
                ExpectedComponentSha256 = request.ExpectedComponentSha256,
                EvidenceDirectory = evidenceDirectory,
                EmbeddedScriptSha256 = scriptSha256,
                GuiExecutionAuthorized = request.GuiExecutionAuthorized,
                AuthorizationReference = request.AuthorizationReference,
                VrcConnectionPerformed = false,
                SimulationPerformed = false,
                NativeKssStatus = NativeKssStatus.NotRun,
                Command = command,
                PreExistingProcesses = preExistingProcesses,
                InProcessResult = inProcessResult,
                Checks = checks,
                Files = files,
                SideEffects = sideEffects,
                EnvironmentReusable = environmentReusable,
                UnsupportedGaps = unsupportedGaps
            };
            var receipt = new KukaSimComponentSmokeReceipt
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
            };
            return new KukaSimComponentSmokeOutcome(receipt);
        }
    }

    internal static List<string> ValidateReadyInProcessResult(
        KukaSimInProcessResult? result,
        string componentPath,
        string expectedComponentSha256,
        string layoutPath)
    {
        var errors = new List<string>();
        if (result is null)
        {
            errors.Add("result is missing");
            return errors;
        }

        if (!string.Equals(result.SchemaIdentity, KukaSimComponentSmokeContract.ResultSchemaIdentity, StringComparison.Ordinal)
            || result.SchemaVersion != KukaSimComponentSmokeContract.ResultSchemaVersion)
        {
            errors.Add("result schema is invalid");
        }

        if (!string.Equals(result.Status, "Passed", StringComparison.Ordinal))
        {
            errors.Add("result status is not Passed");
        }

        if (!PathsEqual(result.ComponentPath, componentPath)
            || !string.Equals(result.ComponentSha256, expectedComponentSha256, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("component identity does not match the request");
        }

        if (!PathsEqual(result.LayoutPath, layoutPath) || !result.LayoutSaved)
        {
            errors.Add("saved layout identity is invalid");
        }

        if (!result.IsComponent
            || result.LoadedComponentCount < 1
            || result.LoadedComponentNames is null
            || result.LoadedComponentNames.Count != result.LoadedComponentCount)
        {
            errors.Add("loaded component evidence is incomplete");
        }

        if (!result.ApplicationInitialized
            || !result.ApplicationReady
            || !result.ValidLicenseExists
            || !result.ExitRequested)
        {
            errors.Add("application readiness/license/exit evidence is incomplete");
        }

        if (string.IsNullOrWhiteSpace(result.EngineVersion)
            || !string.IsNullOrEmpty(result.ErrorType)
            || !string.IsNullOrEmpty(result.ErrorMessage))
        {
            errors.Add("engine identity or error fields are inconsistent with Passed");
        }

        return errors;
    }

    private static byte[] ReadEmbeddedScript()
    {
        using var stream = typeof(KukaSimComponentSmokeRunner).Assembly.GetManifestResourceStream(
            KukaSimComponentSmokeContract.EmbeddedScriptResource)
            ?? throw new InvalidOperationException("Pinned KUKA.Sim smoke resource is missing.");
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(false, true),
            detectEncodingFromByteOrderMarks: true);
        var canonicalText = reader.ReadToEnd()
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return Encoding.UTF8.GetBytes(canonicalText);
    }

    internal static string ResolveFrameworkCompilerPath()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var framework64 = Path.Combine(windows, "Microsoft.NET", "Framework64", "v4.0.30319", "csc.exe");
        return File.Exists(framework64)
            ? framework64
            : Path.Combine(windows, "Microsoft.NET", "Framework", "v4.0.30319", "csc.exe");
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

    private static EnvironmentFileObservation ObserveExistingFile(
        string id,
        string path,
        bool includeVersion)
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
        stream.Flush(true);
    }

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static void ValidateIdentifier(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (!Regex.IsMatch(
                value,
                "^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$",
                RegexOptions.CultureInvariant))
        {
            throw new ArgumentException(
                "Identifier must match [A-Za-z0-9][A-Za-z0-9._-]{2,127}.",
                parameterName);
        }
    }

    internal static bool PathsEqual(string left, string right)
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

    private static EnvironmentCheck Passed(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Passed, Detail = detail };

    private static EnvironmentCheck Blocked(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Blocked, Detail = detail };

    private static EnvironmentCheck Failed(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Failed, Detail = detail };
}

public sealed record KukaSimComponentSmokeReceiptVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}

public static class KukaSimComponentSmokeReceiptVerifier
{
    public static KukaSimComponentSmokeReceiptVerificationResult Verify(
        KukaSimComponentSmokeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (!string.Equals(
                receipt.SchemaIdentity,
                KukaSimComponentSmokeContract.ReceiptSchemaIdentity,
                StringComparison.Ordinal))
        {
            errors.Add($"schemaIdentity must be {KukaSimComponentSmokeContract.ReceiptSchemaIdentity}");
        }

        if (!KukaSimComponentSmokeContract.SupportedReceiptSchemaVersions.Contains(receipt.SchemaVersion))
        {
            errors.Add(
                $"schemaVersion must be one of: {string.Join(", ", KukaSimComponentSmokeContract.SupportedReceiptSchemaVersions.Order())}");
        }

        if (ReferenceEquals(receipt.Payload, null))
        {
            return new KukaSimComponentSmokeReceiptVerificationResult
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
            || !IsSha256(payload.ExpectedComponentSha256)
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

        if (payload.Command is null
            || payload.StartedAtUtc > payload.CompletedAtUtc
            || payload.DurationMilliseconds < 0
            || payload.Command.DurationMilliseconds < 0)
        {
            errors.Add("payload command/timing is invalid");
        }

        if (!PathsAreAbsolute(
                payload.LauncherPath,
                payload.EnginePath,
                payload.ComponentPath,
                payload.EvidenceDirectory))
        {
            errors.Add("payload paths must be absolute");
        }

        if (payload.Checks is null || payload.Checks.Count == 0
            || payload.Checks.Any(check => check is null || string.IsNullOrWhiteSpace(check.Id))
            || payload.Checks.GroupBy(check => check.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            errors.Add("payload.checks must contain evidence with unique non-empty IDs");
        }

        if (payload.Files is null
            || payload.SideEffects is null
            || payload.UnsupportedGaps is null
            || payload.PreExistingProcesses is null)
        {
            errors.Add("payload evidence collections cannot be null");
        }
        else if (payload.Files.Any(file => file is null
                || string.IsNullOrWhiteSpace(file.Id)
                || string.IsNullOrWhiteSpace(file.Path)
                || !Path.IsPathFullyQualified(file.Path))
            || payload.Files.GroupBy(file => file.Id, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1))
        {
            errors.Add("payload.files must have unique non-empty IDs and absolute paths");
        }
        else
        {
            VerifyObservedFiles(payload.Files, errors);
        }

        if (payload.PreExistingProcesses is not null
            && (payload.PreExistingProcesses.Any(process => process is null
                    || process.ProcessId <= 0
                    || string.IsNullOrWhiteSpace(process.ProcessName))
                || payload.PreExistingProcesses.GroupBy(process => process.ProcessId).Any(group => group.Count() > 1)))
        {
            errors.Add("pre-existing process evidence is invalid");
        }

        if (payload.NativeKssStatus != NativeKssStatus.NotRun
            || payload.VrcConnectionPerformed
            || payload.SimulationPerformed)
        {
            errors.Add("component smoke cannot claim VRC, simulation or native KSS evidence");
        }

        if (payload.UnsupportedGaps is null
            || KukaSimComponentSmokeContract.RequiredUnsupportedGaps.Any(
                required => !payload.UnsupportedGaps.Contains(required, StringComparer.Ordinal)))
        {
            errors.Add("component smoke must preserve its VRC, KSS and compatibility limitations");
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

        if (payload.Command is not null
            && !payload.GuiExecutionAuthorized
            && payload.Command.ProcessStarted)
        {
            errors.Add("KUKA.Sim cannot start without recorded GUI execution authorization");
        }

        if (payload.Command is not null
            && payload.PreExistingProcesses is { Count: > 0 }
            && payload.Command.ProcessStarted)
        {
            errors.Add("component smoke cannot start while pre-existing KUKA.Sim processes are recorded");
        }

        if (payload.Command is not null
            && !payload.Command.CleanupVerified
            && payload.EnvironmentReusable)
        {
            errors.Add("environment cannot be reusable when process cleanup is unverified");
        }

        if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready)
        {
            VerifyReadyEvidence(payload, receipt.SchemaVersion, errors);
        }

        if (!string.Equals(
                receipt.PayloadSha256,
                ReceiptSerialization.ComputeCanonicalSha256(payload),
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("payloadSha256 does not match the canonical payload");
        }

        return new KukaSimComponentSmokeReceiptVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }

    private static void VerifyReadyEvidence(
        KukaSimComponentSmokePayload payload,
        int receiptSchemaVersion,
        List<string> errors)
    {
        if (!payload.GuiExecutionAuthorized
            || string.IsNullOrWhiteSpace(payload.AuthorizationReference)
            || payload.PreExistingProcesses.Count != 0
            || !payload.Command.ProcessStarted
            || payload.Command.ExitCode != 0
            || payload.Command.TimedOut
            || !payload.Command.CleanupVerified
            || !payload.EnvironmentReusable
            || !string.Equals(
                payload.EmbeddedScriptSha256,
                KukaSimComponentSmokeContract.EmbeddedScriptSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("ready component smoke lacks authorization, ownership, pinned-script, process or cleanup evidence");
        }

        var expectedLayoutPath = Path.Combine(
            payload.EvidenceDirectory,
            $"{payload.AttemptId}.loaded-layout.vcmx");
        errors.AddRange(KukaSimComponentSmokeRunner.ValidateReadyInProcessResult(
            payload.InProcessResult,
            payload.ComponentPath,
            payload.ExpectedComponentSha256,
            expectedLayoutPath));

        var requiredCheckIds = new List<string>
        {
            "kukasim-launcher",
            "kukasim-engine",
            "kukasim-create3d-api",
            "kukasim-component-source",
            "component-identity",
            "gui-authorization",
            "process-ownership",
            "evidence-directory",
            "safe-smoke-script",
            "in-process-result",
            "saved-layout",
            "process-cleanup",
            "component-load"
        };
        if (receiptSchemaVersion == 1)
        {
            requiredCheckIds.Insert(2, "kukasim-script-starter");
        }
        else if (receiptSchemaVersion == 2)
        {
            requiredCheckIds.InsertRange(
                2,
                [
                    "kukasim-script-starter",
                    "kukasim-visualstudio-interop",
                    "kukasim-codedom-provider",
                    "kukasim-roslyn-compiler"
                ]);
        }
        else
        {
            requiredCheckIds.InsertRange(
                2,
                [
                    "kukasim-bootstrap-plugin",
                    "kukasim-ux-shared",
                    "kukasim-caliburn",
                    "netfx-csharp-compiler",
                    "compiled-probe"
                ]);
        }
        foreach (var checkId in requiredCheckIds)
        {
            if (!payload.Checks.Any(check => check.Id == checkId
                    && check.Status == EnvironmentCheckStatus.Passed))
            {
                errors.Add($"ready component smoke lacks passed check: {checkId}");
            }
        }

        var requiredFileIds = new List<string>
        {
            "kukasim-launcher",
            "kukasim-engine",
            "kukasim-create3d-api",
            "kukasim-component-source",
            "kukasim-safe-smoke-script",
            "kukasim-in-process-result",
            "kukasim-saved-layout"
        };
        if (receiptSchemaVersion == 1)
        {
            requiredFileIds.Insert(2, "kukasim-script-starter");
        }
        else if (receiptSchemaVersion == 2)
        {
            requiredFileIds.InsertRange(
                2,
                [
                    "kukasim-script-starter",
                    "kukasim-visualstudio-interop",
                    "kukasim-codedom-provider",
                    "kukasim-roslyn-compiler"
                ]);
        }
        else
        {
            requiredFileIds.InsertRange(
                2,
                [
                    "kukasim-bootstrap-plugin",
                    "kukasim-ux-shared",
                    "kukasim-caliburn",
                    "netfx-csharp-compiler",
                    "kukasim-compiled-probe"
                ]);
        }
        foreach (var fileId in requiredFileIds)
        {
            if (!payload.Files.Any(file => file.Id == fileId && file.Exists))
            {
                errors.Add($"ready component smoke lacks observed file: {fileId}");
            }
        }

        var component = payload.Files.SingleOrDefault(file => file.Id == "kukasim-component-source");
        if (component is not null
            && (!KukaSimComponentSmokeRunner.PathsEqual(component.Path, payload.ComponentPath)
                || !string.Equals(
                    component.Sha256,
                    payload.ExpectedComponentSha256,
                    StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("component file evidence does not match the requested component identity");
        }

        var script = payload.Files.SingleOrDefault(file => file.Id == "kukasim-safe-smoke-script");
        if (script is not null
            && !string.Equals(
                script.Sha256,
                KukaSimComponentSmokeContract.EmbeddedScriptSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("script file evidence does not match the pinned KUKA.Sim smoke script");
        }

        var requiredSideEffectPrefixes = new[]
        {
            "CreateProbeScript:",
            "StartOwnedKukaSim:",
            "RequestOwnedKukaSimExit:"
        };
        foreach (var prefix in requiredSideEffectPrefixes)
        {
            if (!payload.SideEffects.Any(effect => effect.StartsWith(prefix, StringComparison.Ordinal)))
            {
                errors.Add($"ready component smoke lacks declared side effect: {prefix}");
            }
        }

        if (receiptSchemaVersion >= 3
            && !payload.SideEffects.Any(effect => effect.StartsWith("CompileProbeAssembly:", StringComparison.Ordinal)))
        {
            errors.Add("ready component smoke lacks declared side effect: CompileProbeAssembly:");
        }
    }

    private static bool PathsAreAbsolute(params string[] paths) =>
        paths.All(path => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path));

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

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

public static class KukaSimComponentSmokeReceiptWriter
{
    public static string WriteNew(string outputPath, KukaSimComponentSmokeReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!KukaSimComponentSmokeReceiptVerifier.Verify(receipt).Succeeded)
        {
            throw new InvalidOperationException("KUKA.Sim component smoke receipt integrity is invalid.");
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
        stream.Flush(true);
        return fullPath;
    }
}

internal interface IKukaSimComponentPlatform
{
    IReadOnlyList<KukaSimProcessObservation> FindRunningProcesses(
        string launcherPath,
        string enginePath);

    KukaSimProcessResult Run(
        string launcherPath,
        string enginePath,
        string bootstrapPluginPath,
        string compilerPath,
        string scriptPath,
        string probeAssemblyPath,
        string componentPath,
        string resultPath,
        string layoutPath,
        string bridgeTracePath,
        TimeSpan timeout);
}

internal sealed record KukaSimProcessResult(
    int ExitCode,
    bool ProcessStarted,
    bool TimedOut,
    bool ForcedTerminationUsed,
    bool CleanupVerified,
    long DurationMilliseconds,
    IReadOnlyList<int> OwnedProcessIds,
    string PlatformError);

internal sealed class KukaSimComponentPlatform : IKukaSimComponentPlatform
{
    private static readonly TimeSpan GracefulCloseTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ForcedCloseTimeout = TimeSpan.FromSeconds(30);
    private const uint Th32csSnapProcess = 0x00000002;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    public IReadOnlyList<KukaSimProcessObservation> FindRunningProcesses(
        string launcherPath,
        string enginePath)
    {
        var names = new[]
        {
            Path.GetFileNameWithoutExtension(launcherPath),
            Path.GetFileNameWithoutExtension(enginePath)
        };
        var observations = new List<KukaSimProcessObservation>();
        foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    try
                    {
                        if (process.HasExited)
                        {
                            continue;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        continue;
                    }
                    catch (Exception exception) when (exception is Win32Exception
                        or NotSupportedException)
                    {
                        // Cross-session KUKA.Sim processes can deny the HasExited query
                        // while they are still present. Keep them observable so ownership
                        // and bounded cleanup are decided from fresh PID/path evidence
                        // instead of turning the query failure into a platform failure.
                    }

                    string executablePath;
                    DateTimeOffset? startedAtUtc;
                    try
                    {
                        executablePath = process.MainModule?.FileName ?? string.Empty;
                    }
                    catch (Exception exception) when (exception is Win32Exception
                        or InvalidOperationException
                        or NotSupportedException)
                    {
                        executablePath = string.Empty;
                    }

                    try
                    {
                        startedAtUtc = process.StartTime.ToUniversalTime();
                    }
                    catch (Exception exception) when (exception is Win32Exception
                        or InvalidOperationException
                        or NotSupportedException)
                    {
                        startedAtUtc = null;
                    }

                    observations.Add(new KukaSimProcessObservation
                    {
                        ProcessId = process.Id,
                        ProcessName = process.ProcessName,
                        ExecutablePath = executablePath,
                        StartedAtUtc = startedAtUtc
                    });
                }
            }
        }

        return observations
            .GroupBy(observation => observation.ProcessId)
            .Select(group => group.First())
            .OrderBy(observation => observation.ProcessId)
            .ToList();
    }

    public KukaSimProcessResult Run(
        string launcherPath,
        string enginePath,
        string bootstrapPluginPath,
        string compilerPath,
        string scriptPath,
        string probeAssemblyPath,
        string componentPath,
        string resultPath,
        string layoutPath,
        string bridgeTracePath,
        TimeSpan timeout)
    {
        return RunProbe(
            launcherPath,
            enginePath,
            compilerPath,
            scriptPath,
            probeAssemblyPath,
            "KukaLabComponentLoadSmoke",
            bridgeTracePath,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["KUKA_LAB_RESULT_PATH"] = resultPath,
                ["KUKA_LAB_COMPONENT_PATH"] = componentPath,
                ["KUKA_LAB_LAYOUT_PATH"] = layoutPath
            },
            timeout);
    }

    internal KukaSimProcessResult RunProbe(
        string launcherPath,
        string enginePath,
        string compilerPath,
        string scriptPath,
        string probeAssemblyPath,
        string probeType,
        string bridgeTracePath,
        IReadOnlyDictionary<string, string> environment,
        TimeSpan timeout,
        IReadOnlyList<string>? additionalReferences = null)
    {
        if (!environment.TryGetValue("KUKA_LAB_RESULT_PATH", out var resultPath)
            || string.IsNullOrWhiteSpace(resultPath))
        {
            throw new ArgumentException("A KUKA_LAB_RESULT_PATH environment value is required.", nameof(environment));
        }

        var compileResult = CompileProbe(
            compilerPath,
            enginePath,
            scriptPath,
            probeAssemblyPath,
            additionalReferences);
        if (!compileResult.Succeeded)
        {
            return new KukaSimProcessResult(
                compileResult.ExitCode,
                false,
                false,
                false,
                true,
                compileResult.DurationMilliseconds,
                [],
                compileResult.Error);
        }

        var startInfo = CreateStartInfo(
            launcherPath,
            probeAssemblyPath,
            probeType,
            bridgeTracePath,
            environment);

        using var launcher = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();
        var ownedProcessIds = new HashSet<int>();
        var processStarted = false;
        var timedOut = false;
        var forcedTerminationUsed = false;
        var platformError = string.Empty;
        var launcherExitCode = -1;
        try
        {
            processStarted = launcher.Start();
            if (!processStarted)
            {
                return Result(cleanupVerified: true);
            }

            var launcherProcessId = launcher.Id;
            ownedProcessIds.Add(launcherProcessId);
            var resultObserved = false;
            var quiescentWithoutResultSince = TimeSpan.Zero;
            while (stopwatch.Elapsed < timeout)
            {
                var running = FindOwnedRunningProcesses(
                    launcherPath,
                    enginePath,
                    launcherProcessId,
                    ownedProcessIds);
                foreach (var process in running)
                {
                    ownedProcessIds.Add(process.ProcessId);
                }

                resultObserved |= File.Exists(resultPath);
                var launcherExited = HasExited(launcher);
                if (resultObserved)
                {
                    break;
                }

                if (!resultObserved && launcherExited && running.Count == 0)
                {
                    if (quiescentWithoutResultSince == TimeSpan.Zero)
                    {
                        quiescentWithoutResultSince = stopwatch.Elapsed;
                    }
                    else if (stopwatch.Elapsed - quiescentWithoutResultSince >= TimeSpan.FromSeconds(3))
                    {
                        break;
                    }
                }
                else
                {
                    quiescentWithoutResultSince = TimeSpan.Zero;
                }

                Thread.Sleep(200);
            }

            timedOut = stopwatch.Elapsed >= timeout
                && (!File.Exists(resultPath)
                    || !HasExited(launcher)
                    || FindOwnedRunningProcesses(
                        launcherPath,
                        enginePath,
                        launcherProcessId,
                        ownedProcessIds).Count > 0);

            var remaining = FindOwnedRunningProcesses(
                launcherPath,
                enginePath,
                launcherProcessId,
                ownedProcessIds);
            if (!HasExited(launcher) || remaining.Count > 0)
            {
                RequestGracefulClose(launcher, remaining);
                WaitForNoOwnedProcesses(
                    launcherPath,
                    enginePath,
                    launcherProcessId,
                    ownedProcessIds,
                    GracefulCloseTimeout);
                remaining = FindOwnedRunningProcesses(
                    launcherPath,
                    enginePath,
                    launcherProcessId,
                    ownedProcessIds);
            }

            if (!HasExited(launcher) || remaining.Count > 0)
            {
                forcedTerminationUsed = true;
                ForceTerminateOwnedProcessesUntilGone(
                    launcher,
                    launcherPath,
                    enginePath,
                    launcherProcessId,
                    ownedProcessIds,
                    ForcedCloseTimeout);
            }

            if (TryReadExitCode(launcher, out var observedExitCode))
            {
                launcherExitCode = observedExitCode;
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or Win32Exception)
        {
            platformError = $"{exception.GetType().Name}: {exception.Message}";
            try
            {
                var launcherProcessId = processStarted ? launcher.Id : 0;
                var remaining = launcherProcessId > 0
                    ? FindOwnedRunningProcesses(
                        launcherPath,
                        enginePath,
                        launcherProcessId,
                        ownedProcessIds)
                    : [];
                if (!HasExited(launcher) || remaining.Count > 0)
                {
                    RequestGracefulClose(launcher, remaining);
                    if (launcherProcessId > 0)
                    {
                        WaitForNoOwnedProcesses(
                            launcherPath,
                            enginePath,
                            launcherProcessId,
                            ownedProcessIds,
                            GracefulCloseTimeout);
                        remaining = FindOwnedRunningProcesses(
                            launcherPath,
                            enginePath,
                            launcherProcessId,
                            ownedProcessIds);
                    }
                }

                if (!HasExited(launcher) || remaining.Count > 0)
                {
                    forcedTerminationUsed = true;
                    ForceTerminateOwnedProcessesUntilGone(
                        launcher,
                        launcherPath,
                        enginePath,
                        launcherProcessId,
                        ownedProcessIds,
                        ForcedCloseTimeout);
                    if (launcherProcessId > 0)
                    {
                        WaitForNoOwnedProcesses(
                            launcherPath,
                            enginePath,
                            launcherProcessId,
                            ownedProcessIds,
                            ForcedCloseTimeout);
                    }
                }
            }
            catch (Exception cleanupException) when (cleanupException is InvalidOperationException
                or Win32Exception)
            {
                platformError = $"{platformError}; cleanup: {cleanupException.Message}";
            }
        }

        if (processStarted)
        {
            var lingering = FindTrackedRunningProcesses(launcherPath, enginePath, ownedProcessIds);
            if (lingering.Count > 0)
            {
                RequestGracefulClose(launcher, lingering);
                WaitForNoTrackedProcesses(launcherPath, enginePath, ownedProcessIds, TimeSpan.FromSeconds(5));
                lingering = FindTrackedRunningProcesses(launcherPath, enginePath, ownedProcessIds);
            }
            if (lingering.Count > 0)
            {
                forcedTerminationUsed = true;
                KillOwnedProcesses(launcher, lingering, ownedProcessIds);
                WaitForNoTrackedProcesses(launcherPath, enginePath, ownedProcessIds, ForcedCloseTimeout);
            }
        }

        if (launcherExitCode == -1
            && environment.TryGetValue("KUKA_LAB_TRACE_PATH", out var probeTracePath)
            && TryReadRequestedExitCode(probeTracePath, out var requestedExitCode))
        {
            launcherExitCode = requestedExitCode;
        }

        var cleanupVerified = !processStarted
            || (FindOwnedRunningProcesses(
                    launcherPath,
                    enginePath,
                    launcher.Id,
                    ownedProcessIds).Count == 0
                && FindTrackedRunningProcesses(launcherPath, enginePath, ownedProcessIds).Count == 0);
        return Result(cleanupVerified);

        KukaSimProcessResult Result(bool cleanupVerified)
        {
            stopwatch.Stop();
            return new KukaSimProcessResult(
                launcherExitCode,
                processStarted,
                timedOut,
                forcedTerminationUsed,
                cleanupVerified,
                stopwatch.ElapsedMilliseconds,
                ownedProcessIds.Order().ToList(),
                platformError);
        }
    }

    internal static ProcessStartInfo CreateStartInfo(
        string launcherPath,
        string probeAssemblyPath,
        string componentPath,
        string resultPath,
        string layoutPath,
        string bridgeTracePath)
    {
        return CreateStartInfo(
            launcherPath,
            probeAssemblyPath,
            "KukaLabComponentLoadSmoke",
            bridgeTracePath,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["KUKA_LAB_RESULT_PATH"] = resultPath,
                ["KUKA_LAB_COMPONENT_PATH"] = componentPath,
                ["KUKA_LAB_LAYOUT_PATH"] = layoutPath
            });
    }

    internal static ProcessStartInfo CreateStartInfo(
        string launcherPath,
        string probeAssemblyPath,
        string probeType,
        string bridgeTracePath,
        IReadOnlyDictionary<string, string> environment)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = launcherPath,
            WorkingDirectory = Path.GetDirectoryName(launcherPath) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = false
        };
        startInfo.Environment["KUKA_LAB_PROBE_ASSEMBLY"] = probeAssemblyPath;
        startInfo.Environment["KUKA_LAB_PROBE_TYPE"] = probeType;
        startInfo.Environment["KUKA_LAB_BRIDGE_TRACE_PATH"] = bridgeTracePath;
        foreach (var item in environment)
        {
            startInfo.Environment[item.Key] = item.Value;
        }

        return startInfo;
    }

    private static ProbeCompilationResult CompileProbe(
        string compilerPath,
        string enginePath,
        string sourcePath,
        string outputPath,
        IReadOnlyList<string>? additionalReferences)
    {
        var installRoot = Path.GetDirectoryName(enginePath)
            ?? throw new ArgumentException("KUKA.Sim engine path has no parent directory.", nameof(enginePath));
        var referenceRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Reference Assemblies",
            "Microsoft",
            "Framework",
            ".NETFramework",
            "v4.7.2");
        var startInfo = new ProcessStartInfo
        {
            FileName = compilerPath,
            WorkingDirectory = Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("/nologo");
        startInfo.ArgumentList.Add("/target:library");
        startInfo.ArgumentList.Add($"/out:{outputPath}");
        startInfo.ArgumentList.Add($"/reference:{Path.Combine(referenceRoot, "PresentationFramework.dll")}");
        startInfo.ArgumentList.Add($"/reference:{Path.Combine(referenceRoot, "PresentationCore.dll")}");
        startInfo.ArgumentList.Add($"/reference:{Path.Combine(referenceRoot, "WindowsBase.dll")}");
        startInfo.ArgumentList.Add($"/reference:{Path.Combine(referenceRoot, "System.Xaml.dll")}");
        startInfo.ArgumentList.Add($"/reference:{Path.Combine(installRoot, "Create3D.Shared.dll")}");
        startInfo.ArgumentList.Add($"/reference:{Path.Combine(installRoot, "Caliburn.Micro.dll")}");
        foreach (var additionalReference in additionalReferences ?? [])
        {
            startInfo.ArgumentList.Add($"/reference:{additionalReference}");
        }
        startInfo.ArgumentList.Add(sourcePath);

        var stopwatch = Stopwatch.StartNew();
        using var compiler = new Process { StartInfo = startInfo };
        if (!compiler.Start())
        {
            return new ProbeCompilationResult(false, -1, stopwatch.ElapsedMilliseconds, "The .NET Framework C# compiler did not start.");
        }

        var stdout = compiler.StandardOutput.ReadToEnd();
        var stderr = compiler.StandardError.ReadToEnd();
        compiler.WaitForExit();
        stopwatch.Stop();
        var succeeded = compiler.ExitCode == 0 && File.Exists(outputPath);
        var error = succeeded
            ? string.Empty
            : $"Probe compilation failed with exit code {compiler.ExitCode}: {stdout} {stderr}".Trim();
        return new ProbeCompilationResult(succeeded, compiler.ExitCode, stopwatch.ElapsedMilliseconds, error);
    }

    private sealed record ProbeCompilationResult(
        bool Succeeded,
        int ExitCode,
        long DurationMilliseconds,
        string Error);

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or Win32Exception
            or NotSupportedException)
        {
            try
            {
                using var current = Process.GetProcessById(process.Id);
                return current.HasExited;
            }
            catch (Exception currentException) when (currentException is ArgumentException
                or InvalidOperationException
                or Win32Exception
                or NotSupportedException)
            {
                return true;
            }
        }
    }

    private static bool TryReadExitCode(Process process, out int exitCode)
    {
        exitCode = -1;
        try
        {
            if (!process.HasExited)
            {
                return false;
            }

            exitCode = process.ExitCode;
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or Win32Exception
            or NotSupportedException)
        {
            return false;
        }
    }

    internal static bool TryReadRequestedExitCode(string tracePath, out int exitCode)
    {
        exitCode = -1;
        if (string.IsNullOrWhiteSpace(tracePath) || !File.Exists(tracePath))
        {
            return false;
        }

        try
        {
            foreach (var line in File.ReadLines(tracePath).Reverse())
            {
                const string marker = "EXIT_REQUESTED code=";
                var index = line.LastIndexOf(marker, StringComparison.Ordinal);
                if (index < 0)
                {
                    continue;
                }

                return int.TryParse(
                    line[(index + marker.Length)..].Trim(),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out exitCode);
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
        }

        return false;
    }

    private static void RequestGracefulClose(
        Process launcher,
        IReadOnlyList<KukaSimProcessObservation> processes)
    {
        if (!HasExited(launcher))
        {
            try
            {
                _ = launcher.CloseMainWindow();
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or Win32Exception
                or NotSupportedException)
            {
            }
        }

        foreach (var observation in processes)
        {
            try
            {
                using var process = Process.GetProcessById(observation.ProcessId);
                _ = process.CloseMainWindow();
            }
            catch (Exception exception) when (exception is ArgumentException
                or InvalidOperationException
                or Win32Exception)
            {
            }
        }
    }

    private void KillOwnedProcesses(
        Process launcher,
        IReadOnlyList<KukaSimProcessObservation> processes,
        IReadOnlySet<int> ownedProcessIds)
    {
        if (!HasExited(launcher))
        {
            try
            {
                launcher.Kill(true);
                _ = launcher.WaitForExit(2_000);
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or Win32Exception
                or NotSupportedException)
            {
            }
        }

        foreach (var observation in processes.Where(process => ownedProcessIds.Contains(process.ProcessId)))
        {
            foreach (var process in Process
                .GetProcessesByName(observation.ProcessName)
                .Where(candidate => candidate.Id == observation.ProcessId))
            {
                using (process)
                {
                    try
                    {
                        process.Kill(true);
                        _ = process.WaitForExit(2_000);
                    }
                    catch (Exception exception) when (exception is ArgumentException
                        or InvalidOperationException
                        or Win32Exception)
                    {
                    }
                }
            }
        }
    }

    private void ForceTerminateOwnedProcessesUntilGone(
        Process launcher,
        string launcherPath,
        string enginePath,
        int launcherProcessId,
        IReadOnlySet<int> ownedProcessIds,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            var remaining = FindOwnedRunningProcesses(
                launcherPath,
                enginePath,
                launcherProcessId,
                ownedProcessIds);
            if (HasExited(launcher) && remaining.Count == 0)
            {
                return;
            }

            KillOwnedProcesses(launcher, remaining, ownedProcessIds);
            Thread.Sleep(500);
        }
    }

    private IReadOnlyList<KukaSimProcessObservation> FindOwnedRunningProcesses(
        string launcherPath,
        string enginePath,
        int launcherProcessId,
        IReadOnlySet<int> ownedProcessIds)
    {
        var parents = GetParentProcessIds();
        return FindRunningProcesses(launcherPath, enginePath)
            .Where(process => process.ProcessId == launcherProcessId
                || ownedProcessIds.Contains(process.ProcessId)
                || IsDescendantOf(process.ProcessId, launcherProcessId, parents))
            .ToList();
    }

    internal static bool IsDescendantOf(
        int processId,
        int ancestorProcessId,
        IReadOnlyDictionary<int, int> parentProcessIds)
    {
        if (processId <= 0 || ancestorProcessId <= 0 || processId == ancestorProcessId)
        {
            return false;
        }

        var visited = new HashSet<int>();
        var current = processId;
        while (parentProcessIds.TryGetValue(current, out var parent)
            && parent > 0
            && visited.Add(current))
        {
            if (parent == ancestorProcessId)
            {
                return true;
            }

            current = parent;
        }

        return false;
    }

    private static IReadOnlyDictionary<int, int> GetParentProcessIds()
    {
        var snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
        if (snapshot == InvalidHandleValue)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Process snapshot could not be created.");
        }

        try
        {
            var parents = new Dictionary<int, int>();
            var entry = new ProcessEntry32
            {
                DwSize = (uint)Marshal.SizeOf<ProcessEntry32>()
            };
            if (!Process32First(snapshot, ref entry))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Process snapshot could not be enumerated.");
            }

            do
            {
                parents[checked((int)entry.Th32ProcessId)] = checked((int)entry.Th32ParentProcessId);
                entry.DwSize = (uint)Marshal.SizeOf<ProcessEntry32>();
            }
            while (Process32Next(snapshot, ref entry));

            return parents;
        }
        finally
        {
            _ = CloseHandle(snapshot);
        }
    }

    private void WaitForNoOwnedProcesses(
        string launcherPath,
        string enginePath,
        int launcherProcessId,
        IReadOnlySet<int> ownedProcessIds,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (FindOwnedRunningProcesses(
                    launcherPath,
                    enginePath,
                    launcherProcessId,
                    ownedProcessIds).Count == 0)
            {
                return;
            }

            Thread.Sleep(200);
        }
    }

    private static IReadOnlyList<KukaSimProcessObservation> FindTrackedRunningProcesses(
        string launcherPath,
        string enginePath,
        IReadOnlySet<int> ownedProcessIds)
    {
        var expectedPaths = new[] { Path.GetFullPath(launcherPath), Path.GetFullPath(enginePath) };
        var result = new List<KukaSimProcessObservation>();
        foreach (var processId in ownedProcessIds)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited) continue;
                var executablePath = process.MainModule?.FileName ?? string.Empty;
                if (!expectedPaths.Any(path => string.Equals(path, executablePath, StringComparison.OrdinalIgnoreCase))) continue;
                result.Add(new KukaSimProcessObservation
                {
                    ProcessId = processId,
                    ProcessName = process.ProcessName,
                    ExecutablePath = executablePath,
                    StartedAtUtc = process.StartTime.ToUniversalTime()
                });
            }
            catch (Exception exception) when (exception is ArgumentException
                or InvalidOperationException
                or Win32Exception
                or NotSupportedException)
            {
            }
        }
        return result;
    }

    private static void WaitForNoTrackedProcesses(
        string launcherPath,
        string enginePath,
        IReadOnlySet<int> ownedProcessIds,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (FindTrackedRunningProcesses(launcherPath, enginePath, ownedProcessIds).Count == 0) return;
            Thread.Sleep(200);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint DwSize;
        public uint CntUsage;
        public uint Th32ProcessId;
        public IntPtr Th32DefaultHeapId;
        public uint Th32ModuleId;
        public uint CntThreads;
        public uint Th32ParentProcessId;
        public int PcPriClassBase;
        public uint DwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string SzExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
