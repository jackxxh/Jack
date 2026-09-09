using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class RealControllerEndpointProbeContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.real-controller-endpoint-probe-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const int DeviceInfoPort = 49003;
    public const int MinimumTimeoutMilliseconds = 250;
    public const int MaximumTimeoutMilliseconds = 10_000;

    internal static readonly IReadOnlyList<string> RequiredUnsupportedGaps =
    [
        "A TCP handshake proves only the explicitly named DeviceInfo endpoint is reachable; it does not prove project compatibility.",
        "This probe does not retrieve a WorkVisual project, authenticate, deploy, activate, transfer files or change controller configuration.",
        "This probe does not enable drives, execute KRL, command motion or establish physical qualification."
    ];
}

public enum RealControllerEndpointState
{
    NotAttempted,
    Open,
    ConnectionRefused,
    Timeout,
    HostUnreachable,
    NetworkUnreachable,
    Error
}

public sealed record RealControllerEndpointProbeRequest
{
    public required string TargetInterfaceAlias { get; init; }

    public required string InternetInterfaceAlias { get; init; }

    public required string ControllerAddress { get; init; }

    public required string HostAddress { get; init; }

    public int PrefixLength { get; init; } = 24;

    public int Port { get; init; } = RealControllerEndpointProbeContract.DeviceInfoPort;

    public int TimeoutMilliseconds { get; init; } = 2_000;
}

public sealed record RealControllerNeighborObservation
{
    public bool Observed { get; init; }

    public string InterfaceAlias { get; init; } = string.Empty;

    public int? InterfaceIndex { get; init; }

    public string IpAddress { get; init; } = string.Empty;

    public string LinkLayerAddress { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public string Diagnostic { get; init; } = string.Empty;
}

public sealed record RealControllerSourceBindingObservation
{
    public bool Evaluated { get; init; }

    public bool ReadSucceeded { get; init; }

    public List<string> InterfaceAliases { get; init; } = [];

    public string Diagnostic { get; init; } = string.Empty;
}

public sealed record RealControllerEndpointProbeReceipt
{
    public string SchemaIdentity { get; init; } = RealControllerEndpointProbeContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = RealControllerEndpointProbeContract.ReceiptSchemaVersion;

    public required RealControllerEndpointProbePayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record RealControllerEndpointProbePayload
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

    public string ControllerAddress { get; init; } = string.Empty;

    public string HostAddress { get; init; } = string.Empty;

    public int PrefixLength { get; init; }

    public int Port { get; init; }

    public int TimeoutMilliseconds { get; init; }

    public int ConnectionAttempts { get; init; }

    public RealControllerEndpointState EndpointState { get; init; }

    public bool EndpointReachable { get; init; }

    public bool DeviceInfoTcpHandshake { get; init; }

    public string SocketDiagnostic { get; init; } = string.Empty;

    public RealControllerSourceBindingObservation SourceBinding { get; init; } = new();

    public RealControllerNeighborObservation Neighbor { get; init; } = new();

    public required RealControllerNetworkPreflightReceipt Preflight { get; init; }

    public bool NetworkTrafficSent { get; init; }

    public bool HostConfigurationChanged { get; init; }

    public bool ControllerConfigurationChanged { get; init; }

    public bool ControllerWriteAttempted { get; init; }

    public bool WorkVisualProjectRetrieved { get; init; }

    public bool MotionCommandSent { get; init; }

    public NativeKssStatus NativeKssStatus { get; init; } = NativeKssStatus.NotRun;

    public List<EnvironmentCheck> Checks { get; init; } = [];

    public List<string> SideEffects { get; init; } = [];

    public bool EnvironmentReusable { get; init; } = true;

    public List<string> UnsupportedGaps { get; init; } = [];
}

public sealed record RealControllerEndpointProbeOutcome(RealControllerEndpointProbeReceipt Receipt)
{
    public int ExitCode => Receipt.Payload.TerminalClassification switch
    {
        EnvironmentTerminalClassification.Ready => 0,
        EnvironmentTerminalClassification.Failed => 2,
        _ => 3
    };
}

internal sealed record EndpointProbeNetworkResult(
    RealControllerEndpointState State,
    bool Attempted,
    string Diagnostic);

public sealed class RealControllerEndpointProbeRunner
{
    private static readonly Regex AttemptIdPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private readonly IRealControllerEndpointProbePlatform _platform;
    private readonly TimeProvider _timeProvider;

    public RealControllerEndpointProbeRunner()
        : this(new RealControllerEndpointProbePlatform(), TimeProvider.System)
    {
    }

    internal RealControllerEndpointProbeRunner(
        IRealControllerEndpointProbePlatform platform,
        TimeProvider timeProvider)
    {
        _platform = platform;
        _timeProvider = timeProvider;
    }

    public async Task<RealControllerEndpointProbeOutcome> RunAsync(
        RealControllerEndpointProbeRequest request,
        string attemptId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptId);
        if (!AttemptIdPattern.IsMatch(attemptId))
        {
            throw new ArgumentException(
                "Attempt ID must match [A-Za-z0-9][A-Za-z0-9._-]{2,127}.",
                nameof(attemptId));
        }

        ValidateRequest(request);
        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var preflightAttemptHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(attemptId)))[..16];
        var preflight = new RealControllerNetworkPreflightRunner(_platform, _timeProvider).Run(
            new RealControllerNetworkPreflightRequest
            {
                TargetInterfaceAlias = request.TargetInterfaceAlias,
                InternetInterfaceAlias = request.InternetInterfaceAlias,
                ControllerAddress = request.ControllerAddress,
                ProposedHostAddress = request.HostAddress,
                PrefixLength = request.PrefixLength
            },
            $"endpoint-{preflightAttemptHash}.preflight");
        var controllerAddress = preflight.Receipt.Payload.ControllerAddress;
        var hostAddress = preflight.Receipt.Payload.ProposedHostAddress;
        var checks = new List<EnvironmentCheck>
        {
            Passed(
                "preflight-integrity",
                $"Bound zero-traffic preflight payload {preflight.Receipt.PayloadSha256}."),
            preflight.Receipt.Payload.Disposition == RealControllerNetworkPreflightDisposition.AlreadyConfigured
                ? Passed("preflight-state", "The exact host address is configured on the named target interface.")
                : Blocked(
                    "preflight-state",
                    $"Probe requires AlreadyConfigured; observed {preflight.Receipt.Payload.Disposition}.")
        };
        var result = new EndpointProbeNetworkResult(
            RealControllerEndpointState.NotAttempted,
            Attempted: false,
            "TCP was not attempted because the local preflight did not accept the exact configured route.");
        var sourceBinding = new RealControllerSourceBindingObservation
        {
            Diagnostic = "Source-address exclusivity was not evaluated because local preflight did not accept the configured route."
        };
        var neighbor = new RealControllerNeighborObservation
        {
            InterfaceAlias = request.TargetInterfaceAlias,
            IpAddress = controllerAddress,
            Diagnostic = "Neighbor lookup was not attempted because no TCP packet was sent."
        };

        if (preflight.Receipt.Payload.Disposition == RealControllerNetworkPreflightDisposition.AlreadyConfigured)
        {
            var source = IPAddress.Parse(hostAddress);
            var target = IPAddress.Parse(controllerAddress);
            sourceBinding = _platform.CaptureSourceBindings(source);
            var sourceBindingAccepted = sourceBinding.Evaluated
                && sourceBinding.ReadSucceeded
                && sourceBinding.InterfaceAliases.Count == 1
                && string.Equals(
                    sourceBinding.InterfaceAliases[0],
                    request.TargetInterfaceAlias,
                    StringComparison.OrdinalIgnoreCase);
            checks.Add(!sourceBinding.ReadSucceeded
                ? Failed("source-address-binding", sourceBinding.Diagnostic)
                : sourceBindingAccepted
                    ? Passed(
                        "source-address-binding",
                        $"Source {hostAddress} exists only on {request.TargetInterfaceAlias}.")
                    : Blocked(
                        "source-address-binding",
                        $"Source {hostAddress} is not exclusive to {request.TargetInterfaceAlias}: {sourceBinding.Diagnostic}"));

            if (!sourceBindingAccepted)
            {
                result = new EndpointProbeNetworkResult(
                    RealControllerEndpointState.NotAttempted,
                    Attempted: false,
                    sourceBinding.ReadSucceeded
                        ? "TCP was not attempted because the source address is not exclusive to the named target interface."
                        : "TCP was not attempted because source-address bindings could not be read safely.");
            }

            if (sourceBindingAccepted)
            {
                result = await _platform.ProbeAsync(
                    source,
                    target,
                    request.Port,
                    TimeSpan.FromMilliseconds(request.TimeoutMilliseconds),
                    cancellationToken).ConfigureAwait(false);
                if (result.Attempted)
                {
                    neighbor = _platform.CaptureNeighbor(request.TargetInterfaceAlias, target);
                }

                checks.Add(result.State switch
                {
                    RealControllerEndpointState.Open => Passed(
                        "device-info-endpoint",
                        $"TCP {controllerAddress}:{request.Port} accepted the single connection attempt."),
                    RealControllerEndpointState.Error => Failed(
                        "device-info-endpoint",
                        $"The bounded TCP attempt failed unexpectedly: {result.Diagnostic}"),
                    _ => Blocked(
                        "device-info-endpoint",
                        $"The bounded TCP attempt completed as {result.State}: {result.Diagnostic}")
                });
                checks.Add(IsUsableNeighbor(neighbor, request.TargetInterfaceAlias, controllerAddress)
                    ? Passed(
                        "controller-neighbor",
                        $"Observed {neighbor.IpAddress} as {neighbor.LinkLayerAddress} in state {neighbor.State} on {neighbor.InterfaceAlias}.")
                    : Blocked(
                        "controller-neighbor",
                        $"No usable exact neighbor entry was retained: {neighbor.Diagnostic}"));
            }
        }

        stopwatch.Stop();
        var terminal = checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
            ? EnvironmentTerminalClassification.Failed
            : result.State == RealControllerEndpointState.Open
                && IsUsableNeighbor(neighbor, request.TargetInterfaceAlias, controllerAddress)
                ? EnvironmentTerminalClassification.Ready
                : EnvironmentTerminalClassification.Blocked;
        var completedAt = _timeProvider.GetUtcNow();
        var payload = new RealControllerEndpointProbePayload
        {
            ReceiptId = $"real-controller-endpoint-probe-{attemptId}",
            AttemptId = attemptId,
            CoreAssemblySha256 = ComputeFileSha256(typeof(RealControllerEndpointProbeRunner).Assembly.Location),
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
            ControllerAddress = controllerAddress,
            HostAddress = hostAddress,
            PrefixLength = request.PrefixLength,
            Port = request.Port,
            TimeoutMilliseconds = request.TimeoutMilliseconds,
            ConnectionAttempts = result.Attempted ? 1 : 0,
            EndpointState = result.State,
            EndpointReachable = result.State is RealControllerEndpointState.Open
                or RealControllerEndpointState.ConnectionRefused,
            DeviceInfoTcpHandshake = result.State == RealControllerEndpointState.Open,
            SocketDiagnostic = result.Diagnostic,
            SourceBinding = sourceBinding,
            Neighbor = neighbor,
            Preflight = preflight.Receipt,
            NetworkTrafficSent = result.Attempted,
            HostConfigurationChanged = false,
            ControllerConfigurationChanged = false,
            ControllerWriteAttempted = false,
            WorkVisualProjectRetrieved = false,
            MotionCommandSent = false,
            NativeKssStatus = NativeKssStatus.NotRun,
            Checks = checks,
            SideEffects = result.Attempted
                ? [$"TcpConnectAttempt:{hostAddress}->{controllerAddress}:{request.Port}"]
                : [],
            EnvironmentReusable = true,
            UnsupportedGaps = RealControllerEndpointProbeContract.RequiredUnsupportedGaps.ToList()
        };
        var receipt = new RealControllerEndpointProbeReceipt
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        return new RealControllerEndpointProbeOutcome(receipt);
    }

    private static void ValidateRequest(RealControllerEndpointProbeRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetInterfaceAlias);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.InternetInterfaceAlias);
        if (request.Port != RealControllerEndpointProbeContract.DeviceInfoPort)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"Only the documented DeviceInfo port {RealControllerEndpointProbeContract.DeviceInfoPort} is allowed.");
        }

        if (request.TimeoutMilliseconds is < RealControllerEndpointProbeContract.MinimumTimeoutMilliseconds
            or > RealControllerEndpointProbeContract.MaximumTimeoutMilliseconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"Timeout must be from {RealControllerEndpointProbeContract.MinimumTimeoutMilliseconds} through {RealControllerEndpointProbeContract.MaximumTimeoutMilliseconds} milliseconds.");
        }
    }

    private static bool IsUsableNeighbor(
        RealControllerNeighborObservation observation,
        string targetInterfaceAlias,
        string controllerAddress) =>
        observation.Observed
        && string.Equals(observation.InterfaceAlias, targetInterfaceAlias, StringComparison.OrdinalIgnoreCase)
        && string.Equals(observation.IpAddress, controllerAddress, StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(observation.LinkLayerAddress)
        && observation.State is "Probe" or "Delay" or "Stale" or "Reachable" or "Permanent";

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
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

public sealed record RealControllerEndpointProbeVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}

public static class RealControllerEndpointProbeReceiptVerifier
{
    private static readonly Regex AttemptIdPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static RealControllerEndpointProbeVerificationResult Verify(
        RealControllerEndpointProbeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        var payload = receipt.Payload;
        var preflightAlreadyConfigured = false;
        var sourceBindingAccepted = false;
        var sourceBindingReadFailed = false;
        var sourceBindingAliases = payload.SourceBinding?.InterfaceAliases ?? [];
        if (!string.Equals(
                receipt.SchemaIdentity,
                RealControllerEndpointProbeContract.ReceiptSchemaIdentity,
                StringComparison.Ordinal))
        {
            errors.Add($"schemaIdentity must be {RealControllerEndpointProbeContract.ReceiptSchemaIdentity}");
        }

        if (receipt.SchemaVersion != RealControllerEndpointProbeContract.ReceiptSchemaVersion)
        {
            errors.Add($"schemaVersion must be {RealControllerEndpointProbeContract.ReceiptSchemaVersion}");
        }

        if (string.IsNullOrWhiteSpace(payload.ReceiptId)
            || string.IsNullOrWhiteSpace(payload.AttemptId)
            || !AttemptIdPattern.IsMatch(payload.AttemptId))
        {
            errors.Add("payload receipt and bounded attempt identities are required");
        }

        if (!string.Equals(
                payload.ReceiptId,
                $"real-controller-endpoint-probe-{payload.AttemptId}",
                StringComparison.Ordinal))
        {
            errors.Add("payload.receiptId must be derived from payload.attemptId");
        }

        if (!IsSha256(payload.CoreAssemblySha256))
        {
            errors.Add("payload.coreAssemblySha256 must be a SHA-256 value");
        }

        if (!string.Equals(payload.CoreVersion, FixtureContract.CoreVersion, StringComparison.Ordinal))
        {
            errors.Add($"payload.coreVersion must be {FixtureContract.CoreVersion}");
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

        if (payload.Port != RealControllerEndpointProbeContract.DeviceInfoPort)
        {
            errors.Add($"payload.port must be {RealControllerEndpointProbeContract.DeviceInfoPort}");
        }

        if (payload.TimeoutMilliseconds is < RealControllerEndpointProbeContract.MinimumTimeoutMilliseconds
            or > RealControllerEndpointProbeContract.MaximumTimeoutMilliseconds)
        {
            errors.Add("payload.timeoutMilliseconds is outside the bounded range");
        }

        if (payload.ConnectionAttempts is < 0 or > 1
            || payload.NetworkTrafficSent != (payload.ConnectionAttempts == 1))
        {
            errors.Add("the receipt must bind zero or one TCP attempt exactly");
        }

        if (string.IsNullOrWhiteSpace(payload.SocketDiagnostic))
        {
            errors.Add("payload.socketDiagnostic is required");
        }

        if (payload.SourceBinding is null || string.IsNullOrWhiteSpace(payload.SourceBinding.Diagnostic))
        {
            errors.Add("payload.sourceBinding and its diagnostic are required");
        }
        else
        {
            if (payload.SourceBinding.InterfaceAliases is null
                || sourceBindingAliases.Any(string.IsNullOrWhiteSpace)
                || sourceBindingAliases.Distinct(StringComparer.OrdinalIgnoreCase).Count() != sourceBindingAliases.Count
                || !sourceBindingAliases.SequenceEqual(
                    sourceBindingAliases.Order(StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase))
            {
                errors.Add("payload.sourceBinding interface aliases must be non-empty, unique and ordered");
            }

            if (!payload.SourceBinding.Evaluated
                && (payload.SourceBinding.ReadSucceeded || sourceBindingAliases.Count != 0))
            {
                errors.Add("an unevaluated source binding cannot claim a successful read or interfaces");
            }

            if (payload.SourceBinding.Evaluated
                && !payload.SourceBinding.ReadSucceeded
                && sourceBindingAliases.Count != 0)
            {
                errors.Add("a failed source-binding read cannot claim interfaces");
            }
        }

        if (payload.Preflight is null)
        {
            errors.Add("embedded zero-traffic preflight receipt is required");
        }
        else
        {
            var preflightVerification = RealControllerNetworkPreflightReceiptVerifier.Verify(payload.Preflight);
            if (!preflightVerification.Succeeded)
            {
                errors.Add("embedded zero-traffic preflight receipt is invalid");
            }

            var preflight = payload.Preflight.Payload;
            preflightAlreadyConfigured =
                preflight.Disposition == RealControllerNetworkPreflightDisposition.AlreadyConfigured;
            if (!string.Equals(preflight.ControllerAddress, payload.ControllerAddress, StringComparison.Ordinal)
                || !string.Equals(preflight.ProposedHostAddress, payload.HostAddress, StringComparison.Ordinal)
                || preflight.PrefixLength != payload.PrefixLength)
            {
                errors.Add("embedded preflight addressing does not match the endpoint probe");
            }

            if (payload.NetworkTrafficSent
                && preflight.Disposition != RealControllerNetworkPreflightDisposition.AlreadyConfigured)
            {
                errors.Add("network traffic requires an AlreadyConfigured zero-traffic preflight");
            }

            if (!string.Equals(preflight.CoreVersion, payload.CoreVersion, StringComparison.Ordinal)
                || !string.Equals(preflight.CoreAssemblySha256, payload.CoreAssemblySha256, StringComparison.OrdinalIgnoreCase)
                || preflight.Runtime != payload.Runtime)
            {
                errors.Add("embedded preflight Core/runtime identity must match the endpoint probe");
            }

            if (payload.StartedAtUtc > preflight.StartedAtUtc
                || preflight.CompletedAtUtc > payload.CompletedAtUtc)
            {
                errors.Add("embedded preflight timing must be enclosed by endpoint-probe timing");
            }


            if (!preflightAlreadyConfigured
                && (payload.EndpointState != RealControllerEndpointState.NotAttempted
                    || payload.ConnectionAttempts != 0
                    || payload.NetworkTrafficSent
                    || payload.Neighbor?.Observed == true
                    || payload.SourceBinding?.Evaluated == true))
            {
                errors.Add("a blocked preflight must stop before source-binding, endpoint and neighbor observation");
            }

            if (preflightAlreadyConfigured && payload.SourceBinding is not null)
            {
                sourceBindingReadFailed = payload.SourceBinding.Evaluated
                    && !payload.SourceBinding.ReadSucceeded;
                sourceBindingAccepted = payload.SourceBinding.Evaluated
                    && payload.SourceBinding.ReadSucceeded
                    && sourceBindingAliases.Count == 1
                    && string.Equals(
                        sourceBindingAliases[0],
                        preflight.Snapshot.TargetInterface.Alias,
                        StringComparison.OrdinalIgnoreCase);
                if (!payload.SourceBinding.Evaluated)
                {
                    errors.Add("an accepted preflight requires source-address binding evaluation");
                }

                if (!sourceBindingAccepted
                    && (payload.EndpointState != RealControllerEndpointState.NotAttempted
                        || payload.ConnectionAttempts != 0
                        || payload.NetworkTrafficSent
                        || payload.Neighbor?.Observed == true))
                {
                    errors.Add("a non-exclusive source binding must stop before endpoint and neighbor observation");
                }

                if (sourceBindingAccepted
                    && payload.EndpointState == RealControllerEndpointState.NotAttempted)
                {
                    errors.Add("an accepted exclusive source binding cannot produce a NotAttempted endpoint receipt");
                }
            }
        }

        if ((payload.EndpointState is RealControllerEndpointState.Open
                or RealControllerEndpointState.ConnectionRefused
                or RealControllerEndpointState.Timeout
                or RealControllerEndpointState.HostUnreachable
                or RealControllerEndpointState.NetworkUnreachable)
            && payload.ConnectionAttempts != 1)
        {
            errors.Add("a concrete endpoint result requires exactly one connection attempt");
        }

        var isOpen = payload.EndpointState == RealControllerEndpointState.Open;
        if (payload.DeviceInfoTcpHandshake != isOpen)
        {
            errors.Add("deviceInfoTcpHandshake must match an Open endpoint state");
        }

        var pathObserved = payload.EndpointState is RealControllerEndpointState.Open
            or RealControllerEndpointState.ConnectionRefused;
        if (payload.EndpointReachable != pathObserved)
        {
            errors.Add("endpointReachable must match Open or ConnectionRefused");
        }

        var usableNeighbor = payload.Neighbor is not null
            && payload.Preflight is not null
            && payload.Neighbor.Observed
            && string.Equals(payload.Neighbor.InterfaceAlias, payload.Preflight.Payload.Snapshot.TargetInterface.Alias, StringComparison.OrdinalIgnoreCase)
            && string.Equals(payload.Neighbor.IpAddress, payload.ControllerAddress, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(payload.Neighbor.LinkLayerAddress)
            && !string.Equals(payload.Neighbor.State, "Incomplete", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(payload.Neighbor.State, "Unreachable", StringComparison.OrdinalIgnoreCase);
        if (payload.Neighbor is null || string.IsNullOrWhiteSpace(payload.Neighbor.Diagnostic))
        {
            errors.Add("payload.neighbor and its diagnostic are required");
        }
        else if (payload.Neighbor.Observed
            && (payload.Neighbor.InterfaceIndex is null or <= 0
                || !Regex.IsMatch(
                    payload.Neighbor.LinkLayerAddress,
                    "^(?:[0-9A-Fa-f]{2}-){5}[0-9A-Fa-f]{2}$",
                    RegexOptions.CultureInvariant)
                || string.IsNullOrWhiteSpace(payload.Neighbor.State)))
        {
            errors.Add("observed neighbor evidence is incomplete or malformed");
        }
        var expectedTerminal = sourceBindingReadFailed
            || payload.EndpointState == RealControllerEndpointState.Error
            ? EnvironmentTerminalClassification.Failed
            : isOpen && usableNeighbor
                ? EnvironmentTerminalClassification.Ready
                : EnvironmentTerminalClassification.Blocked;
        if (payload.TerminalClassification != expectedTerminal)
        {
            errors.Add("terminal classification does not match endpoint and neighbor evidence");
        }

        if (payload.EndpointState == RealControllerEndpointState.NotAttempted
            && (payload.ConnectionAttempts != 0 || payload.NetworkTrafficSent))
        {
            errors.Add("NotAttempted cannot claim network traffic");
        }

        if (payload.HostConfigurationChanged
            || payload.ControllerConfigurationChanged
            || payload.ControllerWriteAttempted
            || payload.WorkVisualProjectRetrieved
            || payload.MotionCommandSent
            || payload.NativeKssStatus != NativeKssStatus.NotRun)
        {
            errors.Add("endpoint probe cannot claim host/controller mutation, project retrieval, motion or native KSS execution");
        }

        if (payload.Checks is null || payload.Checks.Count < 2)
        {
            errors.Add("payload.checks must contain preflight and endpoint evidence");
        }
        else
        {
            var expectedCheckIds = !preflightAlreadyConfigured
                ? new[] { "preflight-integrity", "preflight-state" }
                : sourceBindingAccepted
                    ? new[] { "preflight-integrity", "preflight-state", "source-address-binding", "device-info-endpoint", "controller-neighbor" }
                    : new[] { "preflight-integrity", "preflight-state", "source-address-binding" };
            if (!payload.Checks.Select(check => check.Id).SequenceEqual(expectedCheckIds))
            {
                errors.Add("payload.checks must preserve the exact ordered probe evidence set");
            }
        }

        if (payload.SideEffects is null)
        {
            errors.Add("payload.sideEffects cannot be null");
        }
        else
        {
            var expectedTcpEffect = $"TcpConnectAttempt:{payload.HostAddress}->{payload.ControllerAddress}:{payload.Port}";
            var tcpEffects = payload.SideEffects
                .Where(effect => effect.StartsWith("TcpConnectAttempt:", StringComparison.Ordinal))
                .ToList();
            if (tcpEffects.Count != payload.ConnectionAttempts
                || tcpEffects.Any(effect => !string.Equals(effect, expectedTcpEffect, StringComparison.Ordinal)))
            {
                errors.Add("payload.sideEffects must bind the exact single TCP attempt");
            }

            if (payload.SideEffects.Any(effect =>
                !effect.StartsWith("TcpConnectAttempt:", StringComparison.Ordinal)
                && !effect.StartsWith("CreateNewReceiptFile:", StringComparison.Ordinal)))
            {
                errors.Add("payload.sideEffects contains an unsupported effect");
            }

            if (payload.SideEffects.Count(effect => effect.StartsWith("CreateNewReceiptFile:", StringComparison.Ordinal)) > 1)
            {
                errors.Add("payload.sideEffects can bind at most one receipt write");
            }
        }

        if (!payload.EnvironmentReusable
            || payload.UnsupportedGaps is null
            || !payload.UnsupportedGaps.SequenceEqual(RealControllerEndpointProbeContract.RequiredUnsupportedGaps))
        {
            errors.Add("probe must preserve reusable state and the exact unsupported boundary");
        }

        if (!string.Equals(
                receipt.PayloadSha256,
                ReceiptSerialization.ComputeCanonicalSha256(payload),
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("payloadSha256 does not match the canonical payload");
        }

        return new RealControllerEndpointProbeVerificationResult
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

public static class RealControllerEndpointProbeReceiptWriter
{
    public static string WriteNew(string outputPath, RealControllerEndpointProbeReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!RealControllerEndpointProbeReceiptVerifier.Verify(receipt).Succeeded)
        {
            throw new InvalidOperationException("Real-controller endpoint-probe receipt integrity is invalid.");
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

internal interface IRealControllerEndpointProbePlatform : IRealControllerNetworkPlatform
{
    RealControllerSourceBindingObservation CaptureSourceBindings(IPAddress sourceAddress);

    Task<EndpointProbeNetworkResult> ProbeAsync(
        IPAddress sourceAddress,
        IPAddress targetAddress,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    RealControllerNeighborObservation CaptureNeighbor(
        string interfaceAlias,
        IPAddress targetAddress);
}

internal sealed class RealControllerEndpointProbePlatform : IRealControllerEndpointProbePlatform
{
    private readonly RealControllerNetworkPlatform _networkPlatform = new();

    public RealControllerNetworkSnapshot Capture(
        string targetInterfaceAlias,
        string internetInterfaceAlias) =>
        _networkPlatform.Capture(targetInterfaceAlias, internetInterfaceAlias);

    public RealControllerSourceBindingObservation CaptureSourceBindings(IPAddress sourceAddress)
    {
        try
        {
            var aliases = NetworkInterface.GetAllNetworkInterfaces()
                .Where(networkInterface => networkInterface.GetIPProperties().UnicastAddresses.Any(
                    address => address.Address.AddressFamily == AddressFamily.InterNetwork
                        && address.Address.Equals(sourceAddress)))
                .Select(networkInterface => networkInterface.Name)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new RealControllerSourceBindingObservation
            {
                Evaluated = true,
                ReadSucceeded = true,
                InterfaceAliases = aliases,
                Diagnostic = aliases.Count == 0
                    ? $"Source {sourceAddress} was not found on a local interface."
                    : $"Source {sourceAddress} was found on: {string.Join(", ", aliases)}."
            };
        }
        catch (Exception exception) when (exception is NetworkInformationException
            or InvalidOperationException
            or SocketException)
        {
            return new RealControllerSourceBindingObservation
            {
                Evaluated = true,
                ReadSucceeded = false,
                Diagnostic = $"Source-address binding observation failed: {exception.GetType().Name}."
            };
        }
    }

    public async Task<EndpointProbeNetworkResult> ProbeAsync(
        IPAddress sourceAddress,
        IPAddress targetAddress,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            socket.Bind(new IPEndPoint(sourceAddress, 0));
        }
        catch (SocketException exception)
        {
            return new EndpointProbeNetworkResult(
                RealControllerEndpointState.Error,
                Attempted: false,
                $"Source bind failed: {exception.SocketErrorCode}.");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await socket.ConnectAsync(
                new IPEndPoint(targetAddress, port),
                timeoutSource.Token).ConfigureAwait(false);
            return new EndpointProbeNetworkResult(
                RealControllerEndpointState.Open,
                Attempted: true,
                "TCP handshake completed.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new EndpointProbeNetworkResult(
                RealControllerEndpointState.Timeout,
                Attempted: true,
                $"No TCP result within {timeout.TotalMilliseconds:0} ms.");
        }
        catch (SocketException exception)
        {
            var state = exception.SocketErrorCode switch
            {
                SocketError.ConnectionRefused => RealControllerEndpointState.ConnectionRefused,
                SocketError.TimedOut => RealControllerEndpointState.Timeout,
                SocketError.HostUnreachable => RealControllerEndpointState.HostUnreachable,
                SocketError.NetworkUnreachable => RealControllerEndpointState.NetworkUnreachable,
                _ => RealControllerEndpointState.Error
            };
            return new EndpointProbeNetworkResult(
                state,
                Attempted: true,
                $"Socket result: {exception.SocketErrorCode}.");
        }
    }

    public RealControllerNeighborObservation CaptureNeighbor(
        string interfaceAlias,
        IPAddress targetAddress)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new RealControllerNeighborObservation
            {
                InterfaceAlias = interfaceAlias,
                IpAddress = targetAddress.ToString(),
                Diagnostic = "Neighbor capture currently requires Windows."
            };
        }

        var powershell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = powershell,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        };
        startInfo.Environment["KUKA_LAB_INTERFACE_ALIAS"] = interfaceAlias;
        startInfo.Environment["KUKA_LAB_CONTROLLER_IP"] = targetAddress.ToString();
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(
            "$utf8 = New-Object System.Text.UTF8Encoding($false); [Console]::OutputEncoding = $utf8; $OutputEncoding = $utf8; $n = Get-NetNeighbor -InterfaceAlias $env:KUKA_LAB_INTERFACE_ALIAS -IPAddress $env:KUKA_LAB_CONTROLLER_IP -AddressFamily IPv4 -ErrorAction SilentlyContinue | Select-Object -First 1 InterfaceAlias,InterfaceIndex,IPAddress,LinkLayerAddress,State; if ($null -ne $n) { $n | ConvertTo-Json -Compress }");

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unable to start the neighbor observation process.");
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(5_000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                _ = standardOutput.GetAwaiter().GetResult();
                _ = standardError.GetAwaiter().GetResult();
                return new RealControllerNeighborObservation
                {
                    InterfaceAlias = interfaceAlias,
                    IpAddress = targetAddress.ToString(),
                    Diagnostic = "Neighbor observation timed out after 5000 ms."
                };
            }

            var json = standardOutput.GetAwaiter().GetResult().Trim();
            _ = standardError.GetAwaiter().GetResult();
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(json))
            {
                return new RealControllerNeighborObservation
                {
                    InterfaceAlias = interfaceAlias,
                    IpAddress = targetAddress.ToString(),
                    Diagnostic = process.ExitCode == 0
                        ? "No exact neighbor entry was observed."
                        : $"Neighbor observation exited with code {process.ExitCode}."
                };
            }

            return ParseNeighborJson(json, interfaceAlias, targetAddress);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException
            or UnauthorizedAccessException
            or JsonException)
        {
            return new RealControllerNeighborObservation
            {
                InterfaceAlias = interfaceAlias,
                IpAddress = targetAddress.ToString(),
                Diagnostic = $"Neighbor observation failed: {exception.GetType().Name}."
            };
        }
    }

    internal static RealControllerNeighborObservation ParseNeighborJson(
        string json,
        string interfaceAlias,
        IPAddress targetAddress)
    {
        var observed = JsonSerializer.Deserialize<NeighborJson>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return observed is null
            ? new RealControllerNeighborObservation
            {
                InterfaceAlias = interfaceAlias,
                IpAddress = targetAddress.ToString(),
                Diagnostic = "Neighbor observation returned an empty object."
            }
            : new RealControllerNeighborObservation
            {
                Observed = true,
                InterfaceAlias = interfaceAlias,
                InterfaceIndex = observed.InterfaceIndex,
                IpAddress = observed.IPAddress ?? targetAddress.ToString(),
                LinkLayerAddress = observed.LinkLayerAddress ?? string.Empty,
                State = NormalizeNeighborState(observed.State),
                Diagnostic = "Exact post-probe neighbor entry captured."
            };
    }

    private static string NormalizeNeighborState(JsonElement state) => state.ValueKind switch
    {
        JsonValueKind.String => state.GetString() ?? string.Empty,
        JsonValueKind.Number when state.TryGetInt32(out var value) => value switch
        {
            0 => "Unreachable",
            1 => "Incomplete",
            2 => "Probe",
            3 => "Delay",
            4 => "Stale",
            5 => "Reachable",
            6 => "Permanent",
            _ => "Unknown"
        },
        _ => string.Empty
    };

    private sealed record NeighborJson
    {
        public string? InterfaceAlias { get; init; }

        public int? InterfaceIndex { get; init; }

        public string? IPAddress { get; init; }

        public string? LinkLayerAddress { get; init; }

        public JsonElement State { get; init; }
    }
}
