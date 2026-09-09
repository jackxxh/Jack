using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class OfflineLoopComparisonTests
{
    [Fact]
    public void Normalized_payload_preserves_profile_boundaries_and_supported_evidence_only()
    {
        var payload = ValidPayload();

        Assert.Empty(OfflineLoopComparisonRunner.ValidateNormalizedPayload(payload));
        Assert.Equal(NormalizedEvidenceStatus.NotComparableProfile, payload.Positive.ExactC01NativeKssTrajectoryStatus);
        Assert.Equal(NormalizedEvidenceStatus.NotAvailable, payload.Positive.OrientationEvidenceStatus);
        Assert.Equal(NormalizedEvidenceStatus.NotAvailable, payload.Positive.StatusTurnEvidenceStatus);
        Assert.Equal(NormalizedEvidenceStatus.NotAvailable, payload.Positive.CollisionEvidenceStatus);
        Assert.Equal(NormalizedEvidenceStatus.NotAvailable, payload.Positive.CycleTimeEvidenceStatus);
        Assert.Equal(NormalizedEvidenceStatus.NotAvailable, payload.Negative.KukaSimFileLineStatus);
    }

    [Fact]
    public void Accepted_sample_count_is_evidence_based_not_tied_to_polling_frequency()
    {
        var payload = ValidPayload();
        var independentlySampled = payload with
        {
            Positive = payload.Positive with { KukaSimSampleCount = 235 }
        };

        Assert.Empty(OfflineLoopComparisonRunner.ValidateNormalizedPayload(independentlySampled));
    }

    [Fact]
    public void Recomputed_target_order_tamper_is_rejected()
    {
        var payload = ValidPayload();
        var motions = payload.Positive.Motions.ToList();
        motions[1] = motions[1] with { KukaSimTarget = "P_END" };
        motions[2] = motions[2] with { KukaSimTarget = "P_START" };
        var tamperedPayload = payload with { Positive = payload.Positive with { Motions = motions } };
        var receipt = Receipt(tamperedPayload);

        var result = OfflineLoopComparisonReceiptVerifier.Verify(receipt, rehashCurrentInputs: false);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("positive target/motion normalization", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_normalized_target_is_rejected()
    {
        var payload = ValidPayload();
        var shortened = payload.Positive.Motions.Take(2).ToList();
        var tamperedPayload = payload with { Positive = payload.Positive with { Motions = shortened } };

        var errors = OfflineLoopComparisonRunner.ValidateNormalizedPayload(tamperedPayload);

        Assert.Contains(errors, error => error.Contains("positive target/motion normalization", StringComparison.Ordinal));
    }

    [Fact]
    public void Final_tcp_deviation_over_one_millimeter_is_rejected()
    {
        var payload = ValidPayload();
        var tamperedPayload = payload with
        {
            Positive = payload.Positive with { KukaSimFinalTcpDeviationMillimeters = 1.001 }
        };

        var errors = OfflineLoopComparisonRunner.ValidateNormalizedPayload(tamperedPayload);

        Assert.Contains(errors, error => error.Contains("positive KSS/KUKA.Sim state or TCP evidence", StringComparison.Ordinal));
    }

    [Fact]
    public void Bound_input_receipt_hash_tamper_is_rejected_even_with_recomputed_payload_hash()
    {
        var payload = ValidPayload();
        var inputs = payload.Inputs.ToList();
        inputs[0] = inputs[0] with { ActualReceiptSha256 = new string('F', 64) };
        var tamperedPayload = payload with { Inputs = inputs };

        var result = OfflineLoopComparisonReceiptVerifier.Verify(Receipt(tamperedPayload), rehashCurrentInputs: false);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("input receipt identity mismatch", StringComparison.Ordinal));
    }

    private static OfflineLoopComparisonReceipt Receipt(OfflineLoopComparisonPayload payload) => new()
    {
        Payload = payload,
        PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
    };

    private static OfflineLoopComparisonPayload ValidPayload()
    {
        var targets = new[] { "A_HOME", "P_START", "P_END" };
        var motionTypes = new[] { "PTP", "PTP", "LIN" };
        var motions = targets.Select((target, index) => new NormalizedMotionComparison
        {
            Sequence = index + 1,
            OperationId = $"operation-{index + 1}",
            TargetId = $"target-{index + 1}",
            MotionType = motionTypes[index],
            KrlSymbol = target,
            KrlFile = "LAB_MINIMAL.src",
            KrlLine = 10 + index,
            KukaSimTarget = target,
            KukaSimMotionType = motionTypes[index],
            KukaSimStatus = NormalizedEvidenceStatus.Matched,
            NativeKssStatus = NormalizedEvidenceStatus.NotComparableProfile
        }).ToList();

        return new OfflineLoopComparisonPayload
        {
            ReceiptId = "offline-loop-comparison-test-ready",
            AttemptId = "test-ready",
            CoreAssemblySha256 = new string('A', 64),
            Runtime = new RuntimeEnvironment
            {
                OsDescription = "test",
                FrameworkDescription = "test",
                ProcessArchitecture = "X64"
            },
            StartedAtUtc = DateTimeOffset.UnixEpoch,
            CompletedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(1),
            DurationMilliseconds = 1000,
            TerminalClassification = EnvironmentTerminalClassification.Ready,
            ValidationStatus = OfflineLoopComparisonContract.ValidationStatus,
            Inputs = OfflineLoopComparisonContract.ExpectedReceiptSha256.Select(pair => new OfflineLoopInputReceipt
            {
                Role = pair.Key,
                Path = Path.GetFullPath(Path.Combine("test", pair.Key + ".json")),
                SchemaIdentity = OfflineLoopComparisonContract.ExpectedInputSchemaIdentity[pair.Key],
                ReceiptId = pair.Key,
                PayloadSha256 = new string('B', 64),
                ExpectedReceiptSha256 = pair.Value,
                ActualReceiptSha256 = pair.Value
            }).ToList(),
            DeclaredTarget = new ComparisonProfile
            {
                ProfileId = "item9a-declared-target",
                RobotModel = OfflineLoopComparisonContract.ExactRobotModel,
                ControllerModel = OfflineLoopComparisonContract.ExactControllerModel,
                KssVersion = OfflineLoopComparisonContract.KssVersion,
                Scope = "ExactC01DeclaredInput"
            },
            OfficeLiteProfile = new ComparisonProfile
            {
                ProfileId = "item9b-isolated-officelite",
                RobotModel = OfflineLoopComparisonContract.OfficeLiteRobotModel,
                ControllerModel = OfflineLoopComparisonContract.OfficeLiteControllerModel,
                KssVersion = OfflineLoopComparisonContract.KssVersion,
                Scope = "GenericKssSemanticsOnlyNotExactC01Kinematics"
            },
            KukaSimProfile = new ComparisonProfile
            {
                ProfileId = "item9c-exact-c01-integrated",
                RobotModel = OfflineLoopComparisonContract.ExactRobotModel,
                ControllerModel = OfflineLoopComparisonContract.ExactControllerModel,
                KssVersion = "NotAvailable",
                Scope = "ExactC01IntegratedNotNativeKssOrRcs"
            },
            Positive = new OfflineLoopPositiveComparison
            {
                FixtureId = OfflineLoopComparisonContract.ValidFixtureId,
                Motions = motions,
                TargetOrderStatus = NormalizedEvidenceStatus.Matched,
                MotionTypeStatus = NormalizedEvidenceStatus.Matched,
                NativeKssExecutionStatus = NormalizedEvidenceStatus.Matched,
                ExactC01NativeKssTrajectoryStatus = NormalizedEvidenceStatus.NotComparableProfile,
                NativeKssMode = "Go",
                NativeKssFinalState = "End",
                OfficeLiteRelativeXMillimeters = 10.0,
                KukaSimMode = "Go",
                KukaSimFinalState = "End",
                KukaSimSampleCount = 260,
                KukaSimFinalX = 1900.0,
                KukaSimFinalY = 100.0,
                KukaSimFinalZ = 1200.0,
                KukaSimFinalTcpDeviationMillimeters = 0.0,
                AxisEvidenceStatus = NormalizedEvidenceStatus.Matched,
                OrientationEvidenceStatus = NormalizedEvidenceStatus.NotAvailable,
                StatusTurnEvidenceStatus = NormalizedEvidenceStatus.NotAvailable,
                CollisionEvidenceStatus = NormalizedEvidenceStatus.NotAvailable,
                CycleTimeEvidenceStatus = NormalizedEvidenceStatus.NotAvailable
            },
            Negative = new OfflineLoopNegativeComparison
            {
                FixtureId = OfflineLoopComparisonContract.InvalidFixtureId,
                MissingSymbol = "P_MISSING",
                DeclaredFile = "controller-files/LAB_MISSING_TARGET.src",
                DeclaredSourceLine = 16,
                NativeKssMessageCode = 2137,
                NativeKssFile = "LAB_MISSING_TARGET.src",
                NativeKssLine = 12,
                NativeKssColumn = 6,
                NativeKssDiagnosticStatus = NormalizedEvidenceStatus.Matched,
                KukaSimMessage = "Target P_MISSING is not defined.",
                KukaSimCompletedMotionCount = 0,
                KukaSimDiagnosticStatus = NormalizedEvidenceStatus.Matched,
                RejectionBeforeMotionStatus = NormalizedEvidenceStatus.RejectedAsExpected,
                KukaSimFileLineStatus = NormalizedEvidenceStatus.NotAvailable
            },
            Checks = OfflineLoopComparisonContract.RequiredCheckIds.Select(id => new EnvironmentCheck
            {
                Id = id,
                Status = EnvironmentCheckStatus.Passed,
                Detail = "test"
            }).ToList(),
            SideEffects = ["CreateNewComparisonReceipt:C:\\test\\comparison.json"],
            EnvironmentReusable = true,
            UnsupportedGaps = OfflineLoopComparisonContract.RequiredUnsupportedGaps.ToList()
        };
    }
}
