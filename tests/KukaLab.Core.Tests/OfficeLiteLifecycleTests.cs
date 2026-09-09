using KukaLab.Core;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace KukaLab.Core.Tests;

public sealed class OfficeLiteLifecycleTests
{
    [Fact]
    public async Task Production_probe_observes_self_signed_kuka_tls_endpoint()
    {
        using var rsa = RSA.Create(2048);
        var certificateRequest = new CertificateRequest(
            "CN=KUKA Roboter GmbH",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = certificateRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(5));
        using var serverCertificate = new X509Certificate2(
            certificate.Export(X509ContentType.Pfx),
            (string?)null,
            X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var cancellationToken = TestContext.Current.CancellationToken;
        var serverTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
            await ssl.AuthenticateAsServerAsync(
                new SslServerAuthenticationOptions
                {
                    ServerCertificate = serverCertificate,
                    EnabledSslProtocols = SslProtocols.Tls12,
                    ClientCertificateRequired = false,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                },
                cancellationToken);
        }, cancellationToken);

        try
        {
            var result = new OfficeLiteHostPlatform().ProbeGuest(
                "127.0.0.1",
                port,
                3000,
                DateTimeOffset.UtcNow);
            await serverTask;

            Assert.True(result.TlsConnected);
            Assert.NotNull(result.TlsIdentity);
            Assert.Contains("KUKA Roboter GmbH", result.TlsIdentity.Subject, StringComparison.Ordinal);
            Assert.True(result.TlsIdentity.SelfSigned);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public void VMware_dhcp_lease_resolves_static_vmx_mac()
    {
        var root = Path.Combine(Path.GetTempPath(), $"kuka-lab-dhcp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var vmx = Path.Combine(root, "controller.vmx");
            var leases = Path.Combine(root, "vmnetdhcp.leases");
            File.WriteAllText(vmx, "ethernet0.address = \"00:0C:29:3F:C7:D7\"");
            File.WriteAllText(
                leases,
                """
                lease 203.0.113.128 {
                    starts 3 2026/08/26 05:11:11;
                    ends 3 2026/08/26 05:41:11;
                    hardware ethernet 00:0c:29:3f:c7:d7;
                    uid 01:00:0c:29:3f:c7:d7;
                    client-hostname "PCRC-5GSOS9RP8S";
                }
                """);

            using var serviceHandle = new FileStream(
                leases,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.ReadWrite);
            var address = new OfficeLiteHostPlatform().TryResolveDhcpAddress(vmx, leases);

            Assert.Equal("203.0.113.128", address);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void VMware_dhcp_lease_selects_latest_start_for_repeated_mac_independent_of_file_order()
    {
        var root = Path.Combine(Path.GetTempPath(), $"kuka-lab-dhcp-repeated-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var vmx = Path.Combine(root, "controller.vmx");
            var leases = Path.Combine(root, "vmnetdhcp.leases");
            File.WriteAllText(vmx, "ethernet0.address = \"00:0C:29:3F:C7:D7\"");
            File.WriteAllText(
                leases,
                """
                lease 198.51.100.137 {
                    starts 6 2026/08/29 06:22:08;
                    ends 6 2026/08/29 06:52:08;
                    hardware ethernet 00:0c:29:3f:c7:d7;
                }
                lease 198.51.100.134 {
                    starts 6 2026/08/29 05:43:27;
                    ends 6 2026/08/29 06:13:27;
                    hardware ethernet 00:0c:29:3f:c7:d7;
                }
                lease 198.51.100.128 {
                    starts 6 2026/08/29 02:28:29;
                    ends 6 2026/08/29 02:28:29;
                    abandoned;
                    hardware ethernet 00:0c:29:3f:c7:d7;
                }
                """);

            var address = new OfficeLiteHostPlatform().TryResolveDhcpAddress(vmx, leases);

            Assert.Equal("198.51.100.137", address);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Production_port_probe_observes_open_tcp_listener()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var result = Assert.Single(new OfficeLiteHostPlatform().ProbeTcpPorts(
                "127.0.0.1",
                [port],
                1000,
                DateTimeOffset.UtcNow));

            Assert.Equal(port, result.Port);
            Assert.Equal(GuestTcpPortState.Open, result.State);
            Assert.Equal("TCP connection established.", result.Detail);
            Assert.True(result.DurationMilliseconds >= 0);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public void Ready_cycle_requires_kuka_tls_identity_and_verified_soft_shutdown()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(time, environment.PrimaryVmxPath) { ProbeReady = true };
        var outcome = new OfficeLiteCycleRunner(platform, time).Run(
            environment.CreateRequest(),
            "test-officelite-ready");

        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(0, outcome.ExitCode);
        Assert.True(outcome.Receipt.Payload.StartedByThisAttempt);
        Assert.True(outcome.Receipt.Payload.ControllerReady);
        Assert.True(outcome.Receipt.Payload.CleanShutdownVerified);
        Assert.Equal("203.0.113.128", outcome.Receipt.Payload.GuestEndpoint.Address);
        Assert.Equal("KUKA Roboter GmbH", outcome.Receipt.Payload.GuestEndpoint.CertificateSubject);
        Assert.Contains(outcome.Receipt.Payload.Commands, command => command.Name == "start-nogui");
        Assert.Contains(outcome.Receipt.Payload.Commands, command => command.Name == "stop-soft");
        var verification = OfficeLiteCycleReceiptVerifier.Verify(outcome.Receipt);
        Assert.True(verification.Succeeded, string.Join("; ", verification.Errors));
    }

    [Fact]
    public void Receipt_verifier_accepts_legacy_v1_cycle_without_service_diagnostics()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(time, environment.PrimaryVmxPath) { ProbeReady = true };
        var current = new OfficeLiteCycleRunner(platform, time).Run(
            environment.CreateRequest(),
            "test-officelite-v1-compatibility").Receipt;
        var receipt = current with
        {
            SchemaVersion = 1,
            PayloadSha256 = ReceiptSerialization.ComputeOfficeLiteCyclePayloadSha256(current.Payload, 1)
        };

        var json = System.Text.Json.Nodes.JsonNode.Parse(ReceiptSerialization.ToJson(receipt))!.AsObject();
        var legacyPayload = json["payload"]!.AsObject();
        legacyPayload.Remove("workVisualServices");
        legacyPayload.Remove("networkControls");
        var reloaded = ReceiptSerialization.OfficeLiteCycleFromJson(json.ToJsonString());

        Assert.True(OfficeLiteCycleReceiptVerifier.Verify(reloaded).Succeeded);
    }

    [Fact]
    public void Receipt_verifier_rejects_service_diagnostics_claimed_under_legacy_v1_schema()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(time, environment.PrimaryVmxPath) { ProbeReady = true };
        var request = environment.CreateRequest() with
        {
            DiagnoseWorkVisualServices = true,
            ServiceObservationSeconds = 1,
            ServiceProbeIntervalMilliseconds = 500,
            ServiceConnectTimeoutMilliseconds = 250
        };
        var receipt = new OfficeLiteCycleRunner(platform, time).Run(
            request,
            "test-officelite-v1-service-evidence").Receipt with
        {
            SchemaVersion = 1
        };

        var verification = OfficeLiteCycleReceiptVerifier.Verify(receipt);

        Assert.False(verification.Succeeded);
        Assert.Contains(
            verification.Errors,
            error => error.Contains("schemaVersion 1", StringComparison.Ordinal));
    }

    [Fact]
    public void Receipt_verifier_accepts_legacy_v2_service_evidence_without_network_controls()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(time, environment.PrimaryVmxPath) { ProbeReady = true };
        var request = environment.CreateRequest() with
        {
            DiagnoseWorkVisualServices = true,
            ServiceObservationSeconds = 1,
            ServiceProbeIntervalMilliseconds = 500,
            ServiceConnectTimeoutMilliseconds = 250
        };
        var current = new OfficeLiteCycleRunner(platform, time).Run(
            request,
            "test-officelite-v2-compatibility").Receipt;
        var payload = current.Payload with { NetworkControls = new GuestNetworkControlObservation() };
        var legacy = current with
        {
            SchemaVersion = 2,
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeOfficeLiteCyclePayloadSha256(payload, 2)
        };

        var json = System.Text.Json.Nodes.JsonNode.Parse(ReceiptSerialization.ToJson(legacy))!.AsObject();
        json["payload"]!.AsObject().Remove("networkControls");
        var reloaded = ReceiptSerialization.OfficeLiteCycleFromJson(json.ToJsonString());

        Assert.True(OfficeLiteCycleReceiptVerifier.Verify(reloaded).Succeeded);
    }

    [Fact]
    public void WorkVisual_service_diagnostic_records_fixed_ports_without_turning_unreachable_ports_into_failure()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(time, environment.PrimaryVmxPath) { ProbeReady = true };
        var request = environment.CreateRequest() with
        {
            DiagnoseWorkVisualServices = true,
            ServiceObservationSeconds = 1,
            ServiceProbeIntervalMilliseconds = 500,
            ServiceConnectTimeoutMilliseconds = 250
        };

        var outcome = new OfficeLiteCycleRunner(platform, time).Run(
            request,
            "test-officelite-workvisual-services-unreachable");

        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.True(outcome.Receipt.Payload.WorkVisualServices.Requested);
        Assert.True(outcome.Receipt.Payload.WorkVisualServices.ObservationCompleted);
        Assert.False(outcome.Receipt.Payload.WorkVisualServices.DeviceInfoEverOpen);
        Assert.False(outcome.Receipt.Payload.WorkVisualServices.AnyServiceEverOpen);
        Assert.NotEmpty(outcome.Receipt.Payload.WorkVisualServices.Snapshots);
        Assert.All(
            outcome.Receipt.Payload.WorkVisualServices.Snapshots,
            snapshot => Assert.Equal(
                OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts,
                snapshot.Ports.Select(port => port.Port)));
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "workvisual-service-readiness"
                && check.Status == EnvironmentCheckStatus.Warning);
        Assert.True(outcome.Receipt.Payload.CleanShutdownVerified);
        Assert.False(platform.Running);
        Assert.True(OfficeLiteCycleReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void WorkVisual_service_diagnostic_records_network_controls_and_marks_shared_timeout_ambiguous()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(time, environment.PrimaryVmxPath)
        {
            ProbeReady = true,
            OpenServicePorts = [135, 139, 443, 445, 3389]
        };
        var request = environment.CreateRequest() with
        {
            DiagnoseWorkVisualServices = true,
            ServiceObservationSeconds = 1,
            ServiceProbeIntervalMilliseconds = 500,
            ServiceConnectTimeoutMilliseconds = 250
        };

        var receipt = new OfficeLiteCycleRunner(platform, time).Run(
            request,
            "test-officelite-network-control-profile").Receipt;
        var observation = receipt.Payload.NetworkControls;

        Assert.Equal(4, receipt.SchemaVersion);
        Assert.True(observation.Requested);
        Assert.True(observation.ObservationCompleted);
        Assert.Equal(OfficeLiteServiceDiagnosticContract.NetworkControlPorts, observation.ExpectedPorts);
        Assert.Equal(OfficeLiteServiceDiagnosticContract.PositiveControlPort, observation.PositiveControlPort);
        Assert.Equal(OfficeLiteServiceDiagnosticContract.ReferencePorts, observation.ReferencePorts);
        Assert.Equal(OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts, observation.TargetPorts);
        Assert.True(observation.PositiveControlOpen);
        Assert.True(observation.AnyReferencePortOpen);
        Assert.True(observation.AnyReferencePortTimeout);
        Assert.False(observation.AnyReferencePortConnectionRefused);
        Assert.True(observation.AllTargetPortsTimeout);
        Assert.True(observation.TargetTimeoutClassificationAmbiguous);
        Assert.Equal(OfficeLiteServiceDiagnosticContract.NetworkControlPorts, observation.Ports.Select(port => port.Port));
        Assert.Contains(
            receipt.Payload.Checks,
            check => check.Id == "workvisual-network-control" && check.Status == EnvironmentCheckStatus.Passed);
        Assert.Contains(
            receipt.Payload.UnsupportedGaps,
            gap => gap.Contains("unrelated reference ports", StringComparison.Ordinal));
        Assert.True(OfficeLiteCycleReceiptVerifier.Verify(receipt).Succeeded);
    }

    [Fact]
    public void Receipt_verifier_rejects_rehashed_network_control_claim_tampering()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(time, environment.PrimaryVmxPath)
        {
            ProbeReady = true,
            OpenServicePorts = [443]
        };
        var request = environment.CreateRequest() with
        {
            DiagnoseWorkVisualServices = true,
            ServiceObservationSeconds = 1,
            ServiceProbeIntervalMilliseconds = 500,
            ServiceConnectTimeoutMilliseconds = 250
        };
        var receipt = new OfficeLiteCycleRunner(platform, time).Run(
            request,
            "test-officelite-network-control-tamper").Receipt;
        var profile = receipt.Payload.NetworkControls with
        {
            TargetTimeoutClassificationAmbiguous = false
        };
        var payload = receipt.Payload with { NetworkControls = profile };
        var tampered = receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var verification = OfficeLiteCycleReceiptVerifier.Verify(tampered);

        Assert.False(verification.Succeeded);
        Assert.Contains(
            verification.Errors,
            error => error.Contains("ambiguous", StringComparison.OrdinalIgnoreCase));

        var legacy = tampered with { SchemaVersion = 2 };
        var legacyVerification = OfficeLiteCycleReceiptVerifier.Verify(legacy);

        Assert.False(legacyVerification.Succeeded);
        Assert.Contains(
            legacyVerification.Errors,
            error => error.Contains("schemaVersion 2", StringComparison.Ordinal));
    }

    [Fact]
    public void WorkVisual_service_diagnostic_stops_after_DeviceInfo_port_opens()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(time, environment.PrimaryVmxPath)
        {
            ProbeReady = true,
            OpenServicePorts = [OfficeLiteServiceDiagnosticContract.DeviceInfoPort]
        };
        var request = environment.CreateRequest() with
        {
            DiagnoseWorkVisualServices = true,
            ServiceObservationSeconds = 5,
            ServiceProbeIntervalMilliseconds = 500,
            ServiceConnectTimeoutMilliseconds = 250
        };

        var outcome = new OfficeLiteCycleRunner(platform, time).Run(
            request,
            "test-officelite-workvisual-deviceinfo-open");

        Assert.True(outcome.Receipt.Payload.WorkVisualServices.DeviceInfoEverOpen);
        Assert.True(outcome.Receipt.Payload.WorkVisualServices.AnyServiceEverOpen);
        Assert.Single(outcome.Receipt.Payload.WorkVisualServices.Snapshots);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "workvisual-service-readiness"
                && check.Status == EnvironmentCheckStatus.Passed);
        Assert.True(OfficeLiteCycleReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Receipt_verifier_rejects_rehashed_false_DeviceInfo_open_claim()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(time, environment.PrimaryVmxPath) { ProbeReady = true };
        var request = environment.CreateRequest() with
        {
            DiagnoseWorkVisualServices = true,
            ServiceObservationSeconds = 1,
            ServiceProbeIntervalMilliseconds = 500,
            ServiceConnectTimeoutMilliseconds = 250
        };
        var receipt = new OfficeLiteCycleRunner(platform, time).Run(
            request,
            "test-officelite-workvisual-service-tamper").Receipt;
        var serviceObservation = receipt.Payload.WorkVisualServices with { DeviceInfoEverOpen = true };
        var payload = receipt.Payload with { WorkVisualServices = serviceObservation };
        var tampered = receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var verification = OfficeLiteCycleReceiptVerifier.Verify(tampered);

        Assert.False(verification.Succeeded);
        Assert.Contains(
            verification.Errors,
            error => error.Contains("DeviceInfo", StringComparison.Ordinal));

        var shortenedObservation = receipt.Payload.WorkVisualServices with
        {
            ActualObservationMilliseconds = 1,
            Snapshots = [receipt.Payload.WorkVisualServices.Snapshots[0] with { Sequence = 1 }]
        };
        var shortenedPayload = receipt.Payload with { WorkVisualServices = shortenedObservation };
        var shortenedReceipt = receipt with
        {
            Payload = shortenedPayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(shortenedPayload)
        };

        var shortenedVerification = OfficeLiteCycleReceiptVerifier.Verify(shortenedReceipt);

        Assert.False(shortenedVerification.Succeeded);
        Assert.Contains(
            shortenedVerification.Errors,
            error => error.Contains("full requested observation window", StringComparison.Ordinal));

        var nullPortsObservation = receipt.Payload.WorkVisualServices with
        {
            Snapshots = [receipt.Payload.WorkVisualServices.Snapshots[0] with { Ports = null! }]
        };
        var nullPortsPayload = receipt.Payload with { WorkVisualServices = nullPortsObservation };
        var nullPortsReceipt = receipt with
        {
            Payload = nullPortsPayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(nullPortsPayload)
        };

        var nullPortsVerification = OfficeLiteCycleReceiptVerifier.Verify(nullPortsReceipt);

        Assert.False(nullPortsVerification.Succeeded);
        Assert.Contains(
            nullPortsVerification.Errors,
            error => error.Contains("fixed ports", StringComparison.Ordinal));
    }

    [Fact]
    public void WorkVisual_service_probe_failure_is_failed_evidence_with_verified_soft_shutdown()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(time, environment.PrimaryVmxPath)
        {
            ProbeReady = true,
            ThrowServiceProbe = true
        };
        var request = environment.CreateRequest() with
        {
            DiagnoseWorkVisualServices = true,
            ServiceObservationSeconds = 1,
            ServiceProbeIntervalMilliseconds = 500,
            ServiceConnectTimeoutMilliseconds = 250
        };

        var outcome = new OfficeLiteCycleRunner(platform, time).Run(
            request,
            "test-officelite-workvisual-service-adapter-failure");

        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.False(outcome.Receipt.Payload.WorkVisualServices.ObservationCompleted);
        Assert.True(outcome.Receipt.Payload.CleanShutdownVerified);
        Assert.False(platform.Running);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "workvisual-service-readiness"
                && check.Status == EnvironmentCheckStatus.Failed);
        Assert.True(OfficeLiteCycleReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Already_running_vm_is_blocked_without_claiming_or_stopping_it()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(time, environment.PrimaryVmxPath) { Running = true, ProbeReady = true };
        var outcome = new OfficeLiteCycleRunner(platform, time).Run(
            environment.CreateRequest(),
            "test-officelite-already-running");

        Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(3, outcome.ExitCode);
        Assert.False(outcome.Receipt.Payload.StartedByThisAttempt);
        Assert.True(platform.Running);
        Assert.DoesNotContain(outcome.Receipt.Payload.Commands, command => command.Name == "start-nogui");
        Assert.DoesNotContain(outcome.Receipt.Payload.Commands, command => command.Name == "stop-soft");
        Assert.True(OfficeLiteCycleReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Explicitly_authorized_retained_lab_vm_is_adopted_without_restart_and_soft_stopped()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(time, environment.PrimaryVmxPath) { Running = true, ProbeReady = true };
        var request = environment.CreateRequest() with
        {
            AdoptRunningLabVm = true,
            RunningVmOwnershipReference = "TASK-20260829-KUKA-EXACT-C01-NATIVE-LOOP"
        };

        var outcome = new OfficeLiteCycleRunner(platform, time).Run(
            request,
            "test-officelite-adopt-retained-lab-vm");

        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.False(outcome.Receipt.Payload.StartedByThisAttempt);
        Assert.True(outcome.Receipt.Payload.AdoptedRunningLabVm);
        Assert.Equal(
            "TASK-20260829-KUKA-EXACT-C01-NATIVE-LOOP",
            outcome.Receipt.Payload.RunningVmOwnershipReference);
        Assert.True(outcome.Receipt.Payload.CleanShutdownVerified);
        Assert.False(platform.Running);
        Assert.DoesNotContain(outcome.Receipt.Payload.Commands, command => command.Name == "start-nogui");
        Assert.Contains(outcome.Receipt.Payload.Commands, command => command.Name == "list-after-adopt");
        Assert.Contains(outcome.Receipt.Payload.Commands, command => command.Name == "stop-soft");
        Assert.True(OfficeLiteCycleReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Retained_lab_vm_adoption_refuses_to_start_a_missing_session()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(time, environment.PrimaryVmxPath) { Running = false, ProbeReady = true };
        var request = environment.CreateRequest() with
        {
            AdoptRunningLabVm = true,
            RunningVmOwnershipReference = "TASK-20260829-KUKA-EXACT-C01-NATIVE-LOOP"
        };

        var outcome = new OfficeLiteCycleRunner(platform, time).Run(
            request,
            "test-officelite-adopt-missing-retained-lab-vm");

        Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
        Assert.False(outcome.Receipt.Payload.StartedByThisAttempt);
        Assert.False(outcome.Receipt.Payload.AdoptedRunningLabVm);
        Assert.DoesNotContain(outcome.Receipt.Payload.Commands, command => command.Name == "start-nogui");
        Assert.DoesNotContain(outcome.Receipt.Payload.Commands, command => command.Name == "stop-soft");
        Assert.True(OfficeLiteCycleReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Readiness_timeout_fails_but_still_soft_stops_owned_vm()
    {
        using var environment = TestEnvironment.Create(readinessTimeoutSeconds: 1);
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(time, environment.PrimaryVmxPath) { ProbeReady = false };
        var outcome = new OfficeLiteCycleRunner(platform, time).Run(
            environment.CreateRequest(),
            "test-officelite-readiness-timeout");

        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(2, outcome.ExitCode);
        Assert.False(outcome.Receipt.Payload.ControllerReady);
        Assert.True(outcome.Receipt.Payload.CleanShutdownVerified);
        Assert.False(platform.Running);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "kuka-controller-https" && check.Status == EnvironmentCheckStatus.Failed);
        Assert.True(OfficeLiteCycleReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Receipt_verifier_rejects_rehashed_false_ready_claim()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var platform = new FakePlatform(time, environment.PrimaryVmxPath) { ProbeReady = true };
        var receipt = new OfficeLiteCycleRunner(platform, time).Run(
            environment.CreateRequest(),
            "test-officelite-tamper").Receipt;
        var payload = receipt.Payload with { CleanShutdownVerified = false };
        var tampered = receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var verification = OfficeLiteCycleReceiptVerifier.Verify(tampered);

        Assert.False(verification.Succeeded);
        Assert.Contains(
            verification.Errors,
            error => error.Contains("verified clean shutdown", StringComparison.Ordinal));
    }

    private sealed class FakePlatform(ManualTimeProvider time, string vmxPath) : IOfficeLiteHostPlatform
    {
        public bool Running { get; set; }

        public bool ProbeReady { get; set; }

        public HashSet<int> OpenServicePorts { get; init; } = [];

        public bool ThrowServiceProbe { get; init; }

        public VmrunExecutionResult RunVmrun(
            string vmrunPath,
            IReadOnlyList<string> arguments,
            TimeSpan timeout)
        {
            var command = arguments.Count >= 3 ? arguments[2] : string.Empty;
            return command switch
            {
                "list" => Result(Running
                    ? $"Total running VMs: 1{Environment.NewLine}{vmxPath}"
                    : "Total running VMs: 0"),
                "start" => Start(),
                "stop" => Stop(),
                _ => new VmrunExecutionResult(1, false, 1, string.Empty, "unsupported")
            };
        }

        public string? TryResolveDhcpAddress(string vmxPath, string dhcpLeasePath) =>
            "203.0.113.128";

        public GuestProbeResult ProbeGuest(
            string? address,
            int port,
            int timeoutMilliseconds,
            DateTimeOffset observedAtUtc) =>
            ProbeReady
                ? new GuestProbeResult(
                    0,
                    observedAtUtc,
                    address,
                    true,
                    true,
                    "ready",
                    new GuestTlsIdentity(
                        address!,
                        "KUKA Roboter GmbH",
                        "KUKA Roboter GmbH",
                        "4DCFB182A3EF5E53FC908D7999273595E02BC430",
                        "Tls12",
                        true))
                : new GuestProbeResult(0, observedAtUtc, address, true, false, "not-ready", null);

        public IReadOnlyList<GuestTcpPortProbeResult> ProbeTcpPorts(
            string? address,
            IReadOnlyList<int> ports,
            int timeoutMilliseconds,
            DateTimeOffset observedAtUtc)
        {
            if (ThrowServiceProbe)
            {
                throw new InvalidOperationException("synthetic service probe failure");
            }

            return ports.Select(port => new GuestTcpPortProbeResult(
                port,
                observedAtUtc,
                OpenServicePorts.Contains(port) ? GuestTcpPortState.Open : GuestTcpPortState.Timeout,
                OpenServicePorts.Contains(port) ? "Open" : "Timeout",
                1)).ToList();
        }

        public void Delay(TimeSpan duration) => time.Advance(duration);

        private VmrunExecutionResult Start()
        {
            Running = true;
            return Result(string.Empty);
        }

        private VmrunExecutionResult Stop()
        {
            Running = false;
            return Result(string.Empty);
        }

        private static VmrunExecutionResult Result(string standardOutput) =>
            new(0, false, 1, standardOutput, string.Empty);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 8, 26, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }

    private sealed class TestEnvironment : IDisposable
    {
        private const string RelativeVmx =
            "OfficeLite-Work/8.7.8-build04/runs/primary/KR C, V8.7.8OL_Build04.vmx";
        private readonly int _readinessTimeoutSeconds;

        private TestEnvironment(string root, int readinessTimeoutSeconds)
        {
            Root = root;
            _readinessTimeoutSeconds = readinessTimeoutSeconds;
            PrimaryVmxPath = Path.GetFullPath(Path.Combine(
                root,
                RelativeVmx.Replace('/', Path.DirectorySeparatorChar)));
        }

        public string PrimaryVmxPath { get; }

        private string Root { get; }

        public static TestEnvironment Create(int readinessTimeoutSeconds = 5)
        {
            var root = Path.Combine(Path.GetTempPath(), $"kuka-lab-officelite-{Guid.NewGuid():N}");
            var vmware = Path.Combine(root, "VMware");
            Directory.CreateDirectory(vmware);
            File.WriteAllText(Path.Combine(vmware, "vmrun.exe"), "synthetic vmrun");
            var primary = Path.Combine(root, "OfficeLite-Work", "8.7.8-build04", "runs", "primary");
            Directory.CreateDirectory(primary);
            File.WriteAllLines(
                Path.Combine(primary, "KR C, V8.7.8OL_Build04.vmx"),
                [
                    "ethernet0.address = \"00:0C:29:3F:C7:D7\"",
                    "isolation.tools.hgfs.disable = \"TRUE\"",
                    "sharedFolder0.present = \"FALSE\"",
                    "sharedFolder0.enabled = \"FALSE\"",
                    "sharedFolder0.readAccess = \"FALSE\"",
                    "sharedFolder0.writeAccess = \"FALSE\"",
                    "sharedFolder.maxNum = \"0\"",
                    "hgfs.mapRootShare = \"FALSE\""
                ]);
            return new TestEnvironment(root, readinessTimeoutSeconds);
        }

        public OfficeLiteCycleRequest CreateRequest() =>
            OfficeLiteCycleRequest.CreateDefault(
                Root,
                readinessTimeoutSeconds: _readinessTimeoutSeconds) with
            {
                ProbeIntervalMilliseconds = 500,
                ShutdownTimeoutSeconds = 2
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
