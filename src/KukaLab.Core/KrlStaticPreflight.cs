using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class KrlStaticPreflightContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.krl-static-preflight-receipt";
    public const int ReceiptSchemaVersion = 1;

    public static readonly IReadOnlyList<string> UnsupportedClaims =
    [
        "Static KRL preflight does not execute the native KSS parser, compiler or interpreter.",
        "A passing preflight does not prove controller acceptance, reachability, collision freedom, cycle time or physical safety.",
        "The operation reads only the frozen ValidationPackage and does not start KUKA.Sim, OfficeLite, WorkVisual or contact a physical controller."
    ];
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum KrlStaticPreflightDisposition
{
    ReadyForNativeKssSubmission,
    RejectedByLocalPreflight
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum KrlStaticFindingSeverity
{
    Error,
    Warning
}

public sealed record KrlStaticPreflightRequest
{
    public required string PackageRoot { get; init; }

    public required string ValidationPackageReceiptPath { get; init; }

    public required string ReceiptOutputPath { get; init; }
}

public sealed record KrlStaticFinding
{
    public required string Code { get; init; }

    public required KrlStaticFindingSeverity Severity { get; init; }

    public required string File { get; init; }

    public required int Line { get; init; }

    public string? Symbol { get; init; }

    public required string Message { get; init; }
}

public sealed record KrlProgramEvidence
{
    public required string ProgramName { get; init; }

    public required string SourceFile { get; init; }

    public required string DataFile { get; init; }

    public required int SourceLineCount { get; init; }

    public required int DataLineCount { get; init; }

    public required int MotionInstructionCount { get; init; }

    public required bool HasBasInitMov { get; init; }
}

public sealed record KrlStaticPreflightReceipt
{
    public string SchemaIdentity { get; init; } = KrlStaticPreflightContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = KrlStaticPreflightContract.ReceiptSchemaVersion;

    public required KrlStaticPreflightPayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record KrlStaticPreflightPayload
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

    public KrlStaticPreflightDisposition Disposition { get; init; }

    public string ValidationPackageId { get; init; } = string.Empty;

    public string ValidationPackageReceiptPayloadSha256 { get; init; } = string.Empty;

    public string ValidationPackageReceiptFileSha256 { get; init; } = string.Empty;

    public int MotionInstructionCount { get; init; }

    public List<KrlProgramEvidence> Programs { get; init; } = [];

    public List<KrlStaticFinding> Findings { get; init; } = [];

    public List<VerificationCheck> Checks { get; init; } = [];

    public NativeKssObservation NativeKss { get; init; } = new();

    public List<string> SideEffects { get; init; } = [];

    public bool EnvironmentReusable { get; init; } = true;

    public List<string> UnsupportedClaims { get; init; } = [];
}

public sealed record KrlStaticPreflightOutcome(KrlStaticPreflightReceipt Receipt)
{
    public bool Succeeded =>
        Receipt.Payload.TerminalClassification == VerificationStatus.Passed;

    public int ExitCode => Succeeded ? 0 : 2;
}

public sealed record KrlStaticPreflightVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}

public sealed partial class KrlStaticPreflightRunner
{
    private readonly TimeProvider _timeProvider;

    public KrlStaticPreflightRunner(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public KrlStaticPreflightOutcome Run(KrlStaticPreflightRequest request, string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAttemptId(attemptId);
        var packageRoot = Path.GetFullPath(request.PackageRoot);
        var packageReceiptPath = Path.GetFullPath(request.ValidationPackageReceiptPath);
        var outputPath = Path.GetFullPath(request.ReceiptOutputPath);
        ValidationPackageIdentity.ValidateOutputBoundary(packageRoot, outputPath);

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var packageReceiptBytes = ReadStableBytes(packageReceiptPath);
        var packageReceipt = ReceiptSerialization.ValidationPackageFromJson(
            Encoding.UTF8.GetString(packageReceiptBytes));
        var packageVerification = ValidationPackageReceiptVerifier.VerifyCurrentPackage(
            packageReceipt,
            packageRoot);
        if (!packageVerification.Succeeded)
        {
            throw new InvalidDataException(
                "ValidationPackage receipt/current-package verification failed: "
                + string.Join("; ", packageVerification.Errors));
        }

        var analysis = Analyze(packageRoot, packageReceipt.Payload.Manifest);
        var checks = KrlStaticPreflightRules.BuildChecks(analysis.Programs, analysis.Findings);
        stopwatch.Stop();
        var terminal = checks.All(check => check.Status == VerificationStatus.Passed)
            ? VerificationStatus.Passed
            : VerificationStatus.Failed;
        var disposition = terminal == VerificationStatus.Passed
            ? KrlStaticPreflightDisposition.ReadyForNativeKssSubmission
            : KrlStaticPreflightDisposition.RejectedByLocalPreflight;
        var payload = new KrlStaticPreflightPayload
        {
            ReceiptId = $"krl-static-{packageReceipt.Payload.ComputedPackageId[^16..].ToLowerInvariant()}-{attemptId}",
            AttemptId = attemptId,
            CoreAssemblySha256 = ComputeFileSha256(typeof(KrlStaticPreflightRunner).Assembly.Location),
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
            Disposition = disposition,
            ValidationPackageId = packageReceipt.Payload.ComputedPackageId,
            ValidationPackageReceiptPayloadSha256 = packageReceipt.PayloadSha256,
            ValidationPackageReceiptFileSha256 = Convert.ToHexString(SHA256.HashData(packageReceiptBytes)),
            MotionInstructionCount = analysis.Programs.Sum(program => program.MotionInstructionCount),
            Programs = analysis.Programs,
            Findings = analysis.Findings,
            Checks = checks,
            NativeKss = new NativeKssObservation
            {
                Status = NativeKssStatus.NotRun,
                Reason = "Static SRC/DAT envelope and target-reference checks do not execute native KSS."
            },
            SideEffects = [$"CreateNewReceiptFile:{outputPath}"],
            EnvironmentReusable = true,
            UnsupportedClaims = KrlStaticPreflightContract.UnsupportedClaims.ToList()
        };
        return new KrlStaticPreflightOutcome(new KrlStaticPreflightReceipt
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        });
    }

    private static KrlAnalysis Analyze(
        string packageRoot,
        ValidationPackageManifest manifest)
    {
        var findings = new List<KrlStaticFinding>();
        var programs = new List<KrlProgramEvidence>();
        var sources = manifest.Files
            .Where(file => file.Role == ValidationPackageArtifactRole.KrlSource)
            .ToDictionary(file => WithoutExtension(file.RelativePath), StringComparer.OrdinalIgnoreCase);
        var data = manifest.Files
            .Where(file => file.Role == ValidationPackageArtifactRole.KrlData)
            .ToDictionary(file => WithoutExtension(file.RelativePath), StringComparer.OrdinalIgnoreCase);
        var declaredTargets = new Dictionary<string, (string File, int Line)>(StringComparer.OrdinalIgnoreCase);
        var dataViews = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var declaration in data.Values.OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            var lines = ReadDeclaredAsciiLines(packageRoot, declaration);
            dataViews[WithoutExtension(declaration.RelativePath)] = lines;
            for (var index = 0; index < lines.Length; index++)
            {
                var match = TargetDeclarationPattern().Match(StripComment(lines[index]));
                if (!match.Success)
                {
                    continue;
                }

                var symbol = match.Groups[1].Value;
                if (!declaredTargets.TryAdd(symbol, (declaration.RelativePath, index + 1)))
                {
                    findings.Add(Error(
                        "KRL031",
                        declaration.RelativePath,
                        index + 1,
                        symbol,
                        $"Target symbol is declared more than once; first declaration is {declaredTargets[symbol].File}:{declaredTargets[symbol].Line}."));
                }
            }
        }

        foreach (var source in sources.Values.OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            var key = WithoutExtension(source.RelativePath);
            if (!data.TryGetValue(key, out var dataDeclaration))
            {
                findings.Add(Error("KRL001", source.RelativePath, 0, null, "Matching DAT file is missing."));
                continue;
            }

            var sourceLines = ReadDeclaredAsciiLines(packageRoot, source);
            var dataLines = dataViews[key];
            var sourceHeader = FindFirst(sourceLines, SourceHeaderPattern());
            var dataHeader = FindFirst(dataLines, DataHeaderPattern());
            var expectedName = Path.GetFileNameWithoutExtension(source.RelativePath);
            var programName = sourceHeader.Match?.Groups[1].Value ?? expectedName;
            if (sourceHeader.Match is null)
            {
                findings.Add(Error("KRL010", source.RelativePath, 0, null, "DEF program header is missing."));
            }
            else if (!string.Equals(programName, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(Error("KRL012", source.RelativePath, sourceHeader.Line, programName, "DEF name does not match the SRC file name."));
            }

            if (!HasStandaloneTerminator(sourceLines, "END"))
            {
                findings.Add(Error("KRL011", source.RelativePath, 0, null, "Standalone END terminator is missing."));
            }

            if (dataHeader.Match is null)
            {
                findings.Add(Error("KRL013", dataDeclaration.RelativePath, 0, null, "DEFDAT header is missing."));
            }
            else if (!string.Equals(
                         dataHeader.Match.Groups[1].Value,
                         expectedName,
                         StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(Error("KRL015", dataDeclaration.RelativePath, dataHeader.Line, dataHeader.Match.Groups[1].Value, "DEFDAT name does not match the DAT file name."));
            }

            if (!HasStandaloneTerminator(dataLines, "ENDDAT"))
            {
                findings.Add(Error("KRL014", dataDeclaration.RelativePath, 0, null, "Standalone ENDDAT terminator is missing."));
            }

            var hasInitMov = sourceLines.Any(line => BasInitMovPattern().IsMatch(StripComment(line)));
            if (!hasInitMov)
            {
                findings.Add(Error("KRL020", source.RelativePath, 0, null, "BAS(#INITMOV,0) initialization is missing."));
            }

            var motionCount = 0;
            var insideCpSpline = false;
            var cpSplineStartLine = 0;
            for (var index = 0; index < sourceLines.Length; index++)
            {
                var code = StripComment(sourceLines[index]).Trim();
                if (CpSplineStartPattern().IsMatch(code))
                {
                    if (insideCpSpline)
                    {
                        findings.Add(Error(
                            "KRL041",
                            source.RelativePath,
                            index + 1,
                            null,
                            $"Nested CP SPLINE block is not allowed; the current block started at line {cpSplineStartLine}."));
                    }
                    else
                    {
                        insideCpSpline = true;
                        cpSplineStartLine = index + 1;
                    }
                    continue;
                }

                if (CpSplineEndPattern().IsMatch(code))
                {
                    if (!insideCpSpline)
                    {
                        findings.Add(Error(
                            "KRL041",
                            source.RelativePath,
                            index + 1,
                            null,
                            "ENDSPLINE has no matching CP SPLINE block."));
                    }
                    insideCpSpline = false;
                    cpSplineStartLine = 0;
                    continue;
                }

                var motion = MotionPattern().Match(code);
                if (!motion.Success)
                {
                    continue;
                }

                motionCount++;
                var command = motion.Groups[1].Value.ToUpperInvariant();
                var operand = motion.Groups[2].Value.Trim();
                if (command == "SPL" && !insideCpSpline)
                {
                    findings.Add(Error(
                        "KRL040",
                        source.RelativePath,
                        index + 1,
                        null,
                        "SPL is a CP spline segment and must be inside SPLINE/ENDSPLINE."));
                }
                if (insideCpSpline && command is not ("SPL" or "SLIN" or "SCIRC"))
                {
                    findings.Add(Error(
                        "KRL042",
                        source.RelativePath,
                        index + 1,
                        command,
                        "A CP SPLINE block may contain only SPL, SLIN and SCIRC motion segments."));
                }
                if (operand.StartsWith("{", StringComparison.Ordinal))
                {
                    continue;
                }

                var maximumTargets = command is "CIRC" or "SCIRC" ? 2 : 1;
                var targets = IdentifierPattern().Matches(operand)
                    .Select(match => match.Groups[1].Value)
                    .Where(symbol => !symbol.StartsWith("$", StringComparison.Ordinal))
                    .Take(maximumTargets)
                    .ToList();
                foreach (var target in targets.Where(target => !declaredTargets.ContainsKey(target)))
                {
                    findings.Add(Error(
                        "KRL030",
                        source.RelativePath,
                        index + 1,
                        target,
                        "Motion target has no E6POS/E6AXIS/POS/AXIS/FRAME declaration in the package DAT files."));
                }
            }

            if (insideCpSpline)
            {
                findings.Add(Error(
                    "KRL041",
                    source.RelativePath,
                    cpSplineStartLine,
                    null,
                    "CP SPLINE block is missing ENDSPLINE."));
            }

            if (motionCount == 0)
            {
                findings.Add(Error("KRL021", source.RelativePath, 0, null, "No PTP/LIN/CIRC/SPL/SPTP/SLIN/SCIRC motion instruction was found."));
            }

            programs.Add(new KrlProgramEvidence
            {
                ProgramName = programName,
                SourceFile = source.RelativePath,
                DataFile = dataDeclaration.RelativePath,
                SourceLineCount = sourceLines.Length,
                DataLineCount = dataLines.Length,
                MotionInstructionCount = motionCount,
                HasBasInitMov = hasInitMov
            });
        }

        foreach (var orphan in data.Values
                     .Where(item => !sources.ContainsKey(WithoutExtension(item.RelativePath)))
                     .OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            findings.Add(Error("KRL002", orphan.RelativePath, 0, null, "Matching SRC file is missing."));
        }

        return new KrlAnalysis(
            programs.OrderBy(program => program.SourceFile, StringComparer.Ordinal).ToList(),
            findings
                .OrderBy(finding => finding.File, StringComparer.Ordinal)
                .ThenBy(finding => finding.Line)
                .ThenBy(finding => finding.Code, StringComparer.Ordinal)
                .ThenBy(finding => finding.Symbol, StringComparer.Ordinal)
                .ToList());
    }

    private static string[] ReadDeclaredAsciiLines(
        string packageRoot,
        ValidationPackageFileDeclaration declaration)
    {
        var fullPath = Path.GetFullPath(Path.Combine(
            packageRoot,
            declaration.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        var bytes = ReadStableBytes(fullPath);
        if (bytes.LongLength != declaration.Bytes
            || !string.Equals(
                Convert.ToHexString(SHA256.HashData(bytes)),
                declaration.Sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException($"KRL file changed after package verification: {declaration.RelativePath}");
        }

        var characters = bytes.Select(value => value is (byte)'\t' or (byte)'\r' or (byte)'\n'
            || value is >= 0x20 and <= 0x7E
                ? (char)value
                : ' ').ToArray();
        return new string(characters).Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static byte[] ReadStableBytes(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            throw new FileNotFoundException("Required evidence file was not found.", path);
        }

        info.Refresh();
        var length = info.Length;
        var writeTime = info.LastWriteTimeUtc;
        var bytes = File.ReadAllBytes(path);
        info.Refresh();
        if (bytes.LongLength != length || info.Length != length || info.LastWriteTimeUtc != writeTime)
        {
            throw new IOException($"File changed while it was being read: {path}");
        }

        return bytes;
    }

    private static (Match? Match, int Line) FindFirst(string[] lines, Regex pattern)
    {
        for (var index = 0; index < lines.Length; index++)
        {
            var match = pattern.Match(StripComment(lines[index]));
            if (match.Success)
            {
                return (match, index + 1);
            }
        }

        return (null, 0);
    }

    private static bool HasStandaloneTerminator(string[] lines, string terminator) =>
        lines.Any(line => string.Equals(
            StripComment(line).Trim(),
            terminator,
            StringComparison.OrdinalIgnoreCase));

    private static string StripComment(string line)
    {
        var comment = line.IndexOf(';');
        return comment < 0 ? line : line[..comment];
    }

    private static string WithoutExtension(string relativePath) =>
        relativePath[..^Path.GetExtension(relativePath).Length];

    private static KrlStaticFinding Error(
        string code,
        string file,
        int line,
        string? symbol,
        string message) =>
        new()
        {
            Code = code,
            Severity = KrlStaticFindingSeverity.Error,
            File = file,
            Line = line,
            Symbol = symbol,
            Message = message
        };

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
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

    private sealed record KrlAnalysis(
        List<KrlProgramEvidence> Programs,
        List<KrlStaticFinding> Findings);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex AttemptIdPattern();

    [GeneratedRegex(@"^\s*(?:GLOBAL\s+)?DEF\s+([A-Z_][A-Z0-9_$]*)\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SourceHeaderPattern();

    [GeneratedRegex(@"^\s*DEFDAT\s+([A-Z_][A-Z0-9_$]*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DataHeaderPattern();

    [GeneratedRegex(@"^\s*(?:DECL\s+)?(?:E6POS|E6AXIS|POS|AXIS|FRAME)\s+([A-Z_][A-Z0-9_$]*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TargetDeclarationPattern();

    [GeneratedRegex(@"^\s*BAS\s*\(\s*#INITMOV\s*,\s*0\s*\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BasInitMovPattern();

    [GeneratedRegex(@"^\s*(PTP|LIN|CIRC|SPL|SPTP|SLIN|SCIRC)\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MotionPattern();

    [GeneratedRegex(@"^SPLINE(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CpSplineStartPattern();

    [GeneratedRegex(@"^ENDSPLINE$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CpSplineEndPattern();

    [GeneratedRegex(@"(?:^|[^A-Z0-9_$])(\$?[A-Z_][A-Z0-9_$]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();
}

internal static class KrlStaticPreflightRules
{
    internal static List<VerificationCheck> BuildChecks(
        IReadOnlyList<KrlProgramEvidence> programs,
        IReadOnlyList<KrlStaticFinding> findings)
    {
        var errorCodes = findings
            .Where(finding => finding.Severity == KrlStaticFindingSeverity.Error)
            .Select(finding => finding.Code)
            .ToHashSet(StringComparer.Ordinal);
        return
        [
            Passed("validation-package-evidence", "The source ValidationPackage receipt and current package passed strict verification."),
            Check("src-dat-pairing", errorCodes.Overlaps(["KRL001", "KRL002"]), "Every KRL SRC has one matching DAT and no orphan DAT remains."),
            Check("program-envelope", errorCodes.Overlaps(["KRL010", "KRL011", "KRL012", "KRL013", "KRL014", "KRL015", "KRL021"]), "Program names, DEF/DEFDAT terminators and motion presence are structurally consistent."),
            Check("spline-structure", errorCodes.Overlaps(["KRL040", "KRL041", "KRL042"]), "CP SPLINE blocks are balanced and contain only documented segment motion types."),
            Check("initialization", errorCodes.Contains("KRL020"), "Every source program contains BAS(#INITMOV,0)."),
            Check("target-references", errorCodes.Overlaps(["KRL030", "KRL031"]), "Motion target references resolve uniquely to package DAT declarations."),
            programs.Count > 0
                ? Passed("program-inventory", $"Inspected {programs.Count} KRL program pair(s) and {programs.Sum(program => program.MotionInstructionCount)} motion instruction(s).")
                : Failed("program-inventory", "No complete KRL SRC/DAT program pair was available for inspection.")
        ];
    }

    private static VerificationCheck Check(string id, bool failed, string passedDetail) =>
        failed
            ? Failed(id, "One or more static KRL findings block this check; inspect findings for exact file and line evidence.")
            : Passed(id, passedDetail);

    private static VerificationCheck Passed(string id, string detail) =>
        new() { Id = id, Status = VerificationStatus.Passed, Detail = detail };

    private static VerificationCheck Failed(string id, string detail) =>
        new() { Id = id, Status = VerificationStatus.Failed, Detail = detail };
}

public static class KrlStaticPreflightReceiptVerifier
{
    public static KrlStaticPreflightVerificationResult Verify(KrlStaticPreflightReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (!string.Equals(receipt.SchemaIdentity, KrlStaticPreflightContract.ReceiptSchemaIdentity, StringComparison.Ordinal)
            || receipt.SchemaVersion != KrlStaticPreflightContract.ReceiptSchemaVersion)
        {
            errors.Add("KRL static-preflight receipt schema identity/version is unsupported.");
        }

        var payload = receipt.Payload;
        if (payload is null
            || string.IsNullOrWhiteSpace(payload.ReceiptId)
            || string.IsNullOrWhiteSpace(payload.AttemptId)
            || !IsSha256(payload.CoreAssemblySha256)
            || string.IsNullOrWhiteSpace(payload.ValidationPackageId)
            || !payload.ValidationPackageId.StartsWith(ValidationPackageContract.PackageIdPrefix, StringComparison.Ordinal)
            || !IsSha256(payload.ValidationPackageId[ValidationPackageContract.PackageIdPrefix.Length..])
            || !IsSha256(payload.ValidationPackageReceiptPayloadSha256)
            || !IsSha256(payload.ValidationPackageReceiptFileSha256)
            || payload.Runtime is null
            || string.IsNullOrWhiteSpace(payload.Runtime.OsDescription)
            || string.IsNullOrWhiteSpace(payload.Runtime.FrameworkDescription)
            || string.IsNullOrWhiteSpace(payload.Runtime.ProcessArchitecture)
            || payload.Programs is null
            || payload.Findings is null
            || payload.Checks is null
            || payload.NativeKss is null
            || payload.SideEffects is null
            || payload.UnsupportedClaims is null)
        {
            return Failed(receipt.PayloadSha256, "KRL static-preflight payload shape or identities are invalid.");
        }

        if (payload.Programs.Any(program => string.IsNullOrWhiteSpace(program.ProgramName)
                || string.IsNullOrWhiteSpace(program.SourceFile)
                || string.IsNullOrWhiteSpace(program.DataFile)
                || program.SourceLineCount <= 0
                || program.DataLineCount <= 0
                || program.MotionInstructionCount < 0)
            || payload.Findings.Any(finding => string.IsNullOrWhiteSpace(finding.Code)
                || string.IsNullOrWhiteSpace(finding.File)
                || finding.Line < 0
                || finding.Symbol is not null && string.IsNullOrWhiteSpace(finding.Symbol)
                || string.IsNullOrWhiteSpace(finding.Message))
            || payload.Checks.Any(check => string.IsNullOrWhiteSpace(check.Id)
                || string.IsNullOrWhiteSpace(check.Detail)))
        {
            errors.Add("program, finding or check evidence contains invalid values");
        }

        var expectedChecks = KrlStaticPreflightRules.BuildChecks(payload.Programs, payload.Findings);
        if (!expectedChecks.SequenceEqual(payload.Checks))
        {
            errors.Add("checks do not match the recorded programs and findings");
        }

        var expectedStatus = expectedChecks.All(check => check.Status == VerificationStatus.Passed)
            ? VerificationStatus.Passed
            : VerificationStatus.Failed;
        var expectedDisposition = expectedStatus == VerificationStatus.Passed
            ? KrlStaticPreflightDisposition.ReadyForNativeKssSubmission
            : KrlStaticPreflightDisposition.RejectedByLocalPreflight;
        if (payload.TerminalClassification != expectedStatus
            || payload.Disposition != expectedDisposition
            || payload.MotionInstructionCount != payload.Programs.Sum(program => program.MotionInstructionCount))
        {
            errors.Add("terminal disposition or motion count is inconsistent with evidence");
        }

        if (payload.NativeKss.Status != NativeKssStatus.NotRun
            || payload.NativeKss.ExpectedCompileResult is not null
            || string.IsNullOrWhiteSpace(payload.NativeKss.Reason)
            || !payload.EnvironmentReusable)
        {
            errors.Add("static preflight cannot claim native KSS execution or a non-reusable environment");
        }

        if (payload.SideEffects.Count != 1
            || !payload.SideEffects[0].StartsWith("CreateNewReceiptFile:", StringComparison.Ordinal))
        {
            errors.Add("sideEffects must contain exactly one create-new receipt output");
        }

        if (!KrlStaticPreflightContract.UnsupportedClaims.SequenceEqual(payload.UnsupportedClaims, StringComparer.Ordinal))
        {
            errors.Add("unsupportedClaims must preserve the exact static-preflight boundary");
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

        return new KrlStaticPreflightVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }

    private static KrlStaticPreflightVerificationResult Failed(string payloadSha256, string error) =>
        new() { PayloadSha256 = payloadSha256, Errors = [error] };

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);
}

public static class KrlStaticPreflightReceiptWriter
{
    public static string WriteNew(
        string outputPath,
        string packageRoot,
        ValidationPackageIntegrityReceipt packageReceipt,
        KrlStaticPreflightReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(packageReceipt);
        ArgumentNullException.ThrowIfNull(receipt);
        var fullOutput = Path.GetFullPath(outputPath);
        ValidationPackageIdentity.ValidateOutputBoundary(packageRoot, fullOutput);
        if (!ValidationPackageReceiptVerifier.VerifyCurrentPackage(packageReceipt, packageRoot).Succeeded)
        {
            throw new InvalidOperationException("ValidationPackage changed before KRL preflight receipt creation.");
        }

        if (!KrlStaticPreflightReceiptVerifier.Verify(receipt).Succeeded)
        {
            throw new InvalidOperationException("KRL static-preflight receipt integrity is invalid.");
        }

        if (!string.Equals(
            receipt.Payload.SideEffects.Single(),
            $"CreateNewReceiptFile:{fullOutput}",
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException("KRL preflight output does not match its recorded side effect.");
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
