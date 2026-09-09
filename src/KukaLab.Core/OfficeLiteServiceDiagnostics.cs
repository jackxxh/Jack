namespace KukaLab.Core;

public static class OfficeLiteServiceDiagnosticContract
{
    public const int DeviceInfoPort = 49003;

    public const int PositiveControlPort = 443;

    public static IReadOnlyList<int> WorkVisualServicePorts { get; } = Array.AsReadOnly(
        new[] { 49001, 49002, 49003, 49004, 49006, 49010 });

    public static IReadOnlyList<int> ReferencePorts { get; } = Array.AsReadOnly(
        new[] { 22, 80, 81, 111, 135, 139, 444, 445, 3389 });

    public static IReadOnlyList<int> NetworkControlPorts { get; } = Array.AsReadOnly(
        new[]
        {
            22, 80, 81, 111, 135, 139, PositiveControlPort, 444, 445, 3389,
            49001, 49002, 49003, 49004, 49006, 49010
        });
}

public enum GuestTcpPortState
{
    Open,
    ConnectionRefused,
    Timeout,
    Unresolved,
    Error
}

public sealed record GuestTcpPortObservation
{
    public int Port { get; init; }

    public GuestTcpPortState State { get; init; }

    public string Detail { get; init; } = string.Empty;

    public long DurationMilliseconds { get; init; }
}

public sealed record GuestServiceSnapshot
{
    public int Sequence { get; init; }

    public DateTimeOffset ObservedAtUtc { get; init; }

    public List<GuestTcpPortObservation> Ports { get; init; } = [];
}

public sealed record WorkVisualServiceObservation
{
    public bool Requested { get; init; }

    public bool ObservationCompleted { get; init; }

    public int RequestedObservationSeconds { get; init; }

    public long ActualObservationMilliseconds { get; init; }

    public int ProbeIntervalMilliseconds { get; init; }

    public int ConnectTimeoutMilliseconds { get; init; }

    public List<int> ExpectedPorts { get; init; } = [];

    public bool AnyServiceEverOpen { get; init; }

    public bool DeviceInfoEverOpen { get; init; }

    public List<GuestServiceSnapshot> Snapshots { get; init; } = [];
}

public sealed record GuestNetworkControlObservation
{
    public bool Requested { get; init; }

    public bool ObservationCompleted { get; init; }

    public int ConnectTimeoutMilliseconds { get; init; }

    public List<int> ExpectedPorts { get; init; } = [];

    public int PositiveControlPort { get; init; }

    public List<int> ReferencePorts { get; init; } = [];

    public List<int> TargetPorts { get; init; } = [];

    public DateTimeOffset? ObservedAtUtc { get; init; }

    public bool PositiveControlOpen { get; init; }

    public bool AnyReferencePortOpen { get; init; }

    public bool AnyReferencePortTimeout { get; init; }

    public bool AnyReferencePortConnectionRefused { get; init; }

    public bool AllTargetPortsTimeout { get; init; }

    public bool TargetTimeoutClassificationAmbiguous { get; init; }

    public List<GuestTcpPortObservation> Ports { get; init; } = [];
}

internal sealed record GuestTcpPortProbeResult(
    int Port,
    DateTimeOffset ObservedAtUtc,
    GuestTcpPortState State,
    string Detail,
    long DurationMilliseconds)
{
    public GuestTcpPortObservation ToObservation() => new()
    {
        Port = Port,
        State = State,
        Detail = Detail,
        DurationMilliseconds = DurationMilliseconds
    };
}
