using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class RawKrlCandidateTests
{
    [Fact]
    public void Valid_src_dat_pair_creates_verifiable_black_box_candidate()
    {
        using var fixture = CandidateFixture.Create();

        var outcome = fixture.Run("test-raw-krl-valid");
        var written = fixture.Write(outcome.Receipt);
        var readback = ReceiptSerialization.RawKrlCandidateFromJson(File.ReadAllText(written));
        var current = RawKrlCandidateReceiptVerifier.VerifyCurrentSource(readback, fixture.SourceRoot);
        var json = ReceiptSerialization.ToJson(readback);

        Assert.True(outcome.Succeeded, FailureDetails(outcome));
        Assert.True(current.Succeeded, string.Join("; ", current.Errors));
        Assert.True(current.CurrentSourceVerified);
        Assert.Single(outcome.Receipt.Payload.Programs);
        Assert.Equal(2, outcome.Receipt.Payload.Files.Count);
        Assert.StartsWith("sha256:", outcome.Receipt.Payload.CandidateId, StringComparison.Ordinal);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.NativeKss.Status);
        Assert.DoesNotContain(fixture.SourceRoot, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unsupported_file_fails_boundary_without_hashing_or_recording_its_content()
    {
        using var fixture = CandidateFixture.Create();
        var unsupported = Path.Combine(fixture.SourceRoot, "notes.txt");
        File.WriteAllText(unsupported, "must-not-enter-candidate");

        var outcome = fixture.Run("test-raw-krl-extra-file");

        Assert.False(outcome.Succeeded);
        Assert.Equal(1, outcome.Receipt.Payload.UnsupportedFileCount);
        Assert.DoesNotContain(
            outcome.Receipt.Payload.Files,
            file => string.Equals(file.RelativePath, "notes.txt", StringComparison.Ordinal));
        Assert.DoesNotContain("must-not-enter-candidate", ReceiptSerialization.ToJson(outcome.Receipt), StringComparison.Ordinal);
        Assert.True(RawKrlCandidateReceiptVerifier.VerifyIntegrity(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Missing_dat_pair_is_rejected_but_receipt_remains_integrity_valid()
    {
        using var fixture = CandidateFixture.Create(includeData: false);

        var outcome = fixture.Run("test-raw-krl-missing-dat");

        Assert.False(outcome.Succeeded);
        Assert.Empty(outcome.Receipt.Payload.Programs);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "program-pairs" && check.Status == VerificationStatus.Failed);
        Assert.True(RawKrlCandidateReceiptVerifier.VerifyIntegrity(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Rehashed_receipt_cannot_invent_a_program_pair()
    {
        using var fixture = CandidateFixture.Create();
        var receipt = fixture.Run("test-raw-krl-tamper").Receipt;
        var payload = receipt.Payload with { Programs = [] };
        var tampered = receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var result = RawKrlCandidateReceiptVerifier.VerifyIntegrity(tampered);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("program pairs", StringComparison.Ordinal));
    }

    [Fact]
    public void Current_source_verification_detects_post_receipt_drift()
    {
        using var fixture = CandidateFixture.Create();
        var receipt = fixture.Run("test-raw-krl-drift").Receipt;
        File.AppendAllText(Path.Combine(fixture.SourceRoot, "CELL.src"), "; drift");

        var result = RawKrlCandidateReceiptVerifier.VerifyCurrentSource(receipt, fixture.SourceRoot);

        Assert.False(result.Succeeded);
        Assert.False(result.CurrentSourceVerified);
        Assert.Contains(result.Errors, error => error.Contains("no longer matches", StringComparison.Ordinal));
    }

    [Fact]
    public void Receipt_output_inside_source_is_rejected_before_writing()
    {
        using var fixture = CandidateFixture.Create();
        var unsafeOutput = Path.Combine(fixture.SourceRoot, "receipt.json");

        Assert.Throws<ArgumentException>(() => new RawKrlCandidateRunner().Run(
            new RawKrlCandidateRequest
            {
                SourceRoot = fixture.SourceRoot,
                ReceiptOutputPath = unsafeOutput
            },
            "test-raw-krl-output-boundary"));
        Assert.False(File.Exists(unsafeOutput));
    }

    [Fact]
    public void Protected_license_directory_is_rejected_before_file_enumeration()
    {
        var protectedDirectory = @"C:\ProgramData\Visual Components\Visual Components License Server 2.0";

        var exception = Assert.Throws<UnauthorizedAccessException>(() =>
            RawKrlCandidateBoundary.ValidateSourceRoot(protectedDirectory));

        Assert.Contains("protected Visual Components", exception.Message, StringComparison.Ordinal);
    }

    private static string FailureDetails(RawKrlCandidateOutcome outcome) =>
        string.Join(
            Environment.NewLine,
            outcome.Receipt.Payload.Checks
                .Where(check => check.Status == VerificationStatus.Failed)
                .Select(check => $"{check.Id}: {check.Detail}"));

    private sealed class CandidateFixture : IDisposable
    {
        private CandidateFixture(string root, bool includeData)
        {
            Root = root;
            SourceRoot = Path.Combine(root, "source");
            ReceiptPath = Path.Combine(root, "evidence", "raw-krl.receipt.json");
            Directory.CreateDirectory(SourceRoot);
            File.WriteAllText(
                Path.Combine(SourceRoot, "CELL.src"),
                "DEF CELL()\n BAS(#INITMOV,0)\n PTP HOME\nEND\n");
            if (includeData)
            {
                File.WriteAllText(
                    Path.Combine(SourceRoot, "CELL.dat"),
                    "DEFDAT CELL PUBLIC\n DECL E6AXIS HOME={A1 0,A2 -90,A3 90,A4 0,A5 0,A6 0}\nENDDAT\n");
            }
        }

        public string Root { get; }

        public string SourceRoot { get; }

        public string ReceiptPath { get; }

        public static CandidateFixture Create(bool includeData = true)
        {
            var root = Path.Combine(Path.GetTempPath(), $"kuka-lab-raw-krl-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            return new CandidateFixture(root, includeData);
        }

        public RawKrlCandidateOutcome Run(string attemptId) =>
            new RawKrlCandidateRunner().Run(
                new RawKrlCandidateRequest
                {
                    SourceRoot = SourceRoot,
                    ReceiptOutputPath = ReceiptPath
                },
                attemptId);

        public string Write(RawKrlCandidateReceipt receipt) =>
            RawKrlCandidateReceiptWriter.WriteNew(ReceiptPath, SourceRoot, receipt);

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
