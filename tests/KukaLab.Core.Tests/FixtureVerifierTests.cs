using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class FixtureVerifierTests
{
    [Theory]
    [InlineData("minimal-ptp-lin-valid", NativeCompileExpectation.CompileAccepted)]
    [InlineData("minimal-officelite-kr3-execution-valid", NativeCompileExpectation.CompileAccepted)]
    [InlineData("minimal-circ-spline-valid", NativeCompileExpectation.CompileAccepted)]
    [InlineData("minimal-missing-target-invalid", NativeCompileExpectation.CompileRejected)]
    public void Tracked_fixture_passes_integrity_without_claiming_native_kss(
        string fixtureName,
        NativeCompileExpectation expectedCompileResult)
    {
        var outcome = new FixtureVerifier().Verify(FindFixture(fixtureName), $"test-{fixtureName}");

        Assert.True(outcome.Succeeded, FailureDetails(outcome));
        Assert.Equal(VerificationStatus.Passed, outcome.Receipt.Payload.ArtifactIntegrity);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.NativeKss.Status);
        Assert.Equal(expectedCompileResult, outcome.Receipt.Payload.NativeKss.ExpectedCompileResult);
        Assert.True(ReceiptSerialization.HasValidPayloadHash(outcome.Receipt));
    }

    [Fact]
    public void Item9_schema_v2_freezes_exact_isolated_officelite_execution_profile_without_target_workcell_claim()
    {
        var fixture = FindFixture("minimal-officelite-kr3-execution-valid");
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture, "fixture.json")))!.AsObject();
        var outcome = new FixtureVerifier().Verify(fixture, "test-item9-v2-officelite-kr3");

        Assert.True(outcome.Succeeded, FailureDetails(outcome));
        Assert.Equal(2, manifest["schemaVersion"]!.GetValue<int>());
        Assert.Equal("#KR3R540 C4SR", manifest["controllerTarget"]!["robotModel"]!.GetValue<string>());
        Assert.Equal("KRC5_MICRO", manifest["controllerTarget"]!["controllerModel"]!.GetValue<string>());
        Assert.Equal(
            "ExactIsolatedOfficeLiteKr3KssExecutionOnlyNotTargetWorkcellEvidence",
            manifest["controllerTarget"]!["officeLiteScope"]!.GetValue<string>());
        Assert.Null(manifest["controllerTarget"]!["kukaSimComponentSha256"]);
        Assert.Equal("NotRun", manifest["nativeEvidenceStatus"]!.GetValue<string>());
        Assert.Null(manifest["nativeEvidenceReference"]);
        Assert.Equal(2, manifest["correlations"]!.AsArray().Count);
    }

    [Theory]
    [InlineData("minimal-ptp-lin-valid", "BoundedVirtualExecutionExpected", "SyntaxSelectionValidated", "WP4K-20260828-NATIVE-KSS-02", 3)]
    [InlineData("minimal-missing-target-invalid", "RejectedBeforeExecution", "NotRun", null, 1)]
    public void Item9_schema_v2_fixtures_freeze_exact_c01_scope_encoding_and_motion_order(
        string fixtureName,
        string expectedExecution,
        string expectedNativeEvidenceStatus,
        string? expectedNativeEvidenceReference,
        int expectedMotionCount)
    {
        var fixture = FindFixture(fixtureName);
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture, "fixture.json")))!.AsObject();
        var outcome = new FixtureVerifier().Verify(fixture, $"test-item9-v2-{fixtureName}");

        Assert.True(outcome.Succeeded, FailureDetails(outcome));
        Assert.Equal(2, manifest["schemaVersion"]!.GetValue<int>());
        Assert.Equal(expectedExecution, manifest["executionExpectation"]!.GetValue<string>());
        Assert.Equal("#KR210R2700_2 C01 FLR", manifest["controllerTarget"]!["robotModel"]!.GetValue<string>());
        Assert.Equal("KRC5", manifest["controllerTarget"]!["controllerModel"]!.GetValue<string>());
        Assert.Equal("GenericKssSemanticsOnlyNotExactC01Kinematics", manifest["controllerTarget"]!["officeLiteScope"]!.GetValue<string>());
        Assert.Equal(expectedNativeEvidenceStatus, manifest["nativeEvidenceStatus"]!.GetValue<string>());
        Assert.Equal(expectedNativeEvidenceReference, manifest["nativeEvidenceReference"]?.GetValue<string>());
        Assert.Equal(expectedMotionCount, manifest["correlations"]!.AsArray().Count);
        Assert.All(
            outcome.Receipt.Payload.Files,
            file =>
            {
                Assert.Equal(FixtureTextEncoding.Ascii7Bit, file.ExpectedEncoding);
                Assert.Equal(file.ExpectedEncoding, file.ActualEncoding);
                Assert.Equal(FixtureLineEnding.Lf, file.ExpectedLineEnding);
                Assert.Equal(file.ExpectedLineEnding, file.ActualLineEnding);
            });
    }

    [Fact]
    public void Item9_schema_v2_rejects_hash_consistent_crlf_drift()
    {
        using var temporary = TemporaryFixture.CopyOf(FindFixture("minimal-ptp-lin-valid"));
        var sourcePath = Path.Combine(temporary.Path, "controller-files", "LAB_MINIMAL.src");
        var sourceText = File.ReadAllText(sourcePath).Replace("\r\n", "\n", StringComparison.Ordinal);
        var changedBytes = Encoding.ASCII.GetBytes(sourceText.Replace("\n", "\r\n", StringComparison.Ordinal));
        File.WriteAllBytes(sourcePath, changedBytes);

        var manifestPath = Path.Combine(temporary.Path, "fixture.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        var sourceDeclaration = manifest["files"]!.AsArray()
            .Select(node => node!.AsObject())
            .Single(node => node["relativePath"]!.GetValue<string>().EndsWith(".src", StringComparison.OrdinalIgnoreCase));
        sourceDeclaration["bytes"] = changedBytes.LongLength;
        sourceDeclaration["sha256"] = Convert.ToHexString(SHA256.HashData(changedBytes));
        File.WriteAllText(manifestPath, manifest.ToJsonString(new() { WriteIndented = true }));

        var outcome = new FixtureVerifier().Verify(temporary.Path, "test-item9-crlf-drift");

        Assert.False(outcome.Succeeded);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "file-inventory" && check.Status == VerificationStatus.Failed);
    }

    [Fact]
    public void Item9_schema_v2_rejects_correlation_that_no_longer_matches_frozen_krl_line()
    {
        using var temporary = TemporaryFixture.CopyOf(FindFixture("minimal-ptp-lin-valid"));
        var manifestPath = Path.Combine(temporary.Path, "fixture.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        manifest["correlations"]![1]!["krlSymbol"] = "P_WRONG";
        File.WriteAllText(manifestPath, manifest.ToJsonString(new() { WriteIndented = true }));

        var outcome = new FixtureVerifier().Verify(temporary.Path, "test-item9-correlation-drift");

        Assert.False(outcome.Succeeded);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "correlation-map"
                && check.Status == VerificationStatus.Failed
                && check.Detail.Contains("does not match KRL source line", StringComparison.Ordinal));
    }

    [Fact]
    public void Revised_invalid_fixture_cannot_inherit_native_evidence_from_older_bytes()
    {
        using var temporary = TemporaryFixture.CopyOf(FindFixture("minimal-missing-target-invalid"));
        var manifestPath = Path.Combine(temporary.Path, "fixture.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        manifest["nativeEvidenceReference"] = "WP4K-20260828-NATIVE-KSS-02";
        manifest["expectedDiagnostic"]!["nativeMessageCode"] = "2137";
        File.WriteAllText(manifestPath, manifest.ToJsonString(new() { WriteIndented = true }));

        var outcome = new FixtureVerifier().Verify(temporary.Path, "test-stale-native-evidence");

        Assert.False(outcome.Succeeded);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Status == VerificationStatus.Failed
                && (check.Detail.Contains("must not inherit native KSS evidence", StringComparison.Ordinal)
                    || check.Detail.Contains("must not inherit a native KSS evidence reference", StringComparison.Ordinal)));
    }

    [Fact]
    public void Tampered_controller_file_fails_hash_check()
    {
        using var temporary = TemporaryFixture.CopyOf(FindFixture("minimal-ptp-lin-valid"));
        File.AppendAllText(
            Path.Combine(temporary.Path, "controller-files", "LAB_MINIMAL.src"),
            "; tampered");

        var outcome = new FixtureVerifier().Verify(temporary.Path, "test-tampered-file");

        Assert.False(outcome.Succeeded);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "file-inventory" && check.Status == VerificationStatus.Failed);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.NativeKss.Status);
        Assert.True(ReceiptSerialization.HasValidPayloadHash(outcome.Receipt));
    }

    [Fact]
    public void Traversal_path_is_rejected_before_external_file_read()
    {
        using var temporary = TemporaryFixture.CopyOf(FindFixture("minimal-ptp-lin-valid"));
        var manifestPath = Path.Combine(temporary.Path, "fixture.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        manifest["files"]![0]!["relativePath"] = "../outside.src";
        File.WriteAllText(manifestPath, manifest.ToJsonString(new() { WriteIndented = true }));

        var outcome = new FixtureVerifier().Verify(temporary.Path, "test-traversal-path");

        Assert.False(outcome.Succeeded);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "file-boundary" && check.Status == VerificationStatus.Failed);
    }

    [Fact]
    public void Strict_manifest_rejects_unknown_fields()
    {
        using var temporary = TemporaryFixture.CopyOf(FindFixture("minimal-ptp-lin-valid"));
        var manifestPath = Path.Combine(temporary.Path, "fixture.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        manifest["unownedParallelProtocol"] = true;
        File.WriteAllText(manifestPath, manifest.ToJsonString(new() { WriteIndented = true }));

        var outcome = new FixtureVerifier().Verify(temporary.Path, "test-unknown-field");

        Assert.False(outcome.Succeeded);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "manifest-schema" && check.Status == VerificationStatus.Failed);
    }

    [Fact]
    public void Receipt_writer_is_immutable_and_readback_hash_is_valid()
    {
        using var output = TemporaryFixture.Empty();
        var outcome = new FixtureVerifier().Verify(
            FindFixture("minimal-ptp-lin-valid"),
            "test-receipt-write");
        var receiptPath = Path.Combine(output.Path, "receipt.json");

        ReceiptWriter.WriteNew(receiptPath, outcome.Receipt);
        var readback = ReceiptSerialization.FromJson(File.ReadAllText(receiptPath));

        Assert.True(ReceiptSerialization.HasValidPayloadHash(readback));
        Assert.True(ReceiptVerifier.Verify(readback).Succeeded);
        Assert.Throws<IOException>(() => ReceiptWriter.WriteNew(receiptPath, outcome.Receipt));
    }

    [Fact]
    public void Receipt_verifier_rejects_payload_tampering()
    {
        var outcome = new FixtureVerifier().Verify(
            FindFixture("minimal-ptp-lin-valid"),
            "test-receipt-tamper");
        var tamperedPayload = outcome.Receipt.Payload with { FixtureId = "tampered-fixture" };
        var tamperedReceipt = outcome.Receipt with { Payload = tamperedPayload };

        var result = ReceiptVerifier.Verify(tamperedReceipt);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("payloadSha256", StringComparison.Ordinal));
    }

    private static string FailureDetails(FixtureVerificationOutcome outcome) => string.Join(
        Environment.NewLine,
        outcome.Receipt.Payload.Checks
            .Where(check => check.Status == VerificationStatus.Failed)
            .Select(check => $"{check.Id}: {check.Detail}"));

    private static string FindFixture(string fixtureName)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "fixtures", fixtureName);
            if (Directory.Exists(candidate)
                && File.Exists(Path.Combine(current.FullName, "KukaLab.slnx")))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException($"Could not find tracked fixture: {fixtureName}");
    }

    private sealed class TemporaryFixture : IDisposable
    {
        private TemporaryFixture(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryFixture Empty()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"kuka-lab-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryFixture(path);
        }

        public static TemporaryFixture CopyOf(string source)
        {
            var temporary = Empty();
            CopyDirectory(source, temporary.Path);
            return temporary;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(directory.Replace(source, destination, StringComparison.Ordinal));
            }

            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                File.Copy(file, file.Replace(source, destination, StringComparison.Ordinal));
            }
        }
    }
}
