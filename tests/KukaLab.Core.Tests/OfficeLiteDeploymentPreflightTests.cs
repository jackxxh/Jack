using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class OfficeLiteDeploymentPreflightTests
{
    [Fact]
    public void Ready_preflight_preserves_exact_option_blockers_without_deploying_or_mutating_source()
    {
        using var environment = TestEnvironment.Create();
        var sourceHash = Sha256(environment.ProjectPath);
        var time = new ManualTimeProvider();
        var officeLite = new FakeOfficeLitePlatform(time, environment.PrimaryVmxPath)
        {
            ProbeReady = true,
            OpenServicePorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToHashSet()
        };
        var workVisual = new QueueWorkVisualPlatform(
            FailureResult(),
            ConflictResult(),
            mutateAnalysisCopies: true);
        var outcome = new OfficeLiteDeploymentPreflightRunner(
            new OfficeLiteCycleRunner(officeLite, time),
            workVisual,
            time).Run(environment.CreateRequest(), "test-deployment-preflight-ready");

        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(0, outcome.ExitCode);
        Assert.True(outcome.Receipt.Payload.SourceProjectUnchanged);
        Assert.Equal(sourceHash, Sha256(environment.ProjectPath));
        Assert.False(outcome.Receipt.Payload.DeploymentExecuted);
        Assert.False(outcome.Receipt.Payload.Preflight.DeploymentExecuted);
        Assert.False(outcome.Receipt.Payload.Preflight.DeploymentReady);
        Assert.Equal("KR 210 R2700-2 C01", outcome.Receipt.Payload.Preflight.SourceRobot);
        Assert.Equal(2, outcome.Receipt.Payload.Preflight.ProjectOptions.Count);
        Assert.Equal(4, outcome.Receipt.Payload.Preflight.Conflicts.Count);
        Assert.Contains(
            outcome.Receipt.Payload.Preflight.Conflicts,
            conflict => conflict.Type == "OptionVersionMismatch"
                && conflict.Description.Contains("LoadDataDetermination", StringComparison.Ordinal));
        Assert.Contains(
            outcome.Receipt.Payload.Preflight.Conflicts,
            conflict => conflict.Type == "OptionVersionMismatch"
                && conflict.Description.Contains("KUKA.PROFINET S", StringComparison.Ordinal));
        Assert.True(outcome.Receipt.Payload.LifecycleReceipt.Payload.CleanShutdownVerified);
        Assert.False(officeLite.Running);
        Assert.Equal(2, workVisual.Invocations.Count);
        Assert.True(OfficeLiteDeploymentPreflightReceiptVerifier.Verify(outcome.Receipt).Succeeded);

        var json = ReceiptSerialization.ToJson(outcome.Receipt);
        var readback = ReceiptSerialization.OfficeLiteDeploymentPreflightFromJson(json);
        Assert.True(OfficeLiteDeploymentPreflightReceiptVerifier.Verify(readback).Succeeded);
    }

    [Fact]
    public void Receipt_verifier_rejects_rehashed_deployment_execution_claim()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var receipt = new OfficeLiteDeploymentPreflightRunner(
            new OfficeLiteCycleRunner(new FakeOfficeLitePlatform(time, environment.PrimaryVmxPath)
            {
                ProbeReady = true,
                OpenServicePorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToHashSet()
            }, time),
            new QueueWorkVisualPlatform(FailureResult(), ConflictResult()),
            time).Run(environment.CreateRequest(), "test-deployment-preflight-tamper").Receipt;
        var unsafePayload = receipt.Payload with { DeploymentExecuted = true };
        var unsafeReceipt = receipt with
        {
            Payload = unsafePayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(unsafePayload)
        };

        var verification = OfficeLiteDeploymentPreflightReceiptVerifier.Verify(unsafeReceipt);

        Assert.False(verification.Succeeded);
        Assert.Contains(verification.Errors, error => error.Contains("no-deploy safety", StringComparison.Ordinal));
    }

    [Fact]
    public void Existing_evidence_directory_fails_before_starting_officelite_and_still_yields_valid_receipt()
    {
        using var environment = TestEnvironment.Create();
        Directory.CreateDirectory(environment.EvidenceDirectory);
        var time = new ManualTimeProvider();
        var officeLite = new FakeOfficeLitePlatform(time, environment.PrimaryVmxPath);
        var workVisual = new QueueWorkVisualPlatform();

        var outcome = new OfficeLiteDeploymentPreflightRunner(
            new OfficeLiteCycleRunner(officeLite, time),
            workVisual,
            time).Run(environment.CreateRequest(), "test-deployment-preflight-existing-evidence");

        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.Empty(workVisual.Invocations);
        Assert.False(officeLite.Running);
        Assert.True(OfficeLiteDeploymentPreflightReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    private static WorkVisualProcessResult FailureResult() => new(
        42,
        false,
        true,
        10,
        "DEPLOYMENT_PREFLIGHT_FAILED: Kuka.WorkVisual.Scripting.KrcOnline.OnlineScriptingException: unreachable\r\nDEPLOYMENT_EXECUTED=false\r\n",
        string.Empty);

    private static WorkVisualProcessResult ConflictResult() => new(
        0,
        false,
        true,
        10,
        string.Join("\r\n",
        [
            "SOURCE_CONTROLLER=810142756",
            "SOURCE_ADDRESS=192.0.2.147",
            "SOURCE_FIRMWARE=8.7.8",
            "SOURCE_ROBOT=KR 210 R2700-2 C01",
            "TARGET_ADDRESS=198.51.100.128",
            "ACTIVE_OPTION_PROFILE=Default",
            "DOWNLOADED_OPTIONS_DIRECTORY=C:\\Options",
            "PROJECT_OPTION_COUNT=2",
            "PROJECT_OPTION_0=LoadDataDetermination|7.2.10.285",
            "PROJECT_OPTION_1=KUKA.PROFINET S|6.0.1.20",
            "INSTALLED_OPTION_PACKAGE_COUNT=0",
            "HAS_CONFLICTS=true",
            "CAN_EXECUTE=false",
            "CONFLICT_COUNT=4",
            "CONFLICT_0_TYPE=CellNameMismatch",
            "CONFLICT_0_TITLE=Cell name differs",
            "CONFLICT_0_DESCRIPTION=Target cell differs",
            "CONFLICT_0_RESOLUTION_COUNT=1",
            "CONFLICT_0_HAS_DEFAULT=true",
            "CONFLICT_1_TYPE=ControllerNameMismatch",
            "CONFLICT_1_TITLE=Controller name differs",
            "CONFLICT_1_DESCRIPTION=Target controller differs",
            "CONFLICT_1_RESOLUTION_COUNT=1",
            "CONFLICT_1_HAS_DEFAULT=true",
            "CONFLICT_2_TYPE=OptionVersionMismatch",
            "CONFLICT_2_TITLE=Option missing",
            "CONFLICT_2_DESCRIPTION=LoadDataDetermination 7.2.10.285 is missing",
            "CONFLICT_2_RESOLUTION_COUNT=0",
            "CONFLICT_2_HAS_DEFAULT=false",
            "CONFLICT_3_TYPE=OptionVersionMismatch",
            "CONFLICT_3_TITLE=Option missing",
            "CONFLICT_3_DESCRIPTION=KUKA.PROFINET S 6.0.1.20 is missing",
            "CONFLICT_3_RESOLUTION_COUNT=0",
            "CONFLICT_3_HAS_DEFAULT=false",
            "DEPLOYMENT_EXECUTED=false",
            string.Empty
        ]),
        string.Empty);

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
    }

    private sealed class QueueWorkVisualPlatform : IWorkVisualRunnerPlatform
    {
        private readonly Queue<WorkVisualProcessResult> _results;
        private readonly bool _mutateAnalysisCopies;

        public QueueWorkVisualPlatform(params WorkVisualProcessResult[] results)
            : this(results, false)
        {
        }

        public QueueWorkVisualPlatform(WorkVisualProcessResult first, WorkVisualProcessResult second, bool mutateAnalysisCopies)
            : this([first, second], mutateAnalysisCopies)
        {
        }

        private QueueWorkVisualPlatform(IEnumerable<WorkVisualProcessResult> results, bool mutateAnalysisCopies)
        {
            _results = new Queue<WorkVisualProcessResult>(results);
            _mutateAnalysisCopies = mutateAnalysisCopies;
        }

        public List<List<string>> Invocations { get; } = [];

        public WorkVisualProcessResult Run(
            string executablePath,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TimeSpan timeout)
        {
            Invocations.Add(arguments.ToList());
            if (_mutateAnalysisCopies)
            {
                var projectArgument = arguments.Single(argument => argument.StartsWith("-project=", StringComparison.Ordinal));
                File.AppendAllText(projectArgument["-project=".Length..], "analysis-copy-rewrite");
            }
            return _results.Dequeue();
        }
    }

    private sealed class FakeOfficeLitePlatform(ManualTimeProvider time, string vmxPath) : IOfficeLiteHostPlatform
    {
        public bool Running { get; set; }
        public bool ProbeReady { get; init; }
        public HashSet<int> OpenServicePorts { get; init; } = [];

        public VmrunExecutionResult RunVmrun(string vmrunPath, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            var command = arguments.Count >= 3 ? arguments[2] : string.Empty;
            return command switch
            {
                "list" => Result(Running
                    ? $"Total running VMs: 1{Environment.NewLine}{vmxPath}"
                    : "Total running VMs: 0"),
                "start" => Start(),
                "stop" => Stop(),
                _ => new VmrunExecutionResult(1, false, 1, string.Empty, "unsupported")
            };
        }

        public string? TryResolveDhcpAddress(string vmxPath, string dhcpLeasePath) => "198.51.100.128";

        public GuestProbeResult ProbeGuest(
            string? address,
            int port,
            int timeoutMilliseconds,
            DateTimeOffset observedAtUtc) => ProbeReady
                ? new GuestProbeResult(
                    0,
                    observedAtUtc,
                    address,
                    true,
                    true,
                    "ready",
                    new GuestTlsIdentity(
                        address!,
                        "KUKA Roboter GmbH",
                        "KUKA Roboter GmbH",
                        "4DCFB182A3EF5E53FC908D7999273595E02BC430",
                        "Tls12",
                        true))
                : new GuestProbeResult(0, observedAtUtc, address, true, false, "not-ready", null);

        public IReadOnlyList<GuestTcpPortProbeResult> ProbeTcpPorts(
            string? address,
            IReadOnlyList<int> ports,
            int timeoutMilliseconds,
            DateTimeOffset observedAtUtc) => ports.Select(port => new GuestTcpPortProbeResult(
                port,
                observedAtUtc,
                OpenServicePorts.Contains(port) ? GuestTcpPortState.Open : GuestTcpPortState.Timeout,
                OpenServicePorts.Contains(port) ? "Open" : "Timeout",
                1)).ToList();

        public void Delay(TimeSpan duration) => time.Advance(duration);

        private VmrunExecutionResult Start()
        {
            Running = true;
            return Result(string.Empty);
        }

        private VmrunExecutionResult Stop()
        {
            Running = false;
            return Result(string.Empty);
        }

        private static VmrunExecutionResult Result(string output) => new(0, false, 1, output, string.Empty);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 8, 28, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan duration) => _utcNow += duration;
    }

    private sealed class TestEnvironment : IDisposable
    {
        private const string RelativeVmx = "OfficeLite-Work/8.7.8-build04/runs/profile/KR210-C01.vmx";

        private TestEnvironment(string root, string primaryVmxPath, string runnerPath, string projectPath, string evidenceDirectory)
        {
            Root = root;
            PrimaryVmxPath = primaryVmxPath;
            RunnerPath = runnerPath;
            ProjectPath = projectPath;
            EvidenceDirectory = evidenceDirectory;
        }

        public string Root { get; }
        public string PrimaryVmxPath { get; }
        public string RunnerPath { get; }
        public string ProjectPath { get; }
        public string EvidenceDirectory { get; }

        public static TestEnvironment Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"kuka-lab-deployment-preflight-{Guid.NewGuid():N}");
            var vmware = Path.Combine(root, "VMware");
            Directory.CreateDirectory(vmware);
            File.WriteAllText(Path.Combine(vmware, "vmrun.exe"), "synthetic vmrun");
            var primaryVmx = Path.GetFullPath(Path.Combine(root, RelativeVmx.Replace('/', Path.DirectorySeparatorChar)));
            Directory.CreateDirectory(Path.GetDirectoryName(primaryVmx)!);
            File.WriteAllLines(primaryVmx,
            [
                "ethernet0.address = \"00:0C:29:3F:C7:D7\"",
                "isolation.tools.hgfs.disable = \"TRUE\"",
                "sharedFolder0.present = \"FALSE\"",
                "sharedFolder0.enabled = \"FALSE\"",
                "sharedFolder0.readAccess = \"FALSE\"",
                "sharedFolder0.writeAccess = \"FALSE\"",
                "sharedFolder.maxNum = \"0\"",
                "hgfs.mapRootShare = \"FALSE\""
            ]);
            var runnerPath = Path.Combine(root, "wvsr.exe");
            File.WriteAllText(runnerPath, "synthetic WorkVisual runner");
            var projectPath = Path.Combine(root, "controller-baseline.wvs");
            File.WriteAllText(projectPath, "synthetic protected WVS");
            return new TestEnvironment(root, primaryVmx, runnerPath, projectPath, Path.Combine(root, "evidence"));
        }

        public OfficeLiteDeploymentPreflightRequest CreateRequest() => new()
        {
            OfficeLite = OfficeLiteCycleRequest.CreateDefault(Root, readinessTimeoutSeconds: 5) with
            {
                VmxPath = PrimaryVmxPath,
                ProbeIntervalMilliseconds = 500,
                ShutdownTimeoutSeconds = 2,
                DiagnoseWorkVisualServices = true,
                ServiceObservationSeconds = 1,
                ServiceProbeIntervalMilliseconds = 500,
                ServiceConnectTimeoutMilliseconds = 250
            },
            RunnerPath = RunnerPath,
            ProjectPath = ProjectPath,
            EvidenceDirectory = EvidenceDirectory,
            RunnerTimeoutSeconds = 5
        };

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
