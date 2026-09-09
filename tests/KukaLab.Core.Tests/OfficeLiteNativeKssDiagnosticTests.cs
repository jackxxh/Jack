using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class OfficeLiteNativeKssDiagnosticTests
{
    [Fact]
    public void Ready_cycle_accepts_valid_rejects_invalid_and_restores_controller()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var officeLite = new FakeOfficeLitePlatform(time, environment.PrimaryVmxPath)
        {
            ProbeReady = true,
            OpenServicePorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToHashSet()
        };
        var workVisual = new QueueWorkVisualPlatform(NegativeResult(), ReadyResult());
        var outcome = new OfficeLiteNativeKssDiagnosticRunner(
            new OfficeLiteCycleRunner(officeLite, time),
            workVisual,
            time).Run(environment.CreateRequest(), "test-native-kss-ready");

        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(NativeKssStatus.SyntaxSelectionValidated, outcome.Receipt.Payload.NativeKssStatus);
        Assert.Equal(0, outcome.ExitCode);
        Assert.False(officeLite.Running);
        Assert.Equal(2, workVisual.Invocations.Count);
        Assert.Contains("-address=127.0.0.1", workVisual.Invocations[0]);
        Assert.Contains($"-transactionroot={OfficeLiteNativeKssDiagnosticContract.TransactionRoot}", workVisual.Invocations[1]);
        Assert.True(outcome.Receipt.Payload.Diagnostic.ValidAccepted);
        Assert.Empty(outcome.Receipt.Payload.Diagnostic.ValidErrors);
        Assert.True(outcome.Receipt.Payload.Diagnostic.InvalidRejected);
        var error = Assert.Single(outcome.Receipt.Payload.Diagnostic.InvalidErrors);
        Assert.Equal(2137, error.ErrorNumber);
        Assert.Equal(OfficeLiteNativeKssDiagnosticContract.ExpectedInvalidErrorLine, error.Line);
        Assert.Equal(6, error.Column);
        Assert.True(outcome.Receipt.Payload.Diagnostic.CleanupVerified);
        Assert.True(outcome.Receipt.Payload.ControllerMutationPerformed);
        Assert.True(outcome.Receipt.Payload.ControllerStateRestored);
        Assert.True(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.False(outcome.Receipt.Payload.ProgramStartRequested);
        Assert.False(outcome.Receipt.Payload.ProgramRunRequested);
        Assert.False(outcome.Receipt.Payload.MotionRequested);
        Assert.True(OfficeLiteNativeKssDiagnosticReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Cleanup_failure_is_failed_not_promoted_and_vm_still_soft_stops()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var officeLite = new FakeOfficeLitePlatform(time, environment.PrimaryVmxPath)
        {
            ProbeReady = true,
            OpenServicePorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToHashSet()
        };
        var outcome = new OfficeLiteNativeKssDiagnosticRunner(
            new OfficeLiteCycleRunner(officeLite, time),
            new QueueWorkVisualPlatform(NegativeResult(), CleanupFailureResult()),
            time).Run(environment.CreateRequest(), "test-native-kss-cleanup-failure");

        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.NativeKssStatus);
        Assert.Equal(2, outcome.ExitCode);
        Assert.False(officeLite.Running);
        Assert.False(outcome.Receipt.Payload.ControllerStateRestored);
        Assert.False(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "controller-transaction-cleanup" && check.Status == EnvironmentCheckStatus.Failed);
        Assert.True(OfficeLiteNativeKssDiagnosticReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Receipt_verifier_rejects_rehashed_start_claim_diagnostic_tamper_and_evidence_drift()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var officeLite = new FakeOfficeLitePlatform(time, environment.PrimaryVmxPath)
        {
            ProbeReady = true,
            OpenServicePorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToHashSet()
        };
        var receipt = new OfficeLiteNativeKssDiagnosticRunner(
            new OfficeLiteCycleRunner(officeLite, time),
            new QueueWorkVisualPlatform(NegativeResult(), ReadyResult()),
            time).Run(environment.CreateRequest(), "test-native-kss-tamper").Receipt;

        var startPayload = receipt.Payload with { ProgramStartRequested = true };
        var startReceipt = receipt with
        {
            Payload = startPayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(startPayload)
        };
        Assert.False(OfficeLiteNativeKssDiagnosticReceiptVerifier.Verify(startReceipt).Succeeded);

        var invalid = receipt.Payload.Diagnostic.InvalidErrors.Single() with { Line = 0 };
        var diagnosticPayload = receipt.Payload with
        {
            Diagnostic = receipt.Payload.Diagnostic with { InvalidErrors = [invalid] }
        };
        var diagnosticReceipt = receipt with
        {
            Payload = diagnosticPayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(diagnosticPayload)
        };
        Assert.False(OfficeLiteNativeKssDiagnosticReceiptVerifier.Verify(diagnosticReceipt).Succeeded);

        var plausibleWrongError = receipt.Payload.Diagnostic.InvalidErrors.Single() with { Line = 13 };
        var plausibleWrongPayload = receipt.Payload with
        {
            Diagnostic = receipt.Payload.Diagnostic with { InvalidErrors = [plausibleWrongError] }
        };
        var plausibleWrongReceipt = receipt with
        {
            Payload = plausibleWrongPayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(plausibleWrongPayload)
        };
        Assert.False(OfficeLiteNativeKssDiagnosticReceiptVerifier.Verify(plausibleWrongReceipt).Succeeded);

        var commandPayload = receipt.Payload with
        {
            LiveCommand = receipt.Payload.LiveCommand with { StandardOutputSha256 = new string('A', 64) }
        };
        var commandReceipt = receipt with
        {
            Payload = commandPayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(commandPayload)
        };
        Assert.False(OfficeLiteNativeKssDiagnosticReceiptVerifier.Verify(commandReceipt).Succeeded);

        var stdout = receipt.Payload.Files.Single(file => file.Id == "workvisual-live-stdout");
        File.AppendAllText(stdout.Path, "tampered");
        Assert.False(OfficeLiteNativeKssDiagnosticReceiptVerifier.Verify(receipt).Succeeded);
    }

    [Fact]
    public void Receipt_writer_verifies_and_preserves_create_new_semantics()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var receipt = new OfficeLiteNativeKssDiagnosticRunner(
            new OfficeLiteCycleRunner(new FakeOfficeLitePlatform(time, environment.PrimaryVmxPath)
            {
                ProbeReady = true,
                OpenServicePorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToHashSet()
            }, time),
            new QueueWorkVisualPlatform(NegativeResult(), ReadyResult()),
            time).Run(environment.CreateRequest(), "test-native-kss-writer").Receipt;
        var path = Path.Combine(environment.Root, "receipt.json");

        Assert.Equal(Path.GetFullPath(path), OfficeLiteNativeKssDiagnosticReceiptWriter.WriteNew(path, receipt));
        Assert.Throws<IOException>(() => OfficeLiteNativeKssDiagnosticReceiptWriter.WriteNew(path, receipt));

        var invalidPayload = receipt.Payload with { ProgramRunRequested = true };
        var invalidReceipt = receipt with
        {
            Payload = invalidPayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(invalidPayload)
        };
        Assert.Throws<InvalidOperationException>(() =>
            OfficeLiteNativeKssDiagnosticReceiptWriter.WriteNew(Path.Combine(environment.Root, "invalid.json"), invalidReceipt));
    }

    private static WorkVisualProcessResult NegativeResult() => new(
        42,
        false,
        true,
        10,
        "KRL_NATIVE_DIAGNOSTIC_FAILED=ZmFpbHVyZQ==\r\nCLEANUP_VERIFIED=True\r\n",
        string.Empty);

    private static WorkVisualProcessResult ReadyResult() => new(
        0,
        false,
        true,
        20,
        string.Join(
            "\r\n",
            [
                "KRL_NATIVE_DIAGNOSTIC_BEGIN=True",
                "ROBOT_STATE_BEFORE=Free",
                "ROBOT_SELECTED_BEFORE_BASE64=IA==",
                "MESSAGE_WINDOW_UNAVAILABLE=dW5hdmFpbGFibGU=",
                "UPLOAD_VERIFIED=True",
                "VALID_ERROR_COUNT=0",
                "VALID_SELECT_SUCCEEDED=True",
                "VALID_ACCEPTED=True",
                "INVALID_SELECT_EXCEPTION=cmVqZWN0ZWQ=",
                "INVALID_ERROR=S1JDOlxSMVxQcm9ncmFtXEtMQUJfVzRLMVxMQUJfTUlTU0lOR19UQVJHRVQuU1JD|2137|11|6|VmFyaWFibGUgbm90IGRlY2xhcmVk|TElOICBQX01JU1NJTkc=",
                "INVALID_ERROR_COUNT=1",
                "INVALID_SELECT_SUCCEEDED=False",
                "INVALID_SELECT_THREW=True",
                "INVALID_REJECTED=True",
                "VALID_ACCEPTED_FINAL=True",
                "INVALID_REJECTED_FINAL=True",
                "CLEANUP_VERIFIED=True",
                string.Empty
            ]),
        string.Empty);

    private static WorkVisualProcessResult CleanupFailureResult() => new(
        47,
        false,
        true,
        20,
        string.Join(
            "\r\n",
            [
                "KRL_NATIVE_DIAGNOSTIC_BEGIN=True",
                "ROBOT_STATE_BEFORE=Free",
                "ROBOT_SELECTED_BEFORE_BASE64=IA==",
                "UPLOAD_VERIFIED=True",
                "VALID_SELECT_SUCCEEDED=True",
                "VALID_ACCEPTED_FINAL=True",
                "INVALID_SELECT_THREW=True",
                "INVALID_REJECTED_FINAL=True",
                "CLEANUP_VERIFIED=False",
                string.Empty
            ]),
        string.Empty);

    private sealed class QueueWorkVisualPlatform(params WorkVisualProcessResult[] results) : IWorkVisualRunnerPlatform
    {
        private readonly Queue<WorkVisualProcessResult> _results = new(results);
        public List<List<string>> Invocations { get; } = [];

        public WorkVisualProcessResult Run(string executablePath, IReadOnlyList<string> arguments, string workingDirectory, TimeSpan timeout)
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

        public VmrunExecutionResult RunVmrun(string vmrunPath, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            var command = arguments.Count >= 3 ? arguments[2] : string.Empty;
            return command switch
            {
                "list" => Result(Running ? $"Total running VMs: 1{Environment.NewLine}{vmxPath}" : "Total running VMs: 0"),
                "start" => Start(),
                "stop" => Stop(),
                _ => new VmrunExecutionResult(1, false, 1, string.Empty, "unsupported")
            };
        }

        public string? TryResolveDhcpAddress(string vmxPath, string dhcpLeasePath) => "198.51.100.128";

        public GuestProbeResult ProbeGuest(string? address, int port, int timeoutMilliseconds, DateTimeOffset observedAtUtc) =>
            ProbeReady
                ? new GuestProbeResult(0, observedAtUtc, address, true, true, "ready", new GuestTlsIdentity(
                    address!, "KUKA Roboter GmbH", "KUKA Roboter GmbH", "4DCFB182A3EF5E53FC908D7999273595E02BC430", "Tls12", true))
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

        private VmrunExecutionResult Start() { Running = true; return Result(string.Empty); }
        private VmrunExecutionResult Stop() { Running = false; return Result(string.Empty); }
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
        private const string RelativeVmx = "OfficeLite-Work/8.7.8-build04/runs/primary/KR C, V8.7.8OL_Build04.vmx";

        private TestEnvironment(string root, string primaryVmxPath, string runnerPath, string labRoot)
        {
            Root = root;
            PrimaryVmxPath = primaryVmxPath;
            RunnerPath = runnerPath;
            LabRoot = labRoot;
        }

        public string Root { get; }
        public string PrimaryVmxPath { get; }
        private string RunnerPath { get; }
        private string LabRoot { get; }

        public static TestEnvironment Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"kuka-lab-native-kss-{Guid.NewGuid():N}");
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
            return new TestEnvironment(root, primaryVmx, runnerPath, FindLabRoot());
        }

        public OfficeLiteNativeKssDiagnosticRequest CreateRequest() => new()
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
            LabRoot = LabRoot,
            RunnerTimeoutSeconds = 5
        };

        private static string FindLabRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "fixtures", "minimal-ptp-lin-valid", "controller-files", "LAB_MINIMAL.src")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate 04_kuka_lab fixture root.");
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
