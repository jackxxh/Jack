using System.Text;
using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class OfficeLiteControllerProfileReadbackTests
{
    [Fact]
    public void Ready_cycle_reads_controller_profile_and_project_identity_then_soft_stops()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var officeLitePlatform = new FakeOfficeLitePlatform(time, environment.PrimaryVmxPath)
        {
            ProbeReady = true,
            OpenServicePorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToHashSet()
        };
        var workVisualPlatform = new QueueWorkVisualPlatform(FailureResult(), SuccessResult());
        var runner = new OfficeLiteControllerProfileReadbackRunner(
            new OfficeLiteCycleRunner(officeLitePlatform, time),
            workVisualPlatform,
            time);

        var outcome = runner.Run(environment.CreateRequest(), "test-controller-profile-ready");

        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(0, outcome.ExitCode);
        Assert.True(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.True(outcome.Receipt.Payload.LifecycleReceipt.Payload.CleanShutdownVerified);
        Assert.False(officeLitePlatform.Running);
        Assert.Equal(2, workVisualPlatform.Invocations.Count);
        Assert.Contains("-address=127.0.0.1", workVisualPlatform.Invocations[0]);
        Assert.Contains("-address=198.51.100.128", workVisualPlatform.Invocations[1]);
        Assert.Equal(NativeKssCandidateSubmissionContract.RobotType, outcome.Receipt.Payload.Profile.RobotType);
        Assert.Equal("8.7.8", outcome.Receipt.Payload.Profile.KssVersion);
        Assert.Equal("deployment.analysis-copy", outcome.Receipt.Payload.Profile.CurrentProjectName);
        Assert.Equal("deployment.analysis-copy", outcome.Receipt.Payload.Profile.ActiveProject);
        Assert.Equal("Base", outcome.Receipt.Payload.Profile.BaseProject);
        Assert.Equal("Initial", outcome.Receipt.Payload.Profile.InitialProject);
        Assert.Equal(3, outcome.Receipt.Payload.Profile.ProjectCount);
        Assert.False(outcome.Receipt.Payload.CredentialsUsed);
        Assert.False(outcome.Receipt.Payload.ControllerMutationPerformed);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.NativeKssStatus);
        Assert.True(OfficeLiteControllerProfileReadbackReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Receipt_verifier_rejects_rehashed_profile_tamper_against_raw_stdout()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var officeLitePlatform = new FakeOfficeLitePlatform(time, environment.PrimaryVmxPath)
        {
            ProbeReady = true,
            OpenServicePorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToHashSet()
        };
        var runner = new OfficeLiteControllerProfileReadbackRunner(
            new OfficeLiteCycleRunner(officeLitePlatform, time),
            new QueueWorkVisualPlatform(FailureResult(), SuccessResult()),
            time);
        var receipt = runner.Run(
            environment.CreateRequest(),
            "test-controller-profile-tamper").Receipt;
        var tamperedPayload = receipt.Payload with
        {
            Profile = receipt.Payload.Profile with { RobotType = "KR 3 R540" }
        };
        var tampered = receipt with
        {
            Payload = tamperedPayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(tamperedPayload)
        };

        var verification = OfficeLiteControllerProfileReadbackReceiptVerifier.Verify(tampered);

        Assert.False(verification.Succeeded);
        Assert.Contains(verification.Errors, error => error.Contains("raw WorkVisual output", StringComparison.Ordinal));
    }

    private static WorkVisualProcessResult FailureResult() =>
        new(
            42,
            false,
            true,
            10,
            "CONTROLLER_PROFILE_READBACK_FAILED: Kuka.WorkVisual.Scripting.KrcOnline.OnlineScriptingException: unreachable",
            string.Empty);

    private static WorkVisualProcessResult SuccessResult() =>
        new(
            0,
            false,
            true,
            10,
            string.Join(
                "\r\n",
                "CONTROLLER_PROFILE_READBACK_BEGIN=True",
                $"PROFILE_ROBOT_B64={Encode(NativeKssCandidateSubmissionContract.RobotType)}",
                $"PROFILE_KSS_B64={Encode("8.7.8")}",
                $"PROFILE_PROJECT_B64={Encode("deployment.analysis-copy")}",
                $"ACTIVE_PROJECT_B64={Encode("deployment.analysis-copy")}",
                $"BASE_PROJECT_B64={Encode("Base")}",
                $"INITIAL_PROJECT_B64={Encode("Initial")}",
                "PROJECT_COUNT=3"),
            string.Empty);

    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private sealed class QueueWorkVisualPlatform(params WorkVisualProcessResult[] results) : IWorkVisualRunnerPlatform
    {
        private readonly Queue<WorkVisualProcessResult> _results = new(results);

        public List<List<string>> Invocations { get; } = [];

        public WorkVisualProcessResult Run(
            string executablePath,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TimeSpan timeout)
        {
            Invocations.Add(arguments.ToList());
            return _results.Dequeue();
        }
    }

    private sealed class FakeOfficeLitePlatform(ManualTimeProvider time, string vmxPath) : IOfficeLiteHostPlatform
    {
        public bool Running { get; set; }

        public bool ProbeReady { get; init; }

        public HashSet<int> OpenServicePorts { get; init; } = [];

        public VmrunExecutionResult RunVmrun(
            string vmrunPath,
            IReadOnlyList<string> arguments,
            TimeSpan timeout)
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
            DateTimeOffset observedAtUtc) =>
            ProbeReady
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
            DateTimeOffset observedAtUtc) =>
            ports.Select(port => new GuestTcpPortProbeResult(
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
        private DateTimeOffset _utcNow = new(2026, 8, 29, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }

    private sealed class TestEnvironment : IDisposable
    {
        private const string RelativeVmx =
            "OfficeLite-Work/8.7.8-build04/runs/primary/KR C, V8.7.8OL_Build04.vmx";

        private TestEnvironment(string root, string primaryVmxPath, string runnerPath)
        {
            Root = root;
            PrimaryVmxPath = primaryVmxPath;
            RunnerPath = runnerPath;
        }

        public string Root { get; }

        public string PrimaryVmxPath { get; }

        private string RunnerPath { get; }

        public static TestEnvironment Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"kuka-lab-controller-profile-{Guid.NewGuid():N}");
            var vmware = Path.Combine(root, "VMware");
            Directory.CreateDirectory(vmware);
            File.WriteAllText(Path.Combine(vmware, "vmrun.exe"), "synthetic vmrun");
            var primaryVmx = Path.GetFullPath(Path.Combine(
                root,
                RelativeVmx.Replace('/', Path.DirectorySeparatorChar)));
            Directory.CreateDirectory(Path.GetDirectoryName(primaryVmx)!);
            File.WriteAllLines(
                primaryVmx,
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
            return new TestEnvironment(root, primaryVmx, runnerPath);
        }

        public OfficeLiteControllerProfileReadbackRequest CreateRequest() => new()
        {
            OfficeLite = OfficeLiteCycleRequest.CreateDefault(Root, readinessTimeoutSeconds: 5) with
            {
                ProbeIntervalMilliseconds = 500,
                ShutdownTimeoutSeconds = 2,
                DiagnoseWorkVisualServices = true,
                ServiceObservationSeconds = 1,
                ServiceProbeIntervalMilliseconds = 500,
                ServiceConnectTimeoutMilliseconds = 250
            },
            RunnerPath = RunnerPath,
            EvidenceDirectory = Path.Combine(Root, "evidence"),
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
