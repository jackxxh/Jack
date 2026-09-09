using System.Text.Json.Serialization;

namespace KukaLab.Core;

public static class FixtureContract
{
    public const string ManifestSchemaIdentity = "kuka.lab.fixture";
    public const int ManifestSchemaVersion = 2;
    public static readonly IReadOnlySet<int> SupportedManifestSchemaVersions = new HashSet<int> { 1, 2 };
    public const string ReceiptSchemaIdentity = "kuka.lab.fixture-integrity-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const string CoreVersion = "0.1.0";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FixtureClassification
{
    GoldenPath,
    IntentionalFailure
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NativeCompileExpectation
{
    CompileAccepted,
    CompileRejected
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FixtureExecutionExpectation
{
    BoundedVirtualExecutionExpected,
    RejectedBeforeExecution
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FixtureTextEncoding
{
    Ascii7Bit
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FixtureLineEnding
{
    Lf
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VerificationStatus
{
    Passed,
    Failed
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NativeKssStatus
{
    NotRun,
    SyntaxSelectionValidated,
    BoundedExecutionValidated
}

public sealed record FixtureManifest
{
    public required string SchemaIdentity { get; init; }

    public required int SchemaVersion { get; init; }

    public required string FixtureId { get; init; }

    public required FixtureClassification Classification { get; init; }

    public required string Provenance { get; init; }

    public required NativeCompileExpectation NativeExpectation { get; init; }

    public NativeKssStatus? NativeEvidenceStatus { get; init; }

    public string? NativeEvidenceReference { get; init; }

    public FixtureExecutionExpectation? ExecutionExpectation { get; init; }

    public List<string>? MotionProviderPreference { get; init; }

    public required ControllerTarget ControllerTarget { get; init; }

    public required FrameDeclaration Frames { get; init; }

    public required List<FixtureFileDeclaration> Files { get; init; }

    public required List<FixtureCorrelation> Correlations { get; init; }

    public required List<string> ExpectedAssertions { get; init; }

    public ExpectedDiagnostic? ExpectedDiagnostic { get; init; }

    public required SafetyDeclaration Safety { get; init; }
}

public sealed record ControllerTarget
{
    public required string Vendor { get; init; }

    public required string KssVersion { get; init; }

    public required string RobotModel { get; init; }

    public required string RobotEvidence { get; init; }

    public required string ControllerModel { get; init; }

    public string? KukaSimComponentSha256 { get; init; }

    public string? OfficeLiteScope { get; init; }
}

public sealed record FrameDeclaration
{
    public required int ToolNumber { get; init; }

    public required int BaseNumber { get; init; }

    public required string LoadDeclaration { get; init; }

    public required string Provenance { get; init; }
}

public sealed record FixtureFileDeclaration
{
    public required string RelativePath { get; init; }

    public required long Bytes { get; init; }

    public required string Sha256 { get; init; }

    public FixtureTextEncoding? Encoding { get; init; }

    public FixtureLineEnding? LineEnding { get; init; }
}

public sealed record FixtureCorrelation
{
    public int? Sequence { get; init; }

    public required string OperationId { get; init; }

    public required string TargetId { get; init; }

    public required string MotionType { get; init; }

    public string? KrlSymbol { get; init; }

    public required string File { get; init; }

    public required int Line { get; init; }
}

public sealed record ExpectedDiagnostic
{
    public required string Category { get; init; }

    public string? MessageCode { get; init; }

    public required string File { get; init; }

    public required int Line { get; init; }

    public required NativeKssStatus ObservationStatus { get; init; }

    public string? NativeMessageCode { get; init; }

    public string? NativeFile { get; init; }

    public int? NativeLine { get; init; }

    public int? NativeColumn { get; init; }

    public string? EvidenceReference { get; init; }
}

public sealed record SafetyDeclaration
{
    public required string Classification { get; init; }

    public required bool RedactionConfirmed { get; init; }

    public required bool PhysicalMotionAllowed { get; init; }
}

public sealed record FixtureIntegrityReceipt
{
    public string SchemaIdentity { get; init; } = FixtureContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = FixtureContract.ReceiptSchemaVersion;

    public required FixtureIntegrityPayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record FixtureIntegrityPayload
{
    public string ReceiptId { get; init; } = string.Empty;

    public string AttemptId { get; init; } = string.Empty;

    public string FixtureId { get; init; } = string.Empty;

    public string CoreVersion { get; init; } = FixtureContract.CoreVersion;

    public string CoreAssemblySha256 { get; init; } = string.Empty;

    public RuntimeEnvironment Runtime { get; init; } = new();

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset CompletedAtUtc { get; init; }

    public long DurationMilliseconds { get; init; }

    public VerificationStatus TerminalClassification { get; init; }

    public VerificationStatus ArtifactIntegrity { get; init; }

    public string FixtureRoot { get; init; } = string.Empty;

    public string? ManifestSha256 { get; init; }

    public long? ManifestBytes { get; init; }

    public NativeKssObservation NativeKss { get; init; } = new();

    public List<VerificationCheck> Checks { get; init; } = [];

    public List<VerifiedFileEvidence> Files { get; init; } = [];

    public List<string> SideEffects { get; init; } = [];

    public bool EnvironmentReusable { get; init; }

    public List<string> UnsupportedGaps { get; init; } = [];
}

public sealed record RuntimeEnvironment
{
    public string OsDescription { get; init; } = string.Empty;

    public string FrameworkDescription { get; init; } = string.Empty;

    public string ProcessArchitecture { get; init; } = string.Empty;
}

public sealed record NativeKssObservation
{
    public NativeKssStatus Status { get; init; } = NativeKssStatus.NotRun;

    public NativeCompileExpectation? ExpectedCompileResult { get; init; }

    public string Reason { get; init; } = "Artifact integrity verification does not execute a KSS runtime.";
}

public sealed record VerificationCheck
{
    public string Id { get; init; } = string.Empty;

    public VerificationStatus Status { get; init; }

    public string Detail { get; init; } = string.Empty;
}

public sealed record VerifiedFileEvidence
{
    public string RelativePath { get; init; } = string.Empty;

    public long ExpectedBytes { get; init; }

    public long? ActualBytes { get; init; }

    public string ExpectedSha256 { get; init; } = string.Empty;

    public string? ActualSha256 { get; init; }

    public FixtureTextEncoding? ExpectedEncoding { get; init; }

    public FixtureTextEncoding? ActualEncoding { get; init; }

    public FixtureLineEnding? ExpectedLineEnding { get; init; }

    public FixtureLineEnding? ActualLineEnding { get; init; }

    public VerificationStatus Status { get; init; }
}

public sealed record FixtureVerificationOutcome(FixtureIntegrityReceipt Receipt)
{
    public bool Succeeded => Receipt.Payload.TerminalClassification == VerificationStatus.Passed;

    public int ExitCode => Succeeded ? 0 : 2;
}
