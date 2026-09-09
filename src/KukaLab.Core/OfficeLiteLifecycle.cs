using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class OfficeLiteCycleContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.officelite-cycle-receipt";
    public const int MinimumSupportedReceiptSchemaVersion = 1;
    public const int ReceiptSchemaVersion = 4;
}

public sealed record OfficeLiteCycleRequest
{
    private const string PrimaryVmxRelativePath =
        "OfficeLite-Work/8.7.8-build04/runs/primary/KR C, V8.7.8OL_Build04.vmx";

    public required string AssetRoot { get; init; }

    public required string VmrunPath { get; init; }

    public required string VmxPath { get; init; }

    public string? GuestIpAddress { get; init; }

    public required string DhcpLeasePath { get; init; }

    public int GuestTlsPort { get; init; } = 443;

    public int ReadinessTimeoutSeconds { get; init; } = 120;

    public int ProbeIntervalMilliseconds { get; init; } = 1000;

    public bool DiagnoseWorkVisualServices { get; init; }

    public IReadOnlyList<int> RequiredWorkVisualServicePorts { get; init; } =
        [OfficeLiteServiceDiagnosticContract.DeviceInfoPort];

    public int ServiceObservationSeconds { get; init; }

    public int ServiceProbeIntervalMilliseconds { get; init; } = 5000;

    public int ServiceConnectTimeoutMilliseconds { get; init; } = 1000;

    public int VmrunTimeoutSeconds { get; init; } = 30;

    public int ShutdownTimeoutSeconds { get; init; } = 60;

    public bool AdoptRunningLabVm { get; init; }

    public string RunningVmOwnershipReference { get; init; } = string.Empty;

    public static OfficeLiteCycleRequest CreateDefault(
        string assetRoot,
        string? vmrunPath = null,
        string? guestIpAddress = null,
        string? dhcpLeasePath = null,
        int readinessTimeoutSeconds = 120)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetRoot);
        var fullAssetRoot = Path.GetFullPath(assetRoot);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var selectedVmrun = string.IsNullOrWhiteSpace(vmrunPath)
            ? new[]
                {
                    Path.Combine(fullAssetRoot, "VMware", "vmrun.exe"),
                    Path.Combine(programFilesX86, "VMware", "VMware Workstation", "vmrun.exe"),
                    Path.Combine(programFiles, "VMware", "VMware Workstation", "vmrun.exe")
                }
                .Select(Path.GetFullPath)
                .FirstOrDefault(File.Exists)
                ?? Path.Combine(fullAssetRoot, "VMware", "vmrun.exe")
            : Path.GetFullPath(vmrunPath);

        return new OfficeLiteCycleRequest
        {
            AssetRoot = fullAssetRoot,
            VmrunPath = selectedVmrun,
            VmxPath = Path.GetFullPath(Path.Combine(
                fullAssetRoot,
                PrimaryVmxRelativePath.Replace('/', Path.DirectorySeparatorChar))),
            GuestIpAddress = guestIpAddress,
            DhcpLeasePath = Path.GetFullPath(
                dhcpLeasePath ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "VMware",
                    "vmnetdhcp.leases")),
            ReadinessTimeoutSeconds = readinessTimeoutSeconds
        };
    }
}

public sealed record OfficeLiteCycleReceipt
{
    public string SchemaIdentity { get; init; } = OfficeLiteCycleContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = OfficeLiteCycleContract.ReceiptSchemaVersion;

    public required OfficeLiteCyclePayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record OfficeLiteCyclePayload
{
    public string ReceiptId { get; init; } = string.Empty;

    public string AttemptId { get; init; } = string.Empty;

    public string CoreVersion { get; init; } = FixtureContract.CoreVersion;

    public string CoreAssemblySha256 { get; init; } = string.Empty;

    public RuntimeEnvironment Runtime { get; init; } = new();

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset CompletedAtUtc { get; init; }

    public long DurationMilliseconds { get; init; }

    public EnvironmentTerminalClassification TerminalClassification { get; init; }

    public string AssetRoot { get; init; } = string.Empty;

    public string VmxPath { get; init; } = string.Empty;

    public string VmrunPath { get; init; } = string.Empty;

    public bool StartedByThisAttempt { get; init; }

    public bool AdoptedRunningLabVm { get; init; }

    public string RunningVmOwnershipReference { get; init; } = string.Empty;

    public bool ControllerReady { get; init; }

    public bool CleanShutdownVerified { get; init; }

    public NativeKssStatus NativeKssStatus { get; init; } = NativeKssStatus.NotRun;

    public GuestEndpointObservation GuestEndpoint { get; init; } = new();

    public WorkVisualServiceObservation WorkVisualServices { get; init; } = new();

    public GuestNetworkControlObservation NetworkControls { get; init; } = new();

    public List<EnvironmentCheck> Checks { get; init; } = [];

    public List<EnvironmentFileObservation> Files { get; init; } = [];

    public List<VmrunCommandObservation> Commands { get; init; } = [];

    public List<GuestProbeObservation> Probes { get; init; } = [];

    public List<string> SideEffects { get; init; } = [];

    public List<string> UnsupportedGaps { get; init; } = [];
}

public sealed record GuestEndpointObservation
{
    public string? Address { get; init; }

    public int Port { get; init; } = 443;

    public string? MacAddress { get; init; }

    public string? HostName { get; init; }

    public string? CertificateSubject { get; init; }

    public string? CertificateIssuer { get; init; }

    public string? CertificateThumbprint { get; init; }

    public string? TlsProtocol { get; init; }

    public bool SelfSignedCertificateObserved { get; init; }

    public bool CertificatePolicyBypassedForObservation { get; init; }
}

public sealed record VmrunCommandObservation
{
    public string Name { get; init; } = string.Empty;

    public int ExitCode { get; init; }

    public bool TimedOut { get; init; }

    public long DurationMilliseconds { get; init; }

    public string StandardOutput { get; init; } = string.Empty;

    public string StandardError { get; init; } = string.Empty;
}

public sealed record GuestProbeObservation
{
    public int Sequence { get; init; }

    public DateTimeOffset ObservedAtUtc { get; init; }

    public string? Address { get; init; }

    public bool PingSucceeded { get; init; }

    public bool TlsConnected { get; init; }

    public string Detail { get; init; } = string.Empty;
}

public sealed record OfficeLiteCycleOutcome(OfficeLiteCycleReceipt Receipt)
{
    public int ExitCode => Receipt.Payload.TerminalClassification switch
    {
        EnvironmentTerminalClassification.Ready => 0,
        EnvironmentTerminalClassification.Failed => 2,
        _ => 3
    };
}

internal sealed record OfficeLiteReadyContext(string GuestAddress);

public sealed class OfficeLiteCycleRunner
{
    private static readonly string[] RequiredVmxHardeningLines =
    [
        "isolation.tools.hgfs.disable = \"TRUE\"",
        "sharedFolder0.present = \"FALSE\"",
        "sharedFolder0.enabled = \"FALSE\"",
        "sharedFolder0.readAccess = \"FALSE\"",
        "sharedFolder0.writeAccess = \"FALSE\"",
        "sharedFolder.maxNum = \"0\"",
        "hgfs.mapRootShare = \"FALSE\""
    ];

    private readonly IOfficeLiteHostPlatform _platform;
    private readonly TimeProvider _timeProvider;

    public OfficeLiteCycleRunner()
        : this(new OfficeLiteHostPlatform(), TimeProvider.System)
    {
    }

    internal OfficeLiteCycleRunner(IOfficeLiteHostPlatform platform, TimeProvider timeProvider)
    {
        _platform = platform;
        _timeProvider = timeProvider;
    }

    public OfficeLiteCycleOutcome Run(OfficeLiteCycleRequest request, string attemptId)
        => Run(request, attemptId, null);

    internal OfficeLiteCycleOutcome Run(
        OfficeLiteCycleRequest request,
        string attemptId,
        Action<OfficeLiteReadyContext>? onWorkVisualReady)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAttemptId(attemptId);
        ValidateTimeouts(request);

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var checks = new List<EnvironmentCheck>();
        var files = new List<EnvironmentFileObservation>();
        var commands = new List<VmrunCommandObservation>();
        var probes = new List<GuestProbeObservation>();
        var sideEffects = new List<string>();
        var unsupportedGaps = new List<string>
        {
            "Native KSS compilation was not invoked by this VM lifecycle attempt.",
            "The runtime KSS build and controller archive still require controller-native evidence."
        };
        var assetRoot = Path.GetFullPath(request.AssetRoot);
        var vmrunPath = Path.GetFullPath(request.VmrunPath);
        var vmxPath = Path.GetFullPath(request.VmxPath);
        var startedByThisAttempt = false;
        var adoptedRunningLabVm = false;
        var vmOwnedByThisAttempt = false;
        var controllerReady = false;
        var cleanShutdownVerified = false;
        var endpoint = new GuestEndpointObservation { Port = request.GuestTlsPort };
        var workVisualServices = request.DiagnoseWorkVisualServices
            ? new WorkVisualServiceObservation
            {
                Requested = true,
                RequestedObservationSeconds = request.ServiceObservationSeconds,
                ProbeIntervalMilliseconds = request.ServiceProbeIntervalMilliseconds,
                ConnectTimeoutMilliseconds = request.ServiceConnectTimeoutMilliseconds,
                ExpectedPorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToList()
            }
            : new WorkVisualServiceObservation();
        var networkControls = request.DiagnoseWorkVisualServices
            ? CreateRequestedNetworkControl(request.ServiceConnectTimeoutMilliseconds)
            : new GuestNetworkControlObservation();

        if (!OperatingSystem.IsWindows())
        {
            checks.Add(Failed("host-os", "OfficeLite lifecycle requires Windows."));
            return Complete();
        }

        if (!Directory.Exists(assetRoot))
        {
            checks.Add(Blocked("asset-root", "The external asset root does not exist."));
            return Complete();
        }

        if ((File.GetAttributes(assetRoot) & FileAttributes.ReparsePoint) != 0)
        {
            checks.Add(Failed("asset-root", "The external asset root cannot be a reparse point."));
            return Complete();
        }

        if (!IsUnderRoot(assetRoot, vmxPath))
        {
            checks.Add(Failed("officelite-primary-vmx", "The requested VMX is outside the declared asset root."));
            return Complete();
        }

        if (!ObserveFile("vmware-vmrun", vmrunPath, includeVersion: true, files, checks)
            || !ObserveFile("officelite-primary-vmx", vmxPath, includeVersion: false, files, checks))
        {
            return Complete();
        }

        try
        {
            var vmxContent = File.ReadAllText(vmxPath);
            if (RequiredVmxHardeningLines.All(line =>
                    vmxContent.Contains(line, StringComparison.OrdinalIgnoreCase)))
            {
                checks.Add(Passed("officelite-primary-hardening", "HGFS and inherited host-drive sharing remain disabled."));
            }
            else
            {
                checks.Add(Failed("officelite-primary-hardening", "Required VMX share/HGFS hardening is incomplete."));
                return Complete();
            }

            endpoint = endpoint with { MacAddress = TryReadVmxMacAddress(vmxContent) };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            checks.Add(Failed("officelite-primary-vmx", $"The VMX could not be inspected: {exception.Message}"));
            return Complete();
        }

        var listBefore = RunVmrun(
            "list-before",
            vmrunPath,
            ["-T", "ws", "list"],
            request.VmrunTimeoutSeconds,
            commands);
        if (!CommandSucceeded(listBefore))
        {
            checks.Add(Failed("vmrun-list-before", "vmrun could not enumerate running virtual machines."));
            return Complete();
        }

        var targetAlreadyRunning = ContainsRunningVmx(listBefore.StandardOutput, vmxPath);
        if (targetAlreadyRunning && !request.AdoptRunningLabVm)
        {
            checks.Add(Blocked(
                "exclusive-vm-ownership",
                "The OfficeLite primary VM was already running; this attempt did not take ownership or stop it."));
            return Complete();
        }
        if (!targetAlreadyRunning && request.AdoptRunningLabVm)
        {
            checks.Add(Blocked(
                "exclusive-vm-ownership",
                "The explicitly authorized retained Lab VM was not running; the attempt did not start a replacement session."));
            return Complete();
        }
        if (targetAlreadyRunning)
        {
            adoptedRunningLabVm = true;
            vmOwnedByThisAttempt = true;
            sideEffects.Add($"AdoptAuthorizedRunningLabVm:{vmxPath}");
            checks.Add(Passed(
                "exclusive-vm-ownership",
                "The already-running exact Lab VM was explicitly adopted for this same-session operation and final soft teardown."));
        }
        else
        {
            checks.Add(Passed("exclusive-vm-ownership", "The OfficeLite primary VM was stopped before this attempt."));
        }
        try
        {
            if (!adoptedRunningLabVm)
            {
                var start = RunVmrun(
                    "start-nogui",
                    vmrunPath,
                    ["-T", "ws", "start", vmxPath, "nogui"],
                    request.VmrunTimeoutSeconds,
                    commands);
                if (!CommandSucceeded(start))
                {
                    checks.Add(Failed("vm-power-on", "vmrun failed to start the OfficeLite primary VM in nogui mode."));
                    return Complete();
                }

                startedByThisAttempt = true;
                vmOwnedByThisAttempt = true;
                sideEffects.Add($"PowerOnVmNogui:{vmxPath}");
            }
            var listAfterStart = RunVmrun(
                adoptedRunningLabVm ? "list-after-adopt" : "list-after-start",
                vmrunPath,
                ["-T", "ws", "list"],
                request.VmrunTimeoutSeconds,
                commands);
            if (!CommandSucceeded(listAfterStart)
                || !ContainsRunningVmx(listAfterStart.StandardOutput, vmxPath))
            {
                checks.Add(Failed("vm-power-on", "vmrun returned from start but the primary VM was not listed as running."));
            }
            else
            {
                checks.Add(Passed(
                    "vm-power-on",
                    adoptedRunningLabVm
                        ? "The explicitly authorized retained Lab VM remained listed as running after ownership transfer."
                        : "The primary VM started in nogui mode and was listed as running."));
                var readinessDeadline = _timeProvider.GetUtcNow().AddSeconds(request.ReadinessTimeoutSeconds);
                var sequence = 0;
                string? probeAdapterFailure = null;
                while (_timeProvider.GetUtcNow() <= readinessDeadline)
                {
                    sequence++;
                    var address = request.GuestIpAddress
                        ?? _platform.TryResolveDhcpAddress(vmxPath, request.DhcpLeasePath);
                    GuestProbeResult probe;
                    try
                    {
                        probe = _platform.ProbeGuest(
                            address,
                            request.GuestTlsPort,
                            Math.Min(1000, request.ProbeIntervalMilliseconds),
                            _timeProvider.GetUtcNow());
                    }
                    catch (Exception exception) when (exception is IOException
                        or UnauthorizedAccessException
                        or InvalidOperationException
                        or AuthenticationException
                        or SocketException)
                    {
                        probeAdapterFailure = $"{exception.GetType().Name}: {exception.Message}";
                        probe = new GuestProbeResult(
                            0,
                            _timeProvider.GetUtcNow(),
                            address,
                            false,
                            false,
                            probeAdapterFailure,
                            null);
                    }

                    probes.Add(probe.ToObservation() with { Sequence = sequence });
                    endpoint = endpoint with { Address = address };
                    if (probeAdapterFailure is not null)
                    {
                        break;
                    }

                    if (probe.TlsConnected && probe.TlsIdentity is not null)
                    {
                        endpoint = endpoint with
                        {
                            Address = address,
                            HostName = probe.TlsIdentity.HostName,
                            CertificateSubject = probe.TlsIdentity.Subject,
                            CertificateIssuer = probe.TlsIdentity.Issuer,
                            CertificateThumbprint = probe.TlsIdentity.Thumbprint,
                            TlsProtocol = probe.TlsIdentity.Protocol,
                            SelfSignedCertificateObserved = probe.TlsIdentity.SelfSigned,
                            CertificatePolicyBypassedForObservation = true
                        };
                        controllerReady = probe.TlsIdentity.Subject.Contains(
                            "KUKA Roboter GmbH",
                            StringComparison.OrdinalIgnoreCase);
                        break;
                    }

                    _platform.Delay(TimeSpan.FromMilliseconds(request.ProbeIntervalMilliseconds));
                }

                if (controllerReady)
                {
                    checks.Add(Passed(
                        "kuka-controller-https",
                        "The guest endpoint accepted TLS and presented a KUKA Roboter GmbH certificate."));
                }
                else if (probeAdapterFailure is not null)
                {
                    checks.Add(Failed(
                        "kuka-controller-https",
                        $"The readiness adapter failed safely: {probeAdapterFailure}"));
                }
                else if (endpoint.CertificateSubject is not null)
                {
                    checks.Add(Failed(
                        "kuka-controller-https",
                        "A TLS endpoint responded, but its certificate did not identify KUKA Roboter GmbH."));
                }
                else
                {
                    checks.Add(Failed(
                        "kuka-controller-https",
                        "The KUKA HTTPS readiness probe did not succeed before the bounded timeout."));
                }

                if (controllerReady && request.DiagnoseWorkVisualServices)
                {
                    networkControls = ObserveNetworkControls(
                        request,
                        endpoint.Address,
                        checks,
                        unsupportedGaps);
                    workVisualServices = ObserveWorkVisualServices(
                        request,
                        endpoint.Address,
                        checks,
                        unsupportedGaps);
                    if (RequiredWorkVisualPortsOpenedTogether(
                            workVisualServices,
                            request.RequiredWorkVisualServicePorts)
                        && !string.IsNullOrWhiteSpace(endpoint.Address)
                        && onWorkVisualReady is not null)
                    {
                        try
                        {
                            onWorkVisualReady(new OfficeLiteReadyContext(endpoint.Address));
                            checks.Add(Passed(
                                "workvisual-ready-action",
                                "The bounded action completed after all required WorkVisual service ports were open in the same probe."));
                        }
                        catch (Exception exception) when (exception is IOException
                            or UnauthorizedAccessException
                            or InvalidOperationException
                            or Win32Exception)
                        {
                            checks.Add(Failed(
                                "workvisual-ready-action",
                                $"The bounded read-only action failed safely: {exception.GetType().Name}: {exception.Message}"));
                        }
                    }
                }
                else if (request.DiagnoseWorkVisualServices)
                {
                    checks.Add(Blocked(
                        "workvisual-network-control",
                        "The fixed network-control profile did not start because KUKA HTTPS readiness was not established."));
                    checks.Add(Blocked(
                        "workvisual-service-readiness",
                        "The fixed WorkVisual service observation did not start because KUKA HTTPS readiness was not established."));
                }
            }
        }
        finally
        {
            if (vmOwnedByThisAttempt)
            {
                var stop = RunVmrun(
                    "stop-soft",
                    vmrunPath,
                    ["-T", "ws", "stop", vmxPath, "soft"],
                    request.VmrunTimeoutSeconds,
                    commands);
                sideEffects.Add($"PowerOffVmSoft:{vmxPath}");
                if (!CommandSucceeded(stop))
                {
                    checks.Add(Failed("vm-soft-shutdown", "vmrun soft shutdown returned a failure."));
                }
                else
                {
                    var shutdownDeadline = _timeProvider.GetUtcNow().AddSeconds(request.ShutdownTimeoutSeconds);
                    while (_timeProvider.GetUtcNow() <= shutdownDeadline)
                    {
                        var list = RunVmrun(
                            "list-after-stop",
                            vmrunPath,
                            ["-T", "ws", "list"],
                            request.VmrunTimeoutSeconds,
                            commands);
                        if (CommandSucceeded(list) && !ContainsRunningVmx(list.StandardOutput, vmxPath))
                        {
                            cleanShutdownVerified = true;
                            break;
                        }

                        _platform.Delay(TimeSpan.FromMilliseconds(request.ProbeIntervalMilliseconds));
                    }

                    checks.Add(cleanShutdownVerified
                        ? Passed("vm-soft-shutdown", "Soft shutdown completed and the primary VM is no longer running.")
                        : Failed("vm-soft-shutdown", "Soft shutdown could not be verified before the bounded timeout."));
                }
            }
        }

        return Complete();

        OfficeLiteCycleOutcome Complete()
        {
            stopwatch.Stop();
            var completedAt = _timeProvider.GetUtcNow();
            var terminal = checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
                ? EnvironmentTerminalClassification.Failed
                : checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked)
                    ? EnvironmentTerminalClassification.Blocked
                    : EnvironmentTerminalClassification.Ready;
            var payload = new OfficeLiteCyclePayload
            {
                ReceiptId = $"officelite-cycle-{attemptId}",
                AttemptId = attemptId,
                CoreAssemblySha256 = ComputeFileSha256(typeof(OfficeLiteCycleRunner).Assembly.Location),
                Runtime = new RuntimeEnvironment
                {
                    OsDescription = RuntimeInformation.OSDescription,
                    FrameworkDescription = RuntimeInformation.FrameworkDescription,
                    ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
                },
                StartedAtUtc = startedAt,
                CompletedAtUtc = completedAt,
                DurationMilliseconds = Math.Max(0, stopwatch.ElapsedMilliseconds),
                TerminalClassification = terminal,
                AssetRoot = assetRoot,
                VmxPath = vmxPath,
                VmrunPath = vmrunPath,
                StartedByThisAttempt = startedByThisAttempt,
                AdoptedRunningLabVm = adoptedRunningLabVm,
                RunningVmOwnershipReference = adoptedRunningLabVm
                    ? request.RunningVmOwnershipReference
                    : string.Empty,
                ControllerReady = controllerReady,
                CleanShutdownVerified = cleanShutdownVerified,
                NativeKssStatus = NativeKssStatus.NotRun,
                GuestEndpoint = endpoint,
                WorkVisualServices = workVisualServices,
                NetworkControls = networkControls,
                Checks = checks,
                Files = files,
                Commands = commands,
                Probes = probes,
                SideEffects = sideEffects,
                UnsupportedGaps = unsupportedGaps
            };
            var receipt = new OfficeLiteCycleReceipt
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
            };
            return new OfficeLiteCycleOutcome(receipt);
        }
    }

    private VmrunCommandObservation RunVmrun(
        string name,
        string vmrunPath,
        IReadOnlyList<string> arguments,
        int timeoutSeconds,
        List<VmrunCommandObservation> commands)
    {
        VmrunExecutionResult result;
        try
        {
            result = _platform.RunVmrun(vmrunPath, arguments, TimeSpan.FromSeconds(timeoutSeconds));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or Win32Exception)
        {
            result = new VmrunExecutionResult(
                -1,
                false,
                0,
                string.Empty,
                $"{exception.GetType().Name}: {exception.Message}");
        }
        var observation = new VmrunCommandObservation
        {
            Name = name,
            ExitCode = result.ExitCode,
            TimedOut = result.TimedOut,
            DurationMilliseconds = result.DurationMilliseconds,
            StandardOutput = result.StandardOutput.Trim(),
            StandardError = result.StandardError.Trim()
        };
        commands.Add(observation);
        return observation;
    }

    private WorkVisualServiceObservation ObserveWorkVisualServices(
        OfficeLiteCycleRequest request,
        string? address,
        List<EnvironmentCheck> checks,
        List<string> unsupportedGaps)
    {
        var expectedPorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToList();
        var snapshots = new List<GuestServiceSnapshot>();
        var startedAt = _timeProvider.GetUtcNow();
        var deadline = startedAt.AddSeconds(request.ServiceObservationSeconds);
        var sequence = 0;
        string? adapterFailure = null;

        while (_timeProvider.GetUtcNow() <= deadline)
        {
            sequence++;
            var observedAt = _timeProvider.GetUtcNow();
            IReadOnlyList<GuestTcpPortProbeResult> results;
            try
            {
                results = _platform.ProbeTcpPorts(
                    address,
                    expectedPorts,
                    request.ServiceConnectTimeoutMilliseconds,
                    observedAt);
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or SocketException)
            {
                adapterFailure = $"{exception.GetType().Name}: {exception.Message}";
                break;
            }

            if (results.Count != expectedPorts.Count
                || !results.Select(result => result.Port).SequenceEqual(expectedPorts))
            {
                adapterFailure = "The service probe adapter did not return the fixed WorkVisual port set in canonical order.";
                break;
            }

            snapshots.Add(new GuestServiceSnapshot
            {
                Sequence = sequence,
                ObservedAtUtc = observedAt,
                Ports = results.Select(result => result.ToObservation()).ToList()
            });

            if (request.RequiredWorkVisualServicePorts.All(requiredPort =>
                    results.Any(result => result.Port == requiredPort
                        && result.State == GuestTcpPortState.Open)))
            {
                break;
            }

            _platform.Delay(TimeSpan.FromMilliseconds(request.ServiceProbeIntervalMilliseconds));
        }

        var anyServiceOpen = snapshots
            .SelectMany(snapshot => snapshot.Ports)
            .Any(port => port.State == GuestTcpPortState.Open);
        var deviceInfoOpen = snapshots
            .SelectMany(snapshot => snapshot.Ports)
            .Any(port => port.Port == OfficeLiteServiceDiagnosticContract.DeviceInfoPort
                && port.State == GuestTcpPortState.Open);
        var observationCompleted = adapterFailure is null && snapshots.Count > 0;

        if (adapterFailure is not null)
        {
            checks.Add(Failed(
                "workvisual-service-readiness",
                $"The fixed WorkVisual service probe failed safely: {adapterFailure}"));
        }
        else if (deviceInfoOpen)
        {
            checks.Add(Passed(
                "workvisual-service-readiness",
                "TCP 49003 (DeviceInfo / WorkVisual Service Host) became reachable during the bounded observation."));
        }
        else
        {
            checks.Add(Warning(
                "workvisual-service-readiness",
                $"TCP 49003 did not become reachable during the bounded {request.ServiceObservationSeconds}-second observation."));
            unsupportedGaps.Add(
                "The host-side port timeline cannot distinguish an in-guest stopped service, controller runtime error, guest firewall/listen-address isolation or non-default read-access policy.");
        }

        return new WorkVisualServiceObservation
        {
            Requested = true,
            ObservationCompleted = observationCompleted,
            RequestedObservationSeconds = request.ServiceObservationSeconds,
            ActualObservationMilliseconds = Math.Max(
                0,
                (long)(_timeProvider.GetUtcNow() - startedAt).TotalMilliseconds),
            ProbeIntervalMilliseconds = request.ServiceProbeIntervalMilliseconds,
            ConnectTimeoutMilliseconds = request.ServiceConnectTimeoutMilliseconds,
            ExpectedPorts = expectedPorts,
            AnyServiceEverOpen = anyServiceOpen,
            DeviceInfoEverOpen = deviceInfoOpen,
            Snapshots = snapshots
        };
    }

    private static bool RequiredWorkVisualPortsOpenedTogether(
        WorkVisualServiceObservation observation,
        IReadOnlyList<int> requiredPorts)
    {
        return requiredPorts.Count > 0
            && requiredPorts.All(OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.Contains)
            && observation.Snapshots.Any(snapshot => requiredPorts.All(requiredPort =>
                snapshot.Ports.Any(port => port.Port == requiredPort
                    && port.State == GuestTcpPortState.Open)));
    }

    private GuestNetworkControlObservation ObserveNetworkControls(
        OfficeLiteCycleRequest request,
        string? address,
        List<EnvironmentCheck> checks,
        List<string> unsupportedGaps)
    {
        var observation = CreateRequestedNetworkControl(request.ServiceConnectTimeoutMilliseconds);
        var observedAt = _timeProvider.GetUtcNow();
        IReadOnlyList<GuestTcpPortProbeResult> results;
        try
        {
            results = _platform.ProbeTcpPorts(
                address,
                observation.ExpectedPorts,
                request.ServiceConnectTimeoutMilliseconds,
                observedAt);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or SocketException)
        {
            checks.Add(Failed(
                "workvisual-network-control",
                $"The fixed network-control probe failed safely: {exception.GetType().Name}: {exception.Message}"));
            return observation;
        }

        if (results.Count != observation.ExpectedPorts.Count
            || !results.Select(result => result.Port).SequenceEqual(observation.ExpectedPorts))
        {
            checks.Add(Failed(
                "workvisual-network-control",
                "The network-control probe adapter did not return the fixed port set in canonical order."));
            return observation;
        }

        var referenceResults = results.Where(result => observation.ReferencePorts.Contains(result.Port)).ToList();
        var targetResults = results.Where(result => observation.TargetPorts.Contains(result.Port)).ToList();
        var positiveControlOpen = results.Any(result =>
            result.Port == observation.PositiveControlPort && result.State == GuestTcpPortState.Open);
        var anyReferenceOpen = referenceResults.Any(result => result.State == GuestTcpPortState.Open);
        var anyReferenceTimeout = referenceResults.Any(result => result.State == GuestTcpPortState.Timeout);
        var anyReferenceRefused = referenceResults.Any(result => result.State == GuestTcpPortState.ConnectionRefused);
        var allTargetsTimeout = targetResults.Count == observation.TargetPorts.Count
            && targetResults.All(result => result.State == GuestTcpPortState.Timeout);
        var timeoutClassificationAmbiguous = positiveControlOpen
            && anyReferenceTimeout
            && allTargetsTimeout;

        checks.Add(positiveControlOpen
            ? Passed(
                "workvisual-network-control",
                "The fixed network-control profile completed and the known-ready KUKA HTTPS port was open.")
            : Warning(
                "workvisual-network-control",
                "The network-control profile completed, but the known-ready KUKA HTTPS port was no longer open."));
        if (timeoutClassificationAmbiguous)
        {
            unsupportedGaps.Add(
                "WorkVisual target timeouts share the host-side Timeout classification with unrelated reference ports; timeout alone cannot distinguish an absent listener from guest filtering or listen-address isolation.");
        }

        return observation with
        {
            ObservationCompleted = true,
            ObservedAtUtc = observedAt,
            PositiveControlOpen = positiveControlOpen,
            AnyReferencePortOpen = anyReferenceOpen,
            AnyReferencePortTimeout = anyReferenceTimeout,
            AnyReferencePortConnectionRefused = anyReferenceRefused,
            AllTargetPortsTimeout = allTargetsTimeout,
            TargetTimeoutClassificationAmbiguous = timeoutClassificationAmbiguous,
            Ports = results.Select(result => result.ToObservation()).ToList()
        };
    }

    private static GuestNetworkControlObservation CreateRequestedNetworkControl(int connectTimeoutMilliseconds) =>
        new()
        {
            Requested = true,
            ConnectTimeoutMilliseconds = connectTimeoutMilliseconds,
            ExpectedPorts = OfficeLiteServiceDiagnosticContract.NetworkControlPorts.ToList(),
            PositiveControlPort = OfficeLiteServiceDiagnosticContract.PositiveControlPort,
            ReferencePorts = OfficeLiteServiceDiagnosticContract.ReferencePorts.ToList(),
            TargetPorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToList()
        };

    private static bool ObserveFile(
        string id,
        string path,
        bool includeVersion,
        List<EnvironmentFileObservation> files,
        List<EnvironmentCheck> checks)
    {
        if (!File.Exists(path))
        {
            files.Add(new EnvironmentFileObservation { Id = id, Path = path, Exists = false });
            checks.Add(Blocked(id, $"Required file is missing: {path}"));
            return false;
        }

        try
        {
            var info = new FileInfo(path);
            var version = includeVersion ? FileVersionInfo.GetVersionInfo(path) : null;
            files.Add(new EnvironmentFileObservation
            {
                Id = id,
                Path = path,
                Exists = true,
                Bytes = info.Length,
                Sha256 = ComputeFileSha256(path),
                FileVersion = string.IsNullOrWhiteSpace(version?.FileVersion) ? null : version.FileVersion,
                ProductVersion = string.IsNullOrWhiteSpace(version?.ProductVersion) ? null : version.ProductVersion
            });
            checks.Add(Passed(id, "Required file exists and was hashed."));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            checks.Add(Failed(id, $"Required file could not be inventoried: {exception.Message}"));
            return false;
        }
    }

    private static bool CommandSucceeded(VmrunCommandObservation observation) =>
        !observation.TimedOut && observation.ExitCode == 0;

    private static bool ContainsRunningVmx(string standardOutput, string vmxPath) =>
        standardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(line => string.Equals(
                line,
                vmxPath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));

    private static bool IsUnderRoot(string root, string candidate)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidate);
        return normalizedCandidate.StartsWith(
            normalizedRoot,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static string? TryReadVmxMacAddress(string vmxContent)
    {
        var match = Regex.Match(
            vmxContent,
            "^ethernet0\\.address\\s*=\\s*\"(?<mac>[^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["mac"].Value.ToUpperInvariant() : null;
    }

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void ValidateAttemptId(string attemptId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptId);
        if (!Regex.IsMatch(
                attemptId,
                "^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$",
                RegexOptions.CultureInvariant))
        {
            throw new ArgumentException(
                "Attempt ID must match [A-Za-z0-9][A-Za-z0-9._-]{2,127}.",
                nameof(attemptId));
        }
    }

    private static void ValidateTimeouts(OfficeLiteCycleRequest request)
    {
        if (request.GuestTlsPort is < 1 or > 65535
            || request.ReadinessTimeoutSeconds is < 1 or > 900
            || request.ProbeIntervalMilliseconds is < 100 or > 10000
            || request.ServiceProbeIntervalMilliseconds is < 100 or > 10000
            || request.ServiceConnectTimeoutMilliseconds is < 100 or > 5000
            || request.VmrunTimeoutSeconds is < 1 or > 120
            || request.ShutdownTimeoutSeconds is < 1 or > 300)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "OfficeLite lifecycle timing or port settings are outside safe bounds.");
        }

        if (request.DiagnoseWorkVisualServices
            && request.ServiceObservationSeconds is < 1 or > 600)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "WorkVisual service observation must be from 1 through 600 seconds when requested.");
        }

        if (!request.DiagnoseWorkVisualServices && request.ServiceObservationSeconds != 0)
        {
            throw new ArgumentException(
                "ServiceObservationSeconds must be zero when WorkVisual service diagnosis is not requested.",
                nameof(request));
        }

        if (request.GuestIpAddress is not null
            && (!IPAddress.TryParse(request.GuestIpAddress, out var address)
                || address.AddressFamily != AddressFamily.InterNetwork))
        {
            throw new ArgumentException("Guest IP address must be an IPv4 literal.", nameof(request));
        }

        if (request.AdoptRunningLabVm
            && !Regex.IsMatch(
                request.RunningVmOwnershipReference,
                "^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$",
                RegexOptions.CultureInvariant))
        {
            throw new ArgumentException(
                "RunningVmOwnershipReference must be a non-secret identifier matching [A-Za-z0-9][A-Za-z0-9._-]{2,127} when adopting a retained Lab VM.",
                nameof(request));
        }
    }

    private static EnvironmentCheck Passed(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Passed, Detail = detail };

    private static EnvironmentCheck Blocked(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Blocked, Detail = detail };

    private static EnvironmentCheck Warning(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Warning, Detail = detail };

    private static EnvironmentCheck Failed(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Failed, Detail = detail };
}

public sealed record OfficeLiteCycleReceiptVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}

public static class OfficeLiteCycleReceiptVerifier
{
    public static OfficeLiteCycleReceiptVerificationResult Verify(OfficeLiteCycleReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (!string.Equals(
                receipt.SchemaIdentity,
                OfficeLiteCycleContract.ReceiptSchemaIdentity,
                StringComparison.Ordinal))
        {
            errors.Add($"schemaIdentity must be {OfficeLiteCycleContract.ReceiptSchemaIdentity}");
        }

        if (receipt.SchemaVersion is < OfficeLiteCycleContract.MinimumSupportedReceiptSchemaVersion
            or > OfficeLiteCycleContract.ReceiptSchemaVersion)
        {
            errors.Add(
                $"schemaVersion must be between {OfficeLiteCycleContract.MinimumSupportedReceiptSchemaVersion} "
                + $"and {OfficeLiteCycleContract.ReceiptSchemaVersion}");
        }

        if (ReferenceEquals(receipt.Payload, null))
        {
            return new OfficeLiteCycleReceiptVerificationResult
            {
                Succeeded = false,
                PayloadSha256 = receipt.PayloadSha256,
                Errors = ["payload is required"]
            };
        }

        var payload = receipt.Payload;
        if (string.IsNullOrWhiteSpace(payload.ReceiptId)
            || string.IsNullOrWhiteSpace(payload.AttemptId))
        {
            errors.Add("receiptId and attemptId are required");
        }

        if (payload.CoreAssemblySha256.Length != 64
            || payload.CoreAssemblySha256.Any(character => !Uri.IsHexDigit(character)))
        {
            errors.Add("payload.coreAssemblySha256 must be a SHA-256 value");
        }

        if (payload.Runtime is null
            || string.IsNullOrWhiteSpace(payload.Runtime.OsDescription)
            || string.IsNullOrWhiteSpace(payload.Runtime.FrameworkDescription)
            || string.IsNullOrWhiteSpace(payload.Runtime.ProcessArchitecture))
        {
            errors.Add("payload.runtime identity is incomplete");
        }

        if (payload.StartedAtUtc > payload.CompletedAtUtc || payload.DurationMilliseconds < 0)
        {
            errors.Add("payload timing is invalid");
        }

        if (payload.Checks is null || payload.Checks.Count == 0
            || payload.Checks.Any(check => string.IsNullOrWhiteSpace(check.Id))
            || payload.Checks.GroupBy(check => check.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            errors.Add("payload.checks must contain evidence with unique non-empty IDs");
        }

        if (payload.Files is null
            || payload.Commands is null
            || payload.Probes is null
            || payload.SideEffects is null
            || payload.UnsupportedGaps is null)
        {
            errors.Add("payload evidence collections cannot be null");
        }

        if (payload.NativeKssStatus != NativeKssStatus.NotRun)
        {
            errors.Add("VM lifecycle evidence cannot claim native KSS validation");
        }

        if (payload.GuestEndpoint is null)
        {
            errors.Add("payload.guestEndpoint is required");
        }
        else if (payload.ControllerReady
            && (!(payload.StartedByThisAttempt || payload.AdoptedRunningLabVm)
                || string.IsNullOrWhiteSpace(payload.GuestEndpoint.CertificateSubject)
                || !payload.GuestEndpoint.CertificateSubject.Contains(
                    "KUKA Roboter GmbH",
                    StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("controller-ready state lacks KUKA TLS identity or attempt ownership");
        }

        if (receipt.SchemaVersion == 1)
        {
            VerifyLegacyWorkVisualServiceObservation(payload, errors);
        }
        else
        {
            VerifyWorkVisualServiceObservation(payload, errors);
        }

        if (receipt.SchemaVersion <= 2)
        {
            VerifyLegacyNetworkControlObservation(receipt.SchemaVersion, payload, errors);
        }
        else
        {
            VerifyNetworkControlObservation(payload, errors);
        }

        if (payload.StartedByThisAttempt && payload.AdoptedRunningLabVm)
        {
            errors.Add("a lifecycle cannot both start and adopt the same VM");
        }

        if (receipt.SchemaVersion <= 3
            && (payload.AdoptedRunningLabVm || !string.IsNullOrEmpty(payload.RunningVmOwnershipReference)))
        {
            errors.Add("running Lab VM adoption requires lifecycle receipt schema v4");
        }

        if (payload.AdoptedRunningLabVm
            && string.IsNullOrWhiteSpace(payload.RunningVmOwnershipReference))
        {
            errors.Add("adopted running Lab VM evidence requires a non-secret ownership reference");
        }

        if (!payload.AdoptedRunningLabVm
            && !string.IsNullOrEmpty(payload.RunningVmOwnershipReference))
        {
            errors.Add("running VM ownership reference is only valid for an adopted Lab VM");
        }

        if (payload.CleanShutdownVerified
            && !(payload.StartedByThisAttempt || payload.AdoptedRunningLabVm))
        {
            errors.Add("clean shutdown cannot be claimed for a VM this attempt did not own");
        }

        if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready
            && (!payload.ControllerReady || !payload.CleanShutdownVerified))
        {
            errors.Add("ready lifecycle evidence requires controller readiness and verified clean shutdown");
        }

        if (payload.Checks is not null)
        {
            var expectedTerminal = payload.Checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
                ? EnvironmentTerminalClassification.Failed
                : payload.Checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked)
                    ? EnvironmentTerminalClassification.Blocked
                    : EnvironmentTerminalClassification.Ready;
            if (payload.TerminalClassification != expectedTerminal)
            {
                errors.Add("payload.terminalClassification does not agree with check statuses");
            }
        }

        if (!string.Equals(
                receipt.PayloadSha256,
                ReceiptSerialization.ComputeOfficeLiteCyclePayloadSha256(payload, receipt.SchemaVersion),
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("payloadSha256 does not match the canonical payload");
        }

        return new OfficeLiteCycleReceiptVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }

    private static void VerifyWorkVisualServiceObservation(
        OfficeLiteCyclePayload payload,
        List<string> errors)
    {
        var observation = payload.WorkVisualServices;
        if (observation is null)
        {
            errors.Add("payload.workVisualServices is required");
            return;
        }

        if (!observation.Requested)
        {
            if (observation.ObservationCompleted
                || observation.RequestedObservationSeconds != 0
                || observation.ActualObservationMilliseconds != 0
                || observation.ProbeIntervalMilliseconds != 0
                || observation.ConnectTimeoutMilliseconds != 0
                || observation.ExpectedPorts is null
                || observation.ExpectedPorts.Count != 0
                || observation.AnyServiceEverOpen
                || observation.DeviceInfoEverOpen
                || observation.Snapshots is null
                || observation.Snapshots.Count != 0)
            {
                errors.Add("unrequested WorkVisual service evidence must remain empty");
            }

            return;
        }

        var expectedPorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts;
        if (observation.ExpectedPorts is null
            || !observation.ExpectedPorts.SequenceEqual(expectedPorts))
        {
            errors.Add("WorkVisual service evidence must use the fixed canonical port set");
        }

        if (observation.RequestedObservationSeconds is < 1 or > 600
            || observation.ActualObservationMilliseconds < 0
            || observation.ProbeIntervalMilliseconds is < 100 or > 10000
            || observation.ConnectTimeoutMilliseconds is < 100 or > 5000)
        {
            errors.Add("WorkVisual service observation timing is outside safe bounds");
        }

        if (observation.Snapshots is null)
        {
            errors.Add("WorkVisual service snapshots cannot be null");
            return;
        }

        var allPorts = new List<GuestTcpPortObservation>();
        for (var index = 0; index < observation.Snapshots.Count; index++)
        {
            var snapshot = observation.Snapshots[index];
            if (snapshot is null)
            {
                errors.Add("WorkVisual service snapshots cannot contain null entries");
                continue;
            }

            if (snapshot.Sequence != index + 1)
            {
                errors.Add("WorkVisual service snapshot sequence must be contiguous");
            }

            if (snapshot.Ports is null
                || snapshot.Ports.Any(port => port is null)
                || !snapshot.Ports.Select(port => port.Port).SequenceEqual(expectedPorts)
                || snapshot.Ports.Any(port => port.DurationMilliseconds < 0
                    || string.IsNullOrWhiteSpace(port.Detail)))
            {
                errors.Add("each WorkVisual service snapshot must contain the fixed ports in canonical order with valid details");
                continue;
            }

            allPorts.AddRange(snapshot.Ports);
        }

        var derivedAnyOpen = allPorts.Any(port => port.State == GuestTcpPortState.Open);
        var derivedDeviceInfoOpen = allPorts.Any(port =>
            port.Port == OfficeLiteServiceDiagnosticContract.DeviceInfoPort
            && port.State == GuestTcpPortState.Open);
        if (observation.AnyServiceEverOpen != derivedAnyOpen)
        {
            errors.Add("AnyServiceEverOpen does not agree with WorkVisual service snapshots");
        }

        if (observation.DeviceInfoEverOpen != derivedDeviceInfoOpen)
        {
            errors.Add("DeviceInfoEverOpen does not agree with WorkVisual service snapshots");
        }

        if (observation.ObservationCompleted && observation.Snapshots.Count == 0)
        {
            errors.Add("completed WorkVisual service observation requires snapshot evidence");
        }

        if (observation.ObservationCompleted
            && !observation.DeviceInfoEverOpen
            && observation.ActualObservationMilliseconds < observation.RequestedObservationSeconds * 1000L)
        {
            errors.Add("unreachable DeviceInfo evidence requires the full requested observation window");
        }

        var serviceCheck = payload.Checks?.FirstOrDefault(check =>
            string.Equals(check.Id, "workvisual-service-readiness", StringComparison.Ordinal));
        if (serviceCheck is null)
        {
            errors.Add("requested WorkVisual service evidence requires a readiness check");
        }
        else if (observation.ObservationCompleted
            && observation.DeviceInfoEverOpen
            && serviceCheck.Status != EnvironmentCheckStatus.Passed)
        {
            errors.Add("open DeviceInfo evidence requires a passed readiness check");
        }
        else if (observation.ObservationCompleted
            && !observation.DeviceInfoEverOpen
            && serviceCheck.Status != EnvironmentCheckStatus.Warning)
        {
            errors.Add("unreachable DeviceInfo evidence requires a warning readiness check");
        }
        else if (!observation.ObservationCompleted
            && serviceCheck.Status is not EnvironmentCheckStatus.Blocked and not EnvironmentCheckStatus.Failed)
        {
            errors.Add("incomplete WorkVisual service evidence requires a blocked or failed readiness check");
        }

        if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready
            && !observation.ObservationCompleted)
        {
            errors.Add("ready lifecycle evidence requires a completed requested WorkVisual service observation");
        }
    }

    private static void VerifyLegacyWorkVisualServiceObservation(
        OfficeLiteCyclePayload payload,
        List<string> errors)
    {
        var observation = payload.WorkVisualServices;
        if (observation is null)
        {
            return;
        }

        if (observation.Requested
            || observation.ObservationCompleted
            || observation.RequestedObservationSeconds != 0
            || observation.ActualObservationMilliseconds != 0
            || observation.ProbeIntervalMilliseconds != 0
            || observation.ConnectTimeoutMilliseconds != 0
            || observation.ExpectedPorts is null
            || observation.ExpectedPorts.Count != 0
            || observation.AnyServiceEverOpen
            || observation.DeviceInfoEverOpen
            || observation.Snapshots is null
            || observation.Snapshots.Count != 0)
        {
            errors.Add("schemaVersion 1 cannot contain WorkVisual service diagnostic evidence");
        }
    }

    private static void VerifyNetworkControlObservation(
        OfficeLiteCyclePayload payload,
        List<string> errors)
    {
        var observation = payload.NetworkControls;
        if (observation is null)
        {
            errors.Add("payload.networkControls is required");
            return;
        }

        var workVisualRequested = payload.WorkVisualServices?.Requested == true;
        if (observation.Requested != workVisualRequested)
        {
            errors.Add("network-control request state must match WorkVisual service diagnosis");
        }

        if (!observation.Requested)
        {
            if (observation.ObservationCompleted
                || observation.ConnectTimeoutMilliseconds != 0
                || observation.ExpectedPorts is null
                || observation.ExpectedPorts.Count != 0
                || observation.PositiveControlPort != 0
                || observation.ReferencePorts is null
                || observation.ReferencePorts.Count != 0
                || observation.TargetPorts is null
                || observation.TargetPorts.Count != 0
                || observation.ObservedAtUtc is not null
                || observation.PositiveControlOpen
                || observation.AnyReferencePortOpen
                || observation.AnyReferencePortTimeout
                || observation.AnyReferencePortConnectionRefused
                || observation.AllTargetPortsTimeout
                || observation.TargetTimeoutClassificationAmbiguous
                || observation.Ports is null
                || observation.Ports.Count != 0)
            {
                errors.Add("unrequested network-control evidence must remain empty");
            }

            return;
        }

        var expectedPorts = OfficeLiteServiceDiagnosticContract.NetworkControlPorts;
        var expectedReferencePorts = OfficeLiteServiceDiagnosticContract.ReferencePorts;
        var expectedTargetPorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts;
        if (observation.ConnectTimeoutMilliseconds is < 100 or > 5000
            || observation.ExpectedPorts is null
            || !observation.ExpectedPorts.SequenceEqual(expectedPorts)
            || observation.PositiveControlPort != OfficeLiteServiceDiagnosticContract.PositiveControlPort
            || observation.ReferencePorts is null
            || !observation.ReferencePorts.SequenceEqual(expectedReferencePorts)
            || observation.TargetPorts is null
            || !observation.TargetPorts.SequenceEqual(expectedTargetPorts))
        {
            errors.Add("network-control evidence must use the fixed ports, roles and safe timeout");
        }

        var controlCheck = payload.Checks?.FirstOrDefault(check =>
            string.Equals(check.Id, "workvisual-network-control", StringComparison.Ordinal));
        if (controlCheck is null)
        {
            errors.Add("requested network-control evidence requires a check");
        }

        if (!observation.ObservationCompleted)
        {
            if (observation.ObservedAtUtc is not null
                || observation.PositiveControlOpen
                || observation.AnyReferencePortOpen
                || observation.AnyReferencePortTimeout
                || observation.AnyReferencePortConnectionRefused
                || observation.AllTargetPortsTimeout
                || observation.TargetTimeoutClassificationAmbiguous
                || observation.Ports is null
                || observation.Ports.Count != 0)
            {
                errors.Add("incomplete network-control evidence cannot contain observations or derived claims");
            }

            if (controlCheck is not null
                && controlCheck.Status is not EnvironmentCheckStatus.Blocked and not EnvironmentCheckStatus.Failed)
            {
                errors.Add("incomplete network-control evidence requires a blocked or failed check");
            }

            return;
        }

        if (observation.ObservedAtUtc is null
            || observation.Ports is null
            || observation.Ports.Any(port => port is null)
            || !observation.Ports.Select(port => port.Port).SequenceEqual(expectedPorts)
            || observation.Ports.Any(port => port.DurationMilliseconds < 0
                || string.IsNullOrWhiteSpace(port.Detail)))
        {
            errors.Add("completed network-control evidence requires the fixed ordered port observations");
            return;
        }

        var referencePorts = observation.Ports
            .Where(port => expectedReferencePorts.Contains(port.Port))
            .ToList();
        var targetPorts = observation.Ports
            .Where(port => expectedTargetPorts.Contains(port.Port))
            .ToList();
        var derivedPositiveOpen = observation.Ports.Any(port =>
            port.Port == OfficeLiteServiceDiagnosticContract.PositiveControlPort
            && port.State == GuestTcpPortState.Open);
        var derivedReferenceOpen = referencePorts.Any(port => port.State == GuestTcpPortState.Open);
        var derivedReferenceTimeout = referencePorts.Any(port => port.State == GuestTcpPortState.Timeout);
        var derivedReferenceRefused = referencePorts.Any(port => port.State == GuestTcpPortState.ConnectionRefused);
        var derivedAllTargetsTimeout = targetPorts.Count == expectedTargetPorts.Count
            && targetPorts.All(port => port.State == GuestTcpPortState.Timeout);
        var derivedAmbiguous = derivedPositiveOpen && derivedReferenceTimeout && derivedAllTargetsTimeout;

        if (observation.PositiveControlOpen != derivedPositiveOpen
            || observation.AnyReferencePortOpen != derivedReferenceOpen
            || observation.AnyReferencePortTimeout != derivedReferenceTimeout
            || observation.AnyReferencePortConnectionRefused != derivedReferenceRefused
            || observation.AllTargetPortsTimeout != derivedAllTargetsTimeout)
        {
            errors.Add("network-control derived state does not agree with port observations");
        }

        if (observation.TargetTimeoutClassificationAmbiguous != derivedAmbiguous)
        {
            errors.Add("network-control ambiguous timeout claim does not agree with port observations");
        }

        if (controlCheck is not null
            && derivedPositiveOpen
            && controlCheck.Status != EnvironmentCheckStatus.Passed)
        {
            errors.Add("open network positive control requires a passed check");
        }
        else if (controlCheck is not null
            && !derivedPositiveOpen
            && controlCheck.Status != EnvironmentCheckStatus.Warning)
        {
            errors.Add("closed network positive control requires a warning check");
        }

        if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready
            && !observation.ObservationCompleted)
        {
            errors.Add("ready lifecycle evidence requires completed requested network controls");
        }
    }

    private static void VerifyLegacyNetworkControlObservation(
        int schemaVersion,
        OfficeLiteCyclePayload payload,
        List<string> errors)
    {
        var observation = payload.NetworkControls;
        if (observation is null)
        {
            return;
        }

        if (observation.Requested
            || observation.ObservationCompleted
            || observation.ConnectTimeoutMilliseconds != 0
            || observation.ExpectedPorts is null
            || observation.ExpectedPorts.Count != 0
            || observation.PositiveControlPort != 0
            || observation.ReferencePorts is null
            || observation.ReferencePorts.Count != 0
            || observation.TargetPorts is null
            || observation.TargetPorts.Count != 0
            || observation.ObservedAtUtc is not null
            || observation.PositiveControlOpen
            || observation.AnyReferencePortOpen
            || observation.AnyReferencePortTimeout
            || observation.AnyReferencePortConnectionRefused
            || observation.AllTargetPortsTimeout
            || observation.TargetTimeoutClassificationAmbiguous
            || observation.Ports is null
            || observation.Ports.Count != 0)
        {
            errors.Add($"schemaVersion {schemaVersion} cannot contain network-control evidence");
        }
    }
}

internal interface IOfficeLiteHostPlatform
{
    VmrunExecutionResult RunVmrun(string vmrunPath, IReadOnlyList<string> arguments, TimeSpan timeout);

    string? TryResolveDhcpAddress(string vmxPath, string dhcpLeasePath);

    GuestProbeResult ProbeGuest(string? address, int port, int timeoutMilliseconds, DateTimeOffset observedAtUtc);

    IReadOnlyList<GuestTcpPortProbeResult> ProbeTcpPorts(
        string? address,
        IReadOnlyList<int> ports,
        int timeoutMilliseconds,
        DateTimeOffset observedAtUtc);

    void Delay(TimeSpan duration);
}

internal sealed record VmrunExecutionResult(
    int ExitCode,
    bool TimedOut,
    long DurationMilliseconds,
    string StandardOutput,
    string StandardError);

internal sealed record GuestTlsIdentity(
    string HostName,
    string Subject,
    string Issuer,
    string Thumbprint,
    string Protocol,
    bool SelfSigned);

internal sealed record GuestProbeResult(
    int Sequence,
    DateTimeOffset ObservedAtUtc,
    string? Address,
    bool PingSucceeded,
    bool TlsConnected,
    string Detail,
    GuestTlsIdentity? TlsIdentity)
{
    public GuestProbeObservation ToObservation() => new()
    {
        Sequence = Sequence,
        ObservedAtUtc = ObservedAtUtc,
        Address = Address,
        PingSucceeded = PingSucceeded,
        TlsConnected = TlsConnected,
        Detail = Detail
    };
}

internal sealed class OfficeLiteHostPlatform : IOfficeLiteHostPlatform
{
    public VmrunExecutionResult RunVmrun(
        string vmrunPath,
        IReadOnlyList<string> arguments,
        TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = vmrunPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();
        process.Start();
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        var exited = process.WaitForExit((int)Math.Ceiling(timeout.TotalMilliseconds));
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        stopwatch.Stop();
        return new VmrunExecutionResult(
            exited ? process.ExitCode : -1,
            !exited,
            stopwatch.ElapsedMilliseconds,
            standardOutput.GetAwaiter().GetResult(),
            standardError.GetAwaiter().GetResult());
    }

    public string? TryResolveDhcpAddress(string vmxPath, string dhcpLeasePath)
    {
        try
        {
            if (!File.Exists(vmxPath) || !File.Exists(dhcpLeasePath))
            {
                return null;
            }

            var vmxContent = File.ReadAllText(vmxPath);
            var macMatch = Regex.Match(
                vmxContent,
                "^ethernet0\\.address\\s*=\\s*\"(?<mac>[^\"]+)\"",
                RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
            if (!macMatch.Success)
            {
                return null;
            }

            var expectedMac = NormalizeMac(macMatch.Groups["mac"].Value);
            var leaseMatches = Regex.Matches(
                ReadSharedText(dhcpLeasePath),
                "lease\\s+(?<ip>\\d{1,3}(?:\\.\\d{1,3}){3})\\s*\\{(?<body>.*?)\\}",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
            return leaseMatches
                .Cast<Match>()
                .Where(match => NormalizeMac(match.Groups["body"].Value).Contains(expectedMac, StringComparison.Ordinal))
                .Where(match => !Regex.IsMatch(
                    match.Groups["body"].Value,
                    "(?m)^\\s*abandoned\\s*;",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                .Select(match => new
                {
                    IpAddress = match.Groups["ip"].Value,
                    StartedAtUtc = ReadLeaseStartUtc(match.Groups["body"].Value)
                })
                .Where(lease => IPAddress.TryParse(lease.IpAddress, out _))
                .OrderByDescending(lease => lease.StartedAtUtc)
                .Select(lease => lease.IpAddress)
                .FirstOrDefault();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static DateTimeOffset ReadLeaseStartUtc(string leaseBody)
    {
        var match = Regex.Match(
            leaseBody,
            "(?m)^\\s*starts\\s+\\d+\\s+(?<timestamp>\\d{4}/\\d{2}/\\d{2}\\s+\\d{2}:\\d{2}:\\d{2})\\s*;",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success
            && DateTimeOffset.TryParseExact(
                match.Groups["timestamp"].Value,
                "yyyy/MM/dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;
    }

    public GuestProbeResult ProbeGuest(
        string? address,
        int port,
        int timeoutMilliseconds,
        DateTimeOffset observedAtUtc)
    {
        if (address is null || !IPAddress.TryParse(address, out var ipAddress))
        {
            return new GuestProbeResult(
                0,
                observedAtUtc,
                address,
                false,
                false,
                "No valid guest IPv4 address is available yet.",
                null);
        }

        var pingSucceeded = false;
        try
        {
            using var ping = new Ping();
            pingSucceeded = ping.Send(ipAddress, timeoutMilliseconds)?.Status == IPStatus.Success;
        }
        catch (PingException)
        {
        }

        try
        {
            using var cancellation = new CancellationTokenSource(timeoutMilliseconds);
            using var client = new TcpClient(AddressFamily.InterNetwork);
            client.ConnectAsync(ipAddress, port, cancellation.Token).GetAwaiter().GetResult();
            using var ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
            var options = new SslClientAuthenticationOptions
            {
                TargetHost = address,
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            };
            ssl.AuthenticateAsClientAsync(options, cancellation.Token).GetAwaiter().GetResult();
            if (ssl.RemoteCertificate is null)
            {
                return new GuestProbeResult(
                    0,
                    observedAtUtc,
                    address,
                    pingSucceeded,
                    true,
                    "TLS connected without an observable remote certificate.",
                    null);
            }

            using var certificate = new X509Certificate2(ssl.RemoteCertificate);
            var identity = new GuestTlsIdentity(
                address,
                certificate.Subject,
                certificate.Issuer,
                certificate.Thumbprint,
                ssl.SslProtocol.ToString(),
                string.Equals(certificate.Subject, certificate.Issuer, StringComparison.OrdinalIgnoreCase));
            return new GuestProbeResult(
                0,
                observedAtUtc,
                address,
                pingSucceeded,
                true,
                "TLS endpoint responded and its certificate was observed without trusting it.",
                identity);
        }
        catch (Exception exception) when (exception is SocketException or IOException or AuthenticationException or OperationCanceledException)
        {
            return new GuestProbeResult(
                0,
                observedAtUtc,
                address,
                pingSucceeded,
                false,
                exception.GetType().Name,
                null);
        }
    }

    public IReadOnlyList<GuestTcpPortProbeResult> ProbeTcpPorts(
        string? address,
        IReadOnlyList<int> ports,
        int timeoutMilliseconds,
        DateTimeOffset observedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(ports);
        if (address is null
            || !IPAddress.TryParse(address, out var ipAddress)
            || ipAddress.AddressFamily != AddressFamily.InterNetwork)
        {
            return ports.Select(port => new GuestTcpPortProbeResult(
                port,
                observedAtUtc,
                GuestTcpPortState.Unresolved,
                "No valid guest IPv4 address is available.",
                0)).ToList();
        }

        return Task.WhenAll(ports.Select(port => ProbeTcpPortAsync(
                ipAddress,
                port,
                timeoutMilliseconds,
                observedAtUtc)))
            .GetAwaiter()
            .GetResult();
    }

    public void Delay(TimeSpan duration) => Thread.Sleep(duration);

    private static async Task<GuestTcpPortProbeResult> ProbeTcpPortAsync(
        IPAddress address,
        int port,
        int timeoutMilliseconds,
        DateTimeOffset observedAtUtc)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var cancellation = new CancellationTokenSource(timeoutMilliseconds);
            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(address, port, cancellation.Token).ConfigureAwait(false);
            stopwatch.Stop();
            return new GuestTcpPortProbeResult(
                port,
                observedAtUtc,
                GuestTcpPortState.Open,
                "TCP connection established.",
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return new GuestTcpPortProbeResult(
                port,
                observedAtUtc,
                GuestTcpPortState.Timeout,
                "TCP connection attempt timed out.",
                stopwatch.ElapsedMilliseconds);
        }
        catch (SocketException exception)
        {
            stopwatch.Stop();
            var state = exception.SocketErrorCode == SocketError.ConnectionRefused
                ? GuestTcpPortState.ConnectionRefused
                : GuestTcpPortState.Error;
            return new GuestTcpPortProbeResult(
                port,
                observedAtUtc,
                state,
                exception.SocketErrorCode.ToString(),
                stopwatch.ElapsedMilliseconds);
        }
        catch (IOException exception)
        {
            stopwatch.Stop();
            return new GuestTcpPortProbeResult(
                port,
                observedAtUtc,
                GuestTcpPortState.Error,
                $"IOException: {exception.Message}",
                stopwatch.ElapsedMilliseconds);
        }
    }

    private static string NormalizeMac(string value) =>
        new(value.Where(Uri.IsHexDigit).Select(char.ToUpperInvariant).ToArray());

    private static string ReadSharedText(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
