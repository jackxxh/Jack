using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

public static class OfficeLiteNativeKssDiagnosticContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.officelite-native-kss-diagnostic-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const string EmbeddedScriptResource = "KukaLab.Probes.WorkVisual.KrlNativeDiagnostic.csx";
    public const string EmbeddedScriptSha256 = "5A1265B2809BB89B3A5379672CA88802E09D349F9DEFD653FB4EC3F8B76F8A1E";
    public const string TransactionRoot = @"KRC:\R1\Program\KLAB_W4K1";
    public const string FailureMarker = "KRL_NATIVE_DIAGNOSTIC_FAILED=";
    public const string CleanupMarker = "CLEANUP_VERIFIED=";
    public const int ExpectedInvalidErrorNumber = 2137;
    public const int ExpectedInvalidErrorLine = 11;
    public const int ExpectedInvalidErrorColumn = 6;

    public static readonly IReadOnlyDictionary<string, string> FixtureRelativePaths = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["valid-src"] = "fixtures/minimal-ptp-lin-valid/controller-files/LAB_MINIMAL.src",
        ["valid-dat"] = "fixtures/minimal-ptp-lin-valid/controller-files/LAB_MINIMAL.dat",
        ["invalid-src"] = "fixtures/minimal-missing-target-invalid/controller-files/LAB_MISSING_TARGET.src",
        ["invalid-dat"] = "fixtures/minimal-missing-target-invalid/controller-files/LAB_MISSING_TARGET.dat"
    };

    public static readonly IReadOnlyDictionary<string, string> FixtureSha256 = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["valid-src"] = "646D9E216E7E974BADD3C88A067E9E6BBEDF429D0233851C6304ABD5261E07F0",
        ["valid-dat"] = "5EEB76FDC7ECB305D464B9E7D2BCD7834EF96C6DD6852DD1B03405782526502F",
        ["invalid-src"] = "C39DAAA3C5FC40CAF978771C31913AF25C1C5A7C3E1F3D4197D09C95F9C3CA80",
        ["invalid-dat"] = "140B95C2EE526284E532373EBFC5701A705F50612F6CBD27726036DF1EA2B431"
    };

    internal static readonly IReadOnlyList<string> RequiredUnsupportedGaps =
    [
        "The operation validates native KSS program selection and diagnostics only; it never starts or runs a program.",
        "No robot motion, machine-data compatibility, cycle-time or KUKA.Sim numeric equivalence is established.",
        "The fixed OfficeLite MADA remains KR3R540 C4SR and is not evidence for the target KR 210 R2700-2 profile."
    ];
}

public sealed record OfficeLiteNativeKssDiagnosticRequest
{
    public required OfficeLiteCycleRequest OfficeLite { get; init; }
    public required string RunnerPath { get; init; }
    public required string EvidenceDirectory { get; init; }
    public required string LabRoot { get; init; }
    public int RunnerTimeoutSeconds { get; init; } = 60;

    public static OfficeLiteNativeKssDiagnosticRequest CreateDefault(
        string assetRoot,
        string labRoot,
        string evidenceDirectory,
        string? vmrunPath = null,
        string? runnerPath = null,
        string? guestIpAddress = null,
        int readinessTimeoutSeconds = 240,
        int serviceObservationSeconds = 180,
        int runnerTimeoutSeconds = 60)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(labRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceDirectory);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return new OfficeLiteNativeKssDiagnosticRequest
        {
            OfficeLite = OfficeLiteCycleRequest.CreateDefault(
                assetRoot,
                vmrunPath,
                guestIpAddress,
                readinessTimeoutSeconds: readinessTimeoutSeconds) with
            {
                DiagnoseWorkVisualServices = true,
                ServiceObservationSeconds = serviceObservationSeconds
            },
            RunnerPath = Path.GetFullPath(runnerPath
                ?? Path.Combine(programFilesX86, "KUKA", "WorkVisual 6.0", "wvsr.exe")),
            EvidenceDirectory = Path.GetFullPath(evidenceDirectory),
            LabRoot = Path.GetFullPath(labRoot),
            RunnerTimeoutSeconds = runnerTimeoutSeconds
        };
    }
}

public sealed record OfficeLiteNativeKssDiagnosticReceipt
{
    public string SchemaIdentity { get; init; } = OfficeLiteNativeKssDiagnosticContract.ReceiptSchemaIdentity;
    public int SchemaVersion { get; init; } = OfficeLiteNativeKssDiagnosticContract.ReceiptSchemaVersion;
    public required OfficeLiteNativeKssDiagnosticPayload Payload { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record OfficeLiteNativeKssDiagnosticPayload
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
    public string RunnerPath { get; init; } = string.Empty;
    public string EvidenceDirectory { get; init; } = string.Empty;
    public string EmbeddedScriptSha256 { get; init; } = string.Empty;
    public string TransactionRoot { get; init; } = OfficeLiteNativeKssDiagnosticContract.TransactionRoot;
    public OfficeLiteCycleReceipt LifecycleReceipt { get; init; } = null!;
    public NativeKssRunnerCommandObservation NegativeControlCommand { get; init; } = new();
    public NativeKssRunnerCommandObservation LiveCommand { get; init; } = new();
    public NativeKssDiagnosticObservation Diagnostic { get; init; } = new();
    public NativeKssStatus NativeKssStatus { get; init; } = NativeKssStatus.NotRun;
    public bool CredentialsUsed { get; init; }
    public bool ProgramStartRequested { get; init; }
    public bool ProgramRunRequested { get; init; }
    public bool MotionRequested { get; init; }
    public bool ControllerMutationPerformed { get; init; }
    public bool ControllerStateRestored { get; init; }
    public bool EnvironmentReusable { get; init; }
    public List<EnvironmentCheck> Checks { get; init; } = [];
    public List<EnvironmentFileObservation> Files { get; init; } = [];
    public List<string> SideEffects { get; init; } = [];
    public List<string> UnsupportedGaps { get; init; } = [];
}

public sealed record NativeKssRunnerCommandObservation
{
    public bool Attempted { get; init; }
    public int ExitCode { get; init; }
    public bool TimedOut { get; init; }
    public bool CleanupVerified { get; init; }
    public long DurationMilliseconds { get; init; }
    public string StandardOutputSha256 { get; init; } = string.Empty;
    public string StandardErrorSha256 { get; init; } = string.Empty;
}

public sealed record NativeKssDiagnosticObservation
{
    public bool Attempted { get; init; }
    public string RobotStateBefore { get; init; } = string.Empty;
    public string RobotSelectedBefore { get; init; } = string.Empty;
    public bool UploadVerified { get; init; }
    public bool MessageWindowAvailable { get; init; }
    public bool ValidSelectSucceeded { get; init; }
    public bool ValidAccepted { get; init; }
    public List<NativeKssDiagnosticFinding> ValidErrors { get; init; } = [];
    public bool InvalidSelectSucceeded { get; init; }
    public bool InvalidSelectThrew { get; init; }
    public bool InvalidRejected { get; init; }
    public List<NativeKssDiagnosticFinding> InvalidErrors { get; init; } = [];
    public List<NativeKssRuntimeMessage> NewMessages { get; init; } = [];
    public bool CleanupVerified { get; init; }
}

public sealed record NativeKssDiagnosticFinding
{
    public string Module { get; init; } = string.Empty;
    public int ErrorNumber { get; init; }
    public int Line { get; init; }
    public int Column { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Parameter { get; init; } = string.Empty;
}

public sealed record NativeKssRuntimeMessage
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public int Number { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public string ResourceMessage { get; init; } = string.Empty;
}

public sealed record OfficeLiteNativeKssDiagnosticOutcome(OfficeLiteNativeKssDiagnosticReceipt Receipt)
{
    public bool Succeeded => Receipt.Payload.TerminalClassification == EnvironmentTerminalClassification.Ready;

    public int ExitCode => Receipt.Payload.TerminalClassification switch
    {
        EnvironmentTerminalClassification.Ready => 0,
        EnvironmentTerminalClassification.Failed => 2,
        _ => 3
    };
}

public sealed class OfficeLiteNativeKssDiagnosticRunner
{
    private readonly OfficeLiteCycleRunner _officeLiteRunner;
    private readonly IWorkVisualRunnerPlatform _workVisualPlatform;
    private readonly TimeProvider _timeProvider;

    public OfficeLiteNativeKssDiagnosticRunner()
        : this(new OfficeLiteCycleRunner(), new WorkVisualRunnerPlatform(), TimeProvider.System)
    {
    }

    internal OfficeLiteNativeKssDiagnosticRunner(
        OfficeLiteCycleRunner officeLiteRunner,
        IWorkVisualRunnerPlatform workVisualPlatform,
        TimeProvider timeProvider)
    {
        _officeLiteRunner = officeLiteRunner;
        _workVisualPlatform = workVisualPlatform;
        _timeProvider = timeProvider;
    }

    public OfficeLiteNativeKssDiagnosticOutcome Run(OfficeLiteNativeKssDiagnosticRequest request, string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAttemptId(attemptId);
        if (request.RunnerTimeoutSeconds is < 1 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "WorkVisual runner timeout must be from 1 through 180 seconds.");
        }

        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var checks = new List<EnvironmentCheck>();
        var files = new List<EnvironmentFileObservation>();
        var sideEffects = new List<string>();
        var runnerPath = Path.GetFullPath(request.RunnerPath);
        var evidenceDirectory = Path.GetFullPath(request.EvidenceDirectory);
        var labRoot = Path.GetFullPath(request.LabRoot);
        var scriptHash = OfficeLiteNativeKssDiagnosticContract.EmbeddedScriptSha256;
        var negative = new NativeKssRunnerCommandObservation { CleanupVerified = true };
        var live = new NativeKssRunnerCommandObservation { CleanupVerified = true };
        var diagnostic = new NativeKssDiagnosticObservation();
        var runnerCleanupVerified = true;
        OfficeLiteCycleReceipt? lifecycleReceipt = null;
        var fixturePaths = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!OperatingSystem.IsWindows())
        {
            checks.Add(Blocked("host-os", "OfficeLite and WorkVisual Script Runner require Windows."));
            return Complete();
        }

        if (!ObserveFile("workvisual-script-runner", runnerPath, true, null, files, checks))
        {
            return Complete();
        }

        foreach (var declaration in OfficeLiteNativeKssDiagnosticContract.FixtureRelativePaths)
        {
            var fullPath = Path.GetFullPath(Path.Combine(labRoot, declaration.Value.Replace('/', Path.DirectorySeparatorChar)));
            var expectedRoot = labRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase)
                || !ObserveFile($"fixture-{declaration.Key}", fullPath, false,
                    OfficeLiteNativeKssDiagnosticContract.FixtureSha256[declaration.Key], files, checks))
            {
                return Complete();
            }

            fixturePaths[declaration.Key] = fullPath;
        }

        string scriptPath;
        try
        {
            if (Directory.Exists(evidenceDirectory))
            {
                checks.Add(Failed("evidence-directory", "Evidence directory already exists; create-new semantics refused reuse."));
                return Complete();
            }

            Directory.CreateDirectory(evidenceDirectory);
            sideEffects.Add($"CreateEvidenceDirectory:{evidenceDirectory}");
            scriptPath = Path.Combine(evidenceDirectory, $"{attemptId}.krl-native-diagnostic.csx");
            var scriptBytes = ReadEmbeddedScript();
            scriptHash = Convert.ToHexString(SHA256.HashData(scriptBytes));
            if (!string.Equals(scriptHash, OfficeLiteNativeKssDiagnosticContract.EmbeddedScriptSha256, StringComparison.Ordinal))
            {
                checks.Add(Failed("native-kss-script", "Embedded native-KSS diagnostic script hash is not the pinned value."));
                return Complete();
            }

            WriteNew(scriptPath, scriptBytes);
            sideEffects.Add($"CreateProbeScript:{scriptPath}");
            files.Add(ObserveExistingFile("workvisual-krl-native-diagnostic-script", scriptPath, false));
            checks.Add(Passed("native-kss-script", "Pinned native-KSS selection diagnostic script was materialized as create-new evidence."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            checks.Add(Failed("evidence-directory", $"Evidence preparation failed safely: {exception.Message}"));
            return Complete();
        }

        var cycleRequest = request.OfficeLite with
        {
            DiagnoseWorkVisualServices = true,
            ServiceObservationSeconds = request.OfficeLite.ServiceObservationSeconds > 0
                ? request.OfficeLite.ServiceObservationSeconds
                : 180
        };
        var lifecycle = _officeLiteRunner.Run(
            cycleRequest,
            $"{attemptId}-lifecycle",
            context => ExecuteDiagnostic(context.GuestAddress, scriptPath));
        lifecycleReceipt = lifecycle.Receipt;

        if (lifecycleReceipt.Payload.TerminalClassification == EnvironmentTerminalClassification.Ready
            && diagnostic.ValidAccepted
            && diagnostic.InvalidRejected
            && diagnostic.InvalidErrors.Any(error => error.ErrorNumber > 0 && error.Line > 0)
            && diagnostic.CleanupVerified
            && checks.All(check => check.Status is not EnvironmentCheckStatus.Failed and not EnvironmentCheckStatus.Blocked))
        {
            checks.Add(Passed("native-kss-diagnostic", "Native KSS accepted the valid fixed fixture and rejected the intentional failure with attributable diagnostic evidence."));
        }
        else if (!diagnostic.Attempted)
        {
            checks.Add(Blocked("native-kss-diagnostic", "The native-KSS diagnostic did not run because OfficeLite DeviceInfo readiness was not established."));
        }

        checks.Add(lifecycleReceipt.Payload.CleanShutdownVerified && diagnostic.CleanupVerified
            ? Passed("environment-cleanup", "The controller transaction was deleted and OfficeLite soft shutdown reached zero relevant processes.")
            : Failed("environment-cleanup", "Controller transaction cleanup and OfficeLite soft shutdown were not both verified."));
        return Complete();

        void ExecuteDiagnostic(string address, string materializedScriptPath)
        {
            var negativeResult = RunWorkVisual("127.0.0.1", materializedScriptPath);
            negative = ToObservation(negativeResult);
            runnerCleanupVerified &= negativeResult.CleanupVerified;
            PreserveRawEvidence("negative", negativeResult);
            if (negativeResult.TimedOut
                || negativeResult.ExitCode != 42
                || !negativeResult.StandardOutput.Contains(OfficeLiteNativeKssDiagnosticContract.FailureMarker, StringComparison.Ordinal)
                || !ReadBoolMarker(negativeResult.StandardOutput, "CLEANUP_VERIFIED"))
            {
                checks.Add(Failed("negative-control", "The unreachable-address control did not return typed failure 42 with no cleanup obligation."));
                return;
            }

            checks.Add(Passed("negative-control", "The unreachable-address control returned typed failure 42 and verified no residual transaction."));
            var liveResult = RunWorkVisual(address, materializedScriptPath);
            live = ToObservation(liveResult);
            runnerCleanupVerified &= liveResult.CleanupVerified;
            PreserveRawEvidence("live", liveResult);
            diagnostic = ParseDiagnostic(liveResult.StandardOutput);
            sideEffects.Add($"CreateControllerDirectory:{OfficeLiteNativeKssDiagnosticContract.TransactionRoot}");
            sideEffects.Add("UploadFixedFixtureFiles:4");
            sideEffects.Add("SelectAndDeselectFixedPrograms:2");
            sideEffects.Add($"DeleteControllerDirectory:{OfficeLiteNativeKssDiagnosticContract.TransactionRoot}");

            if (!liveResult.CleanupVerified)
            {
                checks.Add(Failed("workvisual-runner-cleanup", "WorkVisual Script Runner cleanup could not be verified."));
            }
            else if (liveResult.ExitCode == 47 || !diagnostic.CleanupVerified)
            {
                checks.Add(Failed("controller-transaction-cleanup", "The isolated controller transaction could not be proven deleted."));
            }
            else if (liveResult.TimedOut)
            {
                checks.Add(Blocked("live-diagnostic", "The native-KSS diagnostic exceeded its bounded timeout."));
            }
            else if (liveResult.ExitCode != 0)
            {
                checks.Add(Blocked("live-diagnostic", $"The native-KSS diagnostic returned exit code {liveResult.ExitCode}."));
            }
            else if (!diagnostic.ValidAccepted || !diagnostic.InvalidRejected)
            {
                checks.Add(Failed("live-diagnostic", "The live diagnostic did not prove both valid acceptance and invalid rejection."));
            }
            else if (!diagnostic.InvalidErrors.Any(error => error.ErrorNumber > 0 && error.Line > 0))
            {
                checks.Add(Failed("live-diagnostic", "The intentional failure lacked attributable KSS error number and line evidence."));
            }
            else
            {
                checks.Add(Passed("live-diagnostic", "The native-KSS selection diagnostic completed its valid/invalid differential."));
            }
        }

        WorkVisualProcessResult RunWorkVisual(string address, string materializedScriptPath)
        {
            try
            {
                return _workVisualPlatform.Run(
                    runnerPath,
                    [
                        "-executescript",
                        $"-scriptpath={materializedScriptPath}",
                        $"-address={address}",
                        $"-validsrc={fixturePaths["valid-src"]}",
                        $"-validdat={fixturePaths["valid-dat"]}",
                        $"-invalidsrc={fixturePaths["invalid-src"]}",
                        $"-invaliddat={fixturePaths["invalid-dat"]}",
                        $"-transactionroot={OfficeLiteNativeKssDiagnosticContract.TransactionRoot}"
                    ],
                    evidenceDirectory,
                    TimeSpan.FromSeconds(request.RunnerTimeoutSeconds));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
            {
                return new WorkVisualProcessResult(-1, false, true, 0, string.Empty, $"{exception.GetType().Name}: {exception.Message}");
            }
        }

        void PreserveRawEvidence(string prefix, WorkVisualProcessResult result)
        {
            var stdoutPath = Path.Combine(evidenceDirectory, $"{attemptId}.{prefix}.stdout.log");
            var stderrPath = Path.Combine(evidenceDirectory, $"{attemptId}.{prefix}.stderr.log");
            WriteNew(stdoutPath, Encoding.UTF8.GetBytes(result.StandardOutput));
            WriteNew(stderrPath, Encoding.UTF8.GetBytes(result.StandardError));
            sideEffects.Add($"CreateRawStdout:{stdoutPath}");
            sideEffects.Add($"CreateRawStderr:{stderrPath}");
            files.Add(ObserveExistingFile($"workvisual-{prefix}-stdout", stdoutPath, false));
            files.Add(ObserveExistingFile($"workvisual-{prefix}-stderr", stderrPath, false));
        }

        OfficeLiteNativeKssDiagnosticOutcome Complete()
        {
            stopwatch.Stop();
            lifecycleReceipt ??= CreateUnavailableLifecycleReceipt(request.OfficeLite, attemptId, startedAt);
            var terminal = checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)
                ? EnvironmentTerminalClassification.Failed
                : checks.Any(check => check.Status == EnvironmentCheckStatus.Blocked)
                    || lifecycleReceipt.Payload.TerminalClassification != EnvironmentTerminalClassification.Ready
                    ? EnvironmentTerminalClassification.Blocked
                    : EnvironmentTerminalClassification.Ready;
            var nativeStatus = terminal == EnvironmentTerminalClassification.Ready
                && diagnostic.ValidAccepted
                && diagnostic.InvalidRejected
                && diagnostic.CleanupVerified
                ? NativeKssStatus.SyntaxSelectionValidated
                : NativeKssStatus.NotRun;
            var payload = new OfficeLiteNativeKssDiagnosticPayload
            {
                ReceiptId = $"officelite-native-kss-diagnostic-{attemptId}",
                AttemptId = attemptId,
                CoreAssemblySha256 = ComputeFileSha256(typeof(OfficeLiteNativeKssDiagnosticRunner).Assembly.Location),
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
                RunnerPath = runnerPath,
                EvidenceDirectory = evidenceDirectory,
                EmbeddedScriptSha256 = scriptHash,
                LifecycleReceipt = lifecycleReceipt,
                NegativeControlCommand = negative,
                LiveCommand = live,
                Diagnostic = diagnostic,
                NativeKssStatus = nativeStatus,
                CredentialsUsed = false,
                ProgramStartRequested = false,
                ProgramRunRequested = false,
                MotionRequested = false,
                ControllerMutationPerformed = diagnostic.Attempted,
                ControllerStateRestored = diagnostic.CleanupVerified,
                EnvironmentReusable = runnerCleanupVerified && diagnostic.CleanupVerified && lifecycleReceipt.Payload.CleanShutdownVerified,
                Checks = checks,
                Files = files,
                SideEffects = sideEffects,
                UnsupportedGaps = OfficeLiteNativeKssDiagnosticContract.RequiredUnsupportedGaps.ToList()
            };
            var receipt = new OfficeLiteNativeKssDiagnosticReceipt
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
            };
            return new OfficeLiteNativeKssDiagnosticOutcome(receipt);
        }
    }

    internal static NativeKssDiagnosticObservation ParseDiagnostic(string output)
    {
        return new NativeKssDiagnosticObservation
        {
            Attempted = output.Contains("KRL_NATIVE_DIAGNOSTIC_BEGIN=True", StringComparison.Ordinal),
            RobotStateBefore = ReadMarker(output, "ROBOT_STATE_BEFORE") ?? string.Empty,
            RobotSelectedBefore = DecodeBase64(ReadMarker(output, "ROBOT_SELECTED_BEFORE_BASE64")),
            UploadVerified = ReadBoolMarker(output, "UPLOAD_VERIFIED"),
            MessageWindowAvailable = !output.Contains("MESSAGE_WINDOW_UNAVAILABLE=", StringComparison.Ordinal),
            ValidSelectSucceeded = ReadBoolMarker(output, "VALID_SELECT_SUCCEEDED"),
            ValidAccepted = ReadBoolMarker(output, "VALID_ACCEPTED_FINAL"),
            ValidErrors = ReadErrors(output, "VALID_ERROR").ToList(),
            InvalidSelectSucceeded = ReadBoolMarker(output, "INVALID_SELECT_SUCCEEDED"),
            InvalidSelectThrew = ReadBoolMarker(output, "INVALID_SELECT_THREW"),
            InvalidRejected = ReadBoolMarker(output, "INVALID_REJECTED_FINAL"),
            InvalidErrors = ReadErrors(output, "INVALID_ERROR").ToList(),
            NewMessages = ReadMessages(output).ToList(),
            CleanupVerified = ReadBoolMarker(output, "CLEANUP_VERIFIED")
        };
    }

    private static IEnumerable<NativeKssDiagnosticFinding> ReadErrors(string output, string marker)
    {
        foreach (var value in ReadMarkerValues(output, marker))
        {
            var parts = value.Split('|');
            if (parts.Length != 6
                || !int.TryParse(parts[1], out var number)
                || !int.TryParse(parts[2], out var line)
                || !int.TryParse(parts[3], out var column))
            {
                continue;
            }

            yield return new NativeKssDiagnosticFinding
            {
                Module = DecodeBase64(parts[0]),
                ErrorNumber = number,
                Line = line,
                Column = column,
                Description = DecodeBase64(parts[4]),
                Parameter = DecodeBase64(parts[5])
            };
        }
    }

    private static IEnumerable<NativeKssRuntimeMessage> ReadMessages(string output)
    {
        foreach (var value in ReadMarkerValues(output, "NEW_MESSAGE"))
        {
            var parts = value.Split('|');
            if (parts.Length != 6 || !int.TryParse(parts[0], out var id) || !int.TryParse(parts[2], out var number))
            {
                continue;
            }

            yield return new NativeKssRuntimeMessage
            {
                Id = id,
                Code = DecodeBase64(parts[1]),
                Number = number,
                Type = DecodeBase64(parts[3]),
                Text = DecodeBase64(parts[4]),
                ResourceMessage = DecodeBase64(parts[5])
            };
        }
    }

    private static IEnumerable<string> ReadMarkerValues(string output, string marker)
    {
        var prefix = marker + "=";
        foreach (var line in output.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                yield return trimmed[prefix.Length..];
            }
        }
    }

    private static string? ReadMarker(string output, string name) => ReadMarkerValues(output, name).LastOrDefault();

    private static bool ReadBoolMarker(string output, string name) =>
        bool.TryParse(ReadMarker(output, name), out var value) && value;

    private static string DecodeBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
        catch (FormatException) { return string.Empty; }
    }

    private static NativeKssRunnerCommandObservation ToObservation(WorkVisualProcessResult result) => new()
    {
        Attempted = true,
        ExitCode = result.ExitCode,
        TimedOut = result.TimedOut,
        CleanupVerified = result.CleanupVerified,
        DurationMilliseconds = result.DurationMilliseconds,
        StandardOutputSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.StandardOutput))),
        StandardErrorSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.StandardError)))
    };

    private static byte[] ReadEmbeddedScript()
    {
        using var stream = typeof(OfficeLiteNativeKssDiagnosticRunner).Assembly.GetManifestResourceStream(
            OfficeLiteNativeKssDiagnosticContract.EmbeddedScriptResource)
            ?? throw new InvalidOperationException("Embedded native-KSS diagnostic script was not found.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static void ValidateAttemptId(string attemptId)
    {
        if (string.IsNullOrWhiteSpace(attemptId) || !Regex.IsMatch(attemptId, "^[A-Za-z0-9][A-Za-z0-9._-]{0,95}$"))
        {
            throw new ArgumentException("Attempt ID must be 1-96 portable identifier characters.", nameof(attemptId));
        }
    }

    private static bool ObserveFile(
        string id,
        string path,
        bool includeVersion,
        string? expectedSha256,
        List<EnvironmentFileObservation> files,
        List<EnvironmentCheck> checks)
    {
        if (!File.Exists(path))
        {
            files.Add(new EnvironmentFileObservation { Id = id, Path = Path.GetFullPath(path), Exists = false });
            checks.Add(Blocked(id, $"Required file is missing: {path}"));
            return false;
        }

        var observation = ObserveExistingFile(id, path, includeVersion);
        files.Add(observation);
        if (expectedSha256 is not null && !string.Equals(observation.Sha256, expectedSha256, StringComparison.Ordinal))
        {
            checks.Add(Failed(id, $"Pinned file SHA-256 mismatch: {path}"));
            return false;
        }

        checks.Add(Passed(id, expectedSha256 is null ? $"Observed required file: {path}" : $"Pinned file hash accepted: {path}"));
        return true;
    }

    private static EnvironmentFileObservation ObserveExistingFile(string id, string path, bool includeVersion)
    {
        var info = new FileInfo(path);
        return new EnvironmentFileObservation
        {
            Id = id,
            Path = info.FullName,
            Exists = true,
            Bytes = info.Length,
            Sha256 = ComputeFileSha256(info.FullName),
            FileVersion = includeVersion ? FileVersionInfo.GetVersionInfo(info.FullName).FileVersion : null,
            ProductVersion = includeVersion ? FileVersionInfo.GetVersionInfo(info.FullName).ProductVersion : null
        };
    }

    private static void WriteNew(string path, byte[] content)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(content);
    }

    private static OfficeLiteCycleReceipt CreateUnavailableLifecycleReceipt(OfficeLiteCycleRequest request, string attemptId, DateTimeOffset observedAt)
    {
        var payload = new OfficeLiteCyclePayload
        {
            ReceiptId = $"officelite-cycle-{attemptId}-not-run",
            AttemptId = $"{attemptId}-not-run",
            CoreAssemblySha256 = ComputeFileSha256(typeof(OfficeLiteCycleRunner).Assembly.Location),
            Runtime = new RuntimeEnvironment
            {
                OsDescription = RuntimeInformation.OSDescription,
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
            },
            StartedAtUtc = observedAt,
            CompletedAtUtc = observedAt,
            DurationMilliseconds = 0,
            TerminalClassification = EnvironmentTerminalClassification.Blocked,
            VmrunPath = Path.GetFullPath(request.VmrunPath),
            Checks = [Blocked("lifecycle", "OfficeLite lifecycle was not started because host-side preconditions failed.")],
            UnsupportedGaps = ["Native KSS was not invoked."]
        };
        return new OfficeLiteCycleReceipt
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
    }

    private static EnvironmentCheck Passed(string id, string detail) => new() { Id = id, Status = EnvironmentCheckStatus.Passed, Detail = detail };
    private static EnvironmentCheck Blocked(string id, string detail) => new() { Id = id, Status = EnvironmentCheckStatus.Blocked, Detail = detail };
    private static EnvironmentCheck Failed(string id, string detail) => new() { Id = id, Status = EnvironmentCheckStatus.Failed, Detail = detail };
    private static string ComputeFileSha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}

public sealed record OfficeLiteNativeKssDiagnosticVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;
    public bool Succeeded { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
    public List<string> Errors { get; init; } = [];
}

public static class OfficeLiteNativeKssDiagnosticReceiptVerifier
{
    private static readonly Regex Sha256Pattern = new("^[0-9A-F]{64}$", RegexOptions.CultureInvariant);
    private static readonly string[] RequiredWorkVisualFileIds =
    [
        "workvisual-script-runner",
        "workvisual-krl-native-diagnostic-script",
        "workvisual-negative-stdout",
        "workvisual-negative-stderr",
        "workvisual-live-stdout",
        "workvisual-live-stderr"
    ];

    public static OfficeLiteNativeKssDiagnosticVerificationResult Verify(OfficeLiteNativeKssDiagnosticReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (receipt.SchemaIdentity != OfficeLiteNativeKssDiagnosticContract.ReceiptSchemaIdentity
            || receipt.SchemaVersion != OfficeLiteNativeKssDiagnosticContract.ReceiptSchemaVersion)
        {
            errors.Add("native-KSS diagnostic receipt schema identity/version is unsupported");
        }

        var payload = receipt.Payload;
        var expectedPayloadHash = ReceiptSerialization.ComputeCanonicalSha256(payload);
        if (!string.Equals(expectedPayloadHash, receipt.PayloadSha256, StringComparison.Ordinal))
        {
            errors.Add("payload SHA-256 mismatch");
        }

        var acceptedOperation = payload.TerminalClassification == EnvironmentTerminalClassification.Ready;
        if (acceptedOperation && payload.NativeKssStatus != NativeKssStatus.SyntaxSelectionValidated)
        {
            errors.Add("Ready receipt must have SyntaxSelectionValidated status");
        }
        else if (!acceptedOperation && payload.NativeKssStatus != NativeKssStatus.NotRun)
        {
            errors.Add("non-Ready receipt must retain native KSS status NotRun");
        }

        if (payload.CredentialsUsed || payload.ProgramStartRequested || payload.ProgramRunRequested || payload.MotionRequested)
        {
            errors.Add("credential, program-start/run and motion claims must remain false");
        }

        if (acceptedOperation && (!payload.ControllerMutationPerformed || !payload.ControllerStateRestored || !payload.EnvironmentReusable))
        {
            errors.Add("accepted receipt must declare temporary controller mutation, verified restoration and reusable environment");
        }

        if (!string.Equals(payload.TransactionRoot, OfficeLiteNativeKssDiagnosticContract.TransactionRoot, StringComparison.Ordinal)
            || !string.Equals(payload.EmbeddedScriptSha256, OfficeLiteNativeKssDiagnosticContract.EmbeddedScriptSha256, StringComparison.Ordinal))
        {
            errors.Add("fixed transaction root or embedded script hash mismatch");
        }

        if (!Sha256Pattern.IsMatch(payload.CoreAssemblySha256)
            || payload.CompletedAtUtc < payload.StartedAtUtc
            || payload.DurationMilliseconds < 0)
        {
            errors.Add("core assembly identity or receipt timing is invalid");
        }

        if (acceptedOperation && (!payload.NegativeControlCommand.Attempted || payload.NegativeControlCommand.ExitCode != 42
            || payload.NegativeControlCommand.TimedOut || !payload.NegativeControlCommand.CleanupVerified))
        {
            errors.Add("negative control must return exit 42 without timeout or residue");
        }

        if (acceptedOperation && (!payload.LiveCommand.Attempted || payload.LiveCommand.ExitCode != 0
            || payload.LiveCommand.TimedOut || !payload.LiveCommand.CleanupVerified))
        {
            errors.Add("live command must exit zero without timeout or runner residue");
        }

        var diagnostic = payload.Diagnostic;
        if (acceptedOperation && (!diagnostic.Attempted || !diagnostic.UploadVerified || !diagnostic.ValidSelectSucceeded
            || !diagnostic.ValidAccepted || diagnostic.ValidErrors.Count != 0))
        {
            errors.Add("valid fixture must upload, select successfully and have zero related KSS errors");
        }

        if (acceptedOperation && (!diagnostic.InvalidRejected || diagnostic.InvalidErrors.Count == 0
            || !diagnostic.InvalidErrors.Any(error => error.ErrorNumber == OfficeLiteNativeKssDiagnosticContract.ExpectedInvalidErrorNumber
                && error.Line == OfficeLiteNativeKssDiagnosticContract.ExpectedInvalidErrorLine
                && error.Column == OfficeLiteNativeKssDiagnosticContract.ExpectedInvalidErrorColumn)
            || diagnostic.InvalidErrors.Any(error => !error.Module.StartsWith(OfficeLiteNativeKssDiagnosticContract.TransactionRoot + "\\", StringComparison.OrdinalIgnoreCase))))
        {
            errors.Add("invalid fixture must be rejected with attributable error/module/line evidence inside the fixed transaction root");
        }

        if (acceptedOperation && (!diagnostic.CleanupVerified || !string.IsNullOrWhiteSpace(diagnostic.RobotSelectedBefore)))
        {
            errors.Add("controller cleanup must pass and the Robot interpreter must have been unselected before mutation");
        }

        if (payload.LifecycleReceipt is null)
        {
            errors.Add("OfficeLite lifecycle receipt is required");
        }
        else
        {
            var lifecycle = OfficeLiteCycleReceiptVerifier.Verify(payload.LifecycleReceipt);
            if (!lifecycle.Succeeded || (acceptedOperation && !payload.LifecycleReceipt.Payload.CleanShutdownVerified))
            {
                errors.Add("nested OfficeLite lifecycle receipt or clean shutdown is invalid");
            }
        }

        foreach (var id in OfficeLiteNativeKssDiagnosticContract.FixtureRelativePaths.Keys)
        {
            var observations = payload.Files.Where(file => file.Id == $"fixture-{id}").ToArray();
            if (acceptedOperation && (observations.Length != 1
                || !observations[0].Exists
                || !File.Exists(observations[0].Path)
                || !string.Equals(observations[0].Sha256, OfficeLiteNativeKssDiagnosticContract.FixtureSha256[id], StringComparison.Ordinal)
                || !string.Equals(ComputeFileSha256(observations[0].Path), observations[0].Sha256, StringComparison.Ordinal)))
            {
                errors.Add($"fixture-{id} current bytes do not match the pinned receipt identity");
            }
        }

        foreach (var file in payload.Files.Where(file => file.Exists))
        {
            if (!File.Exists(file.Path) || file.Sha256 is null || !Sha256Pattern.IsMatch(file.Sha256)
                || !string.Equals(ComputeFileSha256(file.Path), file.Sha256, StringComparison.Ordinal))
            {
                errors.Add($"current evidence drifted: {file.Id}");
            }
        }

        if (acceptedOperation)
        {
            var observedWorkVisualFiles = new Dictionary<string, EnvironmentFileObservation>(StringComparer.Ordinal);
            foreach (var id in RequiredWorkVisualFileIds)
            {
                var observations = payload.Files.Where(file => string.Equals(file.Id, id, StringComparison.Ordinal)).ToArray();
                if (observations.Length != 1 || !observations[0].Exists)
                {
                    errors.Add($"required WorkVisual evidence must occur exactly once: {id}");
                    continue;
                }

                observedWorkVisualFiles[id] = observations[0];
            }

            VerifyRawEvidenceBindings(payload, observedWorkVisualFiles, errors);
        }

        if (payload.UnsupportedGaps is null
            || !payload.UnsupportedGaps.SequenceEqual(OfficeLiteNativeKssDiagnosticContract.RequiredUnsupportedGaps, StringComparer.Ordinal))
        {
            errors.Add("unsupported-gap declarations are incomplete or reordered");
        }

        return new OfficeLiteNativeKssDiagnosticVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = receipt.PayloadSha256,
            Errors = errors
        };
    }

    private static void VerifyRawEvidenceBindings(
        OfficeLiteNativeKssDiagnosticPayload payload,
        IReadOnlyDictionary<string, EnvironmentFileObservation> files,
        List<string> errors)
    {
        if (files.Count != RequiredWorkVisualFileIds.Length)
        {
            return;
        }

        var script = files["workvisual-krl-native-diagnostic-script"];
        var negativeStdout = files["workvisual-negative-stdout"];
        var negativeStderr = files["workvisual-negative-stderr"];
        var liveStdout = files["workvisual-live-stdout"];
        var liveStderr = files["workvisual-live-stderr"];
        if (!string.Equals(script.Sha256, payload.EmbeddedScriptSha256, StringComparison.Ordinal)
            || !string.Equals(negativeStdout.Sha256, payload.NegativeControlCommand.StandardOutputSha256, StringComparison.Ordinal)
            || !string.Equals(negativeStderr.Sha256, payload.NegativeControlCommand.StandardErrorSha256, StringComparison.Ordinal)
            || !string.Equals(liveStdout.Sha256, payload.LiveCommand.StandardOutputSha256, StringComparison.Ordinal)
            || !string.Equals(liveStderr.Sha256, payload.LiveCommand.StandardErrorSha256, StringComparison.Ordinal))
        {
            errors.Add("command output hashes are not bound to the observed raw evidence");
        }

        var evidenceRoot = Path.GetFullPath(payload.EvidenceDirectory).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        foreach (var file in files.Values.Where(file => file.Id != "workvisual-script-runner"))
        {
            if (!Path.GetFullPath(file.Path).StartsWith(evidenceRoot, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"raw evidence escaped the declared evidence directory: {file.Id}");
            }
        }

        if (!File.Exists(negativeStdout.Path) || !File.Exists(liveStdout.Path))
        {
            return;
        }

        var negativeOutput = File.ReadAllText(negativeStdout.Path);
        if (!negativeOutput.Contains(OfficeLiteNativeKssDiagnosticContract.FailureMarker, StringComparison.Ordinal)
            || !negativeOutput.Contains(OfficeLiteNativeKssDiagnosticContract.CleanupMarker + "True", StringComparison.Ordinal))
        {
            errors.Add("negative-control raw output does not corroborate typed failure and cleanup");
        }

        var parsed = OfficeLiteNativeKssDiagnosticRunner.ParseDiagnostic(File.ReadAllText(liveStdout.Path));
        if (!string.Equals(
                ReceiptSerialization.ComputeCanonicalSha256(parsed),
                ReceiptSerialization.ComputeCanonicalSha256(payload.Diagnostic),
                StringComparison.Ordinal))
        {
            errors.Add("live raw output does not corroborate the diagnostic payload");
        }
    }

    private static string ComputeFileSha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}

public static class OfficeLiteNativeKssDiagnosticReceiptWriter
{
    public static string WriteNew(string outputPath, OfficeLiteNativeKssDiagnosticReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!OfficeLiteNativeKssDiagnosticReceiptVerifier.Verify(receipt).Succeeded)
        {
            throw new InvalidOperationException("OfficeLite native-KSS diagnostic receipt integrity is invalid.");
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
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(ReceiptSerialization.ToJson(receipt));
        writer.WriteLine();
        writer.Flush();
        stream.Flush(flushToDisk: true);
        return fullPath;
    }
}
