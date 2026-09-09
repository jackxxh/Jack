using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class EnvironmentInventoryContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.environment-inventory-receipt";
    public const int ReceiptSchemaVersion = 1;
}

public enum EnvironmentCheckStatus
{
    Passed,
    Warning,
    Blocked,
    Failed
}

public enum EnvironmentTerminalClassification
{
    Ready,
    Blocked,
    Failed
}

public sealed record EnvironmentInventoryRequest
{
    public required string AssetRoot { get; init; }

    public required string KukaSimLauncherPath { get; init; }

    public required string WorkVisualPath { get; init; }

    public required List<string> VmrunCandidatePaths { get; init; }

    public List<string> ExactRobotComponentCandidatePaths { get; init; } = [];

    public bool RequireSupportedKukaSim410Release { get; init; } = true;

    public bool RequireExactC01RobotComponent { get; init; }

    public static EnvironmentInventoryRequest CreateDefault(
        string assetRoot,
        string? kukaSimLauncherPath = null,
        string? workVisualPath = null,
        string? vmrunPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetRoot);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var kukaSim = KukaSimInstallationDiscovery.ResolveFromLauncher(kukaSimLauncherPath, componentPath: null);
        var vmrunCandidates = vmrunPath is null
            ? new List<string>
            {
                Path.Combine(Path.GetFullPath(assetRoot), "VMware", "vmrun.exe"),
                Path.Combine(programFilesX86, "VMware", "VMware Workstation", "vmrun.exe"),
                Path.Combine(programFiles, "VMware", "VMware Workstation", "vmrun.exe")
            }
            : [vmrunPath];

        return new EnvironmentInventoryRequest
        {
            AssetRoot = assetRoot,
            KukaSimLauncherPath = kukaSim.LauncherPath,
            WorkVisualPath = workVisualPath
                ?? Path.Combine(programFilesX86, "KUKA", "WorkVisual 6.0", "WorkVisual.exe"),
            VmrunCandidatePaths = vmrunCandidates,
            ExactRobotComponentCandidatePaths = [kukaSim.ComponentPath]
        };
    }
}

public sealed record EnvironmentInventoryReceipt
{
    public string SchemaIdentity { get; init; } = EnvironmentInventoryContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = EnvironmentInventoryContract.ReceiptSchemaVersion;

    public required EnvironmentInventoryPayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record EnvironmentInventoryPayload
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

    public string AssetRoot { get; init; } = string.Empty;

    public List<EnvironmentCheck> Checks { get; init; } = [];

    public List<EnvironmentFileObservation> Files { get; init; } = [];

    public List<string> SideEffects { get; init; } = [];

    public bool EnvironmentReusable { get; init; }

    public List<string> BlockingGaps { get; init; } = [];

    public List<string> Warnings { get; init; } = [];
}

public sealed record EnvironmentCheck
{
    public string Id { get; init; } = string.Empty;

    public EnvironmentCheckStatus Status { get; init; }

    public string Detail { get; init; } = string.Empty;
}

public sealed record EnvironmentFileObservation
{
    public string Id { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public bool Exists { get; init; }

    public long? Bytes { get; init; }

    public string? Sha256 { get; init; }

    public string? FileVersion { get; init; }

    public string? ProductVersion { get; init; }
}

public sealed record EnvironmentInventoryOutcome(EnvironmentInventoryReceipt Receipt)
{
    public int ExitCode => Receipt.Payload.TerminalClassification switch
    {
        EnvironmentTerminalClassification.Ready => 0,
        EnvironmentTerminalClassification.Failed => 2,
        _ => 3
    };
}

public sealed partial class EnvironmentInventoryCollector
{
    private const string PrimaryVmxRelativePath =
        "OfficeLite-Work/8.7.8-build04/runs/primary/KR C, V8.7.8OL_Build04.vmx";
    private const string ComponentsRelativePath = "KUKA Sim11 Components/KUKA Sim11 Components";
    private readonly TimeProvider _timeProvider;

    public EnvironmentInventoryCollector(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public EnvironmentInventoryOutcome Collect(EnvironmentInventoryRequest request, string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptId);
        if (!AttemptIdPattern().IsMatch(attemptId))
        {
            throw new ArgumentException(
                "Attempt ID must match [A-Za-z0-9][A-Za-z0-9._-]{2,127}.",
                nameof(attemptId));
        }

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var checks = new List<EnvironmentCheck>();
        var files = new List<EnvironmentFileObservation>();
        var blockingGaps = new List<string>();
        var warnings = new List<string>();
        var assetRoot = Path.GetFullPath(request.AssetRoot);

        if (!OperatingSystem.IsWindows())
        {
            checks.Add(Failed("host-os", "KUKA vendor software inventory requires Windows."));
        }
        else
        {
            checks.Add(Passed("host-os", RuntimeInformation.OSDescription));
        }

        if (!Directory.Exists(assetRoot))
        {
            checks.Add(Failed("asset-root", "The requested external asset root does not exist."));
        }
        else if (HasReparsePoint(assetRoot))
        {
            checks.Add(Failed("asset-root", "The requested external asset root cannot be a reparse point."));
        }
        else
        {
            checks.Add(Passed("asset-root", "External asset root exists and is not a reparse point."));
        }

        ObserveRequiredBinary("kuka-sim", request.KukaSimLauncherPath, checks, files, blockingGaps);
        if (request.RequireSupportedKukaSim410Release && File.Exists(request.KukaSimLauncherPath))
        {
            if (KukaSimInstallationDiscovery.TryClassifyInstalled410Release(request.KukaSimLauncherPath, out var detail))
            {
                checks.Add(Passed("kuka-sim-release", detail));
            }
            else
            {
                checks.Add(Blocked("kuka-sim-release", detail));
                blockingGaps.Add("KUKA.Sim 4.10.1 or newer in the 4.10 line is required for the KUKA.Sim add-on.");
            }
        }
        ObserveRequiredBinary("workvisual", request.WorkVisualPath, checks, files, blockingGaps);

        var vmrunPath = request.VmrunCandidatePaths
            .Select(Path.GetFullPath)
            .FirstOrDefault(File.Exists);
        if (vmrunPath is null)
        {
            checks.Add(Blocked("vmware-vmrun", "No vmrun.exe was found in the declared candidate paths."));
            blockingGaps.Add("Official VMware Workstation runtime is not installed or vmrun.exe is unavailable.");
            foreach (var candidate in request.VmrunCandidatePaths.Select(Path.GetFullPath))
            {
                files.Add(MissingFile("vmware-vmrun-candidate", candidate));
            }
        }
        else
        {
            ObserveRequiredBinary("vmware-vmrun", vmrunPath, checks, files, blockingGaps);
        }

        if (Directory.Exists(assetRoot))
        {
            InspectPrimaryVmx(assetRoot, checks, files, blockingGaps);
            InspectComponentCatalog(
                assetRoot,
                request.ExactRobotComponentCandidatePaths,
                request.RequireExactC01RobotComponent,
                checks,
                files,
                blockingGaps,
                warnings);
        }

        stopwatch.Stop();
        var terminal = checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
            ? EnvironmentTerminalClassification.Failed
            : checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked)
                ? EnvironmentTerminalClassification.Blocked
                : EnvironmentTerminalClassification.Ready;
        var completedAt = _timeProvider.GetUtcNow();
        var payload = new EnvironmentInventoryPayload
        {
            ReceiptId = $"environment-inventory-{attemptId}",
            AttemptId = attemptId,
            CoreAssemblySha256 = ComputeFileSha256(typeof(EnvironmentInventoryCollector).Assembly.Location),
            Runtime = new RuntimeEnvironment
            {
                OsDescription = RuntimeInformation.OSDescription,
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
            },
            StartedAtUtc = startedAt,
            CompletedAtUtc = completedAt,
            DurationMilliseconds = Math.Max(0, stopwatch.ElapsedMilliseconds),
            TerminalClassification = terminal,
            AssetRoot = assetRoot,
            Checks = checks,
            Files = files,
            SideEffects = [],
            EnvironmentReusable = true,
            BlockingGaps = blockingGaps.Distinct(StringComparer.Ordinal).ToList(),
            Warnings = warnings.Distinct(StringComparer.Ordinal).ToList()
        };
        var receipt = new EnvironmentInventoryReceipt
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        return new EnvironmentInventoryOutcome(receipt);
    }

    private static void ObserveRequiredBinary(
        string id,
        string requestedPath,
        List<EnvironmentCheck> checks,
        List<EnvironmentFileObservation> files,
        List<string> blockingGaps)
    {
        var fullPath = Path.GetFullPath(requestedPath);
        if (!File.Exists(fullPath))
        {
            checks.Add(Blocked(id, $"Required executable is missing: {fullPath}"));
            blockingGaps.Add($"Required executable is missing: {id}.");
            files.Add(MissingFile(id, fullPath));
            return;
        }

        try
        {
            var version = FileVersionInfo.GetVersionInfo(fullPath);
            var info = new FileInfo(fullPath);
            files.Add(new EnvironmentFileObservation
            {
                Id = id,
                Path = fullPath,
                Exists = true,
                Bytes = info.Length,
                Sha256 = ComputeFileSha256(fullPath),
                FileVersion = EmptyToNull(version.FileVersion),
                ProductVersion = EmptyToNull(version.ProductVersion)
            });
            checks.Add(Passed(id, "Required executable exists and was hashed."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            checks.Add(Failed(id, $"Required executable could not be inventoried: {exception.Message}"));
        }
    }

    private static void InspectPrimaryVmx(
        string assetRoot,
        List<EnvironmentCheck> checks,
        List<EnvironmentFileObservation> files,
        List<string> blockingGaps)
    {
        var vmxPath = ResolveUnderRoot(assetRoot, PrimaryVmxRelativePath);
        if (!File.Exists(vmxPath))
        {
            checks.Add(Blocked("officelite-primary-vmx", "Hardened OfficeLite primary VMX is missing."));
            blockingGaps.Add("The hardened OfficeLite primary work copy is unavailable.");
            files.Add(MissingFile("officelite-primary-vmx", vmxPath));
            return;
        }

        try
        {
            var content = File.ReadAllText(vmxPath);
            var info = new FileInfo(vmxPath);
            files.Add(new EnvironmentFileObservation
            {
                Id = "officelite-primary-vmx",
                Path = vmxPath,
                Exists = true,
                Bytes = info.Length,
                Sha256 = ComputeFileSha256(vmxPath)
            });
            checks.Add(Passed("officelite-primary-vmx", "Hardened OfficeLite primary VMX exists and was hashed."));

            var hardened = RequiredVmxHardeningLines.All(line =>
                content.Contains(line, StringComparison.OrdinalIgnoreCase));
            if (hardened)
            {
                checks.Add(Passed("officelite-primary-hardening", "HGFS and the inherited full-drive share remain disabled."));
            }
            else
            {
                checks.Add(Failed("officelite-primary-hardening", "Required HGFS/share hardening lines are missing."));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            checks.Add(Failed("officelite-primary-vmx", $"Primary VMX could not be inventoried: {exception.Message}"));
        }
    }

    private static void InspectComponentCatalog(
        string assetRoot,
        IReadOnlyCollection<string> exactComponentCandidatePaths,
        bool requireExactC01,
        List<EnvironmentCheck> checks,
        List<EnvironmentFileObservation> files,
        List<string> blockingGaps,
        List<string> warnings)
    {
        var exactMatches = new List<string>();
        try
        {
            foreach (var declaredPath in exactComponentCandidatePaths
                         .Where(path => !string.IsNullOrWhiteSpace(path))
                         .Select(Path.GetFullPath)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(declaredPath)
                    || !ExactRobotPattern().IsMatch(Path.GetFileNameWithoutExtension(declaredPath))
                    || !SupportedComponentExtension(declaredPath))
                {
                    continue;
                }

                if (HasReparsePoint(declaredPath))
                {
                    checks.Add(Failed("exact-robot-component", "Exact component candidate cannot be a reparse point."));
                    return;
                }

                var info = new FileInfo(declaredPath);
                files.Add(new EnvironmentFileObservation
                {
                    Id = "kuka-sim-exact-robot-component",
                    Path = declaredPath,
                    Exists = true,
                    Bytes = info.Length,
                    Sha256 = ComputeFileSha256(declaredPath)
                });
                exactMatches.Add(declaredPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            checks.Add(Failed("exact-robot-component", $"Exact component candidate could not be inventoried: {exception.Message}"));
            return;
        }

        var componentsRoot = ResolveUnderRoot(assetRoot, ComponentsRelativePath);
        if (!Directory.Exists(componentsRoot))
        {
            CompleteComponentCheck(exactMatches, "Supplied component catalog is missing.", requireExactC01, checks, blockingGaps, warnings);
            return;
        }

        if (HasReparsePoint(componentsRoot))
        {
            checks.Add(Failed("exact-robot-component", "Component catalog cannot be scanned through a reparse point."));
            return;
        }

        try
        {
            foreach (var matchedPath in Directory
                         .EnumerateFiles(componentsRoot, "*", SearchOption.AllDirectories)
                         .Where(SupportedComponentExtension)
                         .Where(path => ExactRobotPattern().IsMatch(Path.GetFileNameWithoutExtension(path))))
            {
                var fullPath = Path.GetFullPath(matchedPath);
                if (exactMatches.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var info = new FileInfo(fullPath);
                files.Add(new EnvironmentFileObservation
                {
                    Id = "kuka-sim-exact-robot-component",
                    Path = fullPath,
                    Exists = true,
                    Bytes = info.Length,
                    Sha256 = ComputeFileSha256(fullPath)
                });
                exactMatches.Add(fullPath);
            }

            CompleteComponentCheck(
                exactMatches,
                "No KR 210 R2700 filename match was found in the supplied component catalog or declared installed-library candidates.",
                requireExactC01,
                checks,
                blockingGaps,
                warnings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            checks.Add(Failed("exact-robot-component", $"Component catalog could not be scanned: {exception.Message}"));
        }
    }

    private static void CompleteComponentCheck(
        IReadOnlyCollection<string> exactMatches,
        string missingDetail,
        bool requireExactC01,
        List<EnvironmentCheck> checks,
        List<string> blockingGaps,
        List<string> warnings)
    {
        var qualifyingMatches = requireExactC01
            ? exactMatches.Where(path => ExactC01RobotPattern().IsMatch(Path.GetFileNameWithoutExtension(path))).ToList()
            : exactMatches.ToList();
        if (qualifyingMatches.Count == 0)
        {
            var detail = requireExactC01
                ? "No exact KR 210 R2700-2 C01 component was found in the installed KUKA.Sim 4.10 library or supplied catalog."
                : missingDetail;
            if (requireExactC01)
            {
                checks.Add(Blocked("exact-robot-component", detail));
                blockingGaps.Add("The exact KR 210 R2700-2 C01 KUKA.Sim component is required.");
            }
            else
            {
                checks.Add(Warning("exact-robot-component", detail));
                warnings.Add("Exact KR 210 R2700-2 KUKA.Sim component provenance remains unresolved.");
            }
            return;
        }

        checks.Add(Passed(
            "exact-robot-component",
            $"Found and hashed {qualifyingMatches.Count} qualifying component candidate(s); in-application loadability and controller compatibility remain unverified."));
        warnings.Add("The exact KR 210 R2700-2 component file is present, but KUKA.Sim loadability and controller compatibility are not yet accepted.");
    }

    private static bool SupportedComponentExtension(string path) =>
        string.Equals(Path.GetExtension(path), ".vcm", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Path.GetExtension(path), ".vcmx", StringComparison.OrdinalIgnoreCase);

    private static string ResolveUnderRoot(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Built-in environment path escaped the asset root.");
        }

        return candidate;
    }

    private static EnvironmentFileObservation MissingFile(string id, string path) =>
        new() { Id = id, Path = path, Exists = false };

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool HasReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static EnvironmentCheck Passed(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Passed, Detail = detail };

    private static EnvironmentCheck Warning(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Warning, Detail = detail };

    private static EnvironmentCheck Blocked(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Blocked, Detail = detail };

    private static EnvironmentCheck Failed(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Failed, Detail = detail };

    private static readonly string[] RequiredVmxHardeningLines =
    [
        "isolation.tools.hgfs.disable = \"TRUE\"",
        "sharedFolder0.present = \"FALSE\"",
        "sharedFolder0.enabled = \"FALSE\"",
        "sharedFolder0.readAccess = \"FALSE\"",
        "sharedFolder0.writeAccess = \"FALSE\"",
        "sharedFolder.maxNum = \"0\"",
        "hgfs.mapRootShare = \"FALSE\""
    ];

    [GeneratedRegex("KR[ _-]*210(?!0|P).*R[ _-]*2700", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExactRobotPattern();

    [GeneratedRegex("KR[ _-]*210(?!0|P).*R[ _-]*2700.*C[ _-]*01", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExactC01RobotPattern();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex AttemptIdPattern();
}

public sealed record EnvironmentReceiptVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}

public static class EnvironmentReceiptVerifier
{
    public static EnvironmentReceiptVerificationResult Verify(EnvironmentInventoryReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (!string.Equals(
                receipt.SchemaIdentity,
                EnvironmentInventoryContract.ReceiptSchemaIdentity,
                StringComparison.Ordinal))
        {
            errors.Add($"schemaIdentity must be {EnvironmentInventoryContract.ReceiptSchemaIdentity}");
        }

        if (receipt.SchemaVersion != EnvironmentInventoryContract.ReceiptSchemaVersion)
        {
            errors.Add($"schemaVersion must be {EnvironmentInventoryContract.ReceiptSchemaVersion}");
        }

        if (string.IsNullOrWhiteSpace(receipt.Payload.ReceiptId)
            || string.IsNullOrWhiteSpace(receipt.Payload.AttemptId))
        {
            errors.Add("receiptId and attemptId are required");
        }

        if (receipt.Payload.Checks is null || receipt.Payload.Checks.Count == 0)
        {
            errors.Add("payload.checks must contain evidence");
        }
        else if (receipt.Payload.Checks.Any(check => string.IsNullOrWhiteSpace(check.Id))
            || receipt.Payload.Checks
                .GroupBy(check => check.Id, StringComparer.Ordinal)
                .Any(group => group.Count() > 1))
        {
            errors.Add("payload.checks must have non-empty unique IDs");
        }

        if (receipt.Payload.Files is null
            || receipt.Payload.BlockingGaps is null
            || receipt.Payload.Warnings is null
            || receipt.Payload.SideEffects is null)
        {
            errors.Add("payload evidence collections cannot be null");
        }
        else if (receipt.Payload.Files.Any(file =>
                string.IsNullOrWhiteSpace(file.Id) || string.IsNullOrWhiteSpace(file.Path))
            || receipt.Payload.Files
                .GroupBy(file => $"{file.Id}|{file.Path}", StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1))
        {
            errors.Add("payload.files must have non-empty unique ID/path pairs");
        }

        if (receipt.Payload.Runtime is null
            || string.IsNullOrWhiteSpace(receipt.Payload.Runtime.OsDescription)
            || string.IsNullOrWhiteSpace(receipt.Payload.Runtime.FrameworkDescription)
            || string.IsNullOrWhiteSpace(receipt.Payload.Runtime.ProcessArchitecture))
        {
            errors.Add("payload.runtime identity is incomplete");
        }

        if (receipt.Payload.StartedAtUtc > receipt.Payload.CompletedAtUtc
            || receipt.Payload.DurationMilliseconds < 0)
        {
            errors.Add("payload timing is invalid");
        }

        if (receipt.Payload.CoreAssemblySha256.Length != 64
            || receipt.Payload.CoreAssemblySha256.Any(character => !Uri.IsHexDigit(character)))
        {
            errors.Add("payload.coreAssemblySha256 must be a SHA-256 value");
        }

        if (receipt.Payload.Checks is not null)
        {
            var expectedTerminal = receipt.Payload.Checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
                ? EnvironmentTerminalClassification.Failed
                : receipt.Payload.Checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked)
                    ? EnvironmentTerminalClassification.Blocked
                    : EnvironmentTerminalClassification.Ready;
            if (receipt.Payload.TerminalClassification != expectedTerminal)
            {
                errors.Add("payload.terminalClassification does not agree with check statuses");
            }
        }

        if (!string.Equals(
                receipt.PayloadSha256,
                ReceiptSerialization.ComputeCanonicalSha256(receipt.Payload),
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("payloadSha256 does not match the canonical payload");
        }

        return new EnvironmentReceiptVerificationResult
        {
            ReceiptId = receipt.Payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }
}
