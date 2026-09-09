using System.Text;
using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class OfficeLiteActiveProjectDownloadTests
{
    [Fact]
    public void Ready_cycle_requires_negative_control_downloads_active_wvs_and_soft_stops()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var officeLitePlatform = ReadyOfficeLite(time, environment.PrimaryVmxPath);
        var workVisual = new DownloadWorkVisualPlatform(createNegativeArtifact: false, liveSucceeds: true);
        var outcome = new OfficeLiteActiveProjectDownloadRunner(
            new OfficeLiteCycleRunner(officeLitePlatform, time),
            workVisual,
            time).Run(environment.CreateRequest(), "test-active-project-download-ready");

        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(0, outcome.ExitCode);
        Assert.True(outcome.Receipt.Payload.ControllerToPcDownloadPerformed);
        Assert.True(outcome.Receipt.Payload.ReadOnlyControllerOperation);
        Assert.False(outcome.Receipt.Payload.PhysicalControllerContacted);
        Assert.False(outcome.Receipt.Payload.GuestIpOverrideUsed);
        Assert.False(outcome.Receipt.Payload.CredentialsUsed);
        Assert.False(outcome.Receipt.Payload.ControllerProjectModified);
        Assert.False(outcome.Receipt.Payload.ProjectOpenedOrSaved);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.NativeKssStatus);
        Assert.Equal("InitialProject (1)", outcome.Receipt.Payload.ActiveProject);
        Assert.Equal(1, outcome.Receipt.Payload.ProjectCount);
        Assert.True(outcome.Receipt.Payload.DownloadedProject.Exists);
        Assert.EndsWith(".wvs", outcome.Receipt.Payload.DownloadedProject.Path, StringComparison.OrdinalIgnoreCase);
        Assert.True(outcome.Receipt.Payload.DownloadedProject.Bytes > 0);
        Assert.True(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.True(outcome.Receipt.Payload.LifecycleReceipt.Payload.CleanShutdownVerified);
        Assert.False(officeLitePlatform.Running);
        Assert.Equal(2, workVisual.Invocations.Count);
        Assert.Contains("-address=127.0.0.1", workVisual.Invocations[0]);
        Assert.Contains("-address=198.51.100.128", workVisual.Invocations[1]);
        Assert.True(OfficeLiteActiveProjectDownloadReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Typed_live_failure_is_blocked_and_still_soft_stops()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var officeLitePlatform = ReadyOfficeLite(time, environment.PrimaryVmxPath);
        var outcome = new OfficeLiteActiveProjectDownloadRunner(
            new OfficeLiteCycleRunner(officeLitePlatform, time),
            new DownloadWorkVisualPlatform(createNegativeArtifact: false, liveSucceeds: false),
            time).Run(environment.CreateRequest(), "test-active-project-download-blocked");

        Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
        Assert.False(outcome.Receipt.Payload.ControllerToPcDownloadPerformed);
        Assert.True(outcome.Receipt.Payload.LifecycleReceipt.Payload.CleanShutdownVerified);
        Assert.False(officeLitePlatform.Running);
        Assert.True(OfficeLiteActiveProjectDownloadReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Negative_control_artifact_fails_closed_before_live_call()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var officeLitePlatform = ReadyOfficeLite(time, environment.PrimaryVmxPath);
        var workVisual = new DownloadWorkVisualPlatform(createNegativeArtifact: true, liveSucceeds: true);
        var outcome = new OfficeLiteActiveProjectDownloadRunner(
            new OfficeLiteCycleRunner(officeLitePlatform, time),
            workVisual,
            time).Run(environment.CreateRequest(), "test-active-project-download-negative-artifact");

        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.Single(workVisual.Invocations);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "negative-no-file" && check.Status == EnvironmentCheckStatus.Failed);
        Assert.True(outcome.Receipt.Payload.LifecycleReceipt.Payload.CleanShutdownVerified);
        Assert.False(officeLitePlatform.Running);
    }

    [Fact]
    public void Verifier_rejects_safety_claim_and_downloaded_file_drift()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var receipt = new OfficeLiteActiveProjectDownloadRunner(
            new OfficeLiteCycleRunner(ReadyOfficeLite(time, environment.PrimaryVmxPath), time),
            new DownloadWorkVisualPlatform(createNegativeArtifact: false, liveSucceeds: true),
            time).Run(environment.CreateRequest(), "test-active-project-download-tamper").Receipt;
        var unsafePayload = receipt.Payload with { ControllerProjectModified = true };
        var unsafeReceipt = receipt with
        {
            Payload = unsafePayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(unsafePayload)
        };

        Assert.False(OfficeLiteActiveProjectDownloadReceiptVerifier.Verify(unsafeReceipt).Succeeded);

        File.AppendAllText(receipt.Payload.DownloadedProject.Path, "tampered", Encoding.UTF8);
        var verification = OfficeLiteActiveProjectDownloadReceiptVerifier.Verify(receipt);
        Assert.False(verification.Succeeded);
        Assert.Contains(verification.Errors, error => error.Contains("downloaded-active-project", StringComparison.Ordinal));
    }

    [Fact]
    public void Explicit_guest_address_is_rejected_before_vm_or_evidence_side_effects()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var officeLitePlatform = ReadyOfficeLite(time, environment.PrimaryVmxPath);
        var workVisual = new DownloadWorkVisualPlatform(createNegativeArtifact: false, liveSucceeds: true);
        var request = environment.CreateRequest() with
        {
            OfficeLite = environment.CreateRequest().OfficeLite with { GuestIpAddress = "192.0.2.147" }
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            new OfficeLiteActiveProjectDownloadRunner(
                new OfficeLiteCycleRunner(officeLitePlatform, time),
                workVisual,
                time).Run(request, "test-active-project-download-explicit-address"));

        Assert.Contains("forbids", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(officeLitePlatform.Running);
        Assert.Empty(workVisual.Invocations);
        Assert.False(Directory.Exists(request.EvidenceDirectory));
    }

    private static FakeOfficeLitePlatform ReadyOfficeLite(ManualTimeProvider time, string vmxPath) =>
        new(time, vmxPath)
        {
            ProbeReady = true,
            OpenServicePorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToHashSet()
        };

    private sealed class DownloadWorkVisualPlatform(bool createNegativeArtifact, bool liveSucceeds)
        : IWorkVisualRunnerPlatform
    {
        public List<List<string>> Invocations { get; } = [];

        public WorkVisualProcessResult Run(
            string executablePath,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TimeSpan timeout)
        {
            Invocations.Add(arguments.ToList());
            var address = arguments.Single(argument => argument.StartsWith("-address=", StringComparison.Ordinal))[9..];
            var destination = arguments.Single(argument => argument.StartsWith("-destinationfolder=", StringComparison.Ordinal))[19..];
            if (address == "127.0.0.1")
            {
                if (createNegativeArtifact)
                {
                    File.WriteAllText(Path.Combine(destination, "unexpected.wvs"), "unexpected");
                }

                return FailureResult();
            }

            if (!liveSucceeds)
            {
                return FailureResult();
            }

            var path = Path.Combine(destination, "InitialProject.wvs");
            File.WriteAllBytes(path, [0x57, 0x56, 0x53, 0x01]);
            var active = Convert.ToBase64String(Encoding.UTF8.GetBytes("InitialProject (1)"));
            var encodedPath = Convert.ToBase64String(Encoding.UTF8.GetBytes(path));
            return new WorkVisualProcessResult(
                0,
                false,
                true,
                10,
                $"ActiveProjectBase64={active}\r\nDownloadedProjectPathBase64={encodedPath}\r\nProjectCount=1\r\n",
                string.Empty);
        }

        private static WorkVisualProcessResult FailureResult() =>
            new(
                42,
                false,
                true,
                10,
                "ACTIVE_PROJECT_DOWNLOAD_FAILED: Kuka.WorkVisual.Scripting.KrcOnline.OnlineScriptingException: unreachable",
                string.Empty);
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
            var root = Path.Combine(Path.GetTempPath(), $"kuka-lab-active-download-{Guid.NewGuid():N}");
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

        public OfficeLiteActiveProjectDownloadRequest CreateRequest() => new()
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
