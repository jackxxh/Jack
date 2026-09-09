using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class OfflineLoopComparisonContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.offline-loop-comparison-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const string ValidationStatus = "NormalizedOfflineLoopComparisonValidated";
    public const string ValidFixtureId = "minimal-ptp-lin-valid-v1";
    public const string InvalidFixtureId = "minimal-missing-target-invalid-v1";
    public const string ExactRobotModel = "#KR210R2700_2 C01 FLR";
    public const string ExactControllerModel = "KRC5";
    public const string OfficeLiteRobotModel = "#KR3R540 C4SR";
    public const string OfficeLiteControllerModel = "KRC5_MICRO";
    public const string KssVersion = "8.7.8 B671";
    public const double MaximumFinalTcpDeviationMillimeters = 1.0;
    public const int MinimumKukaSimSampleCount = 2;

    public static readonly IReadOnlyDictionary<string, string> ExpectedReceiptSha256 =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["item9a-valid"] = "71B6180439E853883BA45A30B6BFF2CF7D04C1EC91C782278BBDA5488BC83C1F",
            ["item9a-invalid"] = "229FF0631A0A6B70652E363E535915F2E7C2F2C79A00C61D71A8BB3A643ED682",
            ["item9b-native-kss"] = "9DF2705BF0C6D6EDFF27CA4B6AC92912429C5DB1ACCC8D5C57020BB0F3634AD1",
            ["item9c-kukasim"] = "0C2FD30CB8FA47E30CBD92A8212969B5632EADAADE489F78CA85C57A813F1157"
        };

    public static readonly IReadOnlyDictionary<string, string> ExpectedManifestSha256 =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["item9a-valid"] = "5B1CF3BD638B329D61657AF91EC6F11585142E1118A76B73919EB89CA3ED98E3",
            ["item9a-invalid"] = "DBA29CFFA58217C72402E86667DDB6F4E204F45A28CB51A3D33C728A91CB612C"
        };

    public static readonly IReadOnlyDictionary<string, string> ExpectedInputSchemaIdentity =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["item9a-valid"] = FixtureContract.ReceiptSchemaIdentity,
            ["item9a-invalid"] = FixtureContract.ReceiptSchemaIdentity,
            ["item9b-native-kss"] = OfficeLiteNativeKssExecutionContract.ReceiptSchemaIdentity,
            ["item9c-kukasim"] = KukaSimIntegratedValidationContract.ReceiptSchemaIdentity
        };

    internal static readonly IReadOnlyList<string> RequiredCheckIds =
    [
        "profile-boundary",
        "positive-target-order",
        "positive-motion-types",
        "native-kss-bounded-execution",
        "kukasim-axis-evidence",
        "kukasim-final-tcp",
        "negative-native-diagnostic",
        "negative-kukasim-rejection"
    ];

    internal static readonly IReadOnlyList<string> RequiredUnsupportedGaps =
    [
        "The OfficeLite positive execution uses #KR3R540 C4SR and cannot be numerically compared with exact-C01 KUKA.Sim motion.",
        "Exact-C01 KUKA.Sim Integrated evidence is not native KSS, KUKA RCS or an OfficeLite/VRC composition result.",
        "Orientation, S/T, collision and qualified cycle-time comparison are NotAvailable from the accepted item-9 inputs.",
        "No Rhino execution, vendor process, network traffic, physical-controller contact, safety qualification or physical motion occurs in this file-only comparison."
    ];
}

public enum NormalizedEvidenceStatus
{
    Matched,
    RejectedAsExpected,
    NotComparableProfile,
    NotAvailable,
    Unknown
}

public sealed record OfflineLoopComparisonRequest
{
    public required string ValidFixtureReceiptPath { get; init; }
    public required string InvalidFixtureReceiptPath { get; init; }
    public required string NativeKssReceiptPath { get; init; }
    public required string KukaSimReceiptPath { get; init; }
}

public sealed record OfflineLoopComparisonReceipt
{
    public string SchemaIdentity { get; init; } = OfflineLoopComparisonContract.ReceiptSchemaIdentity;
    public int SchemaVersion { get; init; } = OfflineLoopComparisonContract.ReceiptSchemaVersion;
    public required OfflineLoopComparisonPayload Payload { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record OfflineLoopComparisonPayload
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
    public string ValidationStatus { get; init; } = string.Empty;
    public List<OfflineLoopInputReceipt> Inputs { get; init; } = [];
    public ComparisonProfile DeclaredTarget { get; init; } = new();
    public ComparisonProfile OfficeLiteProfile { get; init; } = new();
    public ComparisonProfile KukaSimProfile { get; init; } = new();
    public OfflineLoopPositiveComparison Positive { get; init; } = new();
    public OfflineLoopNegativeComparison Negative { get; init; } = new();
    public bool VendorSoftwareStarted { get; init; }
    public bool NetworkTrafficSent { get; init; }
    public bool PhysicalControllerContacted { get; init; }
    public bool RhinoAccessed { get; init; }
    public List<EnvironmentCheck> Checks { get; init; } = [];
    public List<string> SideEffects { get; init; } = [];
    public bool EnvironmentReusable { get; init; }
    public List<string> UnsupportedGaps { get; init; } = [];
}

public sealed record OfflineLoopInputReceipt
{
    public string Role { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string SchemaIdentity { get; init; } = string.Empty;
    public string ReceiptId { get; init; } = string.Empty;
    public string PayloadSha256 { get; init; } = string.Empty;
    public string ExpectedReceiptSha256 { get; init; } = string.Empty;
    public string ActualReceiptSha256 { get; init; } = string.Empty;
}

public sealed record ComparisonProfile
{
    public string ProfileId { get; init; } = string.Empty;
    public string RobotModel { get; init; } = string.Empty;
    public string ControllerModel { get; init; } = string.Empty;
    public string KssVersion { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
}

public sealed record NormalizedMotionComparison
{
    public int Sequence { get; init; }
    public string OperationId { get; init; } = string.Empty;
    public string TargetId { get; init; } = string.Empty;
    public string MotionType { get; init; } = string.Empty;
    public string KrlSymbol { get; init; } = string.Empty;
    public string KrlFile { get; init; } = string.Empty;
    public int KrlLine { get; init; }
    public string KukaSimTarget { get; init; } = string.Empty;
    public string KukaSimMotionType { get; init; } = string.Empty;
    public NormalizedEvidenceStatus KukaSimStatus { get; init; } = NormalizedEvidenceStatus.Unknown;
    public NormalizedEvidenceStatus NativeKssStatus { get; init; } = NormalizedEvidenceStatus.Unknown;
}

public sealed record OfflineLoopPositiveComparison
{
    public string FixtureId { get; init; } = string.Empty;
    public List<NormalizedMotionComparison> Motions { get; init; } = [];
    public NormalizedEvidenceStatus TargetOrderStatus { get; init; } = NormalizedEvidenceStatus.Unknown;
    public NormalizedEvidenceStatus MotionTypeStatus { get; init; } = NormalizedEvidenceStatus.Unknown;
    public NormalizedEvidenceStatus NativeKssExecutionStatus { get; init; } = NormalizedEvidenceStatus.Unknown;
    public NormalizedEvidenceStatus ExactC01NativeKssTrajectoryStatus { get; init; } = NormalizedEvidenceStatus.Unknown;
    public string NativeKssMode { get; init; } = string.Empty;
    public string NativeKssFinalState { get; init; } = string.Empty;
    public double OfficeLiteRelativeXMillimeters { get; init; }
    public string KukaSimMode { get; init; } = string.Empty;
    public string KukaSimFinalState { get; init; } = string.Empty;
    public int KukaSimSampleCount { get; init; }
    public double KukaSimFinalX { get; init; }
    public double KukaSimFinalY { get; init; }
    public double KukaSimFinalZ { get; init; }
    public double KukaSimFinalTcpDeviationMillimeters { get; init; }
    public NormalizedEvidenceStatus AxisEvidenceStatus { get; init; } = NormalizedEvidenceStatus.Unknown;
    public NormalizedEvidenceStatus OrientationEvidenceStatus { get; init; } = NormalizedEvidenceStatus.Unknown;
    public NormalizedEvidenceStatus StatusTurnEvidenceStatus { get; init; } = NormalizedEvidenceStatus.Unknown;
    public NormalizedEvidenceStatus CollisionEvidenceStatus { get; init; } = NormalizedEvidenceStatus.Unknown;
    public NormalizedEvidenceStatus CycleTimeEvidenceStatus { get; init; } = NormalizedEvidenceStatus.Unknown;
}

public sealed record OfflineLoopNegativeComparison
{
    public string FixtureId { get; init; } = string.Empty;
    public string MissingSymbol { get; init; } = string.Empty;
    public string DeclaredFile { get; init; } = string.Empty;
    public int DeclaredSourceLine { get; init; }
    public int NativeKssMessageCode { get; init; }
    public string NativeKssFile { get; init; } = string.Empty;
    public int NativeKssLine { get; init; }
    public int NativeKssColumn { get; init; }
    public NormalizedEvidenceStatus NativeKssDiagnosticStatus { get; init; } = NormalizedEvidenceStatus.Unknown;
    public string KukaSimMessage { get; init; } = string.Empty;
    public int KukaSimCompletedMotionCount { get; init; }
    public NormalizedEvidenceStatus KukaSimDiagnosticStatus { get; init; } = NormalizedEvidenceStatus.Unknown;
    public NormalizedEvidenceStatus RejectionBeforeMotionStatus { get; init; } = NormalizedEvidenceStatus.Unknown;
    public NormalizedEvidenceStatus KukaSimFileLineStatus { get; init; } = NormalizedEvidenceStatus.Unknown;
}

public sealed record OfflineLoopComparisonOutcome(OfflineLoopComparisonReceipt Receipt)
{
    public bool Succeeded => Receipt.Payload.TerminalClassification == EnvironmentTerminalClassification.Ready;
    public int ExitCode => Succeeded ? 0 : 2;
}

internal sealed record OfflineLoopVerifiedInputs(
    FixtureIntegrityReceipt ValidFixtureReceipt,
    FixtureIntegrityReceipt InvalidFixtureReceipt,
    FixtureManifest ValidManifest,
    FixtureManifest InvalidManifest,
    OfficeLiteNativeKssExecutionReceipt NativeKssReceipt,
    KukaSimIntegratedValidationReceipt KukaSimReceipt,
    List<OfflineLoopInputReceipt> InputReferences);

public sealed partial class OfflineLoopComparisonRunner
{
    private readonly TimeProvider _timeProvider;

    public OfflineLoopComparisonRunner(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public OfflineLoopComparisonOutcome Run(OfflineLoopComparisonRequest request, string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptId);
        if (!AttemptIdPattern().IsMatch(attemptId))
        {
            throw new ArgumentException("Attempt ID must match [A-Za-z0-9][A-Za-z0-9._-]{2,127}.", nameof(attemptId));
        }

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var inputs = LoadVerifiedInputs(request);
        var (positive, negative, profiles, checks) = BuildNormalizedComparison(inputs);
        var failedChecks = checks.Where(check => check.Status != EnvironmentCheckStatus.Passed).ToList();
        if (failedChecks.Count != 0)
        {
            throw new InvalidOperationException(
                "Offline-loop comparison failed: " + string.Join("; ", failedChecks.Select(check => $"{check.Id}: {check.Detail}")));
        }

        stopwatch.Stop();
        var completedAt = _timeProvider.GetUtcNow();
        var payload = new OfflineLoopComparisonPayload
        {
            ReceiptId = $"offline-loop-comparison-{attemptId}",
            AttemptId = attemptId,
            CoreAssemblySha256 = ComputeFileSha256(typeof(OfflineLoopComparisonRunner).Assembly.Location),
            Runtime = new RuntimeEnvironment
            {
                OsDescription = RuntimeInformation.OSDescription,
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
            },
            StartedAtUtc = startedAt,
            CompletedAtUtc = completedAt,
            DurationMilliseconds = Math.Max(0, stopwatch.ElapsedMilliseconds),
            TerminalClassification = EnvironmentTerminalClassification.Ready,
            ValidationStatus = OfflineLoopComparisonContract.ValidationStatus,
            Inputs = inputs.InputReferences,
            DeclaredTarget = profiles.DeclaredTarget,
            OfficeLiteProfile = profiles.OfficeLiteProfile,
            KukaSimProfile = profiles.KukaSimProfile,
            Positive = positive,
            Negative = negative,
            Checks = checks,
            EnvironmentReusable = true,
            UnsupportedGaps = OfflineLoopComparisonContract.RequiredUnsupportedGaps.ToList()
        };
        var receipt = new OfflineLoopComparisonReceipt
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        return new OfflineLoopComparisonOutcome(receipt);
    }

    internal static OfflineLoopVerifiedInputs LoadVerifiedInputs(OfflineLoopComparisonRequest request)
    {
        var valid = ReadFixtureReceipt("item9a-valid", request.ValidFixtureReceiptPath);
        var invalid = ReadFixtureReceipt("item9a-invalid", request.InvalidFixtureReceiptPath);
        var native = ReadNativeKssReceipt(request.NativeKssReceiptPath);
        var sim = ReadKukaSimReceipt(request.KukaSimReceiptPath);

        var validManifest = ReadAndReverifyManifest("item9a-valid", valid.Receipt);
        var invalidManifest = ReadAndReverifyManifest("item9a-invalid", invalid.Receipt);
        return new OfflineLoopVerifiedInputs(
            valid.Receipt,
            invalid.Receipt,
            validManifest,
            invalidManifest,
            native.Receipt,
            sim.Receipt,
            [valid.Reference, invalid.Reference, native.Reference, sim.Reference]);
    }

    internal static (OfflineLoopPositiveComparison Positive, OfflineLoopNegativeComparison Negative,
        (ComparisonProfile DeclaredTarget, ComparisonProfile OfficeLiteProfile, ComparisonProfile KukaSimProfile) Profiles,
        List<EnvironmentCheck> Checks) BuildNormalizedComparison(OfflineLoopVerifiedInputs inputs)
    {
        var checks = new List<EnvironmentCheck>();
        var valid = inputs.ValidManifest;
        var invalid = inputs.InvalidManifest;
        var kss = inputs.NativeKssReceipt.Payload;
        var sim = inputs.KukaSimReceipt.Payload;
        var simPositive = sim.Positive.Result ?? new KukaSimIntegratedRawResult();
        var simNegative = sim.Negative.Result ?? new KukaSimIntegratedRawResult();

        AddCheck(
            checks,
            "profile-boundary",
            valid.ControllerTarget.RobotModel == OfflineLoopComparisonContract.ExactRobotModel
            && invalid.ControllerTarget.RobotModel == OfflineLoopComparisonContract.ExactRobotModel
            && kss.UnsupportedGaps.Any(gap => gap.Contains(OfflineLoopComparisonContract.OfficeLiteRobotModel, StringComparison.Ordinal))
            && simPositive.ComponentName == "KR 210 R2700-2 C01",
            "Declared and KUKA.Sim profiles are exact C01; OfficeLite remains explicitly KR3-scoped.");

        var correlations = valid.Correlations.OrderBy(item => item.Sequence).ToList();
        var simulatedMotions = simPositive.CompletedMotions.OrderBy(item => item.Sequence).ToList();
        var motionCountMatches = correlations.Count == simulatedMotions.Count;
        var motions = new List<NormalizedMotionComparison>();
        for (var index = 0; index < correlations.Count; index++)
        {
            var declared = correlations[index];
            var observed = index < simulatedMotions.Count ? simulatedMotions[index] : null;
            var matched = observed is not null
                && observed.Sequence == declared.Sequence
                && string.Equals(observed.Target, declared.KrlSymbol, StringComparison.Ordinal)
                && string.Equals(observed.MotionType, declared.MotionType, StringComparison.Ordinal);
            motions.Add(new NormalizedMotionComparison
            {
                Sequence = declared.Sequence ?? 0,
                OperationId = declared.OperationId,
                TargetId = declared.TargetId,
                MotionType = declared.MotionType,
                KrlSymbol = declared.KrlSymbol ?? string.Empty,
                KrlFile = declared.File,
                KrlLine = declared.Line,
                KukaSimTarget = observed?.Target ?? string.Empty,
                KukaSimMotionType = observed?.MotionType ?? string.Empty,
                KukaSimStatus = matched ? NormalizedEvidenceStatus.Matched : NormalizedEvidenceStatus.Unknown,
                NativeKssStatus = NormalizedEvidenceStatus.NotComparableProfile
            });
        }

        var targetOrderMatched = motionCountMatches
            && correlations.Select(item => item.KrlSymbol).SequenceEqual(simulatedMotions.Select(item => item.Target), StringComparer.Ordinal);
        var motionTypesMatched = motionCountMatches
            && correlations.Select(item => item.MotionType).SequenceEqual(simulatedMotions.Select(item => item.MotionType), StringComparer.Ordinal);
        AddCheck(checks, "positive-target-order", targetOrderMatched, "9A declared target order matches 9C completed target order.");
        AddCheck(checks, "positive-motion-types", motionTypesMatched, "9A declared motion types match 9C completed motion types.");

        var kssAccepted = kss.NativeKssStatus == NativeKssStatus.BoundedExecutionValidated
            && kss.Execution.GoConfirmed && kss.Execution.ValidCompleted
            && kss.Execution.ValidFinalMode == "Go" && kss.Execution.ValidFinalState == "End";
        AddCheck(checks, "native-kss-bounded-execution", kssAccepted, "9B profile-scoped native KSS completed in Go/End.");

        var finalSample = simPositive.Samples.LastOrDefault();
        var finalDeviation = finalSample is null
            ? double.PositiveInfinity
            : Distance(finalSample.X, finalSample.Y, finalSample.Z, 1900.0, 100.0, 1200.0);
        var axisEvidence = simPositive.Samples.Any(sample => sample.Axes.Count == 6);
        var tcpWithinThreshold = finalSample is not null
            && finalDeviation <= OfflineLoopComparisonContract.MaximumFinalTcpDeviationMillimeters;
        AddCheck(checks, "kukasim-axis-evidence", axisEvidence, "9C contains six-axis samples.");
        AddCheck(checks, "kukasim-final-tcp", tcpWithinThreshold, "9C final TCP is within the fixed 1 mm threshold.");

        var initialPose = kss.Execution.InitialPose;
        var finalPose = kss.Execution.FinalPose;
        var officeLiteDeltaX = initialPose is null || finalPose is null ? double.NaN : finalPose.X - initialPose.X;
        var positive = new OfflineLoopPositiveComparison
        {
            FixtureId = valid.FixtureId,
            Motions = motions,
            TargetOrderStatus = targetOrderMatched ? NormalizedEvidenceStatus.Matched : NormalizedEvidenceStatus.Unknown,
            MotionTypeStatus = motionTypesMatched ? NormalizedEvidenceStatus.Matched : NormalizedEvidenceStatus.Unknown,
            NativeKssExecutionStatus = kssAccepted ? NormalizedEvidenceStatus.Matched : NormalizedEvidenceStatus.Unknown,
            ExactC01NativeKssTrajectoryStatus = NormalizedEvidenceStatus.NotComparableProfile,
            NativeKssMode = kss.Execution.ValidFinalMode,
            NativeKssFinalState = kss.Execution.ValidFinalState,
            OfficeLiteRelativeXMillimeters = officeLiteDeltaX,
            KukaSimMode = simPositive.InterpreterModeAfter,
            KukaSimFinalState = simPositive.InterpreterStateAfter,
            KukaSimSampleCount = simPositive.Samples.Count,
            KukaSimFinalX = finalSample?.X ?? double.NaN,
            KukaSimFinalY = finalSample?.Y ?? double.NaN,
            KukaSimFinalZ = finalSample?.Z ?? double.NaN,
            KukaSimFinalTcpDeviationMillimeters = finalDeviation,
            AxisEvidenceStatus = axisEvidence ? NormalizedEvidenceStatus.Matched : NormalizedEvidenceStatus.Unknown,
            OrientationEvidenceStatus = NormalizedEvidenceStatus.NotAvailable,
            StatusTurnEvidenceStatus = NormalizedEvidenceStatus.NotAvailable,
            CollisionEvidenceStatus = NormalizedEvidenceStatus.NotAvailable,
            CycleTimeEvidenceStatus = NormalizedEvidenceStatus.NotAvailable
        };

        var expectedDiagnostic = invalid.ExpectedDiagnostic;
        var missingCorrelation = invalid.Correlations.SingleOrDefault(item => item.KrlSymbol == "P_MISSING");
        var nativeError = kss.Execution.InvalidErrors.SingleOrDefault(error =>
            error.ErrorNumber == 2137 && error.Line == 12 && error.Column == 6
            && error.Parameter.Contains("P_MISSING", StringComparison.Ordinal));
        var simMessage = simNegative.Messages.SingleOrDefault(message => message.Text.Contains("P_MISSING", StringComparison.Ordinal));
        var nativeDiagnosticMatched = expectedDiagnostic is not null && nativeError is not null
            && expectedDiagnostic.NativeMessageCode == nativeError.ErrorNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)
            && expectedDiagnostic.NativeLine == nativeError.Line && expectedDiagnostic.NativeColumn == nativeError.Column;
        var simDiagnosticMatched = simMessage is not null && simNegative.CompletedMotions.Count == 0;
        AddCheck(checks, "negative-native-diagnostic", nativeDiagnosticMatched, "9A expected native diagnostic matches 9B KSS 2137 file/line/column evidence.");
        AddCheck(checks, "negative-kukasim-rejection", simDiagnosticMatched, "9C reports P_MISSING and completes zero motions.");

        var negative = new OfflineLoopNegativeComparison
        {
            FixtureId = invalid.FixtureId,
            MissingSymbol = missingCorrelation?.KrlSymbol ?? string.Empty,
            DeclaredFile = missingCorrelation?.File ?? string.Empty,
            DeclaredSourceLine = missingCorrelation?.Line ?? 0,
            NativeKssMessageCode = nativeError?.ErrorNumber ?? 0,
            NativeKssFile = nativeError?.Module ?? string.Empty,
            NativeKssLine = nativeError?.Line ?? 0,
            NativeKssColumn = nativeError?.Column ?? 0,
            NativeKssDiagnosticStatus = nativeDiagnosticMatched ? NormalizedEvidenceStatus.Matched : NormalizedEvidenceStatus.Unknown,
            KukaSimMessage = simMessage?.Text ?? string.Empty,
            KukaSimCompletedMotionCount = simNegative.CompletedMotions.Count,
            KukaSimDiagnosticStatus = simDiagnosticMatched ? NormalizedEvidenceStatus.Matched : NormalizedEvidenceStatus.Unknown,
            RejectionBeforeMotionStatus = nativeDiagnosticMatched && simDiagnosticMatched
                ? NormalizedEvidenceStatus.RejectedAsExpected
                : NormalizedEvidenceStatus.Unknown,
            KukaSimFileLineStatus = NormalizedEvidenceStatus.NotAvailable
        };

        var profiles = (
            ExpectedDeclaredTargetProfile(),
            ExpectedOfficeLiteProfile(),
            ExpectedKukaSimProfile());
        return (positive, negative, profiles, checks);
    }

    internal static List<string> ValidateNormalizedPayload(OfflineLoopComparisonPayload payload)
    {
        var errors = new List<string>();
        if (payload.TerminalClassification != EnvironmentTerminalClassification.Ready
            || payload.ValidationStatus != OfflineLoopComparisonContract.ValidationStatus)
        {
            errors.Add("comparison is not in the accepted Ready state");
        }
        if (payload.VendorSoftwareStarted || payload.NetworkTrafficSent || payload.PhysicalControllerContacted || payload.RhinoAccessed)
        {
            errors.Add("file-only comparison overclaims vendor/network/physical/Rhino activity");
        }
        if (!payload.EnvironmentReusable
            || !payload.Checks.Select(check => check.Id).SequenceEqual(OfflineLoopComparisonContract.RequiredCheckIds, StringComparer.Ordinal)
            || payload.Checks.Any(check => check.Status != EnvironmentCheckStatus.Passed || string.IsNullOrWhiteSpace(check.Detail)))
        {
            errors.Add("checks and reusable-environment evidence are invalid");
        }
        if (payload.Inputs.Count != 4 || payload.Inputs.GroupBy(input => input.Role, StringComparer.Ordinal).Any(group => group.Count() != 1))
        {
            errors.Add("exactly four unique input receipt roles are required");
        }
        foreach (var expected in OfflineLoopComparisonContract.ExpectedReceiptSha256)
        {
            var input = payload.Inputs.SingleOrDefault(item => item.Role == expected.Key);
            if (input is null
                || input.ExpectedReceiptSha256 != expected.Value
                || input.ActualReceiptSha256 != expected.Value
                || input.SchemaIdentity != OfflineLoopComparisonContract.ExpectedInputSchemaIdentity[expected.Key]
                || !Path.IsPathFullyQualified(input.Path)
                || string.IsNullOrWhiteSpace(input.ReceiptId)
                || input.PayloadSha256 is not { Length: 64 }
                || input.PayloadSha256.Any(character => !Uri.IsHexDigit(character)))
            {
                errors.Add($"input receipt identity mismatch: {expected.Key}");
            }
        }
        if (payload.DeclaredTarget != ExpectedDeclaredTargetProfile()
            || payload.OfficeLiteProfile != ExpectedOfficeLiteProfile()
            || payload.KukaSimProfile != ExpectedKukaSimProfile())
        {
            errors.Add("profile boundaries are invalid");
        }
        var expectedTargets = new[] { "A_HOME", "P_START", "P_END" };
        var expectedTypes = new[] { "PTP", "PTP", "LIN" };
        if (payload.Positive.FixtureId != OfflineLoopComparisonContract.ValidFixtureId
            || payload.Positive.TargetOrderStatus != NormalizedEvidenceStatus.Matched
            || payload.Positive.MotionTypeStatus != NormalizedEvidenceStatus.Matched
            || payload.Positive.Motions.Select(item => item.KrlSymbol).SequenceEqual(expectedTargets, StringComparer.Ordinal) is false
            || payload.Positive.Motions.Select(item => item.KukaSimTarget).SequenceEqual(expectedTargets, StringComparer.Ordinal) is false
            || payload.Positive.Motions.Select(item => item.MotionType).SequenceEqual(expectedTypes, StringComparer.Ordinal) is false
            || payload.Positive.Motions.Any(item => item.KukaSimStatus != NormalizedEvidenceStatus.Matched
                || item.NativeKssStatus != NormalizedEvidenceStatus.NotComparableProfile))
        {
            errors.Add("positive target/motion normalization is invalid");
        }
        if (payload.Positive.NativeKssExecutionStatus != NormalizedEvidenceStatus.Matched
            || payload.Positive.ExactC01NativeKssTrajectoryStatus != NormalizedEvidenceStatus.NotComparableProfile
            || payload.Positive.NativeKssMode != "Go" || payload.Positive.NativeKssFinalState != "End"
            || Math.Abs(payload.Positive.OfficeLiteRelativeXMillimeters - 10.0) > 0.001
            || payload.Positive.KukaSimMode != "Go" || payload.Positive.KukaSimFinalState != "End"
            || payload.Positive.KukaSimSampleCount < OfflineLoopComparisonContract.MinimumKukaSimSampleCount
            || !double.IsFinite(payload.Positive.KukaSimFinalX)
            || !double.IsFinite(payload.Positive.KukaSimFinalY)
            || !double.IsFinite(payload.Positive.KukaSimFinalZ)
            || !double.IsFinite(payload.Positive.KukaSimFinalTcpDeviationMillimeters)
            || Math.Abs(
                Distance(
                    payload.Positive.KukaSimFinalX,
                    payload.Positive.KukaSimFinalY,
                    payload.Positive.KukaSimFinalZ,
                    1900.0,
                    100.0,
                    1200.0)
                - payload.Positive.KukaSimFinalTcpDeviationMillimeters) > 0.000000001
            || payload.Positive.KukaSimFinalTcpDeviationMillimeters > OfflineLoopComparisonContract.MaximumFinalTcpDeviationMillimeters)
        {
            errors.Add("positive KSS/KUKA.Sim state or TCP evidence is invalid");
        }
        if (payload.Positive.AxisEvidenceStatus != NormalizedEvidenceStatus.Matched
            || payload.Positive.OrientationEvidenceStatus != NormalizedEvidenceStatus.NotAvailable
            || payload.Positive.StatusTurnEvidenceStatus != NormalizedEvidenceStatus.NotAvailable
            || payload.Positive.CollisionEvidenceStatus != NormalizedEvidenceStatus.NotAvailable
            || payload.Positive.CycleTimeEvidenceStatus != NormalizedEvidenceStatus.NotAvailable)
        {
            errors.Add("availability classifications are invalid");
        }
        if (payload.Negative.FixtureId != OfflineLoopComparisonContract.InvalidFixtureId
            || payload.Negative.MissingSymbol != "P_MISSING"
            || !payload.Negative.DeclaredFile.EndsWith("controller-files/LAB_MISSING_TARGET.src", StringComparison.Ordinal)
            || payload.Negative.DeclaredSourceLine != 16
            || payload.Negative.NativeKssMessageCode != 2137
            || !payload.Negative.NativeKssFile.EndsWith("LAB_MISSING_TARGET.SRC", StringComparison.OrdinalIgnoreCase)
            || payload.Negative.NativeKssLine != 12 || payload.Negative.NativeKssColumn != 6
            || payload.Negative.NativeKssDiagnosticStatus != NormalizedEvidenceStatus.Matched
            || payload.Negative.KukaSimCompletedMotionCount != 0
            || !payload.Negative.KukaSimMessage.Contains("P_MISSING", StringComparison.Ordinal)
            || payload.Negative.KukaSimDiagnosticStatus != NormalizedEvidenceStatus.Matched
            || payload.Negative.RejectionBeforeMotionStatus != NormalizedEvidenceStatus.RejectedAsExpected
            || payload.Negative.KukaSimFileLineStatus != NormalizedEvidenceStatus.NotAvailable)
        {
            errors.Add("negative diagnostic normalization is invalid");
        }
        if (!payload.UnsupportedGaps.SequenceEqual(OfflineLoopComparisonContract.RequiredUnsupportedGaps, StringComparer.Ordinal))
        {
            errors.Add("unsupported-gap declarations are incomplete or reordered");
        }
        if (payload.SideEffects.Count != 1
            || !payload.SideEffects[0].StartsWith("CreateNewComparisonReceipt:", StringComparison.Ordinal))
        {
            errors.Add("comparison side effects must contain only the create-new receipt");
        }
        return errors;
    }

    private static ComparisonProfile ExpectedDeclaredTargetProfile() => new()
    {
        ProfileId = "item9a-declared-target",
        RobotModel = OfflineLoopComparisonContract.ExactRobotModel,
        ControllerModel = OfflineLoopComparisonContract.ExactControllerModel,
        KssVersion = OfflineLoopComparisonContract.KssVersion,
        Scope = "ExactC01DeclaredInput"
    };

    private static ComparisonProfile ExpectedOfficeLiteProfile() => new()
    {
        ProfileId = "item9b-isolated-officelite",
        RobotModel = OfflineLoopComparisonContract.OfficeLiteRobotModel,
        ControllerModel = OfflineLoopComparisonContract.OfficeLiteControllerModel,
        KssVersion = OfflineLoopComparisonContract.KssVersion,
        Scope = "GenericKssSemanticsOnlyNotExactC01Kinematics"
    };

    private static ComparisonProfile ExpectedKukaSimProfile() => new()
    {
        ProfileId = "item9c-exact-c01-integrated",
        RobotModel = OfflineLoopComparisonContract.ExactRobotModel,
        ControllerModel = OfflineLoopComparisonContract.ExactControllerModel,
        KssVersion = "NotAvailable",
        Scope = "ExactC01IntegratedNotNativeKssOrRcs"
    };

    private static (FixtureIntegrityReceipt Receipt, OfflineLoopInputReceipt Reference) ReadFixtureReceipt(string role, string path)
    {
        var fullPath = RequireExactInputPath(role, path, out var actualSha);
        var receipt = ReceiptSerialization.FromJson(File.ReadAllText(fullPath));
        var verification = ReceiptVerifier.Verify(receipt);
        if (!verification.Succeeded || receipt.Payload.TerminalClassification != VerificationStatus.Passed)
        {
            throw new InvalidOperationException($"{role} fixture receipt is invalid: {string.Join("; ", verification.Errors)}");
        }
        var expectedFixtureId = role == "item9a-valid"
            ? OfflineLoopComparisonContract.ValidFixtureId
            : OfflineLoopComparisonContract.InvalidFixtureId;
        if (receipt.Payload.FixtureId != expectedFixtureId)
        {
            throw new InvalidOperationException($"{role} fixture ID mismatch.");
        }
        return (receipt, ToReference(role, fullPath, receipt.SchemaIdentity, receipt.Payload.ReceiptId, receipt.PayloadSha256, actualSha));
    }

    private static (OfficeLiteNativeKssExecutionReceipt Receipt, OfflineLoopInputReceipt Reference) ReadNativeKssReceipt(string path)
    {
        const string role = "item9b-native-kss";
        var fullPath = RequireExactInputPath(role, path, out var actualSha);
        var receipt = ReceiptSerialization.OfficeLiteNativeKssExecutionFromJson(File.ReadAllText(fullPath));
        var verification = OfficeLiteNativeKssExecutionReceiptVerifier.Verify(receipt);
        if (!verification.Succeeded)
        {
            throw new InvalidOperationException($"{role} receipt is invalid: {string.Join("; ", verification.Errors)}");
        }
        return (receipt, ToReference(role, fullPath, receipt.SchemaIdentity, receipt.Payload.ReceiptId, receipt.PayloadSha256, actualSha));
    }

    private static (KukaSimIntegratedValidationReceipt Receipt, OfflineLoopInputReceipt Reference) ReadKukaSimReceipt(string path)
    {
        const string role = "item9c-kukasim";
        var fullPath = RequireExactInputPath(role, path, out var actualSha);
        var receipt = ReceiptSerialization.KukaSimIntegratedValidationFromJson(File.ReadAllText(fullPath));
        var verification = KukaSimIntegratedValidationReceiptVerifier.Verify(receipt);
        if (!verification.Succeeded)
        {
            throw new InvalidOperationException($"{role} receipt is invalid: {string.Join("; ", verification.Errors)}");
        }
        return (receipt, ToReference(role, fullPath, receipt.SchemaIdentity, receipt.Payload.ReceiptId, receipt.PayloadSha256, actualSha));
    }

    private static FixtureManifest ReadAndReverifyManifest(string role, FixtureIntegrityReceipt receipt)
    {
        var rerun = new FixtureVerifier().Verify(receipt.Payload.FixtureRoot, $"comparison-{role}").Receipt;
        if (rerun.Payload.TerminalClassification != VerificationStatus.Passed
            || rerun.Payload.ManifestSha256 != receipt.Payload.ManifestSha256
            || rerun.Payload.Files.Count != receipt.Payload.Files.Count)
        {
            throw new InvalidOperationException($"{role} fixture current bytes do not match the accepted receipt.");
        }
        if (receipt.Payload.ManifestSha256 != OfflineLoopComparisonContract.ExpectedManifestSha256[role])
        {
            throw new InvalidOperationException($"{role} manifest SHA-256 mismatch.");
        }
        var manifestPath = Path.Combine(receipt.Payload.FixtureRoot, "fixture.json");
        return JsonSerializer.Deserialize<FixtureManifest>(File.ReadAllBytes(manifestPath), ReceiptSerialization.StrictManifestOptions)
            ?? throw new JsonException($"{role} manifest JSON was empty.");
    }

    private static string RequireExactInputPath(string role, string path, out string actualSha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException($"{role} receipt does not exist.", fullPath);
        actualSha = ComputeFileSha256(fullPath);
        var expectedSha = OfflineLoopComparisonContract.ExpectedReceiptSha256[role];
        if (!string.Equals(actualSha, expectedSha, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{role} receipt SHA-256 mismatch: expected {expectedSha}, actual {actualSha}.");
        }
        return fullPath;
    }

    private static OfflineLoopInputReceipt ToReference(
        string role,
        string path,
        string schemaIdentity,
        string receiptId,
        string payloadSha256,
        string actualSha256) => new()
        {
            Role = role,
            Path = path,
            SchemaIdentity = schemaIdentity,
            ReceiptId = receiptId,
            PayloadSha256 = payloadSha256,
            ExpectedReceiptSha256 = OfflineLoopComparisonContract.ExpectedReceiptSha256[role],
            ActualReceiptSha256 = actualSha256
        };

    private static void AddCheck(List<EnvironmentCheck> checks, string id, bool passed, string detail)
    {
        checks.Add(new EnvironmentCheck
        {
            Id = id,
            Status = passed ? EnvironmentCheckStatus.Passed : EnvironmentCheckStatus.Failed,
            Detail = passed ? detail : $"FAILED: {detail}"
        });
    }

    private static double Distance(double x1, double y1, double z1, double x2, double y2, double z2)
        => Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2) + Math.Pow(z1 - z2, 2));

    internal static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex AttemptIdPattern();
}

public sealed record OfflineLoopComparisonVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;
    public bool Succeeded { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
    public List<string> Errors { get; init; } = [];
}

public static class OfflineLoopComparisonReceiptVerifier
{
    public static OfflineLoopComparisonVerificationResult Verify(OfflineLoopComparisonReceipt receipt)
        => Verify(receipt, rehashCurrentInputs: true);

    internal static OfflineLoopComparisonVerificationResult Verify(
        OfflineLoopComparisonReceipt receipt,
        bool rehashCurrentInputs)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (receipt.SchemaIdentity != OfflineLoopComparisonContract.ReceiptSchemaIdentity
            || receipt.SchemaVersion != OfflineLoopComparisonContract.ReceiptSchemaVersion)
        {
            errors.Add("comparison receipt schema identity/version is unsupported");
        }
        var payload = receipt.Payload;
        if (payload is null)
        {
            return new OfflineLoopComparisonVerificationResult { Succeeded = false, Errors = ["payload is required"] };
        }
        if (string.IsNullOrWhiteSpace(payload.ReceiptId) || string.IsNullOrWhiteSpace(payload.AttemptId)
            || payload.StartedAtUtc > payload.CompletedAtUtc || payload.DurationMilliseconds < 0
            || !IsSha(payload.CoreAssemblySha256)
            || string.IsNullOrWhiteSpace(payload.Runtime.OsDescription)
            || string.IsNullOrWhiteSpace(payload.Runtime.FrameworkDescription)
            || string.IsNullOrWhiteSpace(payload.Runtime.ProcessArchitecture))
        {
            errors.Add("receipt identity, timing or Core hash is invalid");
        }
        if (!string.Equals(receipt.PayloadSha256, ReceiptSerialization.ComputeCanonicalSha256(payload), StringComparison.Ordinal))
        {
            errors.Add("payload SHA-256 mismatch");
        }
        errors.AddRange(OfflineLoopComparisonRunner.ValidateNormalizedPayload(payload));

        if (rehashCurrentInputs)
        {
            try
            {
                var request = new OfflineLoopComparisonRequest
                {
                    ValidFixtureReceiptPath = RequireRole(payload, "item9a-valid").Path,
                    InvalidFixtureReceiptPath = RequireRole(payload, "item9a-invalid").Path,
                    NativeKssReceiptPath = RequireRole(payload, "item9b-native-kss").Path,
                    KukaSimReceiptPath = RequireRole(payload, "item9c-kukasim").Path
                };
                var inputs = OfflineLoopComparisonRunner.LoadVerifiedInputs(request);
                var rebuilt = OfflineLoopComparisonRunner.BuildNormalizedComparison(inputs);
                if (!string.Equals(
                        ReceiptSerialization.ComputeCanonicalSha256(rebuilt.Positive),
                        ReceiptSerialization.ComputeCanonicalSha256(payload.Positive),
                        StringComparison.Ordinal)
                    || !string.Equals(
                        ReceiptSerialization.ComputeCanonicalSha256(rebuilt.Negative),
                        ReceiptSerialization.ComputeCanonicalSha256(payload.Negative),
                        StringComparison.Ordinal)
                    || !string.Equals(
                        ReceiptSerialization.ComputeCanonicalSha256(inputs.InputReferences),
                        ReceiptSerialization.ComputeCanonicalSha256(payload.Inputs),
                        StringComparison.Ordinal)
                    || !string.Equals(
                        ReceiptSerialization.ComputeCanonicalSha256(rebuilt.Profiles.DeclaredTarget),
                        ReceiptSerialization.ComputeCanonicalSha256(payload.DeclaredTarget),
                        StringComparison.Ordinal)
                    || !string.Equals(
                        ReceiptSerialization.ComputeCanonicalSha256(rebuilt.Profiles.OfficeLiteProfile),
                        ReceiptSerialization.ComputeCanonicalSha256(payload.OfficeLiteProfile),
                        StringComparison.Ordinal)
                    || !string.Equals(
                        ReceiptSerialization.ComputeCanonicalSha256(rebuilt.Profiles.KukaSimProfile),
                        ReceiptSerialization.ComputeCanonicalSha256(payload.KukaSimProfile),
                        StringComparison.Ordinal)
                    || !string.Equals(
                        ReceiptSerialization.ComputeCanonicalSha256(rebuilt.Checks),
                        ReceiptSerialization.ComputeCanonicalSha256(payload.Checks),
                        StringComparison.Ordinal))
                {
                    errors.Add("normalized comparison does not match current bound input evidence");
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException
                or InvalidOperationException or ArgumentException)
            {
                errors.Add($"current input verification failed: {exception.Message}");
            }
        }

        return new OfflineLoopComparisonVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }

    private static OfflineLoopInputReceipt RequireRole(OfflineLoopComparisonPayload payload, string role)
        => payload.Inputs.SingleOrDefault(input => input.Role == role)
            ?? throw new InvalidOperationException($"input role is missing: {role}");

    private static bool IsSha(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}

public static class OfflineLoopComparisonReceiptWriter
{
    public static string WriteNew(string outputPath, OfflineLoopComparisonReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        var verification = OfflineLoopComparisonReceiptVerifier.Verify(receipt);
        if (!verification.Succeeded)
        {
            throw new InvalidOperationException(
                "Offline-loop comparison receipt is invalid: " + string.Join("; ", verification.Errors));
        }
        var fullPath = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Receipt output must use the .json extension.", nameof(outputPath));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Output path has no parent directory.", nameof(outputPath)));
        using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
        {
            writer.Write(ReceiptSerialization.ToJson(receipt));
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
        var readback = ReceiptSerialization.OfflineLoopComparisonFromJson(File.ReadAllText(fullPath));
        var readbackVerification = OfflineLoopComparisonReceiptVerifier.Verify(readback);
        if (!readbackVerification.Succeeded)
        {
            throw new IOException("Offline-loop comparison receipt readback verification failed.");
        }
        return fullPath;
    }
}
