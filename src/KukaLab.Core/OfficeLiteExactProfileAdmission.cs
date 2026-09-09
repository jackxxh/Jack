using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KukaLab.Core;

public sealed record OfficeLiteExactProfileAdmissionRequest
{
    public required string AcceptanceReceiptPath { get; init; }

    public string ExpectedProjectName { get; init; } =
        OfficeLiteExactProfileAcceptanceContract.DefaultExpectedProjectName;
}

public sealed record OfficeLiteExactProfileAdmission
{
    public string AcceptanceReceiptPath { get; init; } = string.Empty;

    public string AcceptanceReceiptSha256 { get; init; } = string.Empty;

    public string AcceptancePayloadSha256 { get; init; } = string.Empty;

    public string AcceptanceReceiptId { get; init; } = string.Empty;

    public string ExpectedProjectName { get; init; } = string.Empty;

    public string RuntimeRobotIdentity { get; init; } = string.Empty;

    public string RuntimeKssVersion { get; init; } = string.Empty;

    public string ActiveMachineDataIdentity { get; init; } = string.Empty;

    public string ActiveCabinetKind { get; init; } = string.Empty;

    public bool Accepted { get; init; }
}

internal interface IOfficeLiteExactProfileAdmissionReceiptVerifier
{
    OfficeLiteExactProfileAcceptanceVerificationResult Verify(
        OfficeLiteExactProfileAcceptanceReceipt receipt);
}

internal interface IOfficeLiteExactProfileAdmissionGate
{
    OfficeLiteExactProfileAdmission Admit(OfficeLiteExactProfileAdmissionRequest request);
}

internal sealed class OfficeLiteExactProfileAdmissionReceiptVerifier
    : IOfficeLiteExactProfileAdmissionReceiptVerifier
{
    public OfficeLiteExactProfileAcceptanceVerificationResult Verify(
        OfficeLiteExactProfileAcceptanceReceipt receipt) =>
        OfficeLiteExactProfileAcceptanceReceiptVerifier.Verify(receipt);
}

public sealed class OfficeLiteExactProfileAdmissionGate : IOfficeLiteExactProfileAdmissionGate
{
    private static readonly string ProtectedLicensePath = Path.GetFullPath(
        @"C:\ProgramData\Visual Components\Visual Components License Server 2.0\lservrc.dat");

    private readonly IOfficeLiteExactProfileAdmissionReceiptVerifier _receiptVerifier;

    public OfficeLiteExactProfileAdmissionGate()
        : this(new OfficeLiteExactProfileAdmissionReceiptVerifier())
    {
    }

    internal OfficeLiteExactProfileAdmissionGate(
        IOfficeLiteExactProfileAdmissionReceiptVerifier receiptVerifier)
    {
        _receiptVerifier = receiptVerifier ?? throw new ArgumentNullException(nameof(receiptVerifier));
    }

    public OfficeLiteExactProfileAdmission Admit(OfficeLiteExactProfileAdmissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AcceptanceReceiptPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExpectedProjectName);

        var receiptPath = Path.GetFullPath(request.AcceptanceReceiptPath);
        if (string.Equals(receiptPath, ProtectedLicensePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                "The protected Visual Components license-control file cannot be used as Lab evidence.");
        }

        if (!string.Equals(Path.GetExtension(receiptPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Exact-profile admission requires a JSON acceptance receipt.");
        }

        if (!File.Exists(receiptPath))
        {
            throw new FileNotFoundException("Exact-profile acceptance receipt was not found.", receiptPath);
        }

        if ((File.GetAttributes(receiptPath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Exact-profile acceptance receipt may not cross a reparse boundary.");
        }

        var receiptBytes = File.ReadAllBytes(receiptPath);
        OfficeLiteExactProfileAcceptanceReceipt receipt;
        try
        {
            receipt = ReceiptSerialization.OfficeLiteExactProfileAcceptanceFromJson(
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                    .GetString(receiptBytes));
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
        {
            throw new InvalidDataException("Exact-profile acceptance receipt JSON is invalid.", exception);
        }

        OfficeLiteExactProfileAcceptanceVerificationResult verification;
        try
        {
            verification = _receiptVerifier.Verify(receipt);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidDataException
            or InvalidOperationException
            or JsonException
            or NullReferenceException)
        {
            throw new InvalidDataException(
                "Exact-profile acceptance receipt integrity/current-input verification failed safely.",
                exception);
        }

        if (!verification.Succeeded)
        {
            var detail = verification.Errors.Count == 0
                ? "the strict receipt verifier rejected it"
                : string.Join("; ", verification.Errors);
            throw new InvalidDataException(
                $"Exact-profile acceptance receipt integrity/current-input verification failed: {detail}");
        }

        var payload = receipt.Payload;
        var summary = payload.Summary;
        if (payload.TerminalClassification != EnvironmentTerminalClassification.Ready
            || !summary.Accepted)
        {
            throw new InvalidDataException(
                "The OfficeLite exact profile is not accepted; no stateful vendor operation may start.");
        }

        if (!string.Equals(payload.ExpectedProjectName, request.ExpectedProjectName, StringComparison.Ordinal)
            || !string.Equals(summary.RuntimeCurrentProject, request.ExpectedProjectName, StringComparison.Ordinal)
            || !string.Equals(summary.RuntimeActiveProject, request.ExpectedProjectName, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The accepted OfficeLite project identity does not match the requested project.");
        }

        if (!payload.ReadOnlyComposition
            || payload.CredentialsUsed
            || payload.ControllerMutationPerformed
            || payload.NativeKssStatus != NativeKssStatus.NotRun
            || payload.InputVerificationErrors.Count != 0
            || !payload.EnvironmentReusable)
        {
            throw new InvalidDataException(
                "The exact-profile acceptance receipt does not preserve the required read-only, reusable admission boundary.");
        }

        return new OfficeLiteExactProfileAdmission
        {
            AcceptanceReceiptPath = receiptPath,
            AcceptanceReceiptSha256 = Convert.ToHexString(SHA256.HashData(receiptBytes)),
            AcceptancePayloadSha256 = receipt.PayloadSha256,
            AcceptanceReceiptId = payload.ReceiptId,
            ExpectedProjectName = request.ExpectedProjectName,
            RuntimeRobotIdentity = summary.RuntimeRobotIdentity,
            RuntimeKssVersion = summary.RuntimeKssVersion,
            ActiveMachineDataIdentity = summary.ActiveMachineDataIdentity,
            ActiveCabinetKind = summary.ActiveCabinetKind,
            Accepted = true
        };
    }

    public T RunAfterAdmission<T>(
        OfficeLiteExactProfileAdmissionRequest request,
        Func<OfficeLiteExactProfileAdmission, T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var admission = Admit(request);
        return operation(admission);
    }
}
