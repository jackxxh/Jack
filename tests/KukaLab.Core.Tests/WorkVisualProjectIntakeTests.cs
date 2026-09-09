using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class WorkVisualProjectIntakeTests
{
    [Fact]
    public void Intake_captures_only_local_file_identity_without_path_or_name_disclosure()
    {
        using var fixture = ProjectFixture.Create();

        var outcome = fixture.Run("test-wvs-intake-redacted");
        var json = ReceiptSerialization.ToJson(outcome.Receipt);

        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(
            WorkVisualProjectIntakeDisposition.FileIdentityCaptured,
            outcome.Receipt.Payload.Disposition);
        Assert.True(outcome.Receipt.Payload.FileIdentityCaptured);
        Assert.False(outcome.Receipt.Payload.ProjectExtracted);
        Assert.False(outcome.Receipt.Payload.ProjectContentsInspected);
        Assert.False(outcome.Receipt.Payload.ControllerAccessed);
        Assert.False(outcome.Receipt.Payload.NetworkTrafficSent);
        Assert.False(outcome.Receipt.Payload.CredentialsUsed);
        Assert.False(outcome.Receipt.Payload.SourceProjectChanged);
        Assert.False(outcome.Receipt.Payload.ControllerConfigurationChanged);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.NativeKssStatus);
        Assert.Equal(".wvs", outcome.Receipt.Payload.ProjectFile.Extension);
        Assert.DoesNotContain(fixture.ProjectPath, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.GetFileName(fixture.ProjectPath), json, StringComparison.OrdinalIgnoreCase);
        Assert.True(WorkVisualProjectIntakeReceiptVerifier.VerifyIntegrity(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Writer_creates_receipt_outside_source_directory_and_refuses_overwrite()
    {
        using var fixture = ProjectFixture.Create();
        var receipt = fixture.Run("test-wvs-intake-writer").Receipt;

        var written = WorkVisualProjectIntakeReceiptWriter.WriteNew(
            fixture.OutputPath,
            fixture.ProjectPath,
            receipt);
        var readback = ReceiptSerialization.WorkVisualProjectIntakeFromJson(File.ReadAllText(written));

        Assert.True(File.Exists(written));
        Assert.True(WorkVisualProjectIntakeReceiptVerifier.VerifyIntegrity(readback).Succeeded);
        Assert.Throws<IOException>(() => WorkVisualProjectIntakeReceiptWriter.WriteNew(
            fixture.OutputPath,
            fixture.ProjectPath,
            receipt));
    }

    [Fact]
    public void Output_in_downloaded_project_directory_is_rejected_before_receipt_creation()
    {
        using var fixture = ProjectFixture.Create();
        var unsafeOutput = Path.Combine(Path.GetDirectoryName(fixture.ProjectPath)!, "receipt.json");
        var request = fixture.CreateRequest() with { ReceiptOutputPath = unsafeOutput };

        var exception = Assert.Throws<ArgumentException>(() =>
            new WorkVisualProjectIntakeRunner().Run(request, "test-wvs-intake-boundary"));

        Assert.Contains("outside", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(unsafeOutput));
    }

    [Fact]
    public void Output_below_downloaded_project_directory_is_also_rejected()
    {
        using var fixture = ProjectFixture.Create();
        var unsafeOutput = Path.Combine(
            Path.GetDirectoryName(fixture.ProjectPath)!,
            "nested-evidence",
            "receipt.json");
        var request = fixture.CreateRequest() with { ReceiptOutputPath = unsafeOutput };

        Assert.Throws<ArgumentException>(() =>
            new WorkVisualProjectIntakeRunner().Run(request, "test-wvs-intake-nested-boundary"));
        Assert.False(File.Exists(unsafeOutput));
    }

    [Fact]
    public void Non_wvs_and_missing_sources_fail_predictably_without_receipt()
    {
        using var fixture = ProjectFixture.Create();
        var wrongExtension = Path.ChangeExtension(fixture.ProjectPath, ".zip");
        File.Copy(fixture.ProjectPath, wrongExtension);
        var wrongRequest = fixture.CreateRequest() with { ProjectPath = wrongExtension };
        var missingRequest = fixture.CreateRequest() with
        {
            ProjectPath = Path.Combine(Path.GetDirectoryName(fixture.ProjectPath)!, "missing.wvs")
        };

        Assert.Throws<ArgumentException>(() =>
            new WorkVisualProjectIntakeRunner().Run(wrongRequest, "test-wvs-intake-extension"));
        Assert.Throws<FileNotFoundException>(() =>
            new WorkVisualProjectIntakeRunner().Run(missingRequest, "test-wvs-intake-missing"));
        Assert.False(File.Exists(fixture.OutputPath));
    }

    [Fact]
    public void Empty_wvs_is_rejected_as_no_byte_identity()
    {
        using var fixture = ProjectFixture.Create();
        File.WriteAllBytes(fixture.ProjectPath, []);

        Assert.Throws<InvalidDataException>(() =>
            fixture.Run("test-wvs-intake-empty"));
        Assert.False(File.Exists(fixture.OutputPath));
    }

    [Fact]
    public void Current_project_verification_detects_source_drift()
    {
        using var fixture = ProjectFixture.Create();
        var receipt = fixture.Run("test-wvs-intake-drift").Receipt;

        var before = WorkVisualProjectIntakeReceiptVerifier.VerifyCurrentProject(
            receipt,
            fixture.ProjectPath);
        File.AppendAllText(fixture.ProjectPath, "changed-after-intake");
        var after = WorkVisualProjectIntakeReceiptVerifier.VerifyCurrentProject(
            receipt,
            fixture.ProjectPath);

        Assert.True(before.Succeeded);
        Assert.True(before.CurrentProjectVerified);
        Assert.False(after.Succeeded);
        Assert.False(after.CurrentProjectVerified);
        Assert.Contains(after.Errors, error => error.Contains("no longer matches", StringComparison.Ordinal));
    }

    [Fact]
    public void Writer_rechecks_source_and_rejects_drift_before_writing_receipt()
    {
        using var fixture = ProjectFixture.Create();
        var receipt = fixture.Run("test-wvs-intake-writer-drift").Receipt;
        File.AppendAllText(fixture.ProjectPath, "changed-before-write");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            WorkVisualProjectIntakeReceiptWriter.WriteNew(
                fixture.OutputPath,
                fixture.ProjectPath,
                receipt));

        Assert.Contains("changed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(fixture.OutputPath));
    }

    [Fact]
    public void Verifier_rejects_rehashed_semantic_claim_tampering()
    {
        using var fixture = ProjectFixture.Create();
        var receipt = fixture.Run("test-wvs-intake-tamper").Receipt;
        var payload = receipt.Payload with { ProjectContentsInspected = true };
        var tampered = receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var verification = WorkVisualProjectIntakeReceiptVerifier.VerifyIntegrity(tampered);

        Assert.False(verification.Succeeded);
        Assert.Contains(
            verification.Errors,
            error => error.Contains("content inspection", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Generic_receipt_serialization_roundtrip_preserves_strict_identity()
    {
        using var fixture = ProjectFixture.Create();
        var receipt = fixture.Run("test-wvs-intake-roundtrip").Receipt;

        var readback = ReceiptSerialization.WorkVisualProjectIntakeFromJson(
            ReceiptSerialization.ToJson(receipt));

        Assert.Equal(receipt.SchemaIdentity, readback.SchemaIdentity);
        Assert.Equal(receipt.PayloadSha256, readback.PayloadSha256);
        Assert.Equal(receipt.Payload.ProjectFile, readback.Payload.ProjectFile);
        Assert.True(WorkVisualProjectIntakeReceiptVerifier.VerifyIntegrity(readback).Succeeded);
    }

    private sealed class ProjectFixture : IDisposable
    {
        private ProjectFixture(string root)
        {
            Root = root;
            SourceRoot = Path.Combine(root, "Downloaded Projects");
            EvidenceRoot = Path.Combine(root, "evidence");
            ProjectPath = Path.Combine(SourceRoot, "sensitive-project-name.wvs");
            OutputPath = Path.Combine(EvidenceRoot, "project-intake.receipt.json");
            Directory.CreateDirectory(SourceRoot);
            Directory.CreateDirectory(EvidenceRoot);
            File.WriteAllBytes(ProjectPath, [0x57, 0x56, 0x53, 0x00, 0x01, 0x02, 0x03, 0x04]);
        }

        public string Root { get; }

        public string SourceRoot { get; }

        public string EvidenceRoot { get; }

        public string ProjectPath { get; }

        public string OutputPath { get; }

        public static ProjectFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"kuka-lab-wvs-intake-{Guid.NewGuid():N}");
            return new ProjectFixture(root);
        }

        public WorkVisualProjectIntakeRequest CreateRequest() => new()
        {
            ProjectPath = ProjectPath,
            ReceiptOutputPath = OutputPath,
            CaptureReference = "attended-controller-project-download"
        };

        public WorkVisualProjectIntakeOutcome Run(string attemptId) =>
            new WorkVisualProjectIntakeRunner().Run(CreateRequest(), attemptId);

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
