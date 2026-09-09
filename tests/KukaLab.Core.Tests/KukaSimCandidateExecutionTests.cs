using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class KukaSimCandidateExecutionTests
{
    [Fact]
    public void Create_default_binds_candidate_and_installed_exact_c01_paths()
    {
        using var fixture = CandidateFixture.Create();
        var evidenceDirectory = Path.Combine(fixture.Root, "evidence");
        var submission = fixture.CreateRequest().Submission;
        var request = KukaSimCandidateExecutionRequest.CreateDefault(
            evidenceDirectory,
            submission,
            guiExecutionAuthorized: true,
            authorizationReference: "TASK-20260829-KUKA-EXACT-C01-NATIVE-LOOP",
            enginePath: Path.Combine(fixture.InstallRoot, "VisualComponents.Engine.exe"),
            componentPath: fixture.ComponentPath,
            timeoutSeconds: 120);

        Assert.Equal(Path.Combine(fixture.InstallRoot, "VisualComponents.Engine.Launcher.exe"), request.LauncherPath);
        Assert.Equal(
            Path.Combine(fixture.InstallRoot, KukaSimInstallationDiscovery.BootstrapPluginFileName),
            request.BootstrapPluginPath);
        Assert.Equal(Path.Combine(fixture.InstallRoot, "Create3D.Shared.dll"), request.Create3DSharedPath);
        Assert.Equal(
            KukaSimComponentSmokeContract.ExactKr210R2700Component410Sha256,
            KukaSimCandidateExecutionContract.ExactComponentSha256);
        Assert.Equal(Path.GetFullPath(fixture.ComponentPath), request.SimulationLayoutPath);
        Assert.Equal(Path.GetFullPath(evidenceDirectory), request.EvidenceDirectory);
        Assert.Equal(submission, request.Submission);
        Assert.True(request.GuiExecutionAuthorized);
        Assert.Equal(120, request.TimeoutSeconds);
    }

    [Fact]
    public void Exact_c01_integrated_result_classifies_arbitrary_candidate_as_executed()
    {
        using var fixture = CandidateFixture.Create();
        var plan = fixture.Plan();
        var resultPath = Path.Combine(fixture.Root, "result.tsv");
        File.WriteAllText(resultPath, ValidRaw(plan.ProgramName));
        var result = KukaSimIntegratedRawParser.Parse(resultPath);
        var attempt = new KukaSimIntegratedAttempt
        {
            Role = "Candidate",
            ProgramName = plan.ProgramName,
            SourcePath = Path.Combine(plan.CandidateRoot, plan.Source.RelativePath),
            DataPath = Path.Combine(plan.CandidateRoot, plan.Data.RelativePath),
            RawResultPath = resultPath,
            TracePath = resultPath,
            Command = new KukaSimCommandObservation
            {
                ProcessStarted = true,
                ExitCode = 0,
                ForcedTerminationUsed = true,
                CleanupVerified = true,
                OwnedProcessIds = [1234]
            },
            Result = result,
            ResultCanonicalSha256 = ReceiptSerialization.ComputeCanonicalSha256(result)
        };

        var disposition = KukaSimCandidateExecutionClassifier.Classify(plan, attempt);

        Assert.Equal(KukaSimCandidateExecutionDisposition.Executed, disposition);
        Assert.Equal(["A_HOME"], result.CompletedMotions.Select(motion => motion.Target));
        Assert.All(result.Samples, sample => Assert.Equal(6, sample.Axes.Count));
    }

    [Fact]
    public void Program_finished_callback_with_paused_simulation_accepts_active_interpreter_state()
    {
        using var fixture = CandidateFixture.Create();
        var plan = fixture.Plan();
        var resultPath = Path.Combine(fixture.Root, "finished-paused.tsv");
        File.WriteAllText(
            resultPath,
            ValidRaw(plan.ProgramName)
                .Replace("INTERPRETER_AFTER\tGo\tEnd", "INTERPRETER_AFTER\tGo\tActive", StringComparison.Ordinal)
                .Replace("SIMULATION_AFTER\tRunning=False\tPaused=False", "SIMULATION_AFTER\tRunning=False\tPaused=True", StringComparison.Ordinal));
        var result = KukaSimIntegratedRawParser.Parse(resultPath);
        var attempt = new KukaSimIntegratedAttempt
        {
            Role = "Candidate",
            ProgramName = plan.ProgramName,
            SourcePath = Path.Combine(plan.CandidateRoot, plan.Source.RelativePath),
            DataPath = Path.Combine(plan.CandidateRoot, plan.Data.RelativePath),
            RawResultPath = resultPath,
            TracePath = resultPath,
            Command = new KukaSimCommandObservation
            {
                ProcessStarted = true,
                ExitCode = 0,
                CleanupVerified = true,
                OwnedProcessIds = [1234]
            },
            Result = result,
            ResultCanonicalSha256 = ReceiptSerialization.ComputeCanonicalSha256(result)
        };

        Assert.Equal(
            KukaSimCandidateExecutionDisposition.Executed,
            KukaSimCandidateExecutionClassifier.Classify(plan, attempt));
    }

    [Fact]
    public void Runner_binds_verified_candidate_and_exact_component_to_reusable_receipt()
    {
        using var fixture = CandidateFixture.Create();
        var platform = new FakePlatform();
        var time = new ManualTimeProvider();

        var outcome = new KukaSimCandidateExecutionRunner(platform, time, fixture.HashForTest)
            .Run(fixture.CreateRequest(), "test-kukasim-arbitrary-candidate");

        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(KukaSimCandidateExecutionDisposition.Executed, outcome.Receipt.Payload.Disposition);
        Assert.Equal(fixture.Plan().CandidateId, outcome.Receipt.Payload.Submission.CandidateId);
        Assert.Equal("KR 210 R2700-2 C01", outcome.Receipt.Payload.Attempt.Result!.ComponentName);
        Assert.Equal("Integrated", outcome.Receipt.Payload.Attempt.Result.MotionExecution);
        Assert.Equal("Go", outcome.Receipt.Payload.Attempt.Result.ProgramMode);
        Assert.True(outcome.Receipt.Payload.SimulationPerformed);
        Assert.False(outcome.Receipt.Payload.OfficeLiteConnectionPerformed);
        Assert.False(outcome.Receipt.Payload.PhysicalControllerContacted);
        Assert.True(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.Single(platform.Invocations);
        Assert.Equal(fixture.ComponentPath, platform.Invocations[0].SimulationLayoutPath);
        Assert.True(KukaSimCandidateExecutionReceiptVerifier.Verify(outcome.Receipt, rehashCurrentFiles: false).Succeeded);
    }

    [Fact]
    public void Integrated_rejection_with_messages_and_zero_motion_is_not_infrastructure_failure()
    {
        using var fixture = CandidateFixture.Create();
        var plan = fixture.Plan();
        var resultPath = Path.Combine(fixture.Root, "rejected.tsv");
        File.WriteAllText(resultPath, RejectedRaw(plan.ProgramName));
        var result = KukaSimIntegratedRawParser.Parse(resultPath);
        var attempt = new KukaSimIntegratedAttempt
        {
            Role = "Candidate",
            ProgramName = plan.ProgramName,
            SourcePath = Path.Combine(plan.CandidateRoot, plan.Source.RelativePath),
            DataPath = Path.Combine(plan.CandidateRoot, plan.Data.RelativePath),
            RawResultPath = resultPath,
            TracePath = resultPath,
            Command = new KukaSimCommandObservation
            {
                ProcessStarted = true,
                ExitCode = 4,
                CleanupVerified = true,
                OwnedProcessIds = [1234]
            },
            Result = result,
            ResultCanonicalSha256 = ReceiptSerialization.ComputeCanonicalSha256(result)
        };

        Assert.Equal(
            KukaSimCandidateExecutionDisposition.RejectedByIntegratedInterpreter,
            KukaSimCandidateExecutionClassifier.Classify(plan, attempt));
        Assert.Empty(result.CompletedMotions);
        Assert.Contains(result.Messages, message => message.Text.Contains("P_MISSING", StringComparison.Ordinal));
    }

    [Fact]
    public void Runner_rejects_candidate_drift_before_process_discovery_or_launch()
    {
        using var fixture = CandidateFixture.Create();
        File.AppendAllText(Path.Combine(fixture.SourceRoot, "CELL.src"), "; drift\n");
        var platform = new FakePlatform();

        Assert.Throws<InvalidDataException>(() =>
            new KukaSimCandidateExecutionRunner(platform, new ManualTimeProvider(), fixture.HashForTest)
                .Run(fixture.CreateRequest(), "test-kukasim-candidate-drift"));
        Assert.Equal(0, platform.ProcessDiscoveryCount);
        Assert.Empty(platform.Invocations);
    }

    private static string ValidRaw(string programName) => $"""
        APPLICATION	True	True	True
        COMPONENT	KR 210 R2700-2 C01	True	2	mountplate
        CONFIG_AFTER	Integrated	{programName}	Go	True
        SYNCHRONIZATION	KrlIsNewer	Synchronized	Automatic
        STATEMENT_TREE_COUNT	8
        INTERPRETER_AFTER	Go	End	SawNonIdle=True
        SIMULATION_AFTER	Running=False	Paused=False	AtStart=False
        PROGRAM_FINISHED	True
        STATEMENT_EXECUTED	10	Kuka.Sim.Programming.Statements.MotionStatement	Motion	Immediate=True	DETAILS=MotionIdentifier=PTP;CurrentPointExpression=A_HOME;DisplayName=PTP A_HOME
        SAMPLE	1	BEFORE_START	STATE=Reset,MODE=Go	JOINTS=A1=0,A2=-90,A3=90,A4=0,A5=0,A6=0	TCP=X=1765,Y=0,Z=1910,NX=0,NY=0,NZ=-1
        SAMPLE	10	PROGRAM_FINISHED	STATE=End,MODE=Go	JOINTS=A1=0,A2=-90,A3=90,A4=0,A5=0,A6=0	TCP=X=1765,Y=0,Z=1910,NX=0,NY=0,NZ=-1
        SUCCESS	True
        """;

    private static string RejectedRaw(string programName) => $"""
        APPLICATION	True	True	True
        COMPONENT	KR 210 R2700-2 C01	True	2	mountplate
        CONFIG_AFTER	Integrated	{programName}	Go	True
        SYNCHRONIZATION	KrlIsNewer	Synchronized	Automatic
        STATEMENT_TREE_COUNT	8
        INTERPRETER_AFTER	Go	Stop	SawNonIdle=True
        SIMULATION_AFTER	Running=False	Paused=True	AtStart=False
        PROGRAM_FINISHED	False
        SAMPLE	1	BEFORE_START	STATE=Reset,MODE=Go	JOINTS=A1=0,A2=-90,A3=90,A4=0,A5=0,A6=0	TCP=X=1765,Y=0,Z=1910,NX=0,NY=0,NZ=-1
        MESSAGE	1	Acknowledgment	A memory value with name 'P_MISSING' was not defined.
        SUCCESS	False
        """;

    private sealed class CandidateFixture : IDisposable
    {
        private CandidateFixture(string root)
        {
            Root = root;
            SourceRoot = Path.Combine(root, "source");
            ReceiptPath = Path.Combine(root, "candidate.receipt.json");
            Directory.CreateDirectory(SourceRoot);
            File.WriteAllText(Path.Combine(SourceRoot, "CELL.src"), "DEF CELL()\n BAS(#INITMOV,0)\n PTP HOME\nEND\n");
            File.WriteAllText(Path.Combine(SourceRoot, "CELL.dat"), "DEFDAT CELL PUBLIC\n DECL E6AXIS HOME={A1 0,A2 -90,A3 90,A4 0,A5 0,A6 0}\nENDDAT\n");
            var receipt = new RawKrlCandidateRunner().Run(
                new RawKrlCandidateRequest { SourceRoot = SourceRoot, ReceiptOutputPath = ReceiptPath },
                "test-kukasim-candidate-intake").Receipt;
            RawKrlCandidateReceiptWriter.WriteNew(ReceiptPath, SourceRoot, receipt);
            InstallRoot = Path.Combine(root, "KUKA.Sim 4.10");
            foreach (var relative in new[]
            {
                "VisualComponents.Engine.exe",
                "VisualComponents.Engine.Launcher.exe",
                KukaSimInstallationDiscovery.BootstrapPluginFileName,
                "Create3D.Shared.dll",
                "UX.Shared.dll",
                "Caliburn.Micro.dll",
                Path.Combine("KUKA", "Kuka.Sim.ProgrammingCore.dll")
            })
            {
                var path = Path.Combine(InstallRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "synthetic " + relative);
            }

            ComponentPath = Path.Combine(root, "KR 210 R2700-2 C01.vcmx");
            File.WriteAllText(ComponentPath, "synthetic exact component");
        }

        public string Root { get; }
        public string SourceRoot { get; }
        public string ReceiptPath { get; }
        public string InstallRoot { get; }
        public string ComponentPath { get; }

        public static CandidateFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"kuka-lab-sim-candidate-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            return new CandidateFixture(root);
        }

        public NativeKssCandidateSubmissionPlan Plan() => new NativeKssCandidateSubmissionPlanner().Plan(
            new NativeKssCandidateSubmissionRequest
            {
                CandidateRoot = SourceRoot,
                CandidateReceiptPath = ReceiptPath,
                ProgramRelativeStem = "CELL",
                MaximumStartCommands = 8
            });

        public KukaSimCandidateExecutionRequest CreateRequest() => new()
        {
            EnginePath = Path.Combine(InstallRoot, "VisualComponents.Engine.exe"),
            LauncherPath = Path.Combine(InstallRoot, "VisualComponents.Engine.Launcher.exe"),
            BootstrapPluginPath = Path.Combine(InstallRoot, KukaSimInstallationDiscovery.BootstrapPluginFileName),
            Create3DSharedPath = Path.Combine(InstallRoot, "Create3D.Shared.dll"),
            ComponentPath = ComponentPath,
            EvidenceDirectory = Path.Combine(Root, "evidence"),
            Submission = new NativeKssCandidateSubmissionRequest
            {
                CandidateRoot = SourceRoot,
                CandidateReceiptPath = ReceiptPath,
                ProgramRelativeStem = "CELL",
                MaximumStartCommands = 8
            },
            TimeoutSeconds = 30,
            GuiExecutionAuthorized = true,
            AuthorizationReference = "test-authorization"
        };

        public string HashForTest(string path) =>
            string.Equals(Path.GetFullPath(path), Path.GetFullPath(ComponentPath), StringComparison.OrdinalIgnoreCase)
                ? KukaSimCandidateExecutionContract.ExactComponentSha256
                : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class FakePlatform : IKukaSimCandidateExecutionPlatform
    {
        public List<KukaSimCandidateExecutionProcessRequest> Invocations { get; } = [];
        public int ProcessDiscoveryCount { get; private set; }

        public IReadOnlyList<KukaSimProcessObservation> FindRunningProcesses(string launcherPath, string enginePath)
        {
            ProcessDiscoveryCount++;
            return [];
        }

        public KukaSimProcessResult Run(KukaSimCandidateExecutionProcessRequest request)
        {
            Invocations.Add(request);
            File.WriteAllText(request.ResultPath, ValidRaw(request.ProgramName));
            File.WriteAllText(request.TracePath, "ENTERED\nEXIT_REQUESTED code=0\n");
            return new KukaSimProcessResult(0, true, false, false, true, 100, [4321], string.Empty);
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 8, 29, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
