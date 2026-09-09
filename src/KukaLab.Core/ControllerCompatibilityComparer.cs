using System.Text.Json.Serialization;

namespace KukaLab.Core;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ControllerCompatibilityDisposition
{
    Compatible,
    Inconclusive,
    Incompatible,
    InvalidEvidence
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ControllerCompatibilityStatus
{
    Matched,
    Mismatched,
    Pending
}

public sealed record ControllerCompatibilityDimension
{
    public required string Dimension { get; init; }

    public required string Expected { get; init; }

    public string? Observed { get; init; }

    public required ControllerCompatibilityStatus Status { get; init; }

    public required string Detail { get; init; }
}

public sealed record ControllerCompatibilityResult
{
    public bool Succeeded { get; init; }

    public ControllerCompatibilityDisposition Disposition { get; init; }

    public string PackageId { get; init; } = string.Empty;

    public string PackageReceiptPayloadSha256 { get; init; } = string.Empty;

    public string ObservationFingerprintSha256 { get; init; } = string.Empty;

    public string ObservationReceiptPayloadSha256 { get; init; } = string.Empty;

    public List<ControllerCompatibilityDimension> Dimensions { get; init; } = [];

    public List<string> Errors { get; init; } = [];
}

public static class ControllerCompatibilityComparer
{
    public static ControllerCompatibilityResult Compare(
        ValidationPackageIntegrityReceipt packageReceipt,
        ControllerObservationReceipt observationReceipt)
    {
        ArgumentNullException.ThrowIfNull(packageReceipt);
        ArgumentNullException.ThrowIfNull(observationReceipt);

        var packageVerification = ValidationPackageReceiptVerifier.VerifyIntegrity(packageReceipt);
        var observationVerification = ControllerObservationReceiptVerifier.Verify(observationReceipt);
        var errors = packageVerification.Errors
            .Select(error => $"ValidationPackage: {error}")
            .Concat(observationVerification.Errors.Select(error => $"ControllerObservation: {error}"))
            .ToList();
        if (errors.Count > 0)
        {
            return new ControllerCompatibilityResult
            {
                Succeeded = false,
                Disposition = ControllerCompatibilityDisposition.InvalidEvidence,
                PackageId = packageReceipt.Payload?.DeclaredPackageId ?? string.Empty,
                PackageReceiptPayloadSha256 = packageReceipt.PayloadSha256,
                ObservationFingerprintSha256 = observationReceipt.Payload?.ObservationFingerprintSha256 ?? string.Empty,
                ObservationReceiptPayloadSha256 = observationReceipt.PayloadSha256,
                Errors = errors
            };
        }

        return CompareVerified(
            packageReceipt.Payload.Manifest.CompatibilityTarget,
            packageReceipt.Payload.DeclaredPackageId,
            packageReceipt.PayloadSha256,
            observationReceipt);
    }

    internal static ControllerCompatibilityResult CompareVerified(
        ValidationPackageCompatibilityTarget target,
        string packageId,
        string packageReceiptPayloadSha256,
        ControllerObservationReceipt observationReceipt)
    {
        var facts = observationReceipt.Payload.ObservedFacts;
        var dimensions = new List<ControllerCompatibilityDimension>
        {
            CompareExact(
                "ControllerFamily",
                target.ControllerModel,
                facts.ControllerFamily,
                "ValidationPackage controller target matches the owner-observed controller family.",
                "ValidationPackage controller target differs from the owner-observed controller family."),
            CompareExact(
                "KssVersion",
                target.KssVersion,
                facts.KssVersion,
                "ValidationPackage KSS target matches the owner-observed controller KSS version.",
                "ValidationPackage KSS target differs from the owner-observed controller KSS version."),
            new ControllerCompatibilityDimension
            {
                Dimension = "RobotModel",
                Expected = target.RobotModel,
                Observed = null,
                Status = ControllerCompatibilityStatus.Pending,
                Detail = "Controller-observation v1 deliberately keeps the robot model and machine data pending until WorkVisual project/archive evidence is ingested."
            }
        };

        var disposition = dimensions.Any(dimension => dimension.Status == ControllerCompatibilityStatus.Mismatched)
            ? ControllerCompatibilityDisposition.Incompatible
            : dimensions.Any(dimension => dimension.Status == ControllerCompatibilityStatus.Pending)
                ? ControllerCompatibilityDisposition.Inconclusive
                : ControllerCompatibilityDisposition.Compatible;

        return new ControllerCompatibilityResult
        {
            Succeeded = true,
            Disposition = disposition,
            PackageId = packageId,
            PackageReceiptPayloadSha256 = packageReceiptPayloadSha256,
            ObservationFingerprintSha256 = observationReceipt.Payload.ObservationFingerprintSha256,
            ObservationReceiptPayloadSha256 = observationReceipt.PayloadSha256,
            Dimensions = dimensions
        };
    }

    private static ControllerCompatibilityDimension CompareExact(
        string dimension,
        string expected,
        string observed,
        string matchedDetail,
        string mismatchedDetail)
    {
        var matched = string.Equals(
            expected.Trim(),
            observed.Trim(),
            StringComparison.OrdinalIgnoreCase);
        return new ControllerCompatibilityDimension
        {
            Dimension = dimension,
            Expected = expected,
            Observed = observed,
            Status = matched
                ? ControllerCompatibilityStatus.Matched
                : ControllerCompatibilityStatus.Mismatched,
            Detail = matched ? matchedDetail : mismatchedDetail
        };
    }
}
