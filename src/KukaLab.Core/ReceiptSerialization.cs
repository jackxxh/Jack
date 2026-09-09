using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KukaLab.Core;

public static class ReceiptSerialization
{
    private static readonly JsonSerializerOptions CompactOptions = CreateOptions(writeIndented: false);
    private static readonly JsonSerializerOptions IndentedOptions = CreateOptions(writeIndented: true);

    internal static JsonSerializerOptions StrictManifestOptions { get; } = CreateOptions(
        writeIndented: false,
        rejectUnknownMembers: true);

    private static JsonSerializerOptions StrictReceiptOptions { get; } = CreateOptions(
        writeIndented: false,
        rejectUnknownMembers: true);

    public static string ToJson(FixtureIntegrityReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static FixtureIntegrityReceipt FromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<FixtureIntegrityReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("Receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("Receipt payload was null.");
        }

        return receipt;
    }

    public static string ToJson(ReceiptVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(EnvironmentInventoryReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(EnvironmentReceiptVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(EnvironmentReceiptDiffResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(OfficeLiteCycleReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(OfficeLiteCycleReceiptVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(OfficeLiteProfileCloneReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(OfficeLiteProfileCloneVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(OfficeLiteDeploymentPreflightReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(OfficeLiteDeploymentPreflightVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(WorkVisualRunnerSmokeReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(WorkVisualRunnerSmokeReceiptVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(OfficeLiteOnlineProjectInventoryReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(OfficeLiteOnlineProjectInventoryVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(OfficeLiteControllerProfileReadbackReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(OfficeLiteControllerProfileReadbackVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(OfficeLiteExactProfileAcceptanceReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(OfficeLiteExactProfileAcceptanceVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(OfficeLiteActiveProjectDownloadReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(OfficeLiteActiveProjectDownloadVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(OfficeLiteRepositoryInventoryReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(OfficeLiteRepositoryInventoryVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(OfficeLiteNativeKssDiagnosticReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(OfficeLiteNativeKssDiagnosticVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(OfficeLiteNativeKssExecutionReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(OfficeLiteNativeKssExecutionVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(NativeKssCandidateExecutionReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(NativeKssCandidateExecutionVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(WorkVisualInterfaceInventoryReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(WorkVisualInterfaceInventoryVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(OfficeLiteExpertModeInterfaceInventoryReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(OfficeLiteExpertModeInterfaceInventoryVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(KukaSimComponentSmokeReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(KukaSimComponentSmokeReceiptVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(KukaSimIntegratedValidationReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(KukaSimIntegratedValidationVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(KukaSimCandidateExecutionReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(KukaSimCandidateExecutionVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(KukaSimOfficeLiteVirtualLoopReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(KukaSimOfficeLiteVirtualLoopVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(OfflineLoopComparisonReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(OfflineLoopComparisonVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(HyperVOfficeLiteCycleReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(HyperVOfficeLiteCycleVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(OfficeLiteDeliveryInspectionReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(OfficeLiteDeliveryReceiptVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(RealControllerNetworkPreflightReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(RealControllerNetworkPreflightVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(RealControllerEndpointProbeReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(RealControllerEndpointProbeVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(ControllerObservationReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(ControllerObservationVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(ControllerCompatibilityResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(WorkVisualProjectIntakeReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(WorkVisualProjectIntakeVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(WorkVisualProjectExtractionReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(WorkVisualProjectExtractionVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(ControllerProjectBaselineReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(ControllerProjectBaselineVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(ControllerProjectSoftwareBaseline baseline) =>
        JsonSerializer.Serialize(baseline, IndentedOptions);

    public static string ToJson(ControllerProjectVaultManifest manifest) =>
        JsonSerializer.Serialize(manifest, IndentedOptions);

    public static string ToJson(ValidationPackageManifest manifest) =>
        JsonSerializer.Serialize(manifest, IndentedOptions);

    public static string ToJson(ValidationPackageIntegrityReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(ValidationPackageReceiptVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(KrlStaticPreflightReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(KrlStaticPreflightVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(RawKrlCandidateReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(RawKrlCandidateVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static string ToJson(StageOneEvidenceBundleReceipt receipt) =>
        JsonSerializer.Serialize(receipt, IndentedOptions);

    public static string ToJson(StageOneEvidenceBundleVerificationResult result) =>
        JsonSerializer.Serialize(result, IndentedOptions);

    public static EnvironmentInventoryReceipt EnvironmentFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<EnvironmentInventoryReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("Environment receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("Environment receipt payload was null.");
        }

        return receipt;
    }

    public static OfficeLiteCycleReceipt OfficeLiteCycleFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<OfficeLiteCycleReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("OfficeLite cycle receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("OfficeLite cycle receipt payload was null.");
        }

        return receipt;
    }

    public static OfficeLiteProfileCloneReceipt OfficeLiteProfileCloneFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<OfficeLiteProfileCloneReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("OfficeLite profile-clone receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("OfficeLite profile-clone receipt payload was null.");
        }

        return receipt;
    }

    public static OfficeLiteDeploymentPreflightReceipt OfficeLiteDeploymentPreflightFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<OfficeLiteDeploymentPreflightReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("OfficeLite WorkVisual deployment-preflight receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("OfficeLite WorkVisual deployment-preflight receipt payload was null.");
        }

        return receipt;
    }

    public static WorkVisualRunnerSmokeReceipt WorkVisualRunnerSmokeFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<WorkVisualRunnerSmokeReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("WorkVisual runner smoke receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("WorkVisual runner smoke receipt payload was null.");
        }

        return receipt;
    }

    public static OfficeLiteOnlineProjectInventoryReceipt OfficeLiteOnlineProjectInventoryFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<OfficeLiteOnlineProjectInventoryReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("OfficeLite online project inventory receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("OfficeLite online project inventory receipt payload was null.");
        }

        return receipt;
    }

    public static OfficeLiteControllerProfileReadbackReceipt OfficeLiteControllerProfileReadbackFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<OfficeLiteControllerProfileReadbackReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("OfficeLite controller-profile readback receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("OfficeLite controller-profile readback receipt payload was null.");
        }

        return receipt;
    }

    public static OfficeLiteExactProfileAcceptanceReceipt OfficeLiteExactProfileAcceptanceFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<OfficeLiteExactProfileAcceptanceReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("OfficeLite exact-profile acceptance receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("OfficeLite exact-profile acceptance receipt payload was null.");
        }

        return receipt;
    }

    public static OfficeLiteActiveProjectDownloadReceipt OfficeLiteActiveProjectDownloadFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<OfficeLiteActiveProjectDownloadReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("OfficeLite active-project download receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("OfficeLite active-project download receipt payload was null.");
        }

        return receipt;
    }

    public static OfficeLiteRepositoryInventoryReceipt OfficeLiteRepositoryInventoryFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<OfficeLiteRepositoryInventoryReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("OfficeLite repository inventory receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("OfficeLite repository inventory receipt payload was null.");
        }

        return receipt;
    }

    public static OfficeLiteNativeKssDiagnosticReceipt OfficeLiteNativeKssDiagnosticFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<OfficeLiteNativeKssDiagnosticReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("OfficeLite native-KSS diagnostic receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("OfficeLite native-KSS diagnostic receipt payload was null.");
        }

        return receipt;
    }

    public static OfficeLiteNativeKssExecutionReceipt OfficeLiteNativeKssExecutionFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<OfficeLiteNativeKssExecutionReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("OfficeLite native-KSS execution receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("OfficeLite native-KSS execution receipt payload was null.");
        }

        return receipt;
    }

    public static NativeKssCandidateExecutionReceipt NativeKssCandidateExecutionFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<NativeKssCandidateExecutionReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("Native KSS candidate-execution receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("Native KSS candidate-execution receipt payload was null.");
        }

        return receipt;
    }

    public static WorkVisualInterfaceInventoryReceipt WorkVisualInterfaceInventoryFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<WorkVisualInterfaceInventoryReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("WorkVisual interface inventory receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("WorkVisual interface inventory receipt payload was null.");
        }

        return receipt;
    }

    public static OfficeLiteExpertModeInterfaceInventoryReceipt OfficeLiteExpertModeInterfaceInventoryFromJson(
        string json)
    {
        var receipt = JsonSerializer.Deserialize<OfficeLiteExpertModeInterfaceInventoryReceipt>(
                json,
                StrictReceiptOptions)
            ?? throw new JsonException("OfficeLite expert-mode interface inventory receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("OfficeLite expert-mode interface inventory receipt payload was null.");
        }

        return receipt;
    }

    public static KukaSimComponentSmokeReceipt KukaSimComponentSmokeFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<KukaSimComponentSmokeReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("KUKA.Sim component smoke receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("KUKA.Sim component smoke receipt payload was null.");
        }

        return receipt;
    }

    public static KukaSimIntegratedValidationReceipt KukaSimIntegratedValidationFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<KukaSimIntegratedValidationReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("KUKA.Sim Integrated validation receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("KUKA.Sim Integrated validation receipt payload was null.");
        }

        return receipt;
    }

    public static KukaSimCandidateExecutionReceipt KukaSimCandidateExecutionFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<KukaSimCandidateExecutionReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("KUKA.Sim candidate-execution receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("KUKA.Sim candidate-execution receipt payload was null.");
        }

        return receipt;
    }

    public static KukaSimOfficeLiteVirtualLoopReceipt KukaSimOfficeLiteVirtualLoopFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<KukaSimOfficeLiteVirtualLoopReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("KUKA.Sim/OfficeLite virtual-loop receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("KUKA.Sim/OfficeLite virtual-loop receipt payload was null.");
        }

        return receipt;
    }

    public static OfflineLoopComparisonReceipt OfflineLoopComparisonFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<OfflineLoopComparisonReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("Offline-loop comparison receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("Offline-loop comparison receipt payload was null.");
        }

        return receipt;
    }

    public static HyperVOfficeLiteCycleReceipt HyperVOfficeLiteCycleFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<HyperVOfficeLiteCycleReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("Hyper-V OfficeLite cycle receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("Hyper-V OfficeLite cycle receipt payload was null.");
        }

        return receipt;
    }

    public static OfficeLiteDeliveryInspectionReceipt OfficeLiteDeliveryInspectionFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<OfficeLiteDeliveryInspectionReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("OfficeLite delivery inspection receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("OfficeLite delivery inspection receipt payload was null.");
        }

        return receipt;
    }

    public static RealControllerNetworkPreflightReceipt RealControllerNetworkPreflightFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<RealControllerNetworkPreflightReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("Real-controller network preflight receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("Real-controller network preflight receipt payload was null.");
        }

        return receipt;
    }

    public static RealControllerEndpointProbeReceipt RealControllerEndpointProbeFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<RealControllerEndpointProbeReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("Real-controller endpoint-probe receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("Real-controller endpoint-probe receipt payload was null.");
        }

        return receipt;
    }

    public static ControllerObservationReceipt ControllerObservationFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<ControllerObservationReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("Controller-observation receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("Controller-observation receipt payload was null.");
        }

        return receipt;
    }

    public static WorkVisualProjectIntakeReceipt WorkVisualProjectIntakeFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<WorkVisualProjectIntakeReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("WorkVisual project-intake receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("WorkVisual project-intake receipt payload was null.");
        }

        return receipt;
    }

    public static WorkVisualProjectExtractionReceipt WorkVisualProjectExtractionFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<WorkVisualProjectExtractionReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("WorkVisual project-extraction receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("WorkVisual project-extraction receipt payload was null.");
        }

        return receipt;
    }

    public static ControllerProjectBaselineReceipt ControllerProjectBaselineFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<ControllerProjectBaselineReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("Controller-project baseline receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("Controller-project baseline receipt payload was null.");
        }

        return receipt;
    }

    public static ControllerProjectSoftwareBaseline ControllerProjectSoftwareBaselineFromJson(string json) =>
        JsonSerializer.Deserialize<ControllerProjectSoftwareBaseline>(json, StrictReceiptOptions)
        ?? throw new JsonException("Controller-project software baseline JSON was empty.");

    public static ControllerProjectVaultManifest ControllerProjectVaultManifestFromJson(string json) =>
        JsonSerializer.Deserialize<ControllerProjectVaultManifest>(json, StrictReceiptOptions)
        ?? throw new JsonException("Controller-project vault manifest JSON was empty.");

    public static ValidationPackageIntegrityReceipt ValidationPackageFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<ValidationPackageIntegrityReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("ValidationPackage receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("ValidationPackage receipt payload was null.");
        }

        return receipt;
    }

    public static KrlStaticPreflightReceipt KrlStaticPreflightFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<KrlStaticPreflightReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("KRL static-preflight receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("KRL static-preflight receipt payload was null.");
        }

        return receipt;
    }

    public static RawKrlCandidateReceipt RawKrlCandidateFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<RawKrlCandidateReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("Raw KRL candidate receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("Raw KRL candidate receipt payload was null.");
        }

        return receipt;
    }

    public static StageOneEvidenceBundleReceipt StageOneEvidenceBundleFromJson(string json)
    {
        var receipt = JsonSerializer.Deserialize<StageOneEvidenceBundleReceipt>(json, StrictReceiptOptions)
            ?? throw new JsonException("Stage-one evidence bundle receipt JSON was empty.");
        if (ReferenceEquals(receipt.Payload, null))
        {
            throw new JsonException("Stage-one evidence bundle receipt payload was null.");
        }

        return receipt;
    }

    internal static KukaSimInProcessResult KukaSimInProcessResultFromJson(string json) =>
        JsonSerializer.Deserialize<KukaSimInProcessResult>(json, StrictReceiptOptions)
            ?? throw new JsonException("KUKA.Sim in-process result JSON was empty.");

    public static string ComputePayloadSha256(FixtureIntegrityPayload payload)
    {
        return ComputeCanonicalSha256(payload);
    }

    public static string ComputeCanonicalSha256<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload, CompactOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    internal static string ComputeOfficeLiteCyclePayloadSha256(
        OfficeLiteCyclePayload payload,
        int schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (schemaVersion >= OfficeLiteCycleContract.ReceiptSchemaVersion)
        {
            return ComputeCanonicalSha256(payload);
        }

        if (schemaVersion == 3)
        {
            return ComputeCanonicalSha256(new
            {
                payload.ReceiptId,
                payload.AttemptId,
                payload.CoreVersion,
                payload.CoreAssemblySha256,
                payload.Runtime,
                payload.StartedAtUtc,
                payload.CompletedAtUtc,
                payload.DurationMilliseconds,
                payload.TerminalClassification,
                payload.AssetRoot,
                payload.VmxPath,
                payload.VmrunPath,
                payload.StartedByThisAttempt,
                payload.ControllerReady,
                payload.CleanShutdownVerified,
                payload.NativeKssStatus,
                payload.GuestEndpoint,
                payload.WorkVisualServices,
                payload.NetworkControls,
                payload.Checks,
                payload.Files,
                payload.Commands,
                payload.Probes,
                payload.SideEffects,
                payload.UnsupportedGaps
            });
        }

        if (schemaVersion == 2)
        {
            return ComputeCanonicalSha256(new
            {
                payload.ReceiptId,
                payload.AttemptId,
                payload.CoreVersion,
                payload.CoreAssemblySha256,
                payload.Runtime,
                payload.StartedAtUtc,
                payload.CompletedAtUtc,
                payload.DurationMilliseconds,
                payload.TerminalClassification,
                payload.AssetRoot,
                payload.VmxPath,
                payload.VmrunPath,
                payload.StartedByThisAttempt,
                payload.ControllerReady,
                payload.CleanShutdownVerified,
                payload.NativeKssStatus,
                payload.GuestEndpoint,
                payload.WorkVisualServices,
                payload.Checks,
                payload.Files,
                payload.Commands,
                payload.Probes,
                payload.SideEffects,
                payload.UnsupportedGaps
            });
        }

        if (schemaVersion == 1)
        {
            return ComputeCanonicalSha256(new
            {
                payload.ReceiptId,
                payload.AttemptId,
                payload.CoreVersion,
                payload.CoreAssemblySha256,
                payload.Runtime,
                payload.StartedAtUtc,
                payload.CompletedAtUtc,
                payload.DurationMilliseconds,
                payload.TerminalClassification,
                payload.AssetRoot,
                payload.VmxPath,
                payload.VmrunPath,
                payload.StartedByThisAttempt,
                payload.ControllerReady,
                payload.CleanShutdownVerified,
                payload.NativeKssStatus,
                payload.GuestEndpoint,
                payload.Checks,
                payload.Files,
                payload.Commands,
                payload.Probes,
                payload.SideEffects,
                payload.UnsupportedGaps
            });
        }

        return ComputeCanonicalSha256(payload);
    }

    public static bool HasValidPayloadHash(FixtureIntegrityReceipt receipt) =>
        string.Equals(
            receipt.PayloadSha256,
            ComputePayloadSha256(receipt.Payload),
            StringComparison.OrdinalIgnoreCase);

    private static JsonSerializerOptions CreateOptions(bool writeIndented, bool rejectUnknownMembers = false)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = writeIndented,
            UnmappedMemberHandling = rejectUnknownMembers
                ? JsonUnmappedMemberHandling.Disallow
                : JsonUnmappedMemberHandling.Skip
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed record ReceiptVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}

public static class ReceiptVerifier
{
    public static ReceiptVerificationResult Verify(FixtureIntegrityReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (!string.Equals(receipt.SchemaIdentity, FixtureContract.ReceiptSchemaIdentity, StringComparison.Ordinal))
        {
            errors.Add($"schemaIdentity must be {FixtureContract.ReceiptSchemaIdentity}");
        }

        if (receipt.SchemaVersion != FixtureContract.ReceiptSchemaVersion)
        {
            errors.Add($"schemaVersion must be {FixtureContract.ReceiptSchemaVersion}");
        }

        if (string.IsNullOrWhiteSpace(receipt.Payload.ReceiptId))
        {
            errors.Add("payload.receiptId is required");
        }

        if (string.IsNullOrWhiteSpace(receipt.Payload.AttemptId))
        {
            errors.Add("payload.attemptId is required");
        }

        if (string.IsNullOrWhiteSpace(receipt.Payload.FixtureId))
        {
            errors.Add("payload.fixtureId is required");
        }

        if (receipt.Payload.CoreAssemblySha256.Length != 64
            || receipt.Payload.CoreAssemblySha256.Any(character => !Uri.IsHexDigit(character)))
        {
            errors.Add("payload.coreAssemblySha256 must be a SHA-256 value");
        }

        if (receipt.Payload.Runtime is null
            || string.IsNullOrWhiteSpace(receipt.Payload.Runtime.OsDescription)
            || string.IsNullOrWhiteSpace(receipt.Payload.Runtime.FrameworkDescription)
            || string.IsNullOrWhiteSpace(receipt.Payload.Runtime.ProcessArchitecture))
        {
            errors.Add("payload.runtime identity is incomplete");
        }

        if (receipt.Payload.StartedAtUtc > receipt.Payload.CompletedAtUtc)
        {
            errors.Add("payload timestamps are reversed");
        }

        if (receipt.Payload.DurationMilliseconds < 0)
        {
            errors.Add("payload.durationMilliseconds cannot be negative");
        }

        if (receipt.Payload.Checks is null || receipt.Payload.Checks.Count == 0)
        {
            errors.Add("payload.checks must contain evidence");
        }

        if (receipt.Payload.Files is null)
        {
            errors.Add("payload.files cannot be null");
        }

        if (receipt.Payload.SideEffects is null)
        {
            errors.Add("payload.sideEffects cannot be null");
        }

        if (receipt.Payload.UnsupportedGaps is null)
        {
            errors.Add("payload.unsupportedGaps cannot be null");
        }

        if (receipt.Payload.TerminalClassification != receipt.Payload.ArtifactIntegrity)
        {
            errors.Add("fixture-integrity terminal classification must match artifactIntegrity");
        }

        if (!ReceiptSerialization.HasValidPayloadHash(receipt))
        {
            errors.Add("payloadSha256 does not match the canonical payload");
        }

        return new ReceiptVerificationResult
        {
            ReceiptId = receipt.Payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }
}

public static class ReceiptWriter
{
    public static string WriteNew(string outputPath, FixtureIntegrityReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);

        if (!ReceiptSerialization.HasValidPayloadHash(receipt))
        {
            throw new InvalidOperationException("Receipt payload hash is invalid.");
        }

        var fullPath = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Receipt output must use the .json extension.", nameof(outputPath));
        }

        var parent = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        Directory.CreateDirectory(parent);

        using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(ReceiptSerialization.ToJson(receipt));
        writer.WriteLine();
        writer.Flush();
        stream.Flush(flushToDisk: true);
        return fullPath;
    }
}

public static class EnvironmentReceiptWriter
{
    public static string WriteNew(string outputPath, EnvironmentInventoryReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!EnvironmentReceiptVerifier.Verify(receipt).Succeeded)
        {
            throw new InvalidOperationException("Environment receipt integrity is invalid.");
        }

        var fullPath = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Receipt output must use the .json extension.", nameof(outputPath));
        }

        var parent = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        Directory.CreateDirectory(parent);
        using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(ReceiptSerialization.ToJson(receipt));
        writer.WriteLine();
        writer.Flush();
        stream.Flush(flushToDisk: true);
        return fullPath;
    }
}

public static class OfficeLiteCycleReceiptWriter
{
    public static string WriteNew(string outputPath, OfficeLiteCycleReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!OfficeLiteCycleReceiptVerifier.Verify(receipt).Succeeded)
        {
            throw new InvalidOperationException("OfficeLite cycle receipt integrity is invalid.");
        }

        var fullPath = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Receipt output must use the .json extension.", nameof(outputPath));
        }

        var parent = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        Directory.CreateDirectory(parent);
        using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(ReceiptSerialization.ToJson(receipt));
        writer.WriteLine();
        writer.Flush();
        stream.Flush(flushToDisk: true);
        return fullPath;
    }
}

public static class HyperVOfficeLiteCycleReceiptWriter
{
    public static string WriteNew(string outputPath, HyperVOfficeLiteCycleReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!HyperVOfficeLiteCycleReceiptVerifier.Verify(receipt).Succeeded)
        {
            throw new InvalidOperationException("Hyper-V OfficeLite cycle receipt integrity is invalid.");
        }

        var fullPath = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Receipt output must use the .json extension.", nameof(outputPath));
        }

        var parent = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        Directory.CreateDirectory(parent);
        using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(ReceiptSerialization.ToJson(receipt));
        writer.WriteLine();
        writer.Flush();
        stream.Flush(flushToDisk: true);
        return fullPath;
    }
}
