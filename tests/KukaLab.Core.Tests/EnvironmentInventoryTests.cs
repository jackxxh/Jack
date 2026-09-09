using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class EnvironmentInventoryTests
{
    [Fact]
    public void Default_inventory_discovers_vmrun_under_asset_root()
    {
        using var environment = TestEnvironment.Create(includeVmrun: true);

        var outcome = new EnvironmentInventoryCollector().Collect(
            environment.CreateDefaultRequest(),
            "test-environment-asset-root-vmrun");

        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.Contains(
            outcome.Receipt.Payload.Files,
            file => file.Id == "vmware-vmrun"
                && string.Equals(file.Path, environment.Vmrun, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Missing_vmrun_produces_hash_valid_blocked_receipt()
    {
        using var environment = TestEnvironment.Create(includeVmrun: false);

        var outcome = new EnvironmentInventoryCollector().Collect(
            environment.CreateRequest(),
            "test-environment-blocked");

        Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(3, outcome.ExitCode);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "vmware-vmrun" && check.Status == EnvironmentCheckStatus.Blocked);
        Assert.True(EnvironmentReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Present_required_binaries_and_hardened_vmx_are_ready_with_robot_warning()
    {
        using var environment = TestEnvironment.Create(includeVmrun: true);

        var outcome = new EnvironmentInventoryCollector().Collect(
            environment.CreateRequest(),
            "test-environment-ready");

        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(0, outcome.ExitCode);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "officelite-primary-hardening" && check.Status == EnvironmentCheckStatus.Passed);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "exact-robot-component" && check.Status == EnvironmentCheckStatus.Warning);
    }

    [Fact]
    public void Exact_installed_component_candidate_is_hashed_and_passes_with_loadability_warning()
    {
        using var environment = TestEnvironment.Create(includeVmrun: true);
        var exactComponent = environment.AddExactRobotComponent();
        var request = environment.CreateRequest() with
        {
            ExactRobotComponentCandidatePaths = [exactComponent]
        };

        var outcome = new EnvironmentInventoryCollector().Collect(
            request,
            "test-environment-exact-component");

        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "exact-robot-component" && check.Status == EnvironmentCheckStatus.Passed);
        var observation = Assert.Single(
            outcome.Receipt.Payload.Files,
            file => file.Id == "kuka-sim-exact-robot-component");
        Assert.Equal(Path.GetFullPath(exactComponent), observation.Path);
        Assert.Equal(64, observation.Sha256?.Length);
        Assert.Contains(
            outcome.Receipt.Payload.Warnings,
            warning => warning.Contains("loadability", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Strict_postinstall_inventory_rejects_generic_kr210_without_c01_identity()
    {
        using var environment = TestEnvironment.Create(includeVmrun: true);
        var genericComponent = environment.AddExactRobotComponent();
        var request = environment.CreateRequest() with
        {
            ExactRobotComponentCandidatePaths = [genericComponent],
            RequireExactC01RobotComponent = true
        };

        var outcome = new EnvironmentInventoryCollector().Collect(
            request,
            "test-environment-generic-component-rejected");

        Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "exact-robot-component" && check.Status == EnvironmentCheckStatus.Blocked);
    }

    [Fact]
    public void Strict_postinstall_inventory_accepts_exact_c01_identity()
    {
        using var environment = TestEnvironment.Create(includeVmrun: true);
        var component = environment.AddExactC01RobotComponent();
        var request = environment.CreateRequest() with
        {
            ExactRobotComponentCandidatePaths = [component],
            RequireExactC01RobotComponent = true
        };

        var outcome = new EnvironmentInventoryCollector().Collect(
            request,
            "test-environment-c01-component-accepted");

        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "exact-robot-component" && check.Status == EnvironmentCheckStatus.Passed);
    }

    [Fact]
    public void Environment_receipt_verifier_rejects_payload_tampering()
    {
        using var environment = TestEnvironment.Create(includeVmrun: true);
        var outcome = new EnvironmentInventoryCollector().Collect(
            environment.CreateRequest(),
            "test-environment-tamper");
        var tamperedPayload = outcome.Receipt.Payload with { AssetRoot = "tampered" };
        var tampered = outcome.Receipt with { Payload = tamperedPayload };

        var result = EnvironmentReceiptVerifier.Verify(tampered);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("payloadSha256", StringComparison.Ordinal));
    }

    [Fact]
    public void Environment_receipt_verifier_rejects_duplicate_check_ids_even_when_rehashed()
    {
        using var environment = TestEnvironment.Create(includeVmrun: true);
        var outcome = new EnvironmentInventoryCollector().Collect(
            environment.CreateRequest(),
            "test-environment-duplicate-check");
        var duplicateChecks = outcome.Receipt.Payload.Checks
            .Append(outcome.Receipt.Payload.Checks[0])
            .ToList();
        var payload = outcome.Receipt.Payload with { Checks = duplicateChecks };
        var rehashed = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var result = EnvironmentReceiptVerifier.Verify(rehashed);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("unique IDs", StringComparison.Ordinal));
    }

    [Fact]
    public void Unsafe_primary_vmx_fails_preflight()
    {
        using var environment = TestEnvironment.Create(includeVmrun: true, hardenedVmx: false);

        var outcome = new EnvironmentInventoryCollector().Collect(
            environment.CreateRequest(),
            "test-environment-unsafe-vmx");

        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(2, outcome.ExitCode);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "officelite-primary-hardening" && check.Status == EnvironmentCheckStatus.Failed);
    }

    [Fact]
    public void Environment_receipt_diff_is_empty_for_same_receipt()
    {
        using var environment = TestEnvironment.Create(includeVmrun: true);
        var receipt = new EnvironmentInventoryCollector().Collect(
            environment.CreateRequest(),
            "test-environment-same-diff").Receipt;

        var result = EnvironmentReceiptDiffer.Compare(receipt, receipt);

        Assert.True(result.Succeeded);
        Assert.False(result.HasDifferences);
        Assert.Empty(result.Differences);
    }

    [Fact]
    public void Environment_receipt_diff_localizes_vmware_transition()
    {
        using var environment = TestEnvironment.Create(includeVmrun: false);
        var collector = new EnvironmentInventoryCollector();
        var baseline = collector.Collect(
            environment.CreateRequest(),
            "test-environment-diff-before").Receipt;
        environment.EnableVmrun();
        var current = collector.Collect(
            environment.CreateRequest(),
            "test-environment-diff-after").Receipt;

        var result = EnvironmentReceiptDiffer.Compare(baseline, current);

        Assert.True(result.Succeeded);
        Assert.True(result.HasDifferences);
        Assert.Contains(
            result.Differences,
            difference => difference.Scope == "check" && difference.Key == "vmware-vmrun");
        Assert.Contains(
            result.Differences,
            difference => difference.Scope == "environment" && difference.Key == "terminalClassification");
    }

    private sealed class TestEnvironment : IDisposable
    {
        private TestEnvironment(string root, string kukaSim, string workVisual, string vmrun)
        {
            Root = root;
            KukaSim = kukaSim;
            WorkVisual = workVisual;
            Vmrun = vmrun;
        }

        private string Root { get; }

        private string KukaSim { get; }

        private string WorkVisual { get; }

        public string Vmrun { get; }

        public static TestEnvironment Create(bool includeVmrun, bool hardenedVmx = true)
        {
            var root = Path.Combine(Path.GetTempPath(), $"kuka-lab-environment-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var binaries = Path.Combine(root, "test-binaries");
            Directory.CreateDirectory(binaries);
            var kukaSim = Path.Combine(binaries, "KukaSim.exe");
            var workVisual = Path.Combine(binaries, "WorkVisual.exe");
            var vmware = Path.Combine(root, "VMware");
            Directory.CreateDirectory(vmware);
            var vmrun = Path.Combine(vmware, "vmrun.exe");
            File.WriteAllText(kukaSim, "synthetic KUKA.Sim binary");
            File.WriteAllText(workVisual, "synthetic WorkVisual binary");
            if (includeVmrun)
            {
                File.WriteAllText(vmrun, "synthetic vmrun binary");
            }

            var primary = Path.Combine(
                root,
                "OfficeLite-Work",
                "8.7.8-build04",
                "runs",
                "primary");
            Directory.CreateDirectory(primary);
            var vmxLines = hardenedVmx
                ? new[]
                {
                    "isolation.tools.hgfs.disable = \"TRUE\"",
                    "sharedFolder0.present = \"FALSE\"",
                    "sharedFolder0.enabled = \"FALSE\"",
                    "sharedFolder0.readAccess = \"FALSE\"",
                    "sharedFolder0.writeAccess = \"FALSE\"",
                    "sharedFolder.maxNum = \"0\"",
                    "hgfs.mapRootShare = \"FALSE\""
                }
                : new[]
                {
                    "isolation.tools.hgfs.disable = \"FALSE\"",
                    "sharedFolder0.present = \"TRUE\"",
                    "sharedFolder0.enabled = \"TRUE\""
                };
            File.WriteAllLines(Path.Combine(primary, "KR C, V8.7.8OL_Build04.vmx"), vmxLines);
            Directory.CreateDirectory(Path.Combine(root, "KUKA Sim11 Components", "KUKA Sim11 Components"));
            return new TestEnvironment(root, kukaSim, workVisual, vmrun);
        }

        public EnvironmentInventoryRequest CreateRequest() => new()
        {
            AssetRoot = Root,
            KukaSimLauncherPath = KukaSim,
            WorkVisualPath = WorkVisual,
            VmrunCandidatePaths = [Vmrun],
            RequireSupportedKukaSim410Release = false
        };

        public EnvironmentInventoryRequest CreateDefaultRequest() =>
            EnvironmentInventoryRequest.CreateDefault(Root, KukaSim, WorkVisual) with
            {
                RequireSupportedKukaSim410Release = false
            };

        public void EnableVmrun()
        {
            File.WriteAllText(Vmrun, "synthetic vmrun binary");
        }

        public string AddExactRobotComponent()
        {
            var library = Path.Combine(Root, "installed-library");
            Directory.CreateDirectory(library);
            var component = Path.Combine(library, "KR_210_R2700-2.vcmx");
            File.WriteAllText(component, "synthetic exact robot component");
            return component;
        }

        public string AddExactC01RobotComponent()
        {
            var library = Path.Combine(Root, "installed-library-c01");
            Directory.CreateDirectory(library);
            var component = Path.Combine(library, "KR 210 R2700-2 C01.vcmx");
            File.WriteAllText(component, "synthetic exact C01 robot component");
            return component;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
