using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class OfficeLiteRepositoryInventoryTests
{
    [Fact]
    public void Ready_cycle_runs_differential_read_only_inventory_and_soft_stops()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var officeLite = new FakeOfficeLitePlatform(time, environment.PrimaryVmxPath)
        {
            ProbeReady = true,
            OpenServicePorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToHashSet()
        };
        var workVisual = new QueueWorkVisualPlatform(FailureResult(), SuccessResult());
        var outcome = new OfficeLiteRepositoryInventoryRunner(
            new OfficeLiteCycleRunner(officeLite, time),
            workVisual,
            time).Run(environment.CreateRequest(), "test-repository-inventory-ready");

        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(0, outcome.ExitCode);
        Assert.False(officeLite.Running);
        Assert.True(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.Equal(2, workVisual.Invocations.Count);
        Assert.Contains("-address=127.0.0.1", workVisual.Invocations[0]);
        Assert.Contains("-repositorypath=KRC:\\R1\\Program", workVisual.Invocations[1]);
        Assert.Equal(4, outcome.Receipt.Payload.Repository.FileCount);
        Assert.Equal(4, outcome.Receipt.Payload.Repository.Files.Count);
        Assert.False(outcome.Receipt.Payload.CredentialsUsed);
        Assert.False(outcome.Receipt.Payload.FileContentsRead);
        Assert.False(outcome.Receipt.Payload.RepositoryMutationPerformed);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.NativeKssStatus);
        Assert.True(OfficeLiteRepositoryInventoryReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Missing_repository_path_is_blocked_and_still_soft_stops()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var officeLite = new FakeOfficeLitePlatform(time, environment.PrimaryVmxPath)
        {
            ProbeReady = true,
            OpenServicePorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToHashSet()
        };
        var workVisual = new QueueWorkVisualPlatform(FailureResult(), MissingPathResult());
        var outcome = new OfficeLiteRepositoryInventoryRunner(
            new OfficeLiteCycleRunner(officeLite, time),
            workVisual,
            time).Run(environment.CreateRequest(), "test-repository-inventory-missing");

        Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(3, outcome.ExitCode);
        Assert.False(officeLite.Running);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "live-query" && check.Status == EnvironmentCheckStatus.Blocked);
        Assert.True(OfficeLiteRepositoryInventoryReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Receipt_verifier_rejects_rehashed_mutation_claim_and_evidence_tamper()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var officeLite = new FakeOfficeLitePlatform(time, environment.PrimaryVmxPath)
        {
            ProbeReady = true,
            OpenServicePorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToHashSet()
        };
        var receipt = new OfficeLiteRepositoryInventoryRunner(
            new OfficeLiteCycleRunner(officeLite, time),
            new QueueWorkVisualPlatform(FailureResult(), SuccessResult()),
            time).Run(environment.CreateRequest(), "test-repository-inventory-tamper").Receipt;
        var unsafePayload = receipt.Payload with { RepositoryMutationPerformed = true };
        var unsafeReceipt = receipt with
        {
            Payload = unsafePayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(unsafePayload)
        };

        Assert.False(OfficeLiteRepositoryInventoryReceiptVerifier.Verify(unsafeReceipt).Succeeded);

        var stdout = receipt.Payload.Files.Single(file => file.Id == "workvisual-live-stdout");
        File.AppendAllText(stdout.Path, "tampered");
        var verification = OfficeLiteRepositoryInventoryReceiptVerifier.Verify(receipt);
        Assert.False(verification.Succeeded);
        Assert.Contains(verification.Errors, error => error.Contains("workvisual-live-stdout", StringComparison.Ordinal));
    }

    private static WorkVisualProcessResult FailureResult() =>
        new(
            42,
            false,
            true,
            10,
            "REPOSITORY_INVENTORY_FAILED: KukaRoboter.OnlineServicesFacade.OnlineServiceFaultException: unreachable",
            string.Empty);

    private static WorkVisualProcessResult MissingPathResult() =>
        new(
            43,
            false,
            true,
            10,
            "DirectoryExists=False\r\nREPOSITORY_INVENTORY_PATH_MISSING: KRC:\\R1\\Program\r\n",
            string.Empty);

    private static WorkVisualProcessResult SuccessResult() =>
        new(
            0,
            false,
            true,
            10,
            string.Join(
                "\r\n",
                [
                    "DirectoryExists=True",
                    "DirectoryCount=0",
                    "FileCount=4",
                    "FileBase64=S1JDOlxSMVxQcm9ncmFtXENvbGxEZXRlY3RfVXNlckFjdGlvbi5kYXQ=",
                    "FileBase64=S1JDOlxSMVxQcm9ncmFtXENvbGxEZXRlY3RfVXNlckFjdGlvbi5zcmM=",
                    "FileBase64=S1JDOlxSMVxQcm9ncmFtXG1hc3JlZl91c2VyLmRhdA==",
                    "FileBase64=S1JDOlxSMVxQcm9ncmFtXG1hc3JlZl91c2VyLnNyYw==",
                    string.Empty
                ]),
            string.Empty);

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
        private DateTimeOffset _utcNow = new(2026, 8, 28, 0, 0, 0, TimeSpan.Zero);

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
            var root = Path.Combine(Path.GetTempPath(), $"kuka-lab-repository-inventory-{Guid.NewGuid():N}");
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

        public OfficeLiteRepositoryInventoryRequest CreateRequest() => new()
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
            RepositoryPath = OfficeLiteRepositoryInventoryContract.DefaultRepositoryPath,
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
