using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class WorkVisualProjectIntakeContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.workvisual-project-intake-receipt";
    public const int ReceiptSchemaVersion = 1;

    public static readonly IReadOnlyList<string> RequiredUnsupportedClaims =
    [
        "A WVS extension and byte identity do not prove that the file is a valid or complete WorkVisual project.",
        "This intake does not extract or inspect project contents, controller identity, robot machine data, technology packages, Tool/Base/Load, programs or safety configuration.",
        "This intake does not contact a controller, authenticate, deploy, activate, merge, compile KRL, run KSS, connect KUKA.Sim or qualify physical motion."
    ];
}

public enum WorkVisualProjectIntakeDisposition
{
    FileIdentityCaptured
}

public sealed record WorkVisualProjectIntakeRequest
{
    public required string ProjectPath { get; init; }

    public required string ReceiptOutputPath { get; init; }

    public required string CaptureReference { get; init; }
}

public sealed record WorkVisualProjectFileIdentity
{
    public string SourcePathSha256 { get; init; } = string.Empty;

    public string FileNameSha256 { get; init; } = string.Empty;

    public string Extension { get; init; } = string.Empty;

    public long Bytes { get; init; }

    public DateTimeOffset LastWriteTimeUtc { get; init; }

    public string Sha256 { get; init; } = string.Empty;
}

public sealed record WorkVisualProjectIntakeReceipt
{
    public string SchemaIdentity { get; init; } = WorkVisualProjectIntakeContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = WorkVisualProjectIntakeContract.ReceiptSchemaVersion;

    public required WorkVisualProjectIntakePayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record WorkVisualProjectIntakePayload
{
    public string ReceiptId { get; init; } = string.Empty;

    public string AttemptId { get; init; } = string.Empty;

    public string CaptureReference { get; init; } = string.Empty;

    public string CoreVersion { get; init; } = FixtureContract.CoreVersion;

    public string CoreAssemblySha256 { get; init; } = string.Empty;

    public RuntimeEnvironment Runtime { get; init; } = new();

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset CompletedAtUtc { get; init; }

    public long DurationMilliseconds { get; init; }

    public EnvironmentTerminalClassification TerminalClassification { get; init; }

    public WorkVisualProjectIntakeDisposition Disposition { get; init; }

    public WorkVisualProjectFileIdentity ProjectFile { get; init; } = new();

    public bool FileIdentityCaptured { get; init; }

    public bool ProjectExtracted { get; init; }

    public bool ProjectContentsInspected { get; init; }

    public bool ControllerAccessed { get; init; }

    public bool NetworkTrafficSent { get; init; }

    public bool CredentialsUsed { get; init; }

    public bool SourceProjectChanged { get; init; }

    public bool ControllerConfigurationChanged { get; init; }

    public NativeKssStatus NativeKssStatus { get; init; } = NativeKssStatus.NotRun;

    public List<EnvironmentCheck> Checks { get; init; } = [];

    public List<string> SideEffects { get; init; } = [];

    public bool EnvironmentReusable { get; init; } = true;

    public List<string> UnsupportedClaims { get; init; } = [];
}

public sealed record WorkVisualProjectIntakeOutcome(WorkVisualProjectIntakeReceipt Receipt)
{
    public int ExitCode => 0;
}

public sealed partial class WorkVisualProjectIntakeRunner
{
    internal static readonly IReadOnlyList<string> RequiredCheckIds =
    [
        "source-file",
        "wvs-extension",
        "reparse-boundary",
        "source-output-separation",
        "file-identity",
        "no-extraction",
        "no-network",
        "no-mutation"
    ];

    private readonly TimeProvider _timeProvider;

    public WorkVisualProjectIntakeRunner(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public WorkVisualProjectIntakeOutcome Run(
        WorkVisualProjectIntakeRequest request,
        string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateOpaqueId(attemptId, nameof(attemptId));
        ValidateOpaqueId(request.CaptureReference, nameof(request.CaptureReference));

        var projectPath = Path.GetFullPath(request.ProjectPath);
        var outputPath = Path.GetFullPath(request.ReceiptOutputPath);
        ValidateProjectPath(projectPath);
        ValidateOutputBoundary(projectPath, outputPath);

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var projectFile = CaptureIdentity(projectPath);
        stopwatch.Stop();

        var checks = RequiredCheckIds.Select(id => id switch
        {
            "source-file" => Passed(id, "The local source project exists and was opened for read-only hashing."),
            "wvs-extension" => Passed(id, "The source file uses the vendor-documented WVS extension."),
            "reparse-boundary" => Passed(id, "The source file and its ancestor path contain no reparse point."),
            "source-output-separation" => Passed(id, "The create-new receipt is outside the source project's directory."),
            "file-identity" => Passed(id, $"Captured {projectFile.Bytes} byte(s) and a SHA-256 identity without retaining the source name or path."),
            "no-extraction" => Passed(id, "ProjectExtractor and project-content parsing were not invoked."),
            "no-network" => Passed(id, "The operation used only local file metadata and bytes."),
            "no-mutation" => Passed(id, "The source file length and last-write time remained stable while writes were denied."),
            _ => throw new InvalidOperationException($"Unsupported project-intake check ID: {id}")
        }).ToList();

        var payload = new WorkVisualProjectIntakePayload
        {
            ReceiptId = $"workvisual-project-intake-{attemptId}",
            AttemptId = attemptId,
            CaptureReference = request.CaptureReference,
            CoreAssemblySha256 = ComputeFileSha256(typeof(WorkVisualProjectIntakeRunner).Assembly.Location),
            Runtime = new RuntimeEnvironment
            {
                OsDescription = RuntimeInformation.OSDescription,
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
            },
            StartedAtUtc = startedAt,
            CompletedAtUtc = _timeProvider.GetUtcNow(),
            DurationMilliseconds = Math.Max(0, stopwatch.ElapsedMilliseconds),
            TerminalClassification = EnvironmentTerminalClassification.Ready,
            Disposition = WorkVisualProjectIntakeDisposition.FileIdentityCaptured,
            ProjectFile = projectFile,
            FileIdentityCaptured = true,
            ProjectExtracted = false,
            ProjectContentsInspected = false,
            ControllerAccessed = false,
            NetworkTrafficSent = false,
            CredentialsUsed = false,
            SourceProjectChanged = false,
            ControllerConfigurationChanged = false,
            NativeKssStatus = NativeKssStatus.NotRun,
            Checks = checks,
            SideEffects = [$"CreateNewReceiptFile:{outputPath}"],
            EnvironmentReusable = true,
            UnsupportedClaims = WorkVisualProjectIntakeContract.RequiredUnsupportedClaims.ToList()
        };
        var receipt = new WorkVisualProjectIntakeReceipt
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        return new WorkVisualProjectIntakeOutcome(receipt);
    }

    internal static WorkVisualProjectFileIdentity CaptureIdentity(string projectPath)
    {
        var fullPath = Path.GetFullPath(projectPath);
        ValidateProjectPath(fullPath);
        var before = new FileInfo(fullPath);
        before.Refresh();
        var beforeLength = before.Length;
        var beforeWrite = before.LastWriteTimeUtc;

        string sha256;
        long streamLength;
        using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            sha256 = Convert.ToHexString(SHA256.HashData(stream));
            streamLength = stream.Length;
            before.Refresh();
            if (before.Length != beforeLength || before.LastWriteTimeUtc != beforeWrite)
            {
                throw new IOException("The WVS source changed while its byte identity was being captured.");
            }
        }

        if (streamLength <= 0)
        {
            throw new InvalidDataException("The WVS source cannot be empty.");
        }

        return new WorkVisualProjectFileIdentity
        {
            SourcePathSha256 = ComputeStringSha256(NormalizeLocator(fullPath)),
            FileNameSha256 = ComputeStringSha256(Path.GetFileName(fullPath).ToUpperInvariant()),
            Extension = ".wvs",
            Bytes = streamLength,
            LastWriteTimeUtc = new DateTimeOffset(beforeWrite, TimeSpan.Zero),
            Sha256 = sha256
        };
    }

    internal static void ValidateProjectPath(string projectPath)
    {
        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException("The local WorkVisual project file does not exist.", projectPath);
        }

        if (!string.Equals(Path.GetExtension(projectPath), ".wvs", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The project intake source must use the vendor-documented .wvs extension.", nameof(projectPath));
        }

        var current = new FileInfo(projectPath);
        if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("The WVS source cannot be a reparse point.");
        }

        for (var directory = current.Directory; directory is not null; directory = directory.Parent)
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("The WVS source cannot be reached through a reparse-point directory.");
            }
        }
    }

    internal static void ValidateOutputBoundary(string projectPath, string outputPath)
    {
        if (!string.Equals(Path.GetExtension(outputPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Receipt output must use the .json extension.", nameof(outputPath));
        }

        if (File.Exists(outputPath) || Directory.Exists(outputPath))
        {
            throw new IOException("Receipt output already exists; project intake is create-new only.");
        }

        var sourceDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new ArgumentException("The WVS source has no parent directory.", nameof(projectPath));
        var normalizedSourceDirectory = sourceDirectory
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (outputPath.StartsWith(normalizedSourceDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Receipt output must be outside the downloaded WVS source directory and all of its descendants.",
                nameof(outputPath));
        }
    }

    internal static bool IsOpaqueId(string value) =>
        !string.IsNullOrWhiteSpace(value) && OpaqueIdPattern().IsMatch(value);

    private static void ValidateOpaqueId(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (!IsOpaqueId(value))
        {
            throw new ArgumentException(
                "Value must match [A-Za-z0-9][A-Za-z0-9._-]{2,127} and contain no spaces or path characters.",
                parameterName);
        }
    }

    private static string NormalizeLocator(string path) =>
        Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();

    private static string ComputeStringSha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static EnvironmentCheck Passed(string id, string detail) =>
        new() { Id = id, Status = EnvironmentCheckStatus.Passed, Detail = detail };

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex OpaqueIdPattern();
}

public sealed record WorkVisualProjectIntakeVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public bool CurrentProjectVerified { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}

public static class WorkVisualProjectIntakeReceiptVerifier
{
    public static WorkVisualProjectIntakeVerificationResult VerifyIntegrity(
        WorkVisualProjectIntakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (!string.Equals(
                receipt.SchemaIdentity,
                WorkVisualProjectIntakeContract.ReceiptSchemaIdentity,
                StringComparison.Ordinal))
        {
            errors.Add($"schemaIdentity must be {WorkVisualProjectIntakeContract.ReceiptSchemaIdentity}");
        }

        if (receipt.SchemaVersion != WorkVisualProjectIntakeContract.ReceiptSchemaVersion)
        {
            errors.Add($"schemaVersion must be {WorkVisualProjectIntakeContract.ReceiptSchemaVersion}");
        }

        if (ReferenceEquals(receipt.Payload, null))
        {
            return Failed(receipt.PayloadSha256, "payload is required");
        }

        var payload = receipt.Payload;
        if (string.IsNullOrWhiteSpace(payload.ReceiptId)
            || !WorkVisualProjectIntakeRunner.IsOpaqueId(payload.AttemptId)
            || !WorkVisualProjectIntakeRunner.IsOpaqueId(payload.CaptureReference))
        {
            errors.Add("receiptId, a valid attemptId and a sanitized captureReference are required");
        }

        if (!IsSha256(payload.CoreAssemblySha256)
            || payload.Runtime is null
            || string.IsNullOrWhiteSpace(payload.Runtime.OsDescription)
            || string.IsNullOrWhiteSpace(payload.Runtime.FrameworkDescription)
            || string.IsNullOrWhiteSpace(payload.Runtime.ProcessArchitecture))
        {
            errors.Add("core/runtime identity is incomplete");
        }

        if (payload.StartedAtUtc > payload.CompletedAtUtc || payload.DurationMilliseconds < 0)
        {
            errors.Add("payload timing is invalid");
        }

        ValidateProjectIdentity(payload.ProjectFile, errors);
        ValidateSemantics(payload, errors);
        ValidateCollections(payload, errors);

        if (!string.Equals(
                receipt.PayloadSha256,
                ReceiptSerialization.ComputeCanonicalSha256(payload),
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("payloadSha256 does not match the canonical payload");
        }

        return new WorkVisualProjectIntakeVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            CurrentProjectVerified = false,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }

    public static WorkVisualProjectIntakeVerificationResult VerifyCurrentProject(
        WorkVisualProjectIntakeReceipt receipt,
        string projectPath)
    {
        var integrity = VerifyIntegrity(receipt);
        var errors = integrity.Errors.ToList();
        var currentVerified = false;
        if (integrity.Succeeded)
        {
            try
            {
                var current = WorkVisualProjectIntakeRunner.CaptureIdentity(projectPath);
                currentVerified = current == receipt.Payload.ProjectFile;
                if (!currentVerified)
                {
                    errors.Add("The current WVS source no longer matches the receipt file identity or locator identity.");
                }
            }
            catch (Exception exception) when (exception is IOException
                or InvalidDataException
                or UnauthorizedAccessException
                or ArgumentException)
            {
                errors.Add($"The current WVS source could not be verified: {exception.Message}");
            }
        }

        return integrity with
        {
            Succeeded = errors.Count == 0 && currentVerified,
            CurrentProjectVerified = currentVerified,
            Errors = errors
        };
    }

    private static void ValidateProjectIdentity(
        WorkVisualProjectFileIdentity identity,
        List<string> errors)
    {
        if (identity is null
            || !IsSha256(identity.SourcePathSha256)
            || !IsSha256(identity.FileNameSha256)
            || !IsSha256(identity.Sha256)
            || !string.Equals(identity.Extension, ".wvs", StringComparison.Ordinal)
            || identity.Bytes <= 0
            || identity.LastWriteTimeUtc == default)
        {
            errors.Add("payload.projectFile must contain a non-empty WVS byte identity and only hashed source locators");
        }
    }

    private static void ValidateSemantics(
        WorkVisualProjectIntakePayload payload,
        List<string> errors)
    {
        if (payload.TerminalClassification != EnvironmentTerminalClassification.Ready
            || payload.Disposition != WorkVisualProjectIntakeDisposition.FileIdentityCaptured
            || !payload.FileIdentityCaptured)
        {
            errors.Add("project intake can claim only Ready/FileIdentityCaptured");
        }

        if (payload.ProjectExtracted
            || payload.ProjectContentsInspected
            || payload.ControllerAccessed
            || payload.NetworkTrafficSent
            || payload.CredentialsUsed
            || payload.SourceProjectChanged
            || payload.ControllerConfigurationChanged
            || payload.NativeKssStatus != NativeKssStatus.NotRun
            || !payload.EnvironmentReusable)
        {
            errors.Add("project intake cannot claim extraction, content inspection, controller/network/credential use, mutation, native KSS or a non-reusable environment");
        }
    }

    private static void ValidateCollections(
        WorkVisualProjectIntakePayload payload,
        List<string> errors)
    {
        if (payload.Checks is null
            || payload.SideEffects is null
            || payload.UnsupportedClaims is null)
        {
            errors.Add("payload evidence collections cannot be null");
            return;
        }

        var actualCheckIds = payload.Checks.Select(check => check.Id).ToList();
        if (payload.Checks.Any(check => check.Status != EnvironmentCheckStatus.Passed)
            || actualCheckIds.Distinct(StringComparer.Ordinal).Count() != actualCheckIds.Count
            || !WorkVisualProjectIntakeRunner.RequiredCheckIds.Order(StringComparer.Ordinal).SequenceEqual(
                actualCheckIds.Order(StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            errors.Add("payload.checks must contain the exact unique all-passed project-intake check set");
        }

        if (payload.SideEffects.Count != 1
            || !payload.SideEffects[0].StartsWith("CreateNewReceiptFile:", StringComparison.Ordinal))
        {
            errors.Add("payload.sideEffects must contain exactly one create-new receipt output");
        }

        if (!WorkVisualProjectIntakeContract.RequiredUnsupportedClaims.SequenceEqual(
                payload.UnsupportedClaims,
                StringComparer.Ordinal))
        {
            errors.Add("payload.unsupportedClaims must preserve the exact project-intake claim boundary");
        }
    }

    private static WorkVisualProjectIntakeVerificationResult Failed(
        string payloadSha256,
        string error) =>
        new()
        {
            Succeeded = false,
            PayloadSha256 = payloadSha256,
            Errors = [error]
        };

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);
}

public static class WorkVisualProjectIntakeReceiptWriter
{
    public static string WriteNew(
        string outputPath,
        string sourceProjectPath,
        WorkVisualProjectIntakeReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceProjectPath);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!WorkVisualProjectIntakeReceiptVerifier.VerifyIntegrity(receipt).Succeeded)
        {
            throw new InvalidOperationException("WorkVisual project-intake receipt integrity is invalid.");
        }

        var fullOutputPath = Path.GetFullPath(outputPath);
        WorkVisualProjectIntakeRunner.ValidateOutputBoundary(
            Path.GetFullPath(sourceProjectPath),
            fullOutputPath);
        var currentProject = WorkVisualProjectIntakeRunner.CaptureIdentity(sourceProjectPath);
        if (currentProject != receipt.Payload.ProjectFile)
        {
            throw new InvalidOperationException(
                "The WVS source changed after intake and before receipt creation.");
        }

        var expectedSideEffect = $"CreateNewReceiptFile:{fullOutputPath}";
        if (!string.Equals(receipt.Payload.SideEffects.Single(), expectedSideEffect, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Receipt output does not match the recorded create-new side effect.");
        }

        var parent = Path.GetDirectoryName(fullOutputPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
        Directory.CreateDirectory(parent);
        using var stream = new FileStream(fullOutputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(ReceiptSerialization.ToJson(receipt));
        writer.WriteLine();
        writer.Flush();
        stream.Flush(flushToDisk: true);
        return fullOutputPath;
    }
}
