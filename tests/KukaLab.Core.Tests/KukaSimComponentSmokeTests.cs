using System.Security.Cryptography;
using System.Text.Json;
using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class KukaSimComponentSmokeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Default_request_targets_the_controller_project_C01_component()
    {
        var request = KukaSimComponentSmokeRequest.CreateDefault(
            Path.Combine(Path.GetTempPath(), "kuka-lab-default-component"),
            guiExecutionAuthorized: false,
            authorizationReference: string.Empty);

        Assert.EndsWith("KR 210 R2700-2 C01.vcmx", request.ComponentPath, StringComparison.Ordinal);
        Assert.Equal(KukaSimComponentSmokeContract.ExactKr210R2700Component410Sha256, request.ExpectedComponentSha256);
    }

    [Fact]
    public void Authorized_exact_component_load_produces_ready_hash_valid_receipt()
    {
        using var environment = TestEnvironment.Create();
        var platform = new FakePlatform(
            new KukaSimProcessResult(0, true, false, false, true, 85, [4101, 4102], string.Empty),
            writePassedEvidence: true);

        var outcome = new KukaSimComponentSmokeRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(),
            "test-kukasim-ready");

        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.True(outcome.Receipt.Payload.GuiExecutionAuthorized);
        Assert.Empty(outcome.Receipt.Payload.PreExistingProcesses);
        Assert.NotNull(outcome.Receipt.Payload.InProcessResult);
        Assert.True(outcome.Receipt.Payload.InProcessResult!.LayoutSaved);
        Assert.Equal(1, outcome.Receipt.Payload.InProcessResult.LoadedComponentCount);
        Assert.False(outcome.Receipt.Payload.VrcConnectionPerformed);
        Assert.False(outcome.Receipt.Payload.SimulationPerformed);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.NativeKssStatus);
        Assert.True(KukaSimComponentSmokeReceiptVerifier.Verify(outcome.Receipt).Succeeded);
        Assert.Equal(1, platform.FindCalls);
        Assert.Single(platform.Invocations);
        Assert.DoesNotContain(
            platform.Invocations[0].Values,
            value => value.Contains("password", StringComparison.OrdinalIgnoreCase)
                || value.Contains("credential", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Missing_gui_authorization_blocks_before_process_inspection_or_evidence_write()
    {
        using var environment = TestEnvironment.Create();
        var platform = new FakePlatform(
            new KukaSimProcessResult(0, true, false, false, true, 10, [4101], string.Empty),
            writePassedEvidence: true);

        var outcome = new KukaSimComponentSmokeRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(guiAuthorized: false, authorizationReference: string.Empty),
            "test-kukasim-no-authorization");

        Assert.Equal(3, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(0, platform.FindCalls);
        Assert.Empty(platform.Invocations);
        Assert.False(Directory.Exists(environment.EvidenceDirectory));
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "gui-authorization" && check.Status == EnvironmentCheckStatus.Blocked);
        Assert.True(KukaSimComponentSmokeReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Preexisting_kukasim_process_blocks_without_attach_close_or_launch()
    {
        using var environment = TestEnvironment.Create();
        var existing = new KukaSimProcessObservation
        {
            ProcessId = 8123,
            ProcessName = "VisualComponents.Engine",
            ExecutablePath = environment.EnginePath,
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
        };
        var platform = new FakePlatform(
            new KukaSimProcessResult(0, true, false, false, true, 10, [4101], string.Empty),
            writePassedEvidence: true,
            existingProcesses: [existing]);

        var outcome = new KukaSimComponentSmokeRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(),
            "test-kukasim-preexisting");

        Assert.Equal(3, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
        Assert.Single(outcome.Receipt.Payload.PreExistingProcesses);
        Assert.Empty(platform.Invocations);
        Assert.False(Directory.Exists(environment.EvidenceDirectory));
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "process-ownership" && check.Status == EnvironmentCheckStatus.Blocked);
        Assert.True(KukaSimComponentSmokeReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Component_hash_mismatch_fails_before_authorization_or_process_use()
    {
        using var environment = TestEnvironment.Create();
        var platform = new FakePlatform(
            new KukaSimProcessResult(0, true, false, false, true, 10, [4101], string.Empty),
            writePassedEvidence: true);
        var request = environment.CreateRequest() with
        {
            ExpectedComponentSha256 = new string('A', 64)
        };

        var outcome = new KukaSimComponentSmokeRunner(platform, TimeProvider.System).Run(
            request,
            "test-kukasim-component-mismatch");

        Assert.Equal(2, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(0, platform.FindCalls);
        Assert.Empty(platform.Invocations);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "component-identity" && check.Status == EnvironmentCheckStatus.Failed);
        Assert.True(KukaSimComponentSmokeReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Missing_bootstrap_plugin_blocks_before_authorization_or_process_use()
    {
        using var environment = TestEnvironment.Create();
        File.Delete(environment.BootstrapPluginPath);
        var platform = new FakePlatform(
            new KukaSimProcessResult(0, true, false, false, true, 10, [4101], string.Empty),
            writePassedEvidence: true);

        var outcome = new KukaSimComponentSmokeRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(),
            "test-kukasim-missing-bootstrap");

        Assert.Equal(3, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(0, platform.FindCalls);
        Assert.Empty(platform.Invocations);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "kukasim-bootstrap-plugin"
                && check.Status == EnvironmentCheckStatus.Blocked);
        Assert.True(KukaSimComponentSmokeReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Timeout_with_verified_cleanup_is_blocked_and_reusable()
    {
        using var environment = TestEnvironment.Create();
        var platform = new FakePlatform(
            new KukaSimProcessResult(-1, true, true, false, true, 10_000, [4101], string.Empty),
            writePassedEvidence: false);

        var outcome = new KukaSimComponentSmokeRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(timeoutSeconds: 10),
            "test-kukasim-timeout");

        Assert.Equal(3, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
        Assert.True(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "component-load" && check.Status == EnvironmentCheckStatus.Blocked);
        Assert.True(KukaSimComponentSmokeReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Forced_termination_after_flushed_evidence_is_ready_when_cleanup_is_verified()
    {
        using var environment = TestEnvironment.Create();
        var platform = new FakePlatform(
            new KukaSimProcessResult(0, true, false, true, true, 500, [4101, 4102], string.Empty),
            writePassedEvidence: true);

        var outcome = new KukaSimComponentSmokeRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(),
            "test-kukasim-forced-cleanup");

        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.True(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "process-cleanup" && check.Status == EnvironmentCheckStatus.Passed);
        Assert.True(KukaSimComponentSmokeReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Unverified_cleanup_is_failed_and_not_reusable()
    {
        using var environment = TestEnvironment.Create();
        var platform = new FakePlatform(
            new KukaSimProcessResult(-1, true, true, true, false, 10_000, [4101], string.Empty),
            writePassedEvidence: false);

        var outcome = new KukaSimComponentSmokeRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(timeoutSeconds: 10),
            "test-kukasim-cleanup-failure");

        Assert.Equal(2, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.False(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "process-cleanup" && check.Status == EnvironmentCheckStatus.Failed);
        Assert.True(KukaSimComponentSmokeReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void In_process_failure_stays_adapter_failure_and_never_upgrades_native_kss()
    {
        using var environment = TestEnvironment.Create();
        var platform = new FakePlatform(
            new KukaSimProcessResult(2, true, false, false, true, 90, [4101], string.Empty),
            writePassedEvidence: false,
            writeFailedResult: true);

        var outcome = new KukaSimComponentSmokeRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(),
            "test-kukasim-script-failure");

        Assert.Equal(2, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.NativeKssStatus);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "component-load" && check.Status == EnvironmentCheckStatus.Failed);
        Assert.True(KukaSimComponentSmokeReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Reusing_attempt_id_does_not_overwrite_script_or_launch_twice()
    {
        using var environment = TestEnvironment.Create();
        var platform = new FakePlatform(
            new KukaSimProcessResult(0, true, false, false, true, 85, [4101], string.Empty),
            writePassedEvidence: true);
        var runner = new KukaSimComponentSmokeRunner(platform, TimeProvider.System);

        var first = runner.Run(environment.CreateRequest(), "test-kukasim-create-new");
        var second = runner.Run(environment.CreateRequest(), "test-kukasim-create-new");

        Assert.Equal(EnvironmentTerminalClassification.Ready, first.Receipt.Payload.TerminalClassification);
        Assert.Equal(EnvironmentTerminalClassification.Failed, second.Receipt.Payload.TerminalClassification);
        Assert.Single(platform.Invocations);
        Assert.Contains(
            second.Receipt.Payload.Checks,
            check => check.Id == "safe-smoke-script" && check.Status == EnvironmentCheckStatus.Failed);
    }

    [Fact]
    public void Receipt_verifier_rejects_semantic_and_raw_layout_tampering()
    {
        using var environment = TestEnvironment.Create();
        var platform = new FakePlatform(
            new KukaSimProcessResult(0, true, false, false, true, 85, [4101], string.Empty),
            writePassedEvidence: true);
        var receipt = new KukaSimComponentSmokeRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(),
            "test-kukasim-tamper").Receipt;
        var payload = receipt.Payload with { VrcConnectionPerformed = true };
        var semanticTamper = receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        Assert.False(KukaSimComponentSmokeReceiptVerifier.Verify(semanticTamper).Succeeded);

        var layout = Assert.Single(receipt.Payload.Files, file => file.Id == "kukasim-saved-layout");
        File.AppendAllText(layout.Path, "tampered");
        var rawTamper = KukaSimComponentSmokeReceiptVerifier.Verify(receipt);

        Assert.False(rawTamper.Succeeded);
        Assert.Contains(
            rawTamper.Errors,
            error => error.Contains("observed file length changed", StringComparison.Ordinal));
    }

    [Fact]
    public void Receipt_writer_is_create_new_and_strict_readback_verifies()
    {
        using var environment = TestEnvironment.Create();
        var platform = new FakePlatform(
            new KukaSimProcessResult(0, true, false, false, true, 85, [4101], string.Empty),
            writePassedEvidence: true);
        var receipt = new KukaSimComponentSmokeRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(),
            "test-kukasim-receipt-write").Receipt;
        var receiptPath = Path.Combine(environment.Root, "kukasim-component-smoke.receipt.json");

        KukaSimComponentSmokeReceiptWriter.WriteNew(receiptPath, receipt);
        var readback = ReceiptSerialization.KukaSimComponentSmokeFromJson(File.ReadAllText(receiptPath));

        Assert.True(KukaSimComponentSmokeReceiptVerifier.Verify(readback).Succeeded);
        Assert.Throws<IOException>(() => KukaSimComponentSmokeReceiptWriter.WriteNew(receiptPath, receipt));
    }

    [Fact]
    public void Historical_v1_ready_receipt_remains_verifiable_without_new_runtime_dependency_evidence()
    {
        using var environment = TestEnvironment.Create();
        var platform = new FakePlatform(
            new KukaSimProcessResult(0, true, false, false, true, 85, [4101], string.Empty),
            writePassedEvidence: true);
        var current = new KukaSimComponentSmokeRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(),
            "test-kukasim-historical-v1").Receipt;
        var v3DependencyIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "kukasim-bootstrap-plugin",
            "kukasim-ux-shared",
            "kukasim-caliburn",
            "netfx-csharp-compiler",
            "compiled-probe",
            "kukasim-compiled-probe"
        };
        var historicalPayload = current.Payload with
        {
            Checks = current.Payload.Checks
                .Where(check => !v3DependencyIds.Contains(check.Id))
                .Append(new EnvironmentCheck
                {
                    Id = "kukasim-script-starter",
                    Status = EnvironmentCheckStatus.Passed,
                    Detail = "Historical dependency evidence."
                })
                .ToList(),
            Files = current.Payload.Files
                .Where(file => !v3DependencyIds.Contains(file.Id))
                .Append(current.Payload.Files.Single(file => file.Id == "kukasim-bootstrap-plugin") with
                {
                    Id = "kukasim-script-starter"
                })
                .ToList()
        };
        var historicalReceipt = current with
        {
            SchemaVersion = 1,
            Payload = historicalPayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(historicalPayload)
        };

        var verification = KukaSimComponentSmokeReceiptVerifier.Verify(historicalReceipt);

        Assert.True(verification.Succeeded, string.Join(Environment.NewLine, verification.Errors));
    }

    [Fact]
    public void Process_ownership_accepts_only_transitive_launcher_descendants()
    {
        var parents = new Dictionary<int, int>
        {
            [200] = 100,
            [300] = 200,
            [400] = 999
        };

        Assert.True(KukaSimComponentPlatform.IsDescendantOf(200, 100, parents));
        Assert.True(KukaSimComponentPlatform.IsDescendantOf(300, 100, parents));
        Assert.False(KukaSimComponentPlatform.IsDescendantOf(400, 100, parents));
        Assert.False(KukaSimComponentPlatform.IsDescendantOf(100, 100, parents));
    }

    [Fact]
    public void Process_ownership_rejects_cycles_and_missing_parent_links()
    {
        var cyclicParents = new Dictionary<int, int>
        {
            [200] = 300,
            [300] = 200
        };

        Assert.False(KukaSimComponentPlatform.IsDescendantOf(200, 100, cyclicParents));
        Assert.False(KukaSimComponentPlatform.IsDescendantOf(500, 100, cyclicParents));
        Assert.False(KukaSimComponentPlatform.IsDescendantOf(0, 100, cyclicParents));
    }

    [Fact]
    public void Process_start_uses_launcher_and_passes_compiled_probe_through_environment()
    {
        const string launcherPath = @"C:\Program Files\KUKA\KUKA.Sim 4.10\VisualComponents.Engine.Launcher.exe";
        const string probeAssemblyPath = @"C:/kuka-validation-lab/vendor/KUKA Simulation\evidence\component smoke.dll";
        const string componentPath = @"C:/kuka-validation-lab/vendor/KUKA.Sim-4.10 Public\KR_210_R2700-2.vcmx";
        const string resultPath = @"C:/kuka-validation-lab/vendor/KUKA Simulation\evidence\result.json";
        const string layoutPath = @"C:/kuka-validation-lab/vendor/KUKA Simulation\evidence\layout.vcmx";
        const string tracePath = @"C:/kuka-validation-lab/vendor/KUKA Simulation\evidence\trace.log";

        var startInfo = KukaSimComponentPlatform.CreateStartInfo(
            launcherPath,
            probeAssemblyPath,
            componentPath,
            resultPath,
            layoutPath,
            tracePath);

        Assert.Equal(launcherPath, startInfo.FileName);
        Assert.Equal(Path.GetDirectoryName(launcherPath), startInfo.WorkingDirectory);
        Assert.False(startInfo.UseShellExecute);
        Assert.Empty(startInfo.ArgumentList);
        Assert.Equal(probeAssemblyPath, startInfo.Environment["KUKA_LAB_PROBE_ASSEMBLY"]);
        Assert.Equal("KukaLabComponentLoadSmoke", startInfo.Environment["KUKA_LAB_PROBE_TYPE"]);
        Assert.Equal(tracePath, startInfo.Environment["KUKA_LAB_BRIDGE_TRACE_PATH"]);
        Assert.Equal(resultPath, startInfo.Environment["KUKA_LAB_RESULT_PATH"]);
        Assert.Equal(componentPath, startInfo.Environment["KUKA_LAB_COMPONENT_PATH"]);
        Assert.Equal(layoutPath, startInfo.Environment["KUKA_LAB_LAYOUT_PATH"]);
    }

    private sealed class FakePlatform(
        KukaSimProcessResult result,
        bool writePassedEvidence,
        bool writeFailedResult = false,
        IReadOnlyList<KukaSimProcessObservation>? existingProcesses = null) : IKukaSimComponentPlatform
    {
        public int FindCalls { get; private set; }

        public List<Invocation> Invocations { get; } = [];

        public IReadOnlyList<KukaSimProcessObservation> FindRunningProcesses(
            string launcherPath,
            string enginePath)
        {
            FindCalls++;
            return existingProcesses ?? [];
        }

        public KukaSimProcessResult Run(
            string launcherPath,
            string enginePath,
            string bootstrapPluginPath,
            string compilerPath,
            string scriptPath,
            string probeAssemblyPath,
            string componentPath,
            string resultPath,
            string layoutPath,
            string bridgeTracePath,
            TimeSpan timeout)
        {
            File.WriteAllText(probeAssemblyPath, "synthetic compiled probe");
            Invocations.Add(new Invocation(
                [launcherPath, enginePath, bootstrapPluginPath, compilerPath, scriptPath, probeAssemblyPath, componentPath, resultPath, layoutPath, bridgeTracePath],
                timeout));
            if (writePassedEvidence)
            {
                File.WriteAllText(layoutPath, "synthetic loaded layout");
                WriteResult(resultPath, new KukaSimInProcessResult
                {
                    SchemaIdentity = KukaSimComponentSmokeContract.ResultSchemaIdentity,
                    SchemaVersion = KukaSimComponentSmokeContract.ResultSchemaVersion,
                    Status = "Passed",
                    ComponentPath = componentPath,
                    ComponentSha256 = ComputeSha256(componentPath),
                    LayoutPath = layoutPath,
                    LayoutSaved = true,
                    IsComponent = true,
                    LoadedComponentCount = 1,
                    LoadedComponentNames = ["KR 210 R2700-2"],
                    ApplicationInitialized = true,
                    ApplicationReady = true,
                    ValidLicenseExists = true,
                    EngineVersion = "4.6.0.2",
                    ExitRequested = true
                });
            }
            else if (writeFailedResult)
            {
                WriteResult(resultPath, new KukaSimInProcessResult
                {
                    SchemaIdentity = KukaSimComponentSmokeContract.ResultSchemaIdentity,
                    SchemaVersion = KukaSimComponentSmokeContract.ResultSchemaVersion,
                    Status = "Failed",
                    ComponentPath = componentPath,
                    ComponentSha256 = ComputeSha256(componentPath),
                    LayoutPath = layoutPath,
                    ExitRequested = true,
                    EngineVersion = "4.6.0.2",
                    ErrorType = "SyntheticLoadException",
                    ErrorMessage = "controlled failure"
                });
            }

            return result;
        }

        private static void WriteResult(string path, KukaSimInProcessResult resultValue) =>
            File.WriteAllText(path, JsonSerializer.Serialize(resultValue, JsonOptions));
    }

    private sealed record Invocation(List<string> Values, TimeSpan Timeout);

    private sealed class TestEnvironment : IDisposable
    {
        private TestEnvironment(string root)
        {
            Root = root;
            var install = Path.Combine(root, "install");
            Directory.CreateDirectory(install);
            LauncherPath = CreateFile(install, "VisualComponents.Engine.Launcher.exe", "synthetic launcher");
            EnginePath = CreateFile(install, "VisualComponents.Engine.exe", "synthetic engine");
            BootstrapPluginPath = CreateFile(install, KukaSimInstallationDiscovery.BootstrapPluginFileName, "synthetic bootstrap plugin");
            Create3DSharedPath = CreateFile(install, "Create3D.Shared.dll", "synthetic Create3D API");
            _ = CreateFile(install, "UX.Shared.dll", "synthetic UX API");
            _ = CreateFile(install, "Caliburn.Micro.dll", "synthetic IoC API");
            ComponentPath = CreateFile(root, "KR_210_R2700-2.vcmx", "synthetic exact robot component");
            ComponentSha256 = ComputeSha256(ComponentPath);
            EvidenceDirectory = Path.Combine(root, "evidence");
        }

        public string Root { get; }

        public string LauncherPath { get; }

        public string EnginePath { get; }

        public string BootstrapPluginPath { get; }

        public string Create3DSharedPath { get; }

        public string ComponentPath { get; }

        private string ComponentSha256 { get; }

        public string EvidenceDirectory { get; }

        public static TestEnvironment Create() =>
            new(Path.Combine(Path.GetTempPath(), $"kuka-lab-kukasim-{Guid.NewGuid():N}"));

        public KukaSimComponentSmokeRequest CreateRequest(
            bool guiAuthorized = true,
            string authorizationReference = "owner-test-kukasim-01",
            int timeoutSeconds = 180) =>
            new()
            {
                LauncherPath = LauncherPath,
                EnginePath = EnginePath,
                BootstrapPluginPath = BootstrapPluginPath,
                Create3DSharedPath = Create3DSharedPath,
                ComponentPath = ComponentPath,
                ExpectedComponentSha256 = ComponentSha256,
                EvidenceDirectory = EvidenceDirectory,
                TimeoutSeconds = timeoutSeconds,
                GuiExecutionAuthorized = guiAuthorized,
                AuthorizationReference = authorizationReference
            };

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }

        private static string CreateFile(string directory, string name, string content)
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, name);
            File.WriteAllText(path, content);
            return path;
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
