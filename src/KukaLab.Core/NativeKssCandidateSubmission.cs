using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class NativeKssCandidateSubmissionContract
{
    public const string RobotType = "KR 210 R2700-2 C01";
    public const string MachineDataIdentity = "#KR210R2700_2 C01 FLR";
    public const string CabinetKind = "KRC5";
    public const string KssVersion = "8.7.8";
    public const int MaximumAllowedStartCommands = 64;
}

public sealed record NativeKssCandidateSubmissionRequest
{
    public required string CandidateRoot { get; init; }
    public required string CandidateReceiptPath { get; init; }
    public required string ProgramRelativeStem { get; init; }
    public int MaximumStartCommands { get; init; } = 16;
}

public sealed record NativeKssCandidateRequiredProfile
{
    public string RobotType { get; init; } = NativeKssCandidateSubmissionContract.RobotType;
    public string MachineDataIdentity { get; init; } = NativeKssCandidateSubmissionContract.MachineDataIdentity;
    public string CabinetKind { get; init; } = NativeKssCandidateSubmissionContract.CabinetKind;
    public string KssVersion { get; init; } = NativeKssCandidateSubmissionContract.KssVersion;
}

public sealed record NativeKssCandidateSubmissionPlan
{
    public required string CandidateId { get; init; }
    public required string CandidateRoot { get; init; }
    public required string CandidateReceiptPath { get; init; }
    public required string ProgramRelativeStem { get; init; }
    public required string ProgramName { get; init; }
    public required RawKrlCandidateFile Source { get; init; }
    public required RawKrlCandidateFile Data { get; init; }
    public NativeKssCandidateRequiredProfile RequiredProfile { get; init; } = new();
    public int MaximumStartCommands { get; init; }
    public required string ControllerTransactionRoot { get; init; }
}

public sealed class NativeKssCandidateSubmissionPlanner
{
    private static readonly Regex ProgramStemPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9_./-]{0,159}$",
        RegexOptions.CultureInvariant);

    public NativeKssCandidateSubmissionPlan Plan(NativeKssCandidateSubmissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var candidateRoot = Path.GetFullPath(request.CandidateRoot);
        var receiptPath = Path.GetFullPath(request.CandidateReceiptPath);
        if (request.MaximumStartCommands is < 1 or > NativeKssCandidateSubmissionContract.MaximumAllowedStartCommands)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"Maximum Start commands must be from 1 through {NativeKssCandidateSubmissionContract.MaximumAllowedStartCommands}.");
        }

        var requestedStem = NormalizeProgramStem(request.ProgramRelativeStem);
        if (!File.Exists(receiptPath))
        {
            throw new FileNotFoundException("Raw KRL candidate receipt does not exist.", receiptPath);
        }

        var receipt = ReceiptSerialization.RawKrlCandidateFromJson(File.ReadAllText(receiptPath));
        var verification = RawKrlCandidateReceiptVerifier.VerifyCurrentSource(receipt, candidateRoot);
        if (!verification.Succeeded || !verification.CurrentSourceVerified)
        {
            throw new InvalidDataException(
                "Raw KRL candidate receipt or current source verification failed: "
                + string.Join("; ", verification.Errors));
        }

        var programs = receipt.Payload.Programs.Where(candidate =>
            string.Equals(
                candidate.SourceFile[..^Path.GetExtension(candidate.SourceFile).Length].Replace('\\', '/'),
                requestedStem,
                StringComparison.OrdinalIgnoreCase)).ToList();
        var program = programs.Count == 1 ? programs[0] : null;
        if (program is null)
        {
            throw new InvalidDataException("The requested program does not identify exactly one verified SRC/DAT pair.");
        }

        var source = receipt.Payload.Files.Single(file =>
            string.Equals(file.RelativePath, program.SourceFile, StringComparison.OrdinalIgnoreCase));
        var data = receipt.Payload.Files.Single(file =>
            string.Equals(file.RelativePath, program.DataFile, StringComparison.OrdinalIgnoreCase));
        var candidateHash = receipt.Payload.CandidateId.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? receipt.Payload.CandidateId[7..]
            : throw new InvalidDataException("Raw KRL candidate ID is not SHA-256 based.");

        return new NativeKssCandidateSubmissionPlan
        {
            CandidateId = receipt.Payload.CandidateId,
            CandidateRoot = candidateRoot,
            CandidateReceiptPath = receiptPath,
            ProgramRelativeStem = requestedStem,
            ProgramName = program.ProgramName,
            Source = source,
            Data = data,
            MaximumStartCommands = request.MaximumStartCommands,
            ControllerTransactionRoot = $@"KRC:\R1\Program\KLAB_CAND_{candidateHash[..16].ToUpperInvariant()}"
        };
    }

    private static string NormalizeProgramStem(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Replace('\\', '/').Trim();
        if (!ProgramStemPattern.IsMatch(normalized)
            || !ValidationPackageIdentity.IsSafeRelativePath(normalized + ".src"))
        {
            throw new ArgumentException("Program stem must be a safe portable relative path without an extension.", nameof(value));
        }

        return normalized;
    }
}
