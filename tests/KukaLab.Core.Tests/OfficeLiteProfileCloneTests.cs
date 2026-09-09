using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class OfficeLiteProfileCloneTests
{
    [Fact]
    public void Existing_target_blocks_before_any_vmware_command()
    {
        using var environment = TestEnvironment.Create(createTarget: true);
        var platform = new FakePlatform();
        var outcome = new OfficeLiteProfileCloneRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(),
            "profile-clone-existing-target");

        Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
        Assert.Empty(platform.Commands);
        Assert.False(outcome.Receipt.Payload.TargetCreated);
        Assert.Contains(outcome.Receipt.Payload.Checks, check => check.Id == "create-new-target");
    }

    [Fact]
    public void Full_clone_preserves_source_and_creates_verified_snapshot()
    {
        using var environment = TestEnvironment.Create();
        var platform = new FakePlatform();
        var outcome = new OfficeLiteProfileCloneRunner(platform, TimeProvider.System).Run(
            environment.CreateRequest(),
            "profile-clone-ready");

        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.True(outcome.Receipt.Payload.SourceUnchanged);
        Assert.True(outcome.Receipt.Payload.TargetCreated);
        Assert.True(outcome.Receipt.Payload.SnapshotCreated);
        Assert.True(outcome.Receipt.Payload.TargetHardeningVerified);
        Assert.True(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.Equal(["list", "clone", "snapshot", "listSnapshots", "list"], platform.Commands);
        Assert.True(File.Exists(environment.TargetVmxPath));

        var verification = OfficeLiteProfileCloneReceiptVerifier.Verify(outcome.Receipt);
        Assert.True(verification.Succeeded, string.Join(Environment.NewLine, verification.Errors));
    }

    [Fact]
    public void Verifier_rejects_rehashed_receipt_with_relaxed_limitations()
    {
        using var environment = TestEnvironment.Create();
        var outcome = new OfficeLiteProfileCloneRunner(new FakePlatform(), TimeProvider.System).Run(
            environment.CreateRequest(),
            "profile-clone-tamper");
        var tamperedPayload = outcome.Receipt.Payload with
        {
            UnsupportedGaps = ["Everything is proven."]
        };
        var tampered = outcome.Receipt with
        {
            Payload = tamperedPayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(tamperedPayload)
        };

        var verification = OfficeLiteProfileCloneReceiptVerifier.Verify(tampered);

        Assert.False(verification.Succeeded);
        Assert.Contains(verification.Errors, error => error.Contains("limitations", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakePlatform : IOfficeLiteHostPlatform
    {
        public List<string> Commands { get; } = [];

        public VmrunExecutionResult RunVmrun(
            string vmrunPath,
            IReadOnlyList<string> arguments,
            TimeSpan timeout)
        {
            var command = arguments.Count >= 3 ? arguments[2] : string.Empty;
            Commands.Add(command);
            switch (command)
            {
                case "list":
                    return Result("Total running VMs: 0");
                case "clone":
                    var sourceVmx = arguments[3];
                    var targetVmx = arguments[4];
                    Directory.CreateDirectory(Path.GetDirectoryName(targetVmx)!);
                    File.Copy(sourceVmx, targetVmx);
                    File.WriteAllText(Path.ChangeExtension(targetVmx, ".vmdk"), "isolated clone disk");
                    return Result(string.Empty);
                case "snapshot":
                    return Result(string.Empty);
                case "listSnapshots":
                    return Result($"Total snapshots: 1{Environment.NewLine}{OfficeLiteProfileCloneContract.DefaultSnapshotName}");
                default:
                    return new VmrunExecutionResult(1, false, 1, string.Empty, "unsupported");
            }
        }

        public string? TryResolveDhcpAddress(string vmxPath, string dhcpLeasePath) => null;

        public GuestProbeResult ProbeGuest(
            string? address,
            int port,
            int timeoutMilliseconds,
            DateTimeOffset observedAtUtc) =>
            throw new NotSupportedException();

        public IReadOnlyList<GuestTcpPortProbeResult> ProbeTcpPorts(
            string? address,
            IReadOnlyList<int> ports,
            int timeoutMilliseconds,
            DateTimeOffset observedAtUtc) =>
            throw new NotSupportedException();

        public void Delay(TimeSpan duration) => throw new NotSupportedException();

        private static VmrunExecutionResult Result(string standardOutput) =>
            new(0, false, 1, standardOutput, string.Empty);
    }

    private sealed class TestEnvironment : IDisposable
    {
        private TestEnvironment(string root)
        {
            Root = root;
            AssetRoot = Path.Combine(root, "KUKA Simulation");
            VmrunPath = Path.Combine(AssetRoot, "VMware", "vmrun.exe");
            SourceVmxPath = Path.Combine(
                AssetRoot,
                "OfficeLite-Work",
                "8.7.8-build04",
                "runs",
                "primary",
                "KR C, V8.7.8OL_Build04.vmx");
            TargetVmxPath = Path.Combine(
                AssetRoot,
                "OfficeLite-Work",
                "8.7.8-build04",
                "runs",
                "kr210-c01-item9",
                "KR210-C01-Item9.vmx");
        }

        public string AssetRoot { get; }

        public string VmrunPath { get; }

        public string SourceVmxPath { get; }

        public string TargetVmxPath { get; }

        private string Root { get; }

        public static TestEnvironment Create(bool createTarget = false)
        {
            var environment = new TestEnvironment(
                Path.Combine(Path.GetTempPath(), $"kuka-lab-profile-clone-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(Path.GetDirectoryName(environment.VmrunPath)!);
            File.WriteAllText(environment.VmrunPath, "synthetic vmrun");
            Directory.CreateDirectory(Path.GetDirectoryName(environment.SourceVmxPath)!);
            File.WriteAllLines(environment.SourceVmxPath,
            [
                "config.version = \"8\"",
                "isolation.tools.hgfs.disable = \"TRUE\"",
                "sharedFolder0.present = \"FALSE\"",
                "sharedFolder0.enabled = \"FALSE\"",
                "sharedFolder0.readAccess = \"FALSE\"",
                "sharedFolder0.writeAccess = \"FALSE\"",
                "sharedFolder.maxNum = \"0\"",
                "hgfs.mapRootShare = \"FALSE\""
            ]);
            File.WriteAllText(Path.ChangeExtension(environment.SourceVmxPath, ".vmdk"), "primary disk");
            if (createTarget)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(environment.TargetVmxPath)!);
            }

            return environment;
        }

        public OfficeLiteProfileCloneRequest CreateRequest() =>
            OfficeLiteProfileCloneRequest.CreateDefault(
                AssetRoot,
                TargetVmxPath,
                "KLAB KR210 C01 Item9",
                "TASK-20260826-KUKA-LAB-FOUNDATION-ITEM9-PROFILE-CLONE",
                VmrunPath,
                SourceVmxPath,
                OfficeLiteProfileCloneContract.DefaultSnapshotName,
                60);

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
