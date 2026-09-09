using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace KukaLab.Core;

public static class ControllerProjectBaselineContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.controller-project-baseline-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const string SummarySchemaIdentity = "kuka.lab.controller-project-software-baseline";
    public const int SummarySchemaVersion = 1;

    internal static readonly IReadOnlyList<string> RequiredCheckIds =
    [
        "extraction-receipt",
        "source-current",
        "kukasim-component-selection",
        "vault-boundary",
        "source-preserved",
        "extracted-tree-preserved",
        "baseline-summary",
        "vault-manifest",
        "no-vendor-side-effects"
    ];

    internal static readonly IReadOnlyList<string> RequiredUnsupportedGaps =
    [
        "This is the highest-authority available controller-project software evidence and supersedes public, open-source or inferred software-profile values field by field; it does not supersede direct physical nameplates or direct HMI observations.",
        "Project Tool/Base/Load values are preserved exactly but are not physical calibration, measured payload or load-qualification evidence.",
        "Safety artifacts and full dynamic arrays are preserved and hash-indexed but their content is not interpreted, modified or safety-qualified.",
        "Technology-package, module, fieldbus and program inventories prove project presence/configuration only; they do not prove licenses, runtime activation, compatibility or successful execution.",
        "The exact KUKA.Sim C01 component metadata match does not by itself prove kinematic, collision, timing or native-KSS equivalence.",
        "No controller, network, credential, license, WorkVisual, OfficeLite, KUKA.Sim, KSS execution, deployment, activation, safety change or physical motion is invoked."
    ];
}

public sealed record ControllerProjectBaselineRequest
{
    public required string ProjectPath { get; init; }

    public required WorkVisualProjectExtractionReceipt ExtractionReceipt { get; init; }

    public required string VaultDirectory { get; init; }

    public required string ExactKukaSimComponentPath { get; init; }

    public required string GenericKukaSimComponentPath { get; init; }
}

public sealed record ControllerProjectBaselineReceipt
{
    public string SchemaIdentity { get; init; } = ControllerProjectBaselineContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = ControllerProjectBaselineContract.ReceiptSchemaVersion;

    public required ControllerProjectBaselinePayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record ControllerProjectBaselinePayload
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

    public required WorkVisualProjectExtractionReceipt ExtractionReceipt { get; init; }

    public string ExtractionReceiptSha256 { get; init; } = string.Empty;

    public EnvironmentFileObservation OriginalProject { get; init; } = new();

    public EnvironmentFileObservation ExactKukaSimComponent { get; init; } = new();

    public EnvironmentFileObservation GenericKukaSimComponent { get; init; } = new();

    public string VaultDirectory { get; init; } = string.Empty;

    public EnvironmentFileObservation VaultProject { get; init; } = new();

    public WorkVisualExtractedTreeIdentity VaultExtractedTree { get; init; } = new();

    public WorkVisualExtractedTreeIdentity VaultTree { get; init; } = new();

    public EnvironmentFileObservation VaultSummary { get; init; } = new();

    public EnvironmentFileObservation VaultManifest { get; init; } = new();

    public ControllerProjectSoftwareBaseline? Baseline { get; init; }

    public bool VaultCreated { get; init; }

    public bool OriginalProjectChanged { get; init; }

    public bool ExtractionEvidenceChanged { get; init; }

    public bool ControllerAccessed { get; init; }

    public bool NetworkTrafficSent { get; init; }

    public bool CredentialsUsed { get; init; }

    public bool LicenseStateRead { get; init; }

    public bool SafetyContentInterpreted { get; init; }

    public bool VendorApplicationInvoked { get; init; }

    public NativeKssStatus NativeKssStatus { get; init; } = NativeKssStatus.NotRun;

    public List<EnvironmentCheck> Checks { get; init; } = [];

    public List<string> SideEffects { get; init; } = [];

    public bool EnvironmentReusable { get; init; }

    public List<string> UnsupportedGaps { get; init; } = [];
}

public sealed record ControllerProjectSoftwareBaseline
{
    public string SchemaIdentity { get; init; } = ControllerProjectBaselineContract.SummarySchemaIdentity;

    public int SchemaVersion { get; init; } = ControllerProjectBaselineContract.SummarySchemaVersion;

    public string EvidenceAuthority { get; init; } = "HighestAvailableControllerProjectSoftwareEvidence";

    public string SupersessionRule { get; init; } =
        "Supersedes prior public, open-source or inferred software-profile values field by field when conflicts exist.";

    public List<string> DoesNotSupersede { get; init; } =
    [
        "Direct physical nameplate evidence",
        "Direct smartHMI/KSS version observations",
        "Physical mastering and calibration",
        "Measured payload and center of mass",
        "Safety validation and low-speed physical qualification"
    ];

    public WorkVisualExtractedControllerProfile ControllerProfile { get; init; } = new();

    public List<ControllerAxisMachineData> Axes { get; init; } = [];

    public ControllerNetworkBaseline Network { get; init; } = new();

    public ControllerIoBaseline Io { get; init; } = new();

    public List<ControllerModuleBaseline> Modules { get; init; } = [];

    public List<ControllerProgramBaseline> Programs { get; init; } = [];

    public List<ControllerSafetyArtifact> SafetyArtifacts { get; init; } = [];

    public ControllerProjectInventory Inventory { get; init; } = new();

    public ControllerKukaSimComponentComparison KukaSimComponentComparison { get; init; } = new();

    public List<string> WorkVisualGeneratorVersions { get; init; } = [];

    public List<string> UnqualifiedFacts { get; init; } = [];
}

public sealed record ControllerAxisMachineData
{
    public int AxisNumber { get; init; }

    public double NegativeSoftwareLimitDegrees { get; init; }

    public double PositiveSoftwareLimitDegrees { get; init; }

    public int Direction { get; init; }

    public double MasteringReferenceDegrees { get; init; }

    public int GearRatioNumerator { get; init; }

    public int GearRatioDenominator { get; init; }

    public double MotorMaximumRpm { get; init; }

    public double DerivedMaximumJointSpeedDegreesPerSecond { get; init; }

    public string MotorFile { get; init; } = string.Empty;

    public string ServoFile { get; init; } = string.Empty;

    public string MasteringType { get; init; } = string.Empty;

    public string SimulationMode { get; init; } = string.Empty;
}

public sealed record ControllerNetworkBaseline
{
    public bool KliActive { get; init; }

    public string KliAddress { get; init; } = string.Empty;

    public int KliPrefixLength { get; init; }

    public string KliAddressMode { get; init; } = string.Empty;

    public bool KoniActive { get; init; }

    public string KoniAddress { get; init; } = string.Empty;

    public int KoniPrefixLength { get; init; }

    public List<int> PublishedTcpPorts { get; init; } = [];
}

public sealed record ControllerIoBaseline
{
    public bool ProfinetDriverActive { get; init; }

    public string ProfinetDeviceName { get; init; } = string.Empty;

    public int ProfinetApplicationRelationCount { get; init; }

    public int ProcessDataSegmentCount { get; init; }

    public int DigitalInputSignalCount { get; init; }

    public int DigitalOutputSignalCount { get; init; }

    public int AnalogInputSignalCount { get; init; }

    public int AnalogOutputSignalCount { get; init; }
}

public sealed record ControllerModuleBaseline
{
    public string Name { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public bool Active { get; init; }

    public string ConfigurationFile { get; init; } = string.Empty;

    public string EvidenceFile { get; init; } = string.Empty;
}

public sealed record ControllerProgramBaseline
{
    public string ModuleName { get; init; } = string.Empty;

    public bool HasSrc { get; init; }

    public bool HasDat { get; init; }

    public long TotalBytes { get; init; }

    public int SourceLineCount { get; init; }

    public int RoutineDeclarationCount { get; init; }

    public int PtpInstructionCount { get; init; }

    public int LinInstructionCount { get; init; }

    public int CircInstructionCount { get; init; }

    public int SplineTokenCount { get; init; }

    public List<ControllerProjectFileIdentity> Files { get; init; } = [];
}

public sealed record ControllerSafetyArtifact
{
    public string RelativePath { get; init; } = string.Empty;

    public long Bytes { get; init; }

    public string Sha256 { get; init; } = string.Empty;

    public bool ContentInterpreted { get; init; }
}

public sealed record ControllerProjectInventory
{
    public int FileCount { get; init; }

    public long TotalBytes { get; init; }

    public string TreeSha256 { get; init; } = string.Empty;

    public List<ControllerExtensionCount> Extensions { get; init; } = [];

    public List<ControllerProjectFileIdentity> Files { get; init; } = [];

    public string DynamicModelFileSha256 { get; init; } = string.Empty;

    public int DynamicModelDeclaredValueCount { get; init; }

    public bool DynamicModelInterpreted { get; init; }
}

public sealed record ControllerExtensionCount
{
    public string Extension { get; init; } = string.Empty;

    public int Count { get; init; }
}

public sealed record ControllerProjectFileIdentity
{
    public string RelativePath { get; init; } = string.Empty;

    public long Bytes { get; init; }

    public string Sha256 { get; init; } = string.Empty;
}

public sealed record ControllerKukaSimComponentComparison
{
    public string ExactComponentFileName { get; init; } = string.Empty;

    public string ExactComponentName { get; init; } = string.Empty;

    public string ExactComponentSha256 { get; init; } = string.Empty;

    public string ExactComponentDetailedRevision { get; init; } = string.Empty;

    public string GenericComponentFileName { get; init; } = string.Empty;

    public string GenericComponentName { get; init; } = string.Empty;

    public string GenericComponentSha256 { get; init; } = string.Empty;

    public string GenericComponentDetailedRevision { get; init; } = string.Empty;

    public int DifferingArchiveEntryCount { get; init; }

    public int DifferingControllerPayloadEntryCount { get; init; }

    public bool ExactC01NameMatch { get; init; }

    public bool ExactComponentPreferred { get; init; }
}

public sealed record ControllerProjectVaultManifest
{
    public string SchemaIdentity { get; init; } = "kuka.lab.controller-project-evidence-vault-manifest";

    public int SchemaVersion { get; init; } = 1;

    public ControllerProjectFileIdentity Project { get; init; } = new();

    public WorkVisualExtractedTreeIdentity ExtractedTree { get; init; } = new();

    public ControllerProjectFileIdentity Summary { get; init; } = new();

    public List<ControllerProjectFileIdentity> ExtractedFiles { get; init; } = [];
}

public sealed record ControllerProjectBaselineOutcome(ControllerProjectBaselineReceipt Receipt)
{
    public int ExitCode => Receipt.Payload.TerminalClassification == EnvironmentTerminalClassification.Ready ? 0 : 2;
}

public sealed class ControllerProjectBaselineRunner
{
    private readonly TimeProvider _timeProvider;

    public ControllerProjectBaselineRunner(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public ControllerProjectBaselineOutcome Run(ControllerProjectBaselineRequest request, string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(attemptId);
        var started = _timeProvider.GetUtcNow();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var checks = new List<EnvironmentCheck>();
        var sideEffects = new List<string>();
        var project = Path.GetFullPath(request.ProjectPath);
        var vault = Path.GetFullPath(request.VaultDirectory);
        var exactComponent = Path.GetFullPath(request.ExactKukaSimComponentPath);
        var genericComponent = Path.GetFullPath(request.GenericKukaSimComponentPath);
        var extractionReceiptSha = ReceiptSerialization.ComputeCanonicalSha256(request.ExtractionReceipt);
        var baseline = default(ControllerProjectSoftwareBaseline);
        var vaultProject = new EnvironmentFileObservation { Id = "vault-project" };
        var vaultSummary = new EnvironmentFileObservation { Id = "vault-summary" };
        var vaultManifest = new EnvironmentFileObservation { Id = "vault-manifest" };
        var vaultExtractedTree = new WorkVisualExtractedTreeIdentity();
        var vaultTree = new WorkVisualExtractedTreeIdentity { RootPath = vault };
        var vaultCreated = false;
        var staging = vault + $".staging-{attemptId}";

        try
        {
            var extractionVerification = WorkVisualProjectExtractionReceiptVerifier.Verify(request.ExtractionReceipt);
            if (!extractionVerification.Succeeded
                || request.ExtractionReceipt.Payload.TerminalClassification != EnvironmentTerminalClassification.Ready)
            {
                checks.Add(Failed("extraction-receipt", "The extraction receipt is not current, Ready and integrity-valid."));
                return Complete();
            }

            checks.Add(Passed("extraction-receipt", "The complete extracted project tree and its strict receipt are current and integrity-valid."));
            var sourceObservation = ObserveFile("original-project", project);
            if (!sourceObservation.Exists
                || sourceObservation.Bytes != request.ExtractionReceipt.Payload.ProjectFile.Bytes
                || !string.Equals(sourceObservation.Sha256, request.ExtractionReceipt.Payload.ProjectFile.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                checks.Add(Failed("source-current", "The original WVS no longer matches the extraction receipt."));
                return Complete();
            }

            checks.Add(Passed("source-current", "The original WVS remains byte-identical to its extraction receipt."));
            ControllerKukaSimComponentComparison componentComparison;
            try
            {
                componentComparison = AnalyzeComponents(exactComponent, genericComponent);
                if (!componentComparison.ExactC01NameMatch
                    || !componentComparison.ExactComponentPreferred)
                {
                    throw new InvalidDataException("The nominated exact component is not the distinct named C01 component.");
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                checks.Add(Failed("kukasim-component-selection", $"Exact component selection failed: {exception.Message}"));
                return Complete();
            }

            checks.Add(Passed("kukasim-component-selection", "The distinct named KUKA.Sim C01 component is selected; archive and embedded-controller payload differences from the generic component are recorded rather than assumed equivalent."));
            try
            {
                EnsureVaultBoundary(project, request.ExtractionReceipt.Payload.ExtractedTree.RootPath, vault);
                if (Directory.Exists(vault) || Directory.Exists(staging))
                {
                    throw new IOException("The evidence vault or its staging directory already exists; create-new semantics refused reuse.");
                }

                Directory.CreateDirectory(staging);
                sideEffects.Add($"CreateProtectedEvidenceVault:{vault}");
                checks.Add(Passed("vault-boundary", "The create-new protected vault is disjoint from the original WVS and extraction evidence."));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
            {
                checks.Add(Failed("vault-boundary", $"Protected-vault boundary failed: {exception.Message}"));
                return Complete();
            }

            var sourceDir = Path.Combine(staging, "source");
            var extractedDir = Path.Combine(staging, "extracted-project");
            var summaryDir = Path.Combine(staging, "summary");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(extractedDir);
            Directory.CreateDirectory(summaryDir);
            var stagedProject = Path.Combine(sourceDir, "controller-project.wvs");
            File.Copy(project, stagedProject, overwrite: false);
            CopyTree(request.ExtractionReceipt.Payload.ExtractedTree.RootPath, extractedDir);
            var stagedProjectObservation = ObserveFile("vault-project", stagedProject);
            if (stagedProjectObservation.Bytes != sourceObservation.Bytes
                || !string.Equals(stagedProjectObservation.Sha256, sourceObservation.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                checks.Add(Failed("source-preserved", "The protected WVS copy is not byte-identical to the original."));
                return Complete();
            }

            checks.Add(Passed("source-preserved", "The protected WVS copy is byte-identical and uses a sanitized stable filename."));
            var stagedExtractedTree = WorkVisualProjectExtractionRunner.CaptureTree(extractedDir);
            if (!SameTreeIdentity(stagedExtractedTree, request.ExtractionReceipt.Payload.ExtractedTree))
            {
                checks.Add(Failed("extracted-tree-preserved", "The protected extracted tree is not byte-identical to the accepted extraction."));
                return Complete();
            }

            checks.Add(Passed("extracted-tree-preserved", "All extracted project files are preserved with the accepted count, bytes and deterministic tree hash."));
            baseline = Analyze(extractedDir, exactComponent, genericComponent, componentComparison);
            var summaryPath = Path.Combine(summaryDir, "controller-project-software-baseline.json");
            WriteNewText(summaryPath, ReceiptSerialization.ToJson(baseline));
            var summaryObservation = ObserveFile("vault-summary", summaryPath);
            var roundTrip = ReceiptSerialization.ControllerProjectSoftwareBaselineFromJson(File.ReadAllText(summaryPath));
            if (!string.Equals(
                    ReceiptSerialization.ComputeCanonicalSha256(baseline),
                    ReceiptSerialization.ComputeCanonicalSha256(roundTrip),
                    StringComparison.OrdinalIgnoreCase))
            {
                checks.Add(Failed("baseline-summary", "The baseline summary did not survive strict round-trip parsing."));
                return Complete();
            }

            checks.Add(Passed("baseline-summary", "The structured summary covers exact identity, axes/drives, Tool/Base/Load, network, I/O, modules, programs, safety inventory and KUKA.Sim component selection."));
            var manifest = new ControllerProjectVaultManifest
            {
                Project = ToIdentity(stagedProject, staging),
                ExtractedTree = stagedExtractedTree with { RootPath = "extracted-project" },
                Summary = ToIdentity(summaryPath, staging),
                ExtractedFiles = CaptureFiles(extractedDir, extractedDir)
            };
            var manifestPath = Path.Combine(staging, "manifest.json");
            WriteNewText(manifestPath, ReceiptSerialization.ToJson(manifest));
            var roundTripManifest = ReceiptSerialization.ControllerProjectVaultManifestFromJson(File.ReadAllText(manifestPath));
            if (!string.Equals(
                    ReceiptSerialization.ComputeCanonicalSha256(manifest),
                    ReceiptSerialization.ComputeCanonicalSha256(roundTripManifest),
                    StringComparison.OrdinalIgnoreCase))
            {
                checks.Add(Failed("vault-manifest", "The protected-vault manifest did not survive strict round-trip parsing."));
                return Complete();
            }

            Directory.Move(staging, vault);
            vaultCreated = true;
            vaultProject = ObserveFile("vault-project", Path.Combine(vault, "source", "controller-project.wvs"));
            vaultSummary = ObserveFile("vault-summary", Path.Combine(vault, "summary", "controller-project-software-baseline.json"));
            vaultManifest = ObserveFile("vault-manifest", Path.Combine(vault, "manifest.json"));
            vaultExtractedTree = WorkVisualProjectExtractionRunner.CaptureTree(Path.Combine(vault, "extracted-project"));
            vaultTree = WorkVisualProjectExtractionRunner.CaptureTree(vault);
            checks.Add(Passed("vault-manifest", "The finalized protected vault has a strict manifest and deterministic complete-tree identity."));
            checks.Add(Passed("no-vendor-side-effects", "The operation used local file copy, parsing and hashing only; no vendor application, controller, network, credential, license, safety or KSS action occurred."));
            return Complete();
        }
        finally
        {
            if (!vaultCreated && Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }

        ControllerProjectBaselineOutcome Complete()
        {
            stopwatch.Stop();
            if (!checks.Any(check => check.Id == "no-vendor-side-effects"))
            {
                checks.Add(Passed("no-vendor-side-effects", "No vendor application, controller, network, credential, license, safety or KSS action occurred."));
            }

            var terminal = checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
                ? EnvironmentTerminalClassification.Failed
                : EnvironmentTerminalClassification.Ready;
            var payload = new ControllerProjectBaselinePayload
            {
                ReceiptId = $"controller-project-baseline-{attemptId}",
                AttemptId = attemptId,
                CoreAssemblySha256 = ComputeSha256(typeof(ControllerProjectBaselineRunner).Assembly.Location),
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
                ExtractionReceipt = request.ExtractionReceipt,
                ExtractionReceiptSha256 = extractionReceiptSha,
                OriginalProject = ObserveFile("original-project", project),
                ExactKukaSimComponent = ObserveFile("exact-kukasim-component", exactComponent),
                GenericKukaSimComponent = ObserveFile("generic-kukasim-component", genericComponent),
                VaultDirectory = vault,
                VaultProject = vaultProject,
                VaultExtractedTree = vaultExtractedTree,
                VaultTree = vaultTree,
                VaultSummary = vaultSummary,
                VaultManifest = vaultManifest,
                Baseline = baseline,
                VaultCreated = vaultCreated,
                OriginalProjectChanged = false,
                ExtractionEvidenceChanged = false,
                ControllerAccessed = false,
                NetworkTrafficSent = false,
                CredentialsUsed = false,
                LicenseStateRead = false,
                SafetyContentInterpreted = false,
                VendorApplicationInvoked = false,
                NativeKssStatus = NativeKssStatus.NotRun,
                Checks = checks,
                SideEffects = sideEffects,
                EnvironmentReusable = !Directory.Exists(staging),
                UnsupportedGaps = ControllerProjectBaselineContract.RequiredUnsupportedGaps.ToList()
            };
            var receipt = new ControllerProjectBaselineReceipt
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
            };
            return new ControllerProjectBaselineOutcome(receipt);
        }
    }

    internal static ControllerProjectSoftwareBaseline Analyze(
        string extractedRoot,
        string exactComponentPath,
        string genericComponentPath,
        ControllerKukaSimComponentComparison? componentComparison = null)
    {
        var root = Path.GetFullPath(extractedRoot);
        var parsedProfile = WorkVisualProjectExtractionRunner.ParseProfile(root);
        var profile = parsedProfile with
        {
            MachineDat = NormalizeObservationPath(root, parsedProfile.MachineDat),
            RobcorDat = NormalizeObservationPath(root, parsedProfile.RobcorDat),
            ConfigDat = NormalizeObservationPath(root, parsedProfile.ConfigDat),
            CabinetControlXml = NormalizeObservationPath(root, parsedProfile.CabinetControlXml)
        };
        var machinePath = Path.Combine(root, "KRC", "R1", "Mada", "$machine.dat");
        var robcorPath = Path.Combine(root, "KRC", "R1", "Mada", "$robcor.dat");
        var machine = File.ReadAllText(machinePath, Encoding.Latin1);
        var axes = Enumerable.Range(1, 6)
            .Select(index => ParseAxis(root, machine, profile, index))
            .ToList();
        var files = CaptureFiles(root, root);
        var dynamicModelCount = Regex.Matches(
            File.ReadAllText(robcorPath, Encoding.Latin1),
            "(?m)^\\s*\\$DYN_DAT\\[\\d+\\]\\s*=",
            RegexOptions.CultureInvariant).Count;
        return new ControllerProjectSoftwareBaseline
        {
            ControllerProfile = profile,
            Axes = axes,
            Network = ParseNetwork(root),
            Io = ParseIo(root),
            Modules = ParseModules(root),
            Programs = ParsePrograms(root),
            SafetyArtifacts = files
                .Where(file => file.RelativePath.EndsWith(".SAF", StringComparison.OrdinalIgnoreCase))
                .Select(file => new ControllerSafetyArtifact
                {
                    RelativePath = file.RelativePath,
                    Bytes = file.Bytes,
                    Sha256 = file.Sha256,
                    ContentInterpreted = false
                })
                .ToList(),
            Inventory = new ControllerProjectInventory
            {
                FileCount = files.Count,
                TotalBytes = files.Sum(file => file.Bytes),
                TreeSha256 = WorkVisualProjectExtractionRunner.CaptureTree(root).TreeSha256,
                Extensions = files
                    .GroupBy(file => Path.GetExtension(file.RelativePath), StringComparer.OrdinalIgnoreCase)
                    .Select(group => new ControllerExtensionCount
                    {
                        Extension = string.IsNullOrEmpty(group.Key) ? "<none>" : group.Key.ToLowerInvariant(),
                        Count = group.Count()
                    })
                    .OrderByDescending(value => value.Count)
                    .ThenBy(value => value.Extension, StringComparer.Ordinal)
                    .ToList(),
                Files = files,
                DynamicModelFileSha256 = ComputeSha256(robcorPath),
                DynamicModelDeclaredValueCount = dynamicModelCount,
                DynamicModelInterpreted = false
            },
            KukaSimComponentComparison = componentComparison ?? AnalyzeComponents(exactComponentPath, genericComponentPath),
            WorkVisualGeneratorVersions = ParseWorkVisualGeneratorVersions(root),
            UnqualifiedFacts =
            [
                "KSS patch/build is not established by this project export; use direct smartHMI evidence for the exact installed KSS version.",
                "Technology-package directory names are candidates only; package versions and licenses remain unqualified.",
                "Tool/Base/Load values are project values, not proof of physical calibration or measured payload.",
                "Safety artifacts and dynamic arrays are preserved but not interpreted.",
                "Programs are inventoried and token-counted but are not compiled or executed by this baseline operation."
            ]
        };
    }

    internal static ControllerKukaSimComponentComparison AnalyzeComponents(string exactPath, string genericPath)
    {
        var exact = ReadComponent(Path.GetFullPath(exactPath));
        var generic = ReadComponent(Path.GetFullPath(genericPath));
        var keys = exact.Entries.Keys.Concat(generic.Entries.Keys).Distinct(StringComparer.Ordinal).ToArray();
        var differences = keys.Where(key => !string.Equals(
            exact.Entries.GetValueOrDefault(key),
            generic.Entries.GetValueOrDefault(key),
            StringComparison.OrdinalIgnoreCase)).ToArray();
        var controllerDifferences = differences.Count(key => key.StartsWith("packfolder/__CONTROLLER__/", StringComparison.Ordinal));
        var exactC01 = exact.Name.Contains("C01", StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(exactPath).Contains("C01", StringComparison.OrdinalIgnoreCase);
        return new ControllerKukaSimComponentComparison
        {
            ExactComponentFileName = Path.GetFileName(exactPath),
            ExactComponentName = exact.Name,
            ExactComponentSha256 = ComputeSha256(exactPath),
            ExactComponentDetailedRevision = exact.DetailedRevision,
            GenericComponentFileName = Path.GetFileName(genericPath),
            GenericComponentName = generic.Name,
            GenericComponentSha256 = ComputeSha256(genericPath),
            GenericComponentDetailedRevision = generic.DetailedRevision,
            DifferingArchiveEntryCount = differences.Length,
            DifferingControllerPayloadEntryCount = controllerDifferences,
            ExactC01NameMatch = exactC01,
            ExactComponentPreferred = exactC01 && !generic.Name.Contains("C01", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static ControllerAxisMachineData ParseAxis(
        string root,
        string machine,
        WorkVisualExtractedControllerProfile profile,
        int index)
    {
        var axisPath = Path.Combine(root, "Config", "User", "Common", "Mada", "NGAxis", $"A{index}.xml");
        var document = LoadXml(axisPath);
        var machineName = document.Descendants().First(element => element.Name.LocalName == "Machine").Attribute("Name")?.Value;
        if (!string.Equals(machineName, profile.ModelName, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Axis A{index} machine identity does not match the project profile.");
        }

        var toolMotor = document.Descendants().First(element => element.Name.LocalName == "ToolMotor");
        var axisData = document.Descendants().First(element => element.Name.LocalName == "AxisData");
        var mastering = document.Descendants().First(element => element.Name.LocalName == "Mastering");
        var ratio = ReadRatio(machine, "$RAT_MOT_AX", index);
        var motorRpm = ReadNumber(machine, "$VEL_AXIS_MA", index);
        var limit = profile.AxisLimits.Single(value => value.AxisNumber == index);
        return new ControllerAxisMachineData
        {
            AxisNumber = index,
            NegativeSoftwareLimitDegrees = limit.Negative,
            PositiveSoftwareLimitDegrees = limit.Positive,
            Direction = (int)ReadNumber(machine, "$AXIS_DIR", index),
            MasteringReferenceDegrees = ReadNumber(machine, "$MAMES", index),
            GearRatioNumerator = ratio.Numerator,
            GearRatioDenominator = ratio.Denominator,
            MotorMaximumRpm = motorRpm,
            DerivedMaximumJointSpeedDegreesPerSecond = Math.Round(Math.Abs(motorRpm * 6.0 / (ratio.Numerator / (double)ratio.Denominator)), 6),
            MotorFile = Path.GetFileName(toolMotor.Attribute("MotorFile")?.Value ?? string.Empty),
            ServoFile = Path.GetFileName(toolMotor.Attribute("ServoFile")?.Value ?? string.Empty),
            MasteringType = mastering.Attribute("Type")?.Value ?? string.Empty,
            SimulationMode = axisData.Attribute("Simulation")?.Value ?? string.Empty
        };
    }

    private static ControllerNetworkBaseline ParseNetwork(string root)
    {
        var kli = LoadXml(Path.Combine(root, "Config", "User", "Common", "KLIConfig.xml"));
        var koni = LoadXml(Path.Combine(root, "Config", "User", "Common", "KONIConfig.xml"));
        var kliBus = kli.Descendants().First(element => element.Name.LocalName == "BusConfig");
        var koniBus = koni.Descendants().First(element => element.Name.LocalName == "BusConfig");
        var kliIf = kli.Descendants().First(element => element.Name.LocalName == "IfConfig" && element.Attribute("Name")?.Value == "KLI");
        var koniIf = koni.Descendants().First(element => element.Name.LocalName == "IfConfig" && element.Attribute("Name")?.Value == "KONI");
        var ports = kli.Descendants()
            .Where(element => element.Name.LocalName == "NATRule")
            .Select(element => Regex.Match(element.Value, @"\bport\s+(?<port>\d+)\b", RegexOptions.CultureInvariant))
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups["port"].Value, CultureInfo.InvariantCulture))
            .Distinct()
            .Order()
            .ToList();
        return new ControllerNetworkBaseline
        {
            KliActive = ParseBool(kliBus.Attribute("isActive")?.Value),
            KliAddress = kliIf.Attribute("Ip")?.Value ?? string.Empty,
            KliPrefixLength = NetmaskHexToPrefix(kliIf.Attribute("Netmask")?.Value),
            KliAddressMode = kliIf.Attribute("IpConfigType")?.Value ?? string.Empty,
            KoniActive = ParseBool(koniBus.Attribute("isActive")?.Value),
            KoniAddress = koniIf.Attribute("Ip")?.Value ?? string.Empty,
            KoniPrefixLength = NetmaskHexToPrefix(koniIf.Attribute("Netmask")?.Value),
            PublishedTcpPorts = ports
        };
    }

    private static ControllerIoBaseline ParseIo(string root)
    {
        var common = Path.Combine(root, "Config", "User", "Common");
        var pnio = LoadOptionalXml(Path.Combine(common, "PNIODriver.xml"));
        var ippnio = LoadOptionalXml(Path.Combine(common, "IPPNIO.xml"));
        var krcIo = LoadOptionalXml(Path.Combine(common, "KRC_IO.xml"));
        var signals = LoadOptionalXml(Path.Combine(common, "KrcIoSignals.xml"));
        var module = LoadOptionalXml(Path.Combine(common, "Modules_PNIODriver.xml"))
            ?.Descendants().FirstOrDefault(element => element.Name.LocalName == "Module");
        var profinet = pnio?.Descendants().FirstOrDefault(element => element.Name.LocalName == "PROFINET");
        return new ControllerIoBaseline
        {
            ProfinetDriverActive = module is not null && ParseBool(module.Attribute("isActive")?.Value),
            ProfinetDeviceName = profinet?.Attribute("PNIODeviceName")?.Value ?? string.Empty,
            ProfinetApplicationRelationCount = ippnio?.Descendants().Count(element => element.Name.LocalName == "AR") ?? 0,
            ProcessDataSegmentCount = krcIo?.Descendants().Count(element => element.Name.LocalName == "Segment") ?? 0,
            DigitalInputSignalCount = signals?.Descendants().Count(element => element.Name.LocalName == "Signal" && element.Attribute("type")?.Value == "$IN") ?? 0,
            DigitalOutputSignalCount = signals?.Descendants().Count(element => element.Name.LocalName == "Signal" && element.Attribute("type")?.Value == "$OUT") ?? 0,
            AnalogInputSignalCount = signals?.Descendants().Count(element => element.Name.LocalName == "Signal" && element.Attribute("type")?.Value == "$ANIN") ?? 0,
            AnalogOutputSignalCount = signals?.Descendants().Count(element => element.Name.LocalName == "Signal" && element.Attribute("type")?.Value == "$ANOUT") ?? 0
        };
    }

    private static List<ControllerModuleBaseline> ParseModules(string root)
    {
        var common = Path.Combine(root, "Config", "User", "Common");
        return Directory.EnumerateFiles(common, "Modules_*.xml", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .SelectMany(path => LoadXml(path).Descendants()
                .Where(element => element.Name.LocalName == "Module")
                .Select(element => new ControllerModuleBaseline
                {
                    Name = element.Attribute("Name")?.Value ?? string.Empty,
                    DisplayName = element.Attribute("DisplayName")?.Value ?? string.Empty,
                    Active = ParseBool(element.Attribute("isActive")?.Value),
                    ConfigurationFile = element.Attribute("Config")?.Value ?? string.Empty,
                    EvidenceFile = Path.GetFileName(path)
                }))
            .OrderBy(value => value.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static List<ControllerProgramBaseline> ParsePrograms(string root)
    {
        var paths = Directory.EnumerateFiles(Path.Combine(root, "KRC", "R1", "Program"), "*", SearchOption.TopDirectoryOnly)
            .Concat([Path.Combine(root, "KRC", "R1", "cell.src")])
            .Where(File.Exists)
            .ToArray();
        return paths
            .GroupBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var src = group.FirstOrDefault(path => Path.GetExtension(path).Equals(".src", StringComparison.OrdinalIgnoreCase));
                var content = src is null ? string.Empty : File.ReadAllText(src, Encoding.Latin1);
                return new ControllerProgramBaseline
                {
                    ModuleName = group.Key,
                    HasSrc = src is not null,
                    HasDat = group.Any(path => Path.GetExtension(path).Equals(".dat", StringComparison.OrdinalIgnoreCase)),
                    TotalBytes = group.Sum(path => new FileInfo(path).Length),
                    SourceLineCount = src is null ? 0 : CountLines(content),
                    RoutineDeclarationCount = CountToken(content, @"(?m)^\s*(?:GLOBAL\s+)?(?:DEF|DEFFCT)\s+"),
                    PtpInstructionCount = CountToken(content, @"(?m)^\s*PTP\b"),
                    LinInstructionCount = CountToken(content, @"(?m)^\s*LIN\b"),
                    CircInstructionCount = CountToken(content, @"(?m)^\s*CIRC\b"),
                    SplineTokenCount = CountToken(content, @"(?i)\b(?:SPLINE|SPL|SLIN|SCIRC)\b"),
                    Files = group.Select(path => ToIdentity(path, root)).OrderBy(value => value.RelativePath, StringComparer.Ordinal).ToList()
                };
            })
            .OrderBy(value => value.ModuleName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ParseWorkVisualGeneratorVersions(string root)
    {
        var pattern = new Regex(
            @"WorkVisual\s+V(?<version>\d+(?:\.\d+){2}(?:_Build\d+)?)",
            RegexOptions.CultureInvariant);
        return Directory.EnumerateFiles(root, "*.xml", SearchOption.AllDirectories)
            .SelectMany(path => pattern.Matches(File.ReadAllText(path)).Select(match => match.Groups["version"].Value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static XDocument LoadXml(string path)
    {
        using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static XDocument? LoadOptionalXml(string path) => File.Exists(path) ? LoadXml(path) : null;

    private static (int Numerator, int Denominator) ReadRatio(string content, string symbol, int index)
    {
        var match = Regex.Match(content, $@"(?m)^\s*{Regex.Escape(symbol)}\[{index}\]\s*=\s*{{\s*N\s+(?<n>[+-]?\d+)\s*,\s*D\s+(?<d>[+-]?\d+)\s*}}", RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            throw new InvalidDataException($"Required ratio {symbol}[{index}] was not found.");
        }

        return (
            int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["d"].Value, CultureInfo.InvariantCulture));
    }

    private static double ReadNumber(string content, string symbol, int index)
    {
        var match = Regex.Match(content, $@"(?m)^\s*{Regex.Escape(symbol)}\[{index}\]\s*=\s*(?<value>[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[Ee][+-]?\d+)?)", RegexOptions.CultureInvariant);
        if (!match.Success || !double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidDataException($"Required numeric assignment {symbol}[{index}] was not found.");
        }

        return value;
    }

    private static int NetmaskHexToPrefix(string? mask)
    {
        if (string.IsNullOrWhiteSpace(mask) || mask.Length != 8 || !uint.TryParse(mask, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return 0;
        }

        var prefix = 0;
        var zeroSeen = false;
        for (var bit = 31; bit >= 0; bit--)
        {
            var one = (value & (1u << bit)) != 0;
            if (zeroSeen && one)
            {
                return 0;
            }

            if (one) prefix++; else zeroSeen = true;
        }

        return prefix;
    }

    private static bool ParseBool(string? value) => bool.TryParse(value, out var parsed) && parsed;

    private static int CountLines(string text) => string.IsNullOrEmpty(text) ? 0 : text.Count(character => character == '\n') + 1;

    private static int CountToken(string text, string pattern) => Regex.Matches(text, pattern, RegexOptions.CultureInvariant).Count;

    private static List<ControllerProjectFileIdentity> CaptureFiles(string root, string relativeRoot) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => ToIdentity(path, relativeRoot))
            .OrderBy(value => value.RelativePath, StringComparer.Ordinal)
            .ToList();

    private static ControllerProjectFileIdentity ToIdentity(string path, string relativeRoot)
    {
        var info = new FileInfo(path);
        return new ControllerProjectFileIdentity
        {
            RelativePath = Path.GetRelativePath(relativeRoot, path).Replace('\\', '/'),
            Bytes = info.Length,
            Sha256 = ComputeSha256(path)
        };
    }

    private static EnvironmentFileObservation ObserveFile(string id, string path)
    {
        var info = new FileInfo(path);
        return info.Exists
            ? new EnvironmentFileObservation { Id = id, Path = info.FullName, Exists = true, Bytes = info.Length, Sha256 = ComputeSha256(path) }
            : new EnvironmentFileObservation { Id = id, Path = Path.GetFullPath(path), Exists = false };
    }

    private static EnvironmentFileObservation NormalizeObservationPath(string root, EnvironmentFileObservation value) =>
        value with { Path = Path.GetRelativePath(root, value.Path).Replace('\\', '/') };

    private static bool SameTreeIdentity(WorkVisualExtractedTreeIdentity left, WorkVisualExtractedTreeIdentity right) =>
        left.FileCount == right.FileCount
        && left.TotalBytes == right.TotalBytes
        && string.Equals(left.TreeSha256, right.TreeSha256, StringComparison.OrdinalIgnoreCase);

    private static void CopyTree(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
    }

    private static void EnsureVaultBoundary(string projectPath, string extractedRoot, string vault)
    {
        foreach (var source in new[] { Path.GetDirectoryName(Path.GetFullPath(projectPath))!, Path.GetFullPath(extractedRoot) })
        {
            var sourcePrefix = source.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var vaultPrefix = vault.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (vault.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase)
                || source.StartsWith(vaultPrefix, StringComparison.OrdinalIgnoreCase)
                || string.Equals(source, vault, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Protected vault must be disjoint from source and extraction evidence directories.");
            }
        }
    }

    private static void WriteNewText(string path, string text)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(text);
        writer.WriteLine();
        writer.Flush();
        stream.Flush(true);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void ValidateId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("Attempt ID is invalid.", nameof(value));
        }
    }

    private static EnvironmentCheck Passed(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Passed, Detail = detail };

    private static EnvironmentCheck Failed(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Failed, Detail = detail };

    private static ComponentArchive ReadComponent(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("KUKA.Sim component is missing.", path);
        }

        using var archive = ZipFile.OpenRead(path);
        var modelEntry = archive.GetEntry("model.xml") ?? throw new InvalidDataException("KUKA.Sim component lacks model.xml.");
        string name;
        string detailedRevision;
        using (var stream = modelEntry.Open())
        using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
        {
            var model = XDocument.Load(reader, LoadOptions.None);
            var properties = model.Descendants().Where(element => element.Name.LocalName == "Property")
                .ToDictionary(element => element.Attribute("name")?.Value ?? string.Empty, element => element.Value, StringComparer.Ordinal);
            name = properties.GetValueOrDefault("Name") ?? string.Empty;
            detailedRevision = properties.GetValueOrDefault("DetailedRevision") ?? string.Empty;
        }

        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            var normalized = Regex.Replace(entry.FullName, "__rsimrrsrobotcontroller_[^/]+", "__CONTROLLER__", RegexOptions.CultureInvariant);
            if (entry.FullName.EndsWith('/'))
            {
                entries[normalized] = "DIR";
                continue;
            }

            using var entryStream = entry.Open();
            entries[normalized] = Convert.ToHexString(SHA256.HashData(entryStream));
        }

        return new ComponentArchive(name, detailedRevision, entries);
    }

    private sealed record ComponentArchive(string Name, string DetailedRevision, Dictionary<string, string> Entries);
}

public sealed record ControllerProjectBaselineVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}

public static class ControllerProjectBaselineReceiptVerifier
{
    public static ControllerProjectBaselineVerificationResult Verify(ControllerProjectBaselineReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (ReferenceEquals(receipt.Payload, null))
        {
            return new ControllerProjectBaselineVerificationResult { Errors = ["payload is required"] };
        }

        var payload = receipt.Payload;
        var canonical = ReceiptSerialization.ComputeCanonicalSha256(payload);
        if (receipt.SchemaIdentity != ControllerProjectBaselineContract.ReceiptSchemaIdentity
            || receipt.SchemaVersion != ControllerProjectBaselineContract.ReceiptSchemaVersion)
        {
            errors.Add("schema identity/version is unsupported");
        }

        if (!string.Equals(canonical, receipt.PayloadSha256, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("payload SHA-256 does not match canonical payload");
        }

        if (string.IsNullOrWhiteSpace(payload.ReceiptId)
            || string.IsNullOrWhiteSpace(payload.AttemptId)
            || payload.ReceiptId != $"controller-project-baseline-{payload.AttemptId}"
            || !IsSha(payload.CoreAssemblySha256)
            || payload.StartedAtUtc > payload.CompletedAtUtc
            || payload.DurationMilliseconds < 0)
        {
            errors.Add("receipt, attempt, core or timing identity is invalid");
        }

        if (payload.ExtractionReceipt is null
            || !WorkVisualProjectExtractionReceiptVerifier.Verify(payload.ExtractionReceipt).Succeeded
            || !string.Equals(
                payload.ExtractionReceiptSha256,
                ReceiptSerialization.ComputeCanonicalSha256(payload.ExtractionReceipt),
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("nested extraction receipt is invalid or not hash-bound");
        }

        if (payload.OriginalProjectChanged
            || payload.ExtractionEvidenceChanged
            || payload.ControllerAccessed
            || payload.NetworkTrafficSent
            || payload.CredentialsUsed
            || payload.LicenseStateRead
            || payload.SafetyContentInterpreted
            || payload.VendorApplicationInvoked
            || payload.NativeKssStatus != NativeKssStatus.NotRun)
        {
            errors.Add("baseline safety claims are invalid");
        }

        if (!(payload.UnsupportedGaps ?? []).SequenceEqual(ControllerProjectBaselineContract.RequiredUnsupportedGaps, StringComparer.Ordinal))
        {
            errors.Add("unsupported gaps do not preserve the exact evidence boundary");
        }

        var checks = payload.Checks ?? [];
        var ids = checks.Select(check => check.Id).ToArray();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length
            || ids.Any(id => !ControllerProjectBaselineContract.RequiredCheckIds.Contains(id, StringComparer.Ordinal))
            || (payload.TerminalClassification == EnvironmentTerminalClassification.Ready
                && !ControllerProjectBaselineContract.RequiredCheckIds.All(id => ids.Contains(id, StringComparer.Ordinal))))
        {
            errors.Add("checks are duplicated, unknown or incomplete for a Ready receipt");
        }

        if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready)
        {
            if (checks.Any(check => check.Status != EnvironmentCheckStatus.Passed)
                || !payload.VaultCreated
                || !payload.EnvironmentReusable
                || payload.Baseline is null
                || !ValidFile(payload.OriginalProject)
                || !ValidFile(payload.ExactKukaSimComponent)
                || !ValidFile(payload.GenericKukaSimComponent)
                || !ValidFile(payload.VaultProject)
                || !ValidFile(payload.VaultSummary)
                || !ValidFile(payload.VaultManifest)
                || !Directory.Exists(payload.VaultDirectory)
                || payload.VaultExtractedTree.FileCount <= 0
                || payload.VaultTree.FileCount <= 0)
            {
                errors.Add("Ready receipt lacks accepted source, component, vault, summary, manifest or cleanup evidence");
            }
            else
            {
                VerifyFile(payload.OriginalProject, errors);
                VerifyFile(payload.ExactKukaSimComponent, errors);
                VerifyFile(payload.GenericKukaSimComponent, errors);
                VerifyFile(payload.VaultProject, errors);
                VerifyFile(payload.VaultSummary, errors);
                VerifyFile(payload.VaultManifest, errors);
                var currentExtracted = WorkVisualProjectExtractionRunner.CaptureTree(Path.Combine(payload.VaultDirectory, "extracted-project"));
                if (!SameTree(currentExtracted, payload.VaultExtractedTree)) errors.Add("protected extracted tree drifted");
                var currentVault = WorkVisualProjectExtractionRunner.CaptureTree(payload.VaultDirectory);
                if (!SameTree(currentVault, payload.VaultTree)) errors.Add("protected vault tree drifted");
                try
                {
                    var summary = ReceiptSerialization.ControllerProjectSoftwareBaselineFromJson(File.ReadAllText(payload.VaultSummary.Path));
                    var reparsed = ControllerProjectBaselineRunner.Analyze(
                        Path.Combine(payload.VaultDirectory, "extracted-project"),
                        payload.ExactKukaSimComponent.Path,
                        payload.GenericKukaSimComponent.Path);
                    if (!SameCanonical(summary, payload.Baseline) || !SameCanonical(reparsed, payload.Baseline))
                    {
                        errors.Add("baseline summary does not match current protected evidence");
                    }
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or XmlException or System.Text.Json.JsonException)
                {
                    errors.Add($"baseline summary could not be reparsed: {exception.Message}");
                }
            }
        }
        else if (payload.TerminalClassification != EnvironmentTerminalClassification.Failed
            || !checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
            || payload.VaultCreated)
        {
            errors.Add("Failed classification must contain a failed check and no finalized vault");
        }

        return new ControllerProjectBaselineVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = canonical,
            Errors = errors
        };
    }

    private static bool SameCanonical<T>(T left, T right) => string.Equals(
        ReceiptSerialization.ComputeCanonicalSha256(left),
        ReceiptSerialization.ComputeCanonicalSha256(right),
        StringComparison.OrdinalIgnoreCase);

    private static bool SameTree(WorkVisualExtractedTreeIdentity left, WorkVisualExtractedTreeIdentity right) =>
        left.FileCount == right.FileCount
        && left.TotalBytes == right.TotalBytes
        && string.Equals(left.TreeSha256, right.TreeSha256, StringComparison.OrdinalIgnoreCase);

    private static bool ValidFile(EnvironmentFileObservation value) =>
        value is not null && value.Exists && value.Bytes >= 0 && IsSha(value.Sha256 ?? string.Empty);

    private static void VerifyFile(EnvironmentFileObservation value, List<string> errors)
    {
        if (!File.Exists(value.Path))
        {
            errors.Add($"evidence file is missing: {value.Id}");
            return;
        }

        var info = new FileInfo(value.Path);
        using var stream = File.OpenRead(value.Path);
        var sha = Convert.ToHexString(SHA256.HashData(stream));
        if (info.Length != value.Bytes || !string.Equals(sha, value.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"evidence file drifted: {value.Id}");
        }
    }

    private static bool IsSha(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
}

public static class ControllerProjectBaselineReceiptWriter
{
    public static string WriteNew(string outputPath, ControllerProjectBaselineReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        var verification = ControllerProjectBaselineReceiptVerifier.Verify(receipt);
        if (!verification.Succeeded)
        {
            throw new InvalidOperationException("Controller-project baseline receipt integrity is invalid: " + string.Join("; ", verification.Errors));
        }

        var fullPath = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Receipt output must use the .json extension.", nameof(outputPath));
        }

        var parent = Path.GetDirectoryName(fullPath) ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        Directory.CreateDirectory(parent);
        using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(ReceiptSerialization.ToJson(receipt));
        writer.WriteLine();
        writer.Flush();
        stream.Flush(true);
        return fullPath;
    }
}
