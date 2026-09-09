using System.Text.Json.Serialization;

namespace KukaLab.Core;

public static class ValidationPackageContract
{
    public const string ManifestFileName = "validation-package.json";
    public const string ManifestSchemaIdentity = "kuka.lab.validation-package";
    public const int ManifestSchemaVersion = 1;
    public const string ReceiptSchemaIdentity = "kuka.lab.validation-package-integrity-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const string PackageIdPrefix = "sha256:";
    public const string PackageIdPlaceholder = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

    public static readonly IReadOnlyList<string> RequiredUnsupportedClaims =
    [
        "Package integrity does not execute KUKA.Sim, OfficeLite, KSS, WorkVisual or a physical controller.",
        "Package integrity does not prove kinematic correctness, reachability, collision freedom, native KRL acceptance, cycle time or real-cell equivalence.",
        "Product artifact ownership remains with the producer; the Lab validates a frozen copy and does not become a parallel Workcell, Program, MotionPlan or KRL owner."
    ];
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidationPackageArtifactRole
{
    Profile,
    Workcell,
    MotionPlan,
    Expectations,
    KrlSource,
    KrlData
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidationPackageEvidenceLevel
{
    L1SimValidated,
    L2VirtualKssValidated,
    L3VirtualLoopValidated
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidationPackageProvenance
{
    ProductDeclared,
    OpenReferenceInferred,
    KukaSoftwareObserved,
    ControllerArchiveObserved,
    RealCellMeasured
}

public sealed record ValidationPackageManifest
{
    public required string SchemaIdentity { get; init; }

    public required int SchemaVersion { get; init; }

    public required string PackageId { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required ValidationPackageProducer Producer { get; init; }

    public required ValidationPackageUpstreamIdentity Upstream { get; init; }

    public required ValidationPackageArtifactIdentities ArtifactIdentities { get; init; }

    public required ValidationPackageCompatibilityTarget CompatibilityTarget { get; init; }

    public required ValidationPackageFrameProfile Frames { get; init; }

    public required List<ValidationPackageFileDeclaration> Files { get; init; }

    public required List<ValidationPackageCorrelation> Correlations { get; init; }

    public required List<string> RequestedFixtureIds { get; init; }

    public required List<string> ExpectedAssertions { get; init; }

    public required List<ValidationPackageEvidenceLevel> RequestedEvidenceLevels { get; init; }

    public required ValidationPackageSafety Safety { get; init; }
}

public sealed record ValidationPackageProducer
{
    public required string Product { get; init; }

    public required string Version { get; init; }

    public required string Revision { get; init; }
}

public sealed record ValidationPackageUpstreamIdentity
{
    public required string Revision { get; init; }

    public required string Fingerprint { get; init; }
}

public sealed record ValidationPackageArtifactIdentities
{
    public required string Workcell { get; init; }

    public required string Program { get; init; }

    public required string MotionPlan { get; init; }

    public required string Krl { get; init; }

    public string? Simulation { get; init; }
}

public sealed record ValidationPackageCompatibilityTarget
{
    public required string KukaSimVersion { get; init; }

    public required string KssVersion { get; init; }

    public required string RobotModel { get; init; }

    public required string ControllerModel { get; init; }
}

public sealed record ValidationPackageFrameProfile
{
    public required int ToolNumber { get; init; }

    public required ValidationPackageProvenance ToolProvenance { get; init; }

    public required int BaseNumber { get; init; }

    public required ValidationPackageProvenance BaseProvenance { get; init; }

    public required string LoadDeclaration { get; init; }

    public required ValidationPackageProvenance LoadProvenance { get; init; }
}

public sealed record ValidationPackageFileDeclaration
{
    public required string RelativePath { get; init; }

    public required ValidationPackageArtifactRole Role { get; init; }

    public required string Encoding { get; init; }

    public required long Bytes { get; init; }

    public required string Sha256 { get; init; }
}

public sealed record ValidationPackageCorrelation
{
    public required string CorrelationId { get; init; }

    public required string RhinoObjectId { get; init; }

    public required string ProgramOperationId { get; init; }

    public required string TrajectoryTargetId { get; init; }

    public required string TrajectorySegmentId { get; init; }

    public required string KrlFile { get; init; }

    public required int KrlLine { get; init; }
}

public sealed record ValidationPackageSafety
{
    public required string Classification { get; init; }

    public required bool RedactionConfirmed { get; init; }

    public required bool SecretsIncluded { get; init; }

    public required bool PhysicalMotionAllowed { get; init; }
}

public sealed record ValidationPackageVerificationRequest
{
    public required string PackageRoot { get; init; }

    public required string ReceiptOutputPath { get; init; }
}

public sealed record ValidationPackageObservedFile
{
    public string RelativePath { get; init; } = string.Empty;

    public long Bytes { get; init; }

    public string Sha256 { get; init; } = string.Empty;
}

public sealed record ValidationPackageIntegrityReceipt
{
    public string SchemaIdentity { get; init; } = ValidationPackageContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = ValidationPackageContract.ReceiptSchemaVersion;

    public required ValidationPackageIntegrityPayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record ValidationPackageIntegrityPayload
{
    public string ReceiptId { get; init; } = string.Empty;

    public string AttemptId { get; init; } = string.Empty;

    public string CoreVersion { get; init; } = FixtureContract.CoreVersion;

    public string CoreAssemblySha256 { get; init; } = string.Empty;

    public RuntimeEnvironment Runtime { get; init; } = new();

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset CompletedAtUtc { get; init; }

    public long DurationMilliseconds { get; init; }

    public VerificationStatus TerminalClassification { get; init; }

    public VerificationStatus ArtifactIntegrity { get; init; }

    public string DeclaredPackageId { get; init; } = string.Empty;

    public string ComputedPackageId { get; init; } = string.Empty;

    public string ManifestSha256 { get; init; } = string.Empty;

    public long ManifestBytes { get; init; }

    public ValidationPackageManifest Manifest { get; init; } = null!;

    public List<ValidationPackageObservedFile> Files { get; init; } = [];

    public List<VerificationCheck> Checks { get; init; } = [];

    public NativeKssObservation NativeKss { get; init; } = new();

    public List<string> SideEffects { get; init; } = [];

    public bool EnvironmentReusable { get; init; } = true;

    public List<string> UnsupportedClaims { get; init; } = [];
}

public sealed record ValidationPackageVerificationOutcome(ValidationPackageIntegrityReceipt Receipt)
{
    public bool Succeeded => Receipt.Payload.TerminalClassification == VerificationStatus.Passed;

    public int ExitCode => Succeeded ? 0 : 2;
}

public sealed record ValidationPackageReceiptVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public bool CurrentPackageVerified { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}
