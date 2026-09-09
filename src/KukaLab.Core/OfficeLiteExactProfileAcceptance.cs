using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class OfficeLiteExactProfileAcceptanceContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.officelite-exact-profile-acceptance-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const string DefaultExpectedProjectName = "deployment.analysis-copy";
    public const double VendorRealRoundTripTolerance = 0.000001d;

    internal static readonly IReadOnlyList<string> RequiredUnsupportedGaps =
    [
        "This receipt accepts an isolated OfficeLite software profile only; it is not physical mastering, calibration, measured-load, safety or motion qualification.",
        "Runtime DeviceInfo identity is correlated with a freshly downloaded active-project baseline; neither source alone is sufficient for exact-profile acceptance.",
        "Technology-package differences are reported but are not accepted as installed, licensed or runtime-compatible unless separately qualified.",
        "Native KSS candidate execution and the exact KUKA.Sim online loop remain NotRun and require separate receipts."
    ];
}

public sealed record OfficeLiteExactProfileAcceptanceRequest
{
    public required OfficeLiteControllerProfileReadbackReceipt ProfileReadbackReceipt { get; init; }

    public required OfficeLiteActiveProjectDownloadReceipt ActiveProjectDownloadReceipt { get; init; }

    public required ControllerProjectBaselineReceipt ActiveControllerBaselineReceipt { get; init; }

    public required ControllerProjectBaselineReceipt TrustedControllerBaselineReceipt { get; init; }

    public string ExpectedProjectName { get; init; } = OfficeLiteExactProfileAcceptanceContract.DefaultExpectedProjectName;
}

public sealed record OfficeLiteExactProfileAcceptanceReceipt
{
    public string SchemaIdentity { get; init; } = OfficeLiteExactProfileAcceptanceContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = OfficeLiteExactProfileAcceptanceContract.ReceiptSchemaVersion;

    public required OfficeLiteExactProfileAcceptancePayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record OfficeLiteExactProfileAcceptancePayload
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

    public string ExpectedProjectName { get; init; } = string.Empty;

    public required OfficeLiteControllerProfileReadbackReceipt ProfileReadbackReceipt { get; init; }

    public string ProfileReadbackReceiptSha256 { get; init; } = string.Empty;

    public required OfficeLiteActiveProjectDownloadReceipt ActiveProjectDownloadReceipt { get; init; }

    public string ActiveProjectDownloadReceiptSha256 { get; init; } = string.Empty;

    public required ControllerProjectBaselineReceipt ActiveControllerBaselineReceipt { get; init; }

    public string ActiveControllerBaselineReceiptSha256 { get; init; } = string.Empty;

    public required ControllerProjectBaselineReceipt TrustedControllerBaselineReceipt { get; init; }

    public string TrustedControllerBaselineReceiptSha256 { get; init; } = string.Empty;

    public List<string> InputVerificationErrors { get; init; } = [];

    public OfficeLiteExactProfileAcceptanceSummary Summary { get; init; } = new();

    public bool ReadOnlyComposition { get; init; } = true;

    public bool CredentialsUsed { get; init; }

    public bool ControllerMutationPerformed { get; init; }

    public NativeKssStatus NativeKssStatus { get; init; } = NativeKssStatus.NotRun;

    public List<EnvironmentCheck> Checks { get; init; } = [];

    public List<string> SideEffects { get; init; } = [];

    public bool EnvironmentReusable { get; init; }

    public List<string> UnsupportedGaps { get; init; } = [];
}

public sealed record OfficeLiteExactProfileAcceptanceSummary
{
    public string RuntimeRobotIdentity { get; init; } = string.Empty;

    public string RuntimeKssVersion { get; init; } = string.Empty;

    public string RuntimeCurrentProject { get; init; } = string.Empty;

    public string RuntimeActiveProject { get; init; } = string.Empty;

    public string ActiveMachineDataIdentity { get; init; } = string.Empty;

    public string ActiveCabinetKind { get; init; } = string.Empty;

    public string ActiveControllerSoftwareFamily { get; init; } = string.Empty;

    public bool RuntimeRobotIdentityMatched { get; init; }

    public bool RuntimeKssMatched { get; init; }

    public bool ActiveProjectMatched { get; init; }

    public bool DownloadedProjectBound { get; init; }

    public bool MachineDataIdentityMatched { get; init; }

    public bool CabinetMatched { get; init; }

    public bool ControllerSoftwareFamilyMatched { get; init; }

    public bool AxisLimitsMatched { get; init; }

    public bool DetailedAxesMatched { get; init; }

    public bool ToolDataMatched { get; init; }

    public bool BaseDataMatched { get; init; }

    public bool LoadDataMatched { get; init; }

    public bool ExactKukaSimComponentMatched { get; init; }

    public List<string> MissingActiveModules { get; init; } = [];

    public List<string> AdditionalActiveModules { get; init; } = [];

    public bool Accepted { get; init; }
}

public sealed record OfficeLiteExactProfileAcceptanceOutcome(OfficeLiteExactProfileAcceptanceReceipt Receipt)
{
    public int ExitCode => Receipt.Payload.TerminalClassification == EnvironmentTerminalClassification.Ready ? 0 : 2;
}

internal interface IOfficeLiteExactProfileAcceptanceInputVerifier
{
    IReadOnlyList<string> Verify(OfficeLiteExactProfileAcceptanceRequest request);
}

internal sealed class OfficeLiteExactProfileAcceptanceInputVerifier : IOfficeLiteExactProfileAcceptanceInputVerifier
{
    public IReadOnlyList<string> Verify(OfficeLiteExactProfileAcceptanceRequest request)
    {
        var errors = new List<string>();
        if (!OfficeLiteControllerProfileReadbackReceiptVerifier.Verify(request.ProfileReadbackReceipt).Succeeded
            || request.ProfileReadbackReceipt.Payload.TerminalClassification != EnvironmentTerminalClassification.Ready)
        {
            errors.Add("profile readback receipt is not current, Ready and integrity-valid");
        }

        if (!OfficeLiteActiveProjectDownloadReceiptVerifier.Verify(request.ActiveProjectDownloadReceipt).Succeeded
            || request.ActiveProjectDownloadReceipt.Payload.TerminalClassification != EnvironmentTerminalClassification.Ready)
        {
            errors.Add("active-project download receipt is not current, Ready and integrity-valid");
        }

        if (!ControllerProjectBaselineReceiptVerifier.Verify(request.ActiveControllerBaselineReceipt).Succeeded
            || request.ActiveControllerBaselineReceipt.Payload.TerminalClassification != EnvironmentTerminalClassification.Ready
            || request.ActiveControllerBaselineReceipt.Payload.Baseline is null)
        {
            errors.Add("active controller-project baseline receipt is not current, Ready and integrity-valid");
        }

        if (!ControllerProjectBaselineReceiptVerifier.Verify(request.TrustedControllerBaselineReceipt).Succeeded
            || request.TrustedControllerBaselineReceipt.Payload.TerminalClassification != EnvironmentTerminalClassification.Ready
            || request.TrustedControllerBaselineReceipt.Payload.Baseline is null)
        {
            errors.Add("trusted controller-project baseline receipt is not current, Ready and integrity-valid");
        }

        return errors;
    }
}

public sealed class OfficeLiteExactProfileAcceptanceRunner
{
    private readonly IOfficeLiteExactProfileAcceptanceInputVerifier _inputVerifier;
    private readonly TimeProvider _timeProvider;

    public OfficeLiteExactProfileAcceptanceRunner()
        : this(new OfficeLiteExactProfileAcceptanceInputVerifier(), TimeProvider.System)
    {
    }

    internal OfficeLiteExactProfileAcceptanceRunner(
        IOfficeLiteExactProfileAcceptanceInputVerifier inputVerifier,
        TimeProvider? timeProvider = null)
    {
        _inputVerifier = inputVerifier;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public OfficeLiteExactProfileAcceptanceOutcome Run(
        OfficeLiteExactProfileAcceptanceRequest request,
        string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(attemptId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExpectedProjectName);
        var started = _timeProvider.GetUtcNow();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var inputErrors = _inputVerifier.Verify(request).Order(StringComparer.Ordinal).ToList();
        var summary = inputErrors.Count == 0
            ? DeriveSummary(request)
            : new OfficeLiteExactProfileAcceptanceSummary();
        var checks = BuildChecks(inputErrors, summary);
        stopwatch.Stop();
        var terminal = inputErrors.Count == 0 && summary.Accepted
            ? EnvironmentTerminalClassification.Ready
            : EnvironmentTerminalClassification.Failed;
        var payload = new OfficeLiteExactProfileAcceptancePayload
        {
            ReceiptId = $"officelite-exact-profile-acceptance-{attemptId}",
            AttemptId = attemptId,
            CoreAssemblySha256 = ComputeFileSha256(typeof(OfficeLiteExactProfileAcceptanceRunner).Assembly.Location),
            Runtime = new RuntimeEnvironment
            {
                OsDescription = RuntimeInformation.OSDescription,
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
            },
            StartedAtUtc = started,
            CompletedAtUtc = _timeProvider.GetUtcNow(),
            DurationMilliseconds = Math.Max(0, stopwatch.ElapsedMilliseconds),
            TerminalClassification = terminal,
            ExpectedProjectName = request.ExpectedProjectName,
            ProfileReadbackReceipt = request.ProfileReadbackReceipt,
            ProfileReadbackReceiptSha256 = ReceiptSerialization.ComputeCanonicalSha256(request.ProfileReadbackReceipt),
            ActiveProjectDownloadReceipt = request.ActiveProjectDownloadReceipt,
            ActiveProjectDownloadReceiptSha256 = ReceiptSerialization.ComputeCanonicalSha256(request.ActiveProjectDownloadReceipt),
            ActiveControllerBaselineReceipt = request.ActiveControllerBaselineReceipt,
            ActiveControllerBaselineReceiptSha256 = ReceiptSerialization.ComputeCanonicalSha256(request.ActiveControllerBaselineReceipt),
            TrustedControllerBaselineReceipt = request.TrustedControllerBaselineReceipt,
            TrustedControllerBaselineReceiptSha256 = ReceiptSerialization.ComputeCanonicalSha256(request.TrustedControllerBaselineReceipt),
            InputVerificationErrors = inputErrors,
            Summary = summary,
            Checks = checks,
            EnvironmentReusable = inputErrors.Count == 0
                && request.ProfileReadbackReceipt.Payload.EnvironmentReusable
                && request.ActiveProjectDownloadReceipt.Payload.EnvironmentReusable
                && request.ActiveControllerBaselineReceipt.Payload.EnvironmentReusable
                && request.TrustedControllerBaselineReceipt.Payload.EnvironmentReusable,
            UnsupportedGaps = OfficeLiteExactProfileAcceptanceContract.RequiredUnsupportedGaps.ToList()
        };
        var receipt = new OfficeLiteExactProfileAcceptanceReceipt
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        return new OfficeLiteExactProfileAcceptanceOutcome(receipt);
    }

    internal static OfficeLiteExactProfileAcceptanceSummary DeriveSummary(
        OfficeLiteExactProfileAcceptanceRequest request)
    {
        var readback = request.ProfileReadbackReceipt.Payload.Profile;
        var download = request.ActiveProjectDownloadReceipt.Payload;
        var activeReceipt = request.ActiveControllerBaselineReceipt.Payload;
        var trustedReceipt = request.TrustedControllerBaselineReceipt.Payload;
        var active = activeReceipt.Baseline!;
        var trusted = trustedReceipt.Baseline!;
        var activeProfile = active.ControllerProfile;
        var trustedProfile = trusted.ControllerProfile;
        var runtimeRobotMatched = RuntimeRobotMatchesExactTarget(readback.RobotType);
        var runtimeKssMatched = Regex.IsMatch(
            readback.KssVersion ?? string.Empty,
            "(?<!\\d)8\\.7\\.8(?:\\.|$)",
            RegexOptions.CultureInvariant);
        var activeProjectMatched = StringEquals(readback.CurrentProjectName, request.ExpectedProjectName)
            && StringEquals(readback.ActiveProject, request.ExpectedProjectName)
            && StringEquals(download.ActiveProject, request.ExpectedProjectName);
        var downloadedProjectBound = SameFileIdentity(download.DownloadedProject, activeReceipt.OriginalProject);
        var machineIdentityMatched = activeProfile.RobotIdentityConsistent
            && StringEquals(activeProfile.TrafoName, NativeKssCandidateSubmissionContract.MachineDataIdentity)
            && StringEquals(activeProfile.ModelName, NativeKssCandidateSubmissionContract.MachineDataIdentity)
            && StringEquals(activeProfile.TrafoName, trustedProfile.TrafoName)
            && StringEquals(activeProfile.ModelName, trustedProfile.ModelName);
        var cabinetMatched = StringEquals(activeProfile.CabinetKind, NativeKssCandidateSubmissionContract.CabinetKind)
            && StringEquals(activeProfile.CabinetKind, trustedProfile.CabinetKind);
        var softwareMatched = StringEquals(activeProfile.ControllerSoftwareFamily, trustedProfile.ControllerSoftwareFamily)
            && Regex.IsMatch(activeProfile.ControllerSoftwareFamily, "8\\.7", RegexOptions.CultureInvariant);
        var axisLimitsMatched = activeProfile.AxisMadaFileCount == 6
            && trustedProfile.AxisMadaFileCount == 6
            && SameCanonical(activeProfile.AxisLimits, trustedProfile.AxisLimits);
        var detailedAxesMatched = active.Axes.Count == 6
            && trusted.Axes.Count == 6
            && SameCanonical(active.Axes, trusted.Axes);
        var toolMatched = activeProfile.ToolDataCount == 16
            && trustedProfile.ToolDataCount == 16
            && SameFrames(activeProfile.ToolData, trustedProfile.ToolData);
        var baseMatched = activeProfile.BaseDataCount == 32
            && trustedProfile.BaseDataCount == 32
            && SameFrames(activeProfile.BaseData, trustedProfile.BaseData);
        var loadMatched = activeProfile.LoadDataCount == 16
            && trustedProfile.LoadDataCount == 16
            && SameLoads(activeProfile.LoadData, trustedProfile.LoadData);
        var componentMatched = active.KukaSimComponentComparison.ExactC01NameMatch
            && active.KukaSimComponentComparison.ExactComponentPreferred
            && StringEquals(
                active.KukaSimComponentComparison.ExactComponentName,
                NativeKssCandidateSubmissionContract.RobotType);
        var activeModules = active.Modules.Select(ModuleIdentity).ToHashSet(StringComparer.Ordinal);
        var trustedModules = trusted.Modules.Select(ModuleIdentity).ToHashSet(StringComparer.Ordinal);
        var missingModules = trustedModules.Except(activeModules, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var additionalModules = activeModules.Except(trustedModules, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var accepted = runtimeRobotMatched
            && runtimeKssMatched
            && activeProjectMatched
            && downloadedProjectBound
            && machineIdentityMatched
            && cabinetMatched
            && softwareMatched
            && axisLimitsMatched
            && detailedAxesMatched
            && toolMatched
            && baseMatched
            && loadMatched
            && componentMatched;
        return new OfficeLiteExactProfileAcceptanceSummary
        {
            RuntimeRobotIdentity = readback.RobotType ?? string.Empty,
            RuntimeKssVersion = readback.KssVersion ?? string.Empty,
            RuntimeCurrentProject = readback.CurrentProjectName ?? string.Empty,
            RuntimeActiveProject = readback.ActiveProject ?? string.Empty,
            ActiveMachineDataIdentity = activeProfile.ModelName,
            ActiveCabinetKind = activeProfile.CabinetKind,
            ActiveControllerSoftwareFamily = activeProfile.ControllerSoftwareFamily,
            RuntimeRobotIdentityMatched = runtimeRobotMatched,
            RuntimeKssMatched = runtimeKssMatched,
            ActiveProjectMatched = activeProjectMatched,
            DownloadedProjectBound = downloadedProjectBound,
            MachineDataIdentityMatched = machineIdentityMatched,
            CabinetMatched = cabinetMatched,
            ControllerSoftwareFamilyMatched = softwareMatched,
            AxisLimitsMatched = axisLimitsMatched,
            DetailedAxesMatched = detailedAxesMatched,
            ToolDataMatched = toolMatched,
            BaseDataMatched = baseMatched,
            LoadDataMatched = loadMatched,
            ExactKukaSimComponentMatched = componentMatched,
            MissingActiveModules = missingModules,
            AdditionalActiveModules = additionalModules,
            Accepted = accepted
        };
    }

    internal static List<EnvironmentCheck> BuildChecks(
        IReadOnlyList<string> inputErrors,
        OfficeLiteExactProfileAcceptanceSummary summary)
    {
        if (inputErrors.Count > 0)
        {
            return
            [
                new EnvironmentCheck
                {
                    Id = "input-receipts",
                    Status = EnvironmentCheckStatus.Failed,
                    Detail = string.Join("; ", inputErrors)
                }
            ];
        }

        return
        [
            Check("input-receipts", true, "All four input receipts are current, Ready and integrity-valid."),
            Check("runtime-robot-identity", summary.RuntimeRobotIdentityMatched, "Runtime robot identity matches the exact KR210 C01 display/MADA identity."),
            Check("runtime-kss", summary.RuntimeKssMatched, "Runtime KSS identity is the required 8.7.8 patch line."),
            Check("active-project", summary.ActiveProjectMatched, "Runtime current/active and downloaded active project identities match the expected project."),
            Check("download-baseline-binding", summary.DownloadedProjectBound, "The active-project download is byte-bound to the analyzed active baseline."),
            Check("machine-data-identity", summary.MachineDataIdentityMatched, "Active MADA identity matches the trusted exact controller-project baseline."),
            Check("cabinet", summary.CabinetMatched, "Active cabinet class matches trusted KRC5 evidence."),
            Check("controller-software", summary.ControllerSoftwareFamilyMatched, "Active controller software family matches the trusted 8.7 baseline."),
            Check("axis-limits", summary.AxisLimitsMatched, "All six active software axis limits match the trusted baseline."),
            Check("detailed-axis-data", summary.DetailedAxesMatched, "All six active drive/MADA axis records match the trusted baseline."),
            Check("tool-data", summary.ToolDataMatched, "All 16 active Tool values match the trusted baseline."),
            Check("base-data", summary.BaseDataMatched, "All 32 active Base values match the trusted baseline."),
            Check("load-data", summary.LoadDataMatched, "All 16 active Load values match the trusted baseline."),
            Check("kukasim-component", summary.ExactKukaSimComponentMatched, "The active baseline selects the exact KR 210 R2700-2 C01 KUKA.Sim component."),
            Check("exact-profile-accepted", summary.Accepted, "Every required exact OfficeLite profile field matches.")
        ];
    }

    private static EnvironmentCheck Check(string id, bool passed, string detail) => new()
    {
        Id = id,
        Status = passed ? EnvironmentCheckStatus.Passed : EnvironmentCheckStatus.Failed,
        Detail = detail
    };

    private static bool RuntimeRobotMatchesExactTarget(string? observed)
    {
        var normalized = NormalizeRobotIdentity(observed);
        return normalized == NormalizeRobotIdentity(NativeKssCandidateSubmissionContract.RobotType)
            || normalized == NormalizeRobotIdentity(NativeKssCandidateSubmissionContract.MachineDataIdentity);
    }

    private static string NormalizeRobotIdentity(string? value)
    {
        var normalized = Regex.Replace(
            value ?? string.Empty,
            "[^A-Z0-9]",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).ToUpperInvariant();
        return normalized.EndsWith("FLR", StringComparison.Ordinal)
            ? normalized[..^3]
            : normalized;
    }

    private static bool SameFileIdentity(EnvironmentFileObservation left, EnvironmentFileObservation right) =>
        left.Exists
        && right.Exists
        && left.Bytes == right.Bytes
        && left.Bytes > 0
        && left.Sha256?.Length == 64
        && string.Equals(left.Sha256, right.Sha256, StringComparison.OrdinalIgnoreCase);

    private static bool SameCanonical<T>(T left, T right) =>
        string.Equals(
            ReceiptSerialization.ComputeCanonicalSha256(left!),
            ReceiptSerialization.ComputeCanonicalSha256(right!),
            StringComparison.OrdinalIgnoreCase);

    private static bool SameFrames(
        IReadOnlyList<WorkVisualFrameData> left,
        IReadOnlyList<WorkVisualFrameData> right) =>
        left.Count == right.Count
        && left.Zip(right).All(pair => SameFrame(pair.First, pair.Second));

    private static bool SameFrame(WorkVisualFrameData left, WorkVisualFrameData right) =>
        left.Index == right.Index
        && WithinVendorRealTolerance(left.X, right.X)
        && WithinVendorRealTolerance(left.Y, right.Y)
        && WithinVendorRealTolerance(left.Z, right.Z)
        && WithinVendorRealTolerance(left.A, right.A)
        && WithinVendorRealTolerance(left.B, right.B)
        && WithinVendorRealTolerance(left.C, right.C);

    private static bool SameLoads(
        IReadOnlyList<WorkVisualLoadData> left,
        IReadOnlyList<WorkVisualLoadData> right) =>
        left.Count == right.Count
        && left.Zip(right).All(pair =>
            pair.First.Index == pair.Second.Index
            && WithinVendorRealTolerance(pair.First.Mass, pair.Second.Mass)
            && SameFrame(pair.First.CenterOfMass, pair.Second.CenterOfMass)
            && WithinVendorRealTolerance(pair.First.InertiaX, pair.Second.InertiaX)
            && WithinVendorRealTolerance(pair.First.InertiaY, pair.Second.InertiaY)
            && WithinVendorRealTolerance(pair.First.InertiaZ, pair.Second.InertiaZ));

    private static bool WithinVendorRealTolerance(double left, double right) =>
        double.IsFinite(left)
        && double.IsFinite(right)
        && Math.Abs(left - right) <= OfficeLiteExactProfileAcceptanceContract.VendorRealRoundTripTolerance;

    private static string ModuleIdentity(ControllerModuleBaseline module) =>
        $"{module.Name}|{module.DisplayName}|{module.Active}|{module.ConfigurationFile}";

    private static bool StringEquals(string? left, string? right) =>
        string.Equals(left, right, StringComparison.Ordinal);

    private static void ValidateId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!Regex.IsMatch(id, "^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("Attempt ID must be a safe token of at most 128 characters.", nameof(id));
        }
    }

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}

public sealed record OfficeLiteExactProfileAcceptanceVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}

public static class OfficeLiteExactProfileAcceptanceReceiptVerifier
{
    public static OfficeLiteExactProfileAcceptanceVerificationResult Verify(
        OfficeLiteExactProfileAcceptanceReceipt receipt) => Verify(receipt, reverifyInputs: true);

    internal static OfficeLiteExactProfileAcceptanceVerificationResult Verify(
        OfficeLiteExactProfileAcceptanceReceipt receipt,
        bool reverifyInputs)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        var payload = receipt.Payload;
        if (receipt.SchemaIdentity != OfficeLiteExactProfileAcceptanceContract.ReceiptSchemaIdentity
            || receipt.SchemaVersion != OfficeLiteExactProfileAcceptanceContract.ReceiptSchemaVersion)
        {
            errors.Add("schema identity/version is invalid");
        }

        if (!string.Equals(
                receipt.PayloadSha256,
                ReceiptSerialization.ComputeCanonicalSha256(payload),
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("payloadSha256 does not match the canonical payload");
        }

        VerifyInputHash(payload.ProfileReadbackReceipt, payload.ProfileReadbackReceiptSha256, "profile readback", errors);
        VerifyInputHash(payload.ActiveProjectDownloadReceipt, payload.ActiveProjectDownloadReceiptSha256, "active-project download", errors);
        VerifyInputHash(payload.ActiveControllerBaselineReceipt, payload.ActiveControllerBaselineReceiptSha256, "active baseline", errors);
        VerifyInputHash(payload.TrustedControllerBaselineReceipt, payload.TrustedControllerBaselineReceiptSha256, "trusted baseline", errors);

        var request = new OfficeLiteExactProfileAcceptanceRequest
        {
            ProfileReadbackReceipt = payload.ProfileReadbackReceipt,
            ActiveProjectDownloadReceipt = payload.ActiveProjectDownloadReceipt,
            ActiveControllerBaselineReceipt = payload.ActiveControllerBaselineReceipt,
            TrustedControllerBaselineReceipt = payload.TrustedControllerBaselineReceipt,
            ExpectedProjectName = payload.ExpectedProjectName
        };
        var currentInputErrors = reverifyInputs
            ? new OfficeLiteExactProfileAcceptanceInputVerifier().Verify(request).Order(StringComparer.Ordinal).ToList()
            : payload.InputVerificationErrors;
        if (!payload.InputVerificationErrors.SequenceEqual(currentInputErrors, StringComparer.Ordinal))
        {
            errors.Add("input verification errors do not match current receipt evidence");
        }

        var derived = currentInputErrors.Count == 0
            ? OfficeLiteExactProfileAcceptanceRunner.DeriveSummary(request)
            : new OfficeLiteExactProfileAcceptanceSummary();
        if (!SameCanonical(payload.Summary, derived))
        {
            errors.Add("summary does not match the four bound input receipts");
        }

        var expectedTerminal = currentInputErrors.Count == 0 && derived.Accepted
            ? EnvironmentTerminalClassification.Ready
            : EnvironmentTerminalClassification.Failed;
        if (payload.TerminalClassification != expectedTerminal)
        {
            errors.Add("terminal classification does not match exact-profile acceptance");
        }

        if (!payload.ReadOnlyComposition
            || payload.CredentialsUsed
            || payload.ControllerMutationPerformed
            || payload.NativeKssStatus != NativeKssStatus.NotRun
            || payload.SideEffects.Count > 1
            || payload.SideEffects.Any(sideEffect =>
                !sideEffect.StartsWith("CreateNewReceiptFile:", StringComparison.Ordinal)))
        {
            errors.Add("read-only composition safety assertions are invalid");
        }

        if (!OfficeLiteExactProfileAcceptanceContract.RequiredUnsupportedGaps.SequenceEqual(
                payload.UnsupportedGaps,
                StringComparer.Ordinal))
        {
            errors.Add("unsupportedGaps do not match the contract");
        }

        return new OfficeLiteExactProfileAcceptanceVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }

    private static void VerifyInputHash<T>(T input, string expected, string name, List<string> errors)
    {
        if (!string.Equals(
                expected,
                ReceiptSerialization.ComputeCanonicalSha256(input!),
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{name} receipt hash binding is invalid");
        }
    }

    private static bool SameCanonical<T>(T left, T right) =>
        string.Equals(
            ReceiptSerialization.ComputeCanonicalSha256(left!),
            ReceiptSerialization.ComputeCanonicalSha256(right!),
            StringComparison.OrdinalIgnoreCase);
}

public static class OfficeLiteExactProfileAcceptanceReceiptWriter
{
    public static string WriteNew(string outputPath, OfficeLiteExactProfileAcceptanceReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!OfficeLiteExactProfileAcceptanceReceiptVerifier.Verify(receipt).Succeeded)
        {
            throw new InvalidOperationException("Cannot write an invalid exact-profile acceptance receipt.");
        }

        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(ReceiptSerialization.ToJson(receipt));
        writer.Flush();
        stream.Flush(flushToDisk: true);
        return fullPath;
    }
}
