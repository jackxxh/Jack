using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class RawKrlCandidateContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.raw-krl-candidate-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const string CandidateIdPrefix = "sha256:";

    public static readonly IReadOnlyList<string> UnsupportedClaims =
    [
        "Raw KRL intake proves only exact SRC/DAT bytes and program pairing; it does not prove Rhino, Workcell, MotionPlan, frame or robot-profile semantics.",
        "Raw KRL intake does not execute KUKA.Sim, OfficeLite, native KSS, WorkVisual or a physical controller.",
        "A raw KRL candidate can enter static/native syntax validation, but L1/L3 simulation comparison requires an enriched producer-owned ValidationPackage."
    ];
}

public enum RawKrlArtifactRole
{
    Source,
    Data
}

public sealed record RawKrlCandidateRequest
{
    public required string SourceRoot { get; init; }

    public required string ReceiptOutputPath { get; init; }
}

public sealed record RawKrlCandidateFile
{
    public required string RelativePath { get; init; }

    public required RawKrlArtifactRole Role { get; init; }

    public required string EncodingObservation { get; init; }

    public required long Bytes { get; init; }

    public required string Sha256 { get; init; }
}

public sealed record RawKrlProgramPair
{
    public required string ProgramName { get; init; }

    public required string SourceFile { get; init; }

    public required string DataFile { get; init; }
}

public sealed record RawKrlCandidateReceipt
{
    public string SchemaIdentity { get; init; } = RawKrlCandidateContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = RawKrlCandidateContract.ReceiptSchemaVersion;

    public required RawKrlCandidatePayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record RawKrlCandidatePayload
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

    public string CandidateId { get; init; } = string.Empty;

    public int UnsupportedFileCount { get; init; }

    public List<RawKrlCandidateFile> Files { get; init; } = [];

    public List<RawKrlProgramPair> Programs { get; init; } = [];

    public List<VerificationCheck> Checks { get; init; } = [];

    public NativeKssObservation NativeKss { get; init; } = new();

    public List<string> SideEffects { get; init; } = [];

    public bool EnvironmentReusable { get; init; } = true;

    public List<string> UnsupportedClaims { get; init; } = [];
}

public sealed record RawKrlCandidateOutcome(RawKrlCandidateReceipt Receipt)
{
    public bool Succeeded => Receipt.Payload.TerminalClassification == VerificationStatus.Passed;

    public int ExitCode => Succeeded ? 0 : 2;
}

public sealed record RawKrlCandidateVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public bool CurrentSourceVerified { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}

public sealed partial class RawKrlCandidateRunner
{
    private readonly TimeProvider _timeProvider;

    public RawKrlCandidateRunner(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public RawKrlCandidateOutcome Run(RawKrlCandidateRequest request, string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAttemptId(attemptId);
        var sourceRoot = Path.GetFullPath(request.SourceRoot);
        var outputPath = Path.GetFullPath(request.ReceiptOutputPath);
        RawKrlCandidateBoundary.Validate(sourceRoot, outputPath);

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var snapshot = RawKrlCandidateSnapshot.Capture(sourceRoot);
        var checks = RawKrlCandidateRules.BuildChecks(
            snapshot.Files,
            snapshot.Programs,
            snapshot.UnsupportedFileCount);
        stopwatch.Stop();
        var terminal = checks.All(check => check.Status == VerificationStatus.Passed)
            ? VerificationStatus.Passed
            : VerificationStatus.Failed;
        var payload = new RawKrlCandidatePayload
        {
            ReceiptId = $"raw-krl-{snapshot.CandidateId[^16..].ToLowerInvariant()}-{attemptId}",
            AttemptId = attemptId,
            CoreAssemblySha256 = ComputeFileSha256(typeof(RawKrlCandidateRunner).Assembly.Location),
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
            CandidateId = snapshot.CandidateId,
            UnsupportedFileCount = snapshot.UnsupportedFileCount,
            Files = snapshot.Files,
            Programs = snapshot.Programs,
            Checks = checks,
            NativeKss = new NativeKssObservation
            {
                Status = NativeKssStatus.NotRun,
                Reason = "Raw KRL intake hashes and pairs SRC/DAT files without executing native KSS."
            },
            SideEffects = [$"CreateNewReceiptFile:{outputPath}"],
            EnvironmentReusable = true,
            UnsupportedClaims = RawKrlCandidateContract.UnsupportedClaims.ToList()
        };
        return new RawKrlCandidateOutcome(new RawKrlCandidateReceipt
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        });
    }

    private static void ValidateAttemptId(string attemptId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptId);
        if (!AttemptIdPattern().IsMatch(attemptId))
        {
            throw new ArgumentException(
                "Attempt ID must match [A-Za-z0-9][A-Za-z0-9._-]{2,127}.",
                nameof(attemptId));
        }
    }

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex AttemptIdPattern();
}

internal sealed record RawKrlSnapshot(
    string CandidateId,
    List<RawKrlCandidateFile> Files,
    List<RawKrlProgramPair> Programs,
    int UnsupportedFileCount);

internal static class RawKrlCandidateSnapshot
{
    private static readonly byte[] IdentityDomain = Encoding.UTF8.GetBytes("kuka.lab.raw-krl-candidate.v1");

    internal static RawKrlSnapshot Capture(string sourceRoot)
    {
        var root = Path.GetFullPath(sourceRoot);
        RawKrlCandidateBoundary.ValidateSourceRoot(root);
        var candidates = EnumerateFiles(root);
        var unsupportedFileCount = 0;
        var files = new List<RawKrlCandidateFile>();
        foreach (var fullPath in candidates)
        {
            var relativePath = Path.GetRelativePath(root, fullPath).Replace('\\', '/');
            if (!ValidationPackageIdentity.IsSafeRelativePath(relativePath))
            {
                throw new InvalidDataException("Raw KRL source contains a non-portable path.");
            }

            var extension = Path.GetExtension(relativePath);
            var role = extension.ToLowerInvariant() switch
            {
                ".src" => RawKrlArtifactRole.Source,
                ".dat" => RawKrlArtifactRole.Data,
                _ => (RawKrlArtifactRole?)null
            };
            if (role is null)
            {
                unsupportedFileCount++;
                continue;
            }

            var bytes = ReadStableBytes(fullPath);
            files.Add(new RawKrlCandidateFile
            {
                RelativePath = relativePath,
                Role = role.Value,
                EncodingObservation = ObserveEncoding(bytes),
                Bytes = bytes.LongLength,
                Sha256 = Convert.ToHexString(SHA256.HashData(bytes))
            });
        }

        files = files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToList();
        var programs = RawKrlCandidateRules.BuildPrograms(files);
        return new RawKrlSnapshot(
            ComputeCandidateId(files),
            files,
            programs,
            unsupportedFileCount);
    }

    internal static string ComputeCandidateId(IReadOnlyList<RawKrlCandidateFile> files)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, IdentityDomain);
        Span<byte> length = stackalloc byte[sizeof(long)];
        foreach (var file in files.OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            Append(hash, Encoding.UTF8.GetBytes(file.RelativePath));
            BinaryPrimitives.WriteInt64BigEndian(length, file.Bytes);
            hash.AppendData(length);
            hash.AppendData(Convert.FromHexString(file.Sha256));
        }

        return RawKrlCandidateContract.CandidateIdPrefix + Convert.ToHexString(hash.GetHashAndReset());
    }

    private static List<string> EnumerateFiles(string root)
    {
        var result = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                var info = new DirectoryInfo(child);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("Raw KRL source cannot contain reparse directories.");
                }

                pending.Push(child);
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                var info = new FileInfo(file);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("Raw KRL source cannot contain reparse files.");
                }

                result.Add(info.FullName);
            }
        }

        return result;
    }

    private static byte[] ReadStableBytes(string path)
    {
        var info = new FileInfo(path);
        info.Refresh();
        var length = info.Length;
        var writeTime = info.LastWriteTimeUtc;
        var bytes = File.ReadAllBytes(path);
        info.Refresh();
        if (bytes.LongLength != length || info.Length != length || info.LastWriteTimeUtc != writeTime)
        {
            throw new IOException("Raw KRL file changed while it was being read.");
        }

        return bytes;
    }

    private static string ObserveEncoding(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return "Empty";
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return "UTF-8-BOM";
        }

        if (bytes.All(value => value is (byte)'\t' or (byte)'\r' or (byte)'\n'
                || value is >= 0x20 and <= 0x7E))
        {
            return "ASCII-compatible";
        }

        try
        {
            _ = new UTF8Encoding(false, true).GetString(bytes);
            return "UTF-8";
        }
        catch (DecoderFallbackException)
        {
            return "Unresolved-8bit";
        }
    }

    private static void Append(IncrementalHash hash, ReadOnlySpan<byte> bytes)
    {
        Span<byte> length = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}

internal static class RawKrlCandidateRules
{
    internal static List<RawKrlProgramPair> BuildPrograms(IReadOnlyList<RawKrlCandidateFile> files)
    {
        var sources = BuildByStem(files, RawKrlArtifactRole.Source);
        var data = BuildByStem(files, RawKrlArtifactRole.Data);
        return sources.Keys.Intersect(data.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.Ordinal)
            .Select(key => new RawKrlProgramPair
            {
                ProgramName = Path.GetFileNameWithoutExtension(sources[key].RelativePath),
                SourceFile = sources[key].RelativePath,
                DataFile = data[key].RelativePath
            })
            .ToList();
    }

    internal static List<VerificationCheck> BuildChecks(
        IReadOnlyList<RawKrlCandidateFile> files,
        IReadOnlyList<RawKrlProgramPair> programs,
        int unsupportedFileCount)
    {
        var sourceCount = files.Count(file => file.Role == RawKrlArtifactRole.Source);
        var dataCount = files.Count(file => file.Role == RawKrlArtifactRole.Data);
        var expectedPrograms = BuildPrograms(files);
        var exactPairs = sourceCount > 0
            && dataCount > 0
            && sourceCount == dataCount
            && expectedPrograms.Count == sourceCount
            && expectedPrograms.SequenceEqual(programs);
        return
        [
            Passed("source-root", "Raw KRL source was read without a reparse boundary or protected license-file scope."),
            unsupportedFileCount == 0
                ? Passed("file-boundary", "The source contains only SRC/DAT files.")
                : Failed("file-boundary", $"The source contains {unsupportedFileCount} unsupported file(s); no unsupported content was read."),
            sourceCount > 0 && dataCount > 0
                ? Passed("krl-files", $"Observed {sourceCount} SRC and {dataCount} DAT file(s).")
                : Failed("krl-files", "At least one SRC and one DAT file are required."),
            exactPairs
                ? Passed("program-pairs", $"All {expectedPrograms.Count} program(s) have exact relative-path SRC/DAT pairs.")
                : Failed("program-pairs", "Every SRC must have one same-stem DAT in the same relative directory, with no duplicate stems."),
            files.All(file => file.Bytes >= 0 && IsSha256(file.Sha256))
                ? Passed("file-integrity", "Every KRL file has an exact byte length and SHA-256 identity.")
                : Failed("file-integrity", "One or more KRL file identities are incomplete.")
        ];
    }

    private static Dictionary<string, RawKrlCandidateFile> BuildByStem(
        IReadOnlyList<RawKrlCandidateFile> files,
        RawKrlArtifactRole role)
    {
        var result = new Dictionary<string, RawKrlCandidateFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files.Where(file => file.Role == role))
        {
            var stem = file.RelativePath[..^Path.GetExtension(file.RelativePath).Length];
            if (!result.TryAdd(stem, file))
            {
                return [];
            }
        }

        return result;
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static VerificationCheck Passed(string id, string detail) =>
        new() { Id = id, Status = VerificationStatus.Passed, Detail = detail };

    private static VerificationCheck Failed(string id, string detail) =>
        new() { Id = id, Status = VerificationStatus.Failed, Detail = detail };
}

internal static class RawKrlCandidateBoundary
{
    private static readonly string ProtectedLicensePath = Path.GetFullPath(
        @"C:\ProgramData\Visual Components\Visual Components License Server 2.0\lservrc.dat");

    internal static void Validate(string sourceRoot, string outputPath)
    {
        ValidateSourceRoot(sourceRoot);
        if (!string.Equals(Path.GetExtension(outputPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Raw KRL receipt output must use .json.", nameof(outputPath));
        }

        if (File.Exists(outputPath) || Directory.Exists(outputPath))
        {
            throw new IOException("Raw KRL receipt output already exists; writes are create-new only.");
        }

        var rootPrefix = sourceRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (outputPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Raw KRL receipt output must be outside the source root.", nameof(outputPath));
        }

        EnsureNoReparseAncestor(Path.GetDirectoryName(outputPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath)));
    }

    internal static void ValidateSourceRoot(string sourceRoot)
    {
        var rootPrefix = sourceRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (string.Equals(sourceRoot, Path.GetDirectoryName(ProtectedLicensePath), StringComparison.OrdinalIgnoreCase)
            || ProtectedLicensePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Raw KRL intake rejects any source scope containing the protected Visual Components license-control file.");
        }

        if (!Directory.Exists(sourceRoot))
        {
            throw new DirectoryNotFoundException("Raw KRL source root does not exist.");
        }

        EnsureNoReparseAncestor(sourceRoot);
    }

    private static void EnsureNoReparseAncestor(string path)
    {
        for (var current = new DirectoryInfo(Path.GetFullPath(path)); current is not null; current = current.Parent)
        {
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Raw KRL paths cannot cross a reparse point.");
            }
        }
    }
}

public static class RawKrlCandidateReceiptVerifier
{
    public static RawKrlCandidateVerificationResult VerifyIntegrity(RawKrlCandidateReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (!string.Equals(receipt.SchemaIdentity, RawKrlCandidateContract.ReceiptSchemaIdentity, StringComparison.Ordinal))
        {
            errors.Add($"schemaIdentity must be {RawKrlCandidateContract.ReceiptSchemaIdentity}");
        }

        if (receipt.SchemaVersion != RawKrlCandidateContract.ReceiptSchemaVersion)
        {
            errors.Add($"schemaVersion must be {RawKrlCandidateContract.ReceiptSchemaVersion}");
        }

        if (ReferenceEquals(receipt.Payload, null))
        {
            return Failed(receipt.PayloadSha256, "payload is required");
        }

        var payload = receipt.Payload;
        if (string.IsNullOrWhiteSpace(payload.ReceiptId)
            || string.IsNullOrWhiteSpace(payload.AttemptId)
            || !IsSha256(payload.CoreAssemblySha256)
            || payload.Runtime is null
            || string.IsNullOrWhiteSpace(payload.Runtime.OsDescription)
            || string.IsNullOrWhiteSpace(payload.Runtime.FrameworkDescription)
            || string.IsNullOrWhiteSpace(payload.Runtime.ProcessArchitecture))
        {
            errors.Add("receipt, attempt, implementation or runtime identity is incomplete");
        }

        if (payload.Files is null || payload.Programs is null || payload.Checks is null
            || payload.SideEffects is null || payload.UnsupportedClaims is null)
        {
            return Failed(receipt.PayloadSha256, "payload collections cannot be null");
        }

        var ordered = payload.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToList();
        if (!ordered.SequenceEqual(payload.Files)
            || payload.Files.Any(file => file is null
                || !ValidationPackageIdentity.IsSafeRelativePath(file.RelativePath)
                || file.Bytes < 0
                || !IsSha256(file.Sha256)
                || string.IsNullOrWhiteSpace(file.EncodingObservation)
                || !RoleMatchesExtension(file))
            || payload.Files.Select(file => file.RelativePath)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != payload.Files.Count)
        {
            errors.Add("file evidence must be ordered, unique, portable, role-correct and hash-complete");
        }

        var expectedCandidateId = RawKrlCandidateSnapshot.ComputeCandidateId(payload.Files);
        if (!string.Equals(payload.CandidateId, expectedCandidateId, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("candidateId does not match recorded KRL file identities");
        }

        var expectedPrograms = RawKrlCandidateRules.BuildPrograms(payload.Files);
        if (!expectedPrograms.SequenceEqual(payload.Programs))
        {
            errors.Add("program pairs do not match recorded SRC/DAT files");
        }

        var expectedChecks = RawKrlCandidateRules.BuildChecks(
            payload.Files,
            payload.Programs,
            payload.UnsupportedFileCount);
        if (!expectedChecks.SequenceEqual(payload.Checks))
        {
            errors.Add("checks do not match raw KRL evidence");
        }

        var expectedTerminal = expectedChecks.All(check => check.Status == VerificationStatus.Passed)
            ? VerificationStatus.Passed
            : VerificationStatus.Failed;
        if (payload.TerminalClassification != expectedTerminal
            || payload.UnsupportedFileCount < 0
            || payload.NativeKss.Status != NativeKssStatus.NotRun
            || payload.NativeKss.ExpectedCompileResult is not null
            || string.IsNullOrWhiteSpace(payload.NativeKss.Reason)
            || !payload.EnvironmentReusable)
        {
            errors.Add("terminal, unsupported-file or native-KSS boundary is inconsistent");
        }

        if (payload.SideEffects.Count != 1
            || !payload.SideEffects[0].StartsWith("CreateNewReceiptFile:", StringComparison.Ordinal)
            || !RawKrlCandidateContract.UnsupportedClaims.SequenceEqual(payload.UnsupportedClaims, StringComparer.Ordinal))
        {
            errors.Add("sideEffects or unsupportedClaims do not preserve the raw KRL boundary");
        }

        if (payload.StartedAtUtc > payload.CompletedAtUtc
            || payload.DurationMilliseconds < 0
            || !string.Equals(
                receipt.PayloadSha256,
                ReceiptSerialization.ComputeCanonicalSha256(payload),
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("timestamps, duration or payload hash are invalid");
        }

        return new RawKrlCandidateVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            CurrentSourceVerified = false,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }

    public static RawKrlCandidateVerificationResult VerifyCurrentSource(
        RawKrlCandidateReceipt receipt,
        string sourceRoot)
    {
        var integrity = VerifyIntegrity(receipt);
        if (!integrity.Succeeded)
        {
            return integrity;
        }

        try
        {
            var current = RawKrlCandidateSnapshot.Capture(Path.GetFullPath(sourceRoot));
            var errors = new List<string>();
            if (!string.Equals(current.CandidateId, receipt.Payload.CandidateId, StringComparison.OrdinalIgnoreCase)
                || current.UnsupportedFileCount != receipt.Payload.UnsupportedFileCount
                || !current.Files.SequenceEqual(receipt.Payload.Files)
                || !current.Programs.SequenceEqual(receipt.Payload.Programs))
            {
                errors.Add("current raw KRL source no longer matches the receipt");
            }

            return integrity with
            {
                Succeeded = errors.Count == 0,
                CurrentSourceVerified = errors.Count == 0,
                Errors = errors
            };
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidDataException)
        {
            return integrity with
            {
                Succeeded = false,
                CurrentSourceVerified = false,
                Errors = [$"current raw KRL source could not be verified: {exception.Message}"]
            };
        }
    }

    private static bool RoleMatchesExtension(RawKrlCandidateFile file) =>
        file.Role switch
        {
            RawKrlArtifactRole.Source => string.Equals(Path.GetExtension(file.RelativePath), ".src", StringComparison.OrdinalIgnoreCase),
            RawKrlArtifactRole.Data => string.Equals(Path.GetExtension(file.RelativePath), ".dat", StringComparison.OrdinalIgnoreCase),
            _ => false
        };

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static RawKrlCandidateVerificationResult Failed(string payloadSha256, string error) =>
        new() { PayloadSha256 = payloadSha256, Errors = [error] };
}

public static class RawKrlCandidateReceiptWriter
{
    public static string WriteNew(
        string outputPath,
        string sourceRoot,
        RawKrlCandidateReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var fullOutput = Path.GetFullPath(outputPath);
        var fullSource = Path.GetFullPath(sourceRoot);
        RawKrlCandidateBoundary.Validate(fullSource, fullOutput);
        var verification = RawKrlCandidateReceiptVerifier.VerifyCurrentSource(receipt, fullSource);
        if (!verification.Succeeded)
        {
            throw new InvalidOperationException("Raw KRL candidate changed before receipt creation.");
        }

        if (!string.Equals(
            receipt.Payload.SideEffects.Single(),
            $"CreateNewReceiptFile:{fullOutput}",
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Raw KRL receipt output does not match its recorded side effect.");
        }

        var parent = Path.GetDirectoryName(fullOutput)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        Directory.CreateDirectory(parent);
        using var stream = new FileStream(fullOutput, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(ReceiptSerialization.ToJson(receipt));
        writer.WriteLine();
        writer.Flush();
        stream.Flush(flushToDisk: true);
        return fullOutput;
    }
}
