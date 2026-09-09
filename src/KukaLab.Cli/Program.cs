using KukaLab.Core;

return Run(args);

static int Run(string[] args)
{
    if (args.Length >= 2
        && string.Equals(args[0], "fixture", StringComparison.Ordinal)
        && string.Equals(args[1], "verify", StringComparison.Ordinal))
    {
        return RunFixtureVerify(args[2..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "receipt", StringComparison.Ordinal)
        && string.Equals(args[1], "verify", StringComparison.Ordinal))
    {
        return RunReceiptVerify(args[2..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "environment", StringComparison.Ordinal)
        && string.Equals(args[1], "inventory", StringComparison.Ordinal))
    {
        return RunEnvironmentInventory(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "environment", StringComparison.Ordinal)
        && string.Equals(args[1], "receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunEnvironmentReceiptVerify(args[3..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "environment", StringComparison.Ordinal)
        && string.Equals(args[1], "receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "diff", StringComparison.Ordinal))
    {
        return RunEnvironmentReceiptDiff(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "controller", StringComparison.Ordinal)
        && string.Equals(args[1], "network-preflight", StringComparison.Ordinal))
    {
        return RunRealControllerNetworkPreflight(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "controller", StringComparison.Ordinal)
        && string.Equals(args[1], "network-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunRealControllerNetworkReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "controller", StringComparison.Ordinal)
        && string.Equals(args[1], "endpoint-probe", StringComparison.Ordinal))
    {
        return RunRealControllerEndpointProbe(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "controller", StringComparison.Ordinal)
        && string.Equals(args[1], "endpoint-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunRealControllerEndpointReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "controller", StringComparison.Ordinal)
        && string.Equals(args[1], "observation-intake", StringComparison.Ordinal))
    {
        return RunControllerObservationIntake(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "controller", StringComparison.Ordinal)
        && string.Equals(args[1], "observation-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunControllerObservationReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "package", StringComparison.Ordinal)
        && string.Equals(args[1], "verify", StringComparison.Ordinal))
    {
        return RunValidationPackageVerify(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "package", StringComparison.Ordinal)
        && string.Equals(args[1], "receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunValidationPackageReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "package", StringComparison.Ordinal)
        && string.Equals(args[1], "controller-compare", StringComparison.Ordinal))
    {
        return RunValidationPackageControllerCompare(args[2..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "package", StringComparison.Ordinal)
        && string.Equals(args[1], "krl-preflight", StringComparison.Ordinal))
    {
        return RunKrlStaticPreflight(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "package", StringComparison.Ordinal)
        && string.Equals(args[1], "krl-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunKrlStaticPreflightReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "krl", StringComparison.Ordinal)
        && string.Equals(args[1], "candidate-intake", StringComparison.Ordinal))
    {
        return RunRawKrlCandidateIntake(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "krl", StringComparison.Ordinal)
        && string.Equals(args[1], "candidate-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunRawKrlCandidateReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "controller", StringComparison.Ordinal)
        && string.Equals(args[1], "project-intake", StringComparison.Ordinal))
    {
        return RunWorkVisualProjectIntake(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "controller", StringComparison.Ordinal)
        && string.Equals(args[1], "project-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunWorkVisualProjectReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "controller", StringComparison.Ordinal)
        && string.Equals(args[1], "project-baseline", StringComparison.Ordinal))
    {
        return RunControllerProjectBaseline(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "controller", StringComparison.Ordinal)
        && string.Equals(args[1], "project-baseline-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunControllerProjectBaselineReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "boot-verify", StringComparison.Ordinal))
    {
        return RunOfficeLiteBootVerify(args[2..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "profile-clone", StringComparison.Ordinal))
    {
        return RunOfficeLiteProfileClone(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "profile-clone-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunOfficeLiteProfileCloneReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "service-diagnose", StringComparison.Ordinal))
    {
        return RunOfficeLiteBootVerify(args[2..], diagnoseWorkVisualServices: true);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "hyperv-cycle", StringComparison.Ordinal))
    {
        return RunHyperVOfficeLiteCycle(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "delivery", StringComparison.Ordinal)
        && string.Equals(args[2], "inspect", StringComparison.Ordinal))
    {
        return RunOfficeLiteDeliveryInspect(args[3..]);
    }

    if (args.Length >= 4
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "delivery", StringComparison.Ordinal)
        && string.Equals(args[2], "receipt", StringComparison.Ordinal)
        && string.Equals(args[3], "verify", StringComparison.Ordinal))
    {
        return RunOfficeLiteDeliveryReceiptVerify(args[4..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "hyperv-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunHyperVOfficeLiteReceiptVerify(args[3..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunOfficeLiteReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "project-inventory", StringComparison.Ordinal))
    {
        return RunOfficeLiteOnlineProjectInventory(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "project-inventory-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunOfficeLiteOnlineProjectInventoryReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "controller-profile-readback", StringComparison.Ordinal))
    {
        return RunOfficeLiteControllerProfileReadback(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "controller-profile-readback-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunOfficeLiteControllerProfileReadbackReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "exact-profile-accept", StringComparison.Ordinal))
    {
        return RunOfficeLiteExactProfileAcceptance(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "exact-profile-acceptance-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunOfficeLiteExactProfileAcceptanceReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "deployment-preflight", StringComparison.Ordinal))
    {
        return RunOfficeLiteDeploymentPreflight(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "deployment-preflight-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunOfficeLiteDeploymentPreflightReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "active-project-download", StringComparison.Ordinal))
    {
        return RunOfficeLiteActiveProjectDownload(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "active-project-download-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunOfficeLiteActiveProjectDownloadReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "repository-inventory", StringComparison.Ordinal))
    {
        return RunOfficeLiteRepositoryInventory(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "repository-inventory-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunOfficeLiteRepositoryInventoryReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "kss-diagnostic", StringComparison.Ordinal))
    {
        return RunOfficeLiteNativeKssDiagnostic(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "kss-diagnostic-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunOfficeLiteNativeKssDiagnosticReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "kss-execution", StringComparison.Ordinal))
    {
        return RunOfficeLiteNativeKssExecution(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "kss-execution-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunOfficeLiteNativeKssExecutionReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "kss-candidate-execute", StringComparison.Ordinal))
    {
        return RunOfficeLiteNativeKssCandidateExecution(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "kss-candidate-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunOfficeLiteNativeKssCandidateExecutionReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "expert-interface-inventory", StringComparison.Ordinal))
    {
        return RunOfficeLiteExpertModeInterfaceInventory(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "officelite", StringComparison.Ordinal)
        && string.Equals(args[1], "expert-interface-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunOfficeLiteExpertModeInterfaceReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "workvisual", StringComparison.Ordinal)
        && string.Equals(args[1], "runner-smoke", StringComparison.Ordinal))
    {
        return RunWorkVisualRunnerSmoke(args[2..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "workvisual", StringComparison.Ordinal)
        && string.Equals(args[1], "interface-inventory", StringComparison.Ordinal))
    {
        return RunWorkVisualInterfaceInventory(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "workvisual", StringComparison.Ordinal)
        && string.Equals(args[1], "interface-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunWorkVisualInterfaceReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "workvisual", StringComparison.Ordinal)
        && string.Equals(args[1], "project-extract", StringComparison.Ordinal))
    {
        return RunWorkVisualProjectExtraction(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "workvisual", StringComparison.Ordinal)
        && string.Equals(args[1], "project-extraction-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunWorkVisualProjectExtractionReceiptVerify(args[3..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "workvisual", StringComparison.Ordinal)
        && string.Equals(args[1], "receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunWorkVisualReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "kukasim", StringComparison.Ordinal)
        && string.Equals(args[1], "component-smoke", StringComparison.Ordinal))
    {
        return RunKukaSimComponentSmoke(args[2..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "kukasim", StringComparison.Ordinal)
        && string.Equals(args[1], "integrated-validate", StringComparison.Ordinal))
    {
        return RunKukaSimIntegratedValidation(args[2..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "kukasim", StringComparison.Ordinal)
        && string.Equals(args[1], "candidate-execute", StringComparison.Ordinal))
    {
        return RunKukaSimCandidateExecution(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "kukasim", StringComparison.Ordinal)
        && string.Equals(args[1], "candidate-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunKukaSimCandidateExecutionReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "kukasim", StringComparison.Ordinal)
        && string.Equals(args[1], "officelite-loop", StringComparison.Ordinal))
    {
        return RunKukaSimOfficeLiteVirtualLoop(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "kukasim", StringComparison.Ordinal)
        && string.Equals(args[1], "officelite-loop-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunKukaSimOfficeLiteVirtualLoopReceiptVerify(args[3..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "kukasim", StringComparison.Ordinal)
        && string.Equals(args[1], "integrated-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunKukaSimIntegratedValidationReceiptVerify(args[3..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "kukasim", StringComparison.Ordinal)
        && string.Equals(args[1], "receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunKukaSimReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "offline-loop", StringComparison.Ordinal)
        && string.Equals(args[1], "compare", StringComparison.Ordinal))
    {
        return RunOfflineLoopComparison(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "offline-loop", StringComparison.Ordinal)
        && string.Equals(args[1], "comparison-receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunOfflineLoopComparisonReceiptVerify(args[3..]);
    }

    if (args.Length >= 2
        && string.Equals(args[0], "stage-one", StringComparison.Ordinal)
        && string.Equals(args[1], "compose", StringComparison.Ordinal))
    {
        return RunStageOneEvidenceBundle(args[2..]);
    }

    if (args.Length >= 3
        && string.Equals(args[0], "stage-one", StringComparison.Ordinal)
        && string.Equals(args[1], "receipt", StringComparison.Ordinal)
        && string.Equals(args[2], "verify", StringComparison.Ordinal))
    {
        return RunStageOneEvidenceBundleReceiptVerify(args[3..]);
    }

    return Usage("Expected a fixture, receipt, environment, controller, officelite, workvisual, kukasim, offline-loop or stage-one command.");
}

static int RunFixtureVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--fixture", "--output", "--attempt-id");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--fixture", out var fixturePath)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("Both --fixture and --output are required.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"local-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var outcome = new FixtureVerifier().Verify(fixturePath, attemptId);
        var outputFullPath = Path.GetFullPath(outputPath);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = [$"CreateNewReceiptFile:{outputFullPath}"]
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputePayloadSha256(payload)
        };
        var writtenPath = ReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"Receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
    {
        Console.Error.WriteLine($"kuka-lab fixture verify failed: {exception.Message}");
        return 73;
    }
}

static int RunReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var json = File.ReadAllText(Path.GetFullPath(receiptPath));
        using var document = System.Text.Json.JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("schemaIdentity", out var schemaIdentityElement))
        {
            throw new InvalidOperationException("Receipt schemaIdentity is required.");
        }

        var schemaIdentity = schemaIdentityElement.GetString();
        return schemaIdentity switch
        {
            FixtureContract.ReceiptSchemaIdentity => VerifyFixtureReceipt(json),
            EnvironmentInventoryContract.ReceiptSchemaIdentity => VerifyEnvironmentReceipt(json),
            OfficeLiteCycleContract.ReceiptSchemaIdentity => VerifyOfficeLiteReceipt(json),
            OfficeLiteProfileCloneContract.ReceiptSchemaIdentity => VerifyOfficeLiteProfileCloneReceipt(json),
            OfficeLiteOnlineProjectInventoryContract.ReceiptSchemaIdentity => VerifyOfficeLiteOnlineProjectInventoryReceipt(json),
            OfficeLiteControllerProfileReadbackContract.ReceiptSchemaIdentity => VerifyOfficeLiteControllerProfileReadbackReceipt(json),
            OfficeLiteExactProfileAcceptanceContract.ReceiptSchemaIdentity => VerifyOfficeLiteExactProfileAcceptanceReceipt(json),
            OfficeLiteActiveProjectDownloadContract.ReceiptSchemaIdentity => VerifyOfficeLiteActiveProjectDownloadReceipt(json),
            OfficeLiteRepositoryInventoryContract.ReceiptSchemaIdentity => VerifyOfficeLiteRepositoryInventoryReceipt(json),
            OfficeLiteNativeKssDiagnosticContract.ReceiptSchemaIdentity => VerifyOfficeLiteNativeKssDiagnosticReceipt(json),
            OfficeLiteNativeKssExecutionContract.ReceiptSchemaIdentity => VerifyOfficeLiteNativeKssExecutionReceipt(json),
            HyperVOfficeLiteCycleContract.ReceiptSchemaIdentity => VerifyHyperVOfficeLiteReceipt(json),
            OfficeLiteDeliveryInspectionContract.ReceiptSchemaIdentity => VerifyOfficeLiteDeliveryReceipt(json),
            WorkVisualRunnerSmokeContract.ReceiptSchemaIdentity => VerifyWorkVisualReceipt(json),
            WorkVisualInterfaceInventoryContract.ReceiptSchemaIdentity => VerifyWorkVisualInterfaceReceipt(json),
            OfficeLiteExpertModeInterfaceInventoryContract.ReceiptSchemaIdentity =>
                VerifyOfficeLiteExpertModeInterfaceReceipt(json),
            KukaSimComponentSmokeContract.ReceiptSchemaIdentity => VerifyKukaSimReceipt(json),
            KukaSimIntegratedValidationContract.ReceiptSchemaIdentity => VerifyKukaSimIntegratedValidationReceipt(json),
            KukaSimOfficeLiteVirtualLoopContract.ReceiptSchemaIdentity => VerifyKukaSimOfficeLiteVirtualLoopReceipt(json),
            OfflineLoopComparisonContract.ReceiptSchemaIdentity => VerifyOfflineLoopComparisonReceipt(json),
            RealControllerNetworkPreflightContract.ReceiptSchemaIdentity => VerifyRealControllerNetworkReceipt(json),
            RealControllerEndpointProbeContract.ReceiptSchemaIdentity => VerifyRealControllerEndpointReceipt(json),
            ControllerObservationContract.ReceiptSchemaIdentity => VerifyControllerObservationReceipt(json),
            WorkVisualProjectIntakeContract.ReceiptSchemaIdentity => VerifyWorkVisualProjectIntakeReceipt(json),
            WorkVisualProjectExtractionContract.ReceiptSchemaIdentity => VerifyWorkVisualProjectExtractionReceipt(json),
            ControllerProjectBaselineContract.ReceiptSchemaIdentity => VerifyControllerProjectBaselineReceipt(json),
            ValidationPackageContract.ReceiptSchemaIdentity => VerifyValidationPackageReceipt(json),
            KrlStaticPreflightContract.ReceiptSchemaIdentity => VerifyKrlStaticPreflightReceipt(json),
            RawKrlCandidateContract.ReceiptSchemaIdentity => VerifyRawKrlCandidateReceipt(json),
            StageOneEvidenceBundleContract.ReceiptSchemaIdentity => VerifyStageOneEvidenceBundleReceipt(json),
            _ => throw new InvalidOperationException($"Unsupported receipt schemaIdentity: {schemaIdentity}")
        };
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int VerifyFixtureReceipt(string json)
{
    var result = ReceiptVerifier.Verify(ReceiptSerialization.FromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyEnvironmentReceipt(string json)
{
    var result = EnvironmentReceiptVerifier.Verify(ReceiptSerialization.EnvironmentFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyOfficeLiteReceipt(string json)
{
    var result = OfficeLiteCycleReceiptVerifier.Verify(ReceiptSerialization.OfficeLiteCycleFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyOfficeLiteProfileCloneReceipt(string json)
{
    var result = OfficeLiteProfileCloneReceiptVerifier.Verify(
        ReceiptSerialization.OfficeLiteProfileCloneFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyOfficeLiteOnlineProjectInventoryReceipt(string json)
{
    var result = OfficeLiteOnlineProjectInventoryReceiptVerifier.Verify(
        ReceiptSerialization.OfficeLiteOnlineProjectInventoryFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyOfficeLiteControllerProfileReadbackReceipt(string json)
{
    var result = OfficeLiteControllerProfileReadbackReceiptVerifier.Verify(
        ReceiptSerialization.OfficeLiteControllerProfileReadbackFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyOfficeLiteExactProfileAcceptanceReceipt(string json)
{
    var result = OfficeLiteExactProfileAcceptanceReceiptVerifier.Verify(
        ReceiptSerialization.OfficeLiteExactProfileAcceptanceFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyOfficeLiteActiveProjectDownloadReceipt(string json)
{
    var result = OfficeLiteActiveProjectDownloadReceiptVerifier.Verify(
        ReceiptSerialization.OfficeLiteActiveProjectDownloadFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyOfficeLiteRepositoryInventoryReceipt(string json)
{
    var result = OfficeLiteRepositoryInventoryReceiptVerifier.Verify(
        ReceiptSerialization.OfficeLiteRepositoryInventoryFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyOfficeLiteNativeKssDiagnosticReceipt(string json)
{
    var result = OfficeLiteNativeKssDiagnosticReceiptVerifier.Verify(
        ReceiptSerialization.OfficeLiteNativeKssDiagnosticFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyOfficeLiteNativeKssExecutionReceipt(string json)
{
    var result = OfficeLiteNativeKssExecutionReceiptVerifier.Verify(
        ReceiptSerialization.OfficeLiteNativeKssExecutionFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyHyperVOfficeLiteReceipt(string json)
{
    var result = HyperVOfficeLiteCycleReceiptVerifier.Verify(
        ReceiptSerialization.HyperVOfficeLiteCycleFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyOfficeLiteDeliveryReceipt(string json)
{
    var result = OfficeLiteDeliveryReceiptVerifier.VerifyIntegrity(
        ReceiptSerialization.OfficeLiteDeliveryInspectionFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyWorkVisualReceipt(string json)
{
    var result = WorkVisualRunnerSmokeReceiptVerifier.Verify(ReceiptSerialization.WorkVisualRunnerSmokeFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyWorkVisualInterfaceReceipt(string json)
{
    var result = WorkVisualInterfaceInventoryReceiptVerifier.Verify(
        ReceiptSerialization.WorkVisualInterfaceInventoryFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyOfficeLiteExpertModeInterfaceReceipt(string json)
{
    var result = OfficeLiteExpertModeInterfaceInventoryReceiptVerifier.Verify(
        ReceiptSerialization.OfficeLiteExpertModeInterfaceInventoryFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyKukaSimReceipt(string json)
{
    var result = KukaSimComponentSmokeReceiptVerifier.Verify(ReceiptSerialization.KukaSimComponentSmokeFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyKukaSimIntegratedValidationReceipt(string json)
{
    var result = KukaSimIntegratedValidationReceiptVerifier.Verify(
        ReceiptSerialization.KukaSimIntegratedValidationFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyKukaSimOfficeLiteVirtualLoopReceipt(string json)
{
    var result = KukaSimOfficeLiteVirtualLoopReceiptVerifier.Verify(
        ReceiptSerialization.KukaSimOfficeLiteVirtualLoopFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyOfflineLoopComparisonReceipt(string json)
{
    var result = OfflineLoopComparisonReceiptVerifier.Verify(
        ReceiptSerialization.OfflineLoopComparisonFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyRealControllerNetworkReceipt(string json)
{
    var result = RealControllerNetworkPreflightReceiptVerifier.Verify(
        ReceiptSerialization.RealControllerNetworkPreflightFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyRealControllerEndpointReceipt(string json)
{
    var result = RealControllerEndpointProbeReceiptVerifier.Verify(
        ReceiptSerialization.RealControllerEndpointProbeFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyControllerObservationReceipt(string json)
{
    var result = ControllerObservationReceiptVerifier.Verify(
        ReceiptSerialization.ControllerObservationFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyWorkVisualProjectIntakeReceipt(string json)
{
    var result = WorkVisualProjectIntakeReceiptVerifier.VerifyIntegrity(
        ReceiptSerialization.WorkVisualProjectIntakeFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyWorkVisualProjectExtractionReceipt(string json)
{
    var result = WorkVisualProjectExtractionReceiptVerifier.Verify(
        ReceiptSerialization.WorkVisualProjectExtractionFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyControllerProjectBaselineReceipt(string json)
{
    var result = ControllerProjectBaselineReceiptVerifier.Verify(
        ReceiptSerialization.ControllerProjectBaselineFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyValidationPackageReceipt(string json)
{
    var result = ValidationPackageReceiptVerifier.VerifyIntegrity(
        ReceiptSerialization.ValidationPackageFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyKrlStaticPreflightReceipt(string json)
{
    var result = KrlStaticPreflightReceiptVerifier.Verify(
        ReceiptSerialization.KrlStaticPreflightFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int VerifyRawKrlCandidateReceipt(string json)
{
    var result = RawKrlCandidateReceiptVerifier.VerifyIntegrity(
        ReceiptSerialization.RawKrlCandidateFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static int RunEnvironmentInventory(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--asset-root",
        "--output",
        "--attempt-id",
        "--kuka-sim",
        "--workvisual",
        "--vmrun",
        "--require-exact-c01-component");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--asset-root", out var assetRoot)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("Both --asset-root and --output are required.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"environment-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var requireExactC01 = options.TryGetValue("--require-exact-c01-component", out var requireExactText)
            && bool.TryParse(requireExactText, out var parsedRequireExact)
            && parsedRequireExact;
        if (options.TryGetValue("--require-exact-c01-component", out requireExactText)
            && !bool.TryParse(requireExactText, out _))
        {
            return Usage("--require-exact-c01-component must be true or false.");
        }
        var request = EnvironmentInventoryRequest.CreateDefault(
            assetRoot,
            options.GetValueOrDefault("--kuka-sim"),
            options.GetValueOrDefault("--workvisual"),
            options.GetValueOrDefault("--vmrun")) with
        {
            RequireExactC01RobotComponent = requireExactC01
        };
        var outcome = new EnvironmentInventoryCollector().Collect(request, attemptId);
        var outputFullPath = Path.GetFullPath(outputPath);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = [$"CreateNewReceiptFile:{outputFullPath}"]
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = EnvironmentReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"Environment receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
    {
        Console.Error.WriteLine($"kuka-lab environment inventory failed: {exception.Message}");
        return 73;
    }
}

static int RunEnvironmentReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.EnvironmentFromJson(File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = EnvironmentReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab environment receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunEnvironmentReceiptDiff(string[] args)
{
    var parseResult = ParseOptions(args, "--baseline", "--current");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--baseline", out var baselinePath)
        || !parseResult.Options.TryGetValue("--current", out var currentPath))
    {
        return Usage("Both --baseline and --current are required.");
    }

    try
    {
        var baseline = ReceiptSerialization.EnvironmentFromJson(
            File.ReadAllText(Path.GetFullPath(baselinePath)));
        var current = ReceiptSerialization.EnvironmentFromJson(
            File.ReadAllText(Path.GetFullPath(currentPath)));
        var result = EnvironmentReceiptDiffer.Compare(baseline, current);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        if (!result.Succeeded)
        {
            return 2;
        }

        return result.HasDifferences ? 4 : 0;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab environment receipt diff failed: {exception.Message}");
        return 73;
    }
}

static int RunValidationPackageVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--package", "--output", "--attempt-id");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--package", out var packageRoot)
        || !parseResult.Options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--package and --output are required.");
    }

    var attemptId = parseResult.Options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"validation-package-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var outcome = new ValidationPackageVerifier().Verify(
            new ValidationPackageVerificationRequest
            {
                PackageRoot = packageRoot,
                ReceiptOutputPath = outputPath
            },
            attemptId);
        var writtenPath = ValidationPackageReceiptWriter.WriteNew(
            outputPath,
            packageRoot,
            outcome.Receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(outcome.Receipt));
        Console.Error.WriteLine($"ValidationPackage integrity receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidDataException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab package verify failed: {exception.Message}");
        return 73;
    }
}

static int RunValidationPackageReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt", "--package");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.ValidationPackageFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = parseResult.Options.TryGetValue("--package", out var packageRoot)
            ? ValidationPackageReceiptVerifier.VerifyCurrentPackage(receipt, packageRoot)
            : ValidationPackageReceiptVerifier.VerifyIntegrity(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidDataException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab package receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunKrlStaticPreflight(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--package",
        "--package-receipt",
        "--output",
        "--attempt-id");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--package", out var packageRoot)
        || !parseResult.Options.TryGetValue("--package-receipt", out var packageReceiptPath)
        || !parseResult.Options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--package, --package-receipt and --output are required.");
    }

    var attemptId = parseResult.Options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"krl-static-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var request = new KrlStaticPreflightRequest
        {
            PackageRoot = packageRoot,
            ValidationPackageReceiptPath = packageReceiptPath,
            ReceiptOutputPath = outputPath
        };
        var outcome = new KrlStaticPreflightRunner().Run(request, attemptId);
        var packageReceipt = ReceiptSerialization.ValidationPackageFromJson(
            File.ReadAllText(Path.GetFullPath(packageReceiptPath)));
        var writtenPath = KrlStaticPreflightReceiptWriter.WriteNew(
            outputPath,
            packageRoot,
            packageReceipt,
            outcome.Receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(outcome.Receipt));
        Console.Error.WriteLine($"KRL static-preflight receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidDataException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab package krl-preflight failed: {exception.Message}");
        return 73;
    }
}

static int RunKrlStaticPreflightReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.KrlStaticPreflightFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = KrlStaticPreflightReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidDataException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab package krl-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunRawKrlCandidateIntake(string[] args)
{
    var parseResult = ParseOptions(args, "--source", "--output", "--attempt-id");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--source", out var sourceRoot)
        || !parseResult.Options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--source and --output are required.");
    }

    var attemptId = parseResult.Options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"raw-krl-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var request = new RawKrlCandidateRequest
        {
            SourceRoot = sourceRoot,
            ReceiptOutputPath = outputPath
        };
        var outcome = new RawKrlCandidateRunner().Run(request, attemptId);
        var writtenPath = RawKrlCandidateReceiptWriter.WriteNew(
            outputPath,
            sourceRoot,
            outcome.Receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(outcome.Receipt));
        Console.Error.WriteLine($"Raw KRL candidate receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidDataException
        or InvalidOperationException)
    {
        Console.Error.WriteLine($"kuka-lab krl candidate-intake failed: {exception.Message}");
        return 73;
    }
}

static int RunRawKrlCandidateReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt", "--source");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.RawKrlCandidateFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = parseResult.Options.TryGetValue("--source", out var sourceRoot)
            ? RawKrlCandidateReceiptVerifier.VerifyCurrentSource(receipt, sourceRoot)
            : RawKrlCandidateReceiptVerifier.VerifyIntegrity(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidDataException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab krl candidate-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunValidationPackageControllerCompare(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--package-receipt",
        "--controller-observation-receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--package-receipt", out var packageReceiptPath)
        || !parseResult.Options.TryGetValue(
            "--controller-observation-receipt",
            out var observationReceiptPath))
    {
        return Usage("--package-receipt and --controller-observation-receipt are required.");
    }

    try
    {
        var packageReceipt = ReceiptSerialization.ValidationPackageFromJson(
            File.ReadAllText(Path.GetFullPath(packageReceiptPath)));
        var observationReceipt = ReceiptSerialization.ControllerObservationFromJson(
            File.ReadAllText(Path.GetFullPath(observationReceiptPath)));
        var result = ControllerCompatibilityComparer.Compare(packageReceipt, observationReceipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidDataException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab package controller-compare failed: {exception.Message}");
        return 73;
    }
}

static int RunRealControllerNetworkPreflight(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--interface",
        "--internet-interface",
        "--controller-ip",
        "--host-ip",
        "--prefix-length",
        "--output",
        "--attempt-id");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--interface", out var interfaceAlias)
        || !options.TryGetValue("--internet-interface", out var internetInterfaceAlias)
        || !options.TryGetValue("--controller-ip", out var controllerAddress)
        || !options.TryGetValue("--host-ip", out var proposedHostAddress)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--interface, --internet-interface, --controller-ip, --host-ip and --output are required.");
    }

    var prefixLength = 24;
    if (options.TryGetValue("--prefix-length", out var prefixText)
        && (!int.TryParse(prefixText, out prefixLength) || prefixLength is < 1 or > 30))
    {
        return Usage("--prefix-length must be an integer from 1 through 30.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"controller-network-preflight-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var request = new RealControllerNetworkPreflightRequest
        {
            TargetInterfaceAlias = interfaceAlias,
            InternetInterfaceAlias = internetInterfaceAlias,
            ControllerAddress = controllerAddress,
            ProposedHostAddress = proposedHostAddress,
            PrefixLength = prefixLength
        };
        var outcome = new RealControllerNetworkPreflightRunner().Run(request, attemptId);
        var outputFullPath = Path.GetFullPath(outputPath);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = [$"CreateNewReceiptFile:{outputFullPath}"]
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = RealControllerNetworkPreflightReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"Real-controller network preflight receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab controller network-preflight failed: {exception.Message}");
        return 73;
    }
}

static int RunRealControllerNetworkReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.RealControllerNetworkPreflightFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = RealControllerNetworkPreflightReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab controller network-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunRealControllerEndpointProbe(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--interface",
        "--internet-interface",
        "--controller-ip",
        "--host-ip",
        "--prefix-length",
        "--port",
        "--timeout-milliseconds",
        "--output",
        "--attempt-id");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--interface", out var interfaceAlias)
        || !options.TryGetValue("--internet-interface", out var internetInterfaceAlias)
        || !options.TryGetValue("--controller-ip", out var controllerAddress)
        || !options.TryGetValue("--host-ip", out var hostAddress)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--interface, --internet-interface, --controller-ip, --host-ip and --output are required.");
    }

    var prefixLength = 24;
    if (options.TryGetValue("--prefix-length", out var prefixText)
        && (!int.TryParse(prefixText, out prefixLength) || prefixLength is < 1 or > 30))
    {
        return Usage("--prefix-length must be an integer from 1 through 30.");
    }

    var port = RealControllerEndpointProbeContract.DeviceInfoPort;
    if (options.TryGetValue("--port", out var portText)
        && (!int.TryParse(portText, out port)
            || port != RealControllerEndpointProbeContract.DeviceInfoPort))
    {
        return Usage($"--port must be exactly {RealControllerEndpointProbeContract.DeviceInfoPort}.");
    }

    var timeoutMilliseconds = 2_000;
    if (options.TryGetValue("--timeout-milliseconds", out var timeoutText)
        && (!int.TryParse(timeoutText, out timeoutMilliseconds)
            || timeoutMilliseconds is < RealControllerEndpointProbeContract.MinimumTimeoutMilliseconds
                or > RealControllerEndpointProbeContract.MaximumTimeoutMilliseconds))
    {
        return Usage(
            $"--timeout-milliseconds must be from {RealControllerEndpointProbeContract.MinimumTimeoutMilliseconds} through {RealControllerEndpointProbeContract.MaximumTimeoutMilliseconds}.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"controller-endpoint-probe-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var request = new RealControllerEndpointProbeRequest
        {
            TargetInterfaceAlias = interfaceAlias,
            InternetInterfaceAlias = internetInterfaceAlias,
            ControllerAddress = controllerAddress,
            HostAddress = hostAddress,
            PrefixLength = prefixLength,
            Port = port,
            TimeoutMilliseconds = timeoutMilliseconds
        };
        var outcome = new RealControllerEndpointProbeRunner()
            .RunAsync(request, attemptId)
            .GetAwaiter()
            .GetResult();
        var outputFullPath = Path.GetFullPath(outputPath);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = outcome.Receipt.Payload.SideEffects
                .Append($"CreateNewReceiptFile:{outputFullPath}")
                .ToList()
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = RealControllerEndpointProbeReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"Real-controller endpoint-probe receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Net.Sockets.SocketException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab controller endpoint-probe failed: {exception.Message}");
        return 73;
    }
}

static int RunRealControllerEndpointReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.RealControllerEndpointProbeFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = RealControllerEndpointProbeReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab controller endpoint-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunControllerObservationIntake(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--controller-family",
        "--cabinet-model",
        "--kss-version",
        "--kss-build",
        "--kli-address",
        "--prefix-length",
        "--observed-on",
        "--evidence-references",
        "--output",
        "--attempt-id");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--controller-family", out var controllerFamily)
        || !options.TryGetValue("--cabinet-model", out var cabinetModel)
        || !options.TryGetValue("--kss-version", out var kssVersion)
        || !options.TryGetValue("--kss-build", out var kssBuild)
        || !options.TryGetValue("--kli-address", out var kliAddress)
        || !options.TryGetValue("--observed-on", out var observedOn)
        || !options.TryGetValue("--evidence-references", out var evidenceReferenceText)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("Controller family, cabinet model, KSS version/build, KLI address, observation date, evidence references and output are required.");
    }

    var prefixLength = 24;
    if (options.TryGetValue("--prefix-length", out var prefixText)
        && (!int.TryParse(prefixText, out prefixLength) || prefixLength is < 1 or > 30))
    {
        return Usage("--prefix-length must be an integer from 1 through 30.");
    }

    var evidenceReferences = evidenceReferenceText.Split(
        ',',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"controller-observation-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var outcome = new ControllerObservationRunner().Run(
            new ControllerObservationRequest
            {
                ControllerFamily = controllerFamily,
                CabinetModel = cabinetModel,
                KssVersion = kssVersion,
                KssBuild = kssBuild,
                KliAddress = kliAddress,
                PrefixLength = prefixLength,
                ObservedOnLocalDate = observedOn,
                EvidenceReferences = evidenceReferences
            },
            attemptId);
        var outputFullPath = Path.GetFullPath(outputPath);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = [$"CreateNewReceiptFile:{outputFullPath}"]
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = ControllerObservationReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"Controller-observation receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab controller observation-intake failed: {exception.Message}");
        return 73;
    }
}

static int RunControllerObservationReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.ControllerObservationFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = ControllerObservationReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab controller observation-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunWorkVisualProjectIntake(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--project",
        "--capture-reference",
        "--output",
        "--attempt-id");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--project", out var projectPath)
        || !options.TryGetValue("--capture-reference", out var captureReference)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--project, --capture-reference and --output are required.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"workvisual-project-intake-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var request = new WorkVisualProjectIntakeRequest
        {
            ProjectPath = projectPath,
            ReceiptOutputPath = outputPath,
            CaptureReference = captureReference
        };
        var outcome = new WorkVisualProjectIntakeRunner().Run(request, attemptId);
        var writtenPath = WorkVisualProjectIntakeReceiptWriter.WriteNew(
            outputPath,
            projectPath,
            outcome.Receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(outcome.Receipt));
        Console.Error.WriteLine($"WorkVisual project-intake receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidDataException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab controller project-intake failed: {exception.Message}");
        return 73;
    }
}

static int RunWorkVisualProjectReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt", "--project");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.WorkVisualProjectIntakeFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = parseResult.Options.TryGetValue("--project", out var projectPath)
            ? WorkVisualProjectIntakeReceiptVerifier.VerifyCurrentProject(receipt, projectPath)
            : WorkVisualProjectIntakeReceiptVerifier.VerifyIntegrity(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidDataException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab controller project-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteProfileClone(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--asset-root",
        "--target-vmx",
        "--clone-name",
        "--authorization-ref",
        "--output",
        "--attempt-id",
        "--vmrun",
        "--source-vmx",
        "--snapshot-name",
        "--timeout-seconds");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--asset-root", out var assetRoot)
        || !options.TryGetValue("--target-vmx", out var targetVmx)
        || !options.TryGetValue("--clone-name", out var cloneName)
        || !options.TryGetValue("--authorization-ref", out var authorizationRef)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--asset-root, --target-vmx, --clone-name, --authorization-ref and --output are required.");
    }

    var timeoutSeconds = 900;
    if (options.TryGetValue("--timeout-seconds", out var timeoutText)
        && (!int.TryParse(timeoutText, out timeoutSeconds) || timeoutSeconds is < 30 or > 3600))
    {
        return Usage("--timeout-seconds must be an integer from 30 through 3600.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"officelite-profile-clone-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var request = OfficeLiteProfileCloneRequest.CreateDefault(
            assetRoot,
            targetVmx,
            cloneName,
            authorizationRef,
            options.GetValueOrDefault("--vmrun"),
            options.GetValueOrDefault("--source-vmx"),
            options.GetValueOrDefault("--snapshot-name"),
            timeoutSeconds);
        var outcome = new OfficeLiteProfileCloneRunner().Run(request, attemptId);
        var outputFullPath = Path.GetFullPath(outputPath);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = outcome.Receipt.Payload.SideEffects
                .Append($"CreateNewReceiptFile:{outputFullPath}")
                .ToList()
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = OfficeLiteProfileCloneReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"OfficeLite profile-clone receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidDataException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab officelite profile-clone failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteProfileCloneReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.OfficeLiteProfileCloneFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = OfficeLiteProfileCloneReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidDataException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab officelite profile-clone receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteBootVerify(string[] args, bool diagnoseWorkVisualServices = false)
{
    var supportedOptions = new List<string>
    {
        "--asset-root",
        "--output",
        "--attempt-id",
        "--vmrun",
        "--vmx",
        "--guest-ip",
        "--dhcp-leases",
        "--timeout-seconds"
    };
    if (diagnoseWorkVisualServices)
    {
        supportedOptions.Add("--observation-seconds");
    }

    var parseResult = ParseOptions(args, supportedOptions.ToArray());
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--asset-root", out var assetRoot)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("Both --asset-root and --output are required.");
    }

    var timeoutSeconds = 120;
    if (options.TryGetValue("--timeout-seconds", out var timeoutText)
        && (!int.TryParse(timeoutText, out timeoutSeconds) || timeoutSeconds is < 1 or > 900))
    {
        return Usage("--timeout-seconds must be an integer from 1 through 900.");
    }

    var observationSeconds = diagnoseWorkVisualServices ? 180 : 0;
    if (options.TryGetValue("--observation-seconds", out var observationText)
        && (!int.TryParse(observationText, out observationSeconds)
            || observationSeconds is < 10 or > 600))
    {
        return Usage("--observation-seconds must be an integer from 10 through 600.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"officelite-{(diagnoseWorkVisualServices ? "service" : "boot")}-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var request = OfficeLiteCycleRequest.CreateDefault(
            assetRoot,
            options.GetValueOrDefault("--vmrun"),
            options.GetValueOrDefault("--guest-ip"),
            options.GetValueOrDefault("--dhcp-leases"),
            timeoutSeconds);
        if (options.TryGetValue("--vmx", out var vmxPath))
        {
            request = request with { VmxPath = Path.GetFullPath(vmxPath) };
        }

        request = request with
        {
            DiagnoseWorkVisualServices = diagnoseWorkVisualServices,
            ServiceObservationSeconds = observationSeconds
        };
        var outcome = new OfficeLiteCycleRunner().Run(request, attemptId);
        var outputFullPath = Path.GetFullPath(outputPath);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = outcome.Receipt.Payload.SideEffects
                .Append($"CreateNewReceiptFile:{outputFullPath}")
                .ToList()
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = OfficeLiteCycleReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"OfficeLite {(diagnoseWorkVisualServices ? "service diagnostic" : "cycle")} receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException)
    {
        Console.Error.WriteLine($"kuka-lab officelite {(diagnoseWorkVisualServices ? "service-diagnose" : "boot-verify")} failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.OfficeLiteCycleFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = OfficeLiteCycleReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab officelite receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteOnlineProjectInventory(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--asset-root",
        "--output",
        "--attempt-id",
        "--vmrun",
        "--vmx",
        "--runner",
        "--guest-ip",
        "--dhcp-leases",
        "--timeout-seconds",
        "--observation-seconds",
        "--runner-timeout-seconds");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--asset-root", out var assetRoot)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("Both --asset-root and --output are required.");
    }

    var timeoutSeconds = 240;
    if (options.TryGetValue("--timeout-seconds", out var timeoutText)
        && (!int.TryParse(timeoutText, out timeoutSeconds) || timeoutSeconds is < 1 or > 900))
    {
        return Usage("--timeout-seconds must be an integer from 1 through 900.");
    }

    var observationSeconds = 180;
    if (options.TryGetValue("--observation-seconds", out var observationText)
        && (!int.TryParse(observationText, out observationSeconds) || observationSeconds is < 10 or > 600))
    {
        return Usage("--observation-seconds must be an integer from 10 through 600.");
    }

    var runnerTimeoutSeconds = 30;
    if (options.TryGetValue("--runner-timeout-seconds", out var runnerTimeoutText)
        && (!int.TryParse(runnerTimeoutText, out runnerTimeoutSeconds) || runnerTimeoutSeconds is < 1 or > 120))
    {
        return Usage("--runner-timeout-seconds must be an integer from 1 through 120.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"officelite-project-inventory-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var outputFullPath = Path.GetFullPath(outputPath);
        if (File.Exists(outputFullPath))
        {
            throw new IOException("Receipt output already exists; OfficeLite was not started.");
        }

        var outputDirectory = Path.GetDirectoryName(outputFullPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        var evidenceDirectory = Path.Combine(
            outputDirectory,
            $"{Path.GetFileNameWithoutExtension(outputFullPath)}.evidence");
        var request = OfficeLiteOnlineProjectInventoryRequest.CreateDefault(
            assetRoot,
            evidenceDirectory,
            options.GetValueOrDefault("--vmrun"),
            options.GetValueOrDefault("--runner"),
            options.GetValueOrDefault("--guest-ip"),
            timeoutSeconds,
            observationSeconds,
            runnerTimeoutSeconds);
        if (options.TryGetValue("--vmx", out var vmxPath))
        {
            request = request with
            {
                OfficeLite = request.OfficeLite with { VmxPath = Path.GetFullPath(vmxPath) }
            };
        }
        if (options.TryGetValue("--dhcp-leases", out var dhcpLeasePath))
        {
            request = request with
            {
                OfficeLite = request.OfficeLite with { DhcpLeasePath = Path.GetFullPath(dhcpLeasePath) }
            };
        }

        var outcome = new OfficeLiteOnlineProjectInventoryRunner().Run(request, attemptId);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = outcome.Receipt.Payload.SideEffects
                .Append($"CreateNewReceiptFile:{outputFullPath}")
                .ToList()
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = OfficeLiteOnlineProjectInventoryReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"OfficeLite online project inventory receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException)
    {
        Console.Error.WriteLine($"kuka-lab officelite project-inventory failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteOnlineProjectInventoryReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.OfficeLiteOnlineProjectInventoryFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = OfficeLiteOnlineProjectInventoryReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab officelite project-inventory-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteControllerProfileReadback(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--asset-root",
        "--output",
        "--attempt-id",
        "--vmrun",
        "--vmx",
        "--runner",
        "--guest-ip",
        "--dhcp-leases",
        "--timeout-seconds",
        "--observation-seconds",
        "--runner-timeout-seconds");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--asset-root", out var assetRoot)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("Both --asset-root and --output are required.");
    }

    var timeoutSeconds = 240;
    if (options.TryGetValue("--timeout-seconds", out var timeoutText)
        && (!int.TryParse(timeoutText, out timeoutSeconds) || timeoutSeconds is < 1 or > 900))
    {
        return Usage("--timeout-seconds must be an integer from 1 through 900.");
    }

    var observationSeconds = 180;
    if (options.TryGetValue("--observation-seconds", out var observationText)
        && (!int.TryParse(observationText, out observationSeconds) || observationSeconds is < 10 or > 600))
    {
        return Usage("--observation-seconds must be an integer from 10 through 600.");
    }

    var runnerTimeoutSeconds = 30;
    if (options.TryGetValue("--runner-timeout-seconds", out var runnerTimeoutText)
        && (!int.TryParse(runnerTimeoutText, out runnerTimeoutSeconds) || runnerTimeoutSeconds is < 1 or > 120))
    {
        return Usage("--runner-timeout-seconds must be an integer from 1 through 120.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"officelite-controller-profile-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var outputFullPath = Path.GetFullPath(outputPath);
        if (File.Exists(outputFullPath))
        {
            throw new IOException("Receipt output already exists; OfficeLite was not started.");
        }

        var outputDirectory = Path.GetDirectoryName(outputFullPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        var evidenceDirectory = Path.Combine(
            outputDirectory,
            $"{Path.GetFileNameWithoutExtension(outputFullPath)}.evidence");
        var request = OfficeLiteControllerProfileReadbackRequest.CreateDefault(
            assetRoot,
            evidenceDirectory,
            options.GetValueOrDefault("--vmrun"),
            options.GetValueOrDefault("--runner"),
            options.GetValueOrDefault("--guest-ip"),
            timeoutSeconds,
            observationSeconds,
            runnerTimeoutSeconds);
        if (options.TryGetValue("--vmx", out var vmxPath))
        {
            request = request with
            {
                OfficeLite = request.OfficeLite with { VmxPath = Path.GetFullPath(vmxPath) }
            };
        }

        if (options.TryGetValue("--dhcp-leases", out var dhcpLeasePath))
        {
            request = request with
            {
                OfficeLite = request.OfficeLite with { DhcpLeasePath = Path.GetFullPath(dhcpLeasePath) }
            };
        }

        var outcome = new OfficeLiteControllerProfileReadbackRunner().Run(request, attemptId);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = outcome.Receipt.Payload.SideEffects
                .Append($"CreateNewReceiptFile:{outputFullPath}")
                .ToList()
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = OfficeLiteControllerProfileReadbackReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"OfficeLite controller-profile readback receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException)
    {
        Console.Error.WriteLine($"kuka-lab officelite controller-profile-readback failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteControllerProfileReadbackReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.OfficeLiteControllerProfileReadbackFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = OfficeLiteControllerProfileReadbackReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab officelite controller-profile-readback-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteExactProfileAcceptance(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--profile-readback",
        "--active-project-download",
        "--active-controller-baseline",
        "--trusted-controller-baseline",
        "--expected-project",
        "--output",
        "--attempt-id");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--profile-readback", out var profileReadbackPath)
        || !options.TryGetValue("--active-project-download", out var activeProjectDownloadPath)
        || !options.TryGetValue("--active-controller-baseline", out var activeControllerBaselinePath)
        || !options.TryGetValue("--trusted-controller-baseline", out var trustedControllerBaselinePath)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--profile-readback, --active-project-download, --active-controller-baseline, --trusted-controller-baseline and --output are required.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"officelite-exact-profile-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";
    var expectedProject = options.GetValueOrDefault(
        "--expected-project",
        OfficeLiteExactProfileAcceptanceContract.DefaultExpectedProjectName);

    try
    {
        var outputFullPath = Path.GetFullPath(outputPath);
        if (File.Exists(outputFullPath))
        {
            throw new IOException("Receipt output already exists; no input receipt was composed.");
        }

        var request = new OfficeLiteExactProfileAcceptanceRequest
        {
            ProfileReadbackReceipt = ReceiptSerialization.OfficeLiteControllerProfileReadbackFromJson(
                File.ReadAllText(Path.GetFullPath(profileReadbackPath))),
            ActiveProjectDownloadReceipt = ReceiptSerialization.OfficeLiteActiveProjectDownloadFromJson(
                File.ReadAllText(Path.GetFullPath(activeProjectDownloadPath))),
            ActiveControllerBaselineReceipt = ReceiptSerialization.ControllerProjectBaselineFromJson(
                File.ReadAllText(Path.GetFullPath(activeControllerBaselinePath))),
            TrustedControllerBaselineReceipt = ReceiptSerialization.ControllerProjectBaselineFromJson(
                File.ReadAllText(Path.GetFullPath(trustedControllerBaselinePath))),
            ExpectedProjectName = expectedProject
        };
        var outcome = new OfficeLiteExactProfileAcceptanceRunner().Run(request, attemptId);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = [$"CreateNewReceiptFile:{outputFullPath}"]
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = OfficeLiteExactProfileAcceptanceReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"OfficeLite exact-profile acceptance receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab officelite exact-profile-accept failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteExactProfileAcceptanceReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.OfficeLiteExactProfileAcceptanceFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = OfficeLiteExactProfileAcceptanceReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab officelite exact-profile-acceptance-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteDeploymentPreflight(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--asset-root",
        "--project",
        "--evidence-dir",
        "--output",
        "--attempt-id",
        "--vmrun",
        "--vmx",
        "--runner",
        "--guest-ip",
        "--readiness-timeout-seconds",
        "--observation-seconds",
        "--runner-timeout-seconds");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--asset-root", out var assetRoot)
        || !options.TryGetValue("--project", out var projectPath)
        || !options.TryGetValue("--evidence-dir", out var evidenceDirectory)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--asset-root, --project, --evidence-dir and --output are required.");
    }

    var readinessTimeoutSeconds = 240;
    if (options.TryGetValue("--readiness-timeout-seconds", out var readinessTimeoutText)
        && (!int.TryParse(readinessTimeoutText, out readinessTimeoutSeconds)
            || readinessTimeoutSeconds is < 1 or > 900))
    {
        return Usage("--readiness-timeout-seconds must be an integer from 1 through 900.");
    }

    var observationSeconds = 180;
    if (options.TryGetValue("--observation-seconds", out var observationText)
        && (!int.TryParse(observationText, out observationSeconds)
            || observationSeconds is < 10 or > 600))
    {
        return Usage("--observation-seconds must be an integer from 10 through 600.");
    }

    var runnerTimeoutSeconds = 120;
    if (options.TryGetValue("--runner-timeout-seconds", out var runnerTimeoutText)
        && (!int.TryParse(runnerTimeoutText, out runnerTimeoutSeconds)
            || runnerTimeoutSeconds is < 1 or > 300))
    {
        return Usage("--runner-timeout-seconds must be an integer from 1 through 300.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"officelite-deployment-preflight-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var outputFullPath = Path.GetFullPath(outputPath);
        if (File.Exists(outputFullPath))
        {
            throw new IOException("Receipt output already exists; OfficeLite was not started.");
        }

        var request = OfficeLiteDeploymentPreflightRequest.CreateDefault(
            assetRoot,
            projectPath,
            evidenceDirectory,
            options.GetValueOrDefault("--vmx"),
            options.GetValueOrDefault("--vmrun"),
            options.GetValueOrDefault("--runner"),
            options.GetValueOrDefault("--guest-ip"),
            readinessTimeoutSeconds,
            observationSeconds,
            runnerTimeoutSeconds);
        var outcome = new OfficeLiteDeploymentPreflightRunner().Run(request, attemptId);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = outcome.Receipt.Payload.SideEffects
                .Append($"CreateNewReceiptFile:{outputFullPath}")
                .ToList()
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = OfficeLiteDeploymentPreflightReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"OfficeLite WorkVisual deployment-preflight receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidDataException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab officelite deployment-preflight failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteDeploymentPreflightReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.OfficeLiteDeploymentPreflightFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = OfficeLiteDeploymentPreflightReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidDataException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab officelite deployment-preflight-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteActiveProjectDownload(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--asset-root",
        "--output",
        "--attempt-id",
        "--vmrun",
        "--vmx",
        "--runner",
        "--timeout-seconds",
        "--observation-seconds",
        "--runner-timeout-seconds");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--asset-root", out var assetRoot)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("Both --asset-root and --output are required.");
    }

    var timeoutSeconds = 240;
    if (options.TryGetValue("--timeout-seconds", out var timeoutText)
        && (!int.TryParse(timeoutText, out timeoutSeconds) || timeoutSeconds is < 1 or > 900))
    {
        return Usage("--timeout-seconds must be an integer from 1 through 900.");
    }

    var observationSeconds = 180;
    if (options.TryGetValue("--observation-seconds", out var observationText)
        && (!int.TryParse(observationText, out observationSeconds) || observationSeconds is < 10 or > 600))
    {
        return Usage("--observation-seconds must be an integer from 10 through 600.");
    }

    var runnerTimeoutSeconds = 120;
    if (options.TryGetValue("--runner-timeout-seconds", out var runnerTimeoutText)
        && (!int.TryParse(runnerTimeoutText, out runnerTimeoutSeconds) || runnerTimeoutSeconds is < 1 or > 300))
    {
        return Usage("--runner-timeout-seconds must be an integer from 1 through 300.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"officelite-active-project-download-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var outputFullPath = Path.GetFullPath(outputPath);
        if (File.Exists(outputFullPath))
        {
            throw new IOException("Receipt output already exists; OfficeLite was not started.");
        }

        var outputDirectory = Path.GetDirectoryName(outputFullPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        var evidenceDirectory = Path.Combine(
            outputDirectory,
            $"{Path.GetFileNameWithoutExtension(outputFullPath)}.evidence");
        var request = OfficeLiteActiveProjectDownloadRequest.CreateDefault(
            assetRoot,
            evidenceDirectory,
            options.GetValueOrDefault("--vmrun"),
            options.GetValueOrDefault("--runner"),
            timeoutSeconds,
            observationSeconds,
            runnerTimeoutSeconds);
        if (options.TryGetValue("--vmx", out var vmxPath))
        {
            request = request with
            {
                OfficeLite = request.OfficeLite with { VmxPath = Path.GetFullPath(vmxPath) }
            };
        }

        var outcome = new OfficeLiteActiveProjectDownloadRunner().Run(request, attemptId);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = outcome.Receipt.Payload.SideEffects
                .Append($"CreateNewReceiptFile:{outputFullPath}")
                .ToList()
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = OfficeLiteActiveProjectDownloadReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"OfficeLite active-project download receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException)
    {
        Console.Error.WriteLine($"kuka-lab officelite active-project-download failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteActiveProjectDownloadReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.OfficeLiteActiveProjectDownloadFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = OfficeLiteActiveProjectDownloadReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab officelite active-project-download-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteRepositoryInventory(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--asset-root",
        "--output",
        "--attempt-id",
        "--vmrun",
        "--runner",
        "--guest-ip",
        "--dhcp-leases",
        "--repository-path",
        "--timeout-seconds",
        "--observation-seconds",
        "--runner-timeout-seconds");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--asset-root", out var assetRoot)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("Both --asset-root and --output are required.");
    }

    var timeoutSeconds = 240;
    if (options.TryGetValue("--timeout-seconds", out var timeoutText)
        && (!int.TryParse(timeoutText, out timeoutSeconds) || timeoutSeconds is < 1 or > 900))
    {
        return Usage("--timeout-seconds must be an integer from 1 through 900.");
    }

    var observationSeconds = 180;
    if (options.TryGetValue("--observation-seconds", out var observationText)
        && (!int.TryParse(observationText, out observationSeconds) || observationSeconds is < 10 or > 600))
    {
        return Usage("--observation-seconds must be an integer from 10 through 600.");
    }

    var runnerTimeoutSeconds = 30;
    if (options.TryGetValue("--runner-timeout-seconds", out var runnerTimeoutText)
        && (!int.TryParse(runnerTimeoutText, out runnerTimeoutSeconds) || runnerTimeoutSeconds is < 1 or > 120))
    {
        return Usage("--runner-timeout-seconds must be an integer from 1 through 120.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"officelite-repository-inventory-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var outputFullPath = Path.GetFullPath(outputPath);
        if (File.Exists(outputFullPath))
        {
            throw new IOException("Receipt output already exists; OfficeLite was not started.");
        }

        var outputDirectory = Path.GetDirectoryName(outputFullPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        var evidenceDirectory = Path.Combine(
            outputDirectory,
            $"{Path.GetFileNameWithoutExtension(outputFullPath)}.evidence");
        var request = OfficeLiteRepositoryInventoryRequest.CreateDefault(
            assetRoot,
            evidenceDirectory,
            options.GetValueOrDefault("--vmrun"),
            options.GetValueOrDefault("--runner"),
            options.GetValueOrDefault("--guest-ip"),
            options.GetValueOrDefault("--repository-path"),
            timeoutSeconds,
            observationSeconds,
            runnerTimeoutSeconds);
        if (options.TryGetValue("--dhcp-leases", out var dhcpLeasePath))
        {
            request = request with
            {
                OfficeLite = request.OfficeLite with { DhcpLeasePath = Path.GetFullPath(dhcpLeasePath) }
            };
        }

        var outcome = new OfficeLiteRepositoryInventoryRunner().Run(request, attemptId);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = outcome.Receipt.Payload.SideEffects
                .Append($"CreateNewReceiptFile:{outputFullPath}")
                .ToList()
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = OfficeLiteRepositoryInventoryReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"OfficeLite repository inventory receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException)
    {
        Console.Error.WriteLine($"kuka-lab officelite repository-inventory failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteRepositoryInventoryReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.OfficeLiteRepositoryInventoryFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = OfficeLiteRepositoryInventoryReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab officelite repository-inventory-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteNativeKssDiagnostic(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--asset-root",
        "--lab-root",
        "--output",
        "--attempt-id",
        "--vmrun",
        "--runner",
        "--guest-ip",
        "--dhcp-leases",
        "--timeout-seconds",
        "--observation-seconds",
        "--runner-timeout-seconds");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--asset-root", out var assetRoot)
        || !options.TryGetValue("--lab-root", out var labRoot)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--asset-root, --lab-root and --output are required.");
    }

    var timeoutSeconds = 240;
    if (options.TryGetValue("--timeout-seconds", out var timeoutText)
        && (!int.TryParse(timeoutText, out timeoutSeconds) || timeoutSeconds is < 1 or > 900))
    {
        return Usage("--timeout-seconds must be an integer from 1 through 900.");
    }

    var observationSeconds = 180;
    if (options.TryGetValue("--observation-seconds", out var observationText)
        && (!int.TryParse(observationText, out observationSeconds) || observationSeconds is < 10 or > 600))
    {
        return Usage("--observation-seconds must be an integer from 10 through 600.");
    }

    var runnerTimeoutSeconds = 60;
    if (options.TryGetValue("--runner-timeout-seconds", out var runnerTimeoutText)
        && (!int.TryParse(runnerTimeoutText, out runnerTimeoutSeconds) || runnerTimeoutSeconds is < 1 or > 180))
    {
        return Usage("--runner-timeout-seconds must be an integer from 1 through 180.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"officelite-native-kss-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var outputFullPath = Path.GetFullPath(outputPath);
        if (File.Exists(outputFullPath))
        {
            throw new IOException("Receipt output already exists; OfficeLite was not started.");
        }

        var outputDirectory = Path.GetDirectoryName(outputFullPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        var evidenceDirectory = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(outputFullPath)}.evidence");
        var request = OfficeLiteNativeKssDiagnosticRequest.CreateDefault(
            assetRoot,
            labRoot,
            evidenceDirectory,
            options.GetValueOrDefault("--vmrun"),
            options.GetValueOrDefault("--runner"),
            options.GetValueOrDefault("--guest-ip"),
            timeoutSeconds,
            observationSeconds,
            runnerTimeoutSeconds);
        if (options.TryGetValue("--dhcp-leases", out var dhcpLeasePath))
        {
            request = request with
            {
                OfficeLite = request.OfficeLite with { DhcpLeasePath = Path.GetFullPath(dhcpLeasePath) }
            };
        }

        var outcome = new OfficeLiteNativeKssDiagnosticRunner().Run(request, attemptId);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = outcome.Receipt.Payload.SideEffects.Append($"CreateNewReceiptFile:{outputFullPath}").ToList()
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = OfficeLiteNativeKssDiagnosticReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"OfficeLite native-KSS diagnostic receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
    {
        Console.Error.WriteLine($"kuka-lab officelite kss-diagnostic failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteNativeKssDiagnosticReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.OfficeLiteNativeKssDiagnosticFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = OfficeLiteNativeKssDiagnosticReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab officelite kss-diagnostic-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteNativeKssExecution(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--asset-root",
        "--lab-root",
        "--output",
        "--attempt-id",
        "--vmrun",
        "--vmx",
        "--runner",
        "--guest-ip",
        "--dhcp-leases",
        "--snapshot-name",
        "--timeout-seconds",
        "--observation-seconds",
        "--runner-timeout-seconds");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--asset-root", out var assetRoot)
        || !options.TryGetValue("--lab-root", out var labRoot)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--asset-root, --lab-root and --output are required.");
    }

    var timeoutSeconds = 240;
    if (options.TryGetValue("--timeout-seconds", out var timeoutText)
        && (!int.TryParse(timeoutText, out timeoutSeconds) || timeoutSeconds is < 1 or > 900))
    {
        return Usage("--timeout-seconds must be an integer from 1 through 900.");
    }

    var observationSeconds = 180;
    if (options.TryGetValue("--observation-seconds", out var observationText)
        && (!int.TryParse(observationText, out observationSeconds) || observationSeconds is < 10 or > 600))
    {
        return Usage("--observation-seconds must be an integer from 10 through 600.");
    }

    var runnerTimeoutSeconds = 120;
    if (options.TryGetValue("--runner-timeout-seconds", out var runnerTimeoutText)
        && (!int.TryParse(runnerTimeoutText, out runnerTimeoutSeconds) || runnerTimeoutSeconds is < 1 or > 300))
    {
        return Usage("--runner-timeout-seconds must be an integer from 1 through 300.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"officelite-native-kss-execution-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var outputFullPath = Path.GetFullPath(outputPath);
        if (File.Exists(outputFullPath))
        {
            throw new IOException("Receipt output already exists; OfficeLite was not started.");
        }

        var outputDirectory = Path.GetDirectoryName(outputFullPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        var evidenceDirectory = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(outputFullPath)}.evidence");
        var request = OfficeLiteNativeKssExecutionRequest.CreateDefault(
            assetRoot,
            labRoot,
            evidenceDirectory,
            options.GetValueOrDefault("--vmrun"),
            options.GetValueOrDefault("--vmx"),
            options.GetValueOrDefault("--runner"),
            options.GetValueOrDefault("--guest-ip"),
            options.GetValueOrDefault("--snapshot-name"),
            timeoutSeconds,
            observationSeconds,
            runnerTimeoutSeconds);
        if (options.TryGetValue("--dhcp-leases", out var dhcpLeasePath))
        {
            request = request with
            {
                OfficeLite = request.OfficeLite with { DhcpLeasePath = Path.GetFullPath(dhcpLeasePath) }
            };
        }

        var outcome = new OfficeLiteNativeKssExecutionRunner().Run(request, attemptId);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = outcome.Receipt.Payload.SideEffects.Append($"CreateNewReceiptFile:{outputFullPath}").ToList()
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = OfficeLiteNativeKssExecutionReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"OfficeLite native-KSS execution receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
    {
        Console.Error.WriteLine($"kuka-lab officelite kss-execution failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteNativeKssExecutionReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.OfficeLiteNativeKssExecutionFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = OfficeLiteNativeKssExecutionReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab officelite kss-execution-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteNativeKssCandidateExecution(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--asset-root",
        "--candidate-root",
        "--candidate-receipt",
        "--program",
        "--profile-acceptance",
        "--snapshot-name",
        "--expected-project",
        "--output",
        "--attempt-id",
        "--vmrun",
        "--vmx",
        "--runner",
        "--guest-ip",
        "--dhcp-leases",
        "--timeout-seconds",
        "--observation-seconds",
        "--runner-timeout-seconds",
        "--maximum-start-commands",
        "--adopt-running-lab-vm",
        "--running-vm-ownership-reference");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--asset-root", out var assetRoot)
        || !options.TryGetValue("--candidate-root", out var candidateRoot)
        || !options.TryGetValue("--candidate-receipt", out var candidateReceipt)
        || !options.TryGetValue("--program", out var program)
        || !options.TryGetValue("--profile-acceptance", out var profileAcceptance)
        || !options.TryGetValue("--snapshot-name", out var snapshotName)
        || !options.TryGetValue("--expected-project", out var expectedProject)
        || !options.TryGetValue("--vmx", out var vmxPath)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--asset-root, --candidate-root, --candidate-receipt, --program, --profile-acceptance, --snapshot-name, --expected-project, --vmx and --output are required.");
    }

    var timeoutSeconds = 240;
    if (options.TryGetValue("--timeout-seconds", out var timeoutText)
        && (!int.TryParse(timeoutText, out timeoutSeconds) || timeoutSeconds is < 1 or > 900))
    {
        return Usage("--timeout-seconds must be an integer from 1 through 900.");
    }

    var observationSeconds = 180;
    if (options.TryGetValue("--observation-seconds", out var observationText)
        && (!int.TryParse(observationText, out observationSeconds) || observationSeconds is < 10 or > 600))
    {
        return Usage("--observation-seconds must be an integer from 10 through 600.");
    }

    var runnerTimeoutSeconds = 120;
    if (options.TryGetValue("--runner-timeout-seconds", out var runnerTimeoutText)
        && (!int.TryParse(runnerTimeoutText, out runnerTimeoutSeconds) || runnerTimeoutSeconds is < 1 or > 300))
    {
        return Usage("--runner-timeout-seconds must be an integer from 1 through 300.");
    }

    var maximumStartCommands = 16;
    if (options.TryGetValue("--maximum-start-commands", out var maximumStartsText)
        && (!int.TryParse(maximumStartsText, out maximumStartCommands)
            || maximumStartCommands is < 1 or > NativeKssCandidateSubmissionContract.MaximumAllowedStartCommands))
    {
        return Usage($"--maximum-start-commands must be an integer from 1 through {NativeKssCandidateSubmissionContract.MaximumAllowedStartCommands}.");
    }

    var adoptRunningLabVm = false;
    if (options.TryGetValue("--adopt-running-lab-vm", out var adoptText)
        && !bool.TryParse(adoptText, out adoptRunningLabVm))
    {
        return Usage("--adopt-running-lab-vm must be true or false.");
    }

    var runningVmOwnershipReference = options.GetValueOrDefault("--running-vm-ownership-reference") ?? string.Empty;
    if (adoptRunningLabVm && string.IsNullOrWhiteSpace(runningVmOwnershipReference))
    {
        return Usage("--running-vm-ownership-reference is required when --adopt-running-lab-vm is true.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"officelite-native-kss-candidate-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var outputFullPath = Path.GetFullPath(outputPath);
        if (File.Exists(outputFullPath))
        {
            throw new IOException("Receipt output already exists; OfficeLite was not started.");
        }

        var outputDirectory = Path.GetDirectoryName(outputFullPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        var evidenceDirectory = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(outputFullPath)}.evidence");
        var officeLite = OfficeLiteCycleRequest.CreateDefault(
            assetRoot,
            options.GetValueOrDefault("--vmrun"),
            options.GetValueOrDefault("--guest-ip"),
            options.GetValueOrDefault("--dhcp-leases"),
            timeoutSeconds) with
        {
            VmxPath = Path.GetFullPath(vmxPath),
            DiagnoseWorkVisualServices = true,
            ServiceObservationSeconds = observationSeconds,
            AdoptRunningLabVm = adoptRunningLabVm,
            RunningVmOwnershipReference = adoptRunningLabVm ? runningVmOwnershipReference : string.Empty
        };
        var request = new NativeKssCandidateExecutionRequest
        {
            OfficeLite = officeLite,
            RunnerPath = Path.GetFullPath(options.GetValueOrDefault("--runner")
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "KUKA",
                    "WorkVisual 6.0",
                    "wvsr.exe")),
            EvidenceDirectory = evidenceDirectory,
            Submission = new NativeKssCandidateSubmissionRequest
            {
                CandidateRoot = candidateRoot,
                CandidateReceiptPath = candidateReceipt,
                ProgramRelativeStem = program,
                MaximumStartCommands = maximumStartCommands
            },
            ExactProfileAcceptanceReceiptPath = profileAcceptance,
            SnapshotName = snapshotName,
            ExpectedProjectName = expectedProject,
            RunnerTimeoutSeconds = runnerTimeoutSeconds
        };

        var outcome = new OfficeLiteNativeKssCandidateExecutionRunner().Run(request, attemptId);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = outcome.Receipt.Payload.SideEffects.Append($"CreateNewReceiptFile:{outputFullPath}").ToList()
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = NativeKssCandidateExecutionReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"OfficeLite native-KSS candidate receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidDataException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab officelite kss-candidate-execute failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteNativeKssCandidateExecutionReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.NativeKssCandidateExecutionFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = OfficeLiteNativeKssCandidateExecutionReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidDataException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab officelite kss-candidate-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunHyperVOfficeLiteCycle(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--template-vhd",
        "--template-sha256",
        "--vm-root",
        "--vm-name",
        "--switch-name",
        "--output",
        "--timeout-seconds",
        "--allow-host-change",
        "--authorization-ref",
        "--attempt-id");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--template-vhd", out var templateVhd)
        || !options.TryGetValue("--template-sha256", out var templateSha256)
        || !options.TryGetValue("--vm-root", out var vmRoot)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--template-vhd, --template-sha256, --vm-root and --output are required.");
    }

    var timeoutSeconds = 300;
    if (options.TryGetValue("--timeout-seconds", out var timeoutText)
        && (!int.TryParse(timeoutText, out timeoutSeconds) || timeoutSeconds is < 1 or > 900))
    {
        return Usage("--timeout-seconds must be an integer from 1 through 900.");
    }

    var allowHostChange = false;
    if (options.TryGetValue("--allow-host-change", out var allowText)
        && !bool.TryParse(allowText, out allowHostChange))
    {
        return Usage("--allow-host-change must be true or false.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"hyperv-officelite-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";
    var vmName = options.GetValueOrDefault("--vm-name") ?? "KUKA-OfficeLite-8.7.8-WP3V";
    var switchName = options.GetValueOrDefault("--switch-name") ?? "Default Switch";

    try
    {
        var request = HyperVOfficeLiteCycleRequest.CreateDefault(
            templateVhd,
            templateSha256,
            vmRoot,
            vmName,
            switchName,
            allowHostChange,
            options.GetValueOrDefault("--authorization-ref"),
            timeoutSeconds);
        var outcome = new HyperVOfficeLiteCycleRunner().Run(request, attemptId);
        var outputFullPath = Path.GetFullPath(outputPath);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = outcome.Receipt.Payload.SideEffects
                .Append($"CreateNewReceiptFile:{outputFullPath}")
                .ToList()
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = HyperVOfficeLiteCycleReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"Hyper-V OfficeLite cycle receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException)
    {
        Console.Error.WriteLine($"kuka-lab officelite hyperv-cycle failed: {exception.Message}");
        return 73;
    }
}

static int RunHyperVOfficeLiteReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.HyperVOfficeLiteCycleFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = HyperVOfficeLiteCycleReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab officelite hyperv-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteDeliveryInspect(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--delivery-root",
        "--declared-version",
        "--declared-build",
        "--provenance-ref",
        "--output",
        "--attempt-id");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--delivery-root", out var deliveryRoot)
        || !options.TryGetValue("--declared-version", out var declaredVersion)
        || !options.TryGetValue("--declared-build", out var declaredBuildText)
        || !options.TryGetValue("--provenance-ref", out var provenanceReference)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--delivery-root, --declared-version, --declared-build, --provenance-ref and --output are required.");
    }

    if (!int.TryParse(declaredBuildText, out var declaredBuild) || declaredBuild < 1)
    {
        return Usage("--declared-build must be a positive integer.");
    }

    var fullDeliveryRoot = Path.GetFullPath(deliveryRoot).TrimEnd(Path.DirectorySeparatorChar)
        + Path.DirectorySeparatorChar;
    var fullOutputPath = Path.GetFullPath(outputPath);
    if (fullOutputPath.StartsWith(fullDeliveryRoot, StringComparison.OrdinalIgnoreCase))
    {
        return Usage("--output must be outside --delivery-root so the receipt does not mutate the inspected package.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"officelite-delivery-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var outcome = new OfficeLiteDeliveryInspector().Inspect(
            new OfficeLiteDeliveryInspectionRequest
            {
                DeliveryRoot = deliveryRoot,
                DeclaredProductVersion = declaredVersion,
                DeclaredBuildNumber = declaredBuild,
                ProvenanceReference = provenanceReference
            },
            attemptId);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = [$"CreateNewReceiptFile:{fullOutputPath}"]
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = OfficeLiteDeliveryReceiptWriter.WriteNew(fullOutputPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"OfficeLite delivery inspection receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException)
    {
        Console.Error.WriteLine($"kuka-lab officelite delivery inspect failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteDeliveryReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.OfficeLiteDeliveryInspectionFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = OfficeLiteDeliveryReceiptVerifier.VerifyCurrentRoot(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab officelite delivery receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunWorkVisualRunnerSmoke(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--output",
        "--attempt-id",
        "--runner",
        "--timeout-seconds");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--output is required.");
    }

    var timeoutSeconds = 30;
    if (options.TryGetValue("--timeout-seconds", out var timeoutText)
        && (!int.TryParse(timeoutText, out timeoutSeconds) || timeoutSeconds is < 1 or > 120))
    {
        return Usage("--timeout-seconds must be an integer from 1 through 120.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"workvisual-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var outputFullPath = Path.GetFullPath(outputPath);
        if (File.Exists(outputFullPath))
        {
            throw new IOException("Receipt output already exists; the runner was not invoked.");
        }

        var evidenceDirectory = Path.GetDirectoryName(outputFullPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        var request = WorkVisualRunnerSmokeRequest.CreateDefault(
            evidenceDirectory,
            options.GetValueOrDefault("--runner"),
            timeoutSeconds);
        var outcome = new WorkVisualRunnerSmokeRunner().Run(request, attemptId);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = outcome.Receipt.Payload.SideEffects
                .Append($"CreateNewReceiptFile:{outputFullPath}")
                .ToList()
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = WorkVisualRunnerSmokeReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"WorkVisual runner smoke receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException)
    {
        Console.Error.WriteLine($"kuka-lab workvisual runner-smoke failed: {exception.Message}");
        return 73;
    }
}

static int RunWorkVisualReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.WorkVisualRunnerSmokeFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = WorkVisualRunnerSmokeReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab workvisual receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunWorkVisualInterfaceInventory(string[] args)
{
    var parseResult = ParseOptions(args, "--install-root", "--output", "--attempt-id");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--output is required.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"workvisual-interface-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var outputFullPath = Path.GetFullPath(outputPath);
        if (File.Exists(outputFullPath))
        {
            throw new IOException("Receipt output already exists; WorkVisual files were not inventoried.");
        }

        var request = WorkVisualInterfaceInventoryRequest.CreateDefault(
            options.GetValueOrDefault("--install-root"));
        var outcome = new WorkVisualInterfaceInventoryCollector().Collect(request, attemptId);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = [$"CreateNewReceiptFile:{outputFullPath}"]
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = WorkVisualInterfaceInventoryReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"WorkVisual interface inventory receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException)
    {
        Console.Error.WriteLine($"kuka-lab workvisual interface-inventory failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteExpertModeInterfaceInventory(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--smarthmi-logon-assembly",
        "--krc-security-assembly",
        "--user-access-contracts-assembly",
        "--user-access-service-assembly",
        "--user-access-implementation-assembly",
        "--service-host-configuration",
        "--output",
        "--attempt-id");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    var required = new[]
    {
        "--smarthmi-logon-assembly",
        "--krc-security-assembly",
        "--user-access-contracts-assembly",
        "--user-access-service-assembly",
        "--user-access-implementation-assembly",
        "--service-host-configuration",
        "--output"
    };
    foreach (var name in required)
    {
        if (!options.ContainsKey(name))
        {
            return Usage($"{name} is required.");
        }
    }

    var outputPath = options["--output"];
    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"officelite-expert-interface-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var outputFullPath = Path.GetFullPath(outputPath);
        if (File.Exists(outputFullPath))
        {
            throw new IOException("Receipt output already exists; OfficeLite interface files were not inventoried.");
        }

        var request = new OfficeLiteExpertModeInterfaceInventoryRequest
        {
            SmartHmiLogonAssemblyPath = options["--smarthmi-logon-assembly"],
            KrcSecurityAssemblyPath = options["--krc-security-assembly"],
            UserAccessContractsAssemblyPath = options["--user-access-contracts-assembly"],
            UserAccessServiceAssemblyPath = options["--user-access-service-assembly"],
            UserAccessImplementationAssemblyPath = options["--user-access-implementation-assembly"],
            WorkVisualServiceHostConfigurationPath = options["--service-host-configuration"]
        };
        var outcome = new OfficeLiteExpertModeInterfaceInventoryCollector().Collect(request, attemptId);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = [$"CreateNewReceiptFile:{outputFullPath}"]
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = OfficeLiteExpertModeInterfaceInventoryReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"OfficeLite expert-mode interface inventory receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException)
    {
        Console.Error.WriteLine($"kuka-lab officelite expert-interface-inventory failed: {exception.Message}");
        return 73;
    }
}

static int RunOfficeLiteExpertModeInterfaceReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.OfficeLiteExpertModeInterfaceInventoryFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = OfficeLiteExpertModeInterfaceInventoryReceiptVerifier.Verify(
            receipt,
            verifyCurrentFiles: true);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab officelite expert-interface-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunWorkVisualInterfaceReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.WorkVisualInterfaceInventoryFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = WorkVisualInterfaceInventoryReceiptVerifier.Verify(
            receipt,
            verifyCurrentInstall: true);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab workvisual interface-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunWorkVisualProjectExtraction(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--project",
        "--project-receipt",
        "--output",
        "--extractor",
        "--timeout-seconds",
        "--attempt-id");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--project", out var projectPath)
        || !options.TryGetValue("--project-receipt", out var projectReceiptPath)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--project, --project-receipt and --output are required.");
    }

    var timeoutSeconds = 60;
    if (options.TryGetValue("--timeout-seconds", out var timeoutText)
        && (!int.TryParse(timeoutText, out timeoutSeconds) || timeoutSeconds is < 1 or > 300))
    {
        return Usage("--timeout-seconds must be an integer from 1 through 300.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"workvisual-project-extraction-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var projectFullPath = Path.GetFullPath(projectPath);
        var outputFullPath = Path.GetFullPath(outputPath);
        if (File.Exists(outputFullPath))
        {
            throw new IOException("Receipt output already exists; ProjectExtractor was not invoked.");
        }

        var outputDirectory = Path.GetDirectoryName(outputFullPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        var evidenceDirectory = Path.Combine(
            outputDirectory,
            $"{Path.GetFileNameWithoutExtension(outputFullPath)}.evidence");
        var intakeReceipt = ReceiptSerialization.WorkVisualProjectIntakeFromJson(
            File.ReadAllText(Path.GetFullPath(projectReceiptPath)));
        var request = WorkVisualProjectExtractionRequest.CreateDefault(
            projectFullPath,
            intakeReceipt,
            evidenceDirectory,
            options.GetValueOrDefault("--extractor"),
            timeoutSeconds);
        var outcome = new WorkVisualProjectExtractionRunner().Run(request, attemptId);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = outcome.Receipt.Payload.SideEffects
                .Append($"CreateNewReceiptFile:{outputFullPath}")
                .ToList()
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = WorkVisualProjectExtractionReceiptWriter.WriteNew(
            outputFullPath,
            projectFullPath,
            receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"WorkVisual project-extraction receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidDataException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab workvisual project-extract failed: {exception.Message}");
        return 73;
    }
}

static int RunWorkVisualProjectExtractionReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.WorkVisualProjectExtractionFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = WorkVisualProjectExtractionReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab workvisual project-extraction-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunControllerProjectBaseline(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--project",
        "--extraction-receipt",
        "--vault",
        "--exact-kukasim-component",
        "--generic-kukasim-component",
        "--output",
        "--attempt-id");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--project", out var projectPath)
        || !options.TryGetValue("--extraction-receipt", out var extractionReceiptPath)
        || !options.TryGetValue("--vault", out var vaultPath)
        || !options.TryGetValue("--exact-kukasim-component", out var exactComponentPath)
        || !options.TryGetValue("--generic-kukasim-component", out var genericComponentPath)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--project, --extraction-receipt, --vault, --exact-kukasim-component, --generic-kukasim-component and --output are required.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"controller-project-baseline-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var outputFullPath = Path.GetFullPath(outputPath);
        if (File.Exists(outputFullPath))
        {
            throw new IOException("Receipt output already exists; protected-vault creation was not invoked.");
        }

        var extractionReceipt = ReceiptSerialization.WorkVisualProjectExtractionFromJson(
            File.ReadAllText(Path.GetFullPath(extractionReceiptPath)));
        var outcome = new ControllerProjectBaselineRunner().Run(
            new ControllerProjectBaselineRequest
            {
                ProjectPath = Path.GetFullPath(projectPath),
                ExtractionReceipt = extractionReceipt,
                VaultDirectory = Path.GetFullPath(vaultPath),
                ExactKukaSimComponentPath = Path.GetFullPath(exactComponentPath),
                GenericKukaSimComponentPath = Path.GetFullPath(genericComponentPath)
            },
            attemptId);
        var writtenPath = ControllerProjectBaselineReceiptWriter.WriteNew(outputFullPath, outcome.Receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(outcome.Receipt));
        Console.Error.WriteLine($"Controller-project baseline receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidDataException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab controller project-baseline failed: {exception.Message}");
        return 73;
    }
}

static int RunControllerProjectBaselineReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.ControllerProjectBaselineFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = ControllerProjectBaselineReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidDataException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab controller project-baseline-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunKukaSimComponentSmoke(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--output",
        "--attempt-id",
        "--launcher",
        "--component",
        "--timeout-seconds",
        "--allow-gui",
        "--authorization-ref");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    var options = parseResult.Options;
    if (!options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--output is required.");
    }

    var timeoutSeconds = 180;
    if (options.TryGetValue("--timeout-seconds", out var timeoutText)
        && (!int.TryParse(timeoutText, out timeoutSeconds) || timeoutSeconds is < 10 or > 600))
    {
        return Usage("--timeout-seconds must be an integer from 10 through 600.");
    }

    var allowGui = false;
    if (options.TryGetValue("--allow-gui", out var allowGuiText)
        && (!bool.TryParse(allowGuiText, out allowGui)))
    {
        return Usage("--allow-gui must be true or false.");
    }

    var authorizationReference = options.GetValueOrDefault("--authorization-ref") ?? string.Empty;
    if (allowGui && string.IsNullOrWhiteSpace(authorizationReference))
    {
        return Usage("--authorization-ref is required when --allow-gui is true.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"kukasim-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var outputFullPath = Path.GetFullPath(outputPath);
        if (File.Exists(outputFullPath))
        {
            throw new IOException("Receipt output already exists; KUKA.Sim was not invoked.");
        }

        var evidenceDirectory = Path.GetDirectoryName(outputFullPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        var request = KukaSimComponentSmokeRequest.CreateDefault(
            evidenceDirectory,
            allowGui,
            authorizationReference,
            options.GetValueOrDefault("--launcher"),
            options.GetValueOrDefault("--component"),
            timeoutSeconds);
        var outcome = new KukaSimComponentSmokeRunner().Run(request, attemptId);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = outcome.Receipt.Payload.SideEffects
                .Append($"CreateNewReceiptFile:{outputFullPath}")
                .ToList()
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var writtenPath = KukaSimComponentSmokeReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"KUKA.Sim component smoke receipt written: {writtenPath}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException)
    {
        Console.Error.WriteLine($"kuka-lab kukasim component-smoke failed: {exception.Message}");
        return 73;
    }
}

static int RunKukaSimReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null)
    {
        return Usage(parseResult.Error);
    }

    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }

    try
    {
        var receipt = ReceiptSerialization.KukaSimComponentSmokeFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = KukaSimComponentSmokeReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab kukasim receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunKukaSimIntegratedValidation(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--lab-root",
        "--evidence-dir",
        "--output",
        "--attempt-id",
        "--engine",
        "--component",
        "--timeout-seconds",
        "--allow-gui",
        "--authorization-ref");
    if (parseResult.Error is not null) return Usage(parseResult.Error);
    var options = parseResult.Options;
    if (!options.TryGetValue("--lab-root", out var labRoot)
        || !options.TryGetValue("--evidence-dir", out var evidenceDirectory)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--lab-root, --evidence-dir and --output are required.");
    }
    var timeoutSeconds = 180;
    if (options.TryGetValue("--timeout-seconds", out var timeoutText)
        && (!int.TryParse(timeoutText, out timeoutSeconds) || timeoutSeconds is < 10 or > 600))
    {
        return Usage("--timeout-seconds must be an integer from 10 through 600.");
    }
    var allowGui = false;
    if (options.TryGetValue("--allow-gui", out var allowGuiText) && !bool.TryParse(allowGuiText, out allowGui))
    {
        return Usage("--allow-gui must be true or false.");
    }
    var authorizationReference = options.GetValueOrDefault("--authorization-ref") ?? string.Empty;
    if (allowGui && string.IsNullOrWhiteSpace(authorizationReference))
    {
        return Usage("--authorization-ref is required when --allow-gui is true.");
    }
    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"kukasim-integrated-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";
    try
    {
        var outputFullPath = Path.GetFullPath(outputPath);
        if (File.Exists(outputFullPath)) throw new IOException("Receipt output already exists; KUKA.Sim was not invoked.");
        var request = KukaSimIntegratedValidationRequest.CreateDefault(
            labRoot,
            evidenceDirectory,
            allowGui,
            authorizationReference,
            options.GetValueOrDefault("--engine"),
            options.GetValueOrDefault("--component"),
            timeoutSeconds);
        var outcome = new KukaSimIntegratedValidationRunner().Run(request, attemptId);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = outcome.Receipt.Payload.SideEffects.Append($"CreateNewReceiptFile:{outputFullPath}").ToList()
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        if (string.Equals(
            Environment.GetEnvironmentVariable("KUKA_LAB_DIAGNOSTIC_EXCEPTIONS"),
            "1",
            StringComparison.Ordinal))
        {
            Console.Error.WriteLine(ReceiptSerialization.ToJson(receipt));
        }
        var written = KukaSimIntegratedValidationReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"KUKA.Sim Integrated validation receipt written: {written}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab kukasim integrated-validate failed: {exception.Message}");
        if (string.Equals(
            Environment.GetEnvironmentVariable("KUKA_LAB_DIAGNOSTIC_EXCEPTIONS"),
            "1",
            StringComparison.Ordinal))
        {
            Console.Error.WriteLine(exception);
        }
        return 73;
    }
}

static int RunKukaSimIntegratedValidationReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null) return Usage(parseResult.Error);
    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath)) return Usage("--receipt is required.");
    try
    {
        var receipt = ReceiptSerialization.KukaSimIntegratedValidationFromJson(File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = KukaSimIntegratedValidationReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab kukasim integrated-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunKukaSimCandidateExecution(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--candidate-root",
        "--candidate-receipt",
        "--program",
        "--evidence-dir",
        "--output",
        "--attempt-id",
        "--engine",
        "--component",
        "--layout",
        "--timeout-seconds",
        "--maximum-start-commands",
        "--allow-gui",
        "--authorization-ref");
    if (parseResult.Error is not null) return Usage(parseResult.Error);
    var options = parseResult.Options;
    if (!options.TryGetValue("--candidate-root", out var candidateRoot)
        || !options.TryGetValue("--candidate-receipt", out var candidateReceipt)
        || !options.TryGetValue("--program", out var program)
        || !options.TryGetValue("--evidence-dir", out var evidenceDirectory)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--candidate-root, --candidate-receipt, --program, --evidence-dir and --output are required.");
    }
    var timeoutSeconds = 180;
    if (options.TryGetValue("--timeout-seconds", out var timeoutText)
        && (!int.TryParse(timeoutText, out timeoutSeconds) || timeoutSeconds is < 10 or > 600))
    {
        return Usage("--timeout-seconds must be an integer from 10 through 600.");
    }
    var maximumStartCommands = 16;
    if (options.TryGetValue("--maximum-start-commands", out var maximumStartsText)
        && (!int.TryParse(maximumStartsText, out maximumStartCommands)
            || maximumStartCommands is < 1 or > NativeKssCandidateSubmissionContract.MaximumAllowedStartCommands))
    {
        return Usage($"--maximum-start-commands must be an integer from 1 through {NativeKssCandidateSubmissionContract.MaximumAllowedStartCommands}.");
    }
    if (!options.TryGetValue("--allow-gui", out var allowGuiText)
        || !bool.TryParse(allowGuiText, out var allowGui)
        || !allowGui)
    {
        return Usage("--allow-gui true is required for candidate execution.");
    }
    var authorizationReference = options.GetValueOrDefault("--authorization-ref") ?? string.Empty;
    if (string.IsNullOrWhiteSpace(authorizationReference))
    {
        return Usage("--authorization-ref is required when --allow-gui is true.");
    }
    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"kukasim-candidate-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";
    try
    {
        var outputFullPath = Path.GetFullPath(outputPath);
        if (File.Exists(outputFullPath)) throw new IOException("Receipt output already exists; KUKA.Sim was not invoked.");
        var submission = new NativeKssCandidateSubmissionRequest
        {
            CandidateRoot = candidateRoot,
            CandidateReceiptPath = candidateReceipt,
            ProgramRelativeStem = program,
            MaximumStartCommands = maximumStartCommands
        };
        var request = KukaSimCandidateExecutionRequest.CreateDefault(
            evidenceDirectory,
            submission,
            allowGui,
            authorizationReference,
            enginePath: options.GetValueOrDefault("--engine"),
            componentPath: options.GetValueOrDefault("--component"),
            timeoutSeconds: timeoutSeconds,
            simulationLayoutPath: options.GetValueOrDefault("--layout"));
        var outcome = new KukaSimCandidateExecutionRunner().Run(request, attemptId);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = outcome.Receipt.Payload.SideEffects.Append($"CreateNewReceiptFile:{outputFullPath}").ToList()
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var written = KukaSimCandidateExecutionReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"KUKA.Sim candidate execution receipt written: {written}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab kukasim candidate-execute failed: {exception.Message}");
        return 73;
    }
}

static int RunKukaSimCandidateExecutionReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null) return Usage(parseResult.Error);
    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath)) return Usage("--receipt is required.");
    try
    {
        var receipt = ReceiptSerialization.KukaSimCandidateExecutionFromJson(File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = KukaSimCandidateExecutionReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab kukasim candidate-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunKukaSimOfficeLiteVirtualLoop(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--asset-root", "--lab-root", "--output", "--attempt-id",
        "--vmrun", "--vmx", "--runner", "--engine", "--component", "--layout",
        "--guest-ip", "--dhcp-leases", "--snapshot-name",
        "--timeout-seconds", "--observation-seconds", "--runner-timeout-seconds",
        "--kukasim-timeout-seconds", "--allow-gui", "--authorization-ref", "--adopt-running-lab-vm");
    if (parseResult.Error is not null) return Usage(parseResult.Error);
    var options = parseResult.Options;
    if (!options.TryGetValue("--asset-root", out var assetRoot)
        || !options.TryGetValue("--lab-root", out var labRoot)
        || !options.TryGetValue("--output", out var outputPath)
        || !options.TryGetValue("--layout", out var synchronizedLayoutPath))
    {
        return Usage("--asset-root, --lab-root, --layout and --output are required.");
    }

    var readinessSeconds = 240;
    if (options.TryGetValue("--timeout-seconds", out var readinessText)
        && (!int.TryParse(readinessText, out readinessSeconds) || readinessSeconds is < 1 or > 900))
        return Usage("--timeout-seconds must be an integer from 1 through 900.");
    var observationSeconds = 180;
    if (options.TryGetValue("--observation-seconds", out var observationText)
        && (!int.TryParse(observationText, out observationSeconds) || observationSeconds is < 10 or > 600))
        return Usage("--observation-seconds must be an integer from 10 through 600.");
    var runnerSeconds = 120;
    if (options.TryGetValue("--runner-timeout-seconds", out var runnerText)
        && (!int.TryParse(runnerText, out runnerSeconds) || runnerSeconds is < 1 or > 300))
        return Usage("--runner-timeout-seconds must be an integer from 1 through 300.");
    var kukaSimSeconds = 300;
    if (options.TryGetValue("--kukasim-timeout-seconds", out var kukaSimText)
        && (!int.TryParse(kukaSimText, out kukaSimSeconds) || kukaSimSeconds is < 30 or > 600))
        return Usage("--kukasim-timeout-seconds must be an integer from 30 through 600.");
    var allowGui = false;
    if (options.TryGetValue("--allow-gui", out var allowGuiText) && !bool.TryParse(allowGuiText, out allowGui))
        return Usage("--allow-gui must be true or false.");
    var authorization = options.GetValueOrDefault("--authorization-ref") ?? string.Empty;
    if (allowGui && string.IsNullOrWhiteSpace(authorization))
        return Usage("--authorization-ref is required when --allow-gui is true.");
    var adoptRunningLabVm = false;
    if (options.TryGetValue("--adopt-running-lab-vm", out var adoptText)
        && !bool.TryParse(adoptText, out adoptRunningLabVm))
        return Usage("--adopt-running-lab-vm must be true or false.");
    if (adoptRunningLabVm && string.IsNullOrWhiteSpace(authorization))
        return Usage("--authorization-ref is required when --adopt-running-lab-vm is true.");
    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"kukasim-officelite-loop-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";

    try
    {
        var outputFullPath = Path.GetFullPath(outputPath);
        if (File.Exists(outputFullPath)) throw new IOException("Receipt output already exists; the virtual loop was not invoked.");
        var outputDirectory = Path.GetDirectoryName(outputFullPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        var evidenceDirectory = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(outputFullPath) + ".evidence");
        var request = KukaSimOfficeLiteVirtualLoopRequest.CreateDefault(
            assetRoot, labRoot, evidenceDirectory, allowGui, authorization,
            options.GetValueOrDefault("--vmrun"), options.GetValueOrDefault("--vmx"),
            options.GetValueOrDefault("--runner"), options.GetValueOrDefault("--engine"),
            options.GetValueOrDefault("--component"), synchronizedLayoutPath, options.GetValueOrDefault("--guest-ip"),
            options.GetValueOrDefault("--snapshot-name"), readinessSeconds, observationSeconds,
            runnerSeconds, kukaSimSeconds, adoptRunningLabVm);
        if (options.TryGetValue("--dhcp-leases", out var leases))
            request = request with { OfficeLite = request.OfficeLite with { DhcpLeasePath = Path.GetFullPath(leases) } };
        var outcome = new KukaSimOfficeLiteVirtualLoopRunner().Run(request, attemptId);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = outcome.Receipt.Payload.SideEffects.Append("CreateNewReceiptFile:" + outputFullPath).ToList()
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var written = KukaSimOfficeLiteVirtualLoopReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine("KUKA.Sim/OfficeLite virtual-loop receipt written: " + written);
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
        or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine("kuka-lab kukasim officelite-loop failed: " + exception.Message);
        return 73;
    }
}

static int RunKukaSimOfficeLiteVirtualLoopReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null) return Usage(parseResult.Error);
    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath)) return Usage("--receipt is required.");
    try
    {
        var receipt = ReceiptSerialization.KukaSimOfficeLiteVirtualLoopFromJson(File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = KukaSimOfficeLiteVirtualLoopReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
        or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine("kuka-lab kukasim officelite-loop-receipt verify failed: " + exception.Message);
        return 73;
    }
}

static int RunOfflineLoopComparison(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--valid-fixture-receipt",
        "--invalid-fixture-receipt",
        "--native-kss-receipt",
        "--kukasim-receipt",
        "--output",
        "--attempt-id");
    if (parseResult.Error is not null) return Usage(parseResult.Error);
    var options = parseResult.Options;
    if (!options.TryGetValue("--valid-fixture-receipt", out var validFixtureReceipt)
        || !options.TryGetValue("--invalid-fixture-receipt", out var invalidFixtureReceipt)
        || !options.TryGetValue("--native-kss-receipt", out var nativeKssReceipt)
        || !options.TryGetValue("--kukasim-receipt", out var kukaSimReceipt)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--valid-fixture-receipt, --invalid-fixture-receipt, --native-kss-receipt, --kukasim-receipt and --output are required.");
    }

    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"offline-loop-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";
    try
    {
        var outputFullPath = Path.GetFullPath(outputPath);
        if (File.Exists(outputFullPath))
        {
            throw new IOException("Receipt output already exists; comparison was not run.");
        }
        var request = new OfflineLoopComparisonRequest
        {
            ValidFixtureReceiptPath = validFixtureReceipt,
            InvalidFixtureReceiptPath = invalidFixtureReceipt,
            NativeKssReceiptPath = nativeKssReceipt,
            KukaSimReceiptPath = kukaSimReceipt
        };
        var outcome = new OfflineLoopComparisonRunner().Run(request, attemptId);
        var payload = outcome.Receipt.Payload with
        {
            SideEffects = [$"CreateNewComparisonReceipt:{outputFullPath}"]
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var written = OfflineLoopComparisonReceiptWriter.WriteNew(outputFullPath, receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(receipt));
        Console.Error.WriteLine($"Offline-loop comparison receipt written: {written}");
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab offline-loop compare failed: {exception.Message}");
        return 73;
    }
}

static int RunOfflineLoopComparisonReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null) return Usage(parseResult.Error);
    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath))
    {
        return Usage("--receipt is required.");
    }
    try
    {
        var receipt = ReceiptSerialization.OfflineLoopComparisonFromJson(
            File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = OfflineLoopComparisonReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"kuka-lab offline-loop comparison-receipt verify failed: {exception.Message}");
        return 73;
    }
}

static int RunStageOneEvidenceBundle(string[] args)
{
    var parseResult = ParseOptions(
        args,
        "--candidate-kind",
        "--candidate-root",
        "--candidate-receipt",
        "--package-receipt",
        "--comparison-receipt",
        "--virtual-loop-receipt",
        "--output",
        "--attempt-id");
    if (parseResult.Error is not null) return Usage(parseResult.Error);
    var options = parseResult.Options;
    if (!options.TryGetValue("--candidate-kind", out var candidateKindText)
        || !options.TryGetValue("--candidate-root", out var candidateRoot)
        || !options.TryGetValue("--candidate-receipt", out var candidateReceipt)
        || !options.TryGetValue("--comparison-receipt", out var comparisonReceipt)
        || !options.TryGetValue("--virtual-loop-receipt", out var virtualLoopReceipt)
        || !options.TryGetValue("--output", out var outputPath))
    {
        return Usage("--candidate-kind, --candidate-root, --candidate-receipt, --comparison-receipt, --virtual-loop-receipt and --output are required.");
    }
    var candidateKind = candidateKindText switch
    {
        "raw-krl" => StageOneCandidateEvidenceKind.RawKrlIdentity,
        "validation-package" => StageOneCandidateEvidenceKind.ValidationPackageStaticPreflight,
        _ => (StageOneCandidateEvidenceKind?)null
    };
    if (candidateKind is null)
    {
        return Usage("--candidate-kind must be raw-krl or validation-package.");
    }
    if (candidateKind == StageOneCandidateEvidenceKind.ValidationPackageStaticPreflight
        && !options.ContainsKey("--package-receipt"))
    {
        return Usage("--package-receipt is required for validation-package candidates.");
    }
    if (candidateKind == StageOneCandidateEvidenceKind.RawKrlIdentity
        && options.ContainsKey("--package-receipt"))
    {
        return Usage("--package-receipt is not allowed for raw-krl candidates.");
    }
    var attemptId = options.TryGetValue("--attempt-id", out var suppliedAttemptId)
        ? suppliedAttemptId
        : $"stage-one-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";
    try
    {
        var request = new StageOneEvidenceBundleRequest
        {
            CandidateKind = candidateKind.Value,
            CandidateRoot = candidateRoot,
            CandidateReceiptPath = candidateReceipt,
            ValidationPackageReceiptPath = options.GetValueOrDefault("--package-receipt"),
            ComparisonReceiptPath = comparisonReceipt,
            VirtualLoopReceiptPath = virtualLoopReceipt,
            ReceiptOutputPath = outputPath
        };
        var outcome = new StageOneEvidenceBundleRunner().Run(request, attemptId);
        var written = StageOneEvidenceBundleReceiptWriter.WriteNew(outputPath, outcome.Receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(outcome.Receipt));
        Console.Error.WriteLine("Stage-one evidence bundle receipt written: " + written);
        return outcome.ExitCode;
    }
    catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
        or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine("kuka-lab stage-one compose failed: " + exception.Message);
        return 73;
    }
}

static int RunStageOneEvidenceBundleReceiptVerify(string[] args)
{
    var parseResult = ParseOptions(args, "--receipt");
    if (parseResult.Error is not null) return Usage(parseResult.Error);
    if (!parseResult.Options.TryGetValue("--receipt", out var receiptPath)) return Usage("--receipt is required.");
    try
    {
        var receipt = ReceiptSerialization.StageOneEvidenceBundleFromJson(File.ReadAllText(Path.GetFullPath(receiptPath)));
        var result = StageOneEvidenceBundleReceiptVerifier.Verify(receipt);
        Console.WriteLine(ReceiptSerialization.ToJson(result));
        return result.Succeeded ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
        or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine("kuka-lab stage-one receipt verify failed: " + exception.Message);
        return 73;
    }
}

static int VerifyStageOneEvidenceBundleReceipt(string json)
{
    var result = StageOneEvidenceBundleReceiptVerifier.Verify(ReceiptSerialization.StageOneEvidenceBundleFromJson(json));
    Console.WriteLine(ReceiptSerialization.ToJson(result));
    return result.Succeeded ? 0 : 2;
}

static (Dictionary<string, string> Options, string? Error) ParseOptions(
    string[] args,
    params string[] supportedOptions)
{
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    var supported = supportedOptions.ToHashSet(StringComparer.Ordinal);
    for (var index = 0; index < args.Length; index += 2)
    {
        if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
        {
            return (options, $"Invalid option near: {args[index]}");
        }

        if (!options.TryAdd(args[index], args[index + 1]))
        {
            return (options, $"Duplicate option: {args[index]}");
        }
    }

    var unsupported = options.Keys.FirstOrDefault(key => !supported.Contains(key));
    if (unsupported is not null)
    {
        return (options, $"Unsupported option: {unsupported}");
    }

    return (options, null);
}

static int Usage(string error)
{
    Console.Error.WriteLine(error);
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  kuka-lab fixture verify --fixture <directory> --output <new-receipt.json> [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab environment inventory --asset-root <directory> --output <new-receipt.json> [--attempt-id <id>] [--require-exact-c01-component <true|false>]");
    Console.Error.WriteLine("  kuka-lab environment receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab environment receipt diff --baseline <receipt.json> --current <receipt.json>");
    Console.Error.WriteLine("  kuka-lab package verify --package <validation-package-directory> --output <new-receipt.json> [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab package receipt verify --receipt <receipt.json> [--package <validation-package-directory>]");
    Console.Error.WriteLine("  kuka-lab package krl-preflight --package <validation-package-directory> --package-receipt <receipt.json> --output <new-receipt.json> [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab package krl-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab krl candidate-intake --source <src-dat-directory> --output <new-receipt.json> [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab krl candidate-receipt verify --receipt <receipt.json> [--source <src-dat-directory>]");
    Console.Error.WriteLine("  kuka-lab package controller-compare --package-receipt <receipt.json> --controller-observation-receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab controller network-preflight --interface <alias> --internet-interface <alias> --controller-ip <IPv4> --host-ip <IPv4> --output <new-receipt.json> [--prefix-length <1..30>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab controller network-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab controller endpoint-probe --interface <alias> --internet-interface <alias> --controller-ip <IPv4> --host-ip <IPv4> --output <new-receipt.json> [--prefix-length <1..30>] [--port 49003] [--timeout-milliseconds <250..10000>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab controller endpoint-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab controller observation-intake --controller-family <family> --cabinet-model <model> --kss-version <x.y.z> --kss-build <Bnnn> --kli-address <IPv4> --observed-on <yyyy-MM-dd> --evidence-references <id,id> --output <new-receipt.json> [--prefix-length <1..30>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab controller observation-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab controller project-intake --project <downloaded.wvs> --capture-reference <sanitized-id> --output <new-receipt.json> [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab controller project-receipt verify --receipt <receipt.json> [--project <downloaded.wvs>]");
    Console.Error.WriteLine("  kuka-lab controller project-baseline --project <downloaded.wvs> --extraction-receipt <project-extraction.receipt.json> --vault <new-protected-directory> --exact-kukasim-component <C01.vcmx> --generic-kukasim-component <generic.vcmx> --output <new-receipt.json> [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab controller project-baseline-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab officelite profile-clone --asset-root <directory> --target-vmx <new.vmx> --clone-name <name> --authorization-ref <id> --output <new-receipt.json> [--vmrun <vmrun.exe>] [--source-vmx <primary.vmx>] [--snapshot-name <name>] [--timeout-seconds <30..3600>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab officelite profile-clone-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab officelite boot-verify --asset-root <directory> --output <new-receipt.json> [--vmrun <vmrun.exe>] [--vmx <isolated.vmx>] [--guest-ip <IPv4>] [--timeout-seconds <1..900>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab officelite service-diagnose --asset-root <directory> --output <new-receipt.json> [--vmrun <vmrun.exe>] [--guest-ip <IPv4>] [--timeout-seconds <1..900>] [--observation-seconds <10..600>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab officelite receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab officelite project-inventory --asset-root <directory> --output <new-receipt.json> [--vmrun <vmrun.exe>] [--vmx <isolated.vmx>] [--runner <wvsr.exe>] [--guest-ip <IPv4>] [--timeout-seconds <1..900>] [--observation-seconds <10..600>] [--runner-timeout-seconds <1..120>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab officelite project-inventory-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab officelite controller-profile-readback --asset-root <directory> --output <new-receipt.json> [--vmrun <vmrun.exe>] [--vmx <isolated.vmx>] [--runner <wvsr.exe>] [--guest-ip <IPv4>] [--dhcp-leases <leases-file>] [--timeout-seconds <1..900>] [--observation-seconds <10..600>] [--runner-timeout-seconds <1..120>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab officelite controller-profile-readback-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab officelite exact-profile-accept --profile-readback <receipt.json> --active-project-download <receipt.json> --active-controller-baseline <receipt.json> --trusted-controller-baseline <receipt.json> --output <new-receipt.json> [--expected-project <name>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab officelite exact-profile-acceptance-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab officelite deployment-preflight --asset-root <directory> --project <protected-source.wvs> --evidence-dir <new-directory> --output <new-receipt.json> [--vmrun <vmrun.exe>] [--vmx <isolated.vmx>] [--runner <wvsr.exe>] [--guest-ip <IPv4>] [--readiness-timeout-seconds <1..900>] [--observation-seconds <10..600>] [--runner-timeout-seconds <1..300>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab officelite deployment-preflight-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab officelite active-project-download --asset-root <directory> --output <new-receipt.json> [--vmrun <vmrun.exe>] [--vmx <isolated.vmx>] [--runner <wvsr.exe>] [--timeout-seconds <1..900>] [--observation-seconds <10..600>] [--runner-timeout-seconds <1..300>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab officelite active-project-download-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab officelite repository-inventory --asset-root <directory> --output <new-receipt.json> [--repository-path <KRC path>] [--vmrun <vmrun.exe>] [--runner <wvsr.exe>] [--guest-ip <IPv4>] [--timeout-seconds <1..900>] [--observation-seconds <10..600>] [--runner-timeout-seconds <1..120>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab officelite repository-inventory-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab officelite kss-diagnostic --asset-root <directory> --lab-root <04_kuka_lab> --output <new-receipt.json> [--vmrun <vmrun.exe>] [--runner <wvsr.exe>] [--guest-ip <IPv4>] [--timeout-seconds <1..900>] [--observation-seconds <10..600>] [--runner-timeout-seconds <1..180>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab officelite kss-diagnostic-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab officelite kss-execution --asset-root <directory> --lab-root <04_kuka_lab> --output <new-receipt.json> [--vmrun <vmrun.exe>] [--vmx <isolated.vmx>] [--runner <wvsr.exe>] [--guest-ip <IPv4>] [--snapshot-name <name>] [--timeout-seconds <1..900>] [--observation-seconds <10..600>] [--runner-timeout-seconds <1..300>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab officelite kss-execution-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab officelite kss-candidate-execute --asset-root <directory> --candidate-root <src-dat-directory> --candidate-receipt <receipt.json> --program <relative-stem> --profile-acceptance <receipt.json> --snapshot-name <name> --expected-project <name> --vmx <isolated.vmx> --output <new-receipt.json> [--vmrun <vmrun.exe>] [--runner <wvsr.exe>] [--guest-ip <IPv4>] [--dhcp-leases <leases-file>] [--timeout-seconds <1..900>] [--observation-seconds <10..600>] [--runner-timeout-seconds <1..300>] [--maximum-start-commands <1..64>] [--adopt-running-lab-vm true|false --running-vm-ownership-reference <id>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab officelite kss-candidate-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab officelite expert-interface-inventory --smarthmi-logon-assembly <LogOn.dll> --krc-security-assembly <KUKARoboter.KrcSecurity.dll> --user-access-contracts-assembly <KukaRoboter.Contracts.dll> --user-access-service-assembly <KukaRoboter.Services.dll> --user-access-implementation-assembly <KukaRoboter.Services.Implementation.dll> --service-host-configuration <WorkVisualServiceHost.exe.config> --output <new-receipt.json> [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab officelite expert-interface-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab officelite hyperv-cycle --template-vhd <fixed.vhd> --template-sha256 <sha256> --vm-root <directory> --output <new-receipt.json> [--vm-name <name>] [--switch-name <name>] [--timeout-seconds <1..900>] [--allow-host-change <true|false>] [--authorization-ref <id>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab officelite hyperv-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab officelite delivery inspect --delivery-root <unpacked-directory> --declared-version <8.7.x> --declared-build <5+> --provenance-ref <opaque-id> --output <new-receipt.json> [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab officelite delivery receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab workvisual runner-smoke --output <new-receipt.json> [--runner <wvsr.exe>] [--timeout-seconds <1..120>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab workvisual receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab workvisual interface-inventory --output <new-receipt.json> [--install-root <WorkVisual 6.0>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab workvisual interface-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab workvisual project-extract --project <downloaded.wvs> --project-receipt <project-intake.receipt.json> --output <new-receipt.json> [--extractor <ProjectExtractor.exe>] [--timeout-seconds <1..300>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab workvisual project-extraction-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab kukasim component-smoke --output <new-receipt.json> [--launcher <launcher.exe>] [--component <vcmx>] [--timeout-seconds <10..600>] [--allow-gui <true|false>] [--authorization-ref <id>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab kukasim receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab kukasim integrated-validate --lab-root <04_kuka_lab> --evidence-dir <new-directory> --output <new-receipt.json> [--engine <VisualComponents.Engine.exe>] [--component <exact-C01.vcmx>] [--timeout-seconds <10..600>] [--allow-gui <true|false>] [--authorization-ref <id>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab kukasim integrated-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab kukasim candidate-execute --candidate-root <src-dat-directory> --candidate-receipt <candidate.receipt.json> --program <relative-stem> --evidence-dir <new-directory> --output <new-receipt.json> --allow-gui true --authorization-ref <id> [--engine <VisualComponents.Engine.exe>] [--component <exact-C01.vcmx>] [--layout <synchronized-C01.vcmx>] [--timeout-seconds <10..600>] [--maximum-start-commands <1..64>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab kukasim candidate-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab kukasim officelite-loop --asset-root <C:/kuka-validation-lab/vendor/KUKA Simulation> --lab-root <04_kuka_lab> --output <new-receipt.json> --allow-gui true --authorization-ref <id> [--adopt-running-lab-vm true|false] [--vmrun <vmrun.exe>] [--vmx <isolated.vmx>] [--runner <wvsr.exe>] [--engine <VisualComponents.Engine.exe>] [--component <KR3_R540.vcmx>] [--guest-ip <IPv4>] [--snapshot-name <name>] [--timeout-seconds <1..900>] [--observation-seconds <10..600>] [--runner-timeout-seconds <1..300>] [--kukasim-timeout-seconds <30..600>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab kukasim officelite-loop-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab offline-loop compare --valid-fixture-receipt <item9a-valid.json> --invalid-fixture-receipt <item9a-invalid.json> --native-kss-receipt <item9b.json> --kukasim-receipt <item9c.json> --output <new-receipt.json> [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab offline-loop comparison-receipt verify --receipt <receipt.json>");
    Console.Error.WriteLine("  kuka-lab stage-one compose --candidate-kind <raw-krl|validation-package> --candidate-root <directory> --candidate-receipt <receipt.json> --comparison-receipt <item9d.json> --virtual-loop-receipt <item9e.json> --output <new-receipt.json> [--package-receipt <validation-package.receipt.json>] [--attempt-id <id>]");
    Console.Error.WriteLine("  kuka-lab stage-one receipt verify --receipt <receipt.json>");
    return 64;
}
