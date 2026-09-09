using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class OfficeLiteExpertModeInterfaceInventoryContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.officelite-expert-mode-interface-inventory-receipt";
    public const int ReceiptSchemaVersion = 1;

    internal static readonly IReadOnlyList<string> RequiredFileIds =
    [
        "smarthmi-logon-assembly",
        "krc-security-assembly",
        "user-access-contracts-assembly",
        "user-access-service-assembly",
        "user-access-implementation-assembly",
        "workvisual-service-host-configuration"
    ];

    internal static readonly IReadOnlyDictionary<string, string> ExpectedFileNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["smarthmi-logon-assembly"] = "LogOn.dll",
            ["krc-security-assembly"] = "KUKARoboter.KrcSecurity.dll",
            ["user-access-contracts-assembly"] = "KukaRoboter.Contracts.dll",
            ["user-access-service-assembly"] = "KukaRoboter.Services.dll",
            ["user-access-implementation-assembly"] = "KukaRoboter.Services.Implementation.dll",
            ["workvisual-service-host-configuration"] = "WorkVisualServiceHost.exe.config"
        };

    internal static readonly IReadOnlyList<string> RequiredCheckIds =
    [
        "configured-user-access-endpoints",
        "endpoint-authentication",
        "guest-local-transition",
        "krc-security-assembly",
        "metadata-only",
        "remote-user-access",
        "smarthmi-logon-assembly",
        "smarthmi-logon-plugin",
        "user-access-contracts-assembly",
        "user-access-implementation-assembly",
        "user-access-service-assembly",
        "workvisual-service-host-configuration"
    ];

    internal const string AcceptedConclusion =
        "The installed remote WorkVisual UserAccess surface cannot perform the guest-local ExpertModeTransition; one bounded attended smartHMI transition is required before automated exact-profile activation resumes.";

    internal static readonly IReadOnlyList<string> RequiredLocalTransitionMembers =
    [
        "LogOff",
        "LockSystem",
        "LogonAnnouncedUser",
        "LogonDefaultUser",
        "LogonUser",
        "LogonWellKnownUser",
        "ResetLease"
    ];

    internal static readonly IReadOnlyList<string> RequiredRemoteUserAccessMembers =
    [
        "Connect",
        "Disconnect",
        "GetSpocStateInfo",
        "HasSpoc",
        "Operate",
        "Ping",
        "RequestSpoc",
        "StartRemoteOperating",
        "StopRemoteOperating"
    ];

    internal static readonly IReadOnlyList<string> RemoteTransitionCandidateMembers =
    [
        "ExpertModeTransition",
        "LogOff",
        "LogonAnnouncedUser",
        "LogonDefaultUser",
        "LogonUser",
        "LogonWellKnownUser",
        "SetUserGroup",
        "SetUserLevel"
    ];

    internal static readonly IReadOnlyList<string> RequiredConfiguredEndpoints =
    [
        "UserAccess",
        "UserAccessBasicAuthentication",
        "UserAccessCore"
    ];

    internal static readonly IReadOnlyList<string> RequiredUnsupportedGaps =
    [
        "Managed metadata and service configuration prove interface shape, not successful endpoint execution.",
        "The guest-local ExpertModeTransition was not executed by this inventory.",
        "WorkVisual UserAccess exposes SPOC and remote-operation coordination, not a guest-local user-level transition.",
        "BasicAuthenticationValidator authenticates a protected service endpoint and must not be relabeled as a smartHMI user-group transition.",
        "Exact-C01 activation, native KSS execution and the exact KUKA.Sim online loop remain NotRun."
    ];
}

public sealed record OfficeLiteExpertModeInterfaceInventoryRequest
{
    public required string SmartHmiLogonAssemblyPath { get; init; }

    public required string KrcSecurityAssemblyPath { get; init; }

    public required string UserAccessContractsAssemblyPath { get; init; }

    public required string UserAccessServiceAssemblyPath { get; init; }

    public required string UserAccessImplementationAssemblyPath { get; init; }

    public required string WorkVisualServiceHostConfigurationPath { get; init; }
}

public sealed record OfficeLiteExpertModeInterfaceInventoryReceipt
{
    public string SchemaIdentity { get; init; } = OfficeLiteExpertModeInterfaceInventoryContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = OfficeLiteExpertModeInterfaceInventoryContract.ReceiptSchemaVersion;

    public required OfficeLiteExpertModeInterfaceInventoryPayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record OfficeLiteExpertModeInterfaceInventoryPayload
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

    public List<EnvironmentFileObservation> Files { get; init; } = [];

    public List<EnvironmentCheck> Checks { get; init; } = [];

    public List<string> LocalTransitionMembers { get; init; } = [];

    public List<string> RemoteUserAccessMembers { get; init; } = [];

    public List<string> RemoteImplementationMembers { get; init; } = [];

    public List<string> RemoteTransitionMembers { get; init; } = [];

    public List<string> ConfiguredUserAccessEndpoints { get; init; } = [];

    public bool SmartHmiLogonPluginConfirmed { get; init; }

    public bool GuestLocalTransitionInterfaceConfirmed { get; init; }

    public bool RemoteUserAccessSpocOnly { get; init; }

    public bool EndpointAuthenticationSeparated { get; init; }

    public bool RemoteExpertModeTransitionAvailable { get; init; }

    public bool AttendedOwnerActionRequired { get; init; }

    public bool NetworkTrafficSent { get; init; }

    public bool VmStarted { get; init; }

    public bool ControllerMutationPerformed { get; init; }

    public bool SensitiveConfigurationRead { get; init; }

    public bool ProtectedLicenseRead { get; init; }

    public NativeKssStatus NativeKssStatus { get; init; } = NativeKssStatus.NotRun;

    public List<string> SideEffects { get; init; } = [];

    public bool EnvironmentReusable { get; init; }

    public string Conclusion { get; init; } = string.Empty;

    public List<string> UnsupportedGaps { get; init; } = [];
}

public sealed record OfficeLiteExpertModeInterfaceInventoryOutcome(
    OfficeLiteExpertModeInterfaceInventoryReceipt Receipt)
{
    public int ExitCode => Receipt.Payload.TerminalClassification switch
    {
        EnvironmentTerminalClassification.Ready => 0,
        EnvironmentTerminalClassification.Failed => 2,
        _ => 3
    };
}

public sealed class OfficeLiteExpertModeInterfaceInventoryCollector
{
    private const string SmartHmiLogonType = "KUKARoboter.LogOn.Service.LogonService";
    private const string LocalAuthenticationType =
        "KUKARoboter.KrcSecurity.AuthenticationManager.IAuthenticationManager";
    private const string RemoteUserAccessType = "KukaRoboter.Contracts.UserAccess.IUserAccessService";
    private const string RemoteImplementationType =
        "KukaRoboter.Services.Implementation.KrcControllerEnvironment.UserAccessLogicKrcController";
    private const string EndpointAuthenticationType =
        "KukaRoboter.Services.Security.BasicAuthenticationValidator";

    private static readonly Regex AttemptIdPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly TimeProvider _timeProvider;

    public OfficeLiteExpertModeInterfaceInventoryCollector(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public OfficeLiteExpertModeInterfaceInventoryOutcome Collect(
        OfficeLiteExpertModeInterfaceInventoryRequest request,
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
        var checks = new List<EnvironmentCheck>();
        var files = new List<EnvironmentFileObservation>();
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["smarthmi-logon-assembly"] = Path.GetFullPath(request.SmartHmiLogonAssemblyPath),
            ["krc-security-assembly"] = Path.GetFullPath(request.KrcSecurityAssemblyPath),
            ["user-access-contracts-assembly"] = Path.GetFullPath(request.UserAccessContractsAssemblyPath),
            ["user-access-service-assembly"] = Path.GetFullPath(request.UserAccessServiceAssemblyPath),
            ["user-access-implementation-assembly"] = Path.GetFullPath(request.UserAccessImplementationAssemblyPath),
            ["workvisual-service-host-configuration"] = Path.GetFullPath(request.WorkVisualServiceHostConfigurationPath)
        };
        var localMembers = new List<string>();
        var remoteMembers = new List<string>();
        var remoteImplementationMembers = new List<string>();
        var remoteTransitionMembers = new List<string>();
        var configuredEndpoints = new List<string>();
        var smartHmiLogonPluginConfirmed = false;
        var guestLocalTransitionInterfaceConfirmed = false;
        var remoteUserAccessSpocOnly = false;
        var endpointAuthenticationSeparated = false;

        if (!OperatingSystem.IsWindows())
        {
            checks.Add(Blocked("host-os", "OfficeLite interface inventory requires Windows PE metadata evidence."));
            return Complete();
        }

        foreach (var pair in paths)
        {
            ObserveFile(pair.Key, pair.Value, files, checks);
        }

        if (checks.Any(check => check.Status is EnvironmentCheckStatus.Blocked or EnvironmentCheckStatus.Failed))
        {
            return Complete();
        }

        try
        {
            var logon = ReadPublicTypeSnapshot(paths["smarthmi-logon-assembly"], SmartHmiLogonType);
            var local = ReadPublicTypeSnapshot(paths["krc-security-assembly"], LocalAuthenticationType);
            var remote = ReadPublicTypeSnapshot(paths["user-access-contracts-assembly"], RemoteUserAccessType);
            var remoteImplementation = ReadPublicTypeSnapshot(
                paths["user-access-implementation-assembly"],
                RemoteImplementationType);
            var endpointAuthentication = ReadPublicTypeSnapshot(
                paths["user-access-service-assembly"],
                EndpointAuthenticationType);

            smartHmiLogonPluginConfirmed = logon.TypeFound;
            localMembers = OfficeLiteExpertModeInterfaceInventoryContract.RequiredLocalTransitionMembers
                .Where(local.PublicMembers.Contains)
                .Order(StringComparer.Ordinal)
                .ToList();
            remoteMembers = OfficeLiteExpertModeInterfaceInventoryContract.RequiredRemoteUserAccessMembers
                .Where(remote.PublicMembers.Contains)
                .Order(StringComparer.Ordinal)
                .ToList();
            remoteImplementationMembers = OfficeLiteExpertModeInterfaceInventoryContract.RequiredRemoteUserAccessMembers
                .Where(remoteImplementation.PublicMembers.Contains)
                .Order(StringComparer.Ordinal)
                .ToList();
            remoteTransitionMembers = OfficeLiteExpertModeInterfaceInventoryContract.RemoteTransitionCandidateMembers
                .Where(member => remote.PublicMembers.Contains(member)
                    || remoteImplementation.PublicMembers.Contains(member))
                .Order(StringComparer.Ordinal)
                .ToList();
            guestLocalTransitionInterfaceConfirmed = local.TypeFound
                && localMembers.Count == OfficeLiteExpertModeInterfaceInventoryContract.RequiredLocalTransitionMembers.Count;
            remoteUserAccessSpocOnly = remote.TypeFound
                && remoteImplementation.TypeFound
                && remoteMembers.Count == OfficeLiteExpertModeInterfaceInventoryContract.RequiredRemoteUserAccessMembers.Count
                && remoteImplementationMembers.Count
                    == OfficeLiteExpertModeInterfaceInventoryContract.RequiredRemoteUserAccessMembers.Count
                && remoteTransitionMembers.Count == 0;
            endpointAuthenticationSeparated = endpointAuthentication.TypeFound
                && endpointAuthentication.PublicMembers.Contains("Validate")
                && !endpointAuthentication.PublicMembers.Overlaps(
                    OfficeLiteExpertModeInterfaceInventoryContract.RemoteTransitionCandidateMembers);

            checks.Add(smartHmiLogonPluginConfirmed
                ? Passed("smarthmi-logon-plugin", "The guest smartHMI LogOn plug-in type is present.")
                : Blocked("smarthmi-logon-plugin", $"Required type is missing: {SmartHmiLogonType}"));
            checks.Add(guestLocalTransitionInterfaceConfirmed
                ? Passed("guest-local-transition", "The guest-local authentication manager exposes the complete transition surface.")
                : Blocked("guest-local-transition", "The guest-local transition surface is incomplete."));
            checks.Add(remoteUserAccessSpocOnly
                ? Passed("remote-user-access", "The hosted UserAccess contract and implementation expose SPOC/remote operation only.")
                : Blocked("remote-user-access", "The hosted UserAccess surface is missing required SPOC members or exposes a transition candidate."));
            checks.Add(endpointAuthenticationSeparated
                ? Passed("endpoint-authentication", "BasicAuthenticationValidator exposes Validate only and is separate from the guest user-group transition.")
                : Blocked("endpoint-authentication", "Endpoint authentication could not be separated from the guest-local transition."));

            var configuration = File.ReadAllText(paths["workvisual-service-host-configuration"]);
            configuredEndpoints = OfficeLiteExpertModeInterfaceInventoryContract.RequiredConfiguredEndpoints
                .Where(endpoint => configuration.Contains($"address=\"{endpoint}\"", StringComparison.Ordinal)
                    && configuration.Contains(
                        "contract=\"KukaRoboter.Contracts.UserAccess.IUserAccessService\"",
                        StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .ToList();
            checks.Add(configuredEndpoints.Count
                    == OfficeLiteExpertModeInterfaceInventoryContract.RequiredConfiguredEndpoints.Count
                ? Passed("configured-user-access-endpoints", "ServiceHost binds the three expected UserAccess endpoints to IUserAccessService.")
                : Blocked("configured-user-access-endpoints", "One or more expected UserAccess endpoints are missing."));
            checks.Add(Passed(
                "metadata-only",
                "Only managed PE metadata and the explicitly supplied ServiceHost configuration were read; vendor assemblies were not loaded or executed."));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or BadImageFormatException
            or InvalidOperationException)
        {
            checks.Add(Failed("metadata-inspection", $"OfficeLite interface metadata could not be read safely: {exception.Message}"));
        }

        return Complete();

        OfficeLiteExpertModeInterfaceInventoryOutcome Complete()
        {
            stopwatch.Stop();
            var terminal = checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
                ? EnvironmentTerminalClassification.Failed
                : checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked)
                    ? EnvironmentTerminalClassification.Blocked
                    : EnvironmentTerminalClassification.Ready;
            var payload = new OfficeLiteExpertModeInterfaceInventoryPayload
            {
                ReceiptId = $"officelite-expert-mode-interface-inventory-{attemptId}",
                AttemptId = attemptId,
                CoreAssemblySha256 = ComputeFileSha256(
                    typeof(OfficeLiteExpertModeInterfaceInventoryCollector).Assembly.Location),
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
                Files = files,
                Checks = checks,
                LocalTransitionMembers = localMembers,
                RemoteUserAccessMembers = remoteMembers,
                RemoteImplementationMembers = remoteImplementationMembers,
                RemoteTransitionMembers = remoteTransitionMembers,
                ConfiguredUserAccessEndpoints = configuredEndpoints,
                SmartHmiLogonPluginConfirmed = smartHmiLogonPluginConfirmed,
                GuestLocalTransitionInterfaceConfirmed = guestLocalTransitionInterfaceConfirmed,
                RemoteUserAccessSpocOnly = remoteUserAccessSpocOnly,
                EndpointAuthenticationSeparated = endpointAuthenticationSeparated,
                RemoteExpertModeTransitionAvailable = false,
                AttendedOwnerActionRequired = terminal == EnvironmentTerminalClassification.Ready,
                NetworkTrafficSent = false,
                VmStarted = false,
                ControllerMutationPerformed = false,
                SensitiveConfigurationRead = false,
                ProtectedLicenseRead = false,
                NativeKssStatus = NativeKssStatus.NotRun,
                EnvironmentReusable = true,
                Conclusion = terminal == EnvironmentTerminalClassification.Ready
                    ? OfficeLiteExpertModeInterfaceInventoryContract.AcceptedConclusion
                    : "The installed interface boundary could not be accepted because required evidence was missing or invalid.",
                UnsupportedGaps = OfficeLiteExpertModeInterfaceInventoryContract.RequiredUnsupportedGaps.ToList()
            };
            var receipt = new OfficeLiteExpertModeInterfaceInventoryReceipt
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
            };
            return new OfficeLiteExpertModeInterfaceInventoryOutcome(receipt);
        }
    }

    private static void ObserveFile(
        string id,
        string path,
        ICollection<EnvironmentFileObservation> files,
        ICollection<EnvironmentCheck> checks)
    {
        if (!OfficeLiteExpertModeInterfaceInventoryContract.ExpectedFileNames.TryGetValue(id, out var expectedName))
        {
            throw new InvalidOperationException($"Unknown OfficeLite interface file ID: {id}");
        }
        if (!string.Equals(Path.GetFileName(path), expectedName, StringComparison.OrdinalIgnoreCase))
        {
            checks.Add(Failed(id, $"Expected exact file name {expectedName}; refusing a broader input."));
            return;
        }

        if (!File.Exists(path))
        {
            files.Add(new EnvironmentFileObservation { Id = id, Path = path, Exists = false });
            checks.Add(Blocked(id, $"Required OfficeLite interface file is missing: {path}"));
            return;
        }

        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                checks.Add(Failed(id, "Interface evidence file cannot be a reparse point."));
                return;
            }

            var info = new FileInfo(path);
            var version = FileVersionInfo.GetVersionInfo(path);
            files.Add(new EnvironmentFileObservation
            {
                Id = id,
                Path = path,
                Exists = true,
                Bytes = info.Length,
                Sha256 = ComputeFileSha256(path),
                FileVersion = string.IsNullOrWhiteSpace(version.FileVersion) ? null : version.FileVersion,
                ProductVersion = string.IsNullOrWhiteSpace(version.ProductVersion) ? null : version.ProductVersion
            });
            checks.Add(Passed(id, "Required OfficeLite interface file exists and was hash-bound."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            checks.Add(Failed(id, $"Required OfficeLite interface file could not be inventoried: {exception.Message}"));
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
                .Where(member => !member.StartsWith("get_", StringComparison.Ordinal)
                    && !member.StartsWith("set_", StringComparison.Ordinal)
                    && !member.StartsWith("add_", StringComparison.Ordinal)
                    && !member.StartsWith("remove_", StringComparison.Ordinal)
                    && !string.Equals(member, ".ctor", StringComparison.Ordinal))
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

public sealed record OfficeLiteExpertModeInterfaceInventoryVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public bool CurrentFilesVerified { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}

public static class OfficeLiteExpertModeInterfaceInventoryReceiptVerifier
{
    private static readonly Regex Sha256Pattern = new(
        "^[A-F0-9]{64}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static OfficeLiteExpertModeInterfaceInventoryVerificationResult Verify(
        OfficeLiteExpertModeInterfaceInventoryReceipt receipt,
        bool verifyCurrentFiles = false)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (!string.Equals(
                receipt.SchemaIdentity,
                OfficeLiteExpertModeInterfaceInventoryContract.ReceiptSchemaIdentity,
                StringComparison.Ordinal))
        {
            errors.Add($"schemaIdentity must be {OfficeLiteExpertModeInterfaceInventoryContract.ReceiptSchemaIdentity}");
        }

        if (receipt.SchemaVersion != OfficeLiteExpertModeInterfaceInventoryContract.ReceiptSchemaVersion)
        {
            errors.Add($"schemaVersion must be {OfficeLiteExpertModeInterfaceInventoryContract.ReceiptSchemaVersion}");
        }

        if (ReferenceEquals(receipt.Payload, null))
        {
            return new OfficeLiteExpertModeInterfaceInventoryVerificationResult
            {
                Succeeded = false,
                PayloadSha256 = receipt.PayloadSha256,
                Errors = ["payload is required"]
            };
        }

        var payload = receipt.Payload;
        if (string.IsNullOrWhiteSpace(payload.ReceiptId) || string.IsNullOrWhiteSpace(payload.AttemptId))
        {
            errors.Add("receiptId and attemptId are required");
        }

        if (!IsSha256(payload.CoreAssemblySha256))
        {
            errors.Add("coreAssemblySha256 is invalid");
        }

        if (!string.Equals(
                receipt.PayloadSha256,
                ReceiptSerialization.ComputeCanonicalSha256(payload),
                StringComparison.Ordinal))
        {
            errors.Add("payloadSha256 does not match the canonical payload");
        }

        if (payload.TerminalClassification != EnvironmentTerminalClassification.Ready)
        {
            errors.Add("terminalClassification must be Ready for accepted interface-boundary evidence");
        }

        var expectedFileIds = OfficeLiteExpertModeInterfaceInventoryContract.RequiredFileIds
            .Order(StringComparer.Ordinal)
            .ToList();
        var actualFileIds = payload.Files.Select(file => file.Id).Order(StringComparer.Ordinal).ToList();
        if (!actualFileIds.SequenceEqual(expectedFileIds, StringComparer.Ordinal))
        {
            errors.Add("files must contain the exact required OfficeLite interface evidence IDs");
        }

        foreach (var file in payload.Files)
        {
            if (!file.Exists || file.Bytes <= 0 || !IsSha256(file.Sha256) || string.IsNullOrWhiteSpace(file.Path))
            {
                errors.Add($"file observation is incomplete: {file.Id}");
                continue;
            }

            if (!OfficeLiteExpertModeInterfaceInventoryContract.ExpectedFileNames.TryGetValue(
                    file.Id,
                    out var expectedFileName)
                || !string.Equals(
                    Path.GetFileName(file.Path),
                    expectedFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"file path does not match the exact file-name whitelist: {file.Id}");
            }
        }

        var actualCheckIds = payload.Checks.Select(check => check.Id).Order(StringComparer.Ordinal).ToList();
        if (!actualCheckIds.SequenceEqual(
                OfficeLiteExpertModeInterfaceInventoryContract.RequiredCheckIds.Order(StringComparer.Ordinal),
                StringComparer.Ordinal)
            || payload.Checks.Any(check => check.Status != EnvironmentCheckStatus.Passed))
        {
            errors.Add("checks must contain the exact required IDs and every check must be Passed");
        }

        RequireExactList(
            payload.LocalTransitionMembers,
            OfficeLiteExpertModeInterfaceInventoryContract.RequiredLocalTransitionMembers,
            "localTransitionMembers");
        RequireExactList(
            payload.RemoteUserAccessMembers,
            OfficeLiteExpertModeInterfaceInventoryContract.RequiredRemoteUserAccessMembers,
            "remoteUserAccessMembers");
        RequireExactList(
            payload.RemoteImplementationMembers,
            OfficeLiteExpertModeInterfaceInventoryContract.RequiredRemoteUserAccessMembers,
            "remoteImplementationMembers");
        RequireExactList(
            payload.ConfiguredUserAccessEndpoints,
            OfficeLiteExpertModeInterfaceInventoryContract.RequiredConfiguredEndpoints,
            "configuredUserAccessEndpoints");
        RequireExactList(
            payload.UnsupportedGaps,
            OfficeLiteExpertModeInterfaceInventoryContract.RequiredUnsupportedGaps,
            "unsupportedGaps");
        if (payload.RemoteTransitionMembers.Count != 0)
        {
            errors.Add("remoteTransitionMembers must be empty");
        }

        if (!payload.SmartHmiLogonPluginConfirmed
            || !payload.GuestLocalTransitionInterfaceConfirmed
            || !payload.RemoteUserAccessSpocOnly
            || !payload.EndpointAuthenticationSeparated
            || payload.RemoteExpertModeTransitionAvailable
            || !payload.AttendedOwnerActionRequired)
        {
            errors.Add("interface-boundary booleans are inconsistent with the accepted installed surface");
        }

        if (payload.NetworkTrafficSent
            || payload.VmStarted
            || payload.ControllerMutationPerformed
            || payload.SensitiveConfigurationRead
            || payload.ProtectedLicenseRead
            || payload.NativeKssStatus != NativeKssStatus.NotRun)
        {
            errors.Add("read-only inventory side-effect and native-KSS claims are invalid");
        }

        if (!payload.EnvironmentReusable)
        {
            errors.Add("environmentReusable must be true");
        }

        if (!string.Equals(
                payload.Conclusion,
                OfficeLiteExpertModeInterfaceInventoryContract.AcceptedConclusion,
                StringComparison.Ordinal))
        {
            errors.Add("conclusion does not match the accepted installed-interface boundary");
        }

        if (payload.DurationMilliseconds < 0 || payload.CompletedAtUtc < payload.StartedAtUtc)
        {
            errors.Add("receipt timing is invalid");
        }

        if (payload.SideEffects.Count > 1
            || payload.SideEffects.Any(effect => !effect.StartsWith("CreateNewReceiptFile:", StringComparison.Ordinal)))
        {
            errors.Add("only a CreateNewReceiptFile side effect may be declared");
        }

        var currentFilesVerified = false;
        if (verifyCurrentFiles && errors.Count == 0)
        {
            foreach (var file in payload.Files)
            {
                if (!File.Exists(file.Path))
                {
                    errors.Add($"current evidence file is missing: {file.Id}");
                    continue;
                }

                var info = new FileInfo(file.Path);
                using var stream = new FileStream(
                    file.Path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                var currentSha256 = Convert.ToHexString(SHA256.HashData(stream));
                if (info.Length != file.Bytes || !string.Equals(currentSha256, file.Sha256, StringComparison.Ordinal))
                {
                    errors.Add($"current evidence file identity changed: {file.Id}");
                }
            }

            currentFilesVerified = errors.Count == 0;
        }

        return new OfficeLiteExpertModeInterfaceInventoryVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            CurrentFilesVerified = currentFilesVerified,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };

        void RequireExactList(
            IReadOnlyList<string> actual,
            IReadOnlyList<string> expected,
            string name)
        {
            if (!actual.Order(StringComparer.Ordinal).SequenceEqual(
                    expected.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal))
            {
                errors.Add($"{name} must match the exact required set");
            }
        }
    }

    private static bool IsSha256(string? value) =>
        !string.IsNullOrEmpty(value) && Sha256Pattern.IsMatch(value);
}

public static class OfficeLiteExpertModeInterfaceInventoryReceiptWriter
{
    public static string WriteNew(
        string outputPath,
        OfficeLiteExpertModeInterfaceInventoryReceipt receipt)
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
