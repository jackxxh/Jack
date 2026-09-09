using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class KukaSimIntegratedValidationContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.kukasim-integrated-validation-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const string EmbeddedScriptResource = "KukaLab.Probes.KukaSim.IntegratedKrlValidation.cs";
    public const string EmbeddedScriptSha256 = "8A1C13AA37795D307D723B0B2ADDFFD3C8E24761057B8D4E7F8F6B0322B6E00E";
    public const string ExactKr210R2700ComponentSha256 = KukaSimComponentSmokeContract.ExactKr210R2700ComponentSha256;
    public const string ValidSourceSha256 = "646D9E216E7E974BADD3C88A067E9E6BBEDF429D0233851C6304ABD5261E07F0";
    public const string ValidDataSha256 = "5EEB76FDC7ECB305D464B9E7D2BCD7834EF96C6DD6852DD1B03405782526502F";
    public const string InvalidSourceSha256 = "04975A7FF125363A44BAD5813234ADE8BA30C2751C0A71D7E348293ED310CB05";
    public const string InvalidDataSha256 = "140B95C2EE526284E532373EBFC5701A705F50612F6CBD27726036DF1EA2B431";
    public const string PositiveProgramName = "LAB_MINIMAL";
    public const string NegativeProgramName = "LAB_MISSING_TARGET";

    internal static readonly IReadOnlyList<string> RequiredUnsupportedGaps =
    [
        "The Integrated provider is KUKA.Sim's built-in interpreter, not native KSS or KUKA RCS; native KSS remains separately evidenced by item 9B.",
        "This receipt does not connect OfficeLite/VRC, execute RCS, qualify collision or certify cycle time.",
        "TCP evidence is the exact component mountplate with TOOL 0 / BASE 0 and is not physical Tool/Base/Load calibration.",
        "This virtual result does not authorize or qualify physical robot motion, safety, mastering or payload."
    ];
}

public sealed record KukaSimIntegratedValidationRequest
{
    public required string EnginePath { get; init; }
    public required string LauncherPath { get; init; }
    public required string ScriptStarterPath { get; init; }
    public required string Create3DSharedPath { get; init; }
    public required string ComponentPath { get; init; }
    public required string ValidSourcePath { get; init; }
    public required string ValidDataPath { get; init; }
    public required string InvalidSourcePath { get; init; }
    public required string InvalidDataPath { get; init; }
    public required string EvidenceDirectory { get; init; }
    public int TimeoutSecondsPerAttempt { get; init; } = 180;
    public bool GuiExecutionAuthorized { get; init; }
    public string AuthorizationReference { get; init; } = string.Empty;

    public static KukaSimIntegratedValidationRequest CreateDefault(
        string labRoot,
        string evidenceDirectory,
        bool guiExecutionAuthorized,
        string authorizationReference,
        string? enginePath = null,
        string? componentPath = null,
        int timeoutSecondsPerAttempt = 180)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(labRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceDirectory);
        var root = Path.GetFullPath(labRoot);
        var kukaSim = KukaSimInstallationDiscovery.ResolveFromEngine(enginePath, componentPath);
        return new KukaSimIntegratedValidationRequest
        {
            EnginePath = kukaSim.EnginePath,
            LauncherPath = kukaSim.LauncherPath,
            ScriptStarterPath = kukaSim.ScriptStarterPath,
            Create3DSharedPath = kukaSim.Create3DSharedPath,
            ComponentPath = kukaSim.ComponentPath,
            ValidSourcePath = Path.Combine(root, "fixtures", "minimal-ptp-lin-valid", "controller-files", "LAB_MINIMAL.src"),
            ValidDataPath = Path.Combine(root, "fixtures", "minimal-ptp-lin-valid", "controller-files", "LAB_MINIMAL.dat"),
            InvalidSourcePath = Path.Combine(root, "fixtures", "minimal-missing-target-invalid", "controller-files", "LAB_MISSING_TARGET.src"),
            InvalidDataPath = Path.Combine(root, "fixtures", "minimal-missing-target-invalid", "controller-files", "LAB_MISSING_TARGET.dat"),
            EvidenceDirectory = Path.GetFullPath(evidenceDirectory),
            TimeoutSecondsPerAttempt = timeoutSecondsPerAttempt,
            GuiExecutionAuthorized = guiExecutionAuthorized,
            AuthorizationReference = authorizationReference
        };
    }
}

public sealed record KukaSimIntegratedValidationReceipt
{
    public string SchemaIdentity { get; init; } = KukaSimIntegratedValidationContract.ReceiptSchemaIdentity;
    public int SchemaVersion { get; init; } = KukaSimIntegratedValidationContract.ReceiptSchemaVersion;
    public required KukaSimIntegratedValidationPayload Payload { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record KukaSimIntegratedValidationPayload
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
    public string ValidationStatus { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string EnginePath { get; init; } = string.Empty;
    public string ComponentPath { get; init; } = string.Empty;
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
    public required KukaSimIntegratedAttempt Positive { get; init; }
    public required KukaSimIntegratedAttempt Negative { get; init; }
    public List<EnvironmentCheck> Checks { get; init; } = [];
    public List<EnvironmentFileObservation> Files { get; init; } = [];
    public List<string> SideEffects { get; init; } = [];
    public bool EnvironmentReusable { get; init; }
    public List<string> UnsupportedGaps { get; init; } = [];
}

public sealed record KukaSimIntegratedAttempt
{
    public string Role { get; init; } = string.Empty;
    public string ProgramName { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;
    public string DataPath { get; init; } = string.Empty;
    public string RawResultPath { get; init; } = string.Empty;
    public string TracePath { get; init; } = string.Empty;
    public KukaSimCommandObservation Command { get; init; } = new();
    public KukaSimIntegratedRawResult? Result { get; init; }
    public string ResultCanonicalSha256 { get; init; } = string.Empty;
}

public sealed record KukaSimIntegratedRawResult
{
    public bool ApplicationInitialized { get; init; }
    public bool ApplicationReady { get; init; }
    public bool ValidLicenseExists { get; init; }
    public string ComponentName { get; init; } = string.Empty;
    public bool IsComponent { get; init; }
    public int LoadedObjectCount { get; init; }
    public string TcpNodeName { get; init; } = string.Empty;
    public string MotionExecution { get; init; } = string.Empty;
    public string ProgramName { get; init; } = string.Empty;
    public string ProgramMode { get; init; } = string.Empty;
    public bool TraceDataRecording { get; init; }
    public string SynchronizationBefore { get; init; } = string.Empty;
    public string SynchronizationAfter { get; init; } = string.Empty;
    public string SynchronizationMode { get; init; } = string.Empty;
    public int StatementTreeCount { get; init; }
    public string InterpreterModeAfter { get; init; } = string.Empty;
    public string InterpreterStateAfter { get; init; } = string.Empty;
    public bool SawNonIdleInterpreterState { get; init; }
    public bool SimulationRunningAfter { get; init; }
    public bool SimulationPausedAfter { get; init; }
    public bool SimulationAtStartAfter { get; init; }
    public bool ProgramFinished { get; init; }
    public bool SuccessMarker { get; init; }
    public List<KukaSimIntegratedMotion> CompletedMotions { get; init; } = [];
    public List<KukaSimIntegratedSample> Samples { get; init; } = [];
    public List<KukaSimIntegratedMessage> Messages { get; init; } = [];
    public string ErrorType { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed record KukaSimIntegratedMotion
{
    public int Sequence { get; init; }
    public double ElapsedMilliseconds { get; init; }
    public string MotionType { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}

public sealed record KukaSimIntegratedSample
{
    public int Sequence { get; init; }
    public double ElapsedMilliseconds { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string InterpreterState { get; init; } = string.Empty;
    public string InterpreterMode { get; init; } = string.Empty;
    public List<double> Axes { get; init; } = [];
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
}

public sealed record KukaSimIntegratedMessage
{
    public string Id { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
}

public sealed record KukaSimIntegratedValidationOutcome(KukaSimIntegratedValidationReceipt Receipt)
{
    public int ExitCode => Receipt.Payload.TerminalClassification switch
    {
        EnvironmentTerminalClassification.Ready => 0,
        EnvironmentTerminalClassification.Failed => 2,
        _ => 3
    };
}

internal static class KukaSimIntegratedRawParser
{
    private static readonly Regex DetailRegex = new("(?<key>[^=;]+)=(?<value>[^;]*)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static KukaSimIntegratedRawResult Parse(string path)
    {
        var applicationInitialized = false;
        var applicationReady = false;
        var validLicense = false;
        var componentName = string.Empty;
        var isComponent = false;
        var loadedCount = 0;
        var tcpNode = string.Empty;
        var motionExecution = string.Empty;
        var programName = string.Empty;
        var programMode = string.Empty;
        var trace = false;
        var synchronizationBefore = string.Empty;
        var synchronizationAfter = string.Empty;
        var synchronizationMode = string.Empty;
        var statementTreeCount = 0;
        var interpreterModeAfter = string.Empty;
        var interpreterStateAfter = string.Empty;
        var sawNonIdle = false;
        var simulationRunningAfter = false;
        var simulationPausedAfter = false;
        var simulationAtStartAfter = false;
        var programFinished = false;
        var success = false;
        var errorType = string.Empty;
        var errorMessage = string.Empty;
        var motions = new List<KukaSimIntegratedMotion>();
        var samples = new List<KukaSimIntegratedSample>();
        var messages = new List<KukaSimIntegratedMessage>();

        foreach (var line in File.ReadLines(path, new UTF8Encoding(false, true)))
        {
            var fields = line.Split('\t');
            if (fields.Length == 0) continue;
            switch (fields[0])
            {
                case "APPLICATION" when fields.Length >= 4:
                    applicationInitialized = ParseBool(fields[1]);
                    applicationReady = ParseBool(fields[2]);
                    validLicense = ParseBool(fields[3]);
                    break;
                case "COMPONENT" when fields.Length >= 5:
                    componentName = fields[1];
                    isComponent = ParseBool(fields[2]);
                    loadedCount = ParseInt(fields[3]);
                    tcpNode = fields[4];
                    break;
                case "CONFIG_AFTER" when fields.Length >= 5:
                    motionExecution = fields[1];
                    programName = fields[2];
                    programMode = fields[3];
                    trace = ParseBool(fields[4]);
                    break;
                case "SYNCHRONIZATION" when fields.Length >= 4:
                    synchronizationBefore = fields[1];
                    synchronizationAfter = fields[2];
                    synchronizationMode = fields[3];
                    break;
                case "STATEMENT_TREE_COUNT" when fields.Length >= 2:
                    statementTreeCount = ParseInt(fields[1]);
                    break;
                case "INTERPRETER_AFTER" when fields.Length >= 4:
                    interpreterModeAfter = fields[1];
                    interpreterStateAfter = fields[2];
                    sawNonIdle = ParseKeyBool(fields[3], "SawNonIdle");
                    break;
                case "SIMULATION_AFTER" when fields.Length >= 4:
                    simulationRunningAfter = ParseKeyBool(fields[1], "Running");
                    simulationPausedAfter = ParseKeyBool(fields[2], "Paused");
                    simulationAtStartAfter = ParseKeyBool(fields[3], "AtStart");
                    break;
                case "PROGRAM_FINISHED" when fields.Length >= 2:
                    if (bool.TryParse(fields[1], out var finished)) programFinished = finished;
                    break;
                case "STATEMENT_EXECUTED" when fields.Length >= 6 && fields[2].EndsWith(".MotionStatement", StringComparison.Ordinal):
                    var details = ParseDetails(fields[5]);
                    motions.Add(new KukaSimIntegratedMotion
                    {
                        Sequence = motions.Count + 1,
                        ElapsedMilliseconds = ParseDouble(fields[1]),
                        MotionType = GetDetail(details, "MotionIdentifier", GetDetail(details, "MotionType", string.Empty)).ToUpperInvariant(),
                        Target = GetDetail(details, "CurrentPointExpression", string.Empty),
                        DisplayName = GetDetail(details, "DisplayName", string.Empty)
                    });
                    break;
                case "SAMPLE" when fields.Length >= 6:
                    samples.Add(ParseSample(fields, samples.Count + 1));
                    break;
                case "MESSAGE" when fields.Length >= 4:
                    messages.Add(new KukaSimIntegratedMessage
                    {
                        Id = fields[1],
                        Severity = fields[2],
                        Text = string.Join(" ", fields.Skip(3))
                    });
                    break;
                case "SUCCESS" when fields.Length >= 2:
                    success = ParseBool(fields[1]);
                    break;
                case "ERROR" when fields.Length >= 3:
                    errorType = fields[1];
                    errorMessage = fields[2];
                    break;
            }
        }

        return new KukaSimIntegratedRawResult
        {
            ApplicationInitialized = applicationInitialized,
            ApplicationReady = applicationReady,
            ValidLicenseExists = validLicense,
            ComponentName = componentName,
            IsComponent = isComponent,
            LoadedObjectCount = loadedCount,
            TcpNodeName = tcpNode,
            MotionExecution = motionExecution,
            ProgramName = programName,
            ProgramMode = programMode,
            TraceDataRecording = trace,
            SynchronizationBefore = synchronizationBefore,
            SynchronizationAfter = synchronizationAfter,
            SynchronizationMode = synchronizationMode,
            StatementTreeCount = statementTreeCount,
            InterpreterModeAfter = interpreterModeAfter,
            InterpreterStateAfter = interpreterStateAfter,
            SawNonIdleInterpreterState = sawNonIdle,
            SimulationRunningAfter = simulationRunningAfter,
            SimulationPausedAfter = simulationPausedAfter,
            SimulationAtStartAfter = simulationAtStartAfter,
            ProgramFinished = programFinished,
            SuccessMarker = success,
            CompletedMotions = motions,
            Samples = samples,
            Messages = messages,
            ErrorType = errorType,
            ErrorMessage = errorMessage
        };
    }

    private static KukaSimIntegratedSample ParseSample(string[] fields, int sequence)
    {
        var state = ParseKey(fields[3], "STATE");
        var mode = ParseKey(fields[3], "MODE");
        var axesText = StripPrefix(fields[4], "JOINTS=");
        var axes = new List<double>();
        if (!string.Equals(axesText, "Unavailable", StringComparison.OrdinalIgnoreCase))
        {
            for (var index = 1; index <= 6; index++)
            {
                axes.Add(ParseNamedDouble(axesText, "A" + index));
            }
        }
        var tcp = StripPrefix(fields[5], "TCP=");
        return new KukaSimIntegratedSample
        {
            Sequence = sequence,
            ElapsedMilliseconds = ParseDouble(fields[1]),
            Reason = fields[2],
            InterpreterState = state,
            InterpreterMode = mode,
            Axes = axes,
            X = ParseNamedDouble(tcp, "X"),
            Y = ParseNamedDouble(tcp, "Y"),
            Z = ParseNamedDouble(tcp, "Z")
        };
    }

    private static Dictionary<string, string> ParseDetails(string text) =>
        DetailRegex.Matches(text.StartsWith("DETAILS=", StringComparison.Ordinal) ? text[8..] : text)
            .Cast<Match>()
            .ToDictionary(match => match.Groups["key"].Value, match => match.Groups["value"].Value, StringComparer.Ordinal);

    private static string GetDetail(IReadOnlyDictionary<string, string> details, string key, string fallback) =>
        details.TryGetValue(key, out var value) ? value : fallback;

    private static bool ParseKeyBool(string value, string key) => ParseBool(ParseKey(value, key));

    private static string StripPrefix(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.Ordinal) ? value[prefix.Length..] : string.Empty;

    private static string ParseKey(string value, string key)
    {
        foreach (var pair in value.Split(','))
        {
            var separator = pair.IndexOf('=');
            if (separator > 0 && string.Equals(pair[..separator], key, StringComparison.Ordinal))
            {
                return pair[(separator + 1)..];
            }
        }
        return string.Empty;
    }

    private static double ParseNamedDouble(string value, string key) =>
        ParseDouble(ParseKey(value, key));

    private static bool ParseBool(string value) =>
        bool.TryParse(value, out var result) && result;

    private static int ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static double ParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : double.NaN;
}

public sealed class KukaSimIntegratedValidationRunner
{
    private readonly IKukaSimIntegratedPlatform _platform;
    private readonly TimeProvider _timeProvider;

    public KukaSimIntegratedValidationRunner()
        : this(new KukaSimIntegratedPlatform(), TimeProvider.System)
    {
    }

    internal KukaSimIntegratedValidationRunner(IKukaSimIntegratedPlatform platform, TimeProvider timeProvider)
    {
        _platform = platform;
        _timeProvider = timeProvider;
    }

    public KukaSimIntegratedValidationOutcome Run(KukaSimIntegratedValidationRequest request, string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateIdentifier(attemptId, nameof(attemptId));
        if (request.TimeoutSecondsPerAttempt is < 10 or > 600)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Timeout must be from 10 through 600 seconds per attempt.");
        }
        if (request.GuiExecutionAuthorized) ValidateIdentifier(request.AuthorizationReference, nameof(request.AuthorizationReference));

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var checks = new List<EnvironmentCheck>();
        var files = new List<EnvironmentFileObservation>();
        var sideEffects = new List<string>();
        var preExisting = new List<KukaSimProcessObservation>();
        var scriptSha = string.Empty;
        var positive = EmptyAttempt("Positive", KukaSimIntegratedValidationContract.PositiveProgramName, request.ValidSourcePath, request.ValidDataPath);
        var negative = EmptyAttempt("Negative", KukaSimIntegratedValidationContract.NegativeProgramName, request.InvalidSourcePath, request.InvalidDataPath);
        var environmentReusable = true;
        var enginePath = Path.GetFullPath(request.EnginePath);
        var launcherPath = Path.GetFullPath(request.LauncherPath);
        var componentPath = Path.GetFullPath(request.ComponentPath);
        var evidenceDirectory = Path.GetFullPath(request.EvidenceDirectory);
        var installRoot = Path.GetDirectoryName(enginePath)
            ?? throw new ArgumentException("KUKA.Sim engine path has no parent directory.", nameof(request));

        if (!OperatingSystem.IsWindows())
        {
            checks.Add(Blocked("host-os", "KUKA.Sim Integrated validation requires Windows."));
            return Complete();
        }

        var required = new[]
        {
            ("kukasim-engine", enginePath, string.Empty),
            ("kukasim-launcher", launcherPath, string.Empty),
            ("kukasim-script-starter", Path.GetFullPath(request.ScriptStarterPath), string.Empty),
            ("kukasim-create3d-api", Path.GetFullPath(request.Create3DSharedPath), string.Empty),
            ("kukasim-visualstudio-interop", Path.Combine(installRoot, "Microsoft.VisualStudio.Interop.dll"), string.Empty),
            ("kukasim-codedom-provider", Path.Combine(installRoot, "Microsoft.CodeDom.Providers.DotNetCompilerPlatform.dll"), string.Empty),
            ("kukasim-roslyn-compiler", Path.Combine(installRoot, "bin", "roslyn", "csc.exe"), string.Empty),
            ("exact-c01-component", componentPath, KukaSimIntegratedValidationContract.ExactKr210R2700ComponentSha256),
            ("valid-src", Path.GetFullPath(request.ValidSourcePath), KukaSimIntegratedValidationContract.ValidSourceSha256),
            ("valid-dat", Path.GetFullPath(request.ValidDataPath), KukaSimIntegratedValidationContract.ValidDataSha256),
            ("invalid-src", Path.GetFullPath(request.InvalidSourcePath), KukaSimIntegratedValidationContract.InvalidSourceSha256),
            ("invalid-dat", Path.GetFullPath(request.InvalidDataPath), KukaSimIntegratedValidationContract.InvalidDataSha256)
        };
        foreach (var item in required)
        {
            if (!ObserveRequiredFile(item.Item1, item.Item2, item.Item3, files, checks)) return Complete();
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
            checks.Add(Failed("evidence-directory", exception.Message));
            return Complete();
        }

        var scriptPath = Path.Combine(evidenceDirectory, $"{attemptId}.integrated-krl-validation.cs");
        try
        {
            var scriptBytes = ReadEmbeddedScript();
            scriptSha = Convert.ToHexString(SHA256.HashData(scriptBytes));
            if (!string.Equals(scriptSha, KukaSimIntegratedValidationContract.EmbeddedScriptSha256, StringComparison.OrdinalIgnoreCase))
            {
                checks.Add(Failed("pinned-script", "Embedded Integrated validation script hash does not match the pinned contract."));
                return Complete();
            }
            WriteNew(scriptPath, scriptBytes);
            files.Add(ObserveExistingFile("kukasim-integrated-script", scriptPath));
            sideEffects.Add($"CreateProbeScript:{scriptPath}");
            checks.Add(Passed("pinned-script", "The hash-pinned script was materialized create-new; callers cannot supply script text."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            checks.Add(Failed("pinned-script", exception.Message));
            return Complete();
        }

        positive = RunAttempt("Positive", KukaSimIntegratedValidationContract.PositiveProgramName, request.ValidSourcePath, request.ValidDataPath, "positive");
        if (!positive.Command.CleanupVerified || positive.Command.ForcedTerminationUsed) environmentReusable = false;
        if (!WaitForKukaSimProcessQuiescence(launcherPath, enginePath))
        {
            checks.Add(Failed("positive-process-cleanup", "KUKA.Sim remained running after the bounded positive-attempt cleanup wait."));
            environmentReusable = false;
            return Complete();
        }
        checks.Add(positive.Command.CleanupVerified && !positive.Command.ForcedTerminationUsed
            ? Passed("positive-process-cleanup", "Positive KUKA.Sim process exited without forced termination.")
            : Failed("positive-process-cleanup", "Positive KUKA.Sim cleanup was not deterministic."));

        negative = RunAttempt("Negative", KukaSimIntegratedValidationContract.NegativeProgramName, request.InvalidSourcePath, request.InvalidDataPath, "negative");
        if (!negative.Command.CleanupVerified || negative.Command.ForcedTerminationUsed) environmentReusable = false;
        if (!WaitForKukaSimProcessQuiescence(launcherPath, enginePath))
        {
            checks.Add(Failed("negative-process-cleanup", "KUKA.Sim remained running after the bounded negative-attempt cleanup wait."));
            environmentReusable = false;
        }
        else
        {
            checks.Add(negative.Command.CleanupVerified && !negative.Command.ForcedTerminationUsed
                ? Passed("negative-process-cleanup", "Negative KUKA.Sim process exited without forced termination.")
                : Failed("negative-process-cleanup", "Negative KUKA.Sim cleanup was not deterministic."));
        }

        var positiveErrors = ValidatePositive(positive);
        checks.Add(positiveErrors.Count == 0
            ? Passed("positive-integrated-run", "Go-mode Integrated execution completed A_HOME -> P_START -> P_END with exact target/type order and axis/TCP evidence.")
            : Failed("positive-integrated-run", string.Join("; ", positiveErrors)));
        var negativeErrors = ValidateNegative(negative);
        checks.Add(negativeErrors.Count == 0
            ? Passed("negative-integrated-rejection", "The unresolved P_MISSING fixture stopped before any completed motion and retained the home axes/TCP.")
            : Failed("negative-integrated-rejection", string.Join("; ", negativeErrors)));
        return Complete();

        bool WaitForKukaSimProcessQuiescence(string launcher, string engine)
        {
            var wait = Stopwatch.StartNew();
            do
            {
                if (_platform.FindRunningProcesses(launcher, engine).Count == 0) return true;
                Thread.Sleep(100);
            }
            while (wait.Elapsed < TimeSpan.FromSeconds(10));

            return _platform.FindRunningProcesses(launcher, engine).Count == 0;
        }

        KukaSimIntegratedAttempt RunAttempt(string role, string programName, string sourcePath, string dataPath, string suffix)
        {
            var resultPath = Path.Combine(evidenceDirectory, $"{attemptId}.{suffix}.result.tsv");
            var tracePath = Path.Combine(evidenceDirectory, $"{attemptId}.{suffix}.trace.tsv");
            KukaSimProcessResult process;
            try
            {
                process = _platform.Run(new KukaSimIntegratedProcessRequest(
                    launcherPath,
                    enginePath,
                    scriptPath,
                    componentPath,
                    Path.GetFullPath(sourcePath),
                    Path.GetFullPath(dataPath),
                    programName,
                    resultPath,
                    tracePath,
                    TimeSpan.FromSeconds(request.TimeoutSecondsPerAttempt)));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
            {
                process = new KukaSimProcessResult(-1, false, false, false, true, 0, [], $"{exception.GetType().Name}: {exception.Message}");
            }
            if (process.ProcessStarted)
            {
                sideEffects.Add($"StartOwnedKukaSim:{role}:{enginePath}");
                sideEffects.Add($"RequestOwnedKukaSimExit:{role}:PinnedInProcessScript");
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
                files.Add(ObserveExistingFile($"kukasim-{suffix}-result", resultPath));
                try { result = KukaSimIntegratedRawParser.Parse(resultPath); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException or FormatException)
                {
                    checks.Add(Failed($"{suffix}-result-parse", exception.Message));
                }
            }
            else
            {
                checks.Add(process.TimedOut
                    ? Blocked($"{suffix}-result-parse", "No result was produced before timeout.")
                    : Failed($"{suffix}-result-parse", "KUKA.Sim exited without the required result."));
            }
            if (File.Exists(tracePath)) files.Add(ObserveExistingFile($"kukasim-{suffix}-trace", tracePath));
            return new KukaSimIntegratedAttempt
            {
                Role = role,
                ProgramName = programName,
                SourcePath = Path.GetFullPath(sourcePath),
                DataPath = Path.GetFullPath(dataPath),
                RawResultPath = resultPath,
                TracePath = tracePath,
                Command = command,
                Result = result,
                ResultCanonicalSha256 = result is null ? string.Empty : ReceiptSerialization.ComputeCanonicalSha256(result)
            };
        }

        KukaSimIntegratedValidationOutcome Complete()
        {
            stopwatch.Stop();
            var terminal = checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
                ? EnvironmentTerminalClassification.Failed
                : checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked)
                    ? EnvironmentTerminalClassification.Blocked
                    : EnvironmentTerminalClassification.Ready;
            var accepted = terminal == EnvironmentTerminalClassification.Ready
                && ValidatePositive(positive).Count == 0
                && ValidateNegative(negative).Count == 0
                && environmentReusable;
            var payload = new KukaSimIntegratedValidationPayload
            {
                ReceiptId = $"kukasim-integrated-validation-{attemptId}",
                AttemptId = attemptId,
                CoreAssemblySha256 = ComputeFileSha256(typeof(KukaSimIntegratedValidationRunner).Assembly.Location),
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
                ValidationStatus = accepted ? "ExactC01IntegratedValidated" : "NotValidated",
                Provider = "Integrated",
                EnginePath = enginePath,
                ComponentPath = componentPath,
                EvidenceDirectory = evidenceDirectory,
                EmbeddedScriptSha256 = scriptSha,
                GuiExecutionAuthorized = request.GuiExecutionAuthorized,
                AuthorizationReference = request.AuthorizationReference,
                SimulationPerformed = accepted,
                OfficeLiteConnectionPerformed = false,
                RcsProviderUsed = false,
                PhysicalControllerContacted = false,
                NativeKssStatus = NativeKssStatus.NotRun,
                PreExistingProcesses = preExisting,
                Positive = positive,
                Negative = negative,
                Checks = checks,
                Files = files,
                SideEffects = sideEffects,
                EnvironmentReusable = environmentReusable,
                UnsupportedGaps = KukaSimIntegratedValidationContract.RequiredUnsupportedGaps.ToList()
            };
            return new KukaSimIntegratedValidationOutcome(new KukaSimIntegratedValidationReceipt
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
            });
        }
    }

    internal static List<string> ValidatePositive(KukaSimIntegratedAttempt attempt)
    {
        var errors = ValidateCommand(attempt, expectedExitCode: 0);
        var result = attempt.Result;
        if (result is null) return errors.Append("positive result is missing").ToList();
        if (!result.ApplicationInitialized || !result.ApplicationReady || !result.ValidLicenseExists) errors.Add("KUKA.Sim readiness evidence is incomplete");
        if (!string.Equals(result.ComponentName, "KR 210 R2700-2 C01", StringComparison.Ordinal)
            || !result.IsComponent || result.LoadedObjectCount < 1 || result.TcpNodeName != "mountplate") errors.Add("exact C01 component/mountplate evidence is invalid");
        if (result.MotionExecution != "Integrated" || result.ProgramName != KukaSimIntegratedValidationContract.PositiveProgramName
            || result.ProgramMode != "Go" || !result.TraceDataRecording) errors.Add("positive configuration is not Integrated/LAB_MINIMAL/Go/trace");
        if (result.SynchronizationAfter != "Synchronized" || result.SynchronizationMode != "Automatic") errors.Add("positive program did not synchronize automatically");
        if (result.InterpreterModeAfter != "Go"
            || (result.InterpreterStateAfter != "End" && result.InterpreterStateAfter != "Active")
            || !result.SawNonIdleInterpreterState
            || result.SimulationRunningAfter || !result.ProgramFinished || !result.SuccessMarker)
        {
            errors.Add("positive interpreter did not finish in Go with the simulation stopped");
        }
        var expectedTargets = new[] { "A_HOME", "P_START", "P_END" };
        var expectedTypes = new[] { "PTP", "PTP", "LIN" };
        if (result.CompletedMotions.Count != 3
            || !result.CompletedMotions.Select(motion => motion.Target).SequenceEqual(expectedTargets, StringComparer.Ordinal)
            || !result.CompletedMotions.Select(motion => motion.MotionType).SequenceEqual(expectedTypes, StringComparer.Ordinal))
        {
            errors.Add("runtime motion order must be PTP A_HOME, PTP P_START, LIN P_END");
        }
        var finiteSamples = result.Samples.Where(sample => sample.Axes.Count == 6 && IsFinite(sample.X, sample.Y, sample.Z)).ToList();
        if (finiteSamples.Count < 3) errors.Add("positive axis/TCP sample set is incomplete");
        if (!finiteSamples.Any(sample => Distance(sample, 1900, -100, 1200) <= 1.0)) errors.Add("P_START TCP was not observed within 1 mm");
        if (finiteSamples.Count > 0 && Distance(finiteSamples[^1], 1900, 100, 1200) > 1.0) errors.Add("final P_END TCP is outside 1 mm");
        return errors;
    }

    internal static List<string> ValidateNegative(KukaSimIntegratedAttempt attempt)
    {
        var errors = ValidateCommand(attempt, expectedExitCode: 4);
        var result = attempt.Result;
        if (result is null) return errors.Append("negative result is missing").ToList();
        if (!result.ApplicationInitialized || !result.ApplicationReady || !result.ValidLicenseExists) errors.Add("negative KUKA.Sim readiness evidence is incomplete");
        if (result.MotionExecution != "Integrated" || result.ProgramName != KukaSimIntegratedValidationContract.NegativeProgramName
            || result.ProgramMode != "Go") errors.Add("negative configuration is not Integrated/LAB_MISSING_TARGET/Go");
        if (result.InterpreterModeAfter != "Go" || result.InterpreterStateAfter != "Stop" || !result.SawNonIdleInterpreterState
            || result.SimulationRunningAfter || result.ProgramFinished || result.SuccessMarker) errors.Add("negative interpreter did not stop without completion");
        if (result.CompletedMotions.Count != 0) errors.Add("negative program completed one or more motions");
        if (!result.Messages.Any(message => message.Text.Contains("P_MISSING", StringComparison.Ordinal)
                && message.Text.Contains("not defined", StringComparison.OrdinalIgnoreCase))) errors.Add("negative result lacks the unresolved P_MISSING message");
        var finiteSamples = result.Samples.Where(sample => IsFinite(sample.X, sample.Y, sample.Z)).ToList();
        if (finiteSamples.Count == 0 || finiteSamples.Any(sample => Distance(sample, 1765, 0, 1910) > 0.1)) errors.Add("negative TCP moved away from the exact home mountplate position");
        if (finiteSamples.Any(sample => sample.Axes.Count == 6 && !AxesNear(sample.Axes, [0, -90, 90, 0, 0, 0], 0.01))) errors.Add("negative axes moved away from A_HOME");
        return errors;
    }

    private static List<string> ValidateCommand(KukaSimIntegratedAttempt attempt, int expectedExitCode)
    {
        var errors = new List<string>();
        if (!attempt.Command.ProcessStarted || attempt.Command.ExitCode != expectedExitCode || attempt.Command.TimedOut
            || attempt.Command.ForcedTerminationUsed || !attempt.Command.CleanupVerified || !string.IsNullOrEmpty(attempt.Command.PlatformError))
        {
            errors.Add(
                $"{attempt.Role} process command/cleanup evidence is invalid "
                + $"(started={attempt.Command.ProcessStarted}, exit={attempt.Command.ExitCode}, expectedExit={expectedExitCode}, "
                + $"timedOut={attempt.Command.TimedOut}, forced={attempt.Command.ForcedTerminationUsed}, "
                + $"cleanup={attempt.Command.CleanupVerified}, platformError={attempt.Command.PlatformError})");
        }
        return errors;
    }

    private static double Distance(KukaSimIntegratedSample sample, double x, double y, double z) =>
        Math.Sqrt(Math.Pow(sample.X - x, 2) + Math.Pow(sample.Y - y, 2) + Math.Pow(sample.Z - z, 2));

    private static bool IsFinite(params double[] values) => values.All(double.IsFinite);

    private static bool AxesNear(IReadOnlyList<double> actual, IReadOnlyList<double> expected, double tolerance) =>
        actual.Count == expected.Count && actual.Zip(expected).All(pair => Math.Abs(pair.First - pair.Second) <= tolerance);

    private static KukaSimIntegratedAttempt EmptyAttempt(string role, string programName, string source, string data) => new()
    {
        Role = role,
        ProgramName = programName,
        SourcePath = Path.GetFullPath(source),
        DataPath = Path.GetFullPath(data)
    };

    private static byte[] ReadEmbeddedScript()
    {
        using var stream = typeof(KukaSimIntegratedValidationRunner).Assembly.GetManifestResourceStream(
            KukaSimIntegratedValidationContract.EmbeddedScriptResource)
            ?? throw new InvalidOperationException("Pinned KUKA.Sim Integrated validation resource is missing.");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true);
        var canonical = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return Encoding.UTF8.GetBytes(canonical);
    }

    private static bool ObserveRequiredFile(string id, string path, string expectedSha256, List<EnvironmentFileObservation> files, List<EnvironmentCheck> checks)
    {
        if (!File.Exists(path))
        {
            files.Add(new EnvironmentFileObservation { Id = id, Path = path, Exists = false });
            checks.Add(Blocked(id, $"Required file is missing: {path}"));
            return false;
        }
        try
        {
            var observation = ObserveExistingFile(id, path);
            files.Add(observation);
            if (!string.IsNullOrEmpty(expectedSha256) && !string.Equals(observation.Sha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                checks.Add(Failed(id, "Required file does not match the pinned SHA-256."));
                return false;
            }
            checks.Add(Passed(id, string.IsNullOrEmpty(expectedSha256) ? "Required file exists and was hashed." : "Required file matches the pinned SHA-256."));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            checks.Add(Failed(id, exception.Message));
            return false;
        }
    }

    internal static EnvironmentFileObservation ObserveExistingFile(string id, string path)
    {
        var info = new FileInfo(path);
        return new EnvironmentFileObservation
        {
            Id = id,
            Path = Path.GetFullPath(path),
            Exists = true,
            Bytes = info.Length,
            Sha256 = ComputeFileSha256(path)
        };
    }

    internal static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
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
}

internal sealed record KukaSimIntegratedProcessRequest(
    string LauncherPath,
    string EnginePath,
    string ScriptPath,
    string ComponentPath,
    string SourcePath,
    string DataPath,
    string ProgramName,
    string ResultPath,
    string TracePath,
    TimeSpan Timeout);

internal interface IKukaSimIntegratedPlatform
{
    IReadOnlyList<KukaSimProcessObservation> FindRunningProcesses(string launcherPath, string enginePath);
    KukaSimProcessResult Run(KukaSimIntegratedProcessRequest request);
}

internal sealed class KukaSimIntegratedPlatform : IKukaSimIntegratedPlatform
{
    private readonly KukaSimComponentPlatform _processReader = new();

    public IReadOnlyList<KukaSimProcessObservation> FindRunningProcesses(string launcherPath, string enginePath) =>
        _processReader.FindRunningProcesses(launcherPath, enginePath);

    public KukaSimProcessResult Run(KukaSimIntegratedProcessRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.EnginePath,
            WorkingDirectory = Path.GetDirectoryName(request.EnginePath) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = false
        };
        startInfo.ArgumentList.Add($"/csscript:{request.ScriptPath}");
        startInfo.Environment["KUKA_LAB_RESULT_PATH"] = request.ResultPath;
        startInfo.Environment["KUKA_LAB_TRACE_PATH"] = request.TracePath;
        startInfo.Environment["KUKA_LAB_COMPONENT_PATH"] = request.ComponentPath;
        startInfo.Environment["KUKA_LAB_SOURCE_PATH"] = request.SourcePath;
        startInfo.Environment["KUKA_LAB_DATA_PATH"] = request.DataPath;
        startInfo.Environment["KUKA_LAB_PROGRAM_NAME"] = request.ProgramName;

        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();
        var started = false;
        var timedOut = false;
        var forced = false;
        var exitCode = -1;
        var error = string.Empty;
        var ids = new HashSet<int>();
        try
        {
            started = process.Start();
            if (!started) return Result(true);
            ids.Add(process.Id);
            while (stopwatch.Elapsed < request.Timeout && !process.HasExited) Thread.Sleep(100);
            timedOut = !process.HasExited;
            if (!process.HasExited)
            {
                _ = process.CloseMainWindow();
                if (!process.WaitForExit(10_000))
                {
                    forced = true;
                    process.Kill(true);
                    _ = process.WaitForExit(10_000);
                }
            }
            if (process.HasExited) exitCode = process.ExitCode;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            error = $"{exception.GetType().Name}: {exception.Message}";
            try
            {
                if (started && !process.HasExited)
                {
                    _ = process.CloseMainWindow();
                    if (!process.WaitForExit(10_000))
                    {
                        forced = true;
                        process.Kill(true);
                        _ = process.WaitForExit(10_000);
                    }
                }
            }
            catch (Exception cleanup) when (cleanup is InvalidOperationException or Win32Exception)
            {
                error += $"; cleanup: {cleanup.Message}";
            }
        }

        var remaining = WaitForProcessQuiescence(TimeSpan.FromSeconds(45));
        foreach (var observation in remaining) ids.Add(observation.ProcessId);
        if (remaining.Count > 0)
        {
            foreach (var observation in remaining)
            {
                try
                {
                    using var residual = Process.GetProcessById(observation.ProcessId);
                    _ = residual.CloseMainWindow();
                    if (!residual.WaitForExit(10_000))
                    {
                        forced = true;
                        residual.Kill(true);
                        _ = residual.WaitForExit(10_000);
                    }
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception)
                {
                    error += $"; residual cleanup: {exception.Message}";
                }
            }
        }
        var cleanupVerified = WaitForProcessQuiescence(TimeSpan.FromSeconds(2)).Count == 0;
        return Result(cleanupVerified);

        List<KukaSimProcessObservation> WaitForProcessQuiescence(TimeSpan timeout)
        {
            var wait = Stopwatch.StartNew();
            List<KukaSimProcessObservation> observations;
            do
            {
                observations = FindRunningProcesses(request.LauncherPath, request.EnginePath).ToList();
                if (observations.Count == 0) return observations;
                Thread.Sleep(100);
            }
            while (wait.Elapsed < timeout);

            return FindRunningProcesses(request.LauncherPath, request.EnginePath).ToList();
        }

        KukaSimProcessResult Result(bool cleanupVerified)
        {
            stopwatch.Stop();
            return new KukaSimProcessResult(exitCode, started, timedOut, forced, cleanupVerified, stopwatch.ElapsedMilliseconds, ids.Order().ToList(), error);
        }
    }
}

public sealed record KukaSimIntegratedValidationVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;
    public bool Succeeded { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
    public List<string> Errors { get; init; } = [];
}

public static class KukaSimIntegratedValidationReceiptVerifier
{
    public static KukaSimIntegratedValidationVerificationResult Verify(KukaSimIntegratedValidationReceipt receipt)
        => Verify(receipt, rehashCurrentFiles: true);

    internal static KukaSimIntegratedValidationVerificationResult Verify(
        KukaSimIntegratedValidationReceipt receipt,
        bool rehashCurrentFiles)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (receipt.SchemaIdentity != KukaSimIntegratedValidationContract.ReceiptSchemaIdentity
            || receipt.SchemaVersion != KukaSimIntegratedValidationContract.ReceiptSchemaVersion) errors.Add("receipt schema is invalid");
        var payload = receipt.Payload;
        if (payload is null) return new() { Succeeded = false, Errors = ["payload is required"] };
        if (string.IsNullOrWhiteSpace(payload.ReceiptId) || string.IsNullOrWhiteSpace(payload.AttemptId)) errors.Add("receiptId and attemptId are required");
        if (payload.StartedAtUtc > payload.CompletedAtUtc || payload.DurationMilliseconds < 0) errors.Add("payload timing is invalid");
        if (!IsSha(payload.CoreAssemblySha256) || payload.EmbeddedScriptSha256 != KukaSimIntegratedValidationContract.EmbeddedScriptSha256) errors.Add("implementation hashes are invalid");
        if (payload.TerminalClassification != EnvironmentTerminalClassification.Ready
            || payload.ValidationStatus != "ExactC01IntegratedValidated" || payload.Provider != "Integrated" || !payload.SimulationPerformed) errors.Add("receipt does not claim the accepted exact-C01 Integrated state");
        if (payload.OfficeLiteConnectionPerformed || payload.RcsProviderUsed || payload.PhysicalControllerContacted
            || payload.NativeKssStatus != NativeKssStatus.NotRun) errors.Add("receipt overclaims OfficeLite/RCS/native-KSS/physical evidence");
        if (!payload.GuiExecutionAuthorized || string.IsNullOrWhiteSpace(payload.AuthorizationReference)
            || payload.PreExistingProcesses.Count != 0 || !payload.EnvironmentReusable) errors.Add("authorization/process/environment evidence is invalid");
        if (payload.UnsupportedGaps is null || KukaSimIntegratedValidationContract.RequiredUnsupportedGaps.Any(gap => !payload.UnsupportedGaps.Contains(gap, StringComparer.Ordinal))) errors.Add("required limitations are missing");
        if (payload.Checks is null || payload.Checks.Count == 0 || payload.Checks.Any(check => check.Status != EnvironmentCheckStatus.Passed)) errors.Add("all acceptance checks must be present and passed");
        else
        {
            var requiredChecks = new[]
            {
                "exact-c01-component", "valid-src", "valid-dat", "invalid-src", "invalid-dat",
                "gui-authorization", "process-ownership", "evidence-directory", "pinned-script",
                "positive-process-cleanup", "negative-process-cleanup", "positive-integrated-run", "negative-integrated-rejection"
            };
            foreach (var id in requiredChecks)
            {
                if (!payload.Checks.Any(check => check.Id == id && check.Status == EnvironmentCheckStatus.Passed)) errors.Add($"required passed check is missing: {id}");
            }
        }
        if (payload.Files is null || payload.Files.Count == 0 || payload.Files.GroupBy(file => file.Id, StringComparer.Ordinal).Any(group => group.Count() > 1)) errors.Add("file evidence is missing or duplicated");
        else VerifyFiles(payload.Files, errors, rehashCurrentFiles);
        foreach (var error in KukaSimIntegratedValidationRunner.ValidatePositive(payload.Positive)) errors.Add($"positive: {error}");
        foreach (var error in KukaSimIntegratedValidationRunner.ValidateNegative(payload.Negative)) errors.Add($"negative: {error}");
        VerifyRawResult(payload.Positive, errors, rehashCurrentFiles);
        VerifyRawResult(payload.Negative, errors, rehashCurrentFiles);
        if (!string.Equals(receipt.PayloadSha256, ReceiptSerialization.ComputeCanonicalSha256(payload), StringComparison.OrdinalIgnoreCase)) errors.Add("payloadSha256 does not match the canonical payload");
        return new KukaSimIntegratedValidationVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }

    private static void VerifyFiles(IEnumerable<EnvironmentFileObservation> files, List<string> errors, bool rehashCurrentFiles)
    {
        var observations = files.ToList();
        var expectedHashes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["exact-c01-component"] = KukaSimIntegratedValidationContract.ExactKr210R2700ComponentSha256,
            ["valid-src"] = KukaSimIntegratedValidationContract.ValidSourceSha256,
            ["valid-dat"] = KukaSimIntegratedValidationContract.ValidDataSha256,
            ["invalid-src"] = KukaSimIntegratedValidationContract.InvalidSourceSha256,
            ["invalid-dat"] = KukaSimIntegratedValidationContract.InvalidDataSha256,
            ["kukasim-integrated-script"] = KukaSimIntegratedValidationContract.EmbeddedScriptSha256
        };
        var requiredIds = expectedHashes.Keys.Concat([
            "kukasim-engine", "kukasim-script-starter", "kukasim-create3d-api",
            "kukasim-positive-result", "kukasim-positive-trace", "kukasim-negative-result", "kukasim-negative-trace"
        ]).ToList();
        foreach (var id in requiredIds)
        {
            if (!observations.Any(file => file.Id == id && file.Exists)) errors.Add($"required file evidence is missing: {id}");
        }
        foreach (var pair in expectedHashes)
        {
            var observation = observations.SingleOrDefault(file => file.Id == pair.Key);
            if (observation is not null && !string.Equals(observation.Sha256, pair.Value, StringComparison.OrdinalIgnoreCase)) errors.Add($"pinned file hash is invalid: {pair.Key}");
        }
        foreach (var file in observations)
        {
            if (!file.Exists || string.IsNullOrWhiteSpace(file.Path) || !Path.IsPathFullyQualified(file.Path))
            {
                errors.Add($"observed file is missing: {file.Id}");
                continue;
            }
            if (!rehashCurrentFiles) continue;
            if (!File.Exists(file.Path))
            {
                errors.Add($"observed file is missing: {file.Id}");
                continue;
            }
            var info = new FileInfo(file.Path);
            var hash = KukaSimIntegratedValidationRunner.ComputeFileSha256(file.Path);
            if (info.Length != file.Bytes || !string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase)) errors.Add($"observed file drift: {file.Id}");
        }
    }

    private static void VerifyRawResult(KukaSimIntegratedAttempt attempt, List<string> errors, bool rehashCurrentFiles)
    {
        if (attempt.Result is null)
        {
            errors.Add($"{attempt.Role} stored result is missing");
            return;
        }
        if (!string.Equals(attempt.ResultCanonicalSha256, ReceiptSerialization.ComputeCanonicalSha256(attempt.Result), StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{attempt.Role} stored result hash is invalid");
        }
        if (!rehashCurrentFiles) return;
        if (string.IsNullOrWhiteSpace(attempt.RawResultPath) || !Path.IsPathFullyQualified(attempt.RawResultPath))
        {
            errors.Add($"{attempt.Role} raw result path is missing or not absolute");
            return;
        }
        try
        {
            var reparsed = KukaSimIntegratedRawParser.Parse(attempt.RawResultPath);
            var hash = ReceiptSerialization.ComputeCanonicalSha256(reparsed);
            if (!string.Equals(hash, attempt.ResultCanonicalSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(hash, ReceiptSerialization.ComputeCanonicalSha256(attempt.Result), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{attempt.Role} parsed result does not match bound raw evidence");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException or FormatException or ArgumentException)
        {
            errors.Add($"{attempt.Role} raw result cannot be reparsed: {exception.Message}");
        }
    }

    private static bool IsSha(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}

public static class KukaSimIntegratedValidationReceiptWriter
{
    public static string WriteNew(string outputPath, KukaSimIntegratedValidationReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        var verification = KukaSimIntegratedValidationReceiptVerifier.Verify(receipt);
        if (!verification.Succeeded)
        {
            throw new InvalidOperationException(
                $"KUKA.Sim Integrated validation receipt is invalid: {string.Join("; ", verification.Errors)}");
        }
        var fullPath = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Receipt output must use .json.", nameof(outputPath));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath)));
        using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(ReceiptSerialization.ToJson(receipt));
        writer.WriteLine();
        writer.Flush();
        stream.Flush(true);
        return fullPath;
    }
}
