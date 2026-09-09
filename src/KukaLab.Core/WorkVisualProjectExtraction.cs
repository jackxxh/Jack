using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace KukaLab.Core;

public static class WorkVisualProjectExtractionContract
{
    public const string ReceiptSchemaIdentity = "kuka.lab.workvisual-project-extraction-receipt";
    public const int ReceiptSchemaVersion = 3;
    public const string MalformedFixtureText = "KUKA-LAB-INTENTIONAL-MALFORMED-WVS\n";
    public const string MalformedFixtureSha256 = "EB8E01F39EA053AEAFD81573AFB977FCF3920E3E62CEA630D956F9A3D7D7D7B0";

    internal static readonly IReadOnlyList<string> RequiredCheckIds =
    [
        "project-intake",
        "project-extractor",
        "source-boundary",
        "positive-extraction",
        "negative-malformed-rejection",
        "source-unchanged",
        "extracted-tree",
        "robot-profile",
        "process-cleanup"
    ];

    internal static readonly IReadOnlyList<string> RequiredUnsupportedGaps =
    [
        "Extraction and profile parsing operate on a local WVS copy only; they do not authenticate the origin of that WVS beyond its supplied intake receipt.",
        "Robot identity, controller software family, cabinet kind, axis limits and Tool/Base/Load values are read from extracted project files; they remain project evidence rather than physical qualification.",
        "Technology-package directory names are path-level observations only; they do not prove installed package versions, licenses, compatibility or a complete package inventory.",
        "No controller, network, credential, deployment, activation, project save, KSS execution, KUKA.Sim operation or physical motion is invoked."
    ];
}

public sealed record WorkVisualProjectExtractionRequest
{
    public required string ProjectPath { get; init; }

    public required WorkVisualProjectIntakeReceipt ProjectIntakeReceipt { get; init; }

    public required string ExtractorPath { get; init; }

    public required string EvidenceDirectory { get; init; }

    public int TimeoutSeconds { get; init; } = 60;

    public static WorkVisualProjectExtractionRequest CreateDefault(
        string projectPath,
        WorkVisualProjectIntakeReceipt intakeReceipt,
        string evidenceDirectory,
        string? extractorPath = null,
        int timeoutSeconds = 60)
    {
        ArgumentNullException.ThrowIfNull(intakeReceipt);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return new WorkVisualProjectExtractionRequest
        {
            ProjectPath = Path.GetFullPath(projectPath),
            ProjectIntakeReceipt = intakeReceipt,
            ExtractorPath = Path.GetFullPath(extractorPath
                ?? Path.Combine(programFilesX86, "KUKA", "WorkVisual 6.0", "Tools", "ProjectExtractor", "ProjectExtractor.exe")),
            EvidenceDirectory = Path.GetFullPath(evidenceDirectory),
            TimeoutSeconds = timeoutSeconds
        };
    }
}

public sealed record WorkVisualExtractedTreeIdentity
{
    public string RootPath { get; init; } = string.Empty;

    public int FileCount { get; init; }

    public long TotalBytes { get; init; }

    public string TreeSha256 { get; init; } = string.Empty;
}

public sealed record WorkVisualExtractedControllerProfile
{
    public string TrafoName { get; init; } = string.Empty;

    public string ModelName { get; init; } = string.Empty;

    public bool RobotIdentityConsistent { get; init; }

    public int AxisMadaFileCount { get; init; }

    public int ToolDataCount { get; init; }

    public int BaseDataCount { get; init; }

    public int LoadDataCount { get; init; }

    public List<WorkVisualFrameData> ToolData { get; init; } = [];

    public List<WorkVisualFrameData> BaseData { get; init; } = [];

    public List<WorkVisualLoadData> LoadData { get; init; } = [];

    public string ControllerSoftwareFamily { get; init; } = string.Empty;

    public string CabinetKind { get; init; } = string.Empty;

    public List<WorkVisualAxisLimit> AxisLimits { get; init; } = [];

    public List<string> TechnologyPackageDirectories { get; init; } = [];

    public bool TechnologyPackageSemanticsQualified { get; init; }

    public EnvironmentFileObservation MachineDat { get; init; } = new();

    public EnvironmentFileObservation RobcorDat { get; init; } = new();

    public EnvironmentFileObservation ConfigDat { get; init; } = new();

    public EnvironmentFileObservation CabinetControlXml { get; init; } = new();
}

public sealed record WorkVisualAxisLimit
{
    public int AxisNumber { get; init; }

    public double Negative { get; init; }

    public double Positive { get; init; }

    public string Unit { get; init; } = "deg";
}

public sealed record WorkVisualFrameData
{
    public int Index { get; init; }

    public double X { get; init; }

    public double Y { get; init; }

    public double Z { get; init; }

    public double A { get; init; }

    public double B { get; init; }

    public double C { get; init; }
}

public sealed record WorkVisualLoadData
{
    public int Index { get; init; }

    public double Mass { get; init; }

    public WorkVisualFrameData CenterOfMass { get; init; } = new();

    public double InertiaX { get; init; }

    public double InertiaY { get; init; }

    public double InertiaZ { get; init; }
}

public sealed record WorkVisualProjectExtractionReceipt
{
    public string SchemaIdentity { get; init; } = WorkVisualProjectExtractionContract.ReceiptSchemaIdentity;

    public int SchemaVersion { get; init; } = WorkVisualProjectExtractionContract.ReceiptSchemaVersion;

    public required WorkVisualProjectExtractionPayload Payload { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;
}

public sealed record WorkVisualProjectExtractionPayload
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

    public required WorkVisualProjectIntakeReceipt ProjectIntakeReceipt { get; init; }

    public string ProjectIntakeReceiptSha256 { get; init; } = string.Empty;

    public EnvironmentFileObservation ProjectFile { get; init; } = new();

    public EnvironmentFileObservation ExtractorFile { get; init; } = new();

    public WorkVisualRunnerCommandObservation PositiveCommand { get; init; } = new();

    public WorkVisualRunnerCommandObservation NegativeCommand { get; init; } = new();

    public WorkVisualExtractedTreeIdentity ExtractedTree { get; init; } = new();

    public WorkVisualExtractedControllerProfile ControllerProfile { get; init; } = new();

    public bool ProjectExtracted { get; init; }

    public bool ProjectContentsInspected { get; init; }

    public bool ControllerAccessed { get; init; }

    public bool NetworkTrafficSent { get; init; }

    public bool CredentialsUsed { get; init; }

    public bool SourceProjectChanged { get; init; }

    public bool ControllerConfigurationChanged { get; init; }

    public NativeKssStatus NativeKssStatus { get; init; } = NativeKssStatus.NotRun;

    public List<EnvironmentCheck> Checks { get; init; } = [];

    public List<EnvironmentFileObservation> Files { get; init; } = [];

    public List<string> SideEffects { get; init; } = [];

    public bool EnvironmentReusable { get; init; }

    public List<string> UnsupportedGaps { get; init; } = [];
}

public sealed record WorkVisualProjectExtractionOutcome(WorkVisualProjectExtractionReceipt Receipt)
{
    public int ExitCode => Receipt.Payload.TerminalClassification == EnvironmentTerminalClassification.Ready ? 0 : 2;
}

public sealed class WorkVisualProjectExtractionRunner
{
    private readonly IWorkVisualRunnerPlatform _platform;
    private readonly TimeProvider _timeProvider;

    public WorkVisualProjectExtractionRunner()
        : this(new WorkVisualRunnerPlatform(), TimeProvider.System)
    {
    }

    internal WorkVisualProjectExtractionRunner(
        IWorkVisualRunnerPlatform platform,
        TimeProvider timeProvider)
    {
        _platform = platform;
        _timeProvider = timeProvider;
    }

    public WorkVisualProjectExtractionOutcome Run(
        WorkVisualProjectExtractionRequest request,
        string attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(attemptId);
        if (request.TimeoutSeconds is < 1 or > 300)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "ProjectExtractor timeout must be from 1 through 300 seconds.");
        }

        var projectPath = Path.GetFullPath(request.ProjectPath);
        var extractorPath = Path.GetFullPath(request.ExtractorPath);
        var evidenceDirectory = Path.GetFullPath(request.EvidenceDirectory);
        var positiveTarget = Path.Combine(evidenceDirectory, "positive-target");
        var positiveLogs = Path.Combine(evidenceDirectory, "positive-logs");
        var negativeTarget = Path.Combine(evidenceDirectory, "negative-target");
        var negativeLogs = Path.Combine(evidenceDirectory, "negative-logs");
        var malformedPath = Path.Combine(evidenceDirectory, "intentional-malformed.wvs");
        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var checks = new List<EnvironmentCheck>();
        var files = new List<EnvironmentFileObservation>();
        var sideEffects = new List<string>();
        var positive = new WorkVisualRunnerCommandObservation { CleanupVerified = true };
        var negative = new WorkVisualRunnerCommandObservation { CleanupVerified = true };
        var tree = new WorkVisualExtractedTreeIdentity { RootPath = positiveTarget };
        var profile = new WorkVisualExtractedControllerProfile();
        var projectExtracted = false;
        var projectInspected = false;
        var cleanupVerified = true;
        var intakeReceiptSha = ReceiptSerialization.ComputeCanonicalSha256(request.ProjectIntakeReceipt);

        var intake = WorkVisualProjectIntakeReceiptVerifier.VerifyCurrentProject(
            request.ProjectIntakeReceipt,
            projectPath);
        if (!intake.Succeeded)
        {
            checks.Add(Failed("project-intake", "The WVS no longer matches its strict intake receipt."));
            return Complete();
        }

        checks.Add(Passed("project-intake", "The current WVS exactly matches its strict intake receipt."));
        if (!File.Exists(extractorPath))
        {
            checks.Add(Failed("project-extractor", "The installed ProjectExtractor executable is missing."));
            return Complete();
        }

        var projectBefore = ObserveFile("source-wvs", projectPath, false);
        var extractor = ObserveFile("project-extractor", extractorPath, true);
        files.Add(projectBefore);
        files.Add(extractor);
        checks.Add(Passed("project-extractor", "The installed ProjectExtractor executable exists and was hashed."));

        try
        {
            EnsureSeparate(projectPath, evidenceDirectory);
            if (Directory.Exists(evidenceDirectory))
            {
                throw new IOException("Evidence directory already exists; create-new semantics refused reuse.");
            }

            Directory.CreateDirectory(positiveTarget);
            Directory.CreateDirectory(positiveLogs);
            Directory.CreateDirectory(negativeTarget);
            Directory.CreateDirectory(negativeLogs);
            WriteNew(malformedPath, Encoding.UTF8.GetBytes(WorkVisualProjectExtractionContract.MalformedFixtureText));
            var malformed = ObserveFile("malformed-wvs", malformedPath, false);
            if (!string.Equals(
                    malformed.Sha256,
                    WorkVisualProjectExtractionContract.MalformedFixtureSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The malformed WVS fixture hash is not pinned.");
            }

            files.Add(malformed);
            sideEffects.Add($"CreateEvidenceDirectory:{evidenceDirectory}");
            sideEffects.Add($"CreateMalformedFixture:{malformedPath}");
            checks.Add(Passed("source-boundary", "Extraction targets are create-new and separate from the read-only WVS source."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            checks.Add(Failed("source-boundary", $"Extraction boundary preparation failed: {exception.Message}"));
            return Complete();
        }

        var positiveResult = RunExtractor(projectPath, positiveTarget, positiveLogs);
        positive = ToObservation(positiveResult);
        cleanupVerified &= positiveResult.CleanupVerified;
        PreserveOutput("positive", positiveResult);
        AddLogFiles("positive", positiveLogs);
        if (positiveResult.TimedOut || positiveResult.ExitCode != 0 || !positiveResult.CleanupVerified)
        {
            checks.Add(Failed("positive-extraction", "Known WVS extraction did not exit 0 with verified process cleanup."));
            return Complete();
        }

        tree = CaptureTree(positiveTarget);
        if (tree.FileCount <= 0 || tree.TotalBytes <= 0)
        {
            checks.Add(Failed("positive-extraction", "ProjectExtractor exited 0 but produced no extracted files."));
            return Complete();
        }

        projectExtracted = true;
        checks.Add(Passed("positive-extraction", $"ProjectExtractor produced {tree.FileCount} files and {tree.TotalBytes} bytes."));

        var negativeResult = RunExtractor(malformedPath, negativeTarget, negativeLogs);
        negative = ToObservation(negativeResult);
        cleanupVerified &= negativeResult.CleanupVerified;
        PreserveOutput("negative", negativeResult);
        AddLogFiles("negative", negativeLogs);
        if (negativeResult.TimedOut
            || negativeResult.ExitCode == 0
            || !negativeResult.CleanupVerified
            || Directory.EnumerateFiles(negativeTarget, "*", SearchOption.AllDirectories).Any())
        {
            checks.Add(Failed("negative-malformed-rejection", "Malformed WVS was not rejected with non-zero exit, zero extracted files and verified cleanup."));
            return Complete();
        }

        checks.Add(Passed("negative-malformed-rejection", $"Malformed WVS was rejected with exit {negativeResult.ExitCode} and zero extracted files."));
        var projectAfter = ObserveFile("source-wvs", projectPath, false);
        if (projectAfter.Bytes != projectBefore.Bytes
            || !string.Equals(projectAfter.Sha256, projectBefore.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            checks.Add(Failed("source-unchanged", "The source WVS changed during extraction."));
            return Complete();
        }

        checks.Add(Passed("source-unchanged", "The source WVS byte identity remained unchanged."));
        var currentTree = CaptureTree(positiveTarget);
        if (currentTree != tree)
        {
            checks.Add(Failed("extracted-tree", "The extracted tree changed before receipt creation."));
            return Complete();
        }

        checks.Add(Passed("extracted-tree", "The complete extracted tree has a deterministic SHA-256 identity."));
        try
        {
            profile = ParseProfile(positiveTarget);
            files.Add(profile.MachineDat);
            files.Add(profile.RobcorDat);
            files.Add(profile.ConfigDat);
            files.Add(profile.CabinetControlXml);
            projectInspected = true;
            checks.Add(Passed(
                "robot-profile",
                $"Extracted project consistently identifies {profile.ModelName}, {profile.CabinetKind}, {profile.ControllerSoftwareFamily}, six bounded robot axes and complete Tool/Base/Load arrays."));
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or XmlException)
        {
            checks.Add(Failed("robot-profile", $"Extracted robot profile is incomplete or inconsistent: {exception.Message}"));
            return Complete();
        }

        checks.Add(cleanupVerified
            ? Passed("process-cleanup", "Both ProjectExtractor processes exited and no process-tree termination was required.")
            : Failed("process-cleanup", "ProjectExtractor cleanup could not be verified."));
        return Complete();

        WorkVisualProcessResult RunExtractor(string source, string target, string logs)
        {
            try
            {
                return _platform.Run(
                    extractorPath,
                    [$"sourceproject={source}", $"targetdir={target}", $"logdir={logs}", "-noprojectdirectory"],
                    evidenceDirectory,
                    TimeSpan.FromSeconds(request.TimeoutSeconds));
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or Win32Exception)
            {
                return new WorkVisualProcessResult(-1, false, true, 0, string.Empty, exception.Message);
            }
        }

        void PreserveOutput(string prefix, WorkVisualProcessResult result)
        {
            var stdout = Path.Combine(evidenceDirectory, $"{attemptId}.{prefix}.stdout.log");
            var stderr = Path.Combine(evidenceDirectory, $"{attemptId}.{prefix}.stderr.log");
            WriteNew(stdout, Encoding.UTF8.GetBytes(result.StandardOutput));
            WriteNew(stderr, Encoding.UTF8.GetBytes(result.StandardError));
            files.Add(ObserveFile($"extractor-{prefix}-stdout", stdout, false));
            files.Add(ObserveFile($"extractor-{prefix}-stderr", stderr, false));
            sideEffects.Add($"CreateRawStdout:{stdout}");
            sideEffects.Add($"CreateRawStderr:{stderr}");
        }

        void AddLogFiles(string prefix, string logRoot)
        {
            var index = 0;
            foreach (var path in Directory.EnumerateFiles(logRoot, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                files.Add(ObserveFile($"extractor-{prefix}-log-{index:D3}", path, false));
                index++;
            }
        }

        WorkVisualProjectExtractionOutcome Complete()
        {
            stopwatch.Stop();
            if (!checks.Any(check => check.Id == "process-cleanup"))
            {
                checks.Add(cleanupVerified
                    ? Passed("process-cleanup", "No ProjectExtractor process remains owned by the attempt.")
                    : Failed("process-cleanup", "ProjectExtractor cleanup could not be verified."));
            }

            var terminal = checks.Any(check => check.Status != EnvironmentCheckStatus.Passed)
                ? EnvironmentTerminalClassification.Failed
                : EnvironmentTerminalClassification.Ready;
            var payload = new WorkVisualProjectExtractionPayload
            {
                ReceiptId = $"workvisual-project-extraction-{attemptId}",
                AttemptId = attemptId,
                CoreAssemblySha256 = ComputeSha256(typeof(WorkVisualProjectExtractionRunner).Assembly.Location),
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
                ProjectIntakeReceipt = request.ProjectIntakeReceipt,
                ProjectIntakeReceiptSha256 = intakeReceiptSha,
                ProjectFile = File.Exists(projectPath) ? ObserveFile("source-wvs", projectPath, false) : new EnvironmentFileObservation { Id = "source-wvs" },
                ExtractorFile = File.Exists(extractorPath) ? ObserveFile("project-extractor", extractorPath, true) : new EnvironmentFileObservation { Id = "project-extractor" },
                PositiveCommand = positive,
                NegativeCommand = negative,
                ExtractedTree = tree,
                ControllerProfile = profile,
                ProjectExtracted = projectExtracted,
                ProjectContentsInspected = projectInspected,
                ControllerAccessed = false,
                NetworkTrafficSent = false,
                CredentialsUsed = false,
                SourceProjectChanged = false,
                ControllerConfigurationChanged = false,
                NativeKssStatus = NativeKssStatus.NotRun,
                Checks = checks,
                Files = files,
                SideEffects = sideEffects,
                EnvironmentReusable = cleanupVerified,
                UnsupportedGaps = WorkVisualProjectExtractionContract.RequiredUnsupportedGaps.ToList()
            };
            var receipt = new WorkVisualProjectExtractionReceipt
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
            };
            return new WorkVisualProjectExtractionOutcome(receipt);
        }
    }

    internal static WorkVisualExtractedTreeIdentity CaptureTree(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        if (!Directory.Exists(fullRoot))
        {
            return new WorkVisualExtractedTreeIdentity { RootPath = fullRoot };
        }

        var entries = Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories)
            .Select(path => new
            {
                Path = path,
                Relative = Path.GetRelativePath(fullRoot, path).Replace('\\', '/'),
                Info = new FileInfo(path),
                Sha = ComputeSha256(path)
            })
            .OrderBy(entry => entry.Relative, StringComparer.Ordinal)
            .ToArray();
        var canonical = string.Join(
            "\n",
            entries.Select(entry => $"{entry.Relative}\0{entry.Info.Length}\0{entry.Sha}"));
        return new WorkVisualExtractedTreeIdentity
        {
            RootPath = fullRoot,
            FileCount = entries.Length,
            TotalBytes = entries.Sum(entry => entry.Info.Length),
            TreeSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
        };
    }

    internal static WorkVisualExtractedControllerProfile ParseProfile(string root)
    {
        var machine = Path.Combine(root, "KRC", "R1", "Mada", "$machine.dat");
        var robcor = Path.Combine(root, "KRC", "R1", "Mada", "$robcor.dat");
        var config = Path.Combine(root, "KRC", "R1", "System", "$config.dat");
        var cabinet = Path.Combine(root, "Config", "User", "Common", "CabCtrl.xml");
        var machineText = File.ReadAllText(machine, Encoding.Latin1);
        var robcorText = File.ReadAllText(robcor, Encoding.Latin1);
        var configText = File.ReadAllText(config, Encoding.Latin1);
        using var cabinetReader = XmlReader.Create(
            cabinet,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
        var cabinetDocument = XDocument.Load(cabinetReader, LoadOptions.None);
        var trafo = ReadQuotedAssignment(machineText, "$TRAFONAME[]");
        var model = ReadQuotedAssignment(robcorText, "$MODEL_NAME[]");
        if (!string.Equals(trafo, model, StringComparison.Ordinal))
        {
            throw new InvalidDataException("$TRAFONAME and $MODEL_NAME do not match exactly.");
        }

        var axisCount = Directory.Exists(Path.Combine(root, "Config", "User", "Common", "Mada", "NGAxis"))
            ? Directory.EnumerateFiles(Path.Combine(root, "Config", "User", "Common", "Mada", "NGAxis"), "A*.xml")
                .Count(path => Regex.IsMatch(Path.GetFileName(path), "^A[1-6]\\.xml$", RegexOptions.CultureInvariant))
            : 0;
        var softwareFamily = cabinetDocument.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Info")?
            .Attribute("Version")?.Value?.Trim() ?? string.Empty;
        var cabinetKind = cabinetDocument.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Cabinet")?
            .Attribute("kind")?.Value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(softwareFamily) || string.IsNullOrWhiteSpace(cabinetKind))
        {
            throw new InvalidDataException("CabCtrl.xml does not declare controller software family and cabinet kind.");
        }

        var axisLimits = Enumerable.Range(1, 6)
            .Select(index => new WorkVisualAxisLimit
            {
                AxisNumber = index,
                Negative = ReadNumberAssignment(machineText, $"$SOFTN_END[{index}]"),
                Positive = ReadNumberAssignment(machineText, $"$SOFTP_END[{index}]")
            })
            .ToList();
        if (axisLimits.Any(limit => !double.IsFinite(limit.Negative)
                || !double.IsFinite(limit.Positive)
                || limit.Negative >= limit.Positive))
        {
            throw new InvalidDataException("Robot software axis limits are missing or invalid.");
        }

        var toolData = ReadFrameAssignments(configText, "TOOL_DATA");
        var baseData = ReadFrameAssignments(configText, "BASE_DATA");
        var loadData = ReadLoadAssignments(configText);
        var technologyPackageRoot = Path.Combine(root, "KRC", "R1", "TP");
        var technologyPackageDirectories = Directory.Exists(technologyPackageRoot)
            ? Directory.EnumerateDirectories(technologyPackageRoot)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];
        return new WorkVisualExtractedControllerProfile
        {
            TrafoName = trafo,
            ModelName = model,
            RobotIdentityConsistent = true,
            AxisMadaFileCount = axisCount,
            ToolDataCount = toolData.Count,
            BaseDataCount = baseData.Count,
            LoadDataCount = loadData.Count,
            ToolData = toolData,
            BaseData = baseData,
            LoadData = loadData,
            ControllerSoftwareFamily = softwareFamily,
            CabinetKind = cabinetKind,
            AxisLimits = axisLimits,
            TechnologyPackageDirectories = technologyPackageDirectories,
            TechnologyPackageSemanticsQualified = false,
            MachineDat = ObserveFile("extracted-machine-dat", machine, false),
            RobcorDat = ObserveFile("extracted-robcor-dat", robcor, false),
            ConfigDat = ObserveFile("extracted-config-dat", config, false),
            CabinetControlXml = ObserveFile("extracted-cabinet-control-xml", cabinet, false)
        };
    }

    private static string ReadQuotedAssignment(string content, string symbol)
    {
        var match = Regex.Match(
            content,
            $"(?m)^\\s*{Regex.Escape(symbol)}\\s*=\\s*\"(?<value>[^\"]+)\"",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            throw new InvalidDataException($"Required assignment {symbol} was not found.");
        }

        return match.Groups["value"].Value;
    }

    private static List<WorkVisualFrameData> ReadFrameAssignments(string content, string symbol) =>
        ReadStructuredAssignments(content, symbol)
            .Select(assignment => ReadFrame(assignment.Index, assignment.Value))
            .ToList();

    private static List<WorkVisualLoadData> ReadLoadAssignments(string content) =>
        ReadStructuredAssignments(content, "LOAD_DATA")
            .Select(assignment =>
            {
                var centerOfMass = ReadNamedStructure(assignment.Value, "CM");
                var inertia = ReadNamedStructure(assignment.Value, "J");
                return new WorkVisualLoadData
                {
                    Index = assignment.Index,
                    Mass = ReadNamedNumber(assignment.Value, "M"),
                    CenterOfMass = ReadFrame(assignment.Index, centerOfMass),
                    InertiaX = ReadNamedNumber(inertia, "X"),
                    InertiaY = ReadNamedNumber(inertia, "Y"),
                    InertiaZ = ReadNamedNumber(inertia, "Z")
                };
            })
            .ToList();

    private static List<(int Index, string Value)> ReadStructuredAssignments(string content, string symbol)
    {
        var matches = Regex.Matches(
            content,
            $"(?m)^\\s*{Regex.Escape(symbol)}\\[(?<index>\\d+)\\]\\s*=\\s*\\{{",
            RegexOptions.CultureInvariant);
        var assignments = new List<(int Index, string Value)>();
        foreach (Match match in matches)
        {
            var openBrace = match.Index + match.Length - 1;
            var closeBrace = FindClosingBrace(content, openBrace);
            if (!int.TryParse(
                    match.Groups["index"].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var assignmentIndex))
            {
                throw new InvalidDataException($"{symbol} contains an invalid assignment index.");
            }

            assignments.Add((
                assignmentIndex,
                content[openBrace..(closeBrace + 1)]));
        }

        var ordered = assignments.OrderBy(assignment => assignment.Index).ToList();
        if (ordered.Count == 0
            || ordered.Select(assignment => assignment.Index).Distinct().Count() != ordered.Count
            || !ordered.Select(assignment => assignment.Index).SequenceEqual(Enumerable.Range(1, ordered.Count)))
        {
            throw new InvalidDataException($"{symbol} must contain one-based contiguous, unique assignments.");
        }

        return ordered;
    }

    private static WorkVisualFrameData ReadFrame(int index, string content) =>
        new()
        {
            Index = index,
            X = ReadNamedNumber(content, "X"),
            Y = ReadNamedNumber(content, "Y"),
            Z = ReadNamedNumber(content, "Z"),
            A = ReadNamedNumber(content, "A"),
            B = ReadNamedNumber(content, "B"),
            C = ReadNamedNumber(content, "C")
        };

    private static string ReadNamedStructure(string content, string name)
    {
        var match = Regex.Match(
            content,
            $"(?<![A-Za-z0-9_]){Regex.Escape(name)}\\s*\\{{",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            throw new InvalidDataException($"Required structure {name} was not found.");
        }

        var openBrace = match.Index + match.Length - 1;
        var closeBrace = FindClosingBrace(content, openBrace);
        return content[openBrace..(closeBrace + 1)];
    }

    private static int FindClosingBrace(string content, int openBrace)
    {
        var depth = 0;
        for (var index = openBrace; index < content.Length; index++)
        {
            if (content[index] == '{')
            {
                depth++;
            }
            else if (content[index] == '}' && --depth == 0)
            {
                return index;
            }
        }

        throw new InvalidDataException("Structured KRL assignment has unbalanced braces.");
    }

    private static double ReadNamedNumber(string content, string name)
    {
        var match = Regex.Match(
            content,
            $"(?<![A-Za-z0-9_]){Regex.Escape(name)}\\s+(?<value>[+-]?(?:\\d+(?:\\.\\d*)?|\\.\\d+)(?:[Ee][+-]?\\d+)?)(?=\\s*[,}}])",
            RegexOptions.CultureInvariant);
        if (!match.Success
            || !double.TryParse(
                match.Groups["value"].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value)
            || !double.IsFinite(value))
        {
            throw new InvalidDataException($"Required finite numeric field {name} was not found.");
        }

        return value;
    }

    private static double ReadNumberAssignment(string content, string symbol)
    {
        var match = Regex.Match(
            content,
            $"(?m)^\\s*{Regex.Escape(symbol)}\\s*=\\s*(?<value>[+-]?(?:\\d+(?:\\.\\d*)?|\\.\\d+)(?:[Ee][+-]?\\d+)?)",
            RegexOptions.CultureInvariant);
        if (!match.Success
            || !double.TryParse(
                match.Groups["value"].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw new InvalidDataException($"Required numeric assignment {symbol} was not found.");
        }

        return value;
    }

    internal static void EnsureSeparate(string source, string evidence)
    {
        var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(source))!;
        var evidenceFull = Path.GetFullPath(evidence);
        var sourcePrefix = sourceDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var evidencePrefix = evidenceFull.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (evidenceFull.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase)
            || sourceDirectory.StartsWith(evidencePrefix, StringComparison.OrdinalIgnoreCase)
            || string.Equals(sourceDirectory, evidenceFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Extraction evidence must be disjoint from the source WVS directory.");
        }
    }

    private static EnvironmentFileObservation ObserveFile(string id, string path, bool includeVersion)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            return new EnvironmentFileObservation { Id = id, Path = Path.GetFullPath(path), Exists = false };
        }

        var version = includeVersion ? FileVersionInfo.GetVersionInfo(path) : null;
        return new EnvironmentFileObservation
        {
            Id = id,
            Path = info.FullName,
            Exists = true,
            Bytes = info.Length,
            Sha256 = ComputeSha256(path),
            FileVersion = version?.FileVersion,
            ProductVersion = version?.ProductVersion
        };
    }

    private static void WriteNew(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(true);
    }

    private static WorkVisualRunnerCommandObservation ToObservation(WorkVisualProcessResult result) =>
        new()
        {
            ExitCode = result.ExitCode,
            TimedOut = result.TimedOut,
            CleanupVerified = result.CleanupVerified,
            DurationMilliseconds = Math.Max(0, result.DurationMilliseconds)
        };

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
}

public sealed record WorkVisualProjectExtractionVerificationResult
{
    public string ReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public string PayloadSha256 { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}

public static class WorkVisualProjectExtractionReceiptVerifier
{
    public static WorkVisualProjectExtractionVerificationResult Verify(
        WorkVisualProjectExtractionReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var errors = new List<string>();
        if (ReferenceEquals(receipt.Payload, null))
        {
            return new WorkVisualProjectExtractionVerificationResult
            {
                Succeeded = false,
                PayloadSha256 = receipt.PayloadSha256,
                Errors = ["payload is required"]
            };
        }

        var payload = receipt.Payload;
        var checks = payload.Checks ?? [];
        var evidenceFiles = payload.Files ?? [];
        var unsupportedGaps = payload.UnsupportedGaps ?? [];
        var canonical = ReceiptSerialization.ComputeCanonicalSha256(payload);
        if (receipt.SchemaIdentity != WorkVisualProjectExtractionContract.ReceiptSchemaIdentity
            || receipt.SchemaVersion != WorkVisualProjectExtractionContract.ReceiptSchemaVersion)
        {
            errors.Add("schema identity/version is unsupported");
        }

        if (!string.Equals(canonical, receipt.PayloadSha256, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("payload SHA-256 does not match canonical payload");
        }

        if (string.IsNullOrWhiteSpace(payload.ReceiptId)
            || string.IsNullOrWhiteSpace(payload.AttemptId)
            || !string.Equals(
                payload.ReceiptId,
                $"workvisual-project-extraction-{payload.AttemptId}",
                StringComparison.Ordinal)
            || !IsSha(payload.CoreAssemblySha256)
            || payload.Runtime is null
            || string.IsNullOrWhiteSpace(payload.Runtime.OsDescription)
            || string.IsNullOrWhiteSpace(payload.Runtime.FrameworkDescription)
            || string.IsNullOrWhiteSpace(payload.Runtime.ProcessArchitecture)
            || payload.StartedAtUtc > payload.CompletedAtUtc
            || payload.DurationMilliseconds < 0)
        {
            errors.Add("receipt, attempt, core, runtime or timing identity is invalid");
        }

        var intakeReceipt = payload.ProjectIntakeReceipt;
        if (intakeReceipt is null
            || !WorkVisualProjectIntakeReceiptVerifier.VerifyIntegrity(intakeReceipt).Succeeded
            || !string.Equals(
                payload.ProjectIntakeReceiptSha256,
                ReceiptSerialization.ComputeCanonicalSha256(intakeReceipt),
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("nested project-intake receipt is invalid or not hash-bound");
        }


        if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready
            && (!payload.ProjectFile.Exists
                || intakeReceipt is null
                || payload.ProjectFile.Bytes != intakeReceipt.Payload.ProjectFile.Bytes
                || !string.Equals(
                    payload.ProjectFile.Sha256,
                    intakeReceipt.Payload.ProjectFile.Sha256,
                    StringComparison.OrdinalIgnoreCase)
                || !payload.ExtractorFile.Exists
                || !IsSha(payload.ExtractorFile.Sha256 ?? string.Empty)))
        {
            errors.Add("source WVS or ProjectExtractor identity is incomplete or not bound to intake");
        }

        if (payload.ControllerAccessed
            || payload.NetworkTrafficSent
            || payload.CredentialsUsed
            || payload.SourceProjectChanged
            || payload.ControllerConfigurationChanged
            || payload.NativeKssStatus != NativeKssStatus.NotRun)
        {
            errors.Add("local extraction safety claims are invalid");
        }

        if (!unsupportedGaps.SequenceEqual(
                WorkVisualProjectExtractionContract.RequiredUnsupportedGaps,
                StringComparer.Ordinal))
        {
            errors.Add("unsupported gaps do not preserve the exact boundary");
        }

        var checkIds = checks.Select(check => check.Id).ToArray();
        if (checkIds.Distinct(StringComparer.Ordinal).Count() != checkIds.Length
            || checkIds.Any(id => !WorkVisualProjectExtractionContract.RequiredCheckIds.Contains(id, StringComparer.Ordinal))
            || (payload.TerminalClassification == EnvironmentTerminalClassification.Ready
                && !WorkVisualProjectExtractionContract.RequiredCheckIds.All(id => checkIds.Contains(id, StringComparer.Ordinal))))
        {
            errors.Add("extraction checks are duplicated, unknown or incomplete for a Ready receipt");
        }

        if (payload.TerminalClassification is not (
                EnvironmentTerminalClassification.Ready or EnvironmentTerminalClassification.Failed)
            || (payload.TerminalClassification == EnvironmentTerminalClassification.Ready
                && checks.Any(check => check.Status != EnvironmentCheckStatus.Passed))
            || (payload.TerminalClassification == EnvironmentTerminalClassification.Failed
                && !checks.Any(check => check.Status == EnvironmentCheckStatus.Failed)))
        {
            errors.Add("terminal classification does not match the extraction checks");
        }


        if (payload.Files is null
            || payload.SideEffects is null
            || evidenceFiles.Select(file => file.Id).Distinct(StringComparer.Ordinal).Count() != evidenceFiles.Count)
        {
            errors.Add("evidence files and side effects must be non-null with unique file IDs");
        }

        var requiredFileIds = new[]
        {
            "source-wvs",
            "project-extractor",
            "malformed-wvs",
            "extractor-positive-stdout",
            "extractor-positive-stderr",
            "extractor-negative-stdout",
            "extractor-negative-stderr",
            "extracted-machine-dat",
            "extracted-robcor-dat",
            "extracted-config-dat",
            "extracted-cabinet-control-xml"
        };
        if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready
            && requiredFileIds.Any(id => !evidenceFiles.Any(file => file.Id == id && file.Exists)))
        {
            errors.Add("Ready receipt lacks required source, extractor, negative-control, raw-output or MADA evidence files");
        }

        var malformed = evidenceFiles.FirstOrDefault(file => file.Id == "malformed-wvs");
        if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready
            && (malformed is null
                || malformed.Bytes != Encoding.UTF8.GetByteCount(WorkVisualProjectExtractionContract.MalformedFixtureText)
                || !string.Equals(
                    malformed.Sha256,
                    WorkVisualProjectExtractionContract.MalformedFixtureSha256,
                    StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("malformed negative-control fixture is not pinned to the contract");
        }

        if (payload.TerminalClassification == EnvironmentTerminalClassification.Ready)
        {
            var profile = payload.ControllerProfile;
            var axisLimits = profile?.AxisLimits ?? [];
            var toolData = profile?.ToolData ?? [];
            var baseData = profile?.BaseData ?? [];
            var loadData = profile?.LoadData ?? [];
            var technologyPackageDirectories = profile?.TechnologyPackageDirectories ?? [];
            if (!payload.ProjectExtracted
                || !payload.ProjectContentsInspected
                || !payload.EnvironmentReusable
                || payload.PositiveCommand.ExitCode != 0
                || payload.PositiveCommand.TimedOut
                || !payload.PositiveCommand.CleanupVerified
                || payload.NegativeCommand.ExitCode == 0
                || payload.NegativeCommand.TimedOut
                || !payload.NegativeCommand.CleanupVerified
                || payload.ExtractedTree.FileCount <= 0
                || payload.ExtractedTree.TotalBytes <= 0
                || !IsSha(payload.ExtractedTree.TreeSha256)
                || profile is null
                || !profile.RobotIdentityConsistent
                || string.IsNullOrWhiteSpace(profile.TrafoName)
                || !string.Equals(profile.TrafoName, profile.ModelName, StringComparison.Ordinal)
                || profile.AxisMadaFileCount != 6
                || profile.ToolDataCount <= 0
                || profile.BaseDataCount <= 0
                || profile.LoadDataCount <= 0
                || !ValidFrames(toolData, profile.ToolDataCount)
                || !ValidFrames(baseData, profile.BaseDataCount)
                || !ValidLoads(loadData, profile.LoadDataCount)
                || string.IsNullOrWhiteSpace(profile.ControllerSoftwareFamily)
                || string.IsNullOrWhiteSpace(profile.CabinetKind)
                || axisLimits.Count != 6
                || !axisLimits.Select(limit => limit.AxisNumber).SequenceEqual(Enumerable.Range(1, 6))
                || axisLimits.Any(limit => !double.IsFinite(limit.Negative)
                    || !double.IsFinite(limit.Positive)
                    || limit.Negative >= limit.Positive
                    || limit.Unit != "deg")
                || profile.TechnologyPackageSemanticsQualified
                || !technologyPackageDirectories.SequenceEqual(
                    technologyPackageDirectories.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal)
                || technologyPackageDirectories.Distinct(StringComparer.Ordinal).Count()
                    != technologyPackageDirectories.Count
                || technologyPackageDirectories.Any(name => string.IsNullOrWhiteSpace(name)
                    || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                    || name is "." or "..")
                || checks.Any(check => check.Status != EnvironmentCheckStatus.Passed))
            {
                errors.Add("Ready receipt lacks accepted extraction, negative-control, profile or cleanup evidence");
            }

            if (WorkVisualProjectExtractionRunner.CaptureTree(payload.ExtractedTree.RootPath) != payload.ExtractedTree)
            {
                errors.Add("extracted tree drifted");
            }

            try
            {
                var reparsed = WorkVisualProjectExtractionRunner.ParseProfile(payload.ExtractedTree.RootPath);
                if (profile is null || !string.Equals(
                        ReceiptSerialization.ComputeCanonicalSha256(reparsed),
                        ReceiptSerialization.ComputeCanonicalSha256(profile),
                        StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("controller profile does not match the current extracted evidence files");
                }
            }
            catch (Exception exception) when (exception is IOException
                or InvalidDataException
                or UnauthorizedAccessException
                or XmlException)
            {
                errors.Add($"controller profile could not be reparsed: {exception.Message}");
            }
        }

        foreach (var file in evidenceFiles.Where(file => file.Exists))
        {
            if (!File.Exists(file.Path))
            {
                errors.Add($"evidence file is missing: {file.Id}");
                continue;
            }

            var info = new FileInfo(file.Path);
            using var stream = File.OpenRead(file.Path);
            var hash = Convert.ToHexString(SHA256.HashData(stream));
            if (info.Length != file.Bytes || !string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"evidence file drifted: {file.Id}");
            }
        }

        return new WorkVisualProjectExtractionVerificationResult
        {
            ReceiptId = payload.ReceiptId,
            Succeeded = errors.Count == 0,
            PayloadSha256 = canonical,
            Errors = errors
        };
    }

    private static bool ValidFrames(IReadOnlyList<WorkVisualFrameData> values, int expectedCount) =>
        values.Count == expectedCount
        && values.Select(value => value.Index).SequenceEqual(Enumerable.Range(1, expectedCount))
        && values.All(FiniteFrame);

    private static bool ValidLoads(IReadOnlyList<WorkVisualLoadData> values, int expectedCount) =>
        values.Count == expectedCount
        && values.Select(value => value.Index).SequenceEqual(Enumerable.Range(1, expectedCount))
        && values.All(value => value.CenterOfMass is not null
            && value.CenterOfMass.Index == value.Index
            && double.IsFinite(value.Mass)
            && FiniteFrame(value.CenterOfMass)
            && double.IsFinite(value.InertiaX)
            && double.IsFinite(value.InertiaY)
            && double.IsFinite(value.InertiaZ));

    private static bool FiniteFrame(WorkVisualFrameData value) =>
        double.IsFinite(value.X)
        && double.IsFinite(value.Y)
        && double.IsFinite(value.Z)
        && double.IsFinite(value.A)
        && double.IsFinite(value.B)
        && double.IsFinite(value.C);

    private static bool IsSha(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
}

public static class WorkVisualProjectExtractionReceiptWriter
{
    public static string WriteNew(
        string outputPath,
        string sourceProjectPath,
        WorkVisualProjectExtractionReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceProjectPath);
        ArgumentNullException.ThrowIfNull(receipt);
        var verification = WorkVisualProjectExtractionReceiptVerifier.Verify(receipt);
        if (!verification.Succeeded)
        {
            throw new InvalidOperationException("WorkVisual project-extraction receipt integrity is invalid.");
        }

        var fullPath = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Receipt output must use the .json extension.", nameof(outputPath));
        }

        WorkVisualProjectExtractionRunner.EnsureSeparate(sourceProjectPath, fullPath);
        var current = WorkVisualProjectIntakeReceiptVerifier.VerifyCurrentProject(
            receipt.Payload.ProjectIntakeReceipt,
            sourceProjectPath);
        if (!current.Succeeded)
        {
            throw new InvalidOperationException("The WVS source changed after extraction and before receipt creation.");
        }

        var expectedSideEffect = $"CreateNewReceiptFile:{fullPath}";
        if (!receipt.Payload.SideEffects.Contains(expectedSideEffect, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Receipt output does not match the recorded create-new side effect.");
        }

        var parent = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Receipt output has no parent directory.", nameof(outputPath));
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
