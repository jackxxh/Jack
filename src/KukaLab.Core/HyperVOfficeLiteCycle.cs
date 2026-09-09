using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class HyperVOfficeLiteCycleContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.hyperv-officelite-cycle-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const int Generation = 1;
    public const int ProcessorCount = 2;
    public const long MemoryStartupBytes = 4L * 1024 * 1024 * 1024;
}

public enum HyperVOfficeLiteBootClassification
{
    NotRun,
    HypervisorBlocked,
    BootFailed,
    GuestRunningNoNetwork,
    GuestNetworkReady
}

public sealed record HyperVOfficeLiteCycleRequest
{
    public required string TemplateVhdPath { get; init; }

    public required string ExpectedTemplateSha256 { get; init; }

    public required string VmRoot { get; init; }

    public required string VmName { get; init; }

    public required string SwitchName { get; init; }

    public required string DifferencingDiskPath { get; init; }

    public int Generation { get; init; } = HyperVOfficeLiteCycleContract.Generation;

    public int ProcessorCount { get; init; } = HyperVOfficeLiteCycleContract.ProcessorCount;

    public long MemoryStartupBytes { get; init; } = HyperVOfficeLiteCycleContract.MemoryStartupBytes;

    public int GuestTlsPort { get; init; } = 443;

    public int ReadinessTimeoutSeconds { get; init; } = 300;

    public int ProbeIntervalMilliseconds { get; init; } = 5000;

    public int ProbeTimeoutMilliseconds { get; init; } = 1000;

    public int PowerShellTimeoutSeconds { get; init; } = 60;

    public int ShutdownTimeoutSeconds { get; init; } = 120;

    public bool AllowHostChange { get; init; }

    public string? AuthorizationReference { get; init; }

    public static HyperVOfficeLiteCycleRequest CreateDefault(
        string templateVhdPath,
        string expectedTemplateSha256,
        string vmRoot,
        string vmName,
        string switchName,
        bool allowHostChange,
        string? authorizationReference,
        int readinessTimeoutSeconds = 300)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateVhdPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedTemplateSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(vmRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(vmName);
        ArgumentException.ThrowIfNullOrWhiteSpace(switchName);

        var fullRoot = Path.GetFullPath(vmRoot);
        var safeDiskName = Regex.Replace(vmName, "[^A-Za-z0-9._-]", "-", RegexOptions.CultureInvariant);
        return new HyperVOfficeLiteCycleRequest
        {
            TemplateVhdPath = Path.GetFullPath(templateVhdPath),
            ExpectedTemplateSha256 = expectedTemplateSha256.ToUpperInvariant(),
            VmRoot = fullRoot,
            VmName = vmName,
            SwitchName = switchName,
            DifferencingDiskPath = Path.Combine(fullRoot, "Virtual Hard Disks", $"{safeDiskName}.vhd"),
            AllowHostChange = allowHostChange,
            AuthorizationReference = authorizationReference,
            ReadinessTimeoutSeconds = readinessTimeoutSeconds
        };
    }
}

public sealed record HyperVOfficeLiteCycleReceipt
{
    public string SchemaIdentity { get; init; } = HyperVOfficeLiteCycleContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = HyperVOfficeLiteCycleContract.ReceiptSchemaVersion;

    public required HyperVOfficeLiteCyclePayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record HyperVOfficeLiteCyclePayload
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

    public HyperVOfficeLiteBootClassification BootClassification { get; init; }

    public string TemplateVhdPath { get; init; } = string.Empty;

    public string ExpectedTemplateSha256 { get; init; } = string.Empty;

    public string TemplateSha256Before { get; init; } = string.Empty;

    public string TemplateSha256After { get; init; } = string.Empty;

    public bool TemplateUnchanged { get; init; }

    public string VmRoot { get; init; } = string.Empty;

    public string VmName { get; init; } = string.Empty;

    public string SwitchName { get; init; } = string.Empty;

    public string DifferencingDiskPath { get; init; } = string.Empty;

    public int Generation { get; init; }

    public int ProcessorCount { get; init; }

    public long MemoryStartupBytes { get; init; }

    public bool HostChangeAuthorized { get; init; }

    public string? AuthorizationReference { get; init; }

    public bool VmCreatedByThisAttempt { get; init; }

    public bool StartedByThisAttempt { get; init; }

    public bool VmRetainedStopped { get; init; }

    public bool GuestTlsReady { get; init; }

    public bool ControllerReady { get; init; }

    public bool CleanShutdownVerified { get; init; }

    public bool EnvironmentReusable { get; init; }

    public NativeKssStatus NativeKssStatus { get; init; } = NativeKssStatus.NotRun;

    public GuestEndpointObservation GuestEndpoint { get; init; } = new();

    public List<EnvironmentCheck> Checks { get; init; } = [];

    public List<EnvironmentFileObservation> Files { get; init; } = [];

    public List<HyperVCommandObservation> Commands { get; init; } = [];

    public List<HyperVVmSnapshot> Snapshots { get; init; } = [];

    public List<GuestProbeObservation> Probes { get; init; } = [];

    public List<string> SideEffects { get; init; } = [];

    public List<string> UnsupportedGaps { get; init; } = [];
}

public sealed record HyperVCommandObservation
{
    public string Name { get; init; } = string.Empty;

    public int ExitCode { get; init; }

    public bool TimedOut { get; init; }

    public long DurationMilliseconds { get; init; }

    public string StandardOutput { get; init; } = string.Empty;

    public string StandardError { get; init; } = string.Empty;
}

public sealed record HyperVVmSnapshot
{
    public int Sequence { get; init; }

    public DateTimeOffset ObservedAtUtc { get; init; }

    public string State { get; init; } = string.Empty;

    public long UptimeMilliseconds { get; init; }

    public string Heartbeat { get; init; } = string.Empty;

    public List<string> IpAddresses { get; init; } = [];
}

public sealed record HyperVOfficeLiteCycleOutcome(HyperVOfficeLiteCycleReceipt Receipt)
{
    public int ExitCode => Receipt.Payload.TerminalClassification switch
    {
        EnvironmentTerminalClassification.Ready => 0,
        EnvironmentTerminalClassification.Failed => 2,
        _ => 3
    };
}

public sealed class HyperVOfficeLiteCycleRunner
{
    private readonly IHyperVOfficeLitePlatform _platform;
    private readonly TimeProvider _timeProvider;

    public HyperVOfficeLiteCycleRunner()
        : this(new HyperVOfficeLitePlatform(), TimeProvider.System)
    {
    }

    internal HyperVOfficeLiteCycleRunner(IHyperVOfficeLitePlatform platform, TimeProvider timeProvider)
    {
        _platform = platform;
        _timeProvider = timeProvider;
    }

    public HyperVOfficeLiteCycleOutcome Run(HyperVOfficeLiteCycleRequest request, string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request, attemptId);

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var checks = new List<EnvironmentCheck>();
        var files = new List<EnvironmentFileObservation>();
        var commands = new List<HyperVCommandObservation>();
        var snapshots = new List<HyperVVmSnapshot>();
        var probes = new List<GuestProbeObservation>();
        var sideEffects = new List<string>();
        var unsupportedGaps = new List<string>
        {
            "Native KSS controller readiness is not inferred from Hyper-V state, heartbeat, IP address or TLS alone.",
            "This cycle does not load, compile or execute KRL and leaves nativeKssStatus=NotRun."
        };

        var templatePath = Path.GetFullPath(request.TemplateVhdPath);
        var vmRoot = Path.GetFullPath(request.VmRoot);
        var diskPath = Path.GetFullPath(request.DifferencingDiskPath);
        var expectedHash = request.ExpectedTemplateSha256.ToUpperInvariant();
        var templateHashBefore = string.Empty;
        var templateHashAfter = string.Empty;
        var templateUnchanged = false;
        var vmCreated = false;
        var started = false;
        var retainedStopped = false;
        var cleanShutdown = false;
        var guestTlsReady = false;
        var classification = HyperVOfficeLiteBootClassification.NotRun;
        var endpoint = new GuestEndpointObservation { Port = request.GuestTlsPort };

        if (!OperatingSystem.IsWindows())
        {
            checks.Add(Failed("host-os", "Hyper-V OfficeLite lifecycle requires Windows."));
            return Complete();
        }

        checks.Add(Passed("host-os", RuntimeInformation.OSDescription));
        var preflight = _platform.Preflight(request);
        commands.Add(preflight.Command);
        if (!preflight.Command.Succeeded || preflight.Value is null)
        {
            classification = HyperVOfficeLiteBootClassification.HypervisorBlocked;
            checks.Add(Blocked("hyperv-preflight", "Hyper-V preflight could not complete: " + preflight.Command.Detail));
            return Complete();
        }

        AddPreflightChecks(preflight.Value, checks);
        if (!preflight.Value.Ready || preflight.Value.VmExists)
        {
            classification = HyperVOfficeLiteBootClassification.HypervisorBlocked;
            return Complete();
        }

        if (!request.AllowHostChange || string.IsNullOrWhiteSpace(request.AuthorizationReference))
        {
            classification = HyperVOfficeLiteBootClassification.HypervisorBlocked;
            checks.Add(Blocked(
                "host-change-authorization",
                "Creating a differencing disk and Hyper-V VM requires --allow-host-change true and a non-empty authorization reference."));
            return Complete();
        }

        checks.Add(Passed("host-change-authorization", "External reversible Hyper-V changes are explicitly authorized."));
        if (File.Exists(diskPath))
        {
            classification = HyperVOfficeLiteBootClassification.HypervisorBlocked;
            checks.Add(Blocked("differencing-disk-new", "The requested differencing-disk path already exists."));
            return Complete();
        }

        checks.Add(Passed("differencing-disk-new", "The differencing-disk target is create-new."));
        if (!TryInventoryTemplate(templatePath, files, checks, out templateHashBefore))
        {
            return Complete();
        }

        if (!string.Equals(templateHashBefore, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            checks.Add(Blocked("template-vhd-identity", "The template VHD hash does not match the authorized fixed-VHD identity."));
            return Complete();
        }

        checks.Add(Passed("template-vhd-identity", "The fixed VHD matches the authorized SHA-256."));
        var create = _platform.CreateVm(request);
        commands.Add(create);
        if (!create.Succeeded)
        {
            classification = HyperVOfficeLiteBootClassification.BootFailed;
            checks.Add(Failed("hyperv-vm-create", "Hyper-V VM creation failed: " + create.Detail));
            return Complete();
        }

        vmCreated = true;
        unsupportedGaps.Add("The created VM is retained in a stopped state for the bounded smartHMI/KRC readiness observation.");
        sideEffects.Add($"CreateDifferencingDisk:{diskPath}");
        sideEffects.Add($"RegisterHyperVGeneration1Vm:{request.VmName}");
        checks.Add(Passed("hyperv-vm-create", "Created the Generation-1 VM over a differencing child; the fixed VHD remains the parent."));

        try
        {
            var start = _platform.StartVm(request);
            commands.Add(start);
            if (!start.Succeeded)
            {
                classification = HyperVOfficeLiteBootClassification.BootFailed;
                checks.Add(Failed("hyperv-vm-start", "Hyper-V failed to start the created VM: " + start.Detail));
            }
            else
            {
                started = true;
                sideEffects.Add($"StartHyperVVm:{request.VmName}");
                checks.Add(Passed("hyperv-vm-start", "The created VM accepted exactly one start request."));
                classification = ObserveUntilTerminal(request, commands, snapshots, probes, ref endpoint, ref guestTlsReady);
                switch (classification)
                {
                    case HyperVOfficeLiteBootClassification.GuestNetworkReady:
                        checks.Add(Passed("guest-network-tls", "The guest reported an IPv4 address and exposed the expected KUKA TLS endpoint."));
                        break;
                    case HyperVOfficeLiteBootClassification.GuestRunningNoNetwork:
                        checks.Add(Blocked("guest-network-tls", "The guest remained running but did not expose an IPv4/TLS-ready endpoint before timeout."));
                        break;
                    default:
                        checks.Add(Failed("guest-boot", "The VM left the running/start path before guest network readiness."));
                        break;
                }
            }
        }
        finally
        {
            if (started)
            {
                var stop = _platform.StopVmSoft(request);
                commands.Add(stop);
                sideEffects.Add($"RequestHyperVGuestShutdown:{request.VmName}");
                var final = _platform.ObserveVm(request, _timeProvider.GetUtcNow());
                commands.Add(final.Command);
                if (final.Value is not null)
                {
                    snapshots.Add(final.Value with
                    {
                        Sequence = snapshots.Count + 1,
                        ObservedAtUtc = _timeProvider.GetUtcNow()
                    });
                }

                cleanShutdown = stop.CommandSucceeded
                    && final.Value is not null
                    && string.Equals(final.Value.State, "Off", StringComparison.OrdinalIgnoreCase);
                checks.Add(cleanShutdown
                    ? Passed("hyperv-vm-soft-shutdown", "Guest shutdown completed and the retained VM is Off.")
                    : Failed("hyperv-vm-soft-shutdown", "A soft shutdown and final Off state could not both be verified."));
                retainedStopped = cleanShutdown;
            }
        }

        try
        {
            templateHashAfter = ComputeFileSha256(templatePath);
            templateUnchanged = string.Equals(templateHashBefore, templateHashAfter, StringComparison.OrdinalIgnoreCase)
                && string.Equals(expectedHash, templateHashAfter, StringComparison.OrdinalIgnoreCase);
            checks.Add(templateUnchanged
                ? Passed("template-vhd-unchanged", "The fixed parent VHD retained its authorized SHA-256 after the cycle.")
                : Failed("template-vhd-unchanged", "The fixed parent VHD changed during the Hyper-V cycle."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            checks.Add(Failed("template-vhd-unchanged", "The fixed parent VHD could not be rehashed: " + exception.Message));
        }

        TryInventoryGeneratedDisk(diskPath, files, checks);
        return Complete();

        HyperVOfficeLiteCycleOutcome Complete()
        {
            stopwatch.Stop();
            var terminal = checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
                ? EnvironmentTerminalClassification.Failed
                : checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked)
                    ? EnvironmentTerminalClassification.Blocked
                    : EnvironmentTerminalClassification.Ready;
            var createFailed = checks.Any(check =>
                check.Id == "hyperv-vm-create" && check.Status == EnvironmentCheckStatus.Failed);
            var noMutation = !vmCreated && !createFailed;
            var environmentReusable = noMutation || (cleanShutdown && templateUnchanged && retainedStopped);
            var payload = new HyperVOfficeLiteCyclePayload
            {
                ReceiptId = $"hyperv-officelite-cycle-{attemptId}",
                AttemptId = attemptId,
                CoreAssemblySha256 = ComputeFileSha256(typeof(HyperVOfficeLiteCycleRunner).Assembly.Location),
                Runtime = new RuntimeEnvironment
                {
                    OsDescription = RuntimeInformation.OSDescription,
                    FrameworkDescription = RuntimeInformation.FrameworkDescription,
                    ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
                },
                StartedAtUtc = startedAt,
                CompletedAtUtc = _timeProvider.GetUtcNow(),
                DurationMilliseconds = Math.Max(0, stopwatch.ElapsedMilliseconds),
                TerminalClassification = terminal,
                BootClassification = classification,
                TemplateVhdPath = templatePath,
                ExpectedTemplateSha256 = expectedHash,
                TemplateSha256Before = templateHashBefore,
                TemplateSha256After = templateHashAfter,
                TemplateUnchanged = templateUnchanged,
                VmRoot = vmRoot,
                VmName = request.VmName,
                SwitchName = request.SwitchName,
                DifferencingDiskPath = diskPath,
                Generation = request.Generation,
                ProcessorCount = request.ProcessorCount,
                MemoryStartupBytes = request.MemoryStartupBytes,
                HostChangeAuthorized = request.AllowHostChange,
                AuthorizationReference = request.AuthorizationReference,
                VmCreatedByThisAttempt = vmCreated,
                StartedByThisAttempt = started,
                VmRetainedStopped = retainedStopped,
                GuestTlsReady = guestTlsReady,
                ControllerReady = false,
                CleanShutdownVerified = cleanShutdown,
                EnvironmentReusable = environmentReusable,
                NativeKssStatus = NativeKssStatus.NotRun,
                GuestEndpoint = endpoint,
                Checks = checks,
                Files = files,
                Commands = commands,
                Snapshots = snapshots,
                Probes = probes,
                SideEffects = sideEffects,
                UnsupportedGaps = unsupportedGaps
            };
            var receipt = new HyperVOfficeLiteCycleReceipt
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
            };
            return new HyperVOfficeLiteCycleOutcome(receipt);
        }
    }

    private HyperVOfficeLiteBootClassification ObserveUntilTerminal(
        HyperVOfficeLiteCycleRequest request,
        List<HyperVCommandObservation> commands,
        List<HyperVVmSnapshot> snapshots,
        List<GuestProbeObservation> probes,
        ref GuestEndpointObservation endpoint,
        ref bool guestTlsReady)
    {
        var deadline = _timeProvider.GetUtcNow().AddSeconds(request.ReadinessTimeoutSeconds);
        while (_timeProvider.GetUtcNow() <= deadline)
        {
            var observedAt = _timeProvider.GetUtcNow();
            var observation = _platform.ObserveVm(request, observedAt);
            commands.Add(observation.Command);
            if (!observation.Command.Succeeded || observation.Value is null)
            {
                return HyperVOfficeLiteBootClassification.BootFailed;
            }

            var snapshot = observation.Value with
            {
                Sequence = snapshots.Count + 1,
                ObservedAtUtc = observedAt
            };
            snapshots.Add(snapshot);
            if (string.Equals(snapshot.State, "Off", StringComparison.OrdinalIgnoreCase))
            {
                return HyperVOfficeLiteBootClassification.BootFailed;
            }

            var ipv4 = snapshot.IpAddresses.FirstOrDefault(IsUsableIpv4);
            var probe = _platform.ProbeGuest(
                ipv4,
                request.GuestTlsPort,
                request.ProbeTimeoutMilliseconds,
                observedAt);
            probes.Add(probe.ToObservation() with { Sequence = probes.Count + 1 });
            if (probe.TlsConnected)
            {
                guestTlsReady = true;
                endpoint = new GuestEndpointObservation
                {
                    Address = ipv4,
                    Port = request.GuestTlsPort,
                    HostName = probe.TlsIdentity?.HostName,
                    CertificateSubject = probe.TlsIdentity?.Subject,
                    CertificateIssuer = probe.TlsIdentity?.Issuer,
                    CertificateThumbprint = probe.TlsIdentity?.Thumbprint,
                    TlsProtocol = probe.TlsIdentity?.Protocol,
                    SelfSignedCertificateObserved = probe.TlsIdentity?.SelfSigned ?? false,
                    CertificatePolicyBypassedForObservation = true
                };
                return HyperVOfficeLiteBootClassification.GuestNetworkReady;
            }

            _platform.Delay(TimeSpan.FromMilliseconds(request.ProbeIntervalMilliseconds));
        }

        return HyperVOfficeLiteBootClassification.GuestRunningNoNetwork;
    }

    private static void AddPreflightChecks(HyperVPreflightState state, List<EnvironmentCheck> checks)
    {
        var accessContext = $"elevated={state.IsElevated}";
        var accessError = string.IsNullOrWhiteSpace(state.AccessError)
            ? string.Empty
            : $" Error: {state.AccessError}";
        var parentError = string.IsNullOrWhiteSpace(state.ParentError)
            ? string.Empty
            : $" Error: {state.ParentError}";

        checks.Add(state.ModuleAvailable
            ? Passed("hyperv-module", "The Hyper-V PowerShell module is available.")
            : Blocked("hyperv-module", "The Hyper-V PowerShell module is unavailable; complete the planned restart first."));
        checks.Add(state.VmmsRunning
            ? Passed("hyperv-vmms", "Hyper-V Virtual Machine Management is running.")
            : Blocked("hyperv-vmms", "Hyper-V Virtual Machine Management is not running."));
        checks.Add(state.Accessible
            ? Passed("hyperv-access", $"The current process can query Hyper-V state; {accessContext}.")
            : Blocked("hyperv-access", $"The current process cannot query Hyper-V state; {accessContext}.{accessError}"));
        checks.Add(state.SwitchExists
            ? Passed("hyperv-switch", "The requested existing virtual switch is available.")
            : Blocked("hyperv-switch", "The requested existing virtual switch is unavailable; no switch was created."));
        checks.Add(!state.Accessible
            ? Blocked("hyperv-vm-exclusive", "VM-name exclusivity cannot be established without Hyper-V query access.")
            : state.VmExists
                ? Blocked("hyperv-vm-exclusive", "A VM with the requested name already exists; this attempt did not take ownership.")
                : Passed("hyperv-vm-exclusive", "No VM with the requested name exists."));
        checks.Add(!state.Accessible
            ? Blocked("hyperv-parent-readable", "Parent-disk recognition cannot be established without Hyper-V query access.")
            : state.ParentReadable
                ? Passed("hyperv-parent-readable", "Hyper-V recognizes the requested parent disk.")
                : Blocked("hyperv-parent-readable", $"Hyper-V could not inspect the requested parent disk.{parentError}"));
        checks.Add(!state.ParentReadable
            ? Blocked("hyperv-parent-profile", "Parent-disk format/type are unavailable.")
            : state.ParentProfileAccepted
                ? Passed("hyperv-parent-profile", "Hyper-V reports the parent as a fixed VHD.")
                : Blocked("hyperv-parent-profile", $"Expected fixed VHD, observed {state.ParentFormat}/{state.ParentType}."));
    }

    private static bool TryInventoryTemplate(
        string path,
        List<EnvironmentFileObservation> files,
        List<EnvironmentCheck> checks,
        out string sha256)
    {
        sha256 = string.Empty;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                checks.Add(Failed("template-vhd", "The fixed template VHD does not exist."));
                return false;
            }

            sha256 = ComputeFileSha256(path);
            files.Add(new EnvironmentFileObservation
            {
                Id = "hyperv-template-vhd",
                Path = path,
                Exists = true,
                Bytes = info.Length,
                Sha256 = sha256
            });
            checks.Add(Passed("template-vhd", "The fixed template VHD exists and was hashed."));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            checks.Add(Failed("template-vhd", "The fixed template VHD could not be read: " + exception.Message));
            return false;
        }
    }

    private static void TryInventoryGeneratedDisk(
        string path,
        List<EnvironmentFileObservation> files,
        List<EnvironmentCheck> checks)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                checks.Add(Failed("differencing-disk-retained", "The created differencing disk is missing after the cycle."));
                return;
            }

            files.Add(new EnvironmentFileObservation
            {
                Id = "hyperv-differencing-disk",
                Path = path,
                Exists = true,
                Bytes = info.Length,
                Sha256 = null
            });
            checks.Add(Passed("differencing-disk-retained", "The generated differencing disk remains attached to the retained stopped VM."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            checks.Add(Failed("differencing-disk-retained", "The generated differencing disk could not be inventoried: " + exception.Message));
        }
    }

    private static bool IsUsableIpv4(string value) =>
        IPAddress.TryParse(value, out var address)
        && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
        && !IPAddress.IsLoopback(address)
        && !value.StartsWith("169.254.", StringComparison.Ordinal);

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void ValidateRequest(HyperVOfficeLiteCycleRequest request, string attemptId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptId);
        if (!Regex.IsMatch(attemptId, "^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("Attempt ID must match [A-Za-z0-9][A-Za-z0-9._-]{2,127}.", nameof(attemptId));
        }

        if (!Regex.IsMatch(request.VmName, "^[A-Za-z0-9][A-Za-z0-9._-]{2,63}$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("VM name must match [A-Za-z0-9][A-Za-z0-9._-]{2,63}.", nameof(request));
        }

        if (request.ExpectedTemplateSha256.Length != 64
            || request.ExpectedTemplateSha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("Expected template SHA-256 must contain exactly 64 hexadecimal characters.", nameof(request));
        }

        if (request.Generation != HyperVOfficeLiteCycleContract.Generation
            || request.ProcessorCount != HyperVOfficeLiteCycleContract.ProcessorCount
            || request.MemoryStartupBytes != HyperVOfficeLiteCycleContract.MemoryStartupBytes)
        {
            throw new ArgumentException("The accepted OfficeLite Hyper-V profile is Generation 1, 2 vCPU and 4096 MiB fixed memory.", nameof(request));
        }

        if (request.GuestTlsPort is < 1 or > 65535
            || request.ReadinessTimeoutSeconds is < 1 or > 900
            || request.ProbeIntervalMilliseconds is < 100 or > 30000
            || request.ProbeTimeoutMilliseconds is < 100 or > 10000
            || request.PowerShellTimeoutSeconds is < 1 or > 300
            || request.ShutdownTimeoutSeconds is < 1 or > 300)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Hyper-V lifecycle timing or port settings are outside safe bounds.");
        }

        var normalizedRoot = Path.GetFullPath(request.VmRoot).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedDisk = Path.GetFullPath(request.DifferencingDiskPath);
        if (!normalizedDisk.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The differencing disk must stay under the declared VM root.", nameof(request));
        }

        if (!string.Equals(Path.GetExtension(Path.GetFullPath(request.TemplateVhdPath)), ".vhd", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The accepted OfficeLite fixed parent must use the VHD format for Generation-1 IDE boot.", nameof(request));
        }

        if (!string.Equals(Path.GetExtension(normalizedDisk), ".vhd", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The generated differencing disk must use the same VHD format as the fixed parent.", nameof(request));
        }
    }

    private static EnvironmentCheck Passed(string id, string detail) => new()
    {
        Id = id,
        Status = EnvironmentCheckStatus.Passed,
        Detail = detail
    };

    private static EnvironmentCheck Blocked(string id, string detail) => new()
    {
        Id = id,
        Status = EnvironmentCheckStatus.Blocked,
        Detail = detail
    };

    private static EnvironmentCheck Failed(string id, string detail) => new()
    {
        Id = id,
        Status = EnvironmentCheckStatus.Failed,
        Detail = detail
    };
}

public sealed record HyperVOfficeLiteCycleVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}

public static class HyperVOfficeLiteCycleReceiptVerifier
{
    public static HyperVOfficeLiteCycleVerificationResult Verify(HyperVOfficeLiteCycleReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (!string.Equals(receipt.SchemaIdentity, HyperVOfficeLiteCycleContract.ReceiptSchemaIdentity, StringComparison.Ordinal))
        {
            errors.Add($"schemaIdentity must be {HyperVOfficeLiteCycleContract.ReceiptSchemaIdentity}");
        }

        if (receipt.SchemaVersion != HyperVOfficeLiteCycleContract.ReceiptSchemaVersion)
        {
            errors.Add($"schemaVersion must be {HyperVOfficeLiteCycleContract.ReceiptSchemaVersion}");
        }

        if (ReferenceEquals(receipt.Payload, null))
        {
            return new HyperVOfficeLiteCycleVerificationResult
            {
                Succeeded = false,
                PayloadSha256 = receipt.PayloadSha256,
                Errors = ["payload is required"]
            };
        }

        var payload = receipt.Payload;
        var payloadChecks = payload.Checks ?? [];
        if (string.IsNullOrWhiteSpace(payload.ReceiptId) || string.IsNullOrWhiteSpace(payload.AttemptId))
        {
            errors.Add("receiptId and attemptId are required");
        }

        if (!IsSha256(payload.CoreAssemblySha256)
            || !IsSha256(payload.ExpectedTemplateSha256)
            || (payload.TemplateSha256Before.Length > 0 && !IsSha256(payload.TemplateSha256Before))
            || (payload.TemplateSha256After.Length > 0 && !IsSha256(payload.TemplateSha256After)))
        {
            errors.Add("core/template SHA-256 fields are invalid");
        }

        if (payload.Generation != HyperVOfficeLiteCycleContract.Generation
            || payload.ProcessorCount != HyperVOfficeLiteCycleContract.ProcessorCount
            || payload.MemoryStartupBytes != HyperVOfficeLiteCycleContract.MemoryStartupBytes)
        {
            errors.Add("Hyper-V VM profile must be Generation 1, 2 vCPU and 4096 MiB");
        }

        if (payload.Checks is null || payload.Checks.Count == 0
            || payload.Files is null
            || payload.Commands is null
            || payload.Snapshots is null
            || payload.Probes is null
            || payload.SideEffects is null
            || payload.UnsupportedGaps is null)
        {
            errors.Add("receipt evidence collections cannot be null or empty where required");
        }
        else if (payloadChecks.Select(check => check.Id).Distinct(StringComparer.Ordinal).Count() != payloadChecks.Count)
        {
            errors.Add("check IDs must be unique");
        }

        if (payload.StartedAtUtc > payload.CompletedAtUtc || payload.DurationMilliseconds < 0)
        {
            errors.Add("receipt timing is invalid");
        }

        if (payload.ControllerReady || payload.NativeKssStatus != NativeKssStatus.NotRun)
        {
            errors.Add("schema v1 cannot claim native KSS controller readiness or execution");
        }

        if (payload.VmCreatedByThisAttempt && (!payload.HostChangeAuthorized || string.IsNullOrWhiteSpace(payload.AuthorizationReference)))
        {
            errors.Add("VM creation requires explicit host-change authorization evidence");
        }

        if (payload.StartedByThisAttempt && !payload.VmCreatedByThisAttempt)
        {
            errors.Add("a started VM must have been created by this exclusive attempt");
        }

        if (payload.TemplateUnchanged != (
                string.Equals(payload.ExpectedTemplateSha256, payload.TemplateSha256Before, StringComparison.OrdinalIgnoreCase)
                && string.Equals(payload.TemplateSha256Before, payload.TemplateSha256After, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("templateUnchanged does not match the recorded hashes");
        }

        var expectedTerminal = payloadChecks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
            ? EnvironmentTerminalClassification.Failed
            : payloadChecks.Any(check => check.Status == EnvironmentCheckStatus.Blocked)
                ? EnvironmentTerminalClassification.Blocked
                : EnvironmentTerminalClassification.Ready;
        if (payload.TerminalClassification != expectedTerminal)
        {
            errors.Add("terminalClassification does not match check severity");
        }

        if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready
            && (payload.BootClassification != HyperVOfficeLiteBootClassification.GuestNetworkReady
                || !payload.GuestTlsReady
                || !payload.CleanShutdownVerified
                || !payload.VmRetainedStopped
                || !payload.EnvironmentReusable
                || !payload.TemplateUnchanged))
        {
            errors.Add("Ready requires guest TLS, clean retained-Off cleanup and an unchanged fixed parent VHD");
        }

        if (!string.Equals(
                receipt.PayloadSha256,
                ReceiptSerialization.ComputeCanonicalSha256(payload),
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("payloadSha256 does not match the canonical payload");
        }

        return new HyperVOfficeLiteCycleVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);
}

internal sealed record HyperVPreflightState(
    bool ModuleAvailable,
    bool VmmsRunning,
    bool Accessible,
    bool SwitchExists,
    bool VmExists,
    bool ParentReadable,
    string ParentFormat,
    string ParentType)
{
    public bool IsElevated { get; init; }

    public string Identity { get; init; } = string.Empty;

    public string AccessError { get; init; } = string.Empty;

    public string ParentError { get; init; } = string.Empty;

    public bool ParentProfileAccepted =>
        string.Equals(ParentFormat, "VHD", StringComparison.OrdinalIgnoreCase)
        && string.Equals(ParentType, "Fixed", StringComparison.OrdinalIgnoreCase);

    public bool Ready => ModuleAvailable
        && VmmsRunning
        && Accessible
        && SwitchExists
        && !VmExists
        && ParentReadable
        && ParentProfileAccepted;
}

internal sealed record HyperVPlatformResult<T>(T? Value, HyperVCommandResult Command);

internal sealed record HyperVCommandResult(
    string Name,
    int ExitCode,
    bool TimedOut,
    long DurationMilliseconds,
    string StandardOutput,
    string StandardError)
{
    public bool Succeeded => ExitCode == 0 && !TimedOut;

    public bool CommandSucceeded => Succeeded;

    public string Detail => string.IsNullOrWhiteSpace(StandardError) ? StandardOutput : StandardError;

    public static implicit operator HyperVCommandObservation(HyperVCommandResult result) => new()
    {
        Name = result.Name,
        ExitCode = result.ExitCode,
        TimedOut = result.TimedOut,
        DurationMilliseconds = result.DurationMilliseconds,
        StandardOutput = result.StandardOutput,
        StandardError = result.StandardError
    };
}

internal interface IHyperVOfficeLitePlatform
{
    HyperVPlatformResult<HyperVPreflightState> Preflight(HyperVOfficeLiteCycleRequest request);

    HyperVCommandResult CreateVm(HyperVOfficeLiteCycleRequest request);

    HyperVCommandResult StartVm(HyperVOfficeLiteCycleRequest request);

    HyperVPlatformResult<HyperVVmSnapshot> ObserveVm(HyperVOfficeLiteCycleRequest request, DateTimeOffset observedAtUtc);

    GuestProbeResult ProbeGuest(string? address, int port, int timeoutMilliseconds, DateTimeOffset observedAtUtc);

    HyperVCommandResult StopVmSoft(HyperVOfficeLiteCycleRequest request);

    void Delay(TimeSpan duration);
}

internal sealed class HyperVOfficeLitePlatform : IHyperVOfficeLitePlatform
{
    private readonly OfficeLiteHostPlatform _guestPlatform = new();

    public HyperVPlatformResult<HyperVPreflightState> Preflight(HyperVOfficeLiteCycleRequest request)
    {
        var script = $$"""
            $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
            $principal = [Security.Principal.WindowsPrincipal]::new($identity)
            $isElevated = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
            $moduleAvailable = [bool](Get-Module -ListAvailable -Name Hyper-V)
            $vmms = Get-Service -Name vmms -ErrorAction SilentlyContinue
            $accessible = $false
            $accessError = ''
            $switchExists = $false
            $vmExists = $false
            $parentReadable = $false
            $parentFormat = ''
            $parentType = ''
            $parentError = ''
            if ($moduleAvailable) {
              try {
                $accessStage = 'import-module'
                Import-Module Hyper-V -ErrorAction Stop
                $accessStage = 'get-vm-all'
                $null = @(Get-VM -ErrorAction Stop)
                $accessStage = 'get-switch-by-name'
                $switchExists = [bool](Get-VMSwitch -Name {{PowerShellString(request.SwitchName)}} -ErrorAction SilentlyContinue)
                $accessStage = 'get-vm-by-name'
                $vmExists = [bool](Get-VM -Name {{PowerShellString(request.VmName)}} -ErrorAction SilentlyContinue)
                $accessible = $true
              } catch {
                $accessible = $false
                $accessError = "${accessStage}: $($_.Exception.Message)"
              }
              if ($accessible) {
                try {
                  $parentStage = 'get-vhd'
                  $parent = Get-VHD -Path {{PowerShellString(Path.GetFullPath(request.TemplateVhdPath))}} -ErrorAction Stop
                  $parentReadable = $true
                  $parentFormat = [string]$parent.VhdFormat
                  $parentType = [string]$parent.VhdType
                } catch {
                  $parentReadable = $false
                  $parentError = "${parentStage}: $($_.Exception.Message)"
                }
              }
            }
            [pscustomobject]@{
              isElevated = $isElevated
              identity = $identity.Name
              moduleAvailable = $moduleAvailable
              vmmsRunning = [bool]($vmms -and $vmms.Status -eq 'Running')
              accessible = $accessible
              accessError = $accessError
              switchExists = $switchExists
              vmExists = $vmExists
              parentReadable = $parentReadable
              parentFormat = $parentFormat
              parentType = $parentType
              parentError = $parentError
            } | ConvertTo-Json -Compress
            """;
        var command = RunPowerShell("hyperv-preflight", script, TimeSpan.FromSeconds(request.PowerShellTimeoutSeconds));
        return new HyperVPlatformResult<HyperVPreflightState>(TryDeserialize<HyperVPreflightState>(command), command);
    }

    public HyperVCommandResult CreateVm(HyperVOfficeLiteCycleRequest request)
    {
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            Import-Module Hyper-V -ErrorAction Stop
            $vmName = {{PowerShellString(request.VmName)}}
            $vmRoot = {{PowerShellString(Path.GetFullPath(request.VmRoot))}}
            $template = {{PowerShellString(Path.GetFullPath(request.TemplateVhdPath))}}
            $child = {{PowerShellString(Path.GetFullPath(request.DifferencingDiskPath))}}
            $switch = {{PowerShellString(request.SwitchName)}}
            if (Get-VM -Name $vmName -ErrorAction SilentlyContinue) { throw 'VM already exists.' }
            if (-not (Get-VMSwitch -Name $switch -ErrorAction SilentlyContinue)) { throw 'Requested switch is missing.' }
            if (Test-Path -LiteralPath $child) { throw 'Differencing disk already exists.' }
            $childDirectory = Split-Path -Parent $child
            $childDirectoryExisted = Test-Path -LiteralPath $childDirectory
            $vmCreated = $false
            try {
              New-Item -ItemType Directory -Path $childDirectory -Force | Out-Null
              New-VHD -Path $child -ParentPath $template -Differencing -ErrorAction Stop | Out-Null
              New-VM -Name $vmName -Generation 1 -MemoryStartupBytes {{request.MemoryStartupBytes}} -VHDPath $child -SwitchName $switch -Path $vmRoot -ErrorAction Stop | Out-Null
              $vmCreated = $true
              Set-VMProcessor -VMName $vmName -Count {{request.ProcessorCount}} -ErrorAction Stop
              Set-VMMemory -VMName $vmName -DynamicMemoryEnabled $false -StartupBytes {{request.MemoryStartupBytes}} -ErrorAction Stop
              Set-VM -Name $vmName -AutomaticCheckpointsEnabled $false -AutomaticStartAction Nothing -AutomaticStopAction ShutDown -ErrorAction Stop
              [pscustomobject]@{ created = $true; vmName = $vmName; child = $child } | ConvertTo-Json -Compress
            } catch {
              if ($vmCreated) { Remove-VM -Name $vmName -Force -ErrorAction SilentlyContinue }
              Remove-Item -LiteralPath $child -Force -ErrorAction SilentlyContinue
              if (-not $childDirectoryExisted -and (Test-Path -LiteralPath $childDirectory) -and -not (Get-ChildItem -LiteralPath $childDirectory -Force)) {
                Remove-Item -LiteralPath $childDirectory -Force -ErrorAction SilentlyContinue
              }
              throw
            }
            """;
        return RunPowerShell("hyperv-create-vm", script, TimeSpan.FromSeconds(request.PowerShellTimeoutSeconds));
    }

    public HyperVCommandResult StartVm(HyperVOfficeLiteCycleRequest request)
    {
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            Import-Module Hyper-V -ErrorAction Stop
            Start-VM -Name {{PowerShellString(request.VmName)}} -ErrorAction Stop | Out-Null
            [pscustomobject]@{ started = $true } | ConvertTo-Json -Compress
            """;
        return RunPowerShell("hyperv-start-vm", script, TimeSpan.FromSeconds(request.PowerShellTimeoutSeconds));
    }

    public HyperVPlatformResult<HyperVVmSnapshot> ObserveVm(
        HyperVOfficeLiteCycleRequest request,
        DateTimeOffset observedAtUtc)
    {
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            Import-Module Hyper-V -ErrorAction Stop
            $vm = Get-VM -Name {{PowerShellString(request.VmName)}} -ErrorAction Stop
            $ips = @(Get-VMNetworkAdapter -VMName $vm.Name -ErrorAction SilentlyContinue | ForEach-Object { $_.IPAddresses })
            $heartbeat = Get-VMIntegrationService -VMName $vm.Name -Name Heartbeat -ErrorAction SilentlyContinue
            [pscustomobject]@{
              sequence = 0
              observedAtUtc = {{PowerShellString(observedAtUtc.ToUniversalTime().ToString("o"))}}
              state = [string]$vm.State
              uptimeMilliseconds = [long]$vm.Uptime.TotalMilliseconds
              heartbeat = if ($heartbeat) { [string]$heartbeat.PrimaryStatusDescription } else { '' }
              ipAddresses = @($ips)
            } | ConvertTo-Json -Compress
            """;
        var command = RunPowerShell("hyperv-observe-vm", script, TimeSpan.FromSeconds(request.PowerShellTimeoutSeconds));
        return new HyperVPlatformResult<HyperVVmSnapshot>(TryDeserialize<HyperVVmSnapshot>(command), command);
    }

    public GuestProbeResult ProbeGuest(
        string? address,
        int port,
        int timeoutMilliseconds,
        DateTimeOffset observedAtUtc) =>
        _guestPlatform.ProbeGuest(address, port, timeoutMilliseconds, observedAtUtc);

    public HyperVCommandResult StopVmSoft(HyperVOfficeLiteCycleRequest request)
    {
        var script = CreateSoftStopScript(request);
        return RunPowerShell(
            "hyperv-stop-vm-soft",
            script,
            TimeSpan.FromSeconds(request.ShutdownTimeoutSeconds + 15));
    }

    internal static string CreateSoftStopScript(HyperVOfficeLiteCycleRequest request) =>
        $$"""
            $ErrorActionPreference = 'Stop'
            Import-Module Hyper-V -ErrorAction Stop
            $vmName = {{PowerShellString(request.VmName)}}
            $vm = Get-VM -Name $vmName -ErrorAction Stop
            if ($vm.State -ne 'Off') {
              Stop-VM -Name $vmName -ErrorAction Stop
            }
            $deadline = [DateTime]::UtcNow.AddSeconds({{request.ShutdownTimeoutSeconds}})
            do {
              $vm = Get-VM -Name $vmName -ErrorAction Stop
              if ($vm.State -eq 'Off') { break }
              Start-Sleep -Milliseconds 500
            } while ([DateTime]::UtcNow -lt $deadline)
            if ($vm.State -ne 'Off') { throw 'Soft shutdown did not reach Off before timeout.' }
            [pscustomobject]@{ stopped = $true; state = [string]$vm.State } | ConvertTo-Json -Compress
            """;

    public void Delay(TimeSpan duration) => Thread.Sleep(duration);

    private static HyperVCommandResult RunPowerShell(string name, string script, TimeSpan timeout)
    {
        var prelude = "[Console]::OutputEncoding=[System.Text.UTF8Encoding]::new($false);"
            + "$ProgressPreference='SilentlyContinue';";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(prelude + script));
        var startInfo = CreatePowerShellStartInfo(encoded);

        try
        {
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
            return new HyperVCommandResult(
                name,
                exited ? process.ExitCode : -1,
                !exited,
                stopwatch.ElapsedMilliseconds,
                standardOutput.GetAwaiter().GetResult().Trim(),
                standardError.GetAwaiter().GetResult().Trim());
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or Win32Exception)
        {
            return new HyperVCommandResult(name, -1, false, 0, string.Empty, $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    internal static ProcessStartInfo CreatePowerShellStartInfo(string encodedCommand)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(encodedCommand);
        return startInfo;
    }

    private static T? TryDeserialize<T>(HyperVCommandResult command)
    {
        if (!command.Succeeded || string.IsNullOrWhiteSpace(command.StandardOutput))
        {
            return default;
        }

        try
        {
            var json = command.StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Last();
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return default;
        }
    }

    internal static string PowerShellString(string value)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        return "([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" + encoded + "')))";
    }
}
