using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class KukaSimOfficeLiteVirtualLoopContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.kukasim-officelite-virtual-loop-receipt";
    public const int ReceiptSchemaVersion = 5;
    public const string EmbeddedKukaSimScriptResource = "KukaLab.Probes.KukaSim.OfficeLiteVirtualLoop.cs";
    public const string EmbeddedKukaSimScriptSha256 = "FDD612E4BC93CE0BBE2819D71C6AA690461B934349AFEB9C8F69D6E5FA567916";
    public const string EmbeddedWorkVisualScriptResource = "KukaLab.Probes.WorkVisual.KssCompositionTransaction.csx";
    public const string EmbeddedWorkVisualScriptSha256 = "F4A9C78595E2185275CFF1A633B7E5E1FFA72DD937D9FD9E6F5DFF4DECCD6C05";
    public const string OnlineAssemblySha256 = "F1370F002504E3B754182A51506E2A356F1F27593073FE3AC84729762EBB1C8F";
    public const string VrcWrapperSha256 = "2F1A92D48046D47559FD2ED9914EF76794B69DFC918DFD6FDBBC8205CC0001F5";
    public const string ExactC01ComponentSha256 = "9307A04CC52BF70CC7D357676B80ABC1F3F3D5D452AA2AD64EC48E4A37543F33";
    public const string SourceSha256 = "646D9E216E7E974BADD3C88A067E9E6BBEDF429D0233851C6304ABD5261E07F0";
    public const string DataSha256 = "5EEB76FDC7ECB305D464B9E7D2BCD7834EF96C6DD6852DD1B03405782526502F";
    public const string TransactionRoot = @"KRC:\R1\Program\KLAB_W4AL";
    public const string ProgramPath = TransactionRoot + @"\LAB_MINIMAL.SRC";
    public const string DefaultSnapshotName = "KLAB-EXACT-C01-V2-ACTIVE";

    internal static readonly IReadOnlyList<string> RequiredUnsupportedGaps =
    [
        "This receipt proves the isolated #KR210R2700_2 C01 FLR OfficeLite profile drove the exact KR 210 R2700-2 C01 KUKA.Sim component; it is not physical-controller evidence.",
        "The high-level IRuntimeInterpreter.Start() command is used; no virtual-KCP press/release API is asserted or required by this receipt.",
        "Collision, calibrated Tool/Base/Load, production cycle time, physical safety, payload and mastering remain outside this receipt.",
        "No physical controller connection or motion, safety change, credential use, license activation or license-file read is performed."
    ];
}

public sealed record KukaSimOfficeLiteVirtualLoopRequest
{
    public required OfficeLiteCycleRequest OfficeLite { get; init; }
    public required string RunnerPath { get; init; }
    public required string EnginePath { get; init; }
    public required string LauncherPath { get; init; }
    public required string BootstrapPluginPath { get; init; }
    public required string OnlineAssemblyPath { get; init; }
    public required string VrcWrapperPath { get; init; }
    public required string ComponentPath { get; init; }
    public required string SynchronizedLayoutPath { get; init; }
    public required string SourcePath { get; init; }
    public required string DataPath { get; init; }
    public required string EvidenceDirectory { get; init; }
    public string SnapshotName { get; init; } = KukaSimOfficeLiteVirtualLoopContract.DefaultSnapshotName;
    public int RunnerTimeoutSeconds { get; init; } = 240;
    public int KukaSimTimeoutSeconds { get; init; } = 300;
    public bool GuiExecutionAuthorized { get; init; }
    public string AuthorizationReference { get; init; } = string.Empty;

    public static KukaSimOfficeLiteVirtualLoopRequest CreateDefault(
        string assetRoot,
        string labRoot,
        string evidenceDirectory,
        bool guiExecutionAuthorized,
        string authorizationReference,
        string? vmrunPath = null,
        string? vmxPath = null,
        string? runnerPath = null,
        string? enginePath = null,
        string? componentPath = null,
        string? synchronizedLayoutPath = null,
        string? guestIpAddress = null,
        string? snapshotName = null,
        int readinessTimeoutSeconds = 240,
        int serviceObservationSeconds = 180,
        int runnerTimeoutSeconds = 240,
        int kukaSimTimeoutSeconds = 300,
        bool adoptRunningLabVm = false)
    {
        var fullAssetRoot = Path.GetFullPath(assetRoot);
        var root = Path.GetFullPath(labRoot);
        var defaultVmx = Path.Combine(fullAssetRoot, "OfficeLite-Work", "8.7.8-build04", "runs", "exact-c01-goal-20260829", "Exact-C01-Goal.vmx");
        if (string.IsNullOrWhiteSpace(synchronizedLayoutPath))
            throw new ArgumentException("A controller-synchronized KUKA.Sim layout is required.", nameof(synchronizedLayoutPath));
        var officeLite = OfficeLiteCycleRequest.CreateDefault(
            fullAssetRoot,
            vmrunPath,
            guestIpAddress,
            readinessTimeoutSeconds: readinessTimeoutSeconds) with
        {
            VmxPath = Path.GetFullPath(vmxPath ?? defaultVmx),
            DiagnoseWorkVisualServices = true,
            RequiredWorkVisualServicePorts = [49003, 49004],
            ServiceObservationSeconds = serviceObservationSeconds,
            ServiceProbeIntervalMilliseconds = 5000,
            AdoptRunningLabVm = adoptRunningLabVm,
            RunningVmOwnershipReference = adoptRunningLabVm ? authorizationReference : string.Empty
        };
        var kukaSim = KukaSimInstallationDiscovery.ResolveFromEngine(
            enginePath,
            componentPath,
            componentFileName: "KR 210 R2700-2 C01.vcmx");
        return new KukaSimOfficeLiteVirtualLoopRequest
        {
            OfficeLite = officeLite,
            RunnerPath = Path.GetFullPath(runnerPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "KUKA", "WorkVisual 6.0", "wvsr.exe")),
            EnginePath = kukaSim.EnginePath,
            LauncherPath = kukaSim.LauncherPath,
            BootstrapPluginPath = kukaSim.BootstrapPluginPath,
            OnlineAssemblyPath = Path.Combine(kukaSim.InstallRoot, "KUKA", "Kuka.Sim.Programming.Online.dll"),
            VrcWrapperPath = Path.Combine(kukaSim.InstallRoot, "VisualComponents.KRC.VRCWrapper.dll"),
            ComponentPath = kukaSim.ComponentPath,
            SynchronizedLayoutPath = Path.GetFullPath(synchronizedLayoutPath),
            SourcePath = Path.Combine(root, "fixtures", "minimal-ptp-lin-valid", "controller-files", "LAB_MINIMAL.src"),
            DataPath = Path.Combine(root, "fixtures", "minimal-ptp-lin-valid", "controller-files", "LAB_MINIMAL.dat"),
            EvidenceDirectory = Path.GetFullPath(evidenceDirectory),
            SnapshotName = string.IsNullOrWhiteSpace(snapshotName) ? KukaSimOfficeLiteVirtualLoopContract.DefaultSnapshotName : snapshotName,
            RunnerTimeoutSeconds = runnerTimeoutSeconds,
            KukaSimTimeoutSeconds = kukaSimTimeoutSeconds,
            GuiExecutionAuthorized = guiExecutionAuthorized,
            AuthorizationReference = authorizationReference
        };
    }
}

public sealed record KukaSimOfficeLiteCycleObservation
{
    public int Cycle { get; init; }
    public bool Connected { get; init; }
    public string Interface { get; init; } = string.Empty;
    public string ActiveConnector { get; init; } = string.Empty;
    public int StartCount { get; init; }
    public string Mode { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public bool Deselected { get; init; }
    public bool SimulationInitialized { get; init; }
    public bool CanUpdatePosition { get; init; }
    public bool SceneSynchronized { get; init; }
    public double MaxDisplacementMillimeters { get; init; }
    public List<double> FinalTcp { get; init; } = [];
    public bool Succeeded { get; init; }
}

public sealed record KukaSimOfficeLiteDisconnectObservation
{
    public int Cycle { get; init; }
    public string State { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public bool CanDisconnect { get; init; }
    public bool Succeeded { get; init; }
}

public sealed record KukaSimOfficeLiteRawResult
{
    public bool ApplicationInitialized { get; init; }
    public bool ApplicationReady { get; init; }
    public bool ValidLicenseExists { get; init; }
    public string ComponentName { get; init; } = string.Empty;
    public bool IsComponent { get; init; }
    public int LoadedObjectCount { get; init; }
    public string TcpNodeName { get; init; } = string.Empty;
    public string MotionExecution { get; init; } = string.Empty;
    public string ConnectionMode { get; init; } = string.Empty;
    public string SimulationMode { get; init; } = string.Empty;
    public string StartSemantics { get; init; } = string.Empty;
    public List<KukaSimOfficeLiteCycleObservation> Cycles { get; init; } = [];
    public List<KukaSimOfficeLiteDisconnectObservation> Disconnects { get; init; } = [];
    public double? RepeatabilityMillimeters { get; init; }
    public bool NegativeEndpointConnected { get; init; }
    public string NegativeEndpointState { get; init; } = string.Empty;
    public bool NegativeEndpointSucceeded { get; init; }
    public bool SuccessMarker { get; init; }
    public string ErrorType { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed record KukaSimOfficeLiteWorkVisualObservation
{
    public bool PreCleanupVerified { get; init; }
    public NativeKssRunnerCommandObservation Prepare { get; init; } = new();
    public NativeKssRunnerCommandObservation Cleanup { get; init; } = new();
    public bool UploadVerified { get; init; }
    public bool GoConfirmed { get; init; }
    public bool NoRelatedErrors { get; init; }
    public bool PrepareVerified { get; init; }
    public bool CleanupVerified { get; init; }
}

public sealed record KukaSimOfficeLiteVirtualLoopReceipt
{
    public string SchemaIdentity { get; init; } = KukaSimOfficeLiteVirtualLoopContract.ReceiptSchemaIdentity;
    public int SchemaVersion { get; init; } = KukaSimOfficeLiteVirtualLoopContract.ReceiptSchemaVersion;
    public required KukaSimOfficeLiteVirtualLoopPayload Payload { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record KukaSimOfficeLiteVirtualLoopPayload
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
    public string EvidenceDirectory { get; init; } = string.Empty;
    public string SnapshotName { get; init; } = string.Empty;
    public string ControllerAddress { get; init; } = string.Empty;
    public string SynchronizedLayoutPath { get; init; } = string.Empty;
    public string TransactionRoot { get; init; } = string.Empty;
    public string ProgramPath { get; init; } = string.Empty;
    public string EmbeddedKukaSimScriptSha256 { get; init; } = string.Empty;
    public string EmbeddedWorkVisualScriptSha256 { get; init; } = string.Empty;
    public string MaterializedKukaSimScriptPath { get; init; } = string.Empty;
    public string MaterializedWorkVisualScriptPath { get; init; } = string.Empty;
    public bool TemporaryProbeScriptsRemoved { get; init; }
    public List<KukaSimProcessObservation> PreExistingProcesses { get; init; } = [];
    public VmrunCommandObservation SnapshotInventoryCommand { get; init; } = new();
    public VmrunCommandObservation SnapshotRestoreCommand { get; init; } = new();
    public VmrunCommandObservation FinalVmListCommand { get; init; } = new();
    public bool SnapshotRestored { get; init; }
    public OfficeLiteCycleReceipt LifecycleReceipt { get; init; } = null!;
    public KukaSimOfficeLiteWorkVisualObservation WorkVisual { get; init; } = new();
    public List<KukaSimCommandObservation> KukaSimCommands { get; init; } = [];
    public KukaSimOfficeLiteRawResult? Result { get; init; }
    public string ResultCanonicalSha256 { get; init; } = string.Empty;
    public List<string> RawResultPaths { get; init; } = [];
    public List<string> RawTracePaths { get; init; } = [];
    public bool CompositeConnectionValidated { get; init; }
    public bool DisconnectReconnectValidated { get; init; }
    public bool FailureInjectionValidated { get; init; }
    public bool HighLevelStartValidated { get; init; }
    public bool PhysicalControllerContacted { get; init; }
    public bool CredentialsUsed { get; init; }
    public bool LicenseOperationPerformed { get; init; }
    public bool EnvironmentReusable { get; init; }
    public List<EnvironmentCheck> Checks { get; init; } = [];
    public List<EnvironmentFileObservation> Files { get; init; } = [];
    public List<string> SideEffects { get; init; } = [];
    public List<string> UnsupportedGaps { get; init; } = [];
}

public sealed record KukaSimOfficeLiteVirtualLoopOutcome(KukaSimOfficeLiteVirtualLoopReceipt Receipt)
{
    public int ExitCode => Receipt.Payload.TerminalClassification switch
    {
        EnvironmentTerminalClassification.Ready => 0,
        EnvironmentTerminalClassification.Failed => 2,
        _ => 3
    };
}

internal static class KukaSimOfficeLiteRawParser
{
    internal static KukaSimOfficeLiteRawResult Parse(string path)
    {
        var applicationInitialized = false;
        var applicationReady = false;
        var validLicense = false;
        var componentName = string.Empty;
        var isComponent = false;
        var loadedCount = 0;
        var tcpNode = string.Empty;
        var motion = string.Empty;
        var connection = string.Empty;
        var simulation = string.Empty;
        var startSemantics = string.Empty;
        var cycles = new List<KukaSimOfficeLiteCycleObservation>();
        var disconnects = new List<KukaSimOfficeLiteDisconnectObservation>();
        double? repeatability = null;
        var negativeConnected = false;
        var negativeState = string.Empty;
        var negativeSuccess = false;
        var success = false;
        var errorType = string.Empty;
        var errorMessage = string.Empty;

        foreach (var line in File.ReadLines(path, new UTF8Encoding(false, true)))
        {
            var fields = line.Split('\t');
            if (fields.Length == 0) continue;
            switch (fields[0])
            {
                case "APPLICATION" when fields.Length >= 4:
                    applicationInitialized = Bool(fields[1]);
                    applicationReady = Bool(fields[2]);
                    validLicense = Bool(fields[3]);
                    break;
                case "COMPONENT" when fields.Length >= 5:
                    componentName = fields[1];
                    isComponent = Bool(fields[2]);
                    loadedCount = Int(fields[3]);
                    tcpNode = fields[4];
                    break;
                case "CONTRACT" when fields.Length >= 5:
                    motion = fields[1];
                    connection = fields[2];
                    simulation = fields[3];
                    startSemantics = fields[4];
                    break;
                case "CYCLE" when fields.Length >= 3:
                    var cycle = Fields(fields, 2);
                    cycles.Add(new KukaSimOfficeLiteCycleObservation
                    {
                        Cycle = Int(fields[1]),
                        Connected = Bool(Get(cycle, "Connected")),
                        Interface = Get(cycle, "Interface"),
                        ActiveConnector = Get(cycle, "Active"),
                        StartCount = Int(Get(cycle, "StartCount")),
                        Mode = Get(cycle, "Mode"),
                        State = Get(cycle, "State"),
                        Deselected = Bool(Get(cycle, "Deselected")),
                        SimulationInitialized = Bool(Get(cycle, "SimulationInitialized")),
                        CanUpdatePosition = Bool(Get(cycle, "CanUpdatePosition")),
                        SceneSynchronized = Bool(Get(cycle, "SceneSynchronized")),
                        MaxDisplacementMillimeters = Double(Get(cycle, "MaxDisplacementMm")),
                        FinalTcp = Get(cycle, "Final").Split(',').Select(Double).ToList(),
                        Succeeded = Bool(Get(cycle, "Success"))
                    });
                    break;
                case "DISCONNECT" when fields.Length >= 3:
                    var disconnect = Fields(fields, 2);
                    disconnects.Add(new KukaSimOfficeLiteDisconnectObservation
                    {
                        Cycle = Int(fields[1]),
                        State = Get(disconnect, "State"),
                        Method = Get(disconnect, "Method"),
                        CanDisconnect = Bool(Get(disconnect, "CanDisconnect")),
                        Succeeded = Bool(Get(disconnect, "Success"))
                    });
                    break;
                case "REPEATABILITY" when fields.Length >= 2:
                    repeatability = Double(fields[1]);
                    break;
                case "NEGATIVE_ENDPOINT" when fields.Length >= 2:
                    var negative = Fields(fields, 1);
                    negativeConnected = Bool(Get(negative, "Connected"));
                    negativeState = Get(negative, "State");
                    negativeSuccess = Bool(Get(negative, "Success"));
                    break;
                case "SUCCESS" when fields.Length >= 2:
                    success = Bool(fields[1]);
                    break;
                case "ERROR" when fields.Length >= 3:
                    errorType = fields[1];
                    errorMessage = fields[2];
                    break;
            }
        }

        return new KukaSimOfficeLiteRawResult
        {
            ApplicationInitialized = applicationInitialized,
            ApplicationReady = applicationReady,
            ValidLicenseExists = validLicense,
            ComponentName = componentName,
            IsComponent = isComponent,
            LoadedObjectCount = loadedCount,
            TcpNodeName = tcpNode,
            MotionExecution = motion,
            ConnectionMode = connection,
            SimulationMode = simulation,
            StartSemantics = startSemantics,
            Cycles = cycles,
            Disconnects = disconnects,
            RepeatabilityMillimeters = repeatability,
            NegativeEndpointConnected = negativeConnected,
            NegativeEndpointState = negativeState,
            NegativeEndpointSucceeded = negativeSuccess,
            SuccessMarker = success,
            ErrorType = errorType,
            ErrorMessage = errorMessage
        };
    }

    private static Dictionary<string, string> Fields(string[] fields, int start) => fields.Skip(start)
        .Select(field => field.Split(new[] { '=' }, 2))
        .Where(pair => pair.Length == 2)
        .ToDictionary(pair => pair[0], pair => pair[1], StringComparer.Ordinal);
    private static string Get(IReadOnlyDictionary<string, string> values, string key) => values.TryGetValue(key, out var value) ? value : string.Empty;
    private static bool Bool(string value) => bool.TryParse(value, out var parsed) && parsed;
    private static int Int(string value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    private static double Double(string value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0d;

    internal static KukaSimOfficeLiteRawResult Aggregate(IReadOnlyList<KukaSimOfficeLiteRawResult> parts)
    {
        if (parts.Count == 0) return new KukaSimOfficeLiteRawResult { ErrorType = nameof(InvalidDataException), ErrorMessage = "No phase result was produced." };
        var valid = parts.Where(part => part.Cycles.Count == 1).OrderBy(part => part.Cycles[0].Cycle).ToList();
        var negative = parts.SingleOrDefault(part => part.Cycles.Count == 0 && !string.IsNullOrEmpty(part.NegativeEndpointState));
        var first = valid.FirstOrDefault() ?? parts[0];
        double? repeatability = null;
        if (valid.Count == 2)
        {
            var left = valid[0].Cycles[0].FinalTcp;
            var right = valid[1].Cycles[0].FinalTcp;
            if (left.Count == 3 && right.Count == 3)
                repeatability = Math.Sqrt(Math.Pow(left[0] - right[0], 2) + Math.Pow(left[1] - right[1], 2) + Math.Pow(left[2] - right[2], 2));
        }
        var aggregateError = parts.Count == 3 && valid.Count == 2 && negative is not null ? string.Empty : "The two-valid-plus-negative phase result set is incomplete.";
        return first with
        {
            Cycles = valid.SelectMany(part => part.Cycles).OrderBy(cycle => cycle.Cycle).ToList(),
            Disconnects = valid.SelectMany(part => part.Disconnects).OrderBy(item => item.Cycle).ToList(),
            RepeatabilityMillimeters = repeatability,
            NegativeEndpointConnected = negative?.NegativeEndpointConnected ?? false,
            NegativeEndpointState = negative?.NegativeEndpointState ?? string.Empty,
            NegativeEndpointSucceeded = negative?.NegativeEndpointSucceeded ?? false,
            SuccessMarker = parts.Count == 3 && parts.All(part => part.SuccessMarker),
            ErrorType = string.Join("; ", parts.Select(part => part.ErrorType).Where(value => !string.IsNullOrWhiteSpace(value))
                .Concat(string.IsNullOrEmpty(aggregateError) ? [] : [nameof(InvalidDataException)])),
            ErrorMessage = string.Join("; ", parts.Select(part => part.ErrorMessage).Where(value => !string.IsNullOrWhiteSpace(value))
                .Concat(string.IsNullOrEmpty(aggregateError) ? [] : [aggregateError]))
        };
    }
}

public sealed class KukaSimOfficeLiteVirtualLoopRunner
{
    private readonly IOfficeLiteHostPlatform _officeLitePlatform;
    private readonly OfficeLiteCycleRunner _officeLiteRunner;
    private readonly IWorkVisualRunnerPlatform _workVisualPlatform;
    private readonly IKukaSimOfficeLiteVirtualLoopPlatform _kukaSimPlatform;
    private readonly TimeProvider _timeProvider;
    private readonly Func<string, string> _computeFileSha256;

    public KukaSimOfficeLiteVirtualLoopRunner()
        : this(new OfficeLiteHostPlatform(), new WorkVisualRunnerPlatform(), new KukaSimOfficeLiteVirtualLoopPlatform(), TimeProvider.System)
    {
    }

    internal KukaSimOfficeLiteVirtualLoopRunner(
        IOfficeLiteHostPlatform officeLitePlatform,
        IWorkVisualRunnerPlatform workVisualPlatform,
        IKukaSimOfficeLiteVirtualLoopPlatform kukaSimPlatform,
        TimeProvider timeProvider,
        Func<string, string>? computeFileSha256 = null)
    {
        _officeLitePlatform = officeLitePlatform;
        _officeLiteRunner = new OfficeLiteCycleRunner(officeLitePlatform, timeProvider);
        _workVisualPlatform = workVisualPlatform;
        _kukaSimPlatform = kukaSimPlatform;
        _timeProvider = timeProvider;
        _computeFileSha256 = computeFileSha256 ?? ComputeFileSha256;
    }

    public KukaSimOfficeLiteVirtualLoopOutcome Run(KukaSimOfficeLiteVirtualLoopRequest request, string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateIdentifier(attemptId, nameof(attemptId));
        ValidateIdentifier(request.SnapshotName, nameof(request.SnapshotName));
        if (request.RunnerTimeoutSeconds is < 1 or > 300) throw new ArgumentOutOfRangeException(nameof(request.RunnerTimeoutSeconds));
        if (request.KukaSimTimeoutSeconds is < 30 or > 600) throw new ArgumentOutOfRangeException(nameof(request.KukaSimTimeoutSeconds));
        if (request.GuiExecutionAuthorized) ValidateIdentifier(request.AuthorizationReference, nameof(request.AuthorizationReference));

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var checks = new List<EnvironmentCheck>();
        var files = new List<EnvironmentFileObservation>();
        var sideEffects = new List<string>();
        var evidence = Path.GetFullPath(request.EvidenceDirectory);
        var runner = Path.GetFullPath(request.RunnerPath);
        var engine = Path.GetFullPath(request.EnginePath);
        var launcher = Path.GetFullPath(request.LauncherPath);
        var bootstrapPlugin = Path.GetFullPath(request.BootstrapPluginPath);
        var frameworkCompiler = KukaSimComponentSmokeRunner.ResolveFrameworkCompilerPath();
        var component = Path.GetFullPath(request.ComponentPath);
        var synchronizedLayout = Path.GetFullPath(request.SynchronizedLayoutPath);
        var source = Path.GetFullPath(request.SourcePath);
        var data = Path.GetFullPath(request.DataPath);
        var kukaScriptPath = Path.Combine(evidence, attemptId + ".officelite-virtual-loop.cs");
        var workVisualScriptPath = Path.Combine(evidence, attemptId + ".composition-transaction.csx");
        var rawResultPaths = new List<string>();
        var rawTracePaths = new List<string>();
        var workVisual = new KukaSimOfficeLiteWorkVisualObservation();
        var commands = new List<KukaSimCommandObservation>();
        KukaSimOfficeLiteRawResult? result = null;
        OfficeLiteCycleReceipt? lifecycle = null;
        var preExisting = new List<KukaSimProcessObservation>();
        var snapshotInventory = new VmrunCommandObservation();
        var snapshotRestore = new VmrunCommandObservation();
        var finalVmList = new VmrunCommandObservation();
        var snapshotRestored = false;
        var scriptsRemoved = false;
        var controllerAddress = string.Empty;

        if (!OperatingSystem.IsWindows())
        {
            checks.Add(Blocked("host-os", "KUKA.Sim/OfficeLite virtual loop requires Windows."));
            return Complete();
        }
        if (!request.GuiExecutionAuthorized)
        {
            checks.Add(Blocked("gui-authorization", "KUKA.Sim is a GUI-subsystem process and authorization was not recorded."));
            return Complete();
        }
        checks.Add(Passed("gui-authorization", "Bounded KUKA.Sim execution is authorized by " + request.AuthorizationReference + "."));

        var required = new[]
        {
            ("workvisual-runner", runner, ""),
            ("kukasim-engine", engine, ""),
            ("kukasim-launcher", launcher, ""),
            ("kukasim-bootstrap-plugin", bootstrapPlugin, ""),
            ("netfx-csharp-compiler", frameworkCompiler, ""),
            ("kukasim-online-assembly", Path.GetFullPath(request.OnlineAssemblyPath), KukaSimOfficeLiteVirtualLoopContract.OnlineAssemblySha256),
            ("kukasim-vrc-wrapper", Path.GetFullPath(request.VrcWrapperPath), KukaSimOfficeLiteVirtualLoopContract.VrcWrapperSha256),
            ("exact-c01-component", component, KukaSimOfficeLiteVirtualLoopContract.ExactC01ComponentSha256),
            ("controller-synchronized-layout", synchronizedLayout, ""),
            ("exact-c01-valid-src", source, KukaSimOfficeLiteVirtualLoopContract.SourceSha256),
            ("exact-c01-valid-dat", data, KukaSimOfficeLiteVirtualLoopContract.DataSha256)
        };
        foreach (var item in required)
        {
            if (!ObserveRequiredFile(item.Item1, item.Item2, item.Item3, files, checks)) return Complete();
        }

        try
        {
            preExisting = _kukaSimPlatform.FindRunningProcesses(launcher, engine).ToList();
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            checks.Add(Failed("process-ownership", exception.Message));
            return Complete();
        }
        if (preExisting.Count != 0)
        {
            checks.Add(Blocked("process-ownership", "A KUKA.Sim process was already running; the attempt refused to attach or terminate it."));
            return Complete();
        }
        checks.Add(Passed("process-ownership", "No pre-existing KUKA.Sim process exists."));

        try
        {
            if (Directory.Exists(evidence))
            {
                checks.Add(Failed("evidence-directory", "Evidence directory already exists; create-new semantics refused reuse."));
                return Complete();
            }
            Directory.CreateDirectory(evidence);
            sideEffects.Add("CreateEvidenceDirectory:" + evidence);
            var kukaBytes = ReadEmbedded(KukaSimOfficeLiteVirtualLoopContract.EmbeddedKukaSimScriptResource);
            var wvBytes = ReadEmbedded(KukaSimOfficeLiteVirtualLoopContract.EmbeddedWorkVisualScriptResource);
            if (Hash(kukaBytes) != KukaSimOfficeLiteVirtualLoopContract.EmbeddedKukaSimScriptSha256
                || Hash(wvBytes) != KukaSimOfficeLiteVirtualLoopContract.EmbeddedWorkVisualScriptSha256)
            {
                checks.Add(Failed("embedded-probes", "Embedded probe bytes drifted from the pinned identities."));
                return Complete();
            }
            WriteNew(kukaScriptPath, kukaBytes);
            WriteNew(workVisualScriptPath, wvBytes);
            sideEffects.Add("MaterializeTemporaryProbeScripts");
            checks.Add(Passed("embedded-probes", "Both embedded probe hashes match their pinned identities."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            checks.Add(Failed("evidence-directory", exception.Message));
            RemoveScripts();
            return Complete();
        }

        snapshotInventory = RunVmrun("list-snapshots-before", request.OfficeLite,
            ["-T", "ws", "listSnapshots", request.OfficeLite.VmxPath]);
        if (!CommandSucceeded(snapshotInventory) || !snapshotInventory.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(request.SnapshotName, StringComparer.Ordinal))
        {
            checks.Add(Blocked("named-snapshot", "The required exact named snapshot was not enumerated; OfficeLite was not started."));
            RemoveScripts();
            return Complete();
        }
        checks.Add(Passed("named-snapshot", "The exact clean snapshot was enumerated before startup."));

        var preCleanupVerified = false;
        lifecycle = _officeLiteRunner.Run(request.OfficeLite, attemptId + "-lifecycle", context =>
        {
            controllerAddress = context.GuestAddress;
            WorkVisualProcessResult? prepare = null;
            WorkVisualProcessResult? cleanup = null;
            try
            {
                var preCleanup = RunWorkVisual("cleanup", context.GuestAddress, workVisualScriptPath);
                var preCleanupOut = PersistCommand("preclean", preCleanup);
                preCleanupVerified = preCleanup.ExitCode == 0
                    && !preCleanup.TimedOut
                    && Marker(preCleanupOut, "CLEANUP_VERIFIED");
                workVisual = workVisual with { PreCleanupVerified = preCleanupVerified };
                if (!preCleanupVerified) return;

                prepare = RunWorkVisual("prepare", context.GuestAddress, workVisualScriptPath);
                var prepareOut = PersistCommand("prepare", prepare);
                workVisual = workVisual with
                {
                    Prepare = ObserveCommand(prepare, true),
                    UploadVerified = Marker(prepareOut, "UPLOAD_VERIFIED"),
                    GoConfirmed = Marker(prepareOut, "GO_CONFIRMED"),
                    NoRelatedErrors = Marker(prepareOut, "NO_RELATED_ERRORS"),
                    PrepareVerified = Marker(prepareOut, "PREPARE_VERIFIED")
                };
                if (prepare.ExitCode != 0 || prepare.TimedOut || !workVisual.PrepareVerified) return;

                var parts = new List<KukaSimOfficeLiteRawResult>();
                var valid1 = RunKukaSimPhase("valid", 1, parts);
                if (!valid1)
                {
                    var diagnostic = RunWorkVisual("diagnose", context.GuestAddress, workVisualScriptPath);
                    _ = PersistCommand("diagnose", diagnostic);
                }
                if (valid1 && RunKukaSimPhase("valid", 2, parts))
                {
                    _ = RunKukaSimPhase("negative", 0, parts);
                }
                result = KukaSimOfficeLiteRawParser.Aggregate(parts);

                bool RunKukaSimPhase(string phase, int cycle, List<KukaSimOfficeLiteRawResult> phaseResults)
                {
                    var suffix = phase + "-" + cycle.ToString(CultureInfo.InvariantCulture);
                    var rawResultPath = Path.Combine(evidence, attemptId + "." + suffix + ".result.tsv");
                    var rawTracePath = Path.Combine(evidence, attemptId + "." + suffix + ".trace.tsv");
                    var probeAssemblyPath = Path.Combine(evidence, attemptId + "." + suffix + ".probe.dll");
                    var bridgeTracePath = Path.Combine(evidence, attemptId + "." + suffix + ".bootstrap.log");
                    rawResultPaths.Add(rawResultPath);
                    rawTracePaths.Add(rawTracePath);
                    var process = _kukaSimPlatform.Run(new KukaSimOfficeLiteVirtualLoopProcessRequest(
                        launcher, engine, frameworkCompiler, kukaScriptPath, probeAssemblyPath, bridgeTracePath,
                        component, synchronizedLayout, context.GuestAddress,
                        KukaSimOfficeLiteVirtualLoopContract.ProgramPath, phase, cycle, rawResultPath, rawTracePath,
                        TimeSpan.FromSeconds(request.KukaSimTimeoutSeconds)));
                    commands.Add(new KukaSimCommandObservation
                    {
                        ProcessStarted = process.ProcessStarted,
                        ExitCode = process.ExitCode,
                        TimedOut = process.TimedOut,
                        ForcedTerminationUsed = process.ForcedTerminationUsed,
                        CleanupVerified = process.CleanupVerified,
                        DurationMilliseconds = Math.Max(0, process.DurationMilliseconds),
                        OwnedProcessIds = process.OwnedProcessIds.Distinct().Order().ToList(),
                        PlatformError = process.PlatformError
                    });
                    if (process.ProcessStarted) sideEffects.Add("StartOwnedKukaSim:" + engine + ":" + suffix);
                    KukaSimOfficeLiteRawResult phaseResult;
                    if (File.Exists(rawResultPath))
                    {
                        files.Add(ObserveExistingFile("kukasim-officelite-result-" + suffix, rawResultPath));
                        phaseResult = KukaSimOfficeLiteRawParser.Parse(rawResultPath);
                        phaseResults.Add(phaseResult);
                    }
                    else
                    {
                        phaseResult = new KukaSimOfficeLiteRawResult
                        {
                            ErrorType = nameof(InvalidDataException),
                            ErrorMessage = "KUKA.Sim phase produced no result: " + suffix
                        };
                        phaseResults.Add(phaseResult);
                    }
                    if (File.Exists(rawTracePath)) files.Add(ObserveExistingFile("kukasim-officelite-trace-" + suffix, rawTracePath));
                    if (File.Exists(bridgeTracePath)) files.Add(ObserveExistingFile("kukasim-bootstrap-trace-" + suffix, bridgeTracePath));
                    DeleteRegenerableCompiledProbe(probeAssemblyPath);
                    return process.ProcessStarted
                        && process.ExitCode == 0
                        && !process.TimedOut
                        && !process.ForcedTerminationUsed
                        && process.CleanupVerified
                        && File.Exists(rawResultPath)
                        && string.IsNullOrEmpty(phaseResult.ErrorType)
                        && phaseResult.SuccessMarker;
                }
            }
            finally
            {
                cleanup = RunWorkVisual("cleanup", context.GuestAddress, workVisualScriptPath);
                var cleanupOut = PersistCommand("cleanup", cleanup);
                workVisual = workVisual with
                {
                    Cleanup = ObserveCommand(cleanup, Marker(cleanupOut, "CLEANUP_VERIFIED")),
                    CleanupVerified = Marker(cleanupOut, "CLEANUP_VERIFIED")
                };
            }
        }).Receipt;

        snapshotRestore = RunVmrun("restore-named-snapshot", request.OfficeLite,
            ["-T", "ws", "revertToSnapshot", request.OfficeLite.VmxPath, request.SnapshotName]);
        finalVmList = RunVmrun("list-after-snapshot-restore", request.OfficeLite, ["-T", "ws", "list"]);
        snapshotRestored = CommandSucceeded(snapshotRestore) && CommandSucceeded(finalVmList)
            && !ContainsRunningVmx(finalVmList.StandardOutput, request.OfficeLite.VmxPath);
        checks.Add(snapshotRestored
            ? Passed("snapshot-restore", "The isolated VM returned to the exact clean snapshot and remained stopped.")
            : Failed("snapshot-restore", "Snapshot restoration or stopped-state verification failed."));

        var noKukaSim = WaitForKukaSimQuiescence(launcher, engine);
        checks.Add(noKukaSim && commands.Count == 3 && commands.All(command => command.CleanupVerified && !command.ForcedTerminationUsed)
            ? Passed("kukasim-cleanup", "All three owned KUKA.Sim processes exited without forced termination and no process remains.")
            : Failed("kukasim-cleanup", "One or more KUKA.Sim phases had incomplete cleanup or required forced termination."));
        RemoveScripts();
        checks.Add(scriptsRemoved
            ? Passed("temporary-probe-cleanup", "Regenerable materialized probe scripts were removed after execution.")
            : Failed("temporary-probe-cleanup", "One or more materialized probe scripts remain."));

        var rawErrors = ValidateResult(result);
        checks.Add(rawErrors.Count == 0
            ? Passed("virtual-composition", "Two Controller/ViewOnly/Attach cycles completed with high-level Start, disconnect/reconnect, repeatability and negative-endpoint evidence.")
            : Failed("virtual-composition", string.Join("; ", rawErrors)));
        checks.Add(workVisual.PreCleanupVerified && workVisual.PrepareVerified && workVisual.CleanupVerified
            ? Passed("controller-transaction", "The stale Lab-only transaction was removed, then the isolated exact-C01 program transaction was prepared and removed.")
            : Failed("controller-transaction", "The isolated exact-C01 program transaction was not pre-cleaned, prepared and removed."));
        return Complete();

        WorkVisualProcessResult RunWorkVisual(string action, string address, string scriptPath) => _workVisualPlatform.Run(
            runner,
            ["-executescript", "-scriptpath=" + scriptPath, "-address=" + address, "-action=" + action,
                "-source=" + source, "-data=" + data, "-transactionroot=" + KukaSimOfficeLiteVirtualLoopContract.TransactionRoot],
            Path.GetDirectoryName(runner) ?? Environment.CurrentDirectory,
            TimeSpan.FromSeconds(request.RunnerTimeoutSeconds));

        string PersistCommand(string role, WorkVisualProcessResult process)
        {
            var stdout = Path.Combine(evidence, attemptId + ".workvisual-" + role + ".stdout.txt");
            var stderr = Path.Combine(evidence, attemptId + ".workvisual-" + role + ".stderr.txt");
            WriteNew(stdout, new UTF8Encoding(false).GetBytes(process.StandardOutput));
            WriteNew(stderr, new UTF8Encoding(false).GetBytes(process.StandardError));
            files.Add(ObserveExistingFile("workvisual-" + role + "-stdout", stdout));
            files.Add(ObserveExistingFile("workvisual-" + role + "-stderr", stderr));
            return process.StandardOutput;
        }

        void DeleteRegenerableCompiledProbe(string path)
        {
            if (!File.Exists(path)) return;
            for (var attempt = 0; attempt < 50; attempt++)
            {
                try
                {
                    File.Delete(path);
                    sideEffects.Add("DeleteRegenerableCompiledProbe:" + path);
                    return;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    if (attempt == 49)
                    {
                        files.Add(ObserveExistingFile("kukasim-retained-compiled-probe-" + Path.GetFileNameWithoutExtension(path), path));
                        sideEffects.Add("RetainLockedCompiledProbeAsEvidence:" + path);
                        return;
                    }
                    Thread.Sleep(100);
                }
            }
        }

        void RemoveScripts()
        {
            foreach (var path in new[] { kukaScriptPath, workVisualScriptPath })
            {
                try { if (File.Exists(path)) File.Delete(path); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    checks.Add(Failed("temporary-probe-cleanup", exception.Message));
                }
            }
            scriptsRemoved = !File.Exists(kukaScriptPath) && !File.Exists(workVisualScriptPath);
            if (scriptsRemoved) sideEffects.Add("DeleteRegenerableTemporaryProbeScripts");
        }

        KukaSimOfficeLiteVirtualLoopOutcome Complete()
        {
            stopwatch.Stop();
            var rawErrors = ValidateResult(result);
            var lifecycleReady = lifecycle is not null
                && lifecycle.Payload.TerminalClassification == EnvironmentTerminalClassification.Ready
                && lifecycle.Payload.CleanShutdownVerified;
            var terminal = checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
                ? EnvironmentTerminalClassification.Failed
                : checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked)
                    ? EnvironmentTerminalClassification.Blocked
                    : EnvironmentTerminalClassification.Ready;
            scriptsRemoved = !File.Exists(kukaScriptPath) && !File.Exists(workVisualScriptPath);
            var accepted = terminal == EnvironmentTerminalClassification.Ready && rawErrors.Count == 0
                && lifecycleReady && workVisual.PreCleanupVerified && workVisual.PrepareVerified && workVisual.CleanupVerified
                && snapshotRestored && scriptsRemoved && commands.Count == 3
                && commands.All(command => command.CleanupVerified && !command.ForcedTerminationUsed);
            if (!accepted && terminal == EnvironmentTerminalClassification.Ready) terminal = EnvironmentTerminalClassification.Failed;
            var payload = new KukaSimOfficeLiteVirtualLoopPayload
            {
                ReceiptId = "kukasim-officelite-virtual-loop-" + attemptId,
                AttemptId = attemptId,
                CoreAssemblySha256 = Hash(File.ReadAllBytes(typeof(KukaSimOfficeLiteVirtualLoopRunner).Assembly.Location)),
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
                EvidenceDirectory = evidence,
                SnapshotName = request.SnapshotName,
                ControllerAddress = controllerAddress,
                SynchronizedLayoutPath = synchronizedLayout,
                TransactionRoot = KukaSimOfficeLiteVirtualLoopContract.TransactionRoot,
                ProgramPath = KukaSimOfficeLiteVirtualLoopContract.ProgramPath,
                EmbeddedKukaSimScriptSha256 = KukaSimOfficeLiteVirtualLoopContract.EmbeddedKukaSimScriptSha256,
                EmbeddedWorkVisualScriptSha256 = KukaSimOfficeLiteVirtualLoopContract.EmbeddedWorkVisualScriptSha256,
                MaterializedKukaSimScriptPath = kukaScriptPath,
                MaterializedWorkVisualScriptPath = workVisualScriptPath,
                TemporaryProbeScriptsRemoved = scriptsRemoved,
                PreExistingProcesses = preExisting,
                SnapshotInventoryCommand = snapshotInventory,
                SnapshotRestoreCommand = snapshotRestore,
                FinalVmListCommand = finalVmList,
                SnapshotRestored = snapshotRestored,
                LifecycleReceipt = lifecycle!,
                WorkVisual = workVisual,
                KukaSimCommands = commands,
                Result = result,
                ResultCanonicalSha256 = result is null ? string.Empty : ReceiptSerialization.ComputeCanonicalSha256(result),
                RawResultPaths = rawResultPaths,
                RawTracePaths = rawTracePaths,
                CompositeConnectionValidated = accepted,
                DisconnectReconnectValidated = accepted,
                FailureInjectionValidated = accepted,
                HighLevelStartValidated = accepted,
                PhysicalControllerContacted = false,
                CredentialsUsed = false,
                LicenseOperationPerformed = false,
                EnvironmentReusable = accepted,
                Checks = checks,
                Files = files,
                SideEffects = sideEffects,
                UnsupportedGaps = KukaSimOfficeLiteVirtualLoopContract.RequiredUnsupportedGaps.ToList()
            };
            var receipt = new KukaSimOfficeLiteVirtualLoopReceipt
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
            };
            return new KukaSimOfficeLiteVirtualLoopOutcome(receipt);
        }
    }

    internal static List<string> ValidateResult(KukaSimOfficeLiteRawResult? result)
    {
        var errors = new List<string>();
        if (result is null) return ["KUKA.Sim produced no parseable result"];
        if (!result.ApplicationInitialized || !result.ApplicationReady || !result.ValidLicenseExists) errors.Add("KUKA.Sim application/license readiness was not established");
        if (!result.IsComponent || result.LoadedObjectCount < 1 || !string.Equals(result.ComponentName, "KR 210 R2700-2 C01", StringComparison.Ordinal)
            || !string.Equals(result.TcpNodeName, "FlangeNode", StringComparison.Ordinal)) errors.Add("matching managed exact-C01 component/flange identity failed");
        if (!string.Equals(result.MotionExecution, "Controller", StringComparison.Ordinal)
            || !string.Equals(result.ConnectionMode, "ViewOnly", StringComparison.Ordinal)
            || !string.Equals(result.SimulationMode, "Attach", StringComparison.Ordinal)
            || !string.Equals(result.StartSemantics, "HighLevelStart", StringComparison.Ordinal)) errors.Add("composition contract is not Controller/ViewOnly/Attach/HighLevelStart");
        if (result.Cycles.Count != 2 || result.Cycles.Select(cycle => cycle.Cycle).Order().SequenceEqual(new[] { 1, 2 }) == false) errors.Add("exactly two numbered cycles are required");
        foreach (var cycle in result.Cycles)
        {
            if (!cycle.Connected || cycle.StartCount != 2 || !string.Equals(cycle.Mode, "Go", StringComparison.Ordinal)
                || !string.Equals(cycle.State, "End", StringComparison.Ordinal) || !cycle.Deselected
                || !cycle.SimulationInitialized || !cycle.CanUpdatePosition || !cycle.SceneSynchronized
                || cycle.FinalTcp.Count != 3 || !cycle.Succeeded || !(cycle.MaxDisplacementMillimeters > 0.01))
                errors.Add("cycle " + cycle.Cycle + " failed deterministic connect/run assertions");
        }
        if (result.Disconnects.Count != 2 || result.Disconnects.Any(item => !item.Succeeded || !item.CanDisconnect
            || !string.Equals(item.Method, "IControllerConnector.Disconnect", StringComparison.Ordinal)
            || !string.Equals(item.State, "NoConnection", StringComparison.Ordinal))) errors.Add("both explicit connector disconnect assertions must reach NoConnection");
        if (result.NegativeEndpointConnected || !result.NegativeEndpointSucceeded || !string.Equals(result.NegativeEndpointState, "NoConnection", StringComparison.Ordinal)) errors.Add("negative endpoint did not fail closed in NoConnection");
        if (!result.RepeatabilityMillimeters.HasValue
            || !double.IsFinite(result.RepeatabilityMillimeters.Value)
            || result.RepeatabilityMillimeters.Value > 0.1)
        {
            errors.Add("cycle repeatability is missing, non-finite, or exceeds 0.1 mm");
        }
        if (!result.SuccessMarker || !string.IsNullOrEmpty(result.ErrorType)) errors.Add("KUKA.Sim success marker is absent or an error was recorded");
        return errors;
    }

    private bool WaitForKukaSimQuiescence(string launcher, string engine)
    {
        var wait = Stopwatch.StartNew();
        do
        {
            if (_kukaSimPlatform.FindRunningProcesses(launcher, engine).Count == 0) return true;
            Thread.Sleep(100);
        } while (wait.Elapsed < TimeSpan.FromSeconds(10));
        return _kukaSimPlatform.FindRunningProcesses(launcher, engine).Count == 0;
    }

    private VmrunCommandObservation RunVmrun(string name, OfficeLiteCycleRequest request, IReadOnlyList<string> arguments)
    {
        var result = _officeLitePlatform.RunVmrun(request.VmrunPath, arguments, TimeSpan.FromSeconds(request.VmrunTimeoutSeconds));
        return new VmrunCommandObservation
        {
            Name = name,
            ExitCode = result.ExitCode,
            TimedOut = result.TimedOut,
            DurationMilliseconds = Math.Max(0, result.DurationMilliseconds),
            StandardOutput = result.StandardOutput,
            StandardError = result.StandardError
        };
    }

    private static NativeKssRunnerCommandObservation ObserveCommand(WorkVisualProcessResult result, bool cleanupVerified) => new()
    {
        Attempted = true,
        ExitCode = result.ExitCode,
        TimedOut = result.TimedOut,
        CleanupVerified = cleanupVerified,
        DurationMilliseconds = Math.Max(0, result.DurationMilliseconds),
        StandardOutputSha256 = Hash(new UTF8Encoding(false).GetBytes(result.StandardOutput)),
        StandardErrorSha256 = Hash(new UTF8Encoding(false).GetBytes(result.StandardError))
    };

    private static bool Marker(string output, string key) => output.Replace("\r\n", "\n", StringComparison.Ordinal)
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Any(line => string.Equals(line, key + "=True", StringComparison.Ordinal));

    private static byte[] ReadEmbedded(string resource)
    {
        using var stream = typeof(KukaSimOfficeLiteVirtualLoopRunner).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException("Embedded resource is unavailable: " + resource);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private bool ObserveRequiredFile(string id, string path, string expectedSha, List<EnvironmentFileObservation> files, List<EnvironmentCheck> checks)
    {
        if (!File.Exists(path))
        {
            files.Add(new EnvironmentFileObservation { Id = id, Path = path, Exists = false });
            checks.Add(Blocked(id, "Required file is missing: " + path));
            return false;
        }
        var observation = ObserveExistingFile(id, path);
        files.Add(observation);
        if (!string.IsNullOrEmpty(expectedSha) && !string.Equals(observation.Sha256, expectedSha, StringComparison.Ordinal))
        {
            checks.Add(Failed(id, "SHA-256 does not match the pinned identity."));
            return false;
        }
        checks.Add(Passed(id, "Required file exists" + (string.IsNullOrEmpty(expectedSha) ? "." : " with the pinned SHA-256.")));
        return true;
    }

    private EnvironmentFileObservation ObserveExistingFile(string id, string path)
    {
        var info = new FileInfo(path);
        var version = FileVersionInfo.GetVersionInfo(path);
        return new EnvironmentFileObservation
        {
            Id = id,
            Path = Path.GetFullPath(path),
            Exists = true,
            Bytes = info.Length,
            Sha256 = _computeFileSha256(path),
            FileVersion = version.FileVersion,
            ProductVersion = version.ProductVersion
        };
    }

    private static void WriteNew(string path, byte[] content)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(content);
        stream.Flush(true);
    }
    private static string Hash(byte[] content) => Convert.ToHexString(SHA256.HashData(content));
    private static string ComputeFileSha256(string path) => Hash(File.ReadAllBytes(path));
    private static bool CommandSucceeded(VmrunCommandObservation command) => !command.TimedOut && command.ExitCode == 0;
    private static bool ContainsRunningVmx(string output, string vmxPath) => output.Replace("\r\n", "\n", StringComparison.Ordinal)
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Any(line => line.EndsWith(".vmx", StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetFullPath(line), Path.GetFullPath(vmxPath), StringComparison.OrdinalIgnoreCase));
    private static void ValidateIdentifier(string value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        if (!Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Identifier must match [A-Za-z0-9][A-Za-z0-9._-]{2,127}.", name);
    }
    private static EnvironmentCheck Passed(string id, string detail) => new() { Id = id, Status = EnvironmentCheckStatus.Passed, Detail = detail };
    private static EnvironmentCheck Blocked(string id, string detail) => new() { Id = id, Status = EnvironmentCheckStatus.Blocked, Detail = detail };
    private static EnvironmentCheck Failed(string id, string detail) => new() { Id = id, Status = EnvironmentCheckStatus.Failed, Detail = detail };
}

internal sealed record KukaSimOfficeLiteVirtualLoopProcessRequest(
    string LauncherPath,
    string EnginePath,
    string CompilerPath,
    string ScriptPath,
    string ProbeAssemblyPath,
    string BridgeTracePath,
    string ComponentPath,
    string SynchronizedLayoutPath,
    string ControllerAddress,
    string ProgramPath,
    string Phase,
    int Cycle,
    string ResultPath,
    string TracePath,
    TimeSpan Timeout);

internal interface IKukaSimOfficeLiteVirtualLoopPlatform
{
    IReadOnlyList<KukaSimProcessObservation> FindRunningProcesses(string launcherPath, string enginePath);
    KukaSimProcessResult Run(KukaSimOfficeLiteVirtualLoopProcessRequest request);
}

// KUKA.Sim 4.10 ignores the legacy /csscript launcher argument. Reuse the
// accepted vendor-plugin bootstrap that already powers the 4.10 component smoke.
internal sealed class KukaSimOfficeLiteVirtualLoopPlatform : IKukaSimOfficeLiteVirtualLoopPlatform
{
    private readonly KukaSimComponentPlatform _processReader = new();
    public IReadOnlyList<KukaSimProcessObservation> FindRunningProcesses(string launcherPath, string enginePath) =>
        _processReader.FindRunningProcesses(launcherPath, enginePath);

    public KukaSimProcessResult Run(KukaSimOfficeLiteVirtualLoopProcessRequest request)
    {
        return _processReader.RunProbe(
            request.LauncherPath,
            request.EnginePath,
            request.CompilerPath,
            request.ScriptPath,
            request.ProbeAssemblyPath,
            "KukaLabOfficeLiteVirtualLoopProbe",
            request.BridgeTracePath,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["KUKA_LAB_RESULT_PATH"] = request.ResultPath,
                ["KUKA_LAB_TRACE_PATH"] = request.TracePath,
                ["KUKA_LAB_COMPONENT_PATH"] = request.ComponentPath,
                ["KUKA_LAB_SYNCHRONIZED_LAYOUT_PATH"] = request.SynchronizedLayoutPath,
                ["KUKA_LAB_CONTROLLER_ADDRESS"] = request.ControllerAddress,
                ["KUKA_LAB_PROGRAM_PATH"] = request.ProgramPath,
                ["KUKA_LAB_PHASE"] = request.Phase,
                ["KUKA_LAB_CYCLE"] = request.Cycle.ToString(CultureInfo.InvariantCulture)
            },
            request.Timeout);
    }
}

public sealed record KukaSimOfficeLiteVirtualLoopVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;
    public bool Succeeded { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
    public List<string> Errors { get; init; } = [];
}

public static class KukaSimOfficeLiteVirtualLoopReceiptVerifier
{
    public static KukaSimOfficeLiteVirtualLoopVerificationResult Verify(KukaSimOfficeLiteVirtualLoopReceipt receipt) =>
        Verify(receipt, Hash);

    internal static KukaSimOfficeLiteVirtualLoopVerificationResult Verify(
        KukaSimOfficeLiteVirtualLoopReceipt receipt,
        Func<string, string> computeFileSha256)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(computeFileSha256);
        var errors = new List<string>();
        var payload = receipt.Payload;
        if (receipt.SchemaIdentity != KukaSimOfficeLiteVirtualLoopContract.ReceiptSchemaIdentity
            || receipt.SchemaVersion != KukaSimOfficeLiteVirtualLoopContract.ReceiptSchemaVersion) errors.Add("receipt schema identity/version is unsupported");
        if (ReceiptSerialization.ComputeCanonicalSha256(payload) != receipt.PayloadSha256) errors.Add("payload SHA-256 mismatch");
        var accepted = payload.TerminalClassification == EnvironmentTerminalClassification.Ready;
        if (payload.PhysicalControllerContacted || payload.CredentialsUsed || payload.LicenseOperationPerformed) errors.Add("forbidden side-effect flags must remain false");
        if (payload.TransactionRoot != KukaSimOfficeLiteVirtualLoopContract.TransactionRoot
            || payload.ProgramPath != KukaSimOfficeLiteVirtualLoopContract.ProgramPath
            || payload.SnapshotName != KukaSimOfficeLiteVirtualLoopContract.DefaultSnapshotName
            || payload.EmbeddedKukaSimScriptSha256 != KukaSimOfficeLiteVirtualLoopContract.EmbeddedKukaSimScriptSha256
            || payload.EmbeddedWorkVisualScriptSha256 != KukaSimOfficeLiteVirtualLoopContract.EmbeddedWorkVisualScriptSha256) errors.Add("fixed transaction/snapshot/script identity mismatch");
        if (string.IsNullOrWhiteSpace(payload.SynchronizedLayoutPath)
            || !Path.IsPathRooted(payload.SynchronizedLayoutPath)) errors.Add("controller-synchronized layout identity is missing");
        if (!payload.TemporaryProbeScriptsRemoved || File.Exists(payload.MaterializedKukaSimScriptPath) || File.Exists(payload.MaterializedWorkVisualScriptPath)) errors.Add("regenerable materialized probe scripts were not removed");
        if (payload.PreExistingProcesses.Count != 0) errors.Add("receipt attached to pre-existing KUKA.Sim state");
        if (accepted && (payload.LifecycleReceipt is null || !OfficeLiteCycleReceiptVerifier.Verify(payload.LifecycleReceipt).Succeeded
            || !payload.LifecycleReceipt.Payload.CleanShutdownVerified)) errors.Add("nested OfficeLite lifecycle/clean shutdown is invalid");
        if (accepted && (!payload.SnapshotRestored || !Command(payload.SnapshotInventoryCommand)
            || !Command(payload.SnapshotRestoreCommand) || !Command(payload.FinalVmListCommand))) errors.Add("snapshot inventory/restore/final VM state is invalid");
        if (accepted && (!payload.CompositeConnectionValidated || !payload.DisconnectReconnectValidated
            || !payload.FailureInjectionValidated || !payload.HighLevelStartValidated || !payload.EnvironmentReusable)) errors.Add("accepted loop capability flags are incomplete");
        if (accepted && (!payload.WorkVisual.PreCleanupVerified || !payload.WorkVisual.PrepareVerified || !payload.WorkVisual.CleanupVerified
            || !payload.WorkVisual.UploadVerified || !payload.WorkVisual.GoConfirmed || !payload.WorkVisual.NoRelatedErrors)) errors.Add("WorkVisual transaction evidence is incomplete");
        if (accepted && (payload.KukaSimCommands.Count != 3 || payload.KukaSimCommands.Any(command => !command.ProcessStarted || command.ExitCode != 0
            || command.TimedOut || command.ForcedTerminationUsed || !command.CleanupVerified))) errors.Add("the three owned KUKA.Sim command observations are invalid");
        if (accepted && KukaSimOfficeLiteVirtualLoopRunner.ValidateResult(payload.Result).Count != 0) errors.Add("raw result does not satisfy the virtual-loop assertions");
        if (payload.Result is not null && ReceiptSerialization.ComputeCanonicalSha256(payload.Result) != payload.ResultCanonicalSha256) errors.Add("parsed result canonical SHA-256 mismatch");
        if (payload.CompletedAtUtc < payload.StartedAtUtc || payload.DurationMilliseconds < 0) errors.Add("receipt timing is invalid");
        var duplicates = payload.Files.GroupBy(file => file.Id, StringComparer.Ordinal).Where(group => group.Count() != 1).Select(group => group.Key).ToList();
        if (duplicates.Count != 0) errors.Add("duplicate file observations: " + string.Join(",", duplicates));
        foreach (var file in payload.Files.Where(file => file.Exists))
        {
            if (!File.Exists(file.Path) || file.Sha256 is null || computeFileSha256(file.Path) != file.Sha256) errors.Add("current evidence drifted: " + file.Id);
        }
        if (accepted)
        {
            var required = new[] { "workvisual-runner", "kukasim-engine", "kukasim-launcher", "kukasim-bootstrap-plugin", "netfx-csharp-compiler", "kukasim-online-assembly", "kukasim-vrc-wrapper", "exact-c01-component", "controller-synchronized-layout", "exact-c01-valid-src", "exact-c01-valid-dat", "workvisual-preclean-stdout", "workvisual-preclean-stderr", "workvisual-prepare-stdout", "workvisual-prepare-stderr", "workvisual-cleanup-stdout", "workvisual-cleanup-stderr",
                "kukasim-officelite-result-valid-1", "kukasim-officelite-trace-valid-1", "kukasim-officelite-result-valid-2", "kukasim-officelite-trace-valid-2", "kukasim-officelite-result-negative-0", "kukasim-officelite-trace-negative-0" };
            foreach (var id in required) if (payload.Files.Count(file => file.Id == id && file.Exists) != 1) errors.Add("required evidence must occur exactly once: " + id);
            if (payload.RawResultPaths.Count != 3 || payload.RawTracePaths.Count != 3) errors.Add("exactly three raw result/trace paths are required");
            if (payload.RawResultPaths.Count == 3 && payload.RawResultPaths.All(File.Exists))
            {
                try
                {
                    var parsed = KukaSimOfficeLiteRawParser.Aggregate(payload.RawResultPaths.Select(KukaSimOfficeLiteRawParser.Parse).ToList());
                    if (ReceiptSerialization.ComputeCanonicalSha256(parsed) != payload.ResultCanonicalSha256) errors.Add("raw result no longer corroborates parsed payload");
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException or FormatException)
                {
                    errors.Add("raw result parse failed: " + exception.Message);
                }
            }
        }
        if (payload.UnsupportedGaps is null || !payload.UnsupportedGaps.SequenceEqual(KukaSimOfficeLiteVirtualLoopContract.RequiredUnsupportedGaps, StringComparer.Ordinal)) errors.Add("unsupported-gap declarations are incomplete or reordered");
        return new KukaSimOfficeLiteVirtualLoopVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }
    private static bool Command(VmrunCommandObservation command) => !command.TimedOut && command.ExitCode == 0;
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}

public static class KukaSimOfficeLiteVirtualLoopReceiptWriter
{
    public static string WriteNew(string outputPath, KukaSimOfficeLiteVirtualLoopReceipt receipt)
    {
        var verification = KukaSimOfficeLiteVirtualLoopReceiptVerifier.Verify(receipt);
        if (!verification.Succeeded)
            throw new InvalidOperationException(
                "KUKA.Sim/OfficeLite virtual-loop receipt integrity is invalid: "
                + string.Join("; ", verification.Errors));
        var full = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetExtension(full), ".json", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Receipt output must use .json.", nameof(outputPath));
        Directory.CreateDirectory(Path.GetDirectoryName(full) ?? throw new ArgumentException("Receipt output has no parent."));
        using var stream = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(ReceiptSerialization.ToJson(receipt));
        writer.WriteLine();
        writer.Flush();
        stream.Flush(true);
        return full;
    }
}
