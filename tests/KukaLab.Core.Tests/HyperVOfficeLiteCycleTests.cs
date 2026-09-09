using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class HyperVOfficeLiteCycleTests
{
    [Fact]
    public void Default_profile_keeps_fixed_parent_and_differencing_child_in_vhd_format()
    {
        var request = HyperVOfficeLiteCycleRequest.CreateDefault(
            @"D:\Lab\OfficeLite-fixed.vhd",
            new string('A', 64),
            @"D:\Lab\runs\WP3V",
            "KukaLab-OfficeLite-878-WP3V",
            "Default Switch",
            allowHostChange: false,
            authorizationReference: null);

        Assert.Equal(".vhd", Path.GetExtension(request.TemplateVhdPath), ignoreCase: true);
        Assert.Equal(".vhd", Path.GetExtension(request.DifferencingDiskPath), ignoreCase: true);
        Assert.Equal(HyperVOfficeLiteCycleContract.Generation, request.Generation);
    }

    [Fact]
    public void Authorized_cycle_uses_differencing_disk_reaches_guest_tls_and_soft_stops()
    {
        using var environment = new TestEnvironment();
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(environment.Request, time)
        {
            NetworkReadyAfterObservation = 2
        };

        var outcome = new HyperVOfficeLiteCycleRunner(platform, time).Run(
            environment.Request,
            "test-hyperv-ready");

        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(HyperVOfficeLiteBootClassification.GuestNetworkReady, outcome.Receipt.Payload.BootClassification);
        Assert.True(outcome.Receipt.Payload.TemplateUnchanged);
        Assert.True(outcome.Receipt.Payload.GuestTlsReady);
        Assert.True(outcome.Receipt.Payload.CleanShutdownVerified);
        Assert.True(outcome.Receipt.Payload.VmRetainedStopped);
        Assert.True(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.False(outcome.Receipt.Payload.ControllerReady);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.NativeKssStatus);
        Assert.Equal(1, platform.CreateCalls);
        Assert.Equal(1, platform.StartCalls);
        Assert.Equal(1, platform.StopCalls);
        Assert.Contains(outcome.Receipt.Payload.SideEffects, effect => effect.StartsWith("CreateDifferencingDisk:", StringComparison.Ordinal));
        Assert.True(HyperVOfficeLiteCycleReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Missing_host_change_authorization_blocks_before_mutation()
    {
        using var environment = new TestEnvironment(allowHostChange: false, authorizationReference: null);
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(environment.Request, time);

        var outcome = new HyperVOfficeLiteCycleRunner(platform, time).Run(
            environment.Request,
            "test-hyperv-no-auth");

        Assert.Equal(3, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(HyperVOfficeLiteBootClassification.HypervisorBlocked, outcome.Receipt.Payload.BootClassification);
        Assert.Equal(0, platform.CreateCalls);
        Assert.Equal(0, platform.StartCalls);
        Assert.Empty(outcome.Receipt.Payload.SideEffects);
        Assert.DoesNotContain(
            outcome.Receipt.Payload.UnsupportedGaps,
            gap => gap.Contains("created VM is retained", StringComparison.Ordinal));
        Assert.True(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.True(HyperVOfficeLiteCycleReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Inaccessible_hyperv_does_not_claim_vm_name_is_available()
    {
        using var environment = new TestEnvironment(allowHostChange: false, authorizationReference: null);
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(environment.Request, time)
        {
            PreflightState = new HyperVPreflightState(false, false, false, false, false, false, string.Empty, string.Empty)
        };

        var outcome = new HyperVOfficeLiteCycleRunner(platform, time).Run(
            environment.Request,
            "test-hyperv-inaccessible");

        Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "hyperv-vm-exclusive"
                && check.Status == EnvironmentCheckStatus.Blocked
                && check.Detail.Contains("cannot be established", StringComparison.Ordinal));
        Assert.Equal(0, platform.CreateCalls);
        Assert.True(HyperVOfficeLiteCycleReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Inaccessible_hyperv_preserves_the_exact_access_error_in_the_receipt()
    {
        using var environment = new TestEnvironment(allowHostChange: false, authorizationReference: null);
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(environment.Request, time)
        {
            PreflightState = new HyperVPreflightState(false, true, false, false, false, false, string.Empty, string.Empty)
            {
                IsElevated = true,
                Identity = @"GENSHINIMPACT\PUBLIC_TEST_USER",
                AccessError = "Get-VM access denied"
            }
        };

        var outcome = new HyperVOfficeLiteCycleRunner(platform, time).Run(
            environment.Request,
            "test-hyperv-access-detail");

        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "hyperv-access"
                && check.Status == EnvironmentCheckStatus.Blocked
                && check.Detail.Contains("Get-VM access denied", StringComparison.Ordinal)
                && check.Detail.Contains("elevated=True", StringComparison.Ordinal));
    }

    [Fact]
    public void PowerShell_child_process_decodes_structural_json_as_utf8()
    {
        var startInfo = HyperVOfficeLitePlatform.CreatePowerShellStartInfo("encoded-command");

        Assert.Equal(Encoding.UTF8.WebName, startInfo.StandardOutputEncoding?.WebName);
        Assert.Equal(Encoding.UTF8.WebName, startInfo.StandardErrorEncoding?.WebName);
    }

    [Fact]
    public void Encoded_PowerShell_string_is_one_parameter_in_argument_mode()
    {
        const string expected = "KUKA-OfficeLite 8.7.8 中文";
        var expression = HyperVOfficeLitePlatform.PowerShellString(expected);
        var script = "[Console]::OutputEncoding=[System.Text.UTF8Encoding]::new($false);"
            + "function Write-Name { param([string]$Name) $Name };"
            + $"Write-Name -Name {expression}";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var startInfo = HyperVOfficeLitePlatform.CreatePowerShellStartInfo(encoded);

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.Equal(0, process.ExitCode);
        Assert.Equal(string.Empty, error.Trim());
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void Soft_stop_script_uses_guest_shutdown_semantics_without_hard_power_flags()
    {
        using var environment = new TestEnvironment();

        var script = HyperVOfficeLitePlatform.CreateSoftStopScript(environment.Request);

        Assert.Contains("Stop-VM -Name $vmName -ErrorAction Stop", script, StringComparison.Ordinal);
        Assert.DoesNotContain("-Shutdown", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("-TurnOff", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("-Force", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Existing_vm_blocks_without_taking_ownership()
    {
        using var environment = new TestEnvironment();
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(environment.Request, time)
        {
            PreflightState = new HyperVPreflightState(true, true, true, true, true, true, "VHD", "Fixed")
        };

        var outcome = new HyperVOfficeLiteCycleRunner(platform, time).Run(
            environment.Request,
            "test-hyperv-existing");

        Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
        Assert.Contains(outcome.Receipt.Payload.Checks, check => check.Id == "hyperv-vm-exclusive" && check.Status == EnvironmentCheckStatus.Blocked);
        Assert.Equal(0, platform.CreateCalls);
        Assert.Equal(0, platform.StopCalls);
        Assert.True(HyperVOfficeLiteCycleReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Hyperv_rejects_a_parent_that_is_not_a_fixed_vhd_before_mutation()
    {
        using var environment = new TestEnvironment();
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(environment.Request, time)
        {
            PreflightState = new HyperVPreflightState(true, true, true, true, false, true, "VHDX", "Dynamic")
        };

        var outcome = new HyperVOfficeLiteCycleRunner(platform, time).Run(
            environment.Request,
            "test-hyperv-parent-profile");

        Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "hyperv-parent-profile" && check.Status == EnvironmentCheckStatus.Blocked);
        Assert.Equal(0, platform.CreateCalls);
        Assert.True(HyperVOfficeLiteCycleReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Vm_create_failure_is_failed_and_never_claimed_reusable()
    {
        using var environment = new TestEnvironment();
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(environment.Request, time)
        {
            CreateSucceeds = false
        };

        var outcome = new HyperVOfficeLiteCycleRunner(platform, time).Run(
            environment.Request,
            "test-hyperv-create-failure");

        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.False(outcome.Receipt.Payload.VmCreatedByThisAttempt);
        Assert.False(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.Equal(1, platform.CreateCalls);
        Assert.True(HyperVOfficeLiteCycleReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Running_guest_without_network_times_out_but_still_soft_stops()
    {
        using var environment = new TestEnvironment(readinessTimeoutSeconds: 1);
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(environment.Request, time)
        {
            NetworkReadyAfterObservation = int.MaxValue
        };

        var outcome = new HyperVOfficeLiteCycleRunner(platform, time).Run(
            environment.Request with { ProbeIntervalMilliseconds = 100 },
            "test-hyperv-timeout");

        Assert.Equal(3, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(HyperVOfficeLiteBootClassification.GuestRunningNoNetwork, outcome.Receipt.Payload.BootClassification);
        Assert.True(outcome.Receipt.Payload.CleanShutdownVerified);
        Assert.True(outcome.Receipt.Payload.TemplateUnchanged);
        Assert.True(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.Equal(1, platform.StopCalls);
        Assert.True(HyperVOfficeLiteCycleReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Soft_shutdown_failure_is_failed_and_not_reusable()
    {
        using var environment = new TestEnvironment();
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(environment.Request, time)
        {
            NetworkReadyAfterObservation = 1,
            StopSucceeds = false
        };

        var outcome = new HyperVOfficeLiteCycleRunner(platform, time).Run(
            environment.Request,
            "test-hyperv-stop-fail");

        Assert.Equal(2, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.False(outcome.Receipt.Payload.CleanShutdownVerified);
        Assert.False(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.True(HyperVOfficeLiteCycleReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Verifier_rejects_native_kss_claim_even_when_payload_is_rehashed()
    {
        using var environment = new TestEnvironment();
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(environment.Request, time)
        {
            NetworkReadyAfterObservation = 1
        };
        var receipt = new HyperVOfficeLiteCycleRunner(platform, time).Run(
            environment.Request,
            "test-hyperv-tamper").Receipt;
        var payload = receipt.Payload with { ControllerReady = true };
        var tampered = receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var verification = HyperVOfficeLiteCycleReceiptVerifier.Verify(tampered);

        Assert.False(verification.Succeeded);
        Assert.Contains(verification.Errors, error => error.Contains("cannot claim native KSS", StringComparison.Ordinal));
    }

    [Fact]
    public void Receipt_writer_is_create_new_and_readback_verifies()
    {
        using var environment = new TestEnvironment();
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(environment.Request, time)
        {
            NetworkReadyAfterObservation = 1
        };
        var receipt = new HyperVOfficeLiteCycleRunner(platform, time).Run(
            environment.Request,
            "test-hyperv-writer").Receipt;
        var path = Path.Combine(environment.Root, "hyperv-cycle.receipt.json");

        HyperVOfficeLiteCycleReceiptWriter.WriteNew(path, receipt);
        var reloaded = ReceiptSerialization.HyperVOfficeLiteCycleFromJson(File.ReadAllText(path));

        Assert.True(HyperVOfficeLiteCycleReceiptVerifier.Verify(reloaded).Succeeded);
        Assert.Throws<IOException>(() => HyperVOfficeLiteCycleReceiptWriter.WriteNew(path, receipt));
    }

    private sealed class TestEnvironment : IDisposable
    {
        public TestEnvironment(
            bool allowHostChange = true,
            string? authorizationReference = "AUTH-TEST-HYPERV",
            int readinessTimeoutSeconds = 5)
        {
            Root = Path.Combine(Path.GetTempPath(), "kuka-lab-hyperv-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            var template = Path.Combine(Root, "OfficeLite-8.7.8-fixed.vhd");
            File.WriteAllBytes(template, "test-fixed-vhd"u8.ToArray());
            var sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(template)));
            Request = HyperVOfficeLiteCycleRequest.CreateDefault(
                template,
                sha256,
                Path.Combine(Root, "vm"),
                "KUKA-OfficeLite-Test",
                "Default Switch",
                allowHostChange,
                authorizationReference,
                readinessTimeoutSeconds) with
            {
                ProbeIntervalMilliseconds = 100,
                ProbeTimeoutMilliseconds = 100,
                PowerShellTimeoutSeconds = 5,
                ShutdownTimeoutSeconds = 5
            };
        }

        public string Root { get; }

        public HyperVOfficeLiteCycleRequest Request { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 8, 27, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }

    private sealed class FakePlatform(
        HyperVOfficeLiteCycleRequest request,
        ManualTimeProvider time) : IHyperVOfficeLitePlatform
    {
        public HyperVPreflightState PreflightState { get; set; } = new(true, true, true, true, false, true, "VHD", "Fixed");

        public int NetworkReadyAfterObservation { get; set; } = int.MaxValue;

        public bool StopSucceeds { get; set; } = true;

        public bool CreateSucceeds { get; set; } = true;

        public int CreateCalls { get; private set; }

        public int StartCalls { get; private set; }

        public int StopCalls { get; private set; }

        private int ObserveCalls { get; set; }

        private bool Stopped { get; set; }

        public HyperVPlatformResult<HyperVPreflightState> Preflight(HyperVOfficeLiteCycleRequest ignored) =>
            new(PreflightState, Success("hyperv-preflight"));

        public HyperVCommandResult CreateVm(HyperVOfficeLiteCycleRequest ignored)
        {
            CreateCalls++;
            if (!CreateSucceeds)
            {
                return new HyperVCommandResult("hyperv-create-vm", 1, false, 1, string.Empty, "create failed");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(request.DifferencingDiskPath)!);
            File.WriteAllBytes(request.DifferencingDiskPath, "test-differencing-disk"u8.ToArray());
            return Success("hyperv-create-vm");
        }

        public HyperVCommandResult StartVm(HyperVOfficeLiteCycleRequest ignored)
        {
            StartCalls++;
            return Success("hyperv-start-vm");
        }

        public HyperVPlatformResult<HyperVVmSnapshot> ObserveVm(
            HyperVOfficeLiteCycleRequest ignored,
            DateTimeOffset observedAtUtc)
        {
            ObserveCalls++;
            var ready = ObserveCalls >= NetworkReadyAfterObservation;
            return new HyperVPlatformResult<HyperVVmSnapshot>(
                new HyperVVmSnapshot
                {
                    ObservedAtUtc = observedAtUtc,
                    State = Stopped ? "Off" : "Running",
                    UptimeMilliseconds = ObserveCalls * 1000L,
                    Heartbeat = Stopped ? "No Contact" : "OK",
                    IpAddresses = ready && !Stopped ? ["203.0.113.128"] : []
                },
                Success("hyperv-observe-vm"));
        }

        public GuestProbeResult ProbeGuest(
            string? address,
            int port,
            int timeoutMilliseconds,
            DateTimeOffset observedAtUtc)
        {
            var connected = string.Equals(address, "203.0.113.128", StringComparison.Ordinal);
            return new GuestProbeResult(
                0,
                observedAtUtc,
                address,
                connected,
                connected,
                connected ? "TLS ready." : "No guest address.",
                connected
                    ? new GuestTlsIdentity(
                        "OFFICELITE",
                        "CN=KUKA",
                        "CN=KUKA",
                        new string('A', 40),
                        "Tls12",
                        true)
                    : null);
        }

        public HyperVCommandResult StopVmSoft(HyperVOfficeLiteCycleRequest ignored)
        {
            StopCalls++;
            if (!StopSucceeds)
            {
                return new HyperVCommandResult("hyperv-stop-vm-soft", 1, false, 1, string.Empty, "soft stop failed");
            }

            Stopped = true;
            return Success("hyperv-stop-vm-soft");
        }

        public void Delay(TimeSpan duration) => time.Advance(duration);

        private static HyperVCommandResult Success(string name) =>
            new(name, 0, false, 1, "{}", string.Empty);
    }
}
