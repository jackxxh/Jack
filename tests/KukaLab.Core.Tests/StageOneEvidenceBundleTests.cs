using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class StageOneEvidenceBundleTests
{
    [Fact]
    public void Raw_candidate_composition_keeps_candidate_native_execution_explicitly_not_run()
    {
        using var fixture = RawCandidateFixture.Create();
        var runner = new StageOneEvidenceBundleRunner(TimeProvider.System, new AcceptedBaselineStub(fixture.Root));

        var outcome = runner.Run(fixture.Request(), "test-stage-one-raw");
        var verification = StageOneEvidenceBundleReceiptVerifier.Verify(outcome.Receipt, rehashCurrentInputs: false);

        Assert.True(outcome.Succeeded);
        Assert.True(verification.Succeeded, string.Join("; ", verification.Errors));
        Assert.Equal(StageOneCandidateReadiness.IdentityVerified, outcome.Receipt.Payload.Candidate.Readiness);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.Candidate.CandidateNativeStatus);
        Assert.Equal("NotRun", outcome.Receipt.Payload.Candidate.CandidateKukaSimStatus);
        Assert.False(outcome.Receipt.Payload.EnvironmentBaseline.CandidateNativeExecutionValidated);
        Assert.False(outcome.Receipt.Payload.VendorSoftwareStarted);
        Assert.False(outcome.Receipt.Payload.RhinoAccessed);
    }

    [Fact]
    public void Recomputed_payload_cannot_promote_candidate_to_native_execution()
    {
        using var fixture = RawCandidateFixture.Create();
        var runner = new StageOneEvidenceBundleRunner(TimeProvider.System, new AcceptedBaselineStub(fixture.Root));
        var receipt = runner.Run(fixture.Request(), "test-stage-one-promotion").Receipt;
        var payload = receipt.Payload with
        {
            Candidate = receipt.Payload.Candidate with
            {
                CandidateNativeStatus = NativeKssStatus.BoundedExecutionValidated,
                CandidateKukaSimStatus = "Validated"
            },
            EnvironmentBaseline = receipt.Payload.EnvironmentBaseline with
            {
                CandidateNativeExecutionValidated = true
            }
        };
        var tampered = receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var verification = StageOneEvidenceBundleReceiptVerifier.Verify(tampered, rehashCurrentInputs: false);

        Assert.False(verification.Succeeded);
        Assert.Contains(verification.Errors, error => error.Contains("NotRun", StringComparison.Ordinal));
        Assert.Contains(verification.Errors, error => error.Contains("environment baseline claims", StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_package_candidate_requires_current_package_and_bound_static_preflight()
    {
        using var package = KrlStaticPreflightTests.PreflightFixture.Create("minimal-ptp-lin-valid");
        var preflight = package.Run("test-stage-one-package-preflight");
        package.Write(preflight.Receipt);
        var output = Path.Combine(package.Root, "output", "stage-one-package.receipt.json");
        var runner = new StageOneEvidenceBundleRunner(TimeProvider.System, new AcceptedBaselineStub(package.Root));

        var outcome = runner.Run(
            new StageOneEvidenceBundleRequest
            {
                CandidateKind = StageOneCandidateEvidenceKind.ValidationPackageStaticPreflight,
                CandidateRoot = package.PackageRoot,
                CandidateReceiptPath = package.PreflightReceiptPath,
                ValidationPackageReceiptPath = package.PackageReceiptPath,
                ComparisonReceiptPath = Path.Combine(package.Root, "item9d.json"),
                VirtualLoopReceiptPath = Path.Combine(package.Root, "item9e.json"),
                ReceiptOutputPath = output
            },
            "test-stage-one-package");
        var verification = StageOneEvidenceBundleReceiptVerifier.Verify(outcome.Receipt, rehashCurrentInputs: false);

        Assert.True(outcome.Succeeded);
        Assert.True(verification.Succeeded, string.Join("; ", verification.Errors));
        Assert.Equal(StageOneCandidateReadiness.StaticPreflightReady, outcome.Receipt.Payload.Candidate.Readiness);
        Assert.True(outcome.Receipt.Payload.Candidate.MotionInstructionCount > 0);
        Assert.Equal(
            new[]
            {
                "candidate-validation-package",
                "candidate-krl-static-preflight",
                "item9d-offline-loop-comparison",
                "item9e-kukasim-officelite-virtual-loop"
            },
            outcome.Receipt.Payload.Inputs.Select(input => input.Role));
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.Candidate.CandidateNativeStatus);
    }

    [Fact]
    public void Recomputed_payload_cannot_swap_item9d_and_item9e_roles()
    {
        using var fixture = RawCandidateFixture.Create();
        var runner = new StageOneEvidenceBundleRunner(TimeProvider.System, new AcceptedBaselineStub(fixture.Root));
        var receipt = runner.Run(fixture.Request(), "test-stage-one-role-swap").Receipt;
        var inputs = receipt.Payload.Inputs.ToList();
        (inputs[1], inputs[2]) = (inputs[2], inputs[1]);
        var payload = receipt.Payload with { Inputs = inputs };
        var tampered = receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var verification = StageOneEvidenceBundleReceiptVerifier.Verify(tampered, rehashCurrentInputs: false);

        Assert.False(verification.Succeeded);
        Assert.Contains(verification.Errors, error => error.Contains("input roles", StringComparison.Ordinal));
    }

    [Fact]
    public void Recomputed_payload_cannot_replace_accepted_baseline_hash()
    {
        using var fixture = RawCandidateFixture.Create();
        var runner = new StageOneEvidenceBundleRunner(TimeProvider.System, new AcceptedBaselineStub(fixture.Root));
        var receipt = runner.Run(fixture.Request(), "test-stage-one-baseline-hash").Receipt;
        var inputs = receipt.Payload.Inputs.ToList();
        inputs[1] = inputs[1] with
        {
            ActualReceiptSha256 = new string('F', 64),
            ExpectedReceiptSha256 = new string('F', 64)
        };
        var payload = receipt.Payload with { Inputs = inputs };
        var tampered = receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var verification = StageOneEvidenceBundleReceiptVerifier.Verify(tampered, rehashCurrentInputs: false);

        Assert.False(verification.Succeeded);
        Assert.Contains(verification.Errors, error => error.Contains("item-9D/item-9E", StringComparison.Ordinal));
    }

    private sealed class AcceptedBaselineStub(string root) : IStageOneBaselineVerifier
    {
        public StageOneVerifiedBaseline Verify(string comparisonReceiptPath, string virtualLoopReceiptPath) => new(
        [
            new StageOneEvidenceInput
            {
                Role = "item9d-offline-loop-comparison",
                Path = Path.Combine(root, "item9d.json"),
                SchemaIdentity = OfflineLoopComparisonContract.ReceiptSchemaIdentity,
                ReceiptId = "item9d-test",
                PayloadSha256 = new string('D', 64),
                ActualReceiptSha256 = StageOneEvidenceBundleContract.AcceptedComparisonReceiptSha256,
                ExpectedReceiptSha256 = StageOneEvidenceBundleContract.AcceptedComparisonReceiptSha256
            },
            new StageOneEvidenceInput
            {
                Role = "item9e-kukasim-officelite-virtual-loop",
                Path = Path.Combine(root, "item9e.json"),
                SchemaIdentity = KukaSimOfficeLiteVirtualLoopContract.ReceiptSchemaIdentity,
                ReceiptId = "item9e-test",
                PayloadSha256 = new string('E', 64),
                ActualReceiptSha256 = StageOneEvidenceBundleContract.AcceptedVirtualLoopReceiptSha256,
                ExpectedReceiptSha256 = StageOneEvidenceBundleContract.AcceptedVirtualLoopReceiptSha256
            }
        ]);
    }

    private sealed class RawCandidateFixture : IDisposable
    {
        private RawCandidateFixture(string root)
        {
            Root = root;
            SourceRoot = Path.Combine(root, "source");
            CandidateReceiptPath = Path.Combine(root, "candidate", "raw-krl.receipt.json");
            OutputPath = Path.Combine(root, "output", "stage-one.receipt.json");
            Directory.CreateDirectory(SourceRoot);
            File.WriteAllText(
                Path.Combine(SourceRoot, "CELL.src"),
                "DEF CELL()\n BAS(#INITMOV,0)\n PTP HOME\nEND\n");
            File.WriteAllText(
                Path.Combine(SourceRoot, "CELL.dat"),
                "DEFDAT CELL PUBLIC\n DECL E6AXIS HOME={A1 0,A2 -90,A3 90,A4 0,A5 0,A6 0}\nENDDAT\n");
            var receipt = new RawKrlCandidateRunner().Run(
                new RawKrlCandidateRequest
                {
                    SourceRoot = SourceRoot,
                    ReceiptOutputPath = CandidateReceiptPath
                },
                "stage-one-candidate").Receipt;
            RawKrlCandidateReceiptWriter.WriteNew(CandidateReceiptPath, SourceRoot, receipt);
        }

        public string Root { get; }
        public string SourceRoot { get; }
        public string CandidateReceiptPath { get; }
        public string OutputPath { get; }

        public static RawCandidateFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "kuka-lab-stage-one-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new RawCandidateFixture(root);
        }

        public StageOneEvidenceBundleRequest Request() => new()
        {
            CandidateKind = StageOneCandidateEvidenceKind.RawKrlIdentity,
            CandidateRoot = SourceRoot,
            CandidateReceiptPath = CandidateReceiptPath,
            ComparisonReceiptPath = Path.Combine(Root, "item9d.json"),
            VirtualLoopReceiptPath = Path.Combine(Root, "item9e.json"),
            ReceiptOutputPath = OutputPath
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
