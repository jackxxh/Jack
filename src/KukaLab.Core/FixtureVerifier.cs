using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public sealed partial class FixtureVerifier
{
    private const string ManifestFileName = "fixture.json";
    private readonly TimeProvider _timeProvider;

    public FixtureVerifier(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public FixtureVerificationOutcome Verify(string fixtureDirectory, string attemptId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fixtureDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptId);

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var checks = new List<VerificationCheck>();
        var fileEvidence = new List<VerifiedFileEvidence>();
        var fixtureId = "unresolved-fixture";
        var fixtureRoot = Path.GetFullPath(fixtureDirectory);
        string? manifestSha256 = null;
        long? manifestBytes = null;
        NativeCompileExpectation? nativeExpectation = null;

        if (!AttemptIdPattern().IsMatch(attemptId))
        {
            checks.Add(Failed("attempt-id", "Attempt ID must match [A-Za-z0-9][A-Za-z0-9._-]{2,127}."));
            return Complete();
        }

        if (!Directory.Exists(fixtureRoot))
        {
            checks.Add(Failed("fixture-root", "Fixture root does not exist."));
            return Complete();
        }

        if (HasReparsePoint(fixtureRoot))
        {
            checks.Add(Failed("fixture-root", "Fixture root cannot be a reparse point."));
            return Complete();
        }

        checks.Add(Passed("fixture-root", "Fixture root exists and is not a reparse point."));

        var manifestPath = Path.Combine(fixtureRoot, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            checks.Add(Failed("manifest-presence", $"Required {ManifestFileName} was not found."));
            return Complete();
        }

        byte[] manifestContent;
        FixtureManifest? manifest;
        try
        {
            manifestContent = File.ReadAllBytes(manifestPath);
            manifestBytes = manifestContent.LongLength;
            manifestSha256 = Convert.ToHexString(SHA256.HashData(manifestContent));
            manifest = JsonSerializer.Deserialize<FixtureManifest>(
                manifestContent,
                ReceiptSerialization.StrictManifestOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            checks.Add(Failed("manifest-schema", $"Manifest could not be read as the strict v1 schema: {exception.Message}"));
            return Complete();
        }

        if (manifest is null)
        {
            checks.Add(Failed("manifest-schema", "Manifest content was empty."));
            return Complete();
        }

        fixtureId = string.IsNullOrWhiteSpace(manifest.FixtureId) ? fixtureId : manifest.FixtureId;
        nativeExpectation = manifest.NativeExpectation;
        ValidateManifest(manifest, checks);
        ValidateFiles(fixtureRoot, manifest, checks, fileEvidence);
        ValidateCorrelations(fixtureRoot, manifest, checks);

        return Complete();

        FixtureVerificationOutcome Complete()
        {
            stopwatch.Stop();
            var completedAt = _timeProvider.GetUtcNow();
            var terminalStatus = checks.Count > 0 && checks.All(check => check.Status == VerificationStatus.Passed)
                ? VerificationStatus.Passed
                : VerificationStatus.Failed;
            var receiptIdentity = manifestSha256 is null
                ? "no-manifest"
                : manifestSha256[..16].ToLowerInvariant();
            var payload = new FixtureIntegrityPayload
            {
                ReceiptId = $"fixture-integrity-{receiptIdentity}-{attemptId}",
                AttemptId = attemptId,
                FixtureId = fixtureId,
                CoreAssemblySha256 = ComputeFileSha256(typeof(FixtureVerifier).Assembly.Location),
                Runtime = new RuntimeEnvironment
                {
                    OsDescription = RuntimeInformation.OSDescription,
                    FrameworkDescription = RuntimeInformation.FrameworkDescription,
                    ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
                },
                StartedAtUtc = startedAt,
                CompletedAtUtc = completedAt,
                DurationMilliseconds = Math.Max(0, stopwatch.ElapsedMilliseconds),
                TerminalClassification = terminalStatus,
                ArtifactIntegrity = terminalStatus,
                FixtureRoot = fixtureRoot,
                ManifestSha256 = manifestSha256,
                ManifestBytes = manifestBytes,
                NativeKss = new NativeKssObservation
                {
                    Status = NativeKssStatus.NotRun,
                    ExpectedCompileResult = nativeExpectation
                },
                Checks = checks,
                Files = fileEvidence,
                SideEffects = [],
                EnvironmentReusable = true,
                UnsupportedGaps = ["Native KSS compilation and diagnostics were not executed by this operation."]
            };
            var receipt = new FixtureIntegrityReceipt
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputePayloadSha256(payload)
            };
            return new FixtureVerificationOutcome(receipt);
        }
    }

    private static void ValidateManifest(FixtureManifest manifest, List<VerificationCheck> checks)
    {
        var errors = new List<string>();
        if (!string.Equals(manifest.SchemaIdentity, FixtureContract.ManifestSchemaIdentity, StringComparison.Ordinal))
        {
            errors.Add($"schemaIdentity must be {FixtureContract.ManifestSchemaIdentity}");
        }

        if (!FixtureContract.SupportedManifestSchemaVersions.Contains(manifest.SchemaVersion))
        {
            errors.Add($"schemaVersion must be one of: {string.Join(", ", FixtureContract.SupportedManifestSchemaVersions.Order())}");
        }

        if (!FixtureIdPattern().IsMatch(manifest.FixtureId))
        {
            errors.Add("fixtureId must match [a-z0-9][a-z0-9-]{2,95}");
        }

        if (!string.Equals(manifest.Provenance, "LabSynthetic", StringComparison.Ordinal))
        {
            errors.Add("provenance must explicitly be LabSynthetic for the tracked seed fixtures");
        }

        if (!string.Equals(manifest.ControllerTarget.Vendor, "KUKA", StringComparison.Ordinal))
        {
            errors.Add("controllerTarget.vendor must be KUKA");
        }

        RequireText(manifest.ControllerTarget.KssVersion, "controllerTarget.kssVersion", errors);
        RequireText(manifest.ControllerTarget.RobotModel, "controllerTarget.robotModel", errors);
        RequireText(manifest.ControllerTarget.RobotEvidence, "controllerTarget.robotEvidence", errors);
        RequireText(manifest.ControllerTarget.ControllerModel, "controllerTarget.controllerModel", errors);
        RequireText(manifest.Frames.LoadDeclaration, "frames.loadDeclaration", errors);
        RequireText(manifest.Frames.Provenance, "frames.provenance", errors);

        if (!manifest.Safety.RedactionConfirmed)
        {
            errors.Add("safety.redactionConfirmed must be true");
        }

        if (manifest.Safety.PhysicalMotionAllowed)
        {
            errors.Add("tracked lab fixtures cannot allow physical motion");
        }

        if (!string.Equals(manifest.Safety.Classification, "VirtualOnlyNoSecrets", StringComparison.Ordinal))
        {
            errors.Add("safety.classification must be VirtualOnlyNoSecrets");
        }

        if (manifest.Files.Count == 0)
        {
            errors.Add("files must contain at least one controller artifact");
        }

        if (manifest.ExpectedAssertions.Count == 0 || manifest.ExpectedAssertions.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("expectedAssertions must contain non-empty assertions");
        }

        if (manifest.Classification == FixtureClassification.IntentionalFailure && manifest.ExpectedDiagnostic is null)
        {
            errors.Add("intentional-failure fixtures require expectedDiagnostic");
        }

        if (manifest.Classification == FixtureClassification.GoldenPath
            && manifest.NativeExpectation != NativeCompileExpectation.CompileAccepted)
        {
            errors.Add("golden-path fixtures must expect CompileAccepted");
        }

        if (manifest.Classification == FixtureClassification.IntentionalFailure
            && manifest.NativeExpectation != NativeCompileExpectation.CompileRejected)
        {
            errors.Add("intentional-failure fixtures must expect CompileRejected");
        }

        if (manifest.SchemaVersion >= 2)
        {
            ValidateVersion2Manifest(manifest, errors);
        }

        checks.Add(errors.Count == 0
            ? Passed("manifest-schema", "Manifest identity, provenance, target declaration and safety policy are valid.")
            : Failed("manifest-schema", string.Join("; ", errors)));
    }

    private static void ValidateFiles(
        string fixtureRoot,
        FixtureManifest manifest,
        List<VerificationCheck> checks,
        List<VerifiedFileEvidence> evidence)
    {
        var boundaryErrors = new List<string>();
        var inventoryErrors = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rootPrefix = fixtureRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        foreach (var declaration in manifest.Files)
        {
            var relativePath = declaration.RelativePath;
            if (!IsSafeControllerRelativePath(relativePath) || !seen.Add(relativePath))
            {
                boundaryErrors.Add($"unsafe or duplicate file path: {relativePath}");
                evidence.Add(new VerifiedFileEvidence
                {
                    RelativePath = relativePath,
                    ExpectedBytes = declaration.Bytes,
                    ExpectedSha256 = declaration.Sha256,
                    Status = VerificationStatus.Failed
                });
                continue;
            }

            var platformRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(fixtureRoot, platformRelativePath));
            if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                boundaryErrors.Add($"file resolves outside fixture root: {relativePath}");
                continue;
            }

            if (!File.Exists(fullPath))
            {
                inventoryErrors.Add($"file is missing: {relativePath}");
                evidence.Add(new VerifiedFileEvidence
                {
                    RelativePath = relativePath,
                    ExpectedBytes = declaration.Bytes,
                    ExpectedSha256 = declaration.Sha256,
                    Status = VerificationStatus.Failed
                });
                continue;
            }

            var controllerDirectory = Path.GetDirectoryName(fullPath)!;
            if (HasReparsePoint(controllerDirectory) || HasReparsePoint(fullPath))
            {
                boundaryErrors.Add($"file path cannot cross a reparse point: {relativePath}");
                continue;
            }

            long bytes;
            string sha256;
            FixtureTextEncoding? actualEncoding = null;
            FixtureLineEnding? actualLineEnding = null;
            try
            {
                var content = File.ReadAllBytes(fullPath);
                bytes = content.LongLength;
                sha256 = Convert.ToHexString(SHA256.HashData(content));
                actualEncoding = content.All(value => value <= 0x7F)
                    ? FixtureTextEncoding.Ascii7Bit
                    : null;
                actualLineEnding = content.Length > 0
                    && content[^1] == (byte)'\n'
                    && !content.Contains((byte)'\r')
                        ? FixtureLineEnding.Lf
                        : null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                inventoryErrors.Add($"file could not be read: {relativePath} ({exception.Message})");
                evidence.Add(new VerifiedFileEvidence
                {
                    RelativePath = relativePath,
                    ExpectedBytes = declaration.Bytes,
                    ExpectedSha256 = declaration.Sha256,
                    Status = VerificationStatus.Failed
                });
                continue;
            }
            var declarationShapeValid = declaration.Bytes >= 0 && Sha256Pattern().IsMatch(declaration.Sha256);
            var matches = declarationShapeValid
                && bytes == declaration.Bytes
                && string.Equals(sha256, declaration.Sha256, StringComparison.OrdinalIgnoreCase)
                && (manifest.SchemaVersion < 2
                    || declaration.Encoding == actualEncoding
                    && declaration.LineEnding == actualLineEnding);
            if (!matches)
            {
                inventoryErrors.Add($"length/hash mismatch: {relativePath}");
            }

            evidence.Add(new VerifiedFileEvidence
            {
                RelativePath = relativePath,
                ExpectedBytes = declaration.Bytes,
                ActualBytes = bytes,
                ExpectedSha256 = declaration.Sha256,
                ActualSha256 = sha256,
                ExpectedEncoding = declaration.Encoding,
                ActualEncoding = actualEncoding,
                ExpectedLineEnding = declaration.LineEnding,
                ActualLineEnding = actualLineEnding,
                Status = matches ? VerificationStatus.Passed : VerificationStatus.Failed
            });
        }

        checks.Add(boundaryErrors.Count == 0
            ? Passed("file-boundary", "All declared artifacts are unique, local controller files without reparse points.")
            : Failed("file-boundary", string.Join("; ", boundaryErrors)));
        checks.Add(inventoryErrors.Count == 0
            ? Passed("file-inventory", "All declared artifact lengths and SHA-256 values match.")
            : Failed("file-inventory", string.Join("; ", inventoryErrors)));
    }

    private static void ValidateCorrelations(
        string fixtureRoot,
        FixtureManifest manifest,
        List<VerificationCheck> checks)
    {
        var declaredFiles = manifest.Files
            .Select(file => file.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var operationIds = new HashSet<string>(StringComparer.Ordinal);
        var targetIds = new HashSet<string>(StringComparer.Ordinal);
        var lineCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (manifest.Correlations.Count == 0)
        {
            errors.Add("at least one KRL correlation is required");
        }

        foreach (var correlation in manifest.Correlations)
        {
            RequireText(correlation.OperationId, "correlation.operationId", errors);
            RequireText(correlation.TargetId, "correlation.targetId", errors);
            RequireText(correlation.MotionType, "correlation.motionType", errors);
            if (!operationIds.Add(correlation.OperationId))
            {
                errors.Add($"duplicate operationId: {correlation.OperationId}");
            }

            if (!targetIds.Add(correlation.TargetId))
            {
                errors.Add($"duplicate targetId: {correlation.TargetId}");
            }

            if (!declaredFiles.Contains(correlation.File))
            {
                errors.Add($"correlation file is not declared: {correlation.File}");
            }

            if (correlation.Line <= 0)
            {
                errors.Add($"correlation line must be positive: {correlation.TargetId}");
            }
            else if (TryGetLineCount(fixtureRoot, correlation.File, lineCounts, out var lineCount)
                && correlation.Line > lineCount)
            {
                errors.Add($"correlation line exceeds file length: {correlation.TargetId}");
            }


            if (manifest.SchemaVersion >= 2)
            {
                if (correlation.Sequence is null or <= 0)
                {
                    errors.Add($"correlation sequence must be positive: {correlation.TargetId}");
                }

                RequireText(correlation.KrlSymbol ?? string.Empty, "correlation.krlSymbol", errors);
                if (declaredFiles.Contains(correlation.File)
                    && TryGetLine(
                        fixtureRoot,
                        correlation.File,
                        correlation.Line,
                        out var sourceLine)
                    && !CorrelationMatchesSourceLine(correlation, sourceLine))
                {
                    errors.Add($"correlation does not match KRL source line: {correlation.TargetId}");
                }
            }
        }

        if (manifest.SchemaVersion >= 2)
        {
            var orderedSequences = manifest.Correlations
                .Select(correlation => correlation.Sequence ?? 0)
                .Order()
                .ToList();
            var expectedSequences = Enumerable.Range(1, manifest.Correlations.Count).ToList();
            if (!orderedSequences.SequenceEqual(expectedSequences))
            {
                errors.Add("correlation sequences must be unique and contiguous from 1");
            }
        }

        if (manifest.ExpectedDiagnostic is not null)
        {
            RequireText(manifest.ExpectedDiagnostic.Category, "expectedDiagnostic.category", errors);
            if (!declaredFiles.Contains(manifest.ExpectedDiagnostic.File))
            {
                errors.Add($"expected diagnostic file is not declared: {manifest.ExpectedDiagnostic.File}");
            }

            if (manifest.ExpectedDiagnostic.Line <= 0)
            {
                errors.Add("expected diagnostic line must be positive");
            }
            else if (TryGetLineCount(
                    fixtureRoot,
                    manifest.ExpectedDiagnostic.File,
                    lineCounts,
                    out var diagnosticLineCount)
                && manifest.ExpectedDiagnostic.Line > diagnosticLineCount)
            {
                errors.Add("expected diagnostic line exceeds file length");
            }

            if (manifest.SchemaVersion < 2
                && manifest.ExpectedDiagnostic.ObservationStatus != NativeKssStatus.NotRun)
            {
                errors.Add("tracked preconnection fixture diagnostics must remain NotRun");
            }


            if (manifest.SchemaVersion >= 2
                && manifest.ExpectedDiagnostic.ObservationStatus == NativeKssStatus.SyntaxSelectionValidated)
            {
                RequireText(manifest.ExpectedDiagnostic.NativeMessageCode ?? string.Empty, "expectedDiagnostic.nativeMessageCode", errors);
                RequireText(manifest.ExpectedDiagnostic.NativeFile ?? string.Empty, "expectedDiagnostic.nativeFile", errors);
                RequireText(manifest.ExpectedDiagnostic.EvidenceReference ?? string.Empty, "expectedDiagnostic.evidenceReference", errors);
                if (manifest.ExpectedDiagnostic.NativeLine is null or <= 0
                    || manifest.ExpectedDiagnostic.NativeColumn is null or <= 0)
                {
                    errors.Add("expectedDiagnostic native line and column must be positive");
                }

            }
            else if (manifest.SchemaVersion >= 2
                && manifest.ExpectedDiagnostic.ObservationStatus == NativeKssStatus.NotRun)
            {
                if (!string.IsNullOrWhiteSpace(manifest.ExpectedDiagnostic.NativeMessageCode)
                    || !string.IsNullOrWhiteSpace(manifest.ExpectedDiagnostic.NativeFile)
                    || manifest.ExpectedDiagnostic.NativeLine is not null
                    || manifest.ExpectedDiagnostic.NativeColumn is not null
                    || !string.IsNullOrWhiteSpace(manifest.ExpectedDiagnostic.EvidenceReference))
                {
                    errors.Add("NotRun diagnostics must not inherit native KSS evidence fields");
                }
            }
            else if (manifest.SchemaVersion >= 2)
            {
                errors.Add("v2 intentional-failure diagnostics must be NotRun or cite accepted SyntaxSelectionValidated evidence");
            }
        }

        checks.Add(errors.Count == 0
            ? Passed("correlation-map", "Operation, target and KRL file/line correlations are structurally valid.")
            : Failed("correlation-map", string.Join("; ", errors)));
    }

    private static bool IsSafeControllerRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathRooted(relativePath)
            || relativePath.Contains('\\', StringComparison.Ordinal)
            || relativePath.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2
            || !string.Equals(segments[0], "controller-files", StringComparison.Ordinal)
            || segments.Any(segment => segment is "." or ".."))
        {
            return false;
        }

        var extension = Path.GetExtension(relativePath);
        return string.Equals(extension, ".src", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".dat", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static bool TryGetLineCount(
        string fixtureRoot,
        string relativePath,
        Dictionary<string, int> cache,
        out int lineCount)
    {
        if (cache.TryGetValue(relativePath, out lineCount))
        {
            return true;
        }

        if (!IsSafeControllerRelativePath(relativePath))
        {
            lineCount = 0;
            return false;
        }

        var fullPath = Path.Combine(fixtureRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            lineCount = 0;
            return false;
        }

        try
        {
            lineCount = File.ReadLines(fullPath).Count();
            cache.Add(relativePath, lineCount);
            return true;
        }
        catch (Exception) when (File.Exists(fullPath))
        {
            lineCount = 0;
            return false;
        }
    }

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void ValidateVersion2Manifest(FixtureManifest manifest, List<string> errors)
    {
        if (manifest.ExecutionExpectation is null)
        {
            errors.Add("executionExpectation is required for schema v2");
        }

        if (manifest.MotionProviderPreference is null
            || !manifest.MotionProviderPreference.SequenceEqual(["Integrated", "RCS8.7"], StringComparer.Ordinal))
        {
            errors.Add("motionProviderPreference must be exactly Integrated then RCS8.7 for schema v2");
        }

        var exactC01SimulationProfile =
            string.Equals(manifest.ControllerTarget.RobotModel, "#KR210R2700_2 C01 FLR", StringComparison.Ordinal)
            && string.Equals(manifest.ControllerTarget.ControllerModel, "KRC5", StringComparison.Ordinal)
            && string.Equals(manifest.ControllerTarget.KssVersion, "8.7.8 B671", StringComparison.Ordinal)
            && string.Equals(
                manifest.ControllerTarget.KukaSimComponentSha256,
                KukaSimComponentSmokeContract.ExactKr210R2700ComponentSha256,
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                manifest.ControllerTarget.OfficeLiteScope,
                "GenericKssSemanticsOnlyNotExactC01Kinematics",
                StringComparison.Ordinal);
        var exactIsolatedOfficeLiteProfile =
            string.Equals(manifest.ControllerTarget.RobotModel, "#KR3R540 C4SR", StringComparison.Ordinal)
            && string.Equals(manifest.ControllerTarget.ControllerModel, "KRC5_MICRO", StringComparison.Ordinal)
            && string.Equals(manifest.ControllerTarget.KssVersion, "8.7.8 B671", StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(manifest.ControllerTarget.KukaSimComponentSha256)
            && string.Equals(
                manifest.ControllerTarget.OfficeLiteScope,
                "ExactIsolatedOfficeLiteKr3KssExecutionOnlyNotTargetWorkcellEvidence",
                StringComparison.Ordinal);
        var exactC01NativeOfficeLiteProfile =
            string.Equals(manifest.ControllerTarget.RobotModel, "#KR210R2700_2 C01 FLR", StringComparison.Ordinal)
            && string.Equals(manifest.ControllerTarget.ControllerModel, "KRC5", StringComparison.Ordinal)
            && string.Equals(manifest.ControllerTarget.KssVersion, "8.7.8 B671", StringComparison.Ordinal)
            && string.Equals(
                manifest.ControllerTarget.KukaSimComponentSha256,
                KukaSimComponentSmokeContract.ExactKr210R2700Component410Sha256,
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                manifest.ControllerTarget.OfficeLiteScope,
                "ExactC01OfficeLiteNativeKssExecutionNoMotion",
                StringComparison.Ordinal);

        if (!exactC01SimulationProfile && !exactIsolatedOfficeLiteProfile && !exactC01NativeOfficeLiteProfile)
        {
            errors.Add("schema v2 must bind the exact C01 simulation profile, exact isolated OfficeLite KR3 execution profile, or exact C01 native OfficeLite no-motion profile");
        }

        if (exactC01SimulationProfile
            && manifest.NativeEvidenceStatus == NativeKssStatus.SyntaxSelectionValidated
            && string.IsNullOrWhiteSpace(manifest.NativeEvidenceReference))
        {
            errors.Add("a SyntaxSelectionValidated exact C01 simulation profile must bind its accepted native KSS evidence");
        }

        if (exactC01SimulationProfile
            && manifest.NativeEvidenceStatus == NativeKssStatus.NotRun
            && !string.IsNullOrWhiteSpace(manifest.NativeEvidenceReference))
        {
            errors.Add("a NotRun exact C01 simulation profile must not inherit a native KSS evidence reference");
        }

        if (exactC01SimulationProfile
            && manifest.NativeEvidenceStatus is not (NativeKssStatus.SyntaxSelectionValidated or NativeKssStatus.NotRun))
        {
            errors.Add("the exact C01 simulation profile native evidence status must be NotRun or SyntaxSelectionValidated");
        }

        if (exactIsolatedOfficeLiteProfile
            && ((manifest.NativeEvidenceStatus is not null
                    && manifest.NativeEvidenceStatus != NativeKssStatus.NotRun)
                || !string.IsNullOrWhiteSpace(manifest.NativeEvidenceReference)))
        {
            errors.Add("the isolated OfficeLite execution seed must remain NotRun until a separate native execution receipt is produced");
        }

        if (exactC01NativeOfficeLiteProfile
            && (manifest.NativeEvidenceStatus != NativeKssStatus.BoundedExecutionValidated
                || string.IsNullOrWhiteSpace(manifest.NativeEvidenceReference)))
        {
            errors.Add("the exact C01 native OfficeLite no-motion fixture must bind accepted BoundedExecutionValidated evidence");
        }

        if (manifest.Files.Any(file => file.Encoding != FixtureTextEncoding.Ascii7Bit
                || file.LineEnding != FixtureLineEnding.Lf))
        {
            errors.Add("schema v2 controller files must declare Ascii7Bit encoding and Lf line endings");
        }

        var expectedExecution = manifest.Classification == FixtureClassification.GoldenPath
            ? FixtureExecutionExpectation.BoundedVirtualExecutionExpected
            : FixtureExecutionExpectation.RejectedBeforeExecution;
        if (manifest.ExecutionExpectation != expectedExecution)
        {
            errors.Add($"{manifest.Classification} must declare executionExpectation {expectedExecution}");
        }
    }


    private static bool TryGetLine(
        string fixtureRoot,
        string relativePath,
        int line,
        out string sourceLine)
    {
        sourceLine = string.Empty;
        if (line <= 0 || !IsSafeControllerRelativePath(relativePath))
        {
            return false;
        }

        var fullPath = Path.Combine(fixtureRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            sourceLine = File.ReadLines(fullPath).Skip(line - 1).FirstOrDefault() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(sourceLine);
        }
        catch (Exception) when (File.Exists(fullPath))
        {
            return false;
        }
    }

    private static bool CorrelationMatchesSourceLine(FixtureCorrelation correlation, string sourceLine)
    {
        var code = sourceLine.Split(';', 2)[0].Trim();
        var pattern = $"^{Regex.Escape(correlation.MotionType)}\\s+.*\\b{Regex.Escape(correlation.KrlSymbol!)}\\b";
        return Regex.IsMatch(code, pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }

    private static void RequireText(string value, string field, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{field} is required");
        }
    }

    private static VerificationCheck Passed(string id, string detail) =>
        new() { Id = id, Status = VerificationStatus.Passed, Detail = detail };

    private static VerificationCheck Failed(string id, string detail) =>
        new() { Id = id, Status = VerificationStatus.Failed, Detail = detail };

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{2,95}$", RegexOptions.CultureInvariant)]
    private static partial Regex FixtureIdPattern();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex AttemptIdPattern();

    [GeneratedRegex("^[A-Fa-f0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();
}
