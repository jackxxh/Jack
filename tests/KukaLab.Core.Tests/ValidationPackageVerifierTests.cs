using System.Security.Cryptography;
using System.Text.Json.Nodes;
using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class ValidationPackageVerifierTests
{
    [Fact]
    public void Valid_package_passes_byte_sensitive_integrity_without_vendor_claims()
    {
        using var fixture = PackageFixture.Create();

        var outcome = fixture.Verify("test-validation-package-valid");
        var json = ReceiptSerialization.ToJson(outcome.Receipt);

        Assert.True(outcome.Succeeded, FailureDetails(outcome));
        Assert.Equal(outcome.Receipt.Payload.DeclaredPackageId, outcome.Receipt.Payload.ComputedPackageId);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.NativeKss.Status);
        Assert.DoesNotContain(fixture.PackageRoot, json, StringComparison.OrdinalIgnoreCase);
        Assert.True(ValidationPackageReceiptVerifier.VerifyIntegrity(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Controller_file_tamper_fails_inventory_and_package_identity()
    {
        using var fixture = PackageFixture.Create();
        File.AppendAllText(Path.Combine(fixture.PackageRoot, "controller-files", "CELL.src"), "; drift");

        var outcome = fixture.Verify("test-validation-package-file-drift");

        Assert.False(outcome.Succeeded);
        AssertCheckFailed(outcome, "file-inventory");
        AssertCheckFailed(outcome, "package-id");
        Assert.True(ValidationPackageReceiptVerifier.VerifyIntegrity(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Manifest_byte_only_change_requires_a_new_package_id()
    {
        using var fixture = PackageFixture.Create();
        File.AppendAllText(fixture.ManifestPath, Environment.NewLine);

        var outcome = fixture.Verify("test-validation-package-manifest-bytes");

        Assert.False(outcome.Succeeded);
        AssertCheckFailed(outcome, "package-id");
        AssertCheckPassed(outcome, "file-inventory");
    }

    [Fact]
    public void Undeclared_file_is_part_of_identity_and_fails_exact_boundary()
    {
        using var fixture = PackageFixture.Create();
        File.WriteAllText(Path.Combine(fixture.PackageRoot, "unowned.json"), "{}");

        var outcome = fixture.Verify("test-validation-package-extra-file");

        Assert.False(outcome.Succeeded);
        AssertCheckFailed(outcome, "file-boundary");
        AssertCheckFailed(outcome, "package-id");
    }

    [Fact]
    public void Traversal_declaration_is_rejected_without_reading_outside_package()
    {
        using var fixture = PackageFixture.Create();
        var manifest = fixture.ReadManifestNode();
        manifest["files"]![0]!["relativePath"] = "../outside.src";
        fixture.WriteManifestNode(manifest);

        var outcome = fixture.Verify("test-validation-package-traversal");

        Assert.False(outcome.Succeeded);
        AssertCheckFailed(outcome, "manifest-schema");
        AssertCheckFailed(outcome, "file-boundary");
    }

    [Fact]
    public void Missing_required_artifact_role_is_rejected()
    {
        using var fixture = PackageFixture.Create();
        var manifest = fixture.ReadManifestNode();
        var profile = manifest["files"]!.AsArray()
            .Single(file => string.Equals(
                file!["relativePath"]!.GetValue<string>(),
                "profile.json",
                StringComparison.Ordinal));
        profile!["role"] = "Workcell";
        fixture.WriteManifestNode(manifest);

        var outcome = fixture.Verify("test-validation-package-role");

        Assert.False(outcome.Succeeded);
        AssertCheckFailed(outcome, "manifest-schema");
    }

    [Fact]
    public void Strict_manifest_rejects_unknown_parallel_protocol_fields()
    {
        using var fixture = PackageFixture.Create();
        var manifest = fixture.ReadManifestNode();
        manifest["unownedParallelProtocol"] = true;
        fixture.WriteManifestNode(manifest);

        Assert.Throws<System.Text.Json.JsonException>(() =>
            fixture.Verify("test-validation-package-unknown-field"));
    }

    [Fact]
    public void Receipt_writer_is_create_new_outside_package_and_current_source_verifies()
    {
        using var fixture = PackageFixture.Create();
        var outcome = fixture.Verify("test-validation-package-writer");

        var written = ValidationPackageReceiptWriter.WriteNew(
            fixture.ReceiptPath,
            fixture.PackageRoot,
            outcome.Receipt);
        var readback = ReceiptSerialization.ValidationPackageFromJson(File.ReadAllText(written));
        var current = ValidationPackageReceiptVerifier.VerifyCurrentPackage(
            readback,
            fixture.PackageRoot);

        Assert.True(current.Succeeded, string.Join(Environment.NewLine, current.Errors));
        Assert.True(current.CurrentPackageVerified);
        Assert.Throws<IOException>(() => ValidationPackageReceiptWriter.WriteNew(
            fixture.ReceiptPath,
            fixture.PackageRoot,
            outcome.Receipt));
    }

    [Fact]
    public void Receipt_output_inside_package_is_rejected_before_writing()
    {
        using var fixture = PackageFixture.Create();
        var unsafeOutput = Path.Combine(fixture.PackageRoot, "receipt.json");
        var request = new ValidationPackageVerificationRequest
        {
            PackageRoot = fixture.PackageRoot,
            ReceiptOutputPath = unsafeOutput
        };

        Assert.Throws<ArgumentException>(() =>
            new ValidationPackageVerifier().Verify(request, "test-validation-package-boundary"));
        Assert.False(File.Exists(unsafeOutput));
    }

    [Fact]
    public void Current_package_verification_detects_post_receipt_drift()
    {
        using var fixture = PackageFixture.Create();
        var receipt = fixture.Verify("test-validation-package-current-drift").Receipt;
        File.AppendAllText(Path.Combine(fixture.PackageRoot, "motion-plan.json"), " ");

        var result = ValidationPackageReceiptVerifier.VerifyCurrentPackage(
            receipt,
            fixture.PackageRoot);

        Assert.False(result.Succeeded);
        Assert.False(result.CurrentPackageVerified);
        Assert.Contains(result.Errors, error => error.Contains("no longer matches", StringComparison.Ordinal));
    }

    [Fact]
    public void Rehashed_receipt_claim_tampering_is_rejected_semantically()
    {
        using var fixture = PackageFixture.Create();
        var receipt = fixture.Verify("test-validation-package-receipt-tamper").Receipt;
        var payload = receipt.Payload with { UnsupportedClaims = [] };
        var tampered = receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var result = ValidationPackageReceiptVerifier.VerifyIntegrity(tampered);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("unsupportedClaims", StringComparison.Ordinal));
    }

    [Fact]
    public void Controller_comparison_matches_family_and_kss_but_keeps_robot_pending()
    {
        using var fixture = PackageFixture.Create();
        var packageReceipt = fixture.Verify("test-controller-compare-inconclusive").Receipt;
        var observationReceipt = CreateControllerObservation().Receipt;

        var result = ControllerCompatibilityComparer.Compare(packageReceipt, observationReceipt);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
        Assert.Equal(ControllerCompatibilityDisposition.Inconclusive, result.Disposition);
        Assert.Contains(result.Dimensions, dimension =>
            dimension.Dimension == "ControllerFamily"
            && dimension.Status == ControllerCompatibilityStatus.Matched);
        Assert.Contains(result.Dimensions, dimension =>
            dimension.Dimension == "KssVersion"
            && dimension.Status == ControllerCompatibilityStatus.Matched);
        Assert.Contains(result.Dimensions, dimension =>
            dimension.Dimension == "RobotModel"
            && dimension.Status == ControllerCompatibilityStatus.Pending
            && dimension.Observed is null);
    }

    [Theory]
    [InlineData("KR C4", "8.7.8", "ControllerFamily")]
    [InlineData("KR C5", "8.6.10", "KssVersion")]
    public void Controller_comparison_reports_declared_target_mismatch(
        string controllerModel,
        string kssVersion,
        string mismatchedDimension)
    {
        using var fixture = PackageFixture.Create();
        var packageReceipt = fixture.Verify("test-controller-compare-mismatch").Receipt;
        var target = packageReceipt.Payload.Manifest.CompatibilityTarget with
        {
            ControllerModel = controllerModel,
            KssVersion = kssVersion
        };

        var result = ControllerCompatibilityComparer.CompareVerified(
            target,
            packageReceipt.Payload.DeclaredPackageId,
            packageReceipt.PayloadSha256,
            CreateControllerObservation().Receipt);

        Assert.True(result.Succeeded);
        Assert.Equal(ControllerCompatibilityDisposition.Incompatible, result.Disposition);
        Assert.Contains(result.Dimensions, dimension =>
            dimension.Dimension == mismatchedDimension
            && dimension.Status == ControllerCompatibilityStatus.Mismatched);
    }

    [Fact]
    public void Controller_comparison_rejects_invalid_evidence_instead_of_comparing_claims()
    {
        using var fixture = PackageFixture.Create();
        var packageReceipt = fixture.Verify("test-controller-compare-invalid").Receipt;
        var observationReceipt = CreateControllerObservation().Receipt with
        {
            PayloadSha256 = new string('0', 64)
        };

        var result = ControllerCompatibilityComparer.Compare(packageReceipt, observationReceipt);

        Assert.False(result.Succeeded);
        Assert.Equal(ControllerCompatibilityDisposition.InvalidEvidence, result.Disposition);
        Assert.Empty(result.Dimensions);
        Assert.Contains(result.Errors, error => error.StartsWith("ControllerObservation:", StringComparison.Ordinal));
    }

    private static void AssertCheckPassed(
        ValidationPackageVerificationOutcome outcome,
        string checkId) =>
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == checkId && check.Status == VerificationStatus.Passed);

    private static void AssertCheckFailed(
        ValidationPackageVerificationOutcome outcome,
        string checkId) =>
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == checkId && check.Status == VerificationStatus.Failed);

    private static string FailureDetails(ValidationPackageVerificationOutcome outcome) =>
        string.Join(
            Environment.NewLine,
            outcome.Receipt.Payload.Checks
                .Where(check => check.Status == VerificationStatus.Failed)
                .Select(check => $"{check.Id}: {check.Detail}"));

    private static ControllerObservationOutcome CreateControllerObservation() =>
        new ControllerObservationRunner().Run(
            new ControllerObservationRequest
            {
                ControllerFamily = "KR C5",
                CabinetModel = "KR C5 dualcab AC",
                KssVersion = "8.7.8",
                KssBuild = "B671",
                KliAddress = "192.0.2.147",
                PrefixLength = 24,
                ObservedOnLocalDate = "2026-08-27",
                EvidenceReferences = ["onsite-smartpad-kss-info-20260827"]
            },
            "controller-observation-compare");

    private sealed class PackageFixture : IDisposable
    {
        private PackageFixture(string root)
        {
            Root = root;
            PackageRoot = Path.Combine(root, "package");
            EvidenceRoot = Path.Combine(root, "evidence");
            ReceiptPath = Path.Combine(EvidenceRoot, "validation-package.receipt.json");
            ManifestPath = Path.Combine(PackageRoot, ValidationPackageContract.ManifestFileName);
            Directory.CreateDirectory(Path.Combine(PackageRoot, "controller-files"));
            Directory.CreateDirectory(EvidenceRoot);
            File.WriteAllText(Path.Combine(PackageRoot, "profile.json"), "{\"profile\":\"KR210-KSS8.7.8\"}");
            File.WriteAllText(Path.Combine(PackageRoot, "workcell.json"), "{\"workcell\":\"synthetic\"}");
            File.WriteAllText(Path.Combine(PackageRoot, "motion-plan.json"), "{\"targets\":[\"T1\"]}");
            File.WriteAllText(Path.Combine(PackageRoot, "expectations.json"), "{\"nativeCompile\":\"accepted\"}");
            File.WriteAllText(Path.Combine(PackageRoot, "controller-files", "CELL.src"), "DEF CELL()\n PTP HOME\nEND\n");
            File.WriteAllText(Path.Combine(PackageRoot, "controller-files", "CELL.dat"), "DEFDAT CELL PUBLIC\nENDDAT\n");

            var manifest = CreateManifest();
            File.WriteAllText(ManifestPath, ReceiptSerialization.ToJson(manifest));
            var packageId = ValidationPackageIdentity.ComputePackageId(PackageRoot);
            File.WriteAllText(
                ManifestPath,
                ReceiptSerialization.ToJson(manifest with { PackageId = packageId }));
            Assert.Equal(packageId, ValidationPackageIdentity.ComputePackageId(PackageRoot));
        }

        public string Root { get; }

        public string PackageRoot { get; }

        public string EvidenceRoot { get; }

        public string ReceiptPath { get; }

        public string ManifestPath { get; }

        public static PackageFixture Create()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                $"kuka-lab-validation-package-{Guid.NewGuid():N}");
            return new PackageFixture(root);
        }

        public ValidationPackageVerificationOutcome Verify(string attemptId) =>
            new ValidationPackageVerifier().Verify(
                new ValidationPackageVerificationRequest
                {
                    PackageRoot = PackageRoot,
                    ReceiptOutputPath = ReceiptPath
                },
                attemptId);

        public JsonObject ReadManifestNode() =>
            JsonNode.Parse(File.ReadAllText(ManifestPath))!.AsObject();

        public void WriteManifestNode(JsonObject manifest) =>
            File.WriteAllText(ManifestPath, manifest.ToJsonString(new() { WriteIndented = true }));

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private ValidationPackageManifest CreateManifest()
        {
            var declarations = new[]
            {
                Declare("controller-files/CELL.dat", ValidationPackageArtifactRole.KrlData, "Windows-1252"),
                Declare("controller-files/CELL.src", ValidationPackageArtifactRole.KrlSource, "Windows-1252"),
                Declare("expectations.json", ValidationPackageArtifactRole.Expectations, "UTF-8"),
                Declare("motion-plan.json", ValidationPackageArtifactRole.MotionPlan, "UTF-8"),
                Declare("profile.json", ValidationPackageArtifactRole.Profile, "UTF-8"),
                Declare("workcell.json", ValidationPackageArtifactRole.Workcell, "UTF-8")
            }.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToList();

            return new ValidationPackageManifest
            {
                SchemaIdentity = ValidationPackageContract.ManifestSchemaIdentity,
                SchemaVersion = ValidationPackageContract.ManifestSchemaVersion,
                PackageId = ValidationPackageContract.PackageIdPlaceholder,
                CreatedAtUtc = new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.Zero),
                Producer = new ValidationPackageProducer
                {
                    Product = "Adjust.Robotics.Rhino",
                    Version = "test",
                    Revision = "test-revision"
                },
                Upstream = new ValidationPackageUpstreamIdentity
                {
                    Revision = "test-upstream",
                    Fingerprint = "test-fingerprint"
                },
                ArtifactIdentities = new ValidationPackageArtifactIdentities
                {
                    Workcell = "workcell-test",
                    Program = "program-test",
                    MotionPlan = "motion-plan-test",
                    Krl = "krl-test"
                },
                CompatibilityTarget = new ValidationPackageCompatibilityTarget
                {
                    KukaSimVersion = "4.3.2",
                    KssVersion = "8.7.8",
                    RobotModel = "KR 210 R2700-2",
                    ControllerModel = "KR C5"
                },
                Frames = new ValidationPackageFrameProfile
                {
                    ToolNumber = 1,
                    ToolProvenance = ValidationPackageProvenance.ProductDeclared,
                    BaseNumber = 0,
                    BaseProvenance = ValidationPackageProvenance.ProductDeclared,
                    LoadDeclaration = "Synthetic test load",
                    LoadProvenance = ValidationPackageProvenance.ProductDeclared
                },
                Files = declarations,
                Correlations =
                [
                    new ValidationPackageCorrelation
                    {
                        CorrelationId = "corr-1",
                        RhinoObjectId = "rhino-object-1",
                        ProgramOperationId = "operation-1",
                        TrajectoryTargetId = "target-1",
                        TrajectorySegmentId = "segment-1",
                        KrlFile = "controller-files/CELL.src",
                        KrlLine = 2
                    }
                ],
                RequestedFixtureIds = ["minimal-ptp-lin-valid"],
                ExpectedAssertions = ["ArtifactIntegrity"],
                RequestedEvidenceLevels = [ValidationPackageEvidenceLevel.L1SimValidated],
                Safety = new ValidationPackageSafety
                {
                    Classification = "VirtualOnlyNoSecrets",
                    RedactionConfirmed = true,
                    SecretsIncluded = false,
                    PhysicalMotionAllowed = false
                }
            };
        }

        private ValidationPackageFileDeclaration Declare(
            string relativePath,
            ValidationPackageArtifactRole role,
            string encoding)
        {
            var fullPath = Path.Combine(
                PackageRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            var content = File.ReadAllBytes(fullPath);
            return new ValidationPackageFileDeclaration
            {
                RelativePath = relativePath,
                Role = role,
                Encoding = encoding,
                Bytes = content.LongLength,
                Sha256 = Convert.ToHexString(SHA256.HashData(content))
            };
        }
    }
}
