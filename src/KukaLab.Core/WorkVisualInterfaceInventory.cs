using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class WorkVisualInterfaceInventoryContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.workvisual-interface-inventory-receipt";
    public const int ReceiptSchemaVersion = 1;

    internal static readonly IReadOnlyList<string> RequiredCapabilityIds =
    [
        "controller-project-inventory",
        "controller-project-download",
        "controller-repository-read",
        "controller-repository-write",
        "program-control",
        "runtime-messages",
        "runtime-diagnostics",
        "monitoring-logs",
        "workonline-ui-commands"
    ];

    internal static readonly IReadOnlyList<string> RequiredFileIds =
    [
        "workvisual-script-runner",
        "krc-online-scripting-assembly",
        "krc-online-scripting-documentation",
        "online-services-facade-assembly",
        "online-services-contracts-assembly",
        "workonline-configuration"
    ];

    internal static readonly IReadOnlyList<string> RequiredUnsupportedGaps =
    [
        "Public interface presence does not prove that a controller endpoint is reachable or authenticated.",
        "No WorkVisual, OfficeLite, KUKA.Sim or physical-controller operation was executed by this inventory.",
        "Detected repository-write and program-control methods were not invoked.",
        "Native KSS compilation and execution remain NotRun.",
        "The OfficeLite robot profile is not matched to KR 210 R2700-2 by this inventory."
    ];
}

public enum WorkVisualInterfaceEvidenceStatus
{
    ConfirmedPublicInterface,
    ConfirmedConfiguredUiCommand,
    Missing
}

public enum WorkVisualOperationEffect
{
    ReadOnly,
    HostWriteOnly,
    ControllerWrite,
    ProgramControl,
    DiagnosticRead
}

public sealed record WorkVisualInterfaceInventoryRequest
{
    public required string InstallRoot { get; init; }

    public static WorkVisualInterfaceInventoryRequest CreateDefault(string? installRoot = null)
    {
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return new WorkVisualInterfaceInventoryRequest
        {
            InstallRoot = Path.GetFullPath(installRoot
                ?? Path.Combine(programFilesX86, "KUKA", "WorkVisual 6.0"))
        };
    }
}

public sealed record WorkVisualInterfaceInventoryReceipt
{
    public string SchemaIdentity { get; init; } = WorkVisualInterfaceInventoryContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = WorkVisualInterfaceInventoryContract.ReceiptSchemaVersion;

    public required WorkVisualInterfaceInventoryPayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record WorkVisualInterfaceInventoryPayload
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

    public string InstallRoot { get; init; } = string.Empty;

    public List<EnvironmentFileObservation> Files { get; init; } = [];

    public List<EnvironmentCheck> Checks { get; init; } = [];

    public List<WorkVisualInterfaceCapability> Capabilities { get; init; } = [];

    public bool NetworkTrafficSent { get; init; }

    public bool ControllerMutationPerformed { get; init; }

    public NativeKssStatus NativeKssStatus { get; init; } = NativeKssStatus.NotRun;

    public List<string> SideEffects { get; init; } = [];

    public bool EnvironmentReusable { get; init; }

    public List<string> UnsupportedGaps { get; init; } = [];
}

public sealed record WorkVisualInterfaceCapability
{
    public string Id { get; init; } = string.Empty;

    public WorkVisualInterfaceEvidenceStatus Status { get; init; }

    public WorkVisualOperationEffect Effect { get; init; }

    public string SourceFileId { get; init; } = string.Empty;

    public string TypeOrConfiguration { get; init; } = string.Empty;

    public List<string> RequiredMembers { get; init; } = [];

    public List<string> ObservedMembers { get; init; } = [];

    public bool Executed { get; init; }

    public string Interpretation { get; init; } = string.Empty;
}

public sealed record WorkVisualInterfaceInventoryOutcome(WorkVisualInterfaceInventoryReceipt Receipt)
{
    public int ExitCode => Receipt.Payload.TerminalClassification switch
    {
        EnvironmentTerminalClassification.Ready => 0,
        EnvironmentTerminalClassification.Failed => 2,
        _ => 3
    };
}

public sealed class WorkVisualInterfaceInventoryCollector
{
    private const string ScriptingAssemblyFile = "Kuka.WorkVisual.Scripting.KrcOnline.dll";
    private const string ScriptingDocumentationFile = "Kuka.WorkVisual.Scripting.KrcOnline.xml";
    private const string OnlineFacadeAssemblyFile = "KukaRoboter.OnlineServicesFacade.dll";
    private const string ContractsAssemblyFile = "KukaRoboter.Contracts.dll";
    private const string WorkOnlineConfigurationFile = "wvsr.exe.WorkOnlineWorkVisual.config";
    private const string RunnerFile = "wvsr.exe";

    private static readonly Regex AttemptIdPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly TimeProvider _timeProvider;

    public WorkVisualInterfaceInventoryCollector(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public WorkVisualInterfaceInventoryOutcome Collect(
        WorkVisualInterfaceInventoryRequest request,
        string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptId);
        if (!AttemptIdPattern.IsMatch(attemptId))
        {
            throw new ArgumentException(
                "Attempt ID must match [A-Za-z0-9][A-Za-z0-9._-]{2,127}.",
                nameof(attemptId));
        }

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var root = Path.GetFullPath(request.InstallRoot);
        var checks = new List<EnvironmentCheck>();
        var files = new List<EnvironmentFileObservation>();
        var capabilities = new List<WorkVisualInterfaceCapability>();
        var snapshots = new Dictionary<string, MetadataTypeSnapshot>(StringComparer.Ordinal);
        var configurationText = string.Empty;
        var scriptingDocumentationText = string.Empty;

        if (!OperatingSystem.IsWindows())
        {
            checks.Add(Blocked("host-os", "WorkVisual interface inventory requires Windows installation evidence."));
            return Complete();
        }

        if (!Directory.Exists(root))
        {
            checks.Add(Blocked("install-root", $"WorkVisual install root is missing: {root}"));
            return Complete();
        }

        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
        {
            checks.Add(Failed("install-root", "WorkVisual install root cannot be a reparse point."));
            return Complete();
        }

        checks.Add(Passed("install-root", "WorkVisual install root exists and is not a reparse point."));
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["workvisual-script-runner"] = Path.Combine(root, RunnerFile),
            ["krc-online-scripting-assembly"] = Path.Combine(root, ScriptingAssemblyFile),
            ["krc-online-scripting-documentation"] = Path.Combine(root, ScriptingDocumentationFile),
            ["online-services-facade-assembly"] = Path.Combine(root, OnlineFacadeAssemblyFile),
            ["online-services-contracts-assembly"] = Path.Combine(root, ContractsAssemblyFile),
            ["workonline-configuration"] = Path.Combine(root, WorkOnlineConfigurationFile)
        };

        foreach (var pair in paths)
        {
            ObserveFile(pair.Key, pair.Value, files, checks);
        }

        if (checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked))
        {
            return Complete();
        }

        try
        {
            scriptingDocumentationText = File.ReadAllText(paths["krc-online-scripting-documentation"]);
            configurationText = File.ReadAllText(paths["workonline-configuration"]);
            snapshots["scripting-controller"] = ReadPublicTypeSnapshot(
                paths["krc-online-scripting-assembly"],
                "Kuka.WorkVisual.Scripting.KrcOnline.IScriptingOnlineController");
            snapshots["file-handling"] = ReadPublicTypeSnapshot(
                paths["online-services-facade-assembly"],
                "KukaRoboter.OnlineServicesFacade.IFileHandlingFacade");
            snapshots["runtime-interpreter"] = ReadPublicTypeSnapshot(
                paths["online-services-facade-assembly"],
                "KukaRoboter.OnlineServicesFacade.IRuntimeInterpreter");
            snapshots["runtime-messages"] = ReadPublicTypeSnapshot(
                paths["online-services-facade-assembly"],
                "KukaRoboter.OnlineServicesFacade.IRuntimeMessageWindowFacade");
            snapshots["runtime-manager"] = ReadPublicTypeSnapshot(
                paths["online-services-facade-assembly"],
                "KukaRoboter.OnlineServicesFacade.RuntimeManagerFacade");
            snapshots["monitoring"] = ReadPublicTypeSnapshot(
                paths["online-services-facade-assembly"],
                "KukaRoboter.OnlineServicesFacade.MonitoringFacade");
            checks.Add(Passed(
                "managed-metadata",
                "WorkVisual public managed metadata was read without loading or executing vendor assemblies."));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or BadImageFormatException
            or InvalidOperationException)
        {
            checks.Add(Failed("managed-metadata", $"WorkVisual metadata could not be read safely: {exception.Message}"));
            return Complete();
        }

        AddMetadataCapability(
            "controller-project-inventory",
            WorkVisualOperationEffect.ReadOnly,
            "krc-online-scripting-assembly",
            snapshots["scripting-controller"],
            ["GetProjects"],
            "Public scripting API can enumerate controller projects; controller reachability and authentication are separate.");
        AddMetadataCapability(
            "controller-project-download",
            WorkVisualOperationEffect.HostWriteOnly,
            "krc-online-scripting-assembly",
            snapshots["scripting-controller"],
            ["DownloadProject"],
            "Public scripting API can download a controller project to the host; this inventory did not call it.");
        AddMetadataCapability(
            "controller-repository-read",
            WorkVisualOperationEffect.ReadOnly,
            "online-services-facade-assembly",
            snapshots["file-handling"],
            ["Download", "DownloadStream", "GetFiles", "GetFileInfos", "FileExists"],
            "Public facade exposes controller-repository reads; no endpoint was contacted.");
        AddMetadataCapability(
            "controller-repository-write",
            WorkVisualOperationEffect.ControllerWrite,
            "online-services-facade-assembly",
            snapshots["file-handling"],
            ["Upload", "UploadStream", "CreateDirectory", "DeleteFile", "MoveFile"],
            "Controller-write methods are present but deliberately excluded from this inventory execution.");
        AddMetadataCapability(
            "program-control",
            WorkVisualOperationEffect.ProgramControl,
            "online-services-facade-assembly",
            snapshots["runtime-interpreter"],
            ["Select", "Deselect", "Start", "Stop", "Reset", "SetProgramMode", "Mode", "State", "GetLinkedFiles"],
            "High-level program-control methods plus explicit mode/state members are present but were not invoked.");
        AddMetadataCapability(
            "runtime-messages",
            WorkVisualOperationEffect.DiagnosticRead,
            "online-services-facade-assembly",
            snapshots["runtime-messages"],
            ["GetMessages", "IsEndpointReachable"],
            "Runtime message retrieval can supply native controller messages after a separately accepted connection.");
        AddMetadataCapability(
            "runtime-diagnostics",
            WorkVisualOperationEffect.DiagnosticRead,
            "online-services-facade-assembly",
            snapshots["runtime-manager"],
            ["GetErrors", "GetModulesWithErrors", "GetPosition", "Ping"],
            "Runtime diagnostics expose module errors and controller status without proving KSS execution here.");
        AddMetadataCapability(
            "monitoring-logs",
            WorkVisualOperationEffect.DiagnosticRead,
            "online-services-facade-assembly",
            snapshots["monitoring"],
            ["GetLogSources", "ReadLogMessages"],
            "Monitoring API exposes log sources and log messages; no log endpoint was contacted.");

        var uiCommands = new[]
        {
            "KukaRoboter.UploadRepositoryItems",
            "KukaRoboter.DownloadRepositoryItems",
            "KukaRoboter.SelectProgram",
            "KukaRoboter.DeselectProgram",
            "KukaRoboter.StartProgram",
            "KukaRoboter.StopProgram",
            "KukaRoboter.ResetProgram",
            "KRC: .src .dat .sub",
            "Versions=\"[8.5;\""
        };
        var observedUiCommands = uiCommands
            .Where(item => configurationText.Contains(item, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();
        capabilities.Add(new WorkVisualInterfaceCapability
        {
            Id = "workonline-ui-commands",
            Status = observedUiCommands.Count == uiCommands.Length
                ? WorkVisualInterfaceEvidenceStatus.ConfirmedConfiguredUiCommand
                : WorkVisualInterfaceEvidenceStatus.Missing,
            Effect = WorkVisualOperationEffect.ProgramControl,
            SourceFileId = "workonline-configuration",
            TypeOrConfiguration = WorkOnlineConfigurationFile,
            RequiredMembers = uiCommands.Order(StringComparer.Ordinal).ToList(),
            ObservedMembers = observedUiCommands,
            Executed = false,
            Interpretation = "Installed WorkOnline configuration declares SRC/DAT/SUB repository transfer and program-control UI commands; no UI command was run."
        });

        var scriptingDocsConfirmed = scriptingDocumentationText.Contains(
                "IScriptingOnlineController.GetProjects",
                StringComparison.Ordinal)
            && scriptingDocumentationText.Contains(
                "IScriptingOnlineController.DownloadProject",
                StringComparison.Ordinal);
        checks.Add(scriptingDocsConfirmed
            ? Passed("scripting-documentation", "Installed XML documentation confirms GetProjects and DownloadProject.")
            : Failed("scripting-documentation", "Installed XML documentation does not confirm both required scripting methods."));

        foreach (var capability in capabilities)
        {
            checks.Add(capability.Status == WorkVisualInterfaceEvidenceStatus.Missing
                ? Blocked($"capability:{capability.Id}", "One or more required public/configured members are missing.")
                : Passed($"capability:{capability.Id}", capability.Interpretation));
        }

        return Complete();

        void AddMetadataCapability(
            string id,
            WorkVisualOperationEffect effect,
            string sourceFileId,
            MetadataTypeSnapshot snapshot,
            IReadOnlyList<string> requiredMembers,
            string interpretation)
        {
            var required = requiredMembers.Order(StringComparer.Ordinal).ToList();
            var observed = required
                .Where(snapshot.PublicMembers.Contains)
                .Order(StringComparer.Ordinal)
                .ToList();
            capabilities.Add(new WorkVisualInterfaceCapability
            {
                Id = id,
                Status = snapshot.TypeFound && observed.Count == required.Count
                    ? WorkVisualInterfaceEvidenceStatus.ConfirmedPublicInterface
                    : WorkVisualInterfaceEvidenceStatus.Missing,
                Effect = effect,
                SourceFileId = sourceFileId,
                TypeOrConfiguration = snapshot.TypeName,
                RequiredMembers = required,
                ObservedMembers = observed,
                Executed = false,
                Interpretation = interpretation
            });
        }

        WorkVisualInterfaceInventoryOutcome Complete()
        {
            stopwatch.Stop();
            var terminal = checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
                ? EnvironmentTerminalClassification.Failed
                : checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked)
                    ? EnvironmentTerminalClassification.Blocked
                    : EnvironmentTerminalClassification.Ready;
            var payload = new WorkVisualInterfaceInventoryPayload
            {
                ReceiptId = $"workvisual-interface-inventory-{attemptId}",
                AttemptId = attemptId,
                CoreAssemblySha256 = ComputeFileSha256(typeof(WorkVisualInterfaceInventoryCollector).Assembly.Location),
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
                InstallRoot = root,
                Files = files,
                Checks = checks,
                Capabilities = capabilities,
                NetworkTrafficSent = false,
                ControllerMutationPerformed = false,
                NativeKssStatus = NativeKssStatus.NotRun,
                EnvironmentReusable = true,
                UnsupportedGaps = WorkVisualInterfaceInventoryContract.RequiredUnsupportedGaps.ToList()
            };
            var receipt = new WorkVisualInterfaceInventoryReceipt
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
            };
            return new WorkVisualInterfaceInventoryOutcome(receipt);
        }
    }

    private static void ObserveFile(
        string id,
        string path,
        List<EnvironmentFileObservation> files,
        List<EnvironmentCheck> checks)
    {
        if (!File.Exists(path))
        {
            files.Add(new EnvironmentFileObservation { Id = id, Path = path, Exists = false });
            checks.Add(Blocked(id, $"Required WorkVisual file is missing: {path}"));
            return;
        }

        try
        {
            var info = new FileInfo(path);
            var version = FileVersionInfo.GetVersionInfo(path);
            files.Add(new EnvironmentFileObservation
            {
                Id = id,
                Path = Path.GetFullPath(path),
                Exists = true,
                Bytes = info.Length,
                Sha256 = ComputeFileSha256(path),
                FileVersion = string.IsNullOrWhiteSpace(version.FileVersion) ? null : version.FileVersion,
                ProductVersion = string.IsNullOrWhiteSpace(version.ProductVersion) ? null : version.ProductVersion
            });
            checks.Add(Passed(id, "Required WorkVisual file exists and was hash-bound."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            checks.Add(Failed(id, $"Required WorkVisual file could not be inventoried: {exception.Message}"));
        }
    }

    private static MetadataTypeSnapshot ReadPublicTypeSnapshot(string assemblyPath, string fullTypeName)
    {
        using var stream = new FileStream(
            assemblyPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var peReader = new PEReader(stream, PEStreamOptions.LeaveOpen);
        if (!peReader.HasMetadata)
        {
            throw new BadImageFormatException($"Managed metadata is missing from {assemblyPath}.");
        }

        var reader = peReader.GetMetadataReader();
        foreach (var handle in reader.TypeDefinitions)
        {
            var definition = reader.GetTypeDefinition(handle);
            var name = reader.GetString(definition.Name);
            var @namespace = reader.GetString(definition.Namespace);
            var candidate = string.IsNullOrEmpty(@namespace) ? name : $"{@namespace}.{name}";
            if (!string.Equals(candidate, fullTypeName, StringComparison.Ordinal))
            {
                continue;
            }

            var visibility = definition.Attributes & TypeAttributes.VisibilityMask;
            if (visibility != TypeAttributes.Public)
            {
                return new MetadataTypeSnapshot(fullTypeName, false, []);
            }

            var members = definition.GetMethods()
                .Select(reader.GetMethodDefinition)
                .Where(method => (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public)
                .Select(method => reader.GetString(method.Name))
                .Where(nameValue => !nameValue.StartsWith("get_", StringComparison.Ordinal)
                    && !nameValue.StartsWith("set_", StringComparison.Ordinal)
                    && !nameValue.StartsWith("add_", StringComparison.Ordinal)
                    && !nameValue.StartsWith("remove_", StringComparison.Ordinal)
                    && !string.Equals(nameValue, ".ctor", StringComparison.Ordinal))
                .ToHashSet(StringComparer.Ordinal);
            foreach (var propertyHandle in definition.GetProperties())
            {
                var property = reader.GetPropertyDefinition(propertyHandle);
                var accessors = property.GetAccessors();
                var hasPublicGetter = !accessors.Getter.IsNil
                    && (reader.GetMethodDefinition(accessors.Getter).Attributes & MethodAttributes.MemberAccessMask)
                        == MethodAttributes.Public;
                var hasPublicSetter = !accessors.Setter.IsNil
                    && (reader.GetMethodDefinition(accessors.Setter).Attributes & MethodAttributes.MemberAccessMask)
                        == MethodAttributes.Public;
                if (hasPublicGetter || hasPublicSetter)
                {
                    members.Add(reader.GetString(property.Name));
                }
            }

            return new MetadataTypeSnapshot(fullTypeName, true, members);
        }

        return new MetadataTypeSnapshot(fullTypeName, false, []);
    }

    private static string ComputeFileSha256(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static EnvironmentCheck Passed(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Passed, Detail = detail };

    private static EnvironmentCheck Blocked(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Blocked, Detail = detail };

    private static EnvironmentCheck Failed(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Failed, Detail = detail };

    private sealed record MetadataTypeSnapshot(
        string TypeName,
        bool TypeFound,
        HashSet<string> PublicMembers);
}

public sealed record WorkVisualInterfaceInventoryVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public bool CurrentInstallVerified { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}

public static class WorkVisualInterfaceInventoryReceiptVerifier
{
    private static readonly Regex Sha256Pattern = new(
        "^[A-F0-9]{64}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static WorkVisualInterfaceInventoryVerificationResult Verify(
        WorkVisualInterfaceInventoryReceipt receipt,
        bool verifyCurrentInstall = false)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (!string.Equals(
                receipt.SchemaIdentity,
                WorkVisualInterfaceInventoryContract.ReceiptSchemaIdentity,
                StringComparison.Ordinal))
        {
            errors.Add($"schemaIdentity must be {WorkVisualInterfaceInventoryContract.ReceiptSchemaIdentity}");
        }

        if (receipt.SchemaVersion != WorkVisualInterfaceInventoryContract.ReceiptSchemaVersion)
        {
            errors.Add($"schemaVersion must be {WorkVisualInterfaceInventoryContract.ReceiptSchemaVersion}");
        }

        if (ReferenceEquals(receipt.Payload, null))
        {
            return new WorkVisualInterfaceInventoryVerificationResult
            {
                Succeeded = false,
                PayloadSha256 = receipt.PayloadSha256,
                Errors = ["payload is required"]
            };
        }

        var payload = receipt.Payload;
        if (string.IsNullOrWhiteSpace(payload.ReceiptId)
            || string.IsNullOrWhiteSpace(payload.AttemptId)
            || string.IsNullOrWhiteSpace(payload.InstallRoot))
        {
            errors.Add("receiptId, attemptId and installRoot are required");
        }

        if (!IsSha256(payload.CoreAssemblySha256))
        {
            errors.Add("coreAssemblySha256 is invalid");
        }

        if (payload.NetworkTrafficSent || payload.ControllerMutationPerformed)
        {
            errors.Add("interface inventory cannot send network traffic or mutate a controller");
        }

        if (payload.NativeKssStatus != NativeKssStatus.NotRun)
        {
            errors.Add("nativeKssStatus must remain NotRun");
        }

        if (!payload.EnvironmentReusable)
        {
            errors.Add("environmentReusable must be true for a read-only metadata inventory");
        }

        if (payload.Capabilities.Any(capability => capability.Executed))
        {
            errors.Add("capabilities must be evidence-only and cannot be marked executed");
        }

        var expectedIds = WorkVisualInterfaceInventoryContract.RequiredCapabilityIds.Order(StringComparer.Ordinal).ToList();
        var actualIds = payload.Capabilities.Select(capability => capability.Id).Order(StringComparer.Ordinal).ToList();
        if ((payload.TerminalClassification == EnvironmentTerminalClassification.Ready
                || payload.Capabilities.Count > 0)
            && !expectedIds.SequenceEqual(actualIds, StringComparer.Ordinal))
        {
            errors.Add("capability set is incomplete, duplicated or unexpected");
        }

        foreach (var expectation in CapabilityExpectations)
        {
            var matches = payload.Capabilities
                .Where(capability => string.Equals(capability.Id, expectation.Id, StringComparison.Ordinal))
                .ToList();
            if (matches.Count == 1)
            {
                ValidateCapability(matches[0], expectation, payload.Checks, errors);
            }
        }

        if (payload.UnsupportedGaps.Count != WorkVisualInterfaceInventoryContract.RequiredUnsupportedGaps.Count
            || !payload.UnsupportedGaps.SequenceEqual(
                WorkVisualInterfaceInventoryContract.RequiredUnsupportedGaps,
                StringComparer.Ordinal))
        {
            errors.Add("unsupportedGaps do not match the required boundary statements");
        }

        var expectedFileIds = WorkVisualInterfaceInventoryContract.RequiredFileIds.Order(StringComparer.Ordinal).ToList();
        var actualFileIds = payload.Files.Select(file => file.Id).Order(StringComparer.Ordinal).ToList();
        if ((payload.TerminalClassification == EnvironmentTerminalClassification.Ready
                || payload.Files.Count > 0)
            && !expectedFileIds.SequenceEqual(actualFileIds, StringComparer.Ordinal))
        {
            errors.Add("file observation set is incomplete, duplicated or unexpected");
        }

        foreach (var file in payload.Files)
        {
            if (file.Exists && (!file.Bytes.HasValue || file.Bytes < 1 || !IsSha256(file.Sha256)))
            {
                errors.Add($"file observation is incomplete: {file.Id}");
            }
        }

        if (payload.SideEffects.Count > 1
            || payload.SideEffects.Any(effect => !effect.StartsWith("CreateNewReceiptFile:", StringComparison.Ordinal)))
        {
            errors.Add("sideEffects may contain only one create-new receipt declaration");
        }

        var hasFailed = payload.Checks.Any(check => check.Status == EnvironmentCheckStatus.Failed);
        var hasBlocked = payload.Checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked);
        var expectedTerminal = hasFailed
            ? EnvironmentTerminalClassification.Failed
            : hasBlocked
                ? EnvironmentTerminalClassification.Blocked
                : EnvironmentTerminalClassification.Ready;
        if (payload.TerminalClassification != expectedTerminal)
        {
            errors.Add("terminalClassification does not match check outcomes");
        }

        var computedPayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload);
        if (!IsSha256(receipt.PayloadSha256)
            || !string.Equals(receipt.PayloadSha256, computedPayloadSha256, StringComparison.Ordinal))
        {
            errors.Add("payloadSha256 does not match canonical payload bytes");
        }

        var currentInstallVerified = false;
        if (verifyCurrentInstall)
        {
            currentInstallVerified = VerifyCurrentFiles(payload.Files, errors);
        }

        return new WorkVisualInterfaceInventoryVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            CurrentInstallVerified = currentInstallVerified && errors.Count == 0,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }

    private static bool VerifyCurrentFiles(
        IEnumerable<EnvironmentFileObservation> files,
        List<string> errors)
    {
        var succeeded = true;
        foreach (var file in files.Where(candidate => candidate.Exists))
        {
            if (!File.Exists(file.Path))
            {
                errors.Add($"current WorkVisual file is missing: {file.Id}");
                succeeded = false;
                continue;
            }

            try
            {
                var info = new FileInfo(file.Path);
                using var stream = new FileStream(
                    file.Path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                var currentSha256 = Convert.ToHexString(SHA256.HashData(stream));
                if (info.Length != file.Bytes
                    || !string.Equals(currentSha256, file.Sha256, StringComparison.Ordinal))
                {
                    errors.Add($"current WorkVisual file identity changed: {file.Id}");
                    succeeded = false;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                errors.Add($"current WorkVisual file could not be verified: {file.Id}: {exception.Message}");
                succeeded = false;
            }
        }

        return succeeded;
    }

    private static void ValidateCapability(
        WorkVisualInterfaceCapability capability,
        CapabilityExpectation expectation,
        IReadOnlyList<EnvironmentCheck> checks,
        List<string> errors)
    {
        if (capability.Effect != expectation.Effect
            || !string.Equals(capability.SourceFileId, expectation.SourceFileId, StringComparison.Ordinal)
            || !string.Equals(capability.TypeOrConfiguration, expectation.TypeOrConfiguration, StringComparison.Ordinal))
        {
            errors.Add($"capability identity/effect is invalid: {capability.Id}");
        }

        var expectedMembers = expectation.RequiredMembers.Order(StringComparer.Ordinal).ToList();
        if (!capability.RequiredMembers.SequenceEqual(expectedMembers, StringComparer.Ordinal)
            || capability.ObservedMembers.Count != capability.ObservedMembers.Distinct(StringComparer.Ordinal).Count()
            || capability.ObservedMembers.Any(member => !expectedMembers.Contains(member, StringComparer.Ordinal)))
        {
            errors.Add($"capability members are invalid: {capability.Id}");
        }

        var allObserved = capability.ObservedMembers
            .Order(StringComparer.Ordinal)
            .SequenceEqual(expectedMembers, StringComparer.Ordinal);
        var expectedStatus = allObserved
            ? expectation.ConfirmedStatus
            : WorkVisualInterfaceEvidenceStatus.Missing;
        if (capability.Status != expectedStatus || string.IsNullOrWhiteSpace(capability.Interpretation))
        {
            errors.Add($"capability status/interpretation is invalid: {capability.Id}");
        }

        var capabilityChecks = checks
            .Where(check => string.Equals(check.Id, $"capability:{capability.Id}", StringComparison.Ordinal))
            .ToList();
        var expectedCheckStatus = expectedStatus == WorkVisualInterfaceEvidenceStatus.Missing
            ? EnvironmentCheckStatus.Blocked
            : EnvironmentCheckStatus.Passed;
        if (capabilityChecks.Count != 1 || capabilityChecks[0].Status != expectedCheckStatus)
        {
            errors.Add($"capability check is missing, duplicated or inconsistent: {capability.Id}");
        }
    }

    private static bool IsSha256(string? value) =>
        !string.IsNullOrEmpty(value) && Sha256Pattern.IsMatch(value);

    private static readonly IReadOnlyList<CapabilityExpectation> CapabilityExpectations =
    [
        new(
            "controller-project-inventory",
            WorkVisualInterfaceEvidenceStatus.ConfirmedPublicInterface,
            WorkVisualOperationEffect.ReadOnly,
            "krc-online-scripting-assembly",
            "Kuka.WorkVisual.Scripting.KrcOnline.IScriptingOnlineController",
            ["GetProjects"]),
        new(
            "controller-project-download",
            WorkVisualInterfaceEvidenceStatus.ConfirmedPublicInterface,
            WorkVisualOperationEffect.HostWriteOnly,
            "krc-online-scripting-assembly",
            "Kuka.WorkVisual.Scripting.KrcOnline.IScriptingOnlineController",
            ["DownloadProject"]),
        new(
            "controller-repository-read",
            WorkVisualInterfaceEvidenceStatus.ConfirmedPublicInterface,
            WorkVisualOperationEffect.ReadOnly,
            "online-services-facade-assembly",
            "KukaRoboter.OnlineServicesFacade.IFileHandlingFacade",
            ["Download", "DownloadStream", "FileExists", "GetFileInfos", "GetFiles"]),
        new(
            "controller-repository-write",
            WorkVisualInterfaceEvidenceStatus.ConfirmedPublicInterface,
            WorkVisualOperationEffect.ControllerWrite,
            "online-services-facade-assembly",
            "KukaRoboter.OnlineServicesFacade.IFileHandlingFacade",
            ["CreateDirectory", "DeleteFile", "MoveFile", "Upload", "UploadStream"]),
        new(
            "program-control",
            WorkVisualInterfaceEvidenceStatus.ConfirmedPublicInterface,
            WorkVisualOperationEffect.ProgramControl,
            "online-services-facade-assembly",
            "KukaRoboter.OnlineServicesFacade.IRuntimeInterpreter",
            ["Deselect", "GetLinkedFiles", "Mode", "Reset", "Select", "SetProgramMode", "Start", "State", "Stop"]),
        new(
            "runtime-messages",
            WorkVisualInterfaceEvidenceStatus.ConfirmedPublicInterface,
            WorkVisualOperationEffect.DiagnosticRead,
            "online-services-facade-assembly",
            "KukaRoboter.OnlineServicesFacade.IRuntimeMessageWindowFacade",
            ["GetMessages", "IsEndpointReachable"]),
        new(
            "runtime-diagnostics",
            WorkVisualInterfaceEvidenceStatus.ConfirmedPublicInterface,
            WorkVisualOperationEffect.DiagnosticRead,
            "online-services-facade-assembly",
            "KukaRoboter.OnlineServicesFacade.RuntimeManagerFacade",
            ["GetErrors", "GetModulesWithErrors", "GetPosition", "Ping"]),
        new(
            "monitoring-logs",
            WorkVisualInterfaceEvidenceStatus.ConfirmedPublicInterface,
            WorkVisualOperationEffect.DiagnosticRead,
            "online-services-facade-assembly",
            "KukaRoboter.OnlineServicesFacade.MonitoringFacade",
            ["GetLogSources", "ReadLogMessages"]),
        new(
            "workonline-ui-commands",
            WorkVisualInterfaceEvidenceStatus.ConfirmedConfiguredUiCommand,
            WorkVisualOperationEffect.ProgramControl,
            "workonline-configuration",
            "wvsr.exe.WorkOnlineWorkVisual.config",
            [
                "KRC: .src .dat .sub",
                "KukaRoboter.DeselectProgram",
                "KukaRoboter.DownloadRepositoryItems",
                "KukaRoboter.ResetProgram",
                "KukaRoboter.SelectProgram",
                "KukaRoboter.StartProgram",
                "KukaRoboter.StopProgram",
                "KukaRoboter.UploadRepositoryItems",
                "Versions=\"[8.5;\""
            ])
    ];

    private sealed record CapabilityExpectation(
        string Id,
        WorkVisualInterfaceEvidenceStatus ConfirmedStatus,
        WorkVisualOperationEffect Effect,
        string SourceFileId,
        string TypeOrConfiguration,
        IReadOnlyList<string> RequiredMembers);
}

public static class WorkVisualInterfaceInventoryReceiptWriter
{
    public static string WriteNew(string outputPath, WorkVisualInterfaceInventoryReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        Directory.CreateDirectory(directory);
        using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
        writer.Write(ReceiptSerialization.ToJson(receipt));
        writer.Flush();
        stream.Flush(flushToDisk: true);
        return fullPath;
    }
}
