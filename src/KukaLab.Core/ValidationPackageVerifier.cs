using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

internal sealed record ValidationPackageSnapshot(
    ValidationPackageManifest Manifest,
    byte[] ManifestBytesContent,
    string ManifestSha256,
    string ComputedPackageId,
    List<ValidationPackageObservedFile> Files);

public static partial class ValidationPackageIdentity
{
    private static readonly byte[] DomainSeparator = Encoding.UTF8.GetBytes(
        "kuka.lab.validation-package.v1");

    public static string ComputePackageId(string packageRoot) =>
        Capture(packageRoot).ComputedPackageId;

    internal static ValidationPackageSnapshot Capture(string packageRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        var fullRoot = Path.GetFullPath(packageRoot);
        if (!Directory.Exists(fullRoot))
        {
            throw new DirectoryNotFoundException($"ValidationPackage root does not exist: {fullRoot}");
        }

        EnsureNoReparsePoint(fullRoot);
        var manifestPath = Path.Combine(fullRoot, ValidationPackageContract.ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                $"Required {ValidationPackageContract.ManifestFileName} was not found.",
                manifestPath);
        }

        EnsureNoReparsePoint(manifestPath);
        var manifestBytes = ReadStableBytes(manifestPath);
        var manifest = JsonSerializer.Deserialize<ValidationPackageManifest>(
            manifestBytes,
            ReceiptSerialization.StrictManifestOptions)
            ?? throw new InvalidDataException("ValidationPackage manifest content was empty.");
        var placeholderManifest = ReplacePackageIdWithPlaceholder(manifestBytes);

        using var packageHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendLengthPrefixed(packageHash, DomainSeparator);
        AppendLengthPrefixed(packageHash, placeholderManifest);

        var files = new List<ValidationPackageObservedFile>();
        var candidates = EnumerateSafeFiles(fullRoot)
            .Where(path => !string.Equals(
                Path.GetFullPath(path),
                manifestPath,
                StringComparison.OrdinalIgnoreCase))
            .Select(path => new
            {
                FullPath = Path.GetFullPath(path),
                RelativePath = NormalizeRelativePath(Path.GetRelativePath(fullRoot, path))
            })
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToList();

        foreach (var candidate in candidates)
        {
            if (!IsSafeRelativePath(candidate.RelativePath))
            {
                throw new InvalidDataException(
                    $"Package file path is not portable and local: {candidate.RelativePath}");
            }

            EnsureNoReparsePoint(candidate.FullPath);
            var pathBytes = Encoding.UTF8.GetBytes(candidate.RelativePath);
            AppendLengthPrefixed(packageHash, pathBytes);

            var info = new FileInfo(candidate.FullPath);
            info.Refresh();
            var expectedLength = info.Length;
            var expectedWrite = info.LastWriteTimeUtc;
            AppendInt64(packageHash, expectedLength);

            using var fileHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using (var stream = new FileStream(
                candidate.FullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    packageHash.AppendData(buffer, 0, read);
                    fileHash.AppendData(buffer, 0, read);
                }

                if (stream.Length != expectedLength)
                {
                    throw new IOException($"Package file changed while hashing: {candidate.RelativePath}");
                }
            }

            info.Refresh();
            if (info.Length != expectedLength || info.LastWriteTimeUtc != expectedWrite)
            {
                throw new IOException($"Package file changed while hashing: {candidate.RelativePath}");
            }

            files.Add(new ValidationPackageObservedFile
            {
                RelativePath = candidate.RelativePath,
                Bytes = expectedLength,
                Sha256 = Convert.ToHexString(fileHash.GetHashAndReset())
            });
        }

        return new ValidationPackageSnapshot(
            manifest,
            manifestBytes,
            Convert.ToHexString(SHA256.HashData(manifestBytes)),
            ValidationPackageContract.PackageIdPrefix
                + Convert.ToHexString(packageHash.GetHashAndReset()),
            files);
    }

    internal static bool IsSafeRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathRooted(relativePath)
            || relativePath.Contains('\\')
            || relativePath.StartsWith("/", StringComparison.Ordinal)
            || relativePath.EndsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        var segments = relativePath.Split('/');
        return segments.All(segment =>
            !string.IsNullOrWhiteSpace(segment)
            && segment is not "." and not ".."
            && !segment.Any(character =>
                character < 0x20 || "<>:\"|?*".Contains(character))
            && !segment.EndsWith(" ", StringComparison.Ordinal)
            && !segment.EndsWith(".", StringComparison.Ordinal));
    }

    internal static void ValidateOutputBoundary(string packageRoot, string outputPath)
    {
        var fullRoot = Path.GetFullPath(packageRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullOutput = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetExtension(fullOutput), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("ValidationPackage receipt output must use .json.", nameof(outputPath));
        }

        if (File.Exists(fullOutput) || Directory.Exists(fullOutput))
        {
            throw new IOException("ValidationPackage receipt output already exists; writes are create-new only.");
        }

        var rootPrefix = fullRoot + Path.DirectorySeparatorChar;
        if (fullOutput.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "ValidationPackage receipt output must be outside the package root and descendants.",
                nameof(outputPath));
        }

        var parent = Path.GetDirectoryName(fullOutput)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        for (var current = new DirectoryInfo(parent); current is not null; current = current.Parent)
        {
            if (!current.Exists)
            {
                continue;
            }

            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("ValidationPackage receipt output cannot cross a reparse point.");
            }
        }
    }

    private static byte[] ReplacePackageIdWithPlaceholder(byte[] manifestBytes)
    {
        var jsonOffset = manifestBytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) ? 3 : 0;
        var reader = new Utf8JsonReader(manifestBytes.AsSpan(jsonOffset));
        var valueStart = -1;
        var valueLength = -1;
        var expectingPackageId = false;
        var matches = 0;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName
                && reader.CurrentDepth == 1
                && reader.ValueTextEquals("packageId"))
            {
                expectingPackageId = true;
                continue;
            }

            if (!expectingPackageId)
            {
                continue;
            }

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new InvalidDataException("ValidationPackage packageId must be a JSON string.");
            }

            var packageId = reader.GetString() ?? string.Empty;
            var tokenStart = checked((int)reader.TokenStartIndex);
            var tokenEnd = checked((int)reader.BytesConsumed);
            var rawContentLength = tokenEnd - tokenStart - 2;
            if (packageId.Length != ValidationPackageContract.PackageIdPlaceholder.Length
                || rawContentLength != packageId.Length
                || !packageId.StartsWith(ValidationPackageContract.PackageIdPrefix, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "ValidationPackage packageId must be an unescaped sha256: value with 64 identity characters.");
            }

            valueStart = jsonOffset + tokenStart + 1;
            valueLength = rawContentLength;
            matches++;
            expectingPackageId = false;
        }

        if (matches != 1 || valueStart < 0)
        {
            throw new InvalidDataException("ValidationPackage manifest must contain exactly one root packageId.");
        }

        var normalized = manifestBytes.ToArray();
        var placeholder = Encoding.ASCII.GetBytes(ValidationPackageContract.PackageIdPlaceholder);
        placeholder.CopyTo(normalized.AsSpan(valueStart, valueLength));
        return normalized;
    }

    private static byte[] ReadStableBytes(string path)
    {
        var info = new FileInfo(path);
        info.Refresh();
        var expectedLength = info.Length;
        var expectedWrite = info.LastWriteTimeUtc;
        byte[] content;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (stream.Length > int.MaxValue)
            {
                throw new InvalidDataException("ValidationPackage manifest is too large.");
            }

            content = new byte[checked((int)stream.Length)];
            stream.ReadExactly(content);
        }

        info.Refresh();
        if (info.Length != expectedLength || info.LastWriteTimeUtc != expectedWrite)
        {
            throw new IOException("ValidationPackage manifest changed while reading.");
        }

        return content;
    }

    private static List<string> EnumerateSafeFiles(string root)
    {
        var files = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var childDirectory in Directory.EnumerateDirectories(
                directory,
                "*",
                SearchOption.TopDirectoryOnly))
            {
                var info = new DirectoryInfo(childDirectory);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException(
                        $"ValidationPackage directory cannot be a reparse point: {childDirectory}");
                }

                pending.Push(childDirectory);
            }

            files.AddRange(Directory.EnumerateFiles(
                directory,
                "*",
                SearchOption.TopDirectoryOnly));
        }

        return files;
    }

    private static void EnsureNoReparsePoint(string path)
    {
        FileSystemInfo? current = File.Exists(path)
            ? new FileInfo(path)
            : new DirectoryInfo(path);
        while (current is not null)
        {
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"ValidationPackage path cannot cross a reparse point: {path}");
            }

            current = current switch
            {
                FileInfo file => file.Directory,
                DirectoryInfo directory => directory.Parent,
                _ => null
            };
        }
    }

    private static string NormalizeRelativePath(string relativePath) =>
        relativePath.Replace(Path.DirectorySeparatorChar, '/');

    private static void AppendLengthPrefixed(IncrementalHash hash, ReadOnlySpan<byte> bytes)
    {
        AppendInt64(hash, bytes.Length);
        hash.AppendData(bytes);
    }

    private static void AppendInt64(IncrementalHash hash, long value)
    {
        Span<byte> length = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(length, value);
        hash.AppendData(length);
    }
}

internal static partial class ValidationPackageRules
{
    internal static readonly IReadOnlyList<string> RequiredCheckIds =
    [
        "package-root",
        "manifest-schema",
        "file-boundary",
        "file-inventory",
        "package-id",
        "correlation-chain",
        "safety"
    ];

    internal static List<VerificationCheck> Evaluate(
        ValidationPackageManifest manifest,
        IReadOnlyList<ValidationPackageObservedFile> observedFiles,
        string computedPackageId)
    {
        var checks = new List<VerificationCheck>
        {
            Passed("package-root", "Package root, manifest and observed files were read without a reparse boundary.")
        };
        checks.Add(EvaluateManifest(manifest));
        checks.Add(EvaluateFileBoundary(manifest, observedFiles));
        checks.Add(EvaluateFileInventory(manifest, observedFiles));
        checks.Add(EvaluatePackageId(manifest, computedPackageId));
        checks.Add(EvaluateCorrelations(manifest));
        checks.Add(EvaluateSafety(manifest));
        return checks;
    }

    private static VerificationCheck EvaluateManifest(ValidationPackageManifest manifest)
    {
        var errors = new List<string>();
        if (!string.Equals(
                manifest.SchemaIdentity,
                ValidationPackageContract.ManifestSchemaIdentity,
                StringComparison.Ordinal))
        {
            errors.Add($"schemaIdentity must be {ValidationPackageContract.ManifestSchemaIdentity}");
        }

        if (manifest.SchemaVersion != ValidationPackageContract.ManifestSchemaVersion)
        {
            errors.Add($"schemaVersion must be {ValidationPackageContract.ManifestSchemaVersion}");
        }

        if (manifest.CreatedAtUtc == default)
        {
            errors.Add("createdAtUtc is required");
        }

        RequireObject(manifest.Producer, "producer", errors);
        if (manifest.Producer is not null)
        {
            RequireText(manifest.Producer.Product, "producer.product", errors);
            RequireText(manifest.Producer.Version, "producer.version", errors);
            RequireText(manifest.Producer.Revision, "producer.revision", errors);
        }

        RequireObject(manifest.Upstream, "upstream", errors);
        if (manifest.Upstream is not null)
        {
            RequireText(manifest.Upstream.Revision, "upstream.revision", errors);
            RequireText(manifest.Upstream.Fingerprint, "upstream.fingerprint", errors);
        }

        RequireObject(manifest.ArtifactIdentities, "artifactIdentities", errors);
        if (manifest.ArtifactIdentities is not null)
        {
            RequireText(manifest.ArtifactIdentities.Workcell, "artifactIdentities.workcell", errors);
            RequireText(manifest.ArtifactIdentities.Program, "artifactIdentities.program", errors);
            RequireText(manifest.ArtifactIdentities.MotionPlan, "artifactIdentities.motionPlan", errors);
            RequireText(manifest.ArtifactIdentities.Krl, "artifactIdentities.krl", errors);
        }

        RequireObject(manifest.CompatibilityTarget, "compatibilityTarget", errors);
        if (manifest.CompatibilityTarget is not null)
        {
            RequireText(manifest.CompatibilityTarget.KukaSimVersion, "compatibilityTarget.kukaSimVersion", errors);
            RequireText(manifest.CompatibilityTarget.KssVersion, "compatibilityTarget.kssVersion", errors);
            RequireText(manifest.CompatibilityTarget.RobotModel, "compatibilityTarget.robotModel", errors);
            RequireText(manifest.CompatibilityTarget.ControllerModel, "compatibilityTarget.controllerModel", errors);
        }

        RequireObject(manifest.Frames, "frames", errors);
        if (manifest.Frames is not null)
        {
            if (manifest.Frames.ToolNumber < 0 || manifest.Frames.BaseNumber < 0)
            {
                errors.Add("frames tool/base numbers cannot be negative");
            }

            if (!Enum.IsDefined(manifest.Frames.ToolProvenance)
                || !Enum.IsDefined(manifest.Frames.BaseProvenance)
                || !Enum.IsDefined(manifest.Frames.LoadProvenance))
            {
                errors.Add("frames provenance values must use the defined evidence classes");
            }

            RequireText(manifest.Frames.LoadDeclaration, "frames.loadDeclaration", errors);
        }

        if (manifest.Files is null || manifest.Files.Count == 0)
        {
            errors.Add("files must not be empty");
        }
        else if (manifest.Files.Any(file => file is null))
        {
            errors.Add("files cannot contain null declarations");
        }
        else
        {
            var sorted = manifest.Files.Select(file => file.RelativePath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
            if (!sorted.SequenceEqual(manifest.Files.Select(file => file.RelativePath), StringComparer.Ordinal))
            {
                errors.Add("files must be ordered by relativePath using ordinal order");
            }

            RequireExactRole(manifest.Files, "profile.json", ValidationPackageArtifactRole.Profile, errors);
            RequireExactRole(manifest.Files, "workcell.json", ValidationPackageArtifactRole.Workcell, errors);
            RequireExactRole(manifest.Files, "motion-plan.json", ValidationPackageArtifactRole.MotionPlan, errors);
            RequireExactRole(manifest.Files, "expectations.json", ValidationPackageArtifactRole.Expectations, errors);
            if (manifest.Files.Count(file => file.Role == ValidationPackageArtifactRole.KrlSource) == 0)
            {
                errors.Add("at least one KrlSource file is required");
            }

            if (manifest.Files.Count(file => file.Role == ValidationPackageArtifactRole.KrlData) == 0)
            {
                errors.Add("at least one KrlData file is required");
            }

            foreach (var file in manifest.Files)
            {
                ValidateFileDeclaration(file, errors);
            }
        }

        if (manifest.RequestedFixtureIds is null
            || manifest.RequestedFixtureIds.Count == 0
            || manifest.RequestedFixtureIds.Any(id =>
                id is null || !FixtureIdPattern().IsMatch(id))
            || manifest.RequestedFixtureIds.Distinct(StringComparer.Ordinal).Count()
                != manifest.RequestedFixtureIds.Count)
        {
            errors.Add("requestedFixtureIds must contain unique lower-case fixture IDs");
        }

        if (manifest.ExpectedAssertions is null
            || manifest.ExpectedAssertions.Count == 0
            || manifest.ExpectedAssertions.Any(string.IsNullOrWhiteSpace)
            || manifest.ExpectedAssertions.Distinct(StringComparer.Ordinal).Count()
                != manifest.ExpectedAssertions.Count)
        {
            errors.Add("expectedAssertions must contain unique non-empty values");
        }

        if (manifest.RequestedEvidenceLevels is null
            || manifest.RequestedEvidenceLevels.Count == 0
            || manifest.RequestedEvidenceLevels.Any(level => !Enum.IsDefined(level))
            || manifest.RequestedEvidenceLevels.Distinct().Count()
                != manifest.RequestedEvidenceLevels.Count)
        {
            errors.Add("requestedEvidenceLevels must contain unique L1/L2/L3 values");
        }

        return errors.Count == 0
            ? Passed("manifest-schema", "Manifest ownership, compatibility, artifact, frame, file-role and request declarations are valid.")
            : Failed("manifest-schema", string.Join("; ", errors));
    }

    private static VerificationCheck EvaluateFileBoundary(
        ValidationPackageManifest manifest,
        IReadOnlyList<ValidationPackageObservedFile> observedFiles)
    {
        var errors = new List<string>();
        var declarations = manifest.Files ?? [];
        var declaredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in declarations)
        {
            if (file is null)
            {
                errors.Add("declared file cannot be null");
                continue;
            }

            if (!ValidationPackageIdentity.IsSafeRelativePath(file.RelativePath)
                || !declaredPaths.Add(file.RelativePath))
            {
                errors.Add($"unsafe or duplicate declared path: {file.RelativePath}");
            }
        }

        var observedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var observedOrder = new List<string>();
        foreach (var file in observedFiles)
        {
            if (file is null
                || !ValidationPackageIdentity.IsSafeRelativePath(file.RelativePath)
                || !observedPaths.Add(file.RelativePath))
            {
                errors.Add($"unsafe or duplicate observed path: {file?.RelativePath ?? "<null>"}");
            }
            else
            {
                observedOrder.Add(file.RelativePath);
            }
        }

        if (!observedOrder.OrderBy(path => path, StringComparer.Ordinal)
            .SequenceEqual(observedOrder, StringComparer.Ordinal))
        {
            errors.Add("observed files must be ordered by relativePath using ordinal order");
        }
        foreach (var missing in declaredPaths.Except(observedPaths, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"declared file is missing: {missing}");
        }

        foreach (var extra in observedPaths.Except(declaredPaths, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"undeclared file is present: {extra}");
        }

        return errors.Count == 0
            ? Passed("file-boundary", "Declared and observed portable package file sets match exactly.")
            : Failed("file-boundary", string.Join("; ", errors));
    }

    private static VerificationCheck EvaluateFileInventory(
        ValidationPackageManifest manifest,
        IReadOnlyList<ValidationPackageObservedFile> observedFiles)
    {
        var errors = new List<string>();
        var observed = new Dictionary<string, ValidationPackageObservedFile>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var file in observedFiles)
        {
            if (file is null
                || !ValidationPackageIdentity.IsSafeRelativePath(file.RelativePath)
                || file.Bytes < 0
                || !Sha256Pattern().IsMatch(file.Sha256)
                || !observed.TryAdd(file.RelativePath, file))
            {
                errors.Add($"invalid or duplicate observed file: {file?.RelativePath ?? "<null>"}");
            }
        }

        foreach (var declaration in manifest.Files ?? [])
        {
            if (declaration is null)
            {
                errors.Add("declared file cannot be null");
                continue;
            }

            if (!observed.TryGetValue(declaration.RelativePath, out var actual))
            {
                continue;
            }

            if (declaration.Bytes < 0
                || !Sha256Pattern().IsMatch(declaration.Sha256)
                || declaration.Bytes != actual.Bytes
                || !string.Equals(declaration.Sha256, actual.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"length/hash mismatch: {declaration.RelativePath}");
            }
        }

        return errors.Count == 0
            ? Passed("file-inventory", "Every declared package file matches its exact byte length and SHA-256.")
            : Failed("file-inventory", string.Join("; ", errors));
    }

    private static VerificationCheck EvaluatePackageId(
        ValidationPackageManifest manifest,
        string computedPackageId)
    {
        var validShape = PackageIdPattern().IsMatch(manifest.PackageId)
            && PackageIdPattern().IsMatch(computedPackageId);
        return validShape && string.Equals(manifest.PackageId, computedPackageId, StringComparison.Ordinal)
            ? Passed("package-id", "Declared packageId equals the byte-sensitive manifest/file-tree identity.")
            : Failed("package-id", "Declared packageId does not equal the computed byte-sensitive package identity.");
    }

    private static VerificationCheck EvaluateCorrelations(ValidationPackageManifest manifest)
    {
        var errors = new List<string>();
        if (manifest.Correlations is null || manifest.Correlations.Count == 0)
        {
            errors.Add("at least one correlation is required");
        }
        else
        {
            var declaredSources = (manifest.Files ?? [])
                .Where(file => file is not null)
                .Where(file => file.Role == ValidationPackageArtifactRole.KrlSource)
                .Select(file => file.RelativePath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var correlation in manifest.Correlations)
            {
                if (correlation is null)
                {
                    errors.Add("correlation cannot be null");
                    continue;
                }

                RequireText(correlation.CorrelationId, "correlation.correlationId", errors);
                RequireText(correlation.RhinoObjectId, "correlation.rhinoObjectId", errors);
                RequireText(correlation.ProgramOperationId, "correlation.programOperationId", errors);
                RequireText(correlation.TrajectoryTargetId, "correlation.trajectoryTargetId", errors);
                RequireText(correlation.TrajectorySegmentId, "correlation.trajectorySegmentId", errors);
                if (!ids.Add(correlation.CorrelationId))
                {
                    errors.Add($"duplicate correlationId: {correlation.CorrelationId}");
                }

                if (!declaredSources.Contains(correlation.KrlFile)
                    || correlation.KrlLine < 1)
                {
                    errors.Add($"invalid KRL mapping: {correlation.CorrelationId}");
                }
            }
        }

        return errors.Count == 0
            ? Passed("correlation-chain", "Rhino object, Program operation, trajectory target/segment and KRL file/line mappings are complete.")
            : Failed("correlation-chain", string.Join("; ", errors));
    }

    private static VerificationCheck EvaluateSafety(ValidationPackageManifest manifest)
    {
        var safety = manifest.Safety;
        var passed = safety is not null
            && string.Equals(safety.Classification, "VirtualOnlyNoSecrets", StringComparison.Ordinal)
            && safety.RedactionConfirmed
            && !safety.SecretsIncluded
            && !safety.PhysicalMotionAllowed;
        return passed
            ? Passed("safety", "Package is explicitly virtual-only, redacted, secret-free and grants no physical motion.")
            : Failed("safety", "Package safety must be VirtualOnlyNoSecrets, redacted, secret-free and physicalMotionAllowed=false.");
    }

    private static void ValidateFileDeclaration(
        ValidationPackageFileDeclaration file,
        List<string> errors)
    {
        if (!ValidationPackageIdentity.IsSafeRelativePath(file.RelativePath))
        {
            errors.Add($"unsafe file path: {file.RelativePath}");
        }

        if (file.Bytes < 0 || !Sha256Pattern().IsMatch(file.Sha256))
        {
            errors.Add($"invalid byte/hash declaration: {file.RelativePath}");
        }

        var extension = Path.GetExtension(file.RelativePath);
        var valid = file.Role switch
        {
            ValidationPackageArtifactRole.Profile
                or ValidationPackageArtifactRole.Workcell
                or ValidationPackageArtifactRole.MotionPlan
                or ValidationPackageArtifactRole.Expectations =>
                string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)
                && string.Equals(file.Encoding, "UTF-8", StringComparison.OrdinalIgnoreCase),
            ValidationPackageArtifactRole.KrlSource =>
                string.Equals(extension, ".src", StringComparison.OrdinalIgnoreCase)
                && IsAllowedTextEncoding(file.Encoding),
            ValidationPackageArtifactRole.KrlData =>
                string.Equals(extension, ".dat", StringComparison.OrdinalIgnoreCase)
                && IsAllowedTextEncoding(file.Encoding),
            _ => false
        };
        if (!valid)
        {
            errors.Add($"role/extension/encoding mismatch: {file.RelativePath}");
        }
    }

    private static void RequireExactRole(
        IReadOnlyList<ValidationPackageFileDeclaration> files,
        string path,
        ValidationPackageArtifactRole role,
        List<string> errors)
    {
        if (files.Count(file =>
                file is not null
                &&
                string.Equals(file.RelativePath, path, StringComparison.Ordinal)
                && file.Role == role) != 1)
        {
            errors.Add($"exactly one {path} with role {role} is required");
        }
    }

    private static bool IsAllowedTextEncoding(string encoding) =>
        string.Equals(encoding, "UTF-8", StringComparison.OrdinalIgnoreCase)
        || string.Equals(encoding, "Windows-1252", StringComparison.OrdinalIgnoreCase)
        || string.Equals(encoding, "US-ASCII", StringComparison.OrdinalIgnoreCase);

    private static void RequireObject<T>(T? value, string name, List<string> errors)
        where T : class
    {
        if (value is null)
        {
            errors.Add($"{name} is required");
        }
    }

    private static void RequireText(string? value, string name, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{name} is required");
        }
    }

    private static VerificationCheck Passed(string id, string detail) =>
        new() { Id = id, Status = VerificationStatus.Passed, Detail = detail };

    private static VerificationCheck Failed(string id, string detail) =>
        new() { Id = id, Status = VerificationStatus.Failed, Detail = detail };

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{2,95}$", RegexOptions.CultureInvariant)]
    private static partial Regex FixtureIdPattern();

    [GeneratedRegex("^[A-F0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();

    [GeneratedRegex("^sha256:[A-F0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex PackageIdPattern();
}

public sealed partial class ValidationPackageVerifier
{
    private readonly TimeProvider _timeProvider;

    public ValidationPackageVerifier(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public ValidationPackageVerificationOutcome Verify(
        ValidationPackageVerificationRequest request,
        string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAttemptId(attemptId);
        var packageRoot = Path.GetFullPath(request.PackageRoot);
        var outputPath = Path.GetFullPath(request.ReceiptOutputPath);
        ValidationPackageIdentity.ValidateOutputBoundary(packageRoot, outputPath);

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var snapshot = ValidationPackageIdentity.Capture(packageRoot);
        var checks = ValidationPackageRules.Evaluate(
            snapshot.Manifest,
            snapshot.Files,
            snapshot.ComputedPackageId);
        stopwatch.Stop();
        var status = checks.All(check => check.Status == VerificationStatus.Passed)
            ? VerificationStatus.Passed
            : VerificationStatus.Failed;
        var receipt = new ValidationPackageIntegrityReceipt
        {
            Payload = new ValidationPackageIntegrityPayload
            {
                ReceiptId = $"validation-package-{snapshot.ComputedPackageId[^16..].ToLowerInvariant()}-{attemptId}",
                AttemptId = attemptId,
                CoreAssemblySha256 = ComputeFileSha256(typeof(ValidationPackageVerifier).Assembly.Location),
                Runtime = new RuntimeEnvironment
                {
                    OsDescription = RuntimeInformation.OSDescription,
                    FrameworkDescription = RuntimeInformation.FrameworkDescription,
                    ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
                },
                StartedAtUtc = startedAt,
                CompletedAtUtc = _timeProvider.GetUtcNow(),
                DurationMilliseconds = Math.Max(0, stopwatch.ElapsedMilliseconds),
                TerminalClassification = status,
                ArtifactIntegrity = status,
                DeclaredPackageId = snapshot.Manifest.PackageId,
                ComputedPackageId = snapshot.ComputedPackageId,
                ManifestSha256 = snapshot.ManifestSha256,
                ManifestBytes = snapshot.ManifestBytesContent.LongLength,
                Manifest = snapshot.Manifest,
                Files = snapshot.Files,
                Checks = checks,
                NativeKss = new NativeKssObservation
                {
                    Status = NativeKssStatus.NotRun,
                    Reason = "ValidationPackage integrity verification does not execute a KSS runtime."
                },
                SideEffects = [$"CreateNewReceiptFile:{outputPath}"],
                EnvironmentReusable = true,
                UnsupportedClaims = ValidationPackageContract.RequiredUnsupportedClaims.ToList()
            }
        };
        receipt = receipt with
        {
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(receipt.Payload)
        };
        return new ValidationPackageVerificationOutcome(receipt);
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

public static class ValidationPackageReceiptVerifier
{
    public static ValidationPackageReceiptVerificationResult VerifyIntegrity(
        ValidationPackageIntegrityReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (!string.Equals(
                receipt.SchemaIdentity,
                ValidationPackageContract.ReceiptSchemaIdentity,
                StringComparison.Ordinal)
            || receipt.SchemaVersion != ValidationPackageContract.ReceiptSchemaVersion)
        {
            errors.Add("ValidationPackage receipt schema identity/version is unsupported.");
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
            errors.Add("receipt, attempt, Core and runtime identities are required");
        }

        if (payload.StartedAtUtc > payload.CompletedAtUtc
            || payload.DurationMilliseconds < 0
            || !IsSha256(payload.ManifestSha256)
            || payload.ManifestBytes <= 0
            || payload.Manifest is null
            || payload.Files is null
            || payload.Checks is null
            || payload.SideEffects is null
            || payload.UnsupportedClaims is null)
        {
            errors.Add("timing, manifest or evidence collections are invalid");
        }
        else
        {
            var expectedChecks = ValidationPackageRules.Evaluate(
                payload.Manifest,
                payload.Files,
                payload.ComputedPackageId);
            if (!expectedChecks.SequenceEqual(payload.Checks))
            {
                errors.Add("checks do not match the manifest/file/package evidence");
            }

            var expectedStatus = payload.Checks.All(check => check.Status == VerificationStatus.Passed)
                ? VerificationStatus.Passed
                : VerificationStatus.Failed;
            if (payload.TerminalClassification != expectedStatus
                || payload.ArtifactIntegrity != expectedStatus
                || !string.Equals(payload.DeclaredPackageId, payload.Manifest.PackageId, StringComparison.Ordinal))
            {
                errors.Add("terminal/artifact/package claims are inconsistent with the check evidence");
            }
        }

        if (payload.NativeKss is null
            || payload.NativeKss.Status != NativeKssStatus.NotRun
            || payload.NativeKss.ExpectedCompileResult is not null
            || !payload.EnvironmentReusable)
        {
            errors.Add("package integrity cannot claim native KSS execution or a non-reusable environment");
        }

        if (payload.SideEffects is null
            || payload.SideEffects.Count != 1
            || !payload.SideEffects[0].StartsWith("CreateNewReceiptFile:", StringComparison.Ordinal))
        {
            errors.Add("sideEffects must contain exactly one create-new receipt output");
        }

        if (payload.UnsupportedClaims is null
            || !ValidationPackageContract.RequiredUnsupportedClaims.SequenceEqual(
                payload.UnsupportedClaims,
                StringComparer.Ordinal))
        {
            errors.Add("unsupportedClaims must preserve the exact ValidationPackage boundary");
        }

        if (!string.Equals(
                receipt.PayloadSha256,
                ReceiptSerialization.ComputeCanonicalSha256(payload),
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("payloadSha256 does not match the canonical payload");
        }

        return new ValidationPackageReceiptVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            CurrentPackageVerified = false,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }

    public static ValidationPackageReceiptVerificationResult VerifyCurrentPackage(
        ValidationPackageIntegrityReceipt receipt,
        string packageRoot)
    {
        var integrity = VerifyIntegrity(receipt);
        var errors = integrity.Errors.ToList();
        var currentVerified = false;
        if (integrity.Succeeded)
        {
            try
            {
                var snapshot = ValidationPackageIdentity.Capture(packageRoot);
                currentVerified = string.Equals(
                        snapshot.ManifestSha256,
                        receipt.Payload.ManifestSha256,
                        StringComparison.OrdinalIgnoreCase)
                    && snapshot.ManifestBytesContent.LongLength == receipt.Payload.ManifestBytes
                    && string.Equals(
                        snapshot.ComputedPackageId,
                        receipt.Payload.ComputedPackageId,
                        StringComparison.Ordinal)
                    && snapshot.Files.SequenceEqual(receipt.Payload.Files);
                if (!currentVerified)
                {
                    errors.Add("The current ValidationPackage no longer matches the receipt evidence.");
                }
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or InvalidDataException
                or JsonException)
            {
                errors.Add($"The current ValidationPackage could not be verified: {exception.Message}");
            }
        }

        return integrity with
        {
            Succeeded = errors.Count == 0 && currentVerified,
            CurrentPackageVerified = currentVerified,
            Errors = errors
        };
    }

    private static ValidationPackageReceiptVerificationResult Failed(
        string payloadSha256,
        string error) =>
        new()
        {
            Succeeded = false,
            PayloadSha256 = payloadSha256,
            Errors = [error]
        };

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);
}

public static class ValidationPackageReceiptWriter
{
    public static string WriteNew(
        string outputPath,
        string packageRoot,
        ValidationPackageIntegrityReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!ValidationPackageReceiptVerifier.VerifyIntegrity(receipt).Succeeded)
        {
            throw new InvalidOperationException("ValidationPackage receipt integrity is invalid.");
        }

        var fullOutput = Path.GetFullPath(outputPath);
        ValidationPackageIdentity.ValidateOutputBoundary(packageRoot, fullOutput);
        var current = ValidationPackageIdentity.Capture(packageRoot);
        if (!string.Equals(current.ManifestSha256, receipt.Payload.ManifestSha256, StringComparison.OrdinalIgnoreCase)
            || current.ManifestBytesContent.LongLength != receipt.Payload.ManifestBytes
            || !string.Equals(current.ComputedPackageId, receipt.Payload.ComputedPackageId, StringComparison.Ordinal)
            || !current.Files.SequenceEqual(receipt.Payload.Files))
        {
            throw new InvalidOperationException(
                "The ValidationPackage changed after verification and before receipt creation.");
        }

        var expectedSideEffect = $"CreateNewReceiptFile:{fullOutput}";
        if (!string.Equals(receipt.Payload.SideEffects.Single(), expectedSideEffect, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Receipt output does not match its recorded side effect.");
        }

        var parent = Path.GetDirectoryName(fullOutput)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        Directory.CreateDirectory(parent);
        using var stream = new FileStream(fullOutput, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(ReceiptSerialization.ToJson(receipt));
        writer.WriteLine();
        writer.Flush();
        stream.Flush(flushToDisk: true);
        return fullOutput;
    }
}
