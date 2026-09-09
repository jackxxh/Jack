using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class OfficeLiteDeliveryInspectionContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.officelite-delivery-inspection-receipt";
    public const int ReceiptSchemaVersion = 1;
}

public enum OfficeLiteDeliveryKind
{
    Unknown,
    LegacyVmware,
    HyperVExport,
    HyperVDiskOnly,
    Mixed
}

public enum OfficeLiteDeliveryCheckStatus
{
    Passed,
    Blocked,
    Failed
}

public enum OfficeLiteDeliveryTerminalClassification
{
    Eligible,
    Blocked,
    Failed
}

public sealed record OfficeLiteDeliveryInspectionRequest
{
    public required string DeliveryRoot { get; init; }

    public required string DeclaredProductVersion { get; init; }

    public required int DeclaredBuildNumber { get; init; }

    public required string ProvenanceReference { get; init; }
}

public sealed record OfficeLiteDeliveryInspectionReceipt
{
    public string SchemaIdentity { get; init; } = OfficeLiteDeliveryInspectionContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = OfficeLiteDeliveryInspectionContract.ReceiptSchemaVersion;

    public required OfficeLiteDeliveryInspectionPayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record OfficeLiteDeliveryInspectionPayload
{
    public string ReceiptId { get; init; } = string.Empty;

    public string AttemptId { get; init; } = string.Empty;

    public string CoreVersion { get; init; } = FixtureContract.CoreVersion;

    public string CoreAssemblySha256 { get; init; } = string.Empty;

    public RuntimeEnvironment Runtime { get; init; } = new();

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset CompletedAtUtc { get; init; }

    public long DurationMilliseconds { get; init; }

    public OfficeLiteDeliveryTerminalClassification TerminalClassification { get; init; }

    public string DeliveryRoot { get; init; } = string.Empty;

    public string DeclaredProductVersion { get; init; } = string.Empty;

    public int DeclaredBuildNumber { get; init; }

    public string ProvenanceReference { get; init; } = string.Empty;

    public OfficeLiteDeliveryKind DeliveryKind { get; init; }

    public bool ScanCompleted { get; init; }

    public bool HyperVImportEligible { get; init; }

    public string TreeSha256 { get; init; } = string.Empty;

    public List<OfficeLiteDeliveryCheck> Checks { get; init; } = [];

    public List<OfficeLiteDeliveryFileEvidence> Files { get; init; } = [];

    public List<string> SideEffects { get; init; } = [];

    public List<string> UnsupportedClaims { get; init; } = [];
}

public sealed record OfficeLiteDeliveryCheck
{
    public string Id { get; init; } = string.Empty;

    public OfficeLiteDeliveryCheckStatus Status { get; init; }

    public string Detail { get; init; } = string.Empty;
}

public sealed record OfficeLiteDeliveryFileEvidence
{
    public string RelativePath { get; init; } = string.Empty;

    public long Bytes { get; init; }

    public string Sha256 { get; init; } = string.Empty;
}

public sealed record OfficeLiteDeliveryInspectionOutcome(OfficeLiteDeliveryInspectionReceipt Receipt)
{
    public int ExitCode => Receipt.Payload.TerminalClassification switch
    {
        OfficeLiteDeliveryTerminalClassification.Eligible => 0,
        OfficeLiteDeliveryTerminalClassification.Failed => 2,
        _ => 3
    };
}

public sealed partial class OfficeLiteDeliveryInspector
{
    private readonly TimeProvider _timeProvider;

    public OfficeLiteDeliveryInspector(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public OfficeLiteDeliveryInspectionOutcome Inspect(
        OfficeLiteDeliveryInspectionRequest request,
        string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DeliveryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DeclaredProductVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptId);
        if (!AttemptIdPattern().IsMatch(attemptId))
        {
            throw new ArgumentException(
                "Attempt ID must match [A-Za-z0-9][A-Za-z0-9._-]{2,127}.",
                nameof(attemptId));
        }

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var root = Path.GetFullPath(request.DeliveryRoot);
        var files = new List<OfficeLiteDeliveryFileEvidence>();
        var scanCompleted = false;
        string? scanError = null;

        try
        {
            if (!Directory.Exists(root))
            {
                scanError = "The declared OfficeLite delivery root does not exist.";
            }
            else if (HasReparsePoint(root))
            {
                scanError = "The OfficeLite delivery root cannot be a reparse point.";
            }
            else
            {
                files = SnapshotFiles(root);
                scanCompleted = true;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            scanError = $"The OfficeLite delivery tree could not be inventoried: {exception.Message}";
        }

        var kind = Classify(files);
        var checks = BuildChecks(
            scanCompleted,
            scanError,
            request.DeclaredProductVersion,
            request.DeclaredBuildNumber,
            request.ProvenanceReference,
            kind,
            files.Count);
        var terminal = ClassifyTerminal(checks);
        var eligible = terminal == OfficeLiteDeliveryTerminalClassification.Eligible
            && IsSupported87HyperVBuild(request.DeclaredProductVersion, request.DeclaredBuildNumber)
            && !string.IsNullOrWhiteSpace(request.ProvenanceReference)
            && kind == OfficeLiteDeliveryKind.HyperVExport;
        var treeSha256 = ComputeTreeSha256(files);

        stopwatch.Stop();
        var payload = new OfficeLiteDeliveryInspectionPayload
        {
            ReceiptId = $"officelite-delivery-{attemptId}",
            AttemptId = attemptId,
            CoreAssemblySha256 = ComputeFileSha256(typeof(OfficeLiteDeliveryInspector).Assembly.Location),
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
            DeliveryRoot = root,
            DeclaredProductVersion = request.DeclaredProductVersion.Trim(),
            DeclaredBuildNumber = request.DeclaredBuildNumber,
            ProvenanceReference = request.ProvenanceReference.Trim(),
            DeliveryKind = kind,
            ScanCompleted = scanCompleted,
            HyperVImportEligible = eligible,
            TreeSha256 = treeSha256,
            Checks = checks,
            Files = files,
            SideEffects = [],
            UnsupportedClaims =
            [
                "File shape and a provenance reference do not prove vendor authenticity, purchase entitlement or license readiness.",
                "This inspection does not boot OfficeLite or prove native KSS controller readiness.",
                "This inspection does not prove VRC Interface installation or licensing, KUKA.Sim controller-version matching, connection or motion synchronization."
            ]
        };
        var receipt = new OfficeLiteDeliveryInspectionReceipt
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        return new OfficeLiteDeliveryInspectionOutcome(receipt);
    }

    internal static List<OfficeLiteDeliveryFileEvidence> SnapshotFiles(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        var pending = new Stack<string>();
        pending.Push(fullRoot);
        var files = new List<OfficeLiteDeliveryFileEvidence>();

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var entry in new DirectoryInfo(current).EnumerateFileSystemInfos())
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException($"Reparse points are not allowed in an OfficeLite delivery: {entry.FullName}");
                }

                if (entry is DirectoryInfo directory)
                {
                    pending.Push(directory.FullName);
                    continue;
                }

                if (entry is not FileInfo file)
                {
                    throw new IOException($"Unsupported filesystem entry in OfficeLite delivery: {entry.FullName}");
                }

                var relativePath = NormalizeRelativePath(fullRoot, file.FullName);
                using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                var sha256 = Convert.ToHexString(SHA256.HashData(stream));
                files.Add(new OfficeLiteDeliveryFileEvidence
                {
                    RelativePath = relativePath,
                    Bytes = stream.Length,
                    Sha256 = sha256
                });
            }
        }

        return files
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToList();
    }

    internal static OfficeLiteDeliveryKind Classify(IReadOnlyCollection<OfficeLiteDeliveryFileEvidence> files)
    {
        var hasVmware = files.Any(file => HasExtension(file.RelativePath, ".vmx")
            || HasExtension(file.RelativePath, ".vmdk"));
        var hasVhdx = files.Any(file => HasExtension(file.RelativePath, ".vhdx"));
        var hasHyperVConfiguration = files.Any(file => HasExtension(file.RelativePath, ".vmcx")
            || (HasExtension(file.RelativePath, ".xml")
                && HasPathSegment(file.RelativePath, "Virtual Machines")));
        var hasHyperV = hasVhdx || hasHyperVConfiguration;

        if (hasVmware && hasHyperV)
        {
            return OfficeLiteDeliveryKind.Mixed;
        }

        if (hasVmware)
        {
            return OfficeLiteDeliveryKind.LegacyVmware;
        }

        if (hasVhdx && hasHyperVConfiguration)
        {
            return OfficeLiteDeliveryKind.HyperVExport;
        }

        if (hasVhdx)
        {
            return OfficeLiteDeliveryKind.HyperVDiskOnly;
        }

        return OfficeLiteDeliveryKind.Unknown;
    }

    internal static bool IsSupported87HyperVBuild(string productVersion, int buildNumber)
    {
        return Version.TryParse(productVersion, out var version)
            && version.Major == 8
            && version.Minor == 7
            && buildNumber >= 5;
    }

    internal static string ComputeTreeSha256(IReadOnlyCollection<OfficeLiteDeliveryFileEvidence> files) =>
        ReceiptSerialization.ComputeCanonicalSha256(
            files
                .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(file => file.RelativePath, StringComparer.Ordinal)
                .Select(file => new { file.RelativePath, file.Bytes, file.Sha256 })
                .ToList());

    internal static List<OfficeLiteDeliveryCheck> BuildChecks(
        bool scanCompleted,
        string? scanError,
        string productVersion,
        int buildNumber,
        string provenanceReference,
        OfficeLiteDeliveryKind kind,
        int fileCount)
    {
        var checks = new List<OfficeLiteDeliveryCheck>
        {
            scanCompleted
                ? Passed("delivery-root", "Delivery root exists, contains no traversed reparse point and was read without mutation.")
                : Failed("delivery-root", scanError ?? "Delivery tree inventory failed."),
            IsSupported87HyperVBuild(productVersion, buildNumber)
                ? Passed("declared-release-build", "Declared OfficeLite 8.7 build is Build 05 or later.")
                : Blocked("declared-release-build", "Current acceptance evidence covers OfficeLite 8.7 Build 05 or later; Build 04 and earlier are VMware-generation deliveries."),
            !string.IsNullOrWhiteSpace(provenanceReference)
                ? Passed("provenance-reference", "An opaque owner/vendor delivery reference was supplied; authenticity is not inferred from this value.")
                : Blocked("provenance-reference", "An opaque my.KUKA/order/support delivery reference is required without including credentials or license data."),
            ShapeCheck(kind),
            scanCompleted
                ? Passed("file-tree-integrity", $"Hashed {fileCount} delivery file(s) into a deterministic tree identity.")
                : Failed("file-tree-integrity", "No complete delivery tree identity could be produced.")
        };
        return checks;
    }

    internal static OfficeLiteDeliveryTerminalClassification ClassifyTerminal(
        IReadOnlyCollection<OfficeLiteDeliveryCheck> checks) =>
        checks.Any(check => check.Status == OfficeLiteDeliveryCheckStatus.Failed)
            ? OfficeLiteDeliveryTerminalClassification.Failed
            : checks.Any(check => check.Status == OfficeLiteDeliveryCheckStatus.Blocked)
                ? OfficeLiteDeliveryTerminalClassification.Blocked
                : OfficeLiteDeliveryTerminalClassification.Eligible;

    private static OfficeLiteDeliveryCheck ShapeCheck(OfficeLiteDeliveryKind kind) => kind switch
    {
        OfficeLiteDeliveryKind.HyperVExport => Passed(
            "delivery-shape",
            "Delivery contains both a Hyper-V import configuration and at least one VHDX disk."),
        OfficeLiteDeliveryKind.LegacyVmware => Blocked(
            "delivery-shape",
            "Delivery contains VMware VMX/VMDK surfaces and is not a native Hyper-V import package."),
        OfficeLiteDeliveryKind.HyperVDiskOnly => Blocked(
            "delivery-shape",
            "A VHDX alone is not accepted as the complete KUKA Hyper-V export/import package."),
        OfficeLiteDeliveryKind.Mixed => Failed(
            "delivery-shape",
            "Delivery mixes VMware and Hyper-V surfaces; fail closed until an unambiguous original package is supplied."),
        _ => Blocked(
            "delivery-shape",
            "No accepted OfficeLite Hyper-V export/import structure was found.")
    };

    private static string NormalizeRelativePath(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
        if (Path.IsPathRooted(relative)
            || string.Equals(relative, "..", StringComparison.Ordinal)
            || relative.StartsWith("../", StringComparison.Ordinal)
            || relative.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new IOException("A delivery path escaped or ambiguously addressed the declared root.");
        }

        return relative;
    }

    private static bool HasExtension(string path, string extension) =>
        string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase);

    private static bool HasPathSegment(string path, string segment) =>
        path.Split('/').Any(value => string.Equals(value, segment, StringComparison.OrdinalIgnoreCase));

    private static bool HasReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static OfficeLiteDeliveryCheck Passed(string id, string detail) =>
        new() { Id = id, Status = OfficeLiteDeliveryCheckStatus.Passed, Detail = detail };

    private static OfficeLiteDeliveryCheck Blocked(string id, string detail) =>
        new() { Id = id, Status = OfficeLiteDeliveryCheckStatus.Blocked, Detail = detail };

    private static OfficeLiteDeliveryCheck Failed(string id, string detail) =>
        new() { Id = id, Status = OfficeLiteDeliveryCheckStatus.Failed, Detail = detail };

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex AttemptIdPattern();
}

public sealed record OfficeLiteDeliveryReceiptVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public bool CurrentTreeVerified { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}

public static class OfficeLiteDeliveryReceiptVerifier
{
    private static readonly string[] RequiredCheckIds =
    [
        "delivery-root",
        "declared-release-build",
        "provenance-reference",
        "delivery-shape",
        "file-tree-integrity"
    ];

    public static OfficeLiteDeliveryReceiptVerificationResult VerifyIntegrity(
        OfficeLiteDeliveryInspectionReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        var payload = receipt.Payload;

        if (!string.Equals(
                receipt.SchemaIdentity,
                OfficeLiteDeliveryInspectionContract.ReceiptSchemaIdentity,
                StringComparison.Ordinal))
        {
            errors.Add($"schemaIdentity must be {OfficeLiteDeliveryInspectionContract.ReceiptSchemaIdentity}");
        }

        if (receipt.SchemaVersion != OfficeLiteDeliveryInspectionContract.ReceiptSchemaVersion)
        {
            errors.Add($"schemaVersion must be {OfficeLiteDeliveryInspectionContract.ReceiptSchemaVersion}");
        }

        if (string.IsNullOrWhiteSpace(payload.ReceiptId)
            || string.IsNullOrWhiteSpace(payload.AttemptId)
            || string.IsNullOrWhiteSpace(payload.DeliveryRoot)
            || string.IsNullOrWhiteSpace(payload.DeclaredProductVersion))
        {
            errors.Add("receiptId, attemptId, deliveryRoot and declaredProductVersion are required");
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

        if (payload.Files is null
            || payload.Checks is null
            || payload.SideEffects is null
            || payload.UnsupportedClaims is null)
        {
            errors.Add("payload evidence collections cannot be null");
        }
        else
        {
            ValidateFiles(payload, errors);
            ValidateChecks(payload, errors);
        }

        if (!string.Equals(
                receipt.PayloadSha256,
                ReceiptSerialization.ComputeCanonicalSha256(payload),
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("payloadSha256 does not match the canonical payload");
        }

        return new OfficeLiteDeliveryReceiptVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            CurrentTreeVerified = false,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }

    public static OfficeLiteDeliveryReceiptVerificationResult VerifyCurrentRoot(
        OfficeLiteDeliveryInspectionReceipt receipt)
    {
        var integrity = VerifyIntegrity(receipt);
        var errors = integrity.Errors.ToList();
        var currentTreeVerified = false;
        if (integrity.Succeeded)
        {
            try
            {
                var current = new OfficeLiteDeliveryInspector().Inspect(
                    new OfficeLiteDeliveryInspectionRequest
                    {
                        DeliveryRoot = receipt.Payload.DeliveryRoot,
                        DeclaredProductVersion = receipt.Payload.DeclaredProductVersion,
                        DeclaredBuildNumber = receipt.Payload.DeclaredBuildNumber,
                        ProvenanceReference = receipt.Payload.ProvenanceReference
                    },
                    $"current-{Guid.NewGuid():N}");
                currentTreeVerified = current.Receipt.Payload.ScanCompleted
                    && string.Equals(
                        current.Receipt.Payload.TreeSha256,
                        receipt.Payload.TreeSha256,
                        StringComparison.OrdinalIgnoreCase)
                    && current.Receipt.Payload.DeliveryKind == receipt.Payload.DeliveryKind;
                if (!currentTreeVerified)
                {
                    errors.Add("The current delivery tree no longer matches the receipt tree identity and delivery kind.");
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                errors.Add($"The current delivery tree could not be verified: {exception.Message}");
            }
        }

        return integrity with
        {
            Succeeded = errors.Count == 0 && currentTreeVerified,
            CurrentTreeVerified = currentTreeVerified,
            Errors = errors
        };
    }

    private static void ValidateFiles(
        OfficeLiteDeliveryInspectionPayload payload,
        List<string> errors)
    {
        if (payload.Files.Any(file => !IsSafeRelativePath(file.RelativePath)
                || file.Bytes < 0
                || file.Sha256.Length != 64
                || file.Sha256.Any(character => !Uri.IsHexDigit(character)))
            || payload.Files
                .GroupBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1))
        {
            errors.Add("payload.files must have safe, unique paths, non-negative sizes and SHA-256 identities");
        }

        var expectedTreeSha256 = OfficeLiteDeliveryInspector.ComputeTreeSha256(payload.Files);
        if (!string.Equals(payload.TreeSha256, expectedTreeSha256, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("payload.treeSha256 does not match the canonical file tree");
        }

        var expectedKind = OfficeLiteDeliveryInspector.Classify(payload.Files);
        if (payload.DeliveryKind != expectedKind)
        {
            errors.Add("payload.deliveryKind does not match the recorded file tree");
        }
    }

    private static void ValidateChecks(
        OfficeLiteDeliveryInspectionPayload payload,
        List<string> errors)
    {
        var actualIds = payload.Checks.Select(check => check.Id).ToList();
        if (payload.Checks.Any(check => string.IsNullOrWhiteSpace(check.Id))
            || actualIds.Distinct(StringComparer.Ordinal).Count() != actualIds.Count
            || !RequiredCheckIds.Order(StringComparer.Ordinal).SequenceEqual(
                actualIds.Order(StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            errors.Add("payload.checks must contain the exact unique delivery-inspection check set");
            return;
        }

        var expected = OfficeLiteDeliveryInspector.BuildChecks(
            payload.ScanCompleted,
            payload.ScanCompleted ? null : "Receipt records an incomplete scan.",
            payload.DeclaredProductVersion,
            payload.DeclaredBuildNumber,
            payload.ProvenanceReference,
            payload.DeliveryKind,
            payload.Files.Count);
        foreach (var expectedCheck in expected)
        {
            var actual = payload.Checks.Single(check => check.Id == expectedCheck.Id);
            if (actual.Status != expectedCheck.Status)
            {
                errors.Add($"payload.checks[{actual.Id}] status does not follow the delivery-inspection rules");
            }
        }

        var expectedTerminal = OfficeLiteDeliveryInspector.ClassifyTerminal(expected);
        if (payload.TerminalClassification != expectedTerminal)
        {
            errors.Add("payload.terminalClassification does not follow the delivery-inspection rules");
        }

        var expectedEligibility = expectedTerminal == OfficeLiteDeliveryTerminalClassification.Eligible
            && OfficeLiteDeliveryInspector.IsSupported87HyperVBuild(
                payload.DeclaredProductVersion,
                payload.DeclaredBuildNumber)
            && !string.IsNullOrWhiteSpace(payload.ProvenanceReference)
            && payload.DeliveryKind == OfficeLiteDeliveryKind.HyperVExport;
        if (payload.HyperVImportEligible != expectedEligibility)
        {
            errors.Add("payload.hyperVImportEligible does not follow the delivery-inspection rules");
        }
    }

    private static bool IsSafeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || Path.IsPathRooted(path)
            || path.Contains('\\')
            || string.Equals(path, "..", StringComparison.Ordinal)
            || path.StartsWith("../", StringComparison.Ordinal))
        {
            return false;
        }

        return path.Split('/').All(segment => segment is not ("" or "." or ".."));
    }
}

public static class OfficeLiteDeliveryReceiptWriter
{
    public static string WriteNew(string outputPath, OfficeLiteDeliveryInspectionReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!OfficeLiteDeliveryReceiptVerifier.VerifyIntegrity(receipt).Succeeded)
        {
            throw new InvalidOperationException("OfficeLite delivery receipt integrity is invalid.");
        }

        var fullPath = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Receipt output must use the .json extension.", nameof(outputPath));
        }

        var deliveryRoot = Path.GetFullPath(receipt.Payload.DeliveryRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (fullPath.StartsWith(deliveryRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Receipt output must be outside the inspected delivery root.",
                nameof(outputPath));
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
