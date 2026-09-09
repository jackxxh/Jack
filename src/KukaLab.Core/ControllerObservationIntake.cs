using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class ControllerObservationContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.controller-observation-receipt";
    public const int ReceiptSchemaVersion = 1;

    public static readonly IReadOnlyList<ControllerObservationPendingEvidence> RequiredPendingEvidence =
    [
        ControllerObservationPendingEvidence.RobotModel,
        ControllerObservationPendingEvidence.MachineData,
        ControllerObservationPendingEvidence.TechnologyPackages,
        ControllerObservationPendingEvidence.ToolBaseLoad
    ];

    public static readonly IReadOnlyList<string> RequiredUnsupportedClaims =
    [
        "The receipt binds owner-observed declarations; it does not independently authenticate the controller or source photographs.",
        "Controller endpoint reachability, WorkVisual project identity, robot MADA, technology packages and Tool/Base/Load remain unverified until separate receipts exist.",
        "The intake performs no network traffic, controller read/write, vendor application, native KSS execution, safety change or physical motion."
    ];
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ControllerObservationClassification
{
    OwnerObservedManual
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ControllerObservationPendingEvidence
{
    RobotModel,
    MachineData,
    TechnologyPackages,
    ToolBaseLoad
}

public sealed record ControllerObservationRequest
{
    public required string ControllerFamily { get; init; }

    public required string CabinetModel { get; init; }

    public required string KssVersion { get; init; }

    public required string KssBuild { get; init; }

    public required string KliAddress { get; init; }

    public required int PrefixLength { get; init; }

    public required string ObservedOnLocalDate { get; init; }

    public required List<string> EvidenceReferences { get; init; }
}

public sealed record ControllerObservedFacts
{
    public string ControllerFamily { get; init; } = string.Empty;

    public string CabinetModel { get; init; } = string.Empty;

    public string KssVersion { get; init; } = string.Empty;

    public string KssBuild { get; init; } = string.Empty;

    public string KliAddress { get; init; } = string.Empty;

    public int PrefixLength { get; init; }

    public string ObservedOnLocalDate { get; init; } = string.Empty;
}

public sealed record ControllerObservationReceipt
{
    public string SchemaIdentity { get; init; } = ControllerObservationContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = ControllerObservationContract.ReceiptSchemaVersion;

    public required ControllerObservationPayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record ControllerObservationPayload
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

    public ControllerObservationClassification Classification { get; init; }

    public string ObservationFingerprintSha256 { get; init; } = string.Empty;

    public ControllerObservedFacts ObservedFacts { get; init; } = new();

    public List<string> EvidenceReferences { get; init; } = [];

    public List<ControllerObservationPendingEvidence> PendingEvidence { get; init; } = [];

    public bool HardwareSerialStored { get; init; }

    public bool NetworkTrafficSent { get; init; }

    public bool ControllerReadAttempted { get; init; }

    public bool ControllerWriteAttempted { get; init; }

    public bool VendorApplicationInvoked { get; init; }

    public NativeKssStatus NativeKssStatus { get; init; } = NativeKssStatus.NotRun;

    public bool MotionCommandSent { get; init; }

    public List<VerificationCheck> Checks { get; init; } = [];

    public List<string> SideEffects { get; init; } = [];

    public bool EnvironmentReusable { get; init; } = true;

    public List<string> UnsupportedClaims { get; init; } = [];
}

public sealed record ControllerObservationOutcome(ControllerObservationReceipt Receipt)
{
    public int ExitCode => Receipt.Payload.TerminalClassification == VerificationStatus.Passed ? 0 : 2;
}

public sealed record ControllerObservationVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}

public sealed class ControllerObservationRunner(TimeProvider? timeProvider = null)
{
    private static readonly Regex AttemptIdPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex KssVersionPattern = new(
        "^[0-9]+\\.[0-9]+\\.[0-9]+$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex KssBuildPattern = new(
        "^B[0-9]+$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking | RegexOptions.IgnoreCase);
    private static readonly Regex EvidenceReferencePattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._:-]{2,127}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public ControllerObservationOutcome Run(ControllerObservationRequest request, string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptId);
        if (!AttemptIdPattern.IsMatch(attemptId))
        {
            throw new ArgumentException("Attempt ID must be 3-128 safe identifier characters.", nameof(attemptId));
        }

        var facts = NormalizeAndValidate(request);
        var evidenceReferences = request.EvidenceReferences
            .Select(reference => reference.Trim())
            .Order(StringComparer.Ordinal)
            .ToList();
        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var observationFingerprint = ReceiptSerialization.ComputeCanonicalSha256(new
        {
            classification = ControllerObservationClassification.OwnerObservedManual,
            observedFacts = facts,
            evidenceReferences,
            pendingEvidence = ControllerObservationContract.RequiredPendingEvidence
        });
        stopwatch.Stop();
        var completedAt = _timeProvider.GetUtcNow();
        var payload = new ControllerObservationPayload
        {
            ReceiptId = $"controller-observation-{attemptId}",
            AttemptId = attemptId,
            CoreAssemblySha256 = ComputeFileSha256(typeof(ControllerObservationRunner).Assembly.Location),
            Runtime = new RuntimeEnvironment
            {
                OsDescription = RuntimeInformation.OSDescription,
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
            },
            StartedAtUtc = startedAt,
            CompletedAtUtc = completedAt,
            DurationMilliseconds = Math.Max(0, stopwatch.ElapsedMilliseconds),
            TerminalClassification = VerificationStatus.Passed,
            Classification = ControllerObservationClassification.OwnerObservedManual,
            ObservationFingerprintSha256 = observationFingerprint,
            ObservedFacts = facts,
            EvidenceReferences = evidenceReferences,
            PendingEvidence = ControllerObservationContract.RequiredPendingEvidence.ToList(),
            Checks =
            [
                Passed("declared-observation", "Bound the normalized owner-observed controller declaration."),
                Passed("evidence-references", "Bound sanitized evidence references without copying source media or serial data."),
                Passed("pending-boundary", "Retained robot, MADA, technology-package and Tool/Base/Load evidence as pending.")
            ],
            UnsupportedClaims = ControllerObservationContract.RequiredUnsupportedClaims.ToList()
        };
        var receipt = new ControllerObservationReceipt
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        return new ControllerObservationOutcome(receipt);
    }

    private static ControllerObservedFacts NormalizeAndValidate(ControllerObservationRequest request)
    {
        var family = NormalizeText(request.ControllerFamily, nameof(request.ControllerFamily));
        var cabinet = NormalizeText(request.CabinetModel, nameof(request.CabinetModel));
        var version = NormalizeText(request.KssVersion, nameof(request.KssVersion));
        var build = NormalizeText(request.KssBuild, nameof(request.KssBuild)).ToUpperInvariant();
        if (!KssVersionPattern.IsMatch(version))
        {
            throw new ArgumentException("KSS version must use major.minor.patch notation.", nameof(request));
        }

        if (!KssBuildPattern.IsMatch(build))
        {
            throw new ArgumentException("KSS build must use B followed by digits.", nameof(request));
        }

        if (!IPAddress.TryParse(request.KliAddress, out var address)
            || address.AddressFamily != AddressFamily.InterNetwork
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.Broadcast)
            || IPAddress.IsLoopback(address))
        {
            throw new ArgumentException("KLI address must be a usable non-loopback IPv4 address.", nameof(request));
        }

        if (request.PrefixLength is < 1 or > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Prefix length must be from 1 through 30.");
        }

        if (!DateOnly.TryParseExact(request.ObservedOnLocalDate, "yyyy-MM-dd", out _))
        {
            throw new ArgumentException("Observed local date must use yyyy-MM-dd.", nameof(request));
        }

        if (request.EvidenceReferences is null
            || request.EvidenceReferences.Count is < 1 or > 16
            || request.EvidenceReferences.Any(reference =>
                string.IsNullOrWhiteSpace(reference)
                || !EvidenceReferencePattern.IsMatch(reference.Trim()))
            || request.EvidenceReferences.Select(reference => reference.Trim())
                .Distinct(StringComparer.Ordinal).Count() != request.EvidenceReferences.Count)
        {
            throw new ArgumentException("Provide 1-16 unique sanitized evidence identifiers.", nameof(request));
        }

        return new ControllerObservedFacts
        {
            ControllerFamily = family,
            CabinetModel = cabinet,
            KssVersion = version,
            KssBuild = build,
            KliAddress = address.ToString(),
            PrefixLength = request.PrefixLength,
            ObservedOnLocalDate = request.ObservedOnLocalDate
        };
    }

    private static string NormalizeText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > 128 || normalized.Any(character => char.IsControl(character)))
        {
            throw new ArgumentException("Observation text must be at most 128 printable characters.", parameterName);
        }

        return normalized;
    }

    private static VerificationCheck Passed(string id, string detail) => new()
    {
        Id = id,
        Status = VerificationStatus.Passed,
        Detail = detail
    };

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}

public static class ControllerObservationReceiptVerifier
{
    private static readonly Regex AttemptIdPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex EvidenceReferencePattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._:-]{2,127}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static ControllerObservationVerificationResult Verify(ControllerObservationReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        var payload = receipt.Payload;
        if (!string.Equals(receipt.SchemaIdentity, ControllerObservationContract.ReceiptSchemaIdentity, StringComparison.Ordinal)
            || receipt.SchemaVersion != ControllerObservationContract.ReceiptSchemaVersion)
        {
            errors.Add("controller-observation receipt schema is invalid");
        }

        if (string.IsNullOrWhiteSpace(payload.AttemptId)
            || !AttemptIdPattern.IsMatch(payload.AttemptId)
            || !string.Equals(payload.ReceiptId, $"controller-observation-{payload.AttemptId}", StringComparison.Ordinal))
        {
            errors.Add("receipt and bounded attempt identities are invalid");
        }

        if (!string.Equals(payload.CoreVersion, FixtureContract.CoreVersion, StringComparison.Ordinal)
            || !IsSha256(payload.CoreAssemblySha256)
            || payload.Runtime is null
            || string.IsNullOrWhiteSpace(payload.Runtime.OsDescription)
            || string.IsNullOrWhiteSpace(payload.Runtime.FrameworkDescription)
            || string.IsNullOrWhiteSpace(payload.Runtime.ProcessArchitecture))
        {
            errors.Add("Core/runtime identity is incomplete");
        }

        if (payload.StartedAtUtc > payload.CompletedAtUtc || payload.DurationMilliseconds < 0)
        {
            errors.Add("receipt timing is invalid");
        }

        if (payload.TerminalClassification != VerificationStatus.Passed
            || payload.Classification != ControllerObservationClassification.OwnerObservedManual)
        {
            errors.Add("manual observation classification is invalid");
        }

        if (payload.ObservedFacts is null || !HasValidFacts(payload.ObservedFacts))
        {
            errors.Add("observed controller facts are invalid");
        }

        if (payload.EvidenceReferences is null
            || payload.EvidenceReferences.Count is < 1 or > 16
            || payload.EvidenceReferences.Any(reference =>
                string.IsNullOrWhiteSpace(reference) || !EvidenceReferencePattern.IsMatch(reference))
            || payload.EvidenceReferences.Distinct(StringComparer.Ordinal).Count() != payload.EvidenceReferences.Count
            || !payload.EvidenceReferences.SequenceEqual(payload.EvidenceReferences.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            errors.Add("evidence references must be sanitized, unique and ordered");
        }

        if (payload.ObservedFacts is not null && payload.EvidenceReferences is not null)
        {
            var expectedFingerprint = ReceiptSerialization.ComputeCanonicalSha256(new
            {
                classification = ControllerObservationClassification.OwnerObservedManual,
                observedFacts = payload.ObservedFacts,
                evidenceReferences = payload.EvidenceReferences,
                pendingEvidence = ControllerObservationContract.RequiredPendingEvidence
            });
            if (!string.Equals(payload.ObservationFingerprintSha256, expectedFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("observation fingerprint does not bind the declared facts and evidence references");
            }
        }

        if (payload.PendingEvidence is null
            || !payload.PendingEvidence.SequenceEqual(ControllerObservationContract.RequiredPendingEvidence))
        {
            errors.Add("pending evidence boundary is incomplete");
        }

        if (payload.HardwareSerialStored
            || payload.NetworkTrafficSent
            || payload.ControllerReadAttempted
            || payload.ControllerWriteAttempted
            || payload.VendorApplicationInvoked
            || payload.NativeKssStatus != NativeKssStatus.NotRun
            || payload.MotionCommandSent)
        {
            errors.Add("observation intake cannot claim serial retention or runtime/controller effects");
        }

        var expectedCheckIds = new[] { "declared-observation", "evidence-references", "pending-boundary" };
        if (payload.Checks is null
            || !payload.Checks.Select(check => check.Id).SequenceEqual(expectedCheckIds)
            || payload.Checks.Any(check => check.Status != VerificationStatus.Passed || string.IsNullOrWhiteSpace(check.Detail)))
        {
            errors.Add("observation checks are incomplete or out of order");
        }

        if (payload.SideEffects is null
            || payload.SideEffects.Count > 1
            || payload.SideEffects.Any(effect => !effect.StartsWith("CreateNewReceiptFile:", StringComparison.Ordinal)))
        {
            errors.Add("only one create-new receipt side effect is permitted");
        }

        if (!payload.EnvironmentReusable
            || payload.UnsupportedClaims is null
            || !payload.UnsupportedClaims.SequenceEqual(ControllerObservationContract.RequiredUnsupportedClaims))
        {
            errors.Add("unsupported claims or reusable-state boundary is invalid");
        }

        if (!string.Equals(
                receipt.PayloadSha256,
                ReceiptSerialization.ComputeCanonicalSha256(payload),
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("payloadSha256 does not match the canonical payload");
        }

        return new ControllerObservationVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }

    private static bool HasValidFacts(ControllerObservedFacts facts)
    {
        if (new[] { facts.ControllerFamily, facts.CabinetModel, facts.KssVersion, facts.KssBuild, facts.ObservedOnLocalDate }
            .Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.Any(char.IsControl)))
        {
            return false;
        }

        return Regex.IsMatch(facts.KssVersion, "^[0-9]+\\.[0-9]+\\.[0-9]+$", RegexOptions.CultureInvariant)
            && Regex.IsMatch(facts.KssBuild, "^B[0-9]+$", RegexOptions.CultureInvariant)
            && DateOnly.TryParseExact(facts.ObservedOnLocalDate, "yyyy-MM-dd", out _)
            && IPAddress.TryParse(facts.KliAddress, out var address)
            && address.AddressFamily == AddressFamily.InterNetwork
            && !address.Equals(IPAddress.Any)
            && !address.Equals(IPAddress.Broadcast)
            && !IPAddress.IsLoopback(address)
            && string.Equals(facts.KliAddress, address.ToString(), StringComparison.Ordinal)
            && facts.PrefixLength is >= 1 and <= 30;
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);
}

public static class ControllerObservationReceiptWriter
{
    public static string WriteNew(string outputPath, ControllerObservationReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!ControllerObservationReceiptVerifier.Verify(receipt).Succeeded)
        {
            throw new InvalidOperationException("Controller-observation receipt integrity is invalid.");
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
