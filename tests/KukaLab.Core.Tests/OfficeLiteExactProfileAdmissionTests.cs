using System.Text;
using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class OfficeLiteExactProfileAdmissionTests
{
    [Fact]
    public void Accepted_exact_profile_runs_the_downstream_operation_once_and_binds_receipt_bytes()
    {
        using var environment = TestEnvironment.Create(accepted: true);
        var invocations = 0;
        var gate = new OfficeLiteExactProfileAdmissionGate(new AcceptingReceiptVerifier());

        var result = gate.RunAfterAdmission(
            environment.Request,
            admission =>
            {
                invocations++;
                Assert.Equal(environment.ReceiptPath, admission.AcceptanceReceiptPath);
                Assert.Equal(environment.ReceiptSha256, admission.AcceptanceReceiptSha256);
                Assert.Equal("#KR210R2700_2 C01 FLR", admission.RuntimeRobotIdentity);
                Assert.Equal("KRC5", admission.ActiveCabinetKind);
                return "started";
            });

        Assert.Equal("started", result);
        Assert.Equal(1, invocations);
    }

    [Fact]
    public void Rejected_exact_profile_never_runs_the_downstream_operation()
    {
        using var environment = TestEnvironment.Create(accepted: false);
        var invocations = 0;
        var gate = new OfficeLiteExactProfileAdmissionGate(new AcceptingReceiptVerifier());

        var exception = Assert.Throws<InvalidDataException>(() => gate.RunAfterAdmission(
            environment.Request,
            _ =>
            {
                invocations++;
                return "must-not-run";
            }));

        Assert.Contains("not accepted", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, invocations);
    }

    [Fact]
    public void Wrong_expected_project_never_runs_the_downstream_operation()
    {
        using var environment = TestEnvironment.Create(accepted: true);
        var invocations = 0;
        var gate = new OfficeLiteExactProfileAdmissionGate(new AcceptingReceiptVerifier());

        var exception = Assert.Throws<InvalidDataException>(() => gate.RunAfterAdmission(
            environment.Request with { ExpectedProjectName = "InitialProject" },
            _ =>
            {
                invocations++;
                return "must-not-run";
            }));

        Assert.Contains("project", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, invocations);
    }

    [Fact]
    public void Strict_receipt_verification_failure_never_runs_the_downstream_operation()
    {
        using var environment = TestEnvironment.Create(accepted: true);
        var invocations = 0;
        var gate = new OfficeLiteExactProfileAdmissionGate(new RejectingReceiptVerifier());

        var exception = Assert.Throws<InvalidDataException>(() => gate.RunAfterAdmission(
            environment.Request,
            _ =>
            {
                invocations++;
                return "must-not-run";
            }));

        Assert.Contains("integrity", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, invocations);
    }

    [Fact]
    public void Default_strict_verifier_fails_closed_on_structurally_incomplete_acceptance_evidence()
    {
        using var environment = TestEnvironment.Create(accepted: true);
        var invocations = 0;
        var gate = new OfficeLiteExactProfileAdmissionGate();

        var exception = Assert.Throws<InvalidDataException>(() => gate.RunAfterAdmission(
            environment.Request,
            _ =>
            {
                invocations++;
                return "must-not-run";
            }));

        Assert.Contains("verification failed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, invocations);
    }

    [Fact]
    public void Protected_license_path_is_rejected_before_any_file_read_or_downstream_operation()
    {
        var invocations = 0;
        var gate = new OfficeLiteExactProfileAdmissionGate(new AcceptingReceiptVerifier());
        var request = new OfficeLiteExactProfileAdmissionRequest
        {
            AcceptanceReceiptPath = @"C:\ProgramData\Visual Components\Visual Components License Server 2.0\lservrc.dat"
        };

        var exception = Assert.Throws<UnauthorizedAccessException>(() => gate.RunAfterAdmission(
            request,
            _ =>
            {
                invocations++;
                return "must-not-run";
            }));

        Assert.Contains("protected", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, invocations);
    }

    private sealed class AcceptingReceiptVerifier : IOfficeLiteExactProfileAdmissionReceiptVerifier
    {
        public OfficeLiteExactProfileAcceptanceVerificationResult Verify(
            OfficeLiteExactProfileAcceptanceReceipt receipt) => new()
        {
            ReceiptId = receipt.Payload.ReceiptId,
            Succeeded = true,
            PayloadSha256 = receipt.PayloadSha256
        };
    }

    private sealed class RejectingReceiptVerifier : IOfficeLiteExactProfileAdmissionReceiptVerifier
    {
        public OfficeLiteExactProfileAcceptanceVerificationResult Verify(
            OfficeLiteExactProfileAcceptanceReceipt receipt) => new()
        {
            ReceiptId = receipt.Payload.ReceiptId,
            Succeeded = false,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = ["synthetic current-input drift"]
        };
    }

    private sealed class TestEnvironment : IDisposable
    {
        private TestEnvironment(string root, bool accepted)
        {
            Root = root;
            ReceiptPath = Path.GetFullPath(Path.Combine(root, "exact-profile-acceptance.receipt.json"));
            var summary = new OfficeLiteExactProfileAcceptanceSummary
            {
                RuntimeRobotIdentity = "#KR210R2700_2 C01 FLR",
                RuntimeKssVersion = "V8.7.8.671",
                RuntimeCurrentProject = OfficeLiteExactProfileAcceptanceContract.DefaultExpectedProjectName,
                RuntimeActiveProject = OfficeLiteExactProfileAcceptanceContract.DefaultExpectedProjectName,
                ActiveMachineDataIdentity = "#KR210R2700_2 C01 FLR",
                ActiveCabinetKind = "KRC5",
                ActiveControllerSoftwareFamily = "KUKA V8.7",
                RuntimeRobotIdentityMatched = accepted,
                RuntimeKssMatched = accepted,
                ActiveProjectMatched = accepted,
                DownloadedProjectBound = accepted,
                MachineDataIdentityMatched = accepted,
                CabinetMatched = accepted,
                ControllerSoftwareFamilyMatched = accepted,
                AxisLimitsMatched = accepted,
                DetailedAxesMatched = accepted,
                ToolDataMatched = accepted,
                BaseDataMatched = accepted,
                LoadDataMatched = accepted,
                ExactKukaSimComponentMatched = accepted,
                Accepted = accepted
            };
            var payload = new OfficeLiteExactProfileAcceptancePayload
            {
                ReceiptId = "synthetic-exact-profile",
                AttemptId = "synthetic-exact-profile",
                TerminalClassification = accepted
                    ? EnvironmentTerminalClassification.Ready
                    : EnvironmentTerminalClassification.Failed,
                ExpectedProjectName = OfficeLiteExactProfileAcceptanceContract.DefaultExpectedProjectName,
                ProfileReadbackReceipt = null!,
                ActiveProjectDownloadReceipt = null!,
                ActiveControllerBaselineReceipt = null!,
                TrustedControllerBaselineReceipt = null!,
                Summary = summary,
                ReadOnlyComposition = true,
                NativeKssStatus = NativeKssStatus.NotRun,
                EnvironmentReusable = true
            };
            var receipt = new OfficeLiteExactProfileAcceptanceReceipt
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
            };
            File.WriteAllText(ReceiptPath, ReceiptSerialization.ToJson(receipt), new UTF8Encoding(false));
            ReceiptSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(ReceiptPath)));
            Request = new OfficeLiteExactProfileAdmissionRequest
            {
                AcceptanceReceiptPath = ReceiptPath
            };
        }

        public string Root { get; }
        public string ReceiptPath { get; }
        public string ReceiptSha256 { get; }
        public OfficeLiteExactProfileAdmissionRequest Request { get; }

        public static TestEnvironment Create(bool accepted)
        {
            var root = Path.Combine(Path.GetTempPath(), $"kuka-lab-exact-profile-admission-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            return new TestEnvironment(root, accepted);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
