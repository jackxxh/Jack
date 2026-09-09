using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class OfficeLiteDeliveryInspectionTests
{
    [Fact]
    public void Build05_hyperv_export_is_import_eligible_without_claiming_runtime_readiness()
    {
        using var delivery = DeliveryTestRoot.Create();
        delivery.AddHyperVExport();

        var outcome = new OfficeLiteDeliveryInspector().Inspect(
            delivery.CreateRequest(buildNumber: 5),
            "test-delivery-build05");

        Assert.Equal(OfficeLiteDeliveryKind.HyperVExport, outcome.Receipt.Payload.DeliveryKind);
        Assert.Equal(OfficeLiteDeliveryTerminalClassification.Eligible, outcome.Receipt.Payload.TerminalClassification);
        Assert.True(outcome.Receipt.Payload.HyperVImportEligible);
        Assert.Equal(0, outcome.ExitCode);
        Assert.Contains(
            outcome.Receipt.Payload.UnsupportedClaims,
            claim => claim.Contains("controller readiness", StringComparison.OrdinalIgnoreCase));
        Assert.True(OfficeLiteDeliveryReceiptVerifier.VerifyIntegrity(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Build04_vmware_delivery_is_blocked_for_hyperv_import()
    {
        using var delivery = DeliveryTestRoot.Create();
        delivery.AddLegacyVmware();

        var outcome = new OfficeLiteDeliveryInspector().Inspect(
            delivery.CreateRequest(buildNumber: 4),
            "test-delivery-build04");

        Assert.Equal(OfficeLiteDeliveryKind.LegacyVmware, outcome.Receipt.Payload.DeliveryKind);
        Assert.Equal(OfficeLiteDeliveryTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
        Assert.False(outcome.Receipt.Payload.HyperVImportEligible);
        Assert.Equal(3, outcome.ExitCode);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "declared-release-build"
                && check.Status == OfficeLiteDeliveryCheckStatus.Blocked);
    }

    [Fact]
    public void Vhdx_without_export_configuration_is_not_accepted_as_official_import_package()
    {
        using var delivery = DeliveryTestRoot.Create();
        delivery.AddFile("Virtual Hard Disks/OfficeLite.vhdx", "synthetic disk");

        var outcome = new OfficeLiteDeliveryInspector().Inspect(
            delivery.CreateRequest(buildNumber: 5),
            "test-delivery-disk-only");

        Assert.Equal(OfficeLiteDeliveryKind.HyperVDiskOnly, outcome.Receipt.Payload.DeliveryKind);
        Assert.Equal(OfficeLiteDeliveryTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
        Assert.False(outcome.Receipt.Payload.HyperVImportEligible);
    }

    [Fact]
    public void Mixed_vmware_and_hyperv_surfaces_fail_closed()
    {
        using var delivery = DeliveryTestRoot.Create();
        delivery.AddHyperVExport();
        delivery.AddLegacyVmware();

        var outcome = new OfficeLiteDeliveryInspector().Inspect(
            delivery.CreateRequest(buildNumber: 5),
            "test-delivery-mixed");

        Assert.Equal(OfficeLiteDeliveryKind.Mixed, outcome.Receipt.Payload.DeliveryKind);
        Assert.Equal(OfficeLiteDeliveryTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(2, outcome.ExitCode);
    }

    [Fact]
    public void Receipt_verifier_rejects_rehashed_rule_tampering()
    {
        using var delivery = DeliveryTestRoot.Create();
        delivery.AddHyperVExport();
        var receipt = new OfficeLiteDeliveryInspector().Inspect(
            delivery.CreateRequest(buildNumber: 5),
            "test-delivery-rule-tamper").Receipt;
        var payload = receipt.Payload with { HyperVImportEligible = false };
        var tampered = receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var verification = OfficeLiteDeliveryReceiptVerifier.VerifyIntegrity(tampered);

        Assert.False(verification.Succeeded);
        Assert.Contains(
            verification.Errors,
            error => error.Contains("hyperVImportEligible", StringComparison.Ordinal));
    }

    [Fact]
    public void Current_root_verification_rejects_file_drift()
    {
        using var delivery = DeliveryTestRoot.Create();
        delivery.AddHyperVExport();
        var receipt = new OfficeLiteDeliveryInspector().Inspect(
            delivery.CreateRequest(buildNumber: 5),
            "test-delivery-drift").Receipt;
        delivery.AddFile("Virtual Machines/controller.vmcx", "changed configuration");

        var verification = OfficeLiteDeliveryReceiptVerifier.VerifyCurrentRoot(receipt);

        Assert.False(verification.Succeeded);
        Assert.False(verification.CurrentTreeVerified);
        Assert.Contains(
            verification.Errors,
            error => error.Contains("current delivery tree", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Receipt_writer_is_create_new()
    {
        using var delivery = DeliveryTestRoot.Create();
        delivery.AddHyperVExport();
        var receipt = new OfficeLiteDeliveryInspector().Inspect(
            delivery.CreateRequest(buildNumber: 5),
            "test-delivery-create-new").Receipt;
        var outputRoot = Path.Combine(Path.GetTempPath(), $"kuka-lab-receipt-{Guid.NewGuid():N}");
        var output = Path.Combine(outputRoot, "receipt.json");
        try
        {
            OfficeLiteDeliveryReceiptWriter.WriteNew(output, receipt);

            Assert.Throws<IOException>(() => OfficeLiteDeliveryReceiptWriter.WriteNew(output, receipt));
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Receipt_writer_refuses_to_mutate_delivery_root()
    {
        using var delivery = DeliveryTestRoot.Create();
        delivery.AddHyperVExport();
        var receipt = new OfficeLiteDeliveryInspector().Inspect(
            delivery.CreateRequest(buildNumber: 5),
            "test-delivery-output-boundary").Receipt;
        var output = Path.Combine(delivery.Root, "receipt.json");

        var exception = Assert.Throws<ArgumentException>(
            () => OfficeLiteDeliveryReceiptWriter.WriteNew(output, receipt));

        Assert.Contains("outside", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(output));
    }

    private sealed class DeliveryTestRoot : IDisposable
    {
        private DeliveryTestRoot(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static DeliveryTestRoot Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"kuka-lab-delivery-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            return new DeliveryTestRoot(root);
        }

        public OfficeLiteDeliveryInspectionRequest CreateRequest(int buildNumber) => new()
        {
            DeliveryRoot = Root,
            DeclaredProductVersion = "8.7.8",
            DeclaredBuildNumber = buildNumber,
            ProvenanceReference = "owner-received-kuka-delivery"
        };

        public void AddHyperVExport()
        {
            AddFile("Virtual Machines/controller.vmcx", "synthetic Hyper-V configuration");
            AddFile("Virtual Hard Disks/OfficeLite.vhdx", "synthetic Hyper-V disk");
        }

        public void AddLegacyVmware()
        {
            AddFile("KR C, V8.7.8OL_Build04.vmx", "synthetic VMware configuration");
            AddFile("KR C, V8.7.OL_Build04-cl1.vmdk", "synthetic VMware disk");
        }

        public void AddFile(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
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
