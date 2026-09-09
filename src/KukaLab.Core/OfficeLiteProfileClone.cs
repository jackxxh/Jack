using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace KukaLab.Core;

public static class OfficeLiteProfileCloneContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.officelite-profile-clone-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const string DefaultSnapshotName = "KLAB-ITEM9-CLEAN";

    public static readonly IReadOnlyList<string> RequiredLimitations =
    [
        "This operation creates a recoverable isolated OfficeLite clone but does not start it.",
        "This operation does not deploy or activate a WorkVisual project and does not change controller, KSS, safety, program or motion state.",
        "The clone is not a matching KR 210 profile until a separate WorkVisual/OfficeLite acceptance proves its active project identity."
    ];
}

public sealed record OfficeLiteProfileCloneRequest
{
    private const string PrimaryVmxRelativePath =
        "OfficeLite-Work/8.7.8-build04/runs/primary/KR C, V8.7.8OL_Build04.vmx";

    public required string AssetRoot { get; init; }
    public required string VmrunPath { get; init; }
    public required string SourceVmxPath { get; init; }
    public required string TargetVmxPath { get; init; }
    public required string CloneName { get; init; }
    public string SnapshotName { get; init; } = OfficeLiteProfileCloneContract.DefaultSnapshotName;
    public required string AuthorizationReference { get; init; }
    public int CloneTimeoutSeconds { get; init; } = 900;
    public int VmrunTimeoutSeconds { get; init; } = 60;

    public static OfficeLiteProfileCloneRequest CreateDefault(
        string assetRoot,
        string targetVmxPath,
        string cloneName,
        string authorizationReference,
        string? vmrunPath = null,
        string? sourceVmxPath = null,
        string? snapshotName = null,
        int cloneTimeoutSeconds = 900)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetVmxPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(cloneName);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizationReference);

        var fullAssetRoot = Path.GetFullPath(assetRoot);
        return new OfficeLiteProfileCloneRequest
        {
            AssetRoot = fullAssetRoot,
            VmrunPath = Path.GetFullPath(vmrunPath ?? Path.Combine(fullAssetRoot, "VMware", "vmrun.exe")),
            SourceVmxPath = Path.GetFullPath(sourceVmxPath ?? Path.Combine(
                fullAssetRoot,
                PrimaryVmxRelativePath.Replace('/', Path.DirectorySeparatorChar))),
            TargetVmxPath = Path.GetFullPath(targetVmxPath),
            CloneName = cloneName.Trim(),
            SnapshotName = string.IsNullOrWhiteSpace(snapshotName)
                ? OfficeLiteProfileCloneContract.DefaultSnapshotName
                : snapshotName.Trim(),
            AuthorizationReference = authorizationReference.Trim(),
            CloneTimeoutSeconds = cloneTimeoutSeconds
        };
    }
}

public sealed record OfficeLiteProfileFileStamp
{
    public string RelativePath { get; init; } = string.Empty;
    public long Bytes { get; init; }
    public DateTimeOffset LastWriteTimeUtc { get; init; }
    public string Sha256 { get; init; } = string.Empty;
}

public sealed record OfficeLiteProfileClonePayload
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
    public string VmrunPath { get; init; } = string.Empty;
    public string SourceVmxPath { get; init; } = string.Empty;
    public string TargetVmxPath { get; init; } = string.Empty;
    public string CloneName { get; init; } = string.Empty;
    public string SnapshotName { get; init; } = string.Empty;
    public string AuthorizationReference { get; init; } = string.Empty;
    public IReadOnlyList<OfficeLiteProfileFileStamp> SourceBefore { get; init; } = [];
    public IReadOnlyList<OfficeLiteProfileFileStamp> SourceAfter { get; init; } = [];
    public IReadOnlyList<OfficeLiteProfileFileStamp> TargetAfter { get; init; } = [];
    public IReadOnlyList<VmrunCommandObservation> Commands { get; init; } = [];
    public IReadOnlyList<EnvironmentCheck> Checks { get; init; } = [];
    public IReadOnlyList<string> SideEffects { get; init; } = [];
    public IReadOnlyList<string> UnsupportedGaps { get; init; } = [];
    public bool SourceUnchanged { get; init; }
    public bool TargetCreated { get; init; }
    public bool SnapshotCreated { get; init; }
    public bool TargetHardeningVerified { get; init; }
    public bool EnvironmentReusable { get; init; }
}

public sealed record OfficeLiteProfileCloneReceipt
{
    public string SchemaIdentity { get; init; } = OfficeLiteProfileCloneContract.ReceiptSchemaIdentity;
    public int SchemaVersion { get; init; } = OfficeLiteProfileCloneContract.ReceiptSchemaVersion;
    public required OfficeLiteProfileClonePayload Payload { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record OfficeLiteProfileCloneOutcome(OfficeLiteProfileCloneReceipt Receipt)
{
    public int ExitCode => Receipt.Payload.TerminalClassification switch
    {
        EnvironmentTerminalClassification.Ready => 0,
        EnvironmentTerminalClassification.Failed => 2,
        _ => 3
    };
}

public sealed class OfficeLiteProfileCloneRunner
{
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

    private readonly IOfficeLiteHostPlatform _platform;
    private readonly TimeProvider _timeProvider;

    public OfficeLiteProfileCloneRunner()
        : this(new OfficeLiteHostPlatform(), TimeProvider.System)
    {
    }

    internal OfficeLiteProfileCloneRunner(IOfficeLiteHostPlatform platform, TimeProvider timeProvider)
    {
        _platform = platform;
        _timeProvider = timeProvider;
    }

    public OfficeLiteProfileCloneOutcome Run(OfficeLiteProfileCloneRequest request, string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateToken(attemptId, nameof(attemptId));
        ValidateToken(request.CloneName, nameof(request.CloneName));
        ValidateToken(request.SnapshotName, nameof(request.SnapshotName));
        if (string.IsNullOrWhiteSpace(request.AuthorizationReference))
        {
            throw new ArgumentException("Authorization reference is required.", nameof(request));
        }
        if (request.CloneTimeoutSeconds is < 30 or > 3600 || request.VmrunTimeoutSeconds is < 5 or > 300)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Clone/vmrun timeouts are outside their bounded ranges.");
        }

        var startedAt = _timeProvider.GetUtcNow();
        var timer = Stopwatch.StartNew();
        var checks = new List<EnvironmentCheck>();
        var commands = new List<VmrunCommandObservation>();
        var sideEffects = new List<string>();
        var sourceBefore = new List<OfficeLiteProfileFileStamp>();
        var sourceAfter = new List<OfficeLiteProfileFileStamp>();
        var targetAfter = new List<OfficeLiteProfileFileStamp>();
        var terminal = EnvironmentTerminalClassification.Blocked;
        var sourceUnchanged = false;
        var targetCreated = false;
        var snapshotCreated = false;
        var hardeningVerified = false;

        var assetRoot = Path.GetFullPath(request.AssetRoot);
        var vmrunPath = Path.GetFullPath(request.VmrunPath);
        var sourceVmx = Path.GetFullPath(request.SourceVmxPath);
        var targetVmx = Path.GetFullPath(request.TargetVmxPath);
        var sourceRoot = Path.GetDirectoryName(sourceVmx)!;
        var targetRoot = Path.GetDirectoryName(targetVmx)!;

        if (!OperatingSystem.IsWindows())
        {
            checks.Add(Failed("host-os", "OfficeLite cloning requires Windows and VMware Workstation."));
            return Complete();
        }
        if (!Directory.Exists(assetRoot) || IsReparsePoint(assetRoot))
        {
            checks.Add(Failed("asset-root", "The asset root is missing or is a reparse point."));
            terminal = EnvironmentTerminalClassification.Failed;
            return Complete();
        }
        if (!IsUnderRoot(assetRoot, sourceVmx) || !IsUnderRoot(assetRoot, targetVmx))
        {
            checks.Add(Failed("path-boundary", "Source and target VMX files must remain under the declared asset root."));
            terminal = EnvironmentTerminalClassification.Failed;
            return Complete();
        }
        if (string.Equals(sourceRoot, targetRoot, StringComparison.OrdinalIgnoreCase)
            || IsUnderRoot(sourceRoot, targetVmx))
        {
            checks.Add(Failed("target-isolation", "The target must be a disjoint directory outside the source VM tree."));
            terminal = EnvironmentTerminalClassification.Failed;
            return Complete();
        }
        if (!File.Exists(vmrunPath) || !File.Exists(sourceVmx))
        {
            checks.Add(Blocked("source-prerequisites", "vmrun or the source primary VMX is missing."));
            return Complete();
        }
        if (Directory.Exists(targetRoot) || File.Exists(targetVmx))
        {
            checks.Add(Blocked("create-new-target", "The target directory already exists; no clone command was invoked."));
            return Complete();
        }
        if (Directory.EnumerateFileSystemEntries(sourceRoot, "*.lck", SearchOption.AllDirectories).Any())
        {
            checks.Add(Blocked("source-locks", "The source VM tree contains VMware lock files."));
            return Complete();
        }
        if (!HasRequiredHardening(sourceVmx))
        {
            checks.Add(Failed("source-hardening", "The source VMX does not retain required HGFS/share hardening."));
            terminal = EnvironmentTerminalClassification.Failed;
            return Complete();
        }

        checks.Add(Passed("source-prerequisites", "The stopped hardened primary and signed-install vmrun path are present under the authorized asset root."));
        sourceBefore.AddRange(FingerprintDirectory(sourceRoot));

        var listBefore = RunVmrun("list-before", vmrunPath, ["-T", "ws", "list"], request.VmrunTimeoutSeconds, commands);
        if (!Succeeded(listBefore) || !ContainsZeroRunningVms(listBefore.StandardOutput))
        {
            checks.Add(Blocked("exclusive-vm-ownership", "vmrun did not prove zero running VMs before cloning."));
            return Complete();
        }
        checks.Add(Passed("exclusive-vm-ownership", "vmrun proved zero running VMs before cloning."));

        var clone = RunVmrun(
            "full-clone",
            vmrunPath,
            ["-T", "ws", "clone", sourceVmx, targetVmx, "full", $"-cloneName={request.CloneName}"],
            request.CloneTimeoutSeconds,
            commands);
        if (!Succeeded(clone))
        {
            checks.Add(Failed("full-clone", "VMware full clone failed; any exact partial target must be inspected before retry."));
            terminal = EnvironmentTerminalClassification.Failed;
            return Complete();
        }
        sideEffects.Add($"CreateIsolatedFullClone:{targetRoot}");
        targetCreated = File.Exists(targetVmx);
        if (!targetCreated)
        {
            checks.Add(Failed("full-clone", "VMware reported success but the target VMX is absent."));
            terminal = EnvironmentTerminalClassification.Failed;
            return Complete();
        }
        checks.Add(Passed("full-clone", "VMware created a disjoint full OfficeLite clone."));

        hardeningVerified = HasRequiredHardening(targetVmx);
        if (!hardeningVerified)
        {
            checks.Add(Failed("target-hardening", "The cloned VMX lost required HGFS/share hardening."));
            terminal = EnvironmentTerminalClassification.Failed;
            return Complete();
        }
        checks.Add(Passed("target-hardening", "The cloned VMX retains required HGFS/share hardening."));

        var snapshot = RunVmrun(
            "clean-snapshot",
            vmrunPath,
            ["-T", "ws", "snapshot", targetVmx, request.SnapshotName],
            request.VmrunTimeoutSeconds,
            commands);
        if (!Succeeded(snapshot))
        {
            checks.Add(Failed("clean-snapshot", "The clean rollback snapshot could not be created."));
            terminal = EnvironmentTerminalClassification.Failed;
            return Complete();
        }
        sideEffects.Add($"CreateRollbackSnapshot:{request.SnapshotName}");

        var snapshots = RunVmrun(
            "list-snapshots",
            vmrunPath,
            ["-T", "ws", "listSnapshots", targetVmx],
            request.VmrunTimeoutSeconds,
            commands);
        snapshotCreated = Succeeded(snapshots)
            && snapshots.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Any(line => string.Equals(line.Trim(), request.SnapshotName, StringComparison.Ordinal));
        if (!snapshotCreated)
        {
            checks.Add(Failed("clean-snapshot", "The clean rollback snapshot was not listed after creation."));
            terminal = EnvironmentTerminalClassification.Failed;
            return Complete();
        }
        checks.Add(Passed("clean-snapshot", "The isolated clone has a listed clean rollback snapshot."));

        sourceAfter.AddRange(FingerprintDirectory(sourceRoot));
        targetAfter.AddRange(FingerprintDirectory(targetRoot));
        sourceUnchanged = StampsEqual(sourceBefore, sourceAfter);
        if (!sourceUnchanged)
        {
            checks.Add(Failed("source-immutability", "Source VM metadata/content fingerprint changed during cloning."));
            terminal = EnvironmentTerminalClassification.Failed;
            return Complete();
        }
        checks.Add(Passed("source-immutability", "Source VM metadata/content fingerprint is unchanged."));

        var listAfter = RunVmrun("list-after", vmrunPath, ["-T", "ws", "list"], request.VmrunTimeoutSeconds, commands);
        if (!Succeeded(listAfter) || !ContainsZeroRunningVms(listAfter.StandardOutput))
        {
            checks.Add(Failed("zero-running-vms", "vmrun did not prove zero running VMs after cloning."));
            terminal = EnvironmentTerminalClassification.Failed;
            return Complete();
        }
        checks.Add(Passed("zero-running-vms", "No VM was started by the clone/snapshot operation."));
        terminal = EnvironmentTerminalClassification.Ready;
        return Complete();

        OfficeLiteProfileCloneOutcome Complete()
        {
            timer.Stop();
            var completedAt = _timeProvider.GetUtcNow();
            var payload = new OfficeLiteProfileClonePayload
            {
                ReceiptId = $"officelite-profile-clone-{attemptId}",
                AttemptId = attemptId,
                CoreAssemblySha256 = HashFile(typeof(OfficeLiteProfileCloneRunner).Assembly.Location),
                Runtime = new RuntimeEnvironment
                {
                    OsDescription = RuntimeInformation.OSDescription,
                    FrameworkDescription = RuntimeInformation.FrameworkDescription,
                    ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
                },
                StartedAtUtc = startedAt,
                CompletedAtUtc = completedAt,
                DurationMilliseconds = timer.ElapsedMilliseconds,
                TerminalClassification = terminal,
                AssetRoot = assetRoot,
                VmrunPath = vmrunPath,
                SourceVmxPath = sourceVmx,
                TargetVmxPath = targetVmx,
                CloneName = request.CloneName,
                SnapshotName = request.SnapshotName,
                AuthorizationReference = request.AuthorizationReference,
                SourceBefore = sourceBefore,
                SourceAfter = sourceAfter,
                TargetAfter = targetAfter,
                Commands = commands,
                Checks = checks,
                SideEffects = sideEffects,
                UnsupportedGaps = OfficeLiteProfileCloneContract.RequiredLimitations,
                SourceUnchanged = sourceUnchanged,
                TargetCreated = targetCreated,
                SnapshotCreated = snapshotCreated,
                TargetHardeningVerified = hardeningVerified,
                EnvironmentReusable = terminal == EnvironmentTerminalClassification.Ready
            };
            return new OfficeLiteProfileCloneOutcome(new OfficeLiteProfileCloneReceipt
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
            });
        }
    }

    private VmrunCommandObservation RunVmrun(
        string name,
        string vmrunPath,
        IReadOnlyList<string> arguments,
        int timeoutSeconds,
        List<VmrunCommandObservation> commands)
    {
        VmrunExecutionResult result;
        try
        {
            result = _platform.RunVmrun(vmrunPath, arguments, TimeSpan.FromSeconds(timeoutSeconds));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            result = new VmrunExecutionResult(-1, false, 0, string.Empty, $"{exception.GetType().Name}: {exception.Message}");
        }
        var observation = new VmrunCommandObservation
        {
            Name = name,
            ExitCode = result.ExitCode,
            TimedOut = result.TimedOut,
            DurationMilliseconds = result.DurationMilliseconds,
            StandardOutput = result.StandardOutput.Trim(),
            StandardError = result.StandardError.Trim()
        };
        commands.Add(observation);
        return observation;
    }

    internal static IReadOnlyList<OfficeLiteProfileFileStamp> FingerprintDirectory(string root)
        => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .OrderBy(path => Path.GetRelativePath(root, path), StringComparer.Ordinal)
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new OfficeLiteProfileFileStamp
                {
                    RelativePath = Path.GetRelativePath(root, path).Replace('\\', '/'),
                    Bytes = info.Length,
                    LastWriteTimeUtc = info.LastWriteTimeUtc,
                    Sha256 = info.Length <= 16 * 1024 * 1024 ? HashFile(path) : string.Empty
                };
            })
            .ToList();

    internal static bool HasRequiredHardening(string vmxPath)
    {
        var content = File.ReadAllText(vmxPath);
        return RequiredVmxHardeningLines.All(line => content.Contains(line, StringComparison.OrdinalIgnoreCase));
    }

    private static bool StampsEqual(
        IReadOnlyList<OfficeLiteProfileFileStamp> left,
        IReadOnlyList<OfficeLiteProfileFileStamp> right)
        => string.Equals(
            ReceiptSerialization.ComputeCanonicalSha256(left),
            ReceiptSerialization.ComputeCanonicalSha256(right),
            StringComparison.Ordinal);

    private static bool ContainsZeroRunningVms(string output)
        => output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Any(line => string.Equals(line.Trim(), "Total running VMs: 0", StringComparison.OrdinalIgnoreCase));

    private static bool Succeeded(VmrunCommandObservation command)
        => command.ExitCode == 0 && !command.TimedOut;

    private static bool IsUnderRoot(string root, string candidate)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(candidate));
        return !Path.IsPathRooted(relative)
            && !string.Equals(relative, "..", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static bool IsReparsePoint(string path)
        => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static string HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void ValidateToken(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 128
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Value must be a bounded filename-safe token.", parameter);
        }
    }

    private static EnvironmentCheck Passed(string id, string detail) => new() { Id = id, Status = EnvironmentCheckStatus.Passed, Detail = detail };
    private static EnvironmentCheck Blocked(string id, string detail) => new() { Id = id, Status = EnvironmentCheckStatus.Blocked, Detail = detail };
    private static EnvironmentCheck Failed(string id, string detail) => new() { Id = id, Status = EnvironmentCheckStatus.Failed, Detail = detail };
}

public sealed record OfficeLiteProfileCloneVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;
    public bool Succeeded { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
    public IReadOnlyList<string> Errors { get; init; } = [];
}

public static class OfficeLiteProfileCloneReceiptVerifier
{
    public static OfficeLiteProfileCloneVerificationResult Verify(OfficeLiteProfileCloneReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (!string.Equals(receipt.SchemaIdentity, OfficeLiteProfileCloneContract.ReceiptSchemaIdentity, StringComparison.Ordinal))
        {
            errors.Add("schema identity mismatch");
        }
        if (receipt.SchemaVersion != OfficeLiteProfileCloneContract.ReceiptSchemaVersion)
        {
            errors.Add("schema version mismatch");
        }
        var canonical = ReceiptSerialization.ComputeCanonicalSha256(receipt.Payload);
        if (!string.Equals(canonical, receipt.PayloadSha256, StringComparison.Ordinal))
        {
            errors.Add("payload hash mismatch");
        }
        if (receipt.Payload.TerminalClassification == EnvironmentTerminalClassification.Ready)
        {
            if (!receipt.Payload.SourceUnchanged
                || !receipt.Payload.TargetCreated
                || !receipt.Payload.SnapshotCreated
                || !receipt.Payload.TargetHardeningVerified
                || !receipt.Payload.EnvironmentReusable)
            {
                errors.Add("ready receipt lacks clone, snapshot, hardening, source-immutability or reusable-environment evidence");
            }
            if (!File.Exists(receipt.Payload.SourceVmxPath) || !File.Exists(receipt.Payload.TargetVmxPath))
            {
                errors.Add("source or target VMX is missing");
            }
            else
            {
                var sourceRoot = Path.GetDirectoryName(receipt.Payload.SourceVmxPath)!;
                var currentSource = OfficeLiteProfileCloneRunner.FingerprintDirectory(sourceRoot);
                if (!string.Equals(
                        ReceiptSerialization.ComputeCanonicalSha256(currentSource),
                        ReceiptSerialization.ComputeCanonicalSha256(receipt.Payload.SourceAfter),
                        StringComparison.Ordinal))
                {
                    errors.Add("current source VM fingerprint drifted");
                }
                if (!OfficeLiteProfileCloneRunner.HasRequiredHardening(receipt.Payload.TargetVmxPath))
                {
                    errors.Add("current target VMX hardening is missing");
                }
            }
            if (!OfficeLiteProfileCloneContract.RequiredLimitations.SequenceEqual(receipt.Payload.UnsupportedGaps, StringComparer.Ordinal))
            {
                errors.Add("required limitations changed");
            }
        }
        return new OfficeLiteProfileCloneVerificationResult
        {
            ReceiptId = receipt.Payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = canonical,
            Errors = errors
        };
    }
}

public static class OfficeLiteProfileCloneReceiptWriter
{
    public static string WriteNew(string outputPath, OfficeLiteProfileCloneReceipt receipt)
    {
        var fullPath = Path.GetFullPath(outputPath);
        if (File.Exists(fullPath))
        {
            throw new IOException($"Receipt already exists: {fullPath}");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
        writer.Write(ReceiptSerialization.ToJson(receipt));
        return fullPath;
    }
}
