using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class WorkVisualRunnerSmokeTests
{
    [Fact]
    public void Pinned_read_only_smoke_produces_ready_hash_valid_receipt()
    {
        using var environment = TestEnvironment.Create();
        var platform = new FakePlatform(new WorkVisualProcessResult(
            0,
            false,
            true,
            25,
            "KUKA Lab WorkVisual host-global read-only smoke\nApplication=WorkVisual\n",
            string.Empty));

        var outcome = new WorkVisualRunnerSmokeRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(),
            "test-workvisual-ready");

        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.True(outcome.Receipt.Payload.SuccessMarkerObserved);
        Assert.False(outcome.Receipt.Payload.ProjectInspectionPerformed);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.NativeKssStatus);
        Assert.Equal(
            WorkVisualRunnerSmokeContract.EmbeddedScriptSha256,
            outcome.Receipt.Payload.EmbeddedScriptSha256);
        Assert.True(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.Contains(
            outcome.Receipt.Payload.Files,
            file => file.Id == "workvisual-safe-smoke-script"
                && file.Sha256 == WorkVisualRunnerSmokeContract.EmbeddedScriptSha256);
        Assert.True(WorkVisualRunnerSmokeReceiptVerifier.Verify(outcome.Receipt).Succeeded);
        Assert.Single(platform.Invocations);
        Assert.DoesNotContain(
            platform.Invocations[0].Arguments,
            argument => argument.Contains("password", StringComparison.OrdinalIgnoreCase)
                || argument.Contains("credential", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Nonzero_runner_exit_is_adapter_failure_not_native_kss_failure()
    {
        using var environment = TestEnvironment.Create();
        var platform = new FakePlatform(new WorkVisualProcessResult(
            42,
            false,
            true,
            30,
            "runner failed\n",
            "typed vendor failure\n"));

        var outcome = new WorkVisualRunnerSmokeRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(),
            "test-workvisual-runner-failure");

        Assert.Equal(2, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.NativeKssStatus);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "runner-execution" && check.Status == EnvironmentCheckStatus.Failed);
        Assert.True(WorkVisualRunnerSmokeReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Timeout_with_verified_cleanup_is_blocked_and_reusable()
    {
        using var environment = TestEnvironment.Create();
        var platform = new FakePlatform(new WorkVisualProcessResult(
            -1,
            true,
            true,
            1000,
            string.Empty,
            string.Empty));

        var outcome = new WorkVisualRunnerSmokeRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(timeoutSeconds: 1),
            "test-workvisual-timeout");

        Assert.Equal(3, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
        Assert.True(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "runner-execution" && check.Status == EnvironmentCheckStatus.Blocked);
        Assert.True(WorkVisualRunnerSmokeReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Unverified_timeout_cleanup_is_failed_and_not_reusable()
    {
        using var environment = TestEnvironment.Create();
        var platform = new FakePlatform(new WorkVisualProcessResult(
            -1,
            true,
            false,
            1000,
            string.Empty,
            string.Empty));

        var outcome = new WorkVisualRunnerSmokeRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(timeoutSeconds: 1),
            "test-workvisual-cleanup-failure");

        Assert.Equal(2, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.False(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "runner-cleanup" && check.Status == EnvironmentCheckStatus.Failed);
        Assert.True(WorkVisualRunnerSmokeReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Missing_runner_is_blocked_without_process_invocation_or_evidence_write()
    {
        using var environment = TestEnvironment.Create(includeRunner: false);
        var platform = new FakePlatform(new WorkVisualProcessResult(
            0,
            false,
            true,
            0,
            WorkVisualRunnerSmokeContract.SuccessMarker,
            string.Empty));

        var outcome = new WorkVisualRunnerSmokeRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(),
            "test-workvisual-missing-runner");

        Assert.Equal(3, outcome.ExitCode);
        Assert.Empty(platform.Invocations);
        Assert.Empty(outcome.Receipt.Payload.SideEffects);
        Assert.False(Directory.Exists(environment.EvidenceDirectory));
        Assert.True(WorkVisualRunnerSmokeReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Reusing_attempt_id_does_not_overwrite_evidence_or_invoke_runner_twice()
    {
        using var environment = TestEnvironment.Create();
        var platform = new FakePlatform(new WorkVisualProcessResult(
            0,
            false,
            true,
            25,
            WorkVisualRunnerSmokeContract.SuccessMarker,
            string.Empty));
        var runner = new WorkVisualRunnerSmokeRunner(platform, TimeProvider.System);

        var first = runner.Run(environment.CreateRequest(), "test-workvisual-create-new");
        var second = runner.Run(environment.CreateRequest(), "test-workvisual-create-new");

        Assert.Equal(EnvironmentTerminalClassification.Ready, first.Receipt.Payload.TerminalClassification);
        Assert.Equal(EnvironmentTerminalClassification.Failed, second.Receipt.Payload.TerminalClassification);
        Assert.Single(platform.Invocations);
        Assert.Contains(
            second.Receipt.Payload.Checks,
            check => check.Id == "safe-smoke-script" && check.Status == EnvironmentCheckStatus.Failed);
    }

    [Fact]
    public void Receipt_verifier_rejects_semantic_tampering_even_when_rehashed()
    {
        using var environment = TestEnvironment.Create();
        var platform = new FakePlatform(new WorkVisualProcessResult(
            0,
            false,
            true,
            25,
            WorkVisualRunnerSmokeContract.SuccessMarker,
            string.Empty));
        var receipt = new WorkVisualRunnerSmokeRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(),
            "test-workvisual-tamper").Receipt;
        var payload = receipt.Payload with { SuccessMarkerObserved = false };
        var tampered = receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var verification = WorkVisualRunnerSmokeReceiptVerifier.Verify(tampered);

        Assert.False(verification.Succeeded);
        Assert.Contains(
            verification.Errors,
            error => error.Contains("ready runner smoke", StringComparison.Ordinal));
    }

    [Fact]
    public void Receipt_verifier_rejects_removed_required_evidence_even_when_rehashed()
    {
        using var environment = TestEnvironment.Create();
        var platform = new FakePlatform(new WorkVisualProcessResult(
            0,
            false,
            true,
            25,
            WorkVisualRunnerSmokeContract.SuccessMarker,
            string.Empty));
        var receipt = new WorkVisualRunnerSmokeRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(),
            "test-workvisual-evidence-removal").Receipt;
        var payload = receipt.Payload with
        {
            Files = receipt.Payload.Files
                .Where(file => file.Id != "workvisual-smoke-stdout")
                .ToList()
        };
        var tampered = receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var verification = WorkVisualRunnerSmokeReceiptVerifier.Verify(tampered);

        Assert.False(verification.Succeeded);
        Assert.Contains(
            verification.Errors,
            error => error.Contains("lacks observed file: workvisual-smoke-stdout", StringComparison.Ordinal));
    }

    [Fact]
    public void Receipt_verifier_rejects_raw_evidence_tampering()
    {
        using var environment = TestEnvironment.Create();
        var platform = new FakePlatform(new WorkVisualProcessResult(
            0,
            false,
            true,
            25,
            WorkVisualRunnerSmokeContract.SuccessMarker,
            string.Empty));
        var receipt = new WorkVisualRunnerSmokeRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(),
            "test-workvisual-raw-tamper").Receipt;
        var stdout = Assert.Single(
            receipt.Payload.Files,
            file => file.Id == "workvisual-smoke-stdout");
        File.AppendAllText(stdout.Path, "tampered");

        var verification = WorkVisualRunnerSmokeReceiptVerifier.Verify(receipt);

        Assert.False(verification.Succeeded);
        Assert.Contains(
            verification.Errors,
            error => error.Contains("observed file length changed", StringComparison.Ordinal));
    }

    [Fact]
    public void Receipt_writer_is_create_new_and_strict_readback_verifies()
    {
        using var environment = TestEnvironment.Create();
        var platform = new FakePlatform(new WorkVisualProcessResult(
            0,
            false,
            true,
            25,
            WorkVisualRunnerSmokeContract.SuccessMarker,
            string.Empty));
        var receipt = new WorkVisualRunnerSmokeRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(),
            "test-workvisual-receipt-write").Receipt;
        var receiptPath = Path.Combine(environment.Root, "workvisual-smoke.receipt.json");

        WorkVisualRunnerSmokeReceiptWriter.WriteNew(receiptPath, receipt);
        var readback = ReceiptSerialization.WorkVisualRunnerSmokeFromJson(File.ReadAllText(receiptPath));

        Assert.True(WorkVisualRunnerSmokeReceiptVerifier.Verify(readback).Succeeded);
        Assert.Throws<IOException>(() => WorkVisualRunnerSmokeReceiptWriter.WriteNew(receiptPath, receipt));
    }

    private sealed class FakePlatform(WorkVisualProcessResult result) : IWorkVisualRunnerPlatform
    {
        public List<Invocation> Invocations { get; } = [];

        public WorkVisualProcessResult Run(
            string executablePath,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TimeSpan timeout)
        {
            Invocations.Add(new Invocation(executablePath, arguments.ToList(), workingDirectory, timeout));
            return result;
        }
    }

    private sealed record Invocation(
        string ExecutablePath,
        List<string> Arguments,
        string WorkingDirectory,
        TimeSpan Timeout);

    private sealed class TestEnvironment : IDisposable
    {
        private TestEnvironment(string root, string runnerPath, string evidenceDirectory)
        {
            Root = root;
            RunnerPath = runnerPath;
            EvidenceDirectory = evidenceDirectory;
        }

        public string Root { get; }

        private string RunnerPath { get; }

        public string EvidenceDirectory { get; }

        public static TestEnvironment Create(bool includeRunner = true)
        {
            var root = Path.Combine(Path.GetTempPath(), $"kuka-lab-workvisual-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var runnerPath = Path.Combine(root, "wvsr.exe");
            if (includeRunner)
            {
                File.WriteAllText(runnerPath, "synthetic WorkVisual Script Runner");
            }

            return new TestEnvironment(root, runnerPath, Path.Combine(root, "evidence"));
        }

        public WorkVisualRunnerSmokeRequest CreateRequest(int timeoutSeconds = 30) => new()
        {
            RunnerPath = RunnerPath,
            EvidenceDirectory = EvidenceDirectory,
            TimeoutSeconds = timeoutSeconds
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
