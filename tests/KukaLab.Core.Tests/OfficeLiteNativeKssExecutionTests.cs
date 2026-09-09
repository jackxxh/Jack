using System.Text;
using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class OfficeLiteNativeKssExecutionTests
{
    [Fact]
    public void Ready_cycle_completes_go_mode_ptp_lin_relative_rejects_invalid_and_restores_snapshot()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var officeLite = new FakeOfficeLitePlatform(time, environment.VmxPath)
        {
            ProbeReady = true,
            OpenServicePorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToHashSet()
        };
        var workVisual = new QueueWorkVisualPlatform(ReadyResult());
        var outcome = new OfficeLiteNativeKssExecutionRunner(officeLite, workVisual, time)
            .Run(environment.CreateRequest(), "test-native-kss-execution-ready");

        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(NativeKssStatus.BoundedExecutionValidated, outcome.Receipt.Payload.NativeKssStatus);
        Assert.Equal(0, outcome.ExitCode);
        Assert.False(officeLite.Running);
        Assert.True(officeLite.SnapshotRestored);
        Assert.Single(workVisual.Invocations);
        Assert.Contains($"-transactionroot={OfficeLiteNativeKssExecutionContract.TransactionRoot}", workVisual.Invocations[0]);
        Assert.True(outcome.Receipt.Payload.Execution.ValidCompleted);
        Assert.Equal(2, outcome.Receipt.Payload.Execution.StartCount);
        Assert.Equal("Go", outcome.Receipt.Payload.Execution.ValidFinalMode);
        Assert.Equal("End", outcome.Receipt.Payload.Execution.ValidFinalState);
        Assert.Equal(355, outcome.Receipt.Payload.Execution.InitialPose!.X);
        Assert.Equal(365, outcome.Receipt.Payload.Execution.FinalPose!.X);
        Assert.True(outcome.Receipt.Payload.Execution.InvalidRejected);
        var error = Assert.Single(outcome.Receipt.Payload.Execution.InvalidErrors);
        Assert.Equal(2137, error.ErrorNumber);
        Assert.Equal(OfficeLiteNativeKssExecutionContract.ExpectedInvalidErrorLine, error.Line);
        Assert.Equal(6, error.Column);
        Assert.True(outcome.Receipt.Payload.SnapshotRestored);
        Assert.True(outcome.Receipt.Payload.ControllerStateRestored);
        Assert.True(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.True(outcome.Receipt.Payload.ProgramStartRequested);
        Assert.True(outcome.Receipt.Payload.ProgramRunRequested);
        Assert.True(outcome.Receipt.Payload.VirtualMotionRequested);
        Assert.False(outcome.Receipt.Payload.PhysicalMotionRequested);
        Assert.True(OfficeLiteNativeKssExecutionReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Wrong_tcp_delta_is_failed_and_not_promoted()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var officeLite = ReadyPlatform(time, environment.VmxPath);
        var outcome = new OfficeLiteNativeKssExecutionRunner(
            officeLite,
            new QueueWorkVisualPlatform(ReadyResult(finalX: 366)),
            time).Run(environment.CreateRequest(), "test-native-kss-execution-wrong-delta");

        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.NativeKssStatus);
        Assert.False(outcome.Receipt.Payload.Execution.ValidCompleted &&
            OfficeLiteNativeKssExecutionRunner.IsAcceptedExecution(outcome.Receipt.Payload.Execution));
        Assert.False(officeLite.Running);
        Assert.True(officeLite.SnapshotRestored);
        Assert.True(OfficeLiteNativeKssExecutionReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Snapshot_restore_failure_is_failed_even_after_successful_kss_execution()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var officeLite = ReadyPlatform(time, environment.VmxPath) with { RevertSucceeds = false };
        var outcome = new OfficeLiteNativeKssExecutionRunner(
            officeLite,
            new QueueWorkVisualPlatform(ReadyResult()),
            time).Run(environment.CreateRequest(), "test-native-kss-execution-restore-failure");

        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.NativeKssStatus);
        Assert.False(outcome.Receipt.Payload.SnapshotRestored);
        Assert.False(outcome.Receipt.Payload.ControllerStateRestored);
        Assert.False(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.False(officeLite.Running);
        Assert.True(OfficeLiteNativeKssExecutionReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Receipt_writer_rejects_rehashed_pose_tamper_and_preserves_create_new_semantics()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var receipt = new OfficeLiteNativeKssExecutionRunner(
            ReadyPlatform(time, environment.VmxPath),
            new QueueWorkVisualPlatform(ReadyResult()),
            time).Run(environment.CreateRequest(), "test-native-kss-execution-writer").Receipt;
        var path = Path.Combine(environment.Root, "execution-receipt.json");

        Assert.Equal(Path.GetFullPath(path), OfficeLiteNativeKssExecutionReceiptWriter.WriteNew(path, receipt));
        Assert.Throws<IOException>(() => OfficeLiteNativeKssExecutionReceiptWriter.WriteNew(path, receipt));

        var tamperedExecution = receipt.Payload.Execution with
        {
            FinalPose = receipt.Payload.Execution.FinalPose! with { X = 366 }
        };
        var tamperedPayload = receipt.Payload with { Execution = tamperedExecution };
        var tamperedReceipt = receipt with
        {
            Payload = tamperedPayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(tamperedPayload)
        };
        Assert.False(OfficeLiteNativeKssExecutionReceiptVerifier.Verify(tamperedReceipt).Succeeded);
        Assert.Throws<InvalidOperationException>(() =>
            OfficeLiteNativeKssExecutionReceiptWriter.WriteNew(
                Path.Combine(environment.Root, "tampered.json"), tamperedReceipt));
    }

    [Fact]
    public void Verifier_rejects_missing_lifecycle_receipt_without_throwing()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var receipt = new OfficeLiteNativeKssExecutionRunner(
            ReadyPlatform(time, environment.VmxPath),
            new QueueWorkVisualPlatform(ReadyResult()),
            time).Run(environment.CreateRequest(), "test-native-kss-execution-missing-lifecycle").Receipt;
        var tamperedPayload = receipt.Payload with { LifecycleReceipt = null! };
        var tamperedReceipt = receipt with
        {
            Payload = tamperedPayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(tamperedPayload)
        };

        var result = OfficeLiteNativeKssExecutionReceiptVerifier.Verify(tamperedReceipt);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("lifecycle receipt is required", StringComparison.Ordinal));
    }

    private static FakeOfficeLitePlatform ReadyPlatform(ManualTimeProvider time, string vmxPath) => new(time, vmxPath)
    {
        ProbeReady = true,
        OpenServicePorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToHashSet()
    };

    private static WorkVisualProcessResult ReadyResult(double finalX = 365) => new(
        0,
        false,
        true,
        20,
        string.Join(
            "\r\n",
            [
                "KSS_BOUNDED_EXECUTION_BEGIN=True",
                "ROBOT_STATE_BEFORE=Free",
                "ROBOT_SELECTED_BEFORE=" + B64(" "),
                "UPLOAD_VERIFIED=True",
                "VALID_ERROR_COUNT=0",
                "VALID_SELECT_SUCCEEDED=True",
                "VALID_ACCEPTED=True",
                "GO_CONFIRMED=True",
                Trace(0, "before-start", "Go", "Reset", 355),
                "START_SUCCEEDED=1",
                Trace(1, "after-start-1", "Go", "Stop", 355),
                "START_SUCCEEDED=2",
                Trace(2, "after-start-2", "Go", "End", finalX),
                "START_COUNT=2",
                "VALID_FINAL_MODE=Go",
                "VALID_FINAL_STATE=End",
                "VALID_COMPLETED=True",
                "INVALID_SELECT_EXCEPTION=" + B64("rejected"),
                "INVALID_ERROR=" + B64(OfficeLiteNativeKssExecutionContract.TransactionRoot + "\\LAB_MISSING_TARGET.SRC") +
                    $"|2137|{OfficeLiteNativeKssExecutionContract.ExpectedInvalidErrorLine}|6|" + B64("Variable not declared") + "|" + B64("LIN P_MISSING"),
                "INVALID_ERROR_COUNT=1",
                "INVALID_REJECTED=True",
                "VALID_COMPLETED_FINAL=True",
                "INVALID_REJECTED_FINAL=True",
                "CLEANUP_VERIFIED=True",
                string.Empty
            ]),
        string.Empty);

    private static string Trace(int sequence, string label, string mode, string state, double x) =>
        $"TRACE={sequence}|{label}|{mode}|{state}|{B64($"/R1/LAB_OL_KR3.SRC:{20 + sequence}")}|" +
        $"{B64("{E6AXIS: A1 0, A2 -90, A3 90, A4 0, A5 0, A6 0}")}|" +
        $"{B64($"{{E6POS: X {x}, Y 0, Z 625, A 0, B 90, C 0, S 2, T 2}}")}|" +
        $"{B64(state == "End" ? "#P_END" : state == "Stop" ? "#P_STOP" : "#P_RESET")}|" +
        $"{B64("True")}|{B64("True")}";

    private static string B64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

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

    private sealed record FakeOfficeLitePlatform(ManualTimeProvider Time, string VmxPath) : IOfficeLiteHostPlatform
    {
        public bool Running { get; set; }
        public bool ProbeReady { get; init; }
        public HashSet<int> OpenServicePorts { get; init; } = [];
        public bool RevertSucceeds { get; init; } = true;
        public bool SnapshotRestored { get; private set; }

        public VmrunExecutionResult RunVmrun(string vmrunPath, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            var command = arguments.Count >= 3 ? arguments[2] : string.Empty;
            return command switch
            {
                "listSnapshots" => Result($"Total snapshots: 1{Environment.NewLine}{OfficeLiteNativeKssExecutionContract.DefaultSnapshotName}"),
                "list" => Result(Running ? $"Total running VMs: 1{Environment.NewLine}{VmxPath}" : "Total running VMs: 0"),
                "start" => Start(),
                "stop" => Stop(),
                "revertToSnapshot" => Revert(),
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
            string? address, IReadOnlyList<int> ports, int timeoutMilliseconds, DateTimeOffset observedAtUtc) =>
            ports.Select(port => new GuestTcpPortProbeResult(
                port, observedAtUtc,
                OpenServicePorts.Contains(port) ? GuestTcpPortState.Open : GuestTcpPortState.Timeout,
                OpenServicePorts.Contains(port) ? "Open" : "Timeout", 1)).ToList();

        public void Delay(TimeSpan duration) => Time.Advance(duration);
        private VmrunExecutionResult Start() { Running = true; return Result(string.Empty); }
        private VmrunExecutionResult Stop() { Running = false; return Result(string.Empty); }
        private VmrunExecutionResult Revert()
        {
            SnapshotRestored = RevertSucceeds;
            return RevertSucceeds ? Result(string.Empty) : new VmrunExecutionResult(1, false, 1, string.Empty, "revert failed");
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
        private TestEnvironment(string root, string vmxPath, string runnerPath, string labRoot)
        {
            Root = root;
            VmxPath = vmxPath;
            RunnerPath = runnerPath;
            LabRoot = labRoot;
        }

        public string Root { get; }
        public string VmxPath { get; }
        private string RunnerPath { get; }
        private string LabRoot { get; }

        public static TestEnvironment Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"kuka-lab-native-kss-execution-{Guid.NewGuid():N}");
            var vmware = Path.Combine(root, "VMware");
            Directory.CreateDirectory(vmware);
            File.WriteAllText(Path.Combine(vmware, "vmrun.exe"), "synthetic vmrun");
            var vmx = Path.Combine(root, "OfficeLite-Work", "8.7.8-build04", "runs", "kr210-c01-item9", "KR210-C01-Item9.vmx");
            Directory.CreateDirectory(Path.GetDirectoryName(vmx)!);
            File.WriteAllLines(vmx,
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
            return new TestEnvironment(root, vmx, runnerPath, FindLabRoot());
        }

        public OfficeLiteNativeKssExecutionRequest CreateRequest() => new()
        {
            OfficeLite = OfficeLiteCycleRequest.CreateDefault(Root, readinessTimeoutSeconds: 5) with
            {
                VmxPath = VmxPath,
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
            SnapshotName = OfficeLiteNativeKssExecutionContract.DefaultSnapshotName,
            RunnerTimeoutSeconds = 5
        };

        private static string FindLabRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "fixtures", "minimal-officelite-kr3-execution-valid", "controller-files", "LAB_OL_KR3.src")))
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
