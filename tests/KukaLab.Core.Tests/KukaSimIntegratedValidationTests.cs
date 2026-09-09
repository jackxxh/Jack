using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class KukaSimIntegratedValidationTests
{
    [Fact]
    public void Default_request_binds_exact_C01_and_frozen_item9_fixtures()
    {
        var labRoot = Path.Combine(Path.GetTempPath(), "kuka-lab-root");
        var request = KukaSimIntegratedValidationRequest.CreateDefault(
            labRoot,
            Path.Combine(Path.GetTempPath(), "kuka-lab-evidence"),
            false,
            string.Empty);

        Assert.EndsWith("KR 210 R2700-2 C01.vcmx", request.ComponentPath, StringComparison.Ordinal);
        Assert.EndsWith(Path.Combine("minimal-ptp-lin-valid", "controller-files", "LAB_MINIMAL.src"), request.ValidSourcePath, StringComparison.Ordinal);
        Assert.EndsWith(Path.Combine("minimal-missing-target-invalid", "controller-files", "LAB_MISSING_TARGET.dat"), request.InvalidDataPath, StringComparison.Ordinal);
    }

    [Fact]
    public void Positive_raw_result_proves_Go_target_order_and_axis_tcp_path()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("positive.tsv", PositiveRaw());
        var result = KukaSimIntegratedRawParser.Parse(path);
        var attempt = Attempt("Positive", KukaSimIntegratedValidationContract.PositiveProgramName, 0, result, path);

        Assert.Equal("Go", result.InterpreterModeAfter);
        Assert.Equal("End", result.InterpreterStateAfter);
        Assert.Equal(["A_HOME", "P_START", "P_END"], result.CompletedMotions.Select(motion => motion.Target));
        Assert.Equal(["PTP", "PTP", "LIN"], result.CompletedMotions.Select(motion => motion.MotionType));
        Assert.Empty(KukaSimIntegratedValidationRunner.ValidatePositive(attempt));
    }

    [Fact]
    public void Negative_raw_result_rejects_missing_target_before_completed_motion()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("negative.tsv", NegativeRaw());
        var result = KukaSimIntegratedRawParser.Parse(path);
        var attempt = Attempt("Negative", KukaSimIntegratedValidationContract.NegativeProgramName, 4, result, path);

        Assert.Empty(result.CompletedMotions);
        Assert.False(result.ProgramFinished);
        Assert.Contains(result.Messages, message => message.Text.Contains("P_MISSING", StringComparison.Ordinal));
        Assert.Empty(KukaSimIntegratedValidationRunner.ValidateNegative(attempt));
    }

    [Fact]
    public void Step_mode_or_runtime_target_drift_fails_closed()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("positive.tsv", PositiveRaw());
        var parsed = KukaSimIntegratedRawParser.Parse(path);
        var drifted = parsed with
        {
            ProgramMode = "MStep",
            CompletedMotions = parsed.CompletedMotions.Select((motion, index) =>
                index == 1 ? motion with { Target = "P_WRONG" } : motion).ToList()
        };

        var errors = KukaSimIntegratedValidationRunner.ValidatePositive(
            Attempt("Positive", KukaSimIntegratedValidationContract.PositiveProgramName, 0, drifted, path));

        Assert.Contains(errors, error => error.Contains("Integrated/LAB_MINIMAL/Go", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("runtime motion order", StringComparison.Ordinal));
    }

    [Fact]
    public void Semantic_receipt_verification_rejects_recomputed_Go_tamper()
    {
        using var temp = new TempDirectory();
        var positivePath = temp.Write("positive.tsv", PositiveRaw());
        var negativePath = temp.Write("negative.tsv", NegativeRaw());
        var positive = Attempt("Positive", KukaSimIntegratedValidationContract.PositiveProgramName, 0, KukaSimIntegratedRawParser.Parse(positivePath), positivePath);
        var negative = Attempt("Negative", KukaSimIntegratedValidationContract.NegativeProgramName, 4, KukaSimIntegratedRawParser.Parse(negativePath), negativePath);
        var payload = Payload(temp.Path, positive, negative);
        var receipt = new KukaSimIntegratedValidationReceipt
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        Assert.True(KukaSimIntegratedValidationReceiptVerifier.Verify(receipt, rehashCurrentFiles: false).Succeeded);

        var tamperedPositive = positive with
        {
            Result = positive.Result! with { ProgramMode = "IStep" }
        };
        tamperedPositive = tamperedPositive with
        {
            ResultCanonicalSha256 = ReceiptSerialization.ComputeCanonicalSha256(tamperedPositive.Result)
        };
        var tamperedPayload = payload with { Positive = tamperedPositive };
        var tampered = receipt with
        {
            Payload = tamperedPayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(tamperedPayload)
        };

        var verification = KukaSimIntegratedValidationReceiptVerifier.Verify(tampered, rehashCurrentFiles: false);
        Assert.False(verification.Succeeded);
        Assert.Contains(
            verification.Errors,
            error => error.Contains("positive: positive configuration is not Integrated/LAB_MINIMAL/Go/trace", StringComparison.Ordinal));
    }

    [Fact]
    public void Incomplete_attempt_reports_missing_evidence_instead_of_throwing_on_empty_path()
    {
        using var temp = new TempDirectory();
        var positivePath = temp.Write("positive.tsv", PositiveRaw());
        var positive = Attempt("Positive", KukaSimIntegratedValidationContract.PositiveProgramName, 0, KukaSimIntegratedRawParser.Parse(positivePath), positivePath);
        var incompleteNegative = new KukaSimIntegratedAttempt
        {
            Role = "Negative",
            ProgramName = KukaSimIntegratedValidationContract.NegativeProgramName
        };
        var payload = Payload(temp.Path, positive, incompleteNegative) with
        {
            TerminalClassification = EnvironmentTerminalClassification.Failed,
            ValidationStatus = "NotValidated",
            SimulationPerformed = false
        };
        var receipt = new KukaSimIntegratedValidationReceipt
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var verification = KukaSimIntegratedValidationReceiptVerifier.Verify(receipt, rehashCurrentFiles: true);

        Assert.False(verification.Succeeded);
        Assert.Contains(verification.Errors, error => error.Contains("Negative stored result is missing", StringComparison.Ordinal));
    }

    [Fact]
    public void Receipt_writer_surfaces_exact_semantic_rejection()
    {
        using var temp = new TempDirectory();
        var positivePath = temp.Write("positive.tsv", PositiveRaw());
        var positive = Attempt("Positive", KukaSimIntegratedValidationContract.PositiveProgramName, 0, KukaSimIntegratedRawParser.Parse(positivePath), positivePath);
        var payload = Payload(temp.Path, positive, new KukaSimIntegratedAttempt
        {
            Role = "Negative",
            ProgramName = KukaSimIntegratedValidationContract.NegativeProgramName
        }) with
        {
            TerminalClassification = EnvironmentTerminalClassification.Failed,
            ValidationStatus = "NotValidated",
            SimulationPerformed = false
        };
        var receipt = new KukaSimIntegratedValidationReceipt
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            KukaSimIntegratedValidationReceiptWriter.WriteNew(System.IO.Path.Combine(temp.Path, "receipt.json"), receipt));

        Assert.Contains("Negative stored result is missing", exception.Message, StringComparison.Ordinal);
    }

    private static KukaSimIntegratedAttempt Attempt(string role, string programName, int exitCode, KukaSimIntegratedRawResult result, string rawPath) => new()
    {
        Role = role,
        ProgramName = programName,
        SourcePath = Path.GetFullPath(rawPath),
        DataPath = Path.GetFullPath(rawPath),
        RawResultPath = Path.GetFullPath(rawPath),
        TracePath = Path.GetFullPath(rawPath),
        Command = new KukaSimCommandObservation
        {
            ProcessStarted = true,
            ExitCode = exitCode,
            CleanupVerified = true,
            DurationMilliseconds = 10,
            OwnedProcessIds = [1234]
        },
        Result = result,
        ResultCanonicalSha256 = ReceiptSerialization.ComputeCanonicalSha256(result)
    };

    private static KukaSimIntegratedValidationPayload Payload(
        string root,
        KukaSimIntegratedAttempt positive,
        KukaSimIntegratedAttempt negative)
    {
        var checkIds = new[]
        {
            "exact-c01-component", "valid-src", "valid-dat", "invalid-src", "invalid-dat",
            "gui-authorization", "process-ownership", "evidence-directory", "pinned-script",
            "positive-process-cleanup", "negative-process-cleanup", "positive-integrated-run", "negative-integrated-rejection"
        };
        var fileHashes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["exact-c01-component"] = KukaSimIntegratedValidationContract.ExactKr210R2700ComponentSha256,
            ["valid-src"] = KukaSimIntegratedValidationContract.ValidSourceSha256,
            ["valid-dat"] = KukaSimIntegratedValidationContract.ValidDataSha256,
            ["invalid-src"] = KukaSimIntegratedValidationContract.InvalidSourceSha256,
            ["invalid-dat"] = KukaSimIntegratedValidationContract.InvalidDataSha256,
            ["kukasim-integrated-script"] = KukaSimIntegratedValidationContract.EmbeddedScriptSha256,
            ["kukasim-engine"] = new string('A', 64),
            ["kukasim-script-starter"] = new string('A', 64),
            ["kukasim-create3d-api"] = new string('A', 64),
            ["kukasim-positive-result"] = new string('A', 64),
            ["kukasim-positive-trace"] = new string('A', 64),
            ["kukasim-negative-result"] = new string('A', 64),
            ["kukasim-negative-trace"] = new string('A', 64)
        };
        return new KukaSimIntegratedValidationPayload
        {
            ReceiptId = "kukasim-integrated-validation-test-ready",
            AttemptId = "test-ready",
            CoreAssemblySha256 = new string('B', 64),
            Runtime = new RuntimeEnvironment
            {
                OsDescription = "test",
                FrameworkDescription = "test",
                ProcessArchitecture = "X64"
            },
            StartedAtUtc = DateTimeOffset.UnixEpoch,
            CompletedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(1),
            DurationMilliseconds = 1000,
            TerminalClassification = EnvironmentTerminalClassification.Ready,
            ValidationStatus = "ExactC01IntegratedValidated",
            Provider = "Integrated",
            EnginePath = Path.Combine(root, "engine.exe"),
            ComponentPath = Path.Combine(root, "component.vcmx"),
            EvidenceDirectory = root,
            EmbeddedScriptSha256 = KukaSimIntegratedValidationContract.EmbeddedScriptSha256,
            GuiExecutionAuthorized = true,
            AuthorizationReference = "test-authorization",
            SimulationPerformed = true,
            NativeKssStatus = NativeKssStatus.NotRun,
            Positive = positive,
            Negative = negative,
            Checks = checkIds.Select(id => new EnvironmentCheck { Id = id, Status = EnvironmentCheckStatus.Passed, Detail = "test" }).ToList(),
            Files = fileHashes.Select(pair => new EnvironmentFileObservation
            {
                Id = pair.Key,
                Path = Path.Combine(root, pair.Key + ".bin"),
                Exists = true,
                Bytes = 1,
                Sha256 = pair.Value
            }).ToList(),
            SideEffects = ["CreateProbeScript:test", "StartOwnedKukaSim:Positive:test", "StartOwnedKukaSim:Negative:test"],
            EnvironmentReusable = true,
            UnsupportedGaps = KukaSimIntegratedValidationContract.RequiredUnsupportedGaps.ToList()
        };
    }

    private static string PositiveRaw() => """
        APPLICATION	True	True	True
        COMPONENT	KR 210 R2700-2 C01	True	2	mountplate
        CONFIG_AFTER	Integrated	LAB_MINIMAL	Go	True
        SYNCHRONIZATION	KrlIsNewer	Synchronized	Automatic
        STATEMENT_TREE_COUNT	20
        INTERPRETER_AFTER	Go	End	SawNonIdle=True
        SIMULATION_AFTER	Running=False	Paused=False	AtStart=False
        PROGRAM_FINISHED	True
        STATEMENT_EXECUTED	10	Kuka.Sim.Programming.Statements.MotionStatement	Motion	Immediate=True	DETAILS=MotionIdentifier=PTP;CurrentPointExpression=A_HOME;DisplayName=PTP A_HOME
        STATEMENT_EXECUTED	20	Kuka.Sim.Programming.Statements.MotionStatement	Motion	Immediate=True	DETAILS=MotionIdentifier=PTP;CurrentPointExpression=P_START;DisplayName=PTP P_START
        STATEMENT_EXECUTED	30	Kuka.Sim.Programming.Statements.MotionStatement	Motion	Immediate=True	DETAILS=MotionIdentifier=LIN;CurrentPointExpression=P_END;DisplayName=LIN P_END
        PROGRAM_FINISHED	123.456
        SAMPLE	1	BEFORE_START	STATE=,MODE=	JOINTS=A1=0,A2=-90,A3=90,A4=0,A5=0,A6=0	TCP=X=1765,Y=0,Z=1910,NX=0,NY=0,NZ=-1
        SAMPLE	20	STATEMENT_EXECUTED	STATE=Active,MODE=Go	JOINTS=A1=3,A2=-70,A3=90,A4=0,A5=70,A6=3	TCP=X=1900,Y=-100,Z=1200,NX=0,NY=0,NZ=-1
        SAMPLE	30	PROGRAM_FINISHED	STATE=End,MODE=Go	JOINTS=A1=-3,A2=-70,A3=90,A4=0,A5=70,A6=-3	TCP=X=1900,Y=100,Z=1200,NX=0,NY=0,NZ=-1
        SUCCESS	True
        """;

    private static string NegativeRaw() => """
        APPLICATION	True	True	True
        COMPONENT	KR 210 R2700-2 C01	True	2	mountplate
        CONFIG_AFTER	Integrated	LAB_MISSING_TARGET	Go	True
        SYNCHRONIZATION	KrlIsNewer	Synchronized	Automatic
        STATEMENT_TREE_COUNT	11
        INTERPRETER_AFTER	Go	Stop	SawNonIdle=True
        SIMULATION_AFTER	Running=False	Paused=True	AtStart=False
        PROGRAM_FINISHED	False
        SAMPLE	1	BEFORE_START	STATE=,MODE=	JOINTS=A1=0,A2=-90,A3=90,A4=0,A5=0,A6=0	TCP=X=1765,Y=0,Z=1910,NX=0,NY=0,NZ=-1
        SAMPLE	2	POLL	STATE=Stop,MODE=Go	JOINTS=A1=0,A2=-90,A3=90,A4=0,A5=0,A6=0	TCP=X=1765,Y=0,Z=1910,NX=0,NY=0,NZ=-1
        MESSAGE	1	Acknowledgment	A memory value with name 'P_MISSING' was not defined.
        SUCCESS	False
        """;

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kuka-lab-integrated-tests", Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public string Write(string name, string content)
        {
            var path = System.IO.Path.Combine(Path, name);
            File.WriteAllText(path, content.Replace("\r\n", "\n", StringComparison.Ordinal), new System.Text.UTF8Encoding(false));
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
