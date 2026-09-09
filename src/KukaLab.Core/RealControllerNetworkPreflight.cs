using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class RealControllerNetworkPreflightContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.real-controller-network-preflight-receipt";
    public const int ReceiptSchemaVersion = 1;

    internal static readonly IReadOnlyList<string> RequiredUnsupportedGaps =
    [
        "This preflight does not transmit packets to the controller or any other network device.",
        "This preflight does not change host or controller network configuration.",
        "This preflight does not prove controller reachability, project compatibility, motion safety or physical qualification."
    ];
}

public enum RealControllerNetworkPreflightDisposition
{
    ReadyForAuthorizedHostConfiguration,
    AlreadyConfigured,
    Blocked
}

public sealed record RealControllerNetworkPreflightRequest
{
    public required string TargetInterfaceAlias { get; init; }

    public required string InternetInterfaceAlias { get; init; }

    public required string ControllerAddress { get; init; }

    public required string ProposedHostAddress { get; init; }

    public int PrefixLength { get; init; } = 24;
}

public sealed record NetworkInterfaceObservation
{
    public string Alias { get; init; } = string.Empty;

    public bool Exists { get; init; }

    public string InterfaceType { get; init; } = string.Empty;

    public string OperationalStatus { get; init; } = string.Empty;

    public long LinkSpeedBitsPerSecond { get; init; }

    public bool? DhcpEnabled { get; init; }

    public List<string> Ipv4Addresses { get; init; } = [];

    public List<string> Ipv4DefaultGateways { get; init; } = [];
}

public sealed record RealControllerNetworkSnapshot
{
    public NetworkInterfaceObservation TargetInterface { get; init; } = new();

    public NetworkInterfaceObservation InternetInterface { get; init; } = new();
}

public sealed record RealControllerNetworkPreflightReceipt
{
    public string SchemaIdentity { get; init; } = RealControllerNetworkPreflightContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = RealControllerNetworkPreflightContract.ReceiptSchemaVersion;

    public required RealControllerNetworkPreflightPayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record RealControllerNetworkPreflightPayload
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

    public RealControllerNetworkPreflightDisposition Disposition { get; init; }

    public string ControllerAddress { get; init; } = string.Empty;

    public string ProposedHostAddress { get; init; } = string.Empty;

    public int PrefixLength { get; init; }

    public bool HostConfigurationChangeRequired { get; init; }

    public string RollbackMode { get; init; } = "RestoreDhcpAndAutomaticDns";

    public RealControllerNetworkSnapshot Snapshot { get; init; } = new();

    public bool NetworkTrafficSent { get; init; }

    public bool HostConfigurationChanged { get; init; }

    public bool ControllerConfigurationChanged { get; init; }

    public NativeKssStatus NativeKssStatus { get; init; } = NativeKssStatus.NotRun;

    public List<EnvironmentCheck> Checks { get; init; } = [];

    public List<string> SideEffects { get; init; } = [];

    public bool EnvironmentReusable { get; init; } = true;

    public List<string> UnsupportedGaps { get; init; } = [];
}

public sealed record RealControllerNetworkPreflightOutcome(RealControllerNetworkPreflightReceipt Receipt)
{
    public int ExitCode => Receipt.Payload.TerminalClassification switch
    {
        EnvironmentTerminalClassification.Ready => 0,
        EnvironmentTerminalClassification.Failed => 2,
        _ => 3
    };
}

public sealed class RealControllerNetworkPreflightRunner
{
    private static readonly Regex AttemptIdPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private readonly IRealControllerNetworkPlatform _platform;
    private readonly TimeProvider _timeProvider;

    public RealControllerNetworkPreflightRunner()
        : this(new RealControllerNetworkPlatform(), TimeProvider.System)
    {
    }

    internal RealControllerNetworkPreflightRunner(
        IRealControllerNetworkPlatform platform,
        TimeProvider timeProvider)
    {
        _platform = platform;
        _timeProvider = timeProvider;
    }

    public RealControllerNetworkPreflightOutcome Run(
        RealControllerNetworkPreflightRequest request,
        string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptId);
        if (!AttemptIdPattern.IsMatch(attemptId))
        {
            throw new ArgumentException(
                "Attempt ID must match [A-Za-z0-9][A-Za-z0-9._-]{2,127}.",
                nameof(attemptId));
        }

        var addressing = ValidateRequest(request);
        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var checks = new List<EnvironmentCheck>();
        var snapshot = new RealControllerNetworkSnapshot();

        if (!OperatingSystem.IsWindows())
        {
            checks.Add(Blocked("host-os", "Real-controller network preflight currently requires Windows."));
        }
        else
        {
            checks.Add(Passed("host-os", RuntimeInformation.OSDescription));
            try
            {
                snapshot = _platform.Capture(
                    request.TargetInterfaceAlias,
                    request.InternetInterfaceAlias);
                EvaluateSnapshot(request, addressing, snapshot, checks);
            }
            catch (Exception exception) when (exception is NetworkInformationException
                or InvalidOperationException
                or SocketException)
            {
                checks.Add(Failed("network-snapshot", $"Local interface state could not be read: {exception.Message}"));
            }
        }

        stopwatch.Stop();
        var terminal = checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
            ? EnvironmentTerminalClassification.Failed
            : checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked)
                ? EnvironmentTerminalClassification.Blocked
                : EnvironmentTerminalClassification.Ready;
        var targetAddresses = snapshot.TargetInterface.Ipv4Addresses
            .Select(ParseAddressOrNull)
            .Where(address => address is not null)
            .Cast<IPAddress>()
            .ToList();
        var alreadyConfigured = targetAddresses.Any(address => address.Equals(addressing.ProposedHostAddress));
        var disposition = terminal != EnvironmentTerminalClassification.Ready
            ? RealControllerNetworkPreflightDisposition.Blocked
            : alreadyConfigured
                ? RealControllerNetworkPreflightDisposition.AlreadyConfigured
                : RealControllerNetworkPreflightDisposition.ReadyForAuthorizedHostConfiguration;
        var completedAt = _timeProvider.GetUtcNow();
        var payload = new RealControllerNetworkPreflightPayload
        {
            ReceiptId = $"real-controller-network-preflight-{attemptId}",
            AttemptId = attemptId,
            CoreAssemblySha256 = ComputeFileSha256(typeof(RealControllerNetworkPreflightRunner).Assembly.Location),
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
            Disposition = disposition,
            ControllerAddress = addressing.ControllerAddress.ToString(),
            ProposedHostAddress = addressing.ProposedHostAddress.ToString(),
            PrefixLength = request.PrefixLength,
            HostConfigurationChangeRequired = disposition == RealControllerNetworkPreflightDisposition.ReadyForAuthorizedHostConfiguration,
            Snapshot = snapshot,
            NetworkTrafficSent = false,
            HostConfigurationChanged = false,
            ControllerConfigurationChanged = false,
            NativeKssStatus = NativeKssStatus.NotRun,
            Checks = checks,
            SideEffects = [],
            EnvironmentReusable = true,
            UnsupportedGaps = RealControllerNetworkPreflightContract.RequiredUnsupportedGaps.ToList()
        };
        var receipt = new RealControllerNetworkPreflightReceipt
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        return new RealControllerNetworkPreflightOutcome(receipt);
    }

    private static AddressingPlan ValidateRequest(RealControllerNetworkPreflightRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetInterfaceAlias);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.InternetInterfaceAlias);
        if (string.Equals(
                request.TargetInterfaceAlias,
                request.InternetInterfaceAlias,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Target and internet interface aliases must differ.", nameof(request));
        }

        if (request.PrefixLength is < 1 or > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Prefix length must be from 1 through 30.");
        }

        var controllerAddress = ParseIpv4(request.ControllerAddress, nameof(request.ControllerAddress));
        var proposedHostAddress = ParseIpv4(request.ProposedHostAddress, nameof(request.ProposedHostAddress));
        if (controllerAddress.Equals(proposedHostAddress))
        {
            throw new ArgumentException("Controller and proposed host addresses must differ.", nameof(request));
        }

        var mask = uint.MaxValue << (32 - request.PrefixLength);
        var controllerValue = ToUInt32(controllerAddress);
        var proposedValue = ToUInt32(proposedHostAddress);
        if ((controllerValue & mask) != (proposedValue & mask))
        {
            throw new ArgumentException("Controller and proposed host addresses must share the requested subnet.", nameof(request));
        }

        var network = controllerValue & mask;
        var broadcast = network | ~mask;
        if (controllerValue is var controller && (controller == network || controller == broadcast)
            || proposedValue is var proposed && (proposed == network || proposed == broadcast))
        {
            throw new ArgumentException("Network and broadcast addresses cannot be used as controller or host addresses.", nameof(request));
        }

        return new AddressingPlan(controllerAddress, proposedHostAddress, mask);
    }

    private static void EvaluateSnapshot(
        RealControllerNetworkPreflightRequest request,
        AddressingPlan addressing,
        RealControllerNetworkSnapshot snapshot,
        List<EnvironmentCheck> checks)
    {
        var target = snapshot.TargetInterface;
        var internet = snapshot.InternetInterface;
        if (!target.Exists)
        {
            checks.Add(Blocked("target-interface", $"Interface '{request.TargetInterfaceAlias}' was not found."));
        }
        else
        {
            checks.Add(Passed("target-interface", $"Observed local interface '{target.Alias}' ({target.InterfaceType})."));
            if (!string.Equals(target.OperationalStatus, OperationalStatus.Up.ToString(), StringComparison.Ordinal))
            {
                checks.Add(Blocked("target-link", $"Target interface state is {target.OperationalStatus}, not Up."));
            }
            else
            {
                checks.Add(Passed("target-link", $"Target interface is Up at {target.LinkSpeedBitsPerSecond} bit/s."));
            }

            if (target.Ipv4DefaultGateways.Count > 0)
            {
                checks.Add(Blocked("target-default-route", "Target interface already has an IPv4 default gateway."));
            }
            else
            {
                checks.Add(Passed("target-default-route", "Target interface has no IPv4 default gateway."));
            }

            var targetAddresses = target.Ipv4Addresses
                .Select(ParseAddressOrNull)
                .Where(address => address is not null)
                .Cast<IPAddress>()
                .ToList();
            var exactPresent = targetAddresses.Any(address => address.Equals(addressing.ProposedHostAddress));
            var otherTargetSubnetAddresses = targetAddresses
                .Where(address => (ToUInt32(address) & addressing.Mask)
                    == (ToUInt32(addressing.ControllerAddress) & addressing.Mask))
                .Where(address => !address.Equals(addressing.ProposedHostAddress))
                .ToList();
            if (otherTargetSubnetAddresses.Count > 0)
            {
                checks.Add(Blocked(
                    "target-ipv4-state",
                    "Target interface already has a different address in the controller subnet; manual review is required."));
            }
            else if (exactPresent)
            {
                checks.Add(Passed("target-ipv4-state", "The proposed host address is already configured."));
            }
            else
            {
                checks.Add(Passed(
                    "target-ipv4-state",
                    "No address in the controller subnet is configured; an authorized reversible host-only change is required."));
            }
        }

        if (!internet.Exists)
        {
            checks.Add(Blocked("internet-interface", $"Interface '{request.InternetInterfaceAlias}' was not found."));
        }
        else if (!string.Equals(internet.OperationalStatus, OperationalStatus.Up.ToString(), StringComparison.Ordinal))
        {
            checks.Add(Blocked("internet-interface", $"Internet interface state is {internet.OperationalStatus}, not Up."));
        }
        else if (internet.Ipv4DefaultGateways.Count == 0)
        {
            checks.Add(Blocked("internet-default-route", "Internet interface has no IPv4 default gateway."));
        }
        else
        {
            checks.Add(Passed("internet-default-route", "Internet default route remains on the separate internet interface."));
        }

        checks.Add(Passed(
            "address-plan",
            $"Host-only plan is {addressing.ProposedHostAddress}/{request.PrefixLength}; controller target is {addressing.ControllerAddress}."));
        checks.Add(Passed("no-network-traffic", "Snapshot collection used local interface metadata only."));
        checks.Add(Passed("no-configuration-change", "No host or controller setting was changed."));
    }

    private static IPAddress ParseIpv4(string value, string parameterName)
    {
        if (!IPAddress.TryParse(value, out var address)
            || address.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new ArgumentException("A canonical IPv4 address is required.", parameterName);
        }

        return address;
    }

    private static IPAddress? ParseAddressOrNull(string value) =>
        IPAddress.TryParse(value, out var address) && address.AddressFamily == AddressFamily.InterNetwork
            ? address
            : null;

    private static uint ToUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24)
            | ((uint)bytes[1] << 16)
            | ((uint)bytes[2] << 8)
            | bytes[3];
    }

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static EnvironmentCheck Passed(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Passed, Detail = detail };

    private static EnvironmentCheck Blocked(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Blocked, Detail = detail };

    private static EnvironmentCheck Failed(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Failed, Detail = detail };

    private sealed record AddressingPlan(IPAddress ControllerAddress, IPAddress ProposedHostAddress, uint Mask);
}

public sealed record RealControllerNetworkPreflightVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}

public static class RealControllerNetworkPreflightReceiptVerifier
{
    public static RealControllerNetworkPreflightVerificationResult Verify(
        RealControllerNetworkPreflightReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (!string.Equals(
                receipt.SchemaIdentity,
                RealControllerNetworkPreflightContract.ReceiptSchemaIdentity,
                StringComparison.Ordinal))
        {
            errors.Add($"schemaIdentity must be {RealControllerNetworkPreflightContract.ReceiptSchemaIdentity}");
        }

        if (receipt.SchemaVersion != RealControllerNetworkPreflightContract.ReceiptSchemaVersion)
        {
            errors.Add($"schemaVersion must be {RealControllerNetworkPreflightContract.ReceiptSchemaVersion}");
        }

        if (ReferenceEquals(receipt.Payload, null))
        {
            return new RealControllerNetworkPreflightVerificationResult
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

        if (!IsSha256(payload.CoreAssemblySha256))
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

        if (payload.Checks is null
            || payload.Checks.Count == 0
            || payload.Checks.Any(check => check is null || string.IsNullOrWhiteSpace(check.Id))
            || payload.Checks.GroupBy(check => check.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            errors.Add("payload.checks must contain unique non-empty evidence IDs");
        }
        else
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

        if (payload.Disposition == RealControllerNetworkPreflightDisposition.Blocked
            != (payload.TerminalClassification != EnvironmentTerminalClassification.Ready))
        {
            errors.Add("payload.disposition does not agree with terminal classification");
        }

        if (payload.HostConfigurationChangeRequired
            != (payload.Disposition == RealControllerNetworkPreflightDisposition.ReadyForAuthorizedHostConfiguration))
        {
            errors.Add("payload.hostConfigurationChangeRequired does not agree with disposition");
        }

        if (payload.NetworkTrafficSent
            || payload.HostConfigurationChanged
            || payload.ControllerConfigurationChanged
            || payload.NativeKssStatus != NativeKssStatus.NotRun)
        {
            errors.Add("network preflight cannot claim traffic, configuration change or native KSS execution");
        }

        if (payload.Snapshot is null
            || payload.Snapshot.TargetInterface is null
            || payload.Snapshot.InternetInterface is null)
        {
            errors.Add("payload.snapshot and both interface observations are required");
        }

        if (payload.SideEffects is null
            || payload.SideEffects.Any(sideEffect => !sideEffect.StartsWith("CreateNewReceiptFile:", StringComparison.Ordinal)))
        {
            errors.Add("network preflight side effects are limited to a create-new receipt file");
        }

        if (payload.UnsupportedGaps is null
            || RealControllerNetworkPreflightContract.RequiredUnsupportedGaps.Any(
                required => !payload.UnsupportedGaps.Contains(required, StringComparer.Ordinal)))
        {
            errors.Add("network preflight must preserve traffic, configuration and qualification limitations");
        }

        if (!payload.EnvironmentReusable
            || !string.Equals(payload.RollbackMode, "RestoreDhcpAndAutomaticDns", StringComparison.Ordinal))
        {
            errors.Add("network preflight must preserve a reusable host and explicit DHCP rollback mode");
        }

        if (!string.Equals(
                receipt.PayloadSha256,
                ReceiptSerialization.ComputeCanonicalSha256(payload),
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("payloadSha256 does not match the canonical payload");
        }

        return new RealControllerNetworkPreflightVerificationResult
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

public static class RealControllerNetworkPreflightReceiptWriter
{
    public static string WriteNew(string outputPath, RealControllerNetworkPreflightReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!RealControllerNetworkPreflightReceiptVerifier.Verify(receipt).Succeeded)
        {
            throw new InvalidOperationException("Real-controller network preflight receipt integrity is invalid.");
        }

        var fullPath = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Receipt output must use the .json extension.", nameof(outputPath));
        }

        var parent = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        Directory.CreateDirectory(parent);
        using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(ReceiptSerialization.ToJson(receipt));
        writer.WriteLine();
        writer.Flush();
        stream.Flush(flushToDisk: true);
        return fullPath;
    }
}

internal interface IRealControllerNetworkPlatform
{
    RealControllerNetworkSnapshot Capture(string targetInterfaceAlias, string internetInterfaceAlias);
}

internal sealed class RealControllerNetworkPlatform : IRealControllerNetworkPlatform
{
    public RealControllerNetworkSnapshot Capture(
        string targetInterfaceAlias,
        string internetInterfaceAlias)
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces();
        return new RealControllerNetworkSnapshot
        {
            TargetInterface = Observe(interfaces, targetInterfaceAlias),
            InternetInterface = Observe(interfaces, internetInterfaceAlias)
        };
    }

    private static NetworkInterfaceObservation Observe(
        IReadOnlyList<NetworkInterface> interfaces,
        string alias)
    {
        var networkInterface = interfaces.SingleOrDefault(
            candidate => string.Equals(candidate.Name, alias, StringComparison.OrdinalIgnoreCase));
        if (networkInterface is null)
        {
            return new NetworkInterfaceObservation { Alias = alias, Exists = false };
        }

        var properties = networkInterface.GetIPProperties();
        bool? dhcpEnabled;
        try
        {
            dhcpEnabled = OperatingSystem.IsWindows()
                ? properties.GetIPv4Properties()?.IsDhcpEnabled
                : null;
        }
        catch (NetworkInformationException)
        {
            dhcpEnabled = null;
        }

        return new NetworkInterfaceObservation
        {
            Alias = networkInterface.Name,
            Exists = true,
            InterfaceType = networkInterface.NetworkInterfaceType.ToString(),
            OperationalStatus = networkInterface.OperationalStatus.ToString(),
            LinkSpeedBitsPerSecond = networkInterface.Speed,
            DhcpEnabled = dhcpEnabled,
            Ipv4Addresses = properties.UnicastAddresses
                .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => address.Address.ToString())
                .Order(StringComparer.Ordinal)
                .ToList(),
            Ipv4DefaultGateways = properties.GatewayAddresses
                .Where(gateway => gateway.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(gateway => gateway.Address.ToString())
                .Where(address => !string.Equals(address, IPAddress.Any.ToString(), StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .ToList()
        };
    }
}
