using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class StageOneEvidenceBundleContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.stage-one-evidence-bundle-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const string AcceptedComparisonReceiptSha256 = "32FFF7D5FEA9A811C2DA5CCE8246DEAEEFEF58E24E79BF015419BC5EE605DA78";
    public const string AcceptedVirtualLoopReceiptSha256 = "25777C95FAE0C9E07E42A4861B72C6A083095104A84ECF22E0DE8B6D1F98409F";

    internal static readonly IReadOnlyList<string> RequiredUnsupportedClaims =
    [
        "The candidate has not been executed by native KSS, KUKA.Sim or a physical controller; candidateNativeStatus remains NotRun.",
        "The accepted item-9D and item-9E receipts prove fixed fixture and matching KR3 environment capabilities, not arbitrary-candidate native execution or exact KR 210 C01 OfficeLite path equivalence.",
        "Collision, calibrated Tool/Base/Load, production cycle time, payload, mastering, safety and physical qualification remain outside this receipt.",
        "This file-only composition starts no vendor software, sends no network traffic and does not access Rhino or a physical controller."
    ];
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StageOneCandidateEvidenceKind
{
    RawKrlIdentity,
    ValidationPackageStaticPreflight
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StageOneCandidateReadiness
{
    IdentityVerified,
    StaticPreflightReady,
    Rejected
}

public sealed record StageOneEvidenceBundleRequest
{
    public required StageOneCandidateEvidenceKind CandidateKind { get; init; }
    public required string CandidateRoot { get; init; }
    public required string CandidateReceiptPath { get; init; }
    public string? ValidationPackageReceiptPath { get; init; }
    public required string ComparisonReceiptPath { get; init; }
    public required string VirtualLoopReceiptPath { get; init; }
    public required string ReceiptOutputPath { get; init; }
}

public sealed record StageOneEvidenceInput
{
    public string Role { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string SchemaIdentity { get; init; } = string.Empty;
    public string ReceiptId { get; init; } = string.Empty;
    public string PayloadSha256 { get; init; } = string.Empty;
    public string ActualReceiptSha256 { get; init; } = string.Empty;
    public string ExpectedReceiptSha256 { get; init; } = string.Empty;
}

public sealed record StageOneCandidateEvidence
{
    public StageOneCandidateEvidenceKind Kind { get; init; }
    public StageOneCandidateReadiness Readiness { get; init; }
    public string CandidateId { get; init; } = string.Empty;
    public string CandidateRoot { get; init; } = string.Empty;
    public int ProgramCount { get; init; }
    public int MotionInstructionCount { get; init; }
    public NativeKssStatus CandidateNativeStatus { get; init; } = NativeKssStatus.NotRun;
    public string CandidateKukaSimStatus { get; init; } = "NotRun";
    public string Reason { get; init; } = string.Empty;
}

public sealed record StageOneEnvironmentBaseline
{
    public string ComparisonValidationStatus { get; init; } = string.Empty;
    public string VirtualLoopScope { get; init; } = string.Empty;
    public bool FixedFixtureNativeKssValidated { get; init; }
    public bool ExactC01IntegratedValidated { get; init; }
    public bool MatchingKr3VirtualLoopValidated { get; init; }
    public bool CandidateNativeExecutionValidated { get; init; }
}

public sealed record StageOneEvidenceBundleReceipt
{
    public string SchemaIdentity { get; init; } = StageOneEvidenceBundleContract.ReceiptSchemaIdentity;
    public int SchemaVersion { get; init; } = StageOneEvidenceBundleContract.ReceiptSchemaVersion;
    public required StageOneEvidenceBundlePayload Payload { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record StageOneEvidenceBundlePayload
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
    public StageOneCandidateEvidence Candidate { get; init; } = new();
    public StageOneEnvironmentBaseline EnvironmentBaseline { get; init; } = new();
    public List<StageOneEvidenceInput> Inputs { get; init; } = [];
    public bool VendorSoftwareStarted { get; init; }
    public bool NetworkTrafficSent { get; init; }
    public bool RhinoAccessed { get; init; }
    public bool PhysicalControllerContacted { get; init; }
    public bool EnvironmentReusable { get; init; }
    public List<string> SideEffects { get; init; } = [];
    public List<string> UnsupportedClaims { get; init; } = [];
}

public sealed record StageOneEvidenceBundleOutcome(StageOneEvidenceBundleReceipt Receipt)
{
    public bool Succeeded => Receipt.Payload.TerminalClassification == EnvironmentTerminalClassification.Ready;
    public int ExitCode => Succeeded ? 0 : 2;
}

internal sealed record StageOneVerifiedCandidate(
    StageOneCandidateEvidence Evidence,
    List<StageOneEvidenceInput> Inputs);

internal sealed record StageOneVerifiedBaseline(List<StageOneEvidenceInput> Inputs);

internal interface IStageOneBaselineVerifier
{
    StageOneVerifiedBaseline Verify(string comparisonReceiptPath, string virtualLoopReceiptPath);
}

internal sealed class StageOneBaselineVerifier : IStageOneBaselineVerifier
{
    public StageOneVerifiedBaseline Verify(string comparisonReceiptPath, string virtualLoopReceiptPath)
    {
        var comparison = ReadStable(comparisonReceiptPath);
        var comparisonHash = Hash(comparison.Bytes);
        if (!string.Equals(comparisonHash, StageOneEvidenceBundleContract.AcceptedComparisonReceiptSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Item-9D comparison receipt does not match the accepted immutable SHA-256.");
        }

        var comparisonReceipt = ReceiptSerialization.OfflineLoopComparisonFromJson(Encoding.UTF8.GetString(comparison.Bytes));
        var comparisonVerification = OfflineLoopComparisonReceiptVerifier.Verify(comparisonReceipt);
        if (!comparisonVerification.Succeeded)
        {
            throw new InvalidDataException("Item-9D comparison receipt verification failed: " + string.Join("; ", comparisonVerification.Errors));
        }

        var virtualLoop = ReadStable(virtualLoopReceiptPath);
        var virtualLoopHash = Hash(virtualLoop.Bytes);
        if (!string.Equals(virtualLoopHash, StageOneEvidenceBundleContract.AcceptedVirtualLoopReceiptSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Item-9E virtual-loop receipt does not match the accepted immutable SHA-256.");
        }

        var virtualLoopReceipt = ReceiptSerialization.KukaSimOfficeLiteVirtualLoopFromJson(Encoding.UTF8.GetString(virtualLoop.Bytes));
        var virtualVerification = KukaSimOfficeLiteVirtualLoopReceiptVerifier.Verify(virtualLoopReceipt);
        if (!virtualVerification.Succeeded)
        {
            throw new InvalidDataException("Item-9E virtual-loop receipt verification failed: " + string.Join("; ", virtualVerification.Errors));
        }

        return new StageOneVerifiedBaseline(
        [
            Input(
                "item9d-offline-loop-comparison",
                comparison.Path,
                comparisonReceipt.SchemaIdentity,
                comparisonReceipt.Payload.ReceiptId,
                comparisonReceipt.PayloadSha256,
                comparisonHash,
                StageOneEvidenceBundleContract.AcceptedComparisonReceiptSha256),
            Input(
                "item9e-kukasim-officelite-virtual-loop",
                virtualLoop.Path,
                virtualLoopReceipt.SchemaIdentity,
                virtualLoopReceipt.Payload.ReceiptId,
                virtualLoopReceipt.PayloadSha256,
                virtualLoopHash,
                StageOneEvidenceBundleContract.AcceptedVirtualLoopReceiptSha256)
        ]);
    }

    private static StageOneEvidenceInput Input(
        string role,
        string path,
        string schemaIdentity,
        string receiptId,
        string payloadSha256,
        string actualReceiptSha256,
        string expectedReceiptSha256) => new()
        {
            Role = role,
            Path = path,
            SchemaIdentity = schemaIdentity,
            ReceiptId = receiptId,
            PayloadSha256 = payloadSha256,
            ActualReceiptSha256 = actualReceiptSha256,
            ExpectedReceiptSha256 = expectedReceiptSha256
        };

    internal static (string Path, byte[] Bytes) ReadStable(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var info = new FileInfo(fullPath);
        if (!info.Exists)
        {
            throw new FileNotFoundException("Required evidence receipt was not found.", fullPath);
        }

        info.Refresh();
        var length = info.Length;
        var writeTime = info.LastWriteTimeUtc;
        var bytes = File.ReadAllBytes(fullPath);
        info.Refresh();
        if (bytes.LongLength != length || info.Length != length || info.LastWriteTimeUtc != writeTime)
        {
            throw new IOException("Evidence receipt changed while it was being read: " + fullPath);
        }
        return (fullPath, bytes);
    }

    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}

public sealed partial class StageOneEvidenceBundleRunner
{
    private readonly TimeProvider _timeProvider;
    private readonly IStageOneBaselineVerifier _baselineVerifier;

    public StageOneEvidenceBundleRunner(TimeProvider? timeProvider = null)
        : this(timeProvider ?? TimeProvider.System, new StageOneBaselineVerifier())
    {
    }

    internal StageOneEvidenceBundleRunner(TimeProvider timeProvider, IStageOneBaselineVerifier baselineVerifier)
    {
        _timeProvider = timeProvider;
        _baselineVerifier = baselineVerifier;
    }

    public StageOneEvidenceBundleOutcome Run(StageOneEvidenceBundleRequest request, string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptId);
        if (!AttemptIdPattern().IsMatch(attemptId))
        {
            throw new ArgumentException("Attempt ID must match [A-Za-z0-9][A-Za-z0-9._-]{2,127}.", nameof(attemptId));
        }

        var candidateRoot = Path.GetFullPath(request.CandidateRoot);
        var outputPath = Path.GetFullPath(request.ReceiptOutputPath);
        ValidateOutputBoundary(request.CandidateKind, candidateRoot, outputPath);
        if (File.Exists(outputPath))
        {
            throw new IOException("Receipt output already exists: " + outputPath);
        }

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var candidate = VerifyCandidate(request, candidateRoot);
        var baseline = _baselineVerifier.Verify(request.ComparisonReceiptPath, request.VirtualLoopReceiptPath);
        stopwatch.Stop();
        var terminal = candidate.Evidence.Readiness == StageOneCandidateReadiness.Rejected
            ? EnvironmentTerminalClassification.Failed
            : EnvironmentTerminalClassification.Ready;
        var payload = new StageOneEvidenceBundlePayload
        {
            ReceiptId = $"stage-one-evidence-{attemptId}",
            AttemptId = attemptId,
            CoreAssemblySha256 = ComputeFileSha256(typeof(StageOneEvidenceBundleRunner).Assembly.Location),
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
            Candidate = candidate.Evidence,
            EnvironmentBaseline = new StageOneEnvironmentBaseline
            {
                ComparisonValidationStatus = OfflineLoopComparisonContract.ValidationStatus,
                VirtualLoopScope = "MatchingKr3MechanismOnlyNotExactC01PathEquivalence",
                FixedFixtureNativeKssValidated = true,
                ExactC01IntegratedValidated = true,
                MatchingKr3VirtualLoopValidated = true,
                CandidateNativeExecutionValidated = false
            },
            Inputs = candidate.Inputs.Concat(baseline.Inputs).ToList(),
            VendorSoftwareStarted = false,
            NetworkTrafficSent = false,
            RhinoAccessed = false,
            PhysicalControllerContacted = false,
            EnvironmentReusable = true,
            SideEffects = [$"CreateNewReceiptFile:{outputPath}"],
            UnsupportedClaims = StageOneEvidenceBundleContract.RequiredUnsupportedClaims.ToList()
        };
        var receipt = new StageOneEvidenceBundleReceipt
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var intrinsic = StageOneEvidenceBundleReceiptVerifier.Verify(receipt, rehashCurrentInputs: false);
        if (!intrinsic.Succeeded)
        {
            throw new InvalidOperationException("Generated stage-one evidence bundle is invalid: " + string.Join("; ", intrinsic.Errors));
        }
        return new StageOneEvidenceBundleOutcome(receipt);
    }

    internal StageOneVerifiedCandidate VerifyCandidate(StageOneEvidenceBundleRequest request, string candidateRoot)
    {
        var candidateFile = StageOneBaselineVerifier.ReadStable(request.CandidateReceiptPath);
        var candidateHash = StageOneBaselineVerifier.Hash(candidateFile.Bytes);
        if (request.CandidateKind == StageOneCandidateEvidenceKind.RawKrlIdentity)
        {
            if (!string.IsNullOrWhiteSpace(request.ValidationPackageReceiptPath))
            {
                throw new ArgumentException("ValidationPackage receipt is not allowed for RawKrlIdentity candidates.");
            }
            var receipt = ReceiptSerialization.RawKrlCandidateFromJson(Encoding.UTF8.GetString(candidateFile.Bytes));
            var verification = RawKrlCandidateReceiptVerifier.VerifyCurrentSource(receipt, candidateRoot);
            if (!verification.Succeeded)
            {
                throw new InvalidDataException("Raw KRL candidate verification failed: " + string.Join("; ", verification.Errors));
            }
            var readiness = receipt.Payload.TerminalClassification == VerificationStatus.Passed
                ? StageOneCandidateReadiness.IdentityVerified
                : StageOneCandidateReadiness.Rejected;
            return new StageOneVerifiedCandidate(
                new StageOneCandidateEvidence
                {
                    Kind = request.CandidateKind,
                    Readiness = readiness,
                    CandidateId = receipt.Payload.CandidateId,
                    CandidateRoot = candidateRoot,
                    ProgramCount = receipt.Payload.Programs.Count,
                    MotionInstructionCount = 0,
                    CandidateNativeStatus = NativeKssStatus.NotRun,
                    CandidateKukaSimStatus = "NotRun",
                    Reason = readiness == StageOneCandidateReadiness.IdentityVerified
                        ? "Exact raw SRC/DAT bytes and program pairs are current; semantic and native validation have not run."
                        : "The integrity-valid raw KRL intake receipt records a rejected candidate."
                },
                [CandidateInput("candidate-raw-krl", candidateFile.Path, receipt.SchemaIdentity, receipt.Payload.ReceiptId, receipt.PayloadSha256, candidateHash)]);
        }

        if (string.IsNullOrWhiteSpace(request.ValidationPackageReceiptPath))
        {
            throw new ArgumentException("ValidationPackage receipt is required for ValidationPackageStaticPreflight candidates.");
        }
        var packageFile = StageOneBaselineVerifier.ReadStable(request.ValidationPackageReceiptPath);
        var packageHash = StageOneBaselineVerifier.Hash(packageFile.Bytes);
        var packageReceipt = ReceiptSerialization.ValidationPackageFromJson(Encoding.UTF8.GetString(packageFile.Bytes));
        var packageVerification = ValidationPackageReceiptVerifier.VerifyCurrentPackage(packageReceipt, candidateRoot);
        if (!packageVerification.Succeeded)
        {
            throw new InvalidDataException("ValidationPackage verification failed: " + string.Join("; ", packageVerification.Errors));
        }
        var preflightReceipt = ReceiptSerialization.KrlStaticPreflightFromJson(Encoding.UTF8.GetString(candidateFile.Bytes));
        var preflightVerification = KrlStaticPreflightReceiptVerifier.Verify(preflightReceipt);
        if (!preflightVerification.Succeeded
            || !string.Equals(preflightReceipt.Payload.ValidationPackageId, packageReceipt.Payload.ComputedPackageId, StringComparison.Ordinal)
            || !string.Equals(preflightReceipt.Payload.ValidationPackageReceiptPayloadSha256, packageReceipt.PayloadSha256, StringComparison.Ordinal)
            || !string.Equals(preflightReceipt.Payload.ValidationPackageReceiptFileSha256, packageHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException("KRL static-preflight receipt is invalid or is not bound to the supplied current ValidationPackage receipt.");
        }
        var staticReadiness = preflightReceipt.Payload.Disposition == KrlStaticPreflightDisposition.ReadyForNativeKssSubmission
            ? StageOneCandidateReadiness.StaticPreflightReady
            : StageOneCandidateReadiness.Rejected;
        return new StageOneVerifiedCandidate(
            new StageOneCandidateEvidence
            {
                Kind = request.CandidateKind,
                Readiness = staticReadiness,
                CandidateId = packageReceipt.Payload.ComputedPackageId,
                CandidateRoot = candidateRoot,
                ProgramCount = preflightReceipt.Payload.Programs.Count,
                MotionInstructionCount = preflightReceipt.Payload.MotionInstructionCount,
                CandidateNativeStatus = NativeKssStatus.NotRun,
                CandidateKukaSimStatus = "NotRun",
                Reason = staticReadiness == StageOneCandidateReadiness.StaticPreflightReady
                    ? "ValidationPackage integrity and local KRL static preflight passed; native KSS and KUKA.Sim have not run for this candidate."
                    : "The integrity-valid static-preflight receipt records local KRL rejection."
            },
            [
                CandidateInput("candidate-validation-package", packageFile.Path, packageReceipt.SchemaIdentity, packageReceipt.Payload.ReceiptId, packageReceipt.PayloadSha256, packageHash),
                CandidateInput("candidate-krl-static-preflight", candidateFile.Path, preflightReceipt.SchemaIdentity, preflightReceipt.Payload.ReceiptId, preflightReceipt.PayloadSha256, candidateHash)
            ]);
    }

    private static StageOneEvidenceInput CandidateInput(
        string role,
        string path,
        string schemaIdentity,
        string receiptId,
        string payloadSha256,
        string actualReceiptSha256) => new()
        {
            Role = role,
            Path = path,
            SchemaIdentity = schemaIdentity,
            ReceiptId = receiptId,
            PayloadSha256 = payloadSha256,
            ActualReceiptSha256 = actualReceiptSha256,
            ExpectedReceiptSha256 = actualReceiptSha256
        };

    private static void ValidateOutputBoundary(StageOneCandidateEvidenceKind kind, string candidateRoot, string outputPath)
    {
        if (kind == StageOneCandidateEvidenceKind.RawKrlIdentity)
        {
            RawKrlCandidateBoundary.Validate(candidateRoot, outputPath);
        }
        else
        {
            ValidationPackageIdentity.ValidateOutputBoundary(candidateRoot, outputPath);
        }
    }

    private static string ComputeFileSha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex AttemptIdPattern();
}

public sealed record StageOneEvidenceBundleVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;
    public bool Succeeded { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
    public List<string> Errors { get; init; } = [];
}

public static class StageOneEvidenceBundleReceiptVerifier
{
    public static StageOneEvidenceBundleVerificationResult Verify(StageOneEvidenceBundleReceipt receipt) =>
        Verify(receipt, rehashCurrentInputs: true);

    internal static StageOneEvidenceBundleVerificationResult Verify(
        StageOneEvidenceBundleReceipt receipt,
        bool rehashCurrentInputs)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (receipt.SchemaIdentity != StageOneEvidenceBundleContract.ReceiptSchemaIdentity
            || receipt.SchemaVersion != StageOneEvidenceBundleContract.ReceiptSchemaVersion)
        {
            errors.Add("stage-one receipt schema identity/version is unsupported");
        }
        var payload = receipt.Payload;
        if (payload is null)
        {
            return Result(string.Empty, receipt.PayloadSha256, ["payload is required"]);
        }
        if (string.IsNullOrWhiteSpace(payload.ReceiptId)
            || string.IsNullOrWhiteSpace(payload.AttemptId)
            || !IsSha(payload.CoreAssemblySha256)
            || payload.StartedAtUtc > payload.CompletedAtUtc
            || payload.DurationMilliseconds < 0
            || string.IsNullOrWhiteSpace(payload.Runtime.OsDescription)
            || string.IsNullOrWhiteSpace(payload.Runtime.FrameworkDescription)
            || string.IsNullOrWhiteSpace(payload.Runtime.ProcessArchitecture))
        {
            errors.Add("receipt identity, timing, runtime or Core hash is invalid");
        }
        if (!string.Equals(receipt.PayloadSha256, ReceiptSerialization.ComputeCanonicalSha256(payload), StringComparison.Ordinal))
        {
            errors.Add("payload SHA-256 mismatch");
        }
        errors.AddRange(ValidatePayload(payload));
        if (rehashCurrentInputs)
        {
            try
            {
                foreach (var input in payload.Inputs)
                {
                    var current = StageOneBaselineVerifier.ReadStable(input.Path);
                    if (!string.Equals(StageOneBaselineVerifier.Hash(current.Bytes), input.ActualReceiptSha256, StringComparison.Ordinal))
                    {
                        errors.Add("current input receipt drifted: " + input.Role);
                    }
                }

                var candidateReceipt = RequireRole(payload, payload.Candidate.Kind == StageOneCandidateEvidenceKind.RawKrlIdentity
                    ? "candidate-raw-krl"
                    : "candidate-krl-static-preflight");
                var packageReceipt = payload.Candidate.Kind == StageOneCandidateEvidenceKind.ValidationPackageStaticPreflight
                    ? RequireRole(payload, "candidate-validation-package").Path
                    : null;
                var rebuiltCandidate = new StageOneEvidenceBundleRunner().VerifyCandidate(
                    new StageOneEvidenceBundleRequest
                    {
                        CandidateKind = payload.Candidate.Kind,
                        CandidateRoot = payload.Candidate.CandidateRoot,
                        CandidateReceiptPath = candidateReceipt.Path,
                        ValidationPackageReceiptPath = packageReceipt,
                        ComparisonReceiptPath = RequireRole(payload, "item9d-offline-loop-comparison").Path,
                        VirtualLoopReceiptPath = RequireRole(payload, "item9e-kukasim-officelite-virtual-loop").Path,
                        ReceiptOutputPath = payload.SideEffects.Single()["CreateNewReceiptFile:".Length..]
                    },
                    payload.Candidate.CandidateRoot);
                if (!string.Equals(
                        ReceiptSerialization.ComputeCanonicalSha256(rebuiltCandidate.Evidence),
                        ReceiptSerialization.ComputeCanonicalSha256(payload.Candidate),
                        StringComparison.Ordinal)
                    || !string.Equals(
                        ReceiptSerialization.ComputeCanonicalSha256(rebuiltCandidate.Inputs),
                        ReceiptSerialization.ComputeCanonicalSha256(
                            payload.Inputs.Where(input => input.Role.StartsWith("candidate-", StringComparison.Ordinal)).ToList()),
                        StringComparison.Ordinal))
                {
                    errors.Add("candidate evidence no longer matches the current source/package receipts");
                }

                var rebuiltBaseline = new StageOneBaselineVerifier().Verify(
                    RequireRole(payload, "item9d-offline-loop-comparison").Path,
                    RequireRole(payload, "item9e-kukasim-officelite-virtual-loop").Path);
                if (!string.Equals(
                        ReceiptSerialization.ComputeCanonicalSha256(rebuiltBaseline.Inputs),
                        ReceiptSerialization.ComputeCanonicalSha256(
                            payload.Inputs.Where(input => input.Role.StartsWith("item9", StringComparison.Ordinal)).ToList()),
                        StringComparison.Ordinal))
                {
                    errors.Add("accepted item-9D/item-9E evidence no longer matches the bundle");
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException
                or InvalidDataException or InvalidOperationException or JsonException)
            {
                errors.Add("current input verification failed: " + exception.Message);
            }
        }
        return Result(payload.ReceiptId, receipt.PayloadSha256, errors);
    }

    internal static List<string> ValidatePayload(StageOneEvidenceBundlePayload payload)
    {
        var errors = new List<string>();
        var expectedTerminal = payload.Candidate.Readiness == StageOneCandidateReadiness.Rejected
            ? EnvironmentTerminalClassification.Failed
            : EnvironmentTerminalClassification.Ready;
        if (payload.TerminalClassification != expectedTerminal
            || string.IsNullOrWhiteSpace(payload.Candidate.CandidateId)
            || string.IsNullOrWhiteSpace(payload.Candidate.CandidateRoot)
            || payload.Candidate.ProgramCount < 0
            || payload.Candidate.MotionInstructionCount < 0
            || payload.Candidate.CandidateNativeStatus != NativeKssStatus.NotRun
            || payload.Candidate.CandidateKukaSimStatus != "NotRun"
            || string.IsNullOrWhiteSpace(payload.Candidate.Reason))
        {
            errors.Add("candidate readiness, identity or NotRun boundaries are inconsistent");
        }
        if (payload.Candidate.Kind == StageOneCandidateEvidenceKind.RawKrlIdentity
            && payload.Candidate.Readiness == StageOneCandidateReadiness.StaticPreflightReady
            || payload.Candidate.Kind == StageOneCandidateEvidenceKind.ValidationPackageStaticPreflight
            && payload.Candidate.Readiness == StageOneCandidateReadiness.IdentityVerified)
        {
            errors.Add("candidate kind/readiness pairing is invalid");
        }
        var expectedRoles = payload.Candidate.Kind == StageOneCandidateEvidenceKind.RawKrlIdentity
            ? new[] { "candidate-raw-krl", "item9d-offline-loop-comparison", "item9e-kukasim-officelite-virtual-loop" }
            : new[] { "candidate-validation-package", "candidate-krl-static-preflight", "item9d-offline-loop-comparison", "item9e-kukasim-officelite-virtual-loop" };
        if (payload.Inputs.Count != expectedRoles.Length
            || !expectedRoles.SequenceEqual(payload.Inputs.Select(input => input.Role), StringComparer.Ordinal)
            || payload.Inputs.Any(input => string.IsNullOrWhiteSpace(input.Path)
                || string.IsNullOrWhiteSpace(input.SchemaIdentity)
                || string.IsNullOrWhiteSpace(input.ReceiptId)
                || !IsSha(input.PayloadSha256)
                || !IsSha(input.ActualReceiptSha256)
                || !IsSha(input.ExpectedReceiptSha256)
                || !string.Equals(input.ActualReceiptSha256, input.ExpectedReceiptSha256, StringComparison.Ordinal)))
        {
            errors.Add("input roles, identities or immutable hashes are invalid");
        }
        var comparison = payload.Inputs.SingleOrDefault(input => input.Role == "item9d-offline-loop-comparison");
        var virtualLoop = payload.Inputs.SingleOrDefault(input => input.Role == "item9e-kukasim-officelite-virtual-loop");
        if (comparison?.SchemaIdentity != OfflineLoopComparisonContract.ReceiptSchemaIdentity
            || comparison.ActualReceiptSha256 != StageOneEvidenceBundleContract.AcceptedComparisonReceiptSha256
            || virtualLoop?.SchemaIdentity != KukaSimOfficeLiteVirtualLoopContract.ReceiptSchemaIdentity
            || virtualLoop.ActualReceiptSha256 != StageOneEvidenceBundleContract.AcceptedVirtualLoopReceiptSha256)
        {
            errors.Add("accepted item-9D/item-9E baseline identity is invalid");
        }
        if (payload.EnvironmentBaseline.ComparisonValidationStatus != OfflineLoopComparisonContract.ValidationStatus
            || payload.EnvironmentBaseline.VirtualLoopScope != "MatchingKr3MechanismOnlyNotExactC01PathEquivalence"
            || !payload.EnvironmentBaseline.FixedFixtureNativeKssValidated
            || !payload.EnvironmentBaseline.ExactC01IntegratedValidated
            || !payload.EnvironmentBaseline.MatchingKr3VirtualLoopValidated
            || payload.EnvironmentBaseline.CandidateNativeExecutionValidated)
        {
            errors.Add("environment baseline claims exceed or understate the accepted fixed evidence");
        }
        if (payload.VendorSoftwareStarted || payload.NetworkTrafficSent || payload.RhinoAccessed
            || payload.PhysicalControllerContacted || !payload.EnvironmentReusable)
        {
            errors.Add("file-only side-effect boundary is violated");
        }
        if (payload.SideEffects.Count != 1
            || !payload.SideEffects[0].StartsWith("CreateNewReceiptFile:", StringComparison.Ordinal)
            || !StageOneEvidenceBundleContract.RequiredUnsupportedClaims.SequenceEqual(payload.UnsupportedClaims, StringComparer.Ordinal))
        {
            errors.Add("side-effect or unsupported-claim declarations are invalid");
        }
        return errors;
    }

    private static StageOneEvidenceBundleVerificationResult Result(string receiptId, string payloadSha256, List<string> errors) => new()
    {
        ReceiptId = receiptId,
        Succeeded = errors.Count == 0,
        PayloadSha256 = payloadSha256,
        Errors = errors
    };

    private static StageOneEvidenceInput RequireRole(StageOneEvidenceBundlePayload payload, string role) =>
        payload.Inputs.SingleOrDefault(input => input.Role == role)
        ?? throw new InvalidOperationException("input role is missing: " + role);

    private static bool IsSha(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}

public static class StageOneEvidenceBundleReceiptWriter
{
    public static string WriteNew(string outputPath, StageOneEvidenceBundleReceipt receipt)
    {
        var verification = StageOneEvidenceBundleReceiptVerifier.Verify(receipt);
        if (!verification.Succeeded)
        {
            throw new InvalidOperationException("Stage-one evidence bundle receipt is invalid: " + string.Join("; ", verification.Errors));
        }
        var fullPath = Path.GetFullPath(outputPath);
        if (!string.Equals(receipt.Payload.SideEffects.Single(), $"CreateNewReceiptFile:{fullPath}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Receipt output does not match its recorded side effect.");
        }
        var parent = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        Directory.CreateDirectory(parent);
        using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(ReceiptSerialization.ToJson(receipt));
        writer.WriteLine();
        writer.Flush();
        stream.Flush(true);
        return fullPath;
    }
}
