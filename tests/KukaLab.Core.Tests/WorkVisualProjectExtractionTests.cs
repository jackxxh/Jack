using System.Text;
using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class WorkVisualProjectExtractionTests
{
    [Fact]
    public void Ready_extraction_requires_real_profile_shape_negative_rejection_and_roundtrip_receipt()
    {
        using var fixture = ExtractionFixture.Create();
        var platform = new FakeProjectExtractorPlatform();
        var outcome = fixture.Run(platform, "test-project-extraction-ready");

        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(2, platform.Invocations.Count);
        Assert.True(outcome.Receipt.Payload.ProjectExtracted);
        Assert.True(outcome.Receipt.Payload.ProjectContentsInspected);
        Assert.False(outcome.Receipt.Payload.ControllerAccessed);
        Assert.False(outcome.Receipt.Payload.NetworkTrafficSent);
        Assert.False(outcome.Receipt.Payload.CredentialsUsed);
        Assert.False(outcome.Receipt.Payload.SourceProjectChanged);
        Assert.False(outcome.Receipt.Payload.ControllerConfigurationChanged);
        Assert.Equal(NativeKssStatus.NotRun, outcome.Receipt.Payload.NativeKssStatus);
        Assert.Equal("#KR3R540 C4SR", outcome.Receipt.Payload.ControllerProfile.ModelName);
        Assert.Equal(6, outcome.Receipt.Payload.ControllerProfile.AxisMadaFileCount);
        Assert.Equal(16, outcome.Receipt.Payload.ControllerProfile.ToolDataCount);
        Assert.Equal(32, outcome.Receipt.Payload.ControllerProfile.BaseDataCount);
        Assert.Equal(16, outcome.Receipt.Payload.ControllerProfile.LoadDataCount);
        Assert.Equal(16, outcome.Receipt.Payload.ControllerProfile.ToolData.Count);
        Assert.Equal(10.0, outcome.Receipt.Payload.ControllerProfile.ToolData[0].X);
        Assert.Equal(3.0, outcome.Receipt.Payload.ControllerProfile.ToolData[0].C);
        Assert.Equal(32, outcome.Receipt.Payload.ControllerProfile.BaseData.Count);
        Assert.Equal(100.0, outcome.Receipt.Payload.ControllerProfile.BaseData[0].X);
        Assert.Equal(16, outcome.Receipt.Payload.ControllerProfile.LoadData.Count);
        Assert.Equal(5.5, outcome.Receipt.Payload.ControllerProfile.LoadData[0].Mass);
        Assert.Equal(1.0, outcome.Receipt.Payload.ControllerProfile.LoadData[0].CenterOfMass.X);
        Assert.Equal(0.3, outcome.Receipt.Payload.ControllerProfile.LoadData[0].InertiaZ);
        Assert.Equal("KUKA V8.7", outcome.Receipt.Payload.ControllerProfile.ControllerSoftwareFamily);
        Assert.Equal("KRC5_MICRO", outcome.Receipt.Payload.ControllerProfile.CabinetKind);
        Assert.Equal(6, outcome.Receipt.Payload.ControllerProfile.AxisLimits.Count);
        Assert.Equal(-170.0, outcome.Receipt.Payload.ControllerProfile.AxisLimits[0].Negative);
        Assert.Equal(170.0, outcome.Receipt.Payload.ControllerProfile.AxisLimits[0].Positive);
        Assert.Equal(["BrakeTest"], outcome.Receipt.Payload.ControllerProfile.TechnologyPackageDirectories);
        Assert.False(outcome.Receipt.Payload.ControllerProfile.TechnologyPackageSemanticsQualified);
        Assert.Equal(0, outcome.Receipt.Payload.PositiveCommand.ExitCode);
        Assert.NotEqual(0, outcome.Receipt.Payload.NegativeCommand.ExitCode);
        Assert.True(WorkVisualProjectExtractionReceiptVerifier.Verify(outcome.Receipt).Succeeded);

        var payload = outcome.Receipt.Payload with
        {
            SideEffects = outcome.Receipt.Payload.SideEffects
                .Append($"CreateNewReceiptFile:{fixture.OutputPath}")
                .ToList()
        };
        var receipt = outcome.Receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
        var written = WorkVisualProjectExtractionReceiptWriter.WriteNew(
            fixture.OutputPath,
            fixture.ProjectPath,
            receipt);
        var readback = ReceiptSerialization.WorkVisualProjectExtractionFromJson(File.ReadAllText(written));

        Assert.True(WorkVisualProjectExtractionReceiptVerifier.Verify(readback).Succeeded);
        Assert.Throws<IOException>(() => WorkVisualProjectExtractionReceiptWriter.WriteNew(
            fixture.OutputPath,
            fixture.ProjectPath,
            receipt));
    }

    [Fact]
    public void Negative_control_that_extracts_or_exits_zero_fails_closed()
    {
        using var fixture = ExtractionFixture.Create();
        var outcome = fixture.Run(
            new FakeProjectExtractorPlatform { NegativeSucceeds = true },
            "test-project-extraction-negative-control");

        Assert.Equal(2, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "negative-malformed-rejection"
                && check.Status == EnvironmentCheckStatus.Failed);
        Assert.True(WorkVisualProjectExtractionReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Source_drift_is_rejected_before_ProjectExtractor_runs()
    {
        using var fixture = ExtractionFixture.Create();
        File.AppendAllText(fixture.ProjectPath, "drift-after-intake", Encoding.UTF8);
        var platform = new FakeProjectExtractorPlatform();

        var outcome = fixture.Run(platform, "test-project-extraction-source-drift");

        Assert.Equal(2, outcome.ExitCode);
        Assert.Empty(platform.Invocations);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "project-intake" && check.Status == EnvironmentCheckStatus.Failed);
        Assert.True(WorkVisualProjectExtractionReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Profile_mismatch_and_rehashed_safety_tampering_are_rejected()
    {
        using var mismatchFixture = ExtractionFixture.Create();
        var mismatch = mismatchFixture.Run(
            new FakeProjectExtractorPlatform { ProfileMismatch = true },
            "test-project-extraction-profile-mismatch");

        Assert.Equal(EnvironmentTerminalClassification.Failed, mismatch.Receipt.Payload.TerminalClassification);
        Assert.Contains(
            mismatch.Receipt.Payload.Checks,
            check => check.Id == "robot-profile" && check.Status == EnvironmentCheckStatus.Failed);
        Assert.True(WorkVisualProjectExtractionReceiptVerifier.Verify(mismatch.Receipt).Succeeded);

        using var readyFixture = ExtractionFixture.Create();
        var ready = readyFixture.Run(
            new FakeProjectExtractorPlatform(),
            "test-project-extraction-semantic-tamper").Receipt;
        var unsafePayload = ready.Payload with { NetworkTrafficSent = true };
        var unsafeReceipt = ready with
        {
            Payload = unsafePayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(unsafePayload)
        };

        Assert.False(WorkVisualProjectExtractionReceiptVerifier.Verify(unsafeReceipt).Succeeded);

        var tamperedProfile = ready.Payload.ControllerProfile with
        {
            AxisLimits = ready.Payload.ControllerProfile.AxisLimits
                .Select(limit => limit.AxisNumber == 1 ? limit with { Positive = 999.0 } : limit)
                .ToList()
        };
        var tamperedProfilePayload = ready.Payload with { ControllerProfile = tamperedProfile };
        var tamperedProfileReceipt = ready with
        {
            Payload = tamperedProfilePayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(tamperedProfilePayload)
        };
        var profileTamperVerification = WorkVisualProjectExtractionReceiptVerifier.Verify(tamperedProfileReceipt);
        Assert.False(profileTamperVerification.Succeeded);
        Assert.Contains(
            profileTamperVerification.Errors,
            error => error.Contains("does not match", StringComparison.Ordinal));

        var tamperedToolProfile = ready.Payload.ControllerProfile with
        {
            ToolData = ready.Payload.ControllerProfile.ToolData
                .Select(value => value.Index == 1 ? value with { X = 999.0 } : value)
                .ToList()
        };
        var tamperedToolPayload = ready.Payload with { ControllerProfile = tamperedToolProfile };
        var tamperedToolReceipt = ready with
        {
            Payload = tamperedToolPayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(tamperedToolPayload)
        };
        Assert.False(WorkVisualProjectExtractionReceiptVerifier.Verify(tamperedToolReceipt).Succeeded);

        File.WriteAllText(
            Path.Combine(ready.Payload.ExtractedTree.RootPath, "tree-drift.txt"),
            "drift",
            Encoding.UTF8);
        var drift = WorkVisualProjectExtractionReceiptVerifier.Verify(ready);
        Assert.False(drift.Succeeded);
        Assert.Contains(drift.Errors, error => error.Contains("tree drifted", StringComparison.Ordinal));
    }

    [Fact]
    public void Incomplete_controller_profile_fails_before_ready_classification()
    {
        using var fixture = ExtractionFixture.Create();
        var outcome = fixture.Run(
            new FakeProjectExtractorPlatform { IncompleteControllerProfile = true },
            "test-project-extraction-incomplete-controller-profile");

        Assert.Equal(2, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "robot-profile" && check.Status == EnvironmentCheckStatus.Failed);
        Assert.True(WorkVisualProjectExtractionReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Incomplete_tool_frame_fails_before_ready_classification()
    {
        using var fixture = ExtractionFixture.Create();
        var outcome = fixture.Run(
            new FakeProjectExtractorPlatform { IncompleteToolFrame = true },
            "test-project-extraction-incomplete-tool-frame");

        Assert.Equal(2, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "robot-profile" && check.Status == EnvironmentCheckStatus.Failed);
        Assert.True(WorkVisualProjectExtractionReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    private sealed class FakeProjectExtractorPlatform : IWorkVisualRunnerPlatform
    {
        public bool NegativeSucceeds { get; init; }

        public bool ProfileMismatch { get; init; }

        public bool IncompleteControllerProfile { get; init; }

        public bool IncompleteToolFrame { get; init; }

        public List<IReadOnlyList<string>> Invocations { get; } = [];

        public WorkVisualProcessResult Run(
            string executablePath,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TimeSpan timeout)
        {
            Invocations.Add(arguments.ToArray());
            var source = Value(arguments, "sourceproject=");
            var target = Value(arguments, "targetdir=");
            var logs = Value(arguments, "logdir=");
            Directory.CreateDirectory(logs);
            if (Path.GetFileName(source).Equals("intentional-malformed.wvs", StringComparison.Ordinal))
            {
                File.WriteAllText(Path.Combine(logs, "ProjectExtractor.log"), "invalid format", Encoding.UTF8);
                if (NegativeSucceeds)
                {
                    File.WriteAllText(Path.Combine(target, "unexpected.txt"), "incorrectly extracted", Encoding.UTF8);
                    return Result(0, "unexpected success");
                }

                return Result(2, "invalid format");
            }

            CreateProfile(target, ProfileMismatch, IncompleteControllerProfile, IncompleteToolFrame);
            File.WriteAllText(Path.Combine(logs, "ProjectExtractor.log"), "extraction complete", Encoding.UTF8);
            return Result(0, "extraction complete");
        }

        private static string Value(IReadOnlyList<string> arguments, string prefix) =>
            arguments.Single(argument => argument.StartsWith(prefix, StringComparison.Ordinal))[prefix.Length..];

        private static WorkVisualProcessResult Result(int exitCode, string output) =>
            new(exitCode, false, true, 7, output, string.Empty);

        private static void CreateProfile(
            string target,
            bool mismatch,
            bool incompleteControllerProfile,
            bool incompleteToolFrame)
        {
            var mada = Path.Combine(target, "KRC", "R1", "Mada");
            var system = Path.Combine(target, "KRC", "R1", "System");
            var axes = Path.Combine(target, "Config", "User", "Common", "Mada", "NGAxis");
            var common = Path.Combine(target, "Config", "User", "Common");
            var technologyPackage = Path.Combine(target, "KRC", "R1", "TP", "BrakeTest");
            Directory.CreateDirectory(mada);
            Directory.CreateDirectory(system);
            Directory.CreateDirectory(axes);
            Directory.CreateDirectory(common);
            Directory.CreateDirectory(technologyPackage);
            var negativeLimits = new[] { -170.0, -170.0, -110.0, -175.0, -120.0, -350.0 };
            var positiveLimits = new[] { 170.0, 50.0, 155.0, 175.0, 120.0, 350.0 };
            var machine = new List<string> { "$TRAFONAME[]=\"#KR3R540 C4SR\"" };
            machine.AddRange(Enumerable.Range(1, 6)
                .Select(index => $"$SOFTN_END[{index}]={negativeLimits[index - 1].ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
            machine.AddRange(Enumerable.Range(1, incompleteControllerProfile ? 5 : 6)
                .Select(index => $"$SOFTP_END[{index}]={positiveLimits[index - 1].ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
            File.WriteAllText(
                Path.Combine(mada, "$machine.dat"),
                string.Join("\r\n", machine) + "\r\n",
                Encoding.Latin1);
            File.WriteAllText(
                Path.Combine(mada, "$robcor.dat"),
                mismatch
                    ? "$MODEL_NAME[]=\"#DIFFERENT ROBOT\"\r\n"
                    : "$MODEL_NAME[]=\"#KR3R540 C4SR\"\r\n",
                Encoding.Latin1);
            var config = string.Join(
                "\r\n",
                Enumerable.Range(1, 16).Select(index => index == 1
                    ? incompleteToolFrame
                        ? "TOOL_DATA[1]={X 10.0,Y 20.0,Z 30.0,A 1.0,B 2.0}"
                        : "TOOL_DATA[1]={X 10.0,Y 20.0,Z 30.0,A 1.0,B 2.0,C 3.0}"
                    : $"TOOL_DATA[{index}]={{X 0.0,Y 0.0,Z 0.0,A 0.0,B 0.0,C 0.0}}")
                    .Concat(Enumerable.Range(1, 32).Select(index => index == 1
                        ? "BASE_DATA[1]={X 100.0,Y 200.0,Z 300.0,A 10.0,B 20.0,C 30.0}"
                        : $"BASE_DATA[{index}]={{X 0.0,Y 0.0,Z 0.0,A 0.0,B 0.0,C 0.0}}"))
                    .Concat(Enumerable.Range(1, 16).Select(index => index == 1
                        ? "LOAD_DATA[1]={M 5.5,CM {X 1.0,Y 2.0,Z 3.0,A 0.0,B 0.0,C 0.0},J {X 0.1,Y 0.2,Z 0.3}}"
                        : $"LOAD_DATA[{index}]={{M -1.0,CM {{X 0.0,Y 0.0,Z 0.0,A 0.0,B 0.0,C 0.0}},J {{X 0.0,Y 0.0,Z 0.0}}}}")));
            File.WriteAllText(Path.Combine(system, "$config.dat"), config, Encoding.Latin1);
            foreach (var index in Enumerable.Range(1, 6))
            {
                File.WriteAllText(Path.Combine(axes, $"A{index}.xml"), $"<Axis Id=\"A{index}\" />", Encoding.UTF8);
            }

            File.WriteAllText(
                Path.Combine(common, "CabCtrl.xml"),
                "<CabCtrl><Version><Info Version=\"KUKA V8.7\" /></Version><CabinetInformation><Cabinet kind=\"KRC5_MICRO\" /></CabinetInformation></CabCtrl>",
                Encoding.UTF8);
            File.WriteAllText(Path.Combine(technologyPackage, "BrakeTest.src"), "DEF BrakeTest()\r\nEND\r\n", Encoding.Latin1);
        }
    }

    private sealed class ExtractionFixture : IDisposable
    {
        private ExtractionFixture(string root)
        {
            Root = root;
            ProjectPath = Path.Combine(root, "source", "InitialProject.wvs");
            ExtractorPath = Path.Combine(root, "tools", "ProjectExtractor.exe");
            EvidencePath = Path.Combine(root, "extraction", "evidence");
            OutputPath = Path.Combine(root, "receipts", "project-extraction.receipt.json");
            var intakeOutput = Path.Combine(root, "intake-receipt", "project-intake.receipt.json");
            Directory.CreateDirectory(Path.GetDirectoryName(ProjectPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(ExtractorPath)!);
            File.WriteAllBytes(ProjectPath, [0x57, 0x56, 0x53, 0x01, 0x02, 0x03]);
            File.WriteAllText(ExtractorPath, "synthetic ProjectExtractor", Encoding.UTF8);
            IntakeReceipt = new WorkVisualProjectIntakeRunner().Run(
                new WorkVisualProjectIntakeRequest
                {
                    ProjectPath = ProjectPath,
                    ReceiptOutputPath = intakeOutput,
                    CaptureReference = "isolated-office-lite-download"
                },
                "test-project-extraction-intake").Receipt;
        }

        public string Root { get; }

        public string ProjectPath { get; }

        public string ExtractorPath { get; }

        public string EvidencePath { get; }

        public string OutputPath { get; }

        public WorkVisualProjectIntakeReceipt IntakeReceipt { get; }

        public static ExtractionFixture Create() =>
            new(Path.Combine(Path.GetTempPath(), $"kuka-lab-project-extraction-{Guid.NewGuid():N}"));

        public WorkVisualProjectExtractionOutcome Run(
            IWorkVisualRunnerPlatform platform,
            string attemptId) =>
            new WorkVisualProjectExtractionRunner(platform, TimeProvider.System).Run(
                new WorkVisualProjectExtractionRequest
                {
                    ProjectPath = ProjectPath,
                    ProjectIntakeReceipt = IntakeReceipt,
                    ExtractorPath = ExtractorPath,
                    EvidenceDirectory = EvidencePath,
                    TimeoutSeconds = 10
                },
                attemptId);

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
