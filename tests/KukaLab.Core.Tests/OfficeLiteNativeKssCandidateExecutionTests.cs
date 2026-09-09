using System.Text;
using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class OfficeLiteNativeKssCandidateExecutionTests
{
    [Fact]
    public void Exact_profile_candidate_completes_with_bounded_starts_and_restores_snapshot()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var officeLite = new FakeOfficeLitePlatform(time, environment.VmxPath)
        {
            ProbeReady = true,
            OpenServicePorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToHashSet()
        };
        var workVisual = new QueueWorkVisualPlatform(ReadyResult());

        var outcome = new OfficeLiteNativeKssCandidateExecutionRunner(
            officeLite, workVisual, environment.AdmissionGate, time)
            .Run(environment.CreateRequest(), "test-exact-c01-candidate-ready");

        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(NativeKssCandidateExecutionDisposition.Executed, outcome.Receipt.Payload.Disposition);
        Assert.Equal(NativeKssStatus.BoundedExecutionValidated, outcome.Receipt.Payload.NativeKssStatus);
        Assert.Equal(environment.CandidateId, outcome.Receipt.Payload.Submission.CandidateId);
        Assert.Equal(NativeKssCandidateSubmissionContract.MachineDataIdentity, outcome.Receipt.Payload.ObservedProfile.RobotType);
        Assert.Equal(NativeKssCandidateSubmissionContract.CabinetKind, outcome.Receipt.Payload.Submission.RequiredProfile.CabinetKind);
        Assert.Equal("V8.7.8.671", outcome.Receipt.Payload.ObservedProfile.KssVersion);
        Assert.Equal("Go", outcome.Receipt.Payload.Execution.Mode);
        Assert.Equal("End", outcome.Receipt.Payload.Execution.FinalState);
        Assert.Equal(2, outcome.Receipt.Payload.Execution.StartCount);
        Assert.True(outcome.Receipt.Payload.Execution.StartCount <= outcome.Receipt.Payload.Submission.MaximumStartCommands);
        Assert.False(outcome.Receipt.Payload.PhysicalMotionRequested);
        Assert.True(outcome.Receipt.Payload.VirtualMotionRequested);
        Assert.True(outcome.Receipt.Payload.SnapshotRestored);
        Assert.True(outcome.Receipt.Payload.ControllerStateRestored);
        Assert.True(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.False(officeLite.Running);
        Assert.True(officeLite.SnapshotRestored);
        Assert.Single(workVisual.Invocations);
        Assert.Contains($"-maxstarts={environment.MaximumStartCommands}", workVisual.Invocations[0]);
        Assert.Contains(
            $"-expectedrobotb64={Convert.ToBase64String(Encoding.UTF8.GetBytes(NativeKssCandidateSubmissionContract.MachineDataIdentity))}",
            workVisual.Invocations[0]);
        Assert.Contains(
            $"-expectedkssb64={Convert.ToBase64String(Encoding.UTF8.GetBytes("V8.7.8.671"))}",
            workVisual.Invocations[0]);
        Assert.True(OfficeLiteNativeKssCandidateExecutionReceiptVerifier.Verify(
            outcome.Receipt, environment.AdmissionGate).Succeeded);
    }

    [Fact]
    public void Profile_mismatch_starts_nothing_and_cannot_be_promoted()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var officeLite = new FakeOfficeLitePlatform(time, environment.VmxPath)
        {
            ProbeReady = true,
            OpenServicePorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToHashSet()
        };
        var workVisual = new QueueWorkVisualPlatform(ProfileMismatchResult());

        var outcome = new OfficeLiteNativeKssCandidateExecutionRunner(
            officeLite, workVisual, environment.AdmissionGate, time)
            .Run(environment.CreateRequest(), "test-exact-c01-profile-mismatch");

        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(NativeKssCandidateExecutionDisposition.InfrastructureFailed, outcome.Receipt.Payload.Disposition);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.NativeKssStatus);
        Assert.Equal("#KR3R540 C4SR", outcome.Receipt.Payload.ObservedProfile.RobotType);
        Assert.False(outcome.Receipt.Payload.Execution.ProfileMatched);
        Assert.Equal(0, outcome.Receipt.Payload.Execution.StartCount);
        Assert.False(outcome.Receipt.Payload.VirtualMotionRequested);
        Assert.False(outcome.Receipt.Payload.PhysicalMotionRequested);
        Assert.True(outcome.Receipt.Payload.SnapshotRestored);
        Assert.True(outcome.Receipt.Payload.ControllerStateRestored);
        Assert.True(OfficeLiteNativeKssCandidateExecutionReceiptVerifier.Verify(
            outcome.Receipt, environment.AdmissionGate).Succeeded);
    }

    [Fact]
    public void Native_kss_rejection_is_a_ready_diagnostic_receipt_with_zero_starts()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var officeLite = new FakeOfficeLitePlatform(time, environment.VmxPath)
        {
            ProbeReady = true,
            OpenServicePorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToHashSet()
        };
        var outcome = new OfficeLiteNativeKssCandidateExecutionRunner(
            officeLite,
            new QueueWorkVisualPlatform(NativeRejectionResult()),
            environment.AdmissionGate,
            time).Run(environment.CreateRequest(), "test-exact-c01-native-rejection");

        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(NativeKssCandidateExecutionDisposition.RejectedByNativeKss, outcome.Receipt.Payload.Disposition);
        Assert.Equal(NativeKssStatus.SyntaxSelectionValidated, outcome.Receipt.Payload.NativeKssStatus);
        Assert.Equal(0, outcome.Receipt.Payload.Execution.StartCount);
        Assert.False(outcome.Receipt.Payload.VirtualMotionRequested);
        var diagnostic = Assert.Single(outcome.Receipt.Payload.Execution.Diagnostics);
        Assert.EndsWith("CELL.src", diagnostic.Module, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2137, diagnostic.ErrorNumber);
        Assert.Equal(3, diagnostic.Line);
        Assert.Equal(7, diagnostic.Column);
        Assert.True(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.True(OfficeLiteNativeKssCandidateExecutionReceiptVerifier.Verify(
            outcome.Receipt, environment.AdmissionGate).Succeeded);
    }

    [Fact]
    public void Unready_lifecycle_produces_a_verifiable_blocked_receipt_without_live_logs()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var officeLite = new FakeOfficeLitePlatform(time, environment.VmxPath);
        var workVisual = new QueueWorkVisualPlatform(ReadyResult());

        var outcome = new OfficeLiteNativeKssCandidateExecutionRunner(
            officeLite, workVisual, environment.AdmissionGate, time)
            .Run(environment.CreateRequest(), "test-exact-c01-candidate-unready");

        Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
        Assert.False(outcome.Receipt.Payload.LiveCommand.Attempted);
        Assert.DoesNotContain(outcome.Receipt.Payload.Files, file => file.Id == "workvisual-live-stdout");
        Assert.DoesNotContain(outcome.Receipt.Payload.Files, file => file.Id == "workvisual-live-stderr");
        Assert.True(OfficeLiteNativeKssCandidateExecutionReceiptVerifier.Verify(
            outcome.Receipt, environment.AdmissionGate).Succeeded);
    }

    [Fact]
    public void Candidate_drift_is_rejected_before_vm_or_workvisual_invocation()
    {
        using var environment = TestEnvironment.Create();
        File.AppendAllText(Path.Combine(environment.SourceRoot, "CELL.src"), "; drift\n");
        var time = new ManualTimeProvider();
        var officeLite = new FakeOfficeLitePlatform(time, environment.VmxPath);
        var workVisual = new QueueWorkVisualPlatform(ReadyResult());

        var exception = Assert.Throws<InvalidDataException>(() =>
            new OfficeLiteNativeKssCandidateExecutionRunner(
                officeLite, workVisual, environment.AdmissionGate, time)
                .Run(environment.CreateRequest(), "test-exact-c01-candidate-drift"));

        Assert.Contains("current source verification failed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, officeLite.VmrunInvocationCount);
        Assert.Empty(workVisual.Invocations);
    }

    [Fact]
    public void Rejected_exact_profile_admission_stops_before_vm_workvisual_or_evidence_side_effects()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var officeLite = new FakeOfficeLitePlatform(time, environment.VmxPath);
        var workVisual = new QueueWorkVisualPlatform(ReadyResult());

        var exception = Assert.Throws<InvalidDataException>(() =>
            new OfficeLiteNativeKssCandidateExecutionRunner(
                officeLite,
                workVisual,
                new RejectingExactProfileAdmissionGate(),
                time).Run(environment.CreateRequest(), "test-exact-c01-admission-rejected"));

        Assert.Contains("profile not accepted", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, officeLite.VmrunInvocationCount);
        Assert.Empty(workVisual.Invocations);
        Assert.False(Directory.Exists(environment.EvidenceDirectory));
    }

    [Fact]
    public void Verifier_rejects_rehashed_start_count_above_candidate_bound()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var receipt = new OfficeLiteNativeKssCandidateExecutionRunner(
            new FakeOfficeLitePlatform(time, environment.VmxPath)
            {
                ProbeReady = true,
                OpenServicePorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToHashSet()
            },
            new QueueWorkVisualPlatform(ReadyResult()),
            environment.AdmissionGate,
            time).Run(environment.CreateRequest(), "test-exact-c01-start-bound-tamper").Receipt;
        var tamperedExecution = receipt.Payload.Execution with
        {
            StartCount = receipt.Payload.Submission.MaximumStartCommands + 1
        };
        var tamperedPayload = receipt.Payload with { Execution = tamperedExecution };
        var tamperedReceipt = receipt with
        {
            Payload = tamperedPayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(tamperedPayload)
        };

        var verification = OfficeLiteNativeKssCandidateExecutionReceiptVerifier.Verify(
            tamperedReceipt, environment.AdmissionGate);

        Assert.False(verification.Succeeded);
        Assert.Contains(verification.Errors, error => error.Contains("Start count exceeded", StringComparison.Ordinal));
    }

    [Fact]
    public void Verifier_rejects_rehashed_exact_profile_admission_drift()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var receipt = new OfficeLiteNativeKssCandidateExecutionRunner(
            new FakeOfficeLitePlatform(time, environment.VmxPath)
            {
                ProbeReady = true,
                OpenServicePorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToHashSet()
            },
            new QueueWorkVisualPlatform(ReadyResult()),
            environment.AdmissionGate,
            time).Run(environment.CreateRequest(), "test-exact-c01-admission-tamper").Receipt;
        var tamperedPayload = receipt.Payload with
        {
            ProfileAdmission = receipt.Payload.ProfileAdmission with
            {
                AcceptanceReceiptSha256 = new string('C', 64)
            }
        };
        var tamperedReceipt = receipt with
        {
            Payload = tamperedPayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(tamperedPayload)
        };

        var verification = OfficeLiteNativeKssCandidateExecutionReceiptVerifier.Verify(
            tamperedReceipt, environment.AdmissionGate);

        Assert.False(verification.Succeeded);
        Assert.Contains(verification.Errors, error =>
            error.Contains("admission no longer matches", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Legacy_schema_v1_cannot_promote_a_ready_exact_profile_execution()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var current = new OfficeLiteNativeKssCandidateExecutionRunner(
            new FakeOfficeLitePlatform(time, environment.VmxPath)
            {
                ProbeReady = true,
                OpenServicePorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToHashSet()
            },
            new QueueWorkVisualPlatform(ReadyResult()),
            environment.AdmissionGate,
            time).Run(environment.CreateRequest(), "test-exact-c01-legacy-ready").Receipt;
        var legacy = current with
        {
            SchemaVersion = NativeKssCandidateExecutionContract.LegacyReceiptSchemaVersion
        };

        var verification = OfficeLiteNativeKssCandidateExecutionReceiptVerifier.Verify(
            legacy, environment.AdmissionGate);

        Assert.False(verification.Succeeded);
        Assert.Contains(verification.Errors, error =>
            error.Contains("legacy schema v1", StringComparison.OrdinalIgnoreCase));
    }

    private static WorkVisualProcessResult ReadyResult() => new(
        0,
        false,
        true,
        20,
        string.Join(
            "\r\n",
            [
                "KSS_CANDIDATE_EXECUTION_BEGIN=True",
                "PROFILE_ROBOT=" + B64(NativeKssCandidateSubmissionContract.MachineDataIdentity),
                "PROFILE_KSS=" + B64("V8.7.8.671"),
                "PROFILE_PROJECT=" + B64("deployment.analysis-copy"),
                "UPLOAD_VERIFIED=True",
                "SELECT_SUCCEEDED=True",
                "DIAGNOSTIC_COUNT=0",
                "MODE=Go",
                "START_SUCCEEDED=1",
                "START_SUCCEEDED=2",
                "START_COUNT=2",
                "FINAL_STATE=End",
                "COMPLETED=True",
                "CLEANUP_VERIFIED=True",
                string.Empty
            ]),
        string.Empty);

    private static WorkVisualProcessResult ProfileMismatchResult() => new(
        42,
        false,
        true,
        10,
        string.Join(
            "\r\n",
            [
                "KSS_CANDIDATE_EXECUTION_BEGIN=True",
                "PROFILE_ROBOT=" + B64("#KR3R540 C4SR"),
                "PROFILE_KSS=" + B64("V8.7.8.671"),
                "PROFILE_PROJECT=" + B64("InitialProject (1)"),
                "PROFILE_MATCHED=False",
                "START_COUNT=0",
                "COMPLETED=False",
                "REJECTED=False",
                "DISPOSITION=InfrastructureFailed",
                "CLEANUP_VERIFIED=True",
                string.Empty
            ]),
        string.Empty);

    private static WorkVisualProcessResult NativeRejectionResult() => new(
        0,
        false,
        true,
        15,
        string.Join(
            "\r\n",
            [
                "KSS_CANDIDATE_EXECUTION_BEGIN=True",
                "PROFILE_ROBOT=" + B64(NativeKssCandidateSubmissionContract.MachineDataIdentity),
                "PROFILE_KSS=" + B64("V8.7.8.671"),
                "PROFILE_PROJECT=" + B64(NativeKssCandidateExecutionContract.DefaultExpectedProjectName),
                "PROFILE_MATCHED=True",
                "UPLOAD_VERIFIED=True",
                "SELECT_SUCCEEDED=False",
                "DIAGNOSTIC=" + B64(@"KRC:\R1\Program\KLAB_CAND_1234567890ABCDEF\CELL.src") +
                    "|2137|3|7|" + B64("Variable not declared") + "|" + B64("P_MISSING"),
                "DIAGNOSTIC_COUNT=1",
                "START_COUNT=0",
                "COMPLETED=False",
                "REJECTED=True",
                "DISPOSITION=RejectedByNativeKss",
                "CLEANUP_VERIFIED=True",
                string.Empty
            ]),
        string.Empty);

    private static string B64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

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

    private sealed record FakeOfficeLitePlatform(ManualTimeProvider Time, string VmxPath) : IOfficeLiteHostPlatform
    {
        public bool Running { get; set; }
        public bool ProbeReady { get; init; }
        public HashSet<int> OpenServicePorts { get; init; } = [];
        public bool SnapshotRestored { get; private set; }
        public int VmrunInvocationCount { get; private set; }

        public VmrunExecutionResult RunVmrun(string vmrunPath, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            VmrunInvocationCount++;
            var command = arguments.Count >= 3 ? arguments[2] : string.Empty;
            return command switch
            {
                "listSnapshots" => Result($"Total snapshots: 1{Environment.NewLine}{NativeKssCandidateExecutionContract.DefaultSnapshotName}"),
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

        public void Delay(TimeSpan duration) => Time.Advance(duration);

        private VmrunExecutionResult Start() { Running = true; return Result(string.Empty); }
        private VmrunExecutionResult Stop() { Running = false; return Result(string.Empty); }
        private VmrunExecutionResult Revert() { SnapshotRestored = true; return Result(string.Empty); }
        private static VmrunExecutionResult Result(string output) => new(0, false, 1, output, string.Empty);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 8, 29, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan duration) => _utcNow += duration;
    }

    private sealed class RejectingExactProfileAdmissionGate : IOfficeLiteExactProfileAdmissionGate
    {
        public OfficeLiteExactProfileAdmission Admit(OfficeLiteExactProfileAdmissionRequest request) =>
            throw new InvalidDataException("profile not accepted");
    }

    private sealed class AcceptingExactProfileAdmissionGate : IOfficeLiteExactProfileAdmissionGate
    {
        public OfficeLiteExactProfileAdmission Admit(OfficeLiteExactProfileAdmissionRequest request) => new()
        {
            AcceptanceReceiptPath = Path.GetFullPath(request.AcceptanceReceiptPath),
            AcceptanceReceiptSha256 = new string('A', 64),
            AcceptancePayloadSha256 = new string('B', 64),
            AcceptanceReceiptId = "synthetic-exact-profile-acceptance",
            ExpectedProjectName = request.ExpectedProjectName,
            RuntimeRobotIdentity = NativeKssCandidateSubmissionContract.MachineDataIdentity,
            RuntimeKssVersion = "V8.7.8.671",
            ActiveMachineDataIdentity = NativeKssCandidateSubmissionContract.MachineDataIdentity,
            ActiveCabinetKind = NativeKssCandidateSubmissionContract.CabinetKind,
            Accepted = true
        };
    }

    private sealed class TestEnvironment : IDisposable
    {
        private TestEnvironment(string root)
        {
            Root = root;
            var vmware = Path.Combine(root, "VMware");
            Directory.CreateDirectory(vmware);
            VmrunPath = Path.Combine(vmware, "vmrun.exe");
            File.WriteAllText(VmrunPath, "synthetic vmrun");
            VmxPath = Path.Combine(root, "OfficeLite-Work", "Exact-C01-Goal.vmx");
            Directory.CreateDirectory(Path.GetDirectoryName(VmxPath)!);
            File.WriteAllLines(VmxPath,
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
            RunnerPath = Path.Combine(root, "wvsr.exe");
            File.WriteAllText(RunnerPath, "synthetic WorkVisual runner");
            SourceRoot = Path.Combine(root, "candidate");
            Directory.CreateDirectory(SourceRoot);
            File.WriteAllText(Path.Combine(SourceRoot, "CELL.src"), "DEF CELL()\n BAS(#INITMOV,0)\n PTP HOME\nEND\n");
            File.WriteAllText(Path.Combine(SourceRoot, "CELL.dat"), "DEFDAT CELL PUBLIC\n DECL E6AXIS HOME={A1 0,A2 -90,A3 90,A4 0,A5 0,A6 0}\nENDDAT\n");
            CandidateReceiptPath = Path.Combine(root, "candidate.receipt.json");
            var candidate = new RawKrlCandidateRunner().Run(
                new RawKrlCandidateRequest { SourceRoot = SourceRoot, ReceiptOutputPath = CandidateReceiptPath },
                "test-exact-c01-candidate-intake");
            RawKrlCandidateReceiptWriter.WriteNew(CandidateReceiptPath, SourceRoot, candidate.Receipt);
            CandidateId = candidate.Receipt.Payload.CandidateId;
        }

        public string Root { get; }
        public string VmrunPath { get; }
        public string VmxPath { get; }
        public string RunnerPath { get; }
        public string SourceRoot { get; }
        public string CandidateReceiptPath { get; }
        public string CandidateId { get; }
        public IOfficeLiteExactProfileAdmissionGate AdmissionGate { get; } =
            new AcceptingExactProfileAdmissionGate();
        public string EvidenceDirectory => Path.Combine(Root, "evidence");
        public int MaximumStartCommands => 5;

        public static TestEnvironment Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"kuka-lab-exact-c01-candidate-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            return new TestEnvironment(root);
        }

        public NativeKssCandidateExecutionRequest CreateRequest() => new()
        {
            OfficeLite = OfficeLiteCycleRequest.CreateDefault(Root, VmrunPath, readinessTimeoutSeconds: 5) with
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
            EvidenceDirectory = EvidenceDirectory,
            Submission = new NativeKssCandidateSubmissionRequest
            {
                CandidateRoot = SourceRoot,
                CandidateReceiptPath = CandidateReceiptPath,
                ProgramRelativeStem = "CELL",
                MaximumStartCommands = MaximumStartCommands
            },
            ExactProfileAcceptanceReceiptPath = Path.Combine(Root, "exact-profile-acceptance.receipt.json"),
            SnapshotName = NativeKssCandidateExecutionContract.DefaultSnapshotName,
            RunnerTimeoutSeconds = 5
        };

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
