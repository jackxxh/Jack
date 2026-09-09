using System.Security.Cryptography;
using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class KrlStaticPreflightTests
{
    [Fact]
    public void Valid_fixture_package_is_ready_for_native_kss_submission_without_claiming_kss()
    {
        using var fixture = PreflightFixture.Create("minimal-ptp-lin-valid");

        var outcome = fixture.Run("test-krl-static-valid");

        Assert.True(outcome.Succeeded, FailureDetails(outcome));
        Assert.Equal(
            KrlStaticPreflightDisposition.ReadyForNativeKssSubmission,
            outcome.Receipt.Payload.Disposition);
        Assert.Equal(4, outcome.Receipt.Payload.MotionInstructionCount);
        Assert.Single(outcome.Receipt.Payload.Programs);
        Assert.True(outcome.Receipt.Payload.Programs[0].HasBasInitMov);
        Assert.Empty(outcome.Receipt.Payload.Findings);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.NativeKss.Status);
        Assert.True(KrlStaticPreflightReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Spline_motion_family_is_inventoried_and_target_checked()
    {
        using var fixture = PreflightFixture.Create(
            "minimal-ptp-lin-valid",
            source => source
                .Replace(
                    "PTP P_START\n  $VEL.CP=0.1\n  LIN P_END",
                    "SPLINE\n    SPL P_START\n    SLIN P_END\n  ENDSPLINE",
                    StringComparison.Ordinal));

        var outcome = fixture.Run("test-krl-static-spline-family");

        Assert.True(outcome.Succeeded, FailureDetails(outcome));
        Assert.Equal(4, outcome.Receipt.Payload.MotionInstructionCount);
        Assert.Empty(outcome.Receipt.Payload.Findings);
    }

    [Fact]
    public void Circ_and_cp_spline_fixture_is_ready_for_native_submission_without_claiming_native_kss()
    {
        using var fixture = PreflightFixture.Create("minimal-circ-spline-valid");

        var outcome = fixture.Run("test-krl-static-circ-spline-valid");

        Assert.True(outcome.Succeeded, FailureDetails(outcome));
        Assert.Equal(6, outcome.Receipt.Payload.MotionInstructionCount);
        Assert.Empty(outcome.Receipt.Payload.Findings);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.NativeKss.Status);
    }

    [Theory]
    [InlineData("PTP P_START", "SPL P_START", "KRL040")]
    [InlineData("DEF LAB_MINIMAL()", "DEF WRONG_NAME()", "KRL012")]
    [InlineData("BAS(#INITMOV,0)", "; initialization removed", "KRL020")]
    [InlineData("\nEND\n", "\n; END removed\n", "KRL011")]
    public void Deliberate_fault_injections_are_rejected_with_specific_diagnostics(
        string original,
        string replacement,
        string expectedCode)
    {
        using var fixture = PreflightFixture.Create(
            "minimal-ptp-lin-valid",
            source => source.Replace(original, replacement, StringComparison.Ordinal));

        var outcome = fixture.Run($"test-krl-static-fault-{expectedCode.ToLowerInvariant()}");

        Assert.False(outcome.Succeeded);
        Assert.Equal(
            KrlStaticPreflightDisposition.RejectedByLocalPreflight,
            outcome.Receipt.Payload.Disposition);
        Assert.Contains(outcome.Receipt.Payload.Findings, finding => finding.Code == expectedCode);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.NativeKss.Status);
    }

    [Fact]
    public void Unterminated_cp_spline_block_is_rejected()
    {
        using var fixture = PreflightFixture.Create(
            "minimal-ptp-lin-valid",
            source => source.Replace(
                "PTP P_START\n  $VEL.CP=0.5\n  LIN P_END",
                "SPLINE\n    SPL P_START\n    SLIN P_END",
                StringComparison.Ordinal));

        var outcome = fixture.Run("test-krl-static-unterminated-spline");

        Assert.False(outcome.Succeeded);
        Assert.Contains(outcome.Receipt.Payload.Findings, finding => finding.Code == "KRL041");
    }

    [Fact]
    public void Missing_motion_target_is_rejected_with_exact_file_line_and_symbol()
    {
        using var fixture = PreflightFixture.Create("minimal-missing-target-invalid");

        var outcome = fixture.Run("test-krl-static-missing-target");

        Assert.False(outcome.Succeeded);
        Assert.Equal(
            KrlStaticPreflightDisposition.RejectedByLocalPreflight,
            outcome.Receipt.Payload.Disposition);
        var finding = Assert.Single(
            outcome.Receipt.Payload.Findings,
            item => item.Code == "KRL030" && item.Symbol == "P_MISSING");
        Assert.EndsWith("LAB_MISSING_TARGET.src", finding.File, StringComparison.Ordinal);
        Assert.True(finding.Line > 0);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "target-references" && check.Status == VerificationStatus.Failed);
        Assert.True(KrlStaticPreflightReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Rehashed_receipt_cannot_hide_static_findings()
    {
        using var fixture = PreflightFixture.Create("minimal-missing-target-invalid");
        var receipt = fixture.Run("test-krl-static-tamper").Receipt;
        var payload = receipt.Payload with { Findings = [] };
        var tampered = receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var result = KrlStaticPreflightReceiptVerifier.Verify(tampered);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("checks", StringComparison.Ordinal));
    }

    [Fact]
    public void Rehashed_receipt_with_malformed_program_evidence_is_rejected()
    {
        using var fixture = PreflightFixture.Create("minimal-ptp-lin-valid");
        var receipt = fixture.Run("test-krl-static-malformed-evidence").Receipt;
        var malformedProgram = receipt.Payload.Programs[0] with { SourceLineCount = 0 };
        var payload = receipt.Payload with { Programs = [malformedProgram] };
        var tampered = receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var result = KrlStaticPreflightReceiptVerifier.Verify(tampered);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("invalid values", StringComparison.Ordinal));
    }

    [Fact]
    public void Package_drift_after_integrity_receipt_is_rejected_before_analysis()
    {
        using var fixture = PreflightFixture.Create("minimal-ptp-lin-valid");
        File.AppendAllText(fixture.SourcePath, "; drift");

        var exception = Assert.Throws<InvalidDataException>(() =>
            fixture.Run("test-krl-static-package-drift"));

        Assert.Contains("current-package verification failed", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(fixture.PreflightReceiptPath));
    }

    private static string FailureDetails(KrlStaticPreflightOutcome outcome) =>
        string.Join(
            Environment.NewLine,
            outcome.Receipt.Payload.Checks
                .Where(check => check.Status == VerificationStatus.Failed)
                .Select(check => $"{check.Id}: {check.Detail}"));

    internal sealed class PreflightFixture : IDisposable
    {
        private PreflightFixture(
            string root,
            string fixtureId,
            Func<string, string>? sourceTransform = null)
        {
            Root = root;
            PackageRoot = Path.Combine(root, "package");
            PackageReceiptPath = Path.Combine(root, "validation-package.receipt.json");
            PreflightReceiptPath = Path.Combine(root, "krl-static-preflight.receipt.json");
            Directory.CreateDirectory(Path.Combine(PackageRoot, "controller-files"));
            foreach (var file in new[] { "profile.json", "workcell.json", "motion-plan.json", "expectations.json" })
            {
                File.WriteAllText(Path.Combine(PackageRoot, file), $"{{\"fixture\":\"{fixtureId}\",\"file\":\"{file}\"}}");
            }

            var repositoryRoot = FindRepositoryRoot();
            var fixtureRoot = Path.Combine(repositoryRoot, "fixtures", fixtureId, "controller-files");
            foreach (var sourceFile in Directory.EnumerateFiles(fixtureRoot))
            {
                File.Copy(sourceFile, Path.Combine(PackageRoot, "controller-files", Path.GetFileName(sourceFile)));
            }

            SourcePath = Directory.EnumerateFiles(
                    Path.Combine(PackageRoot, "controller-files"),
                    "*.src")
                .Single();
            if (sourceTransform is not null)
            {
                File.WriteAllText(SourcePath, sourceTransform(File.ReadAllText(SourcePath)));
            }

            var sourceRelativePath = $"controller-files/{Path.GetFileName(SourcePath)}";
            var manifest = new ValidationPackageManifest
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
                Files = Directory.EnumerateFiles(PackageRoot, "*", SearchOption.AllDirectories)
                    .Select(path => Declare(PackageRoot, path))
                    .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                    .ToList(),
                Correlations =
                [
                    new ValidationPackageCorrelation
                    {
                        CorrelationId = "corr-1",
                        RhinoObjectId = "rhino-object-1",
                        ProgramOperationId = "operation-1",
                        TrajectoryTargetId = "target-1",
                        TrajectorySegmentId = "segment-1",
                        KrlFile = sourceRelativePath,
                        KrlLine = 1
                    }
                ],
                RequestedFixtureIds = [fixtureId],
                ExpectedAssertions = ["KrlStaticPreflight"],
                RequestedEvidenceLevels = [ValidationPackageEvidenceLevel.L2VirtualKssValidated],
                Safety = new ValidationPackageSafety
                {
                    Classification = "VirtualOnlyNoSecrets",
                    RedactionConfirmed = true,
                    SecretsIncluded = false,
                    PhysicalMotionAllowed = false
                }
            };
            var manifestPath = Path.Combine(PackageRoot, ValidationPackageContract.ManifestFileName);
            File.WriteAllText(manifestPath, ReceiptSerialization.ToJson(manifest));
            var packageId = ValidationPackageIdentity.ComputePackageId(PackageRoot);
            File.WriteAllText(manifestPath, ReceiptSerialization.ToJson(manifest with { PackageId = packageId }));
            var packageOutcome = new ValidationPackageVerifier().Verify(
                new ValidationPackageVerificationRequest
                {
                    PackageRoot = PackageRoot,
                    ReceiptOutputPath = PackageReceiptPath
                },
                $"package-{fixtureId}");
            Assert.True(packageOutcome.Succeeded);
            ValidationPackageReceiptWriter.WriteNew(PackageReceiptPath, PackageRoot, packageOutcome.Receipt);
        }

        public string Root { get; }

        public string PackageRoot { get; }

        public string PackageReceiptPath { get; }

        public string PreflightReceiptPath { get; }

        public string SourcePath { get; }

        public static PreflightFixture Create(
            string fixtureId,
            Func<string, string>? sourceTransform = null)
        {
            var root = Path.Combine(Path.GetTempPath(), $"kuka-lab-krl-static-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            return new PreflightFixture(root, fixtureId, sourceTransform);
        }

        public KrlStaticPreflightOutcome Run(string attemptId) =>
            new KrlStaticPreflightRunner().Run(
                new KrlStaticPreflightRequest
                {
                    PackageRoot = PackageRoot,
                    ValidationPackageReceiptPath = PackageReceiptPath,
                    ReceiptOutputPath = PreflightReceiptPath
                },
                attemptId);

        public string Write(KrlStaticPreflightReceipt receipt)
        {
            var packageReceipt = ReceiptSerialization.ValidationPackageFromJson(
                File.ReadAllText(PackageReceiptPath));
            return KrlStaticPreflightReceiptWriter.WriteNew(
                PreflightReceiptPath,
                PackageRoot,
                packageReceipt,
                receipt);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private static ValidationPackageFileDeclaration Declare(string packageRoot, string fullPath)
        {
            var relativePath = Path.GetRelativePath(packageRoot, fullPath).Replace('\\', '/');
            var extension = Path.GetExtension(relativePath);
            var role = relativePath switch
            {
                "profile.json" => ValidationPackageArtifactRole.Profile,
                "workcell.json" => ValidationPackageArtifactRole.Workcell,
                "motion-plan.json" => ValidationPackageArtifactRole.MotionPlan,
                "expectations.json" => ValidationPackageArtifactRole.Expectations,
                _ when string.Equals(extension, ".src", StringComparison.OrdinalIgnoreCase) => ValidationPackageArtifactRole.KrlSource,
                _ when string.Equals(extension, ".dat", StringComparison.OrdinalIgnoreCase) => ValidationPackageArtifactRole.KrlData,
                _ => throw new InvalidDataException($"Unexpected package file: {relativePath}")
            };
            var bytes = File.ReadAllBytes(fullPath);
            return new ValidationPackageFileDeclaration
            {
                RelativePath = relativePath,
                Role = role,
                Encoding = role is ValidationPackageArtifactRole.KrlSource or ValidationPackageArtifactRole.KrlData
                    ? "Windows-1252"
                    : "UTF-8",
                Bytes = bytes.LongLength,
                Sha256 = Convert.ToHexString(SHA256.HashData(bytes))
            };
        }

        private static string FindRepositoryRoot()
        {
            for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            {
                if (File.Exists(Path.Combine(current.FullName, "KukaLab.slnx"))
                    && File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
                {
                    return current.FullName;
                }
            }

            throw new DirectoryNotFoundException("Repository root was not found from the test runtime.");
        }
    }
}
