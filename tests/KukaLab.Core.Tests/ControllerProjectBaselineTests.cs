using System.IO.Compression;
using System.Text;
using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class ControllerProjectBaselineTests
{
    [Fact]
    public void Ready_baseline_preserves_all_bytes_and_selects_exact_C01_component()
    {
        using var fixture = BaselineFixture.Create();

        var outcome = fixture.Run("test-controller-project-baseline-ready");

        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.True(outcome.Receipt.Payload.VaultCreated);
        Assert.Equal("#KR210R2700_2 C01 FLR", outcome.Receipt.Payload.Baseline!.ControllerProfile.ModelName);
        Assert.Equal(6, outcome.Receipt.Payload.Baseline.Axes.Count);
        Assert.Equal("192.0.2.147", outcome.Receipt.Payload.Baseline.Network.KliAddress);
        Assert.Equal(24, outcome.Receipt.Payload.Baseline.Network.KliPrefixLength);
        Assert.Equal(0, outcome.Receipt.Payload.Baseline.Io.AnalogInputSignalCount);
        Assert.Equal(0, outcome.Receipt.Payload.Baseline.Io.AnalogOutputSignalCount);
        Assert.True(outcome.Receipt.Payload.Baseline.KukaSimComponentComparison.ExactComponentPreferred);
        Assert.Equal(0, outcome.Receipt.Payload.Baseline.KukaSimComponentComparison.DifferingControllerPayloadEntryCount);
        Assert.Equal(["6.0.31_Build3247"], outcome.Receipt.Payload.Baseline.WorkVisualGeneratorVersions);
        Assert.Single(outcome.Receipt.Payload.Baseline.SafetyArtifacts);
        Assert.All(outcome.Receipt.Payload.Baseline.SafetyArtifacts, artifact => Assert.False(artifact.ContentInterpreted));
        Assert.Equal(
            fixture.ExtractionReceipt.Payload.ExtractedTree.TreeSha256,
            outcome.Receipt.Payload.VaultExtractedTree.TreeSha256);
        var verification = ControllerProjectBaselineReceiptVerifier.Verify(outcome.Receipt);
        Assert.True(verification.Succeeded, string.Join("; ", verification.Errors));
    }

    [Fact]
    public void Ready_baseline_accepts_zero_option_project_without_profinet_files()
    {
        using var fixture = BaselineFixture.Create(includeProfinet: false);

        var outcome = fixture.Run("test-controller-project-baseline-zero-option");

        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.False(outcome.Receipt.Payload.Baseline!.Io.ProfinetDriverActive);
        Assert.Equal(string.Empty, outcome.Receipt.Payload.Baseline.Io.ProfinetDeviceName);
        Assert.Equal(0, outcome.Receipt.Payload.Baseline.Io.ProfinetApplicationRelationCount);
        Assert.DoesNotContain(outcome.Receipt.Payload.Baseline.Modules, module => module.Name == "PNIODriver.o");
        var verification = ControllerProjectBaselineReceiptVerifier.Verify(outcome.Receipt);
        Assert.True(verification.Succeeded, string.Join("; ", verification.Errors));
    }

    [Fact]
    public void Ready_baseline_accepts_project_without_optional_io_descriptor_files()
    {
        using var fixture = BaselineFixture.Create(includeIoDescriptors: false);

        var outcome = fixture.Run("test-controller-project-baseline-no-io-descriptors");

        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(0, outcome.Receipt.Payload.Baseline!.Io.ProcessDataSegmentCount);
        Assert.Equal(0, outcome.Receipt.Payload.Baseline.Io.DigitalInputSignalCount);
        Assert.Equal(0, outcome.Receipt.Payload.Baseline.Io.DigitalOutputSignalCount);
        var verification = ControllerProjectBaselineReceiptVerifier.Verify(outcome.Receipt);
        Assert.True(verification.Succeeded, string.Join("; ", verification.Errors));
    }

    [Fact]
    public void Ready_baseline_accepts_410_model_only_exact_component_and_records_payload_difference()
    {
        using var fixture = BaselineFixture.Create(modelOnlyExactComponent: true);

        var outcome = fixture.Run("test-controller-project-baseline-model-only-exact");

        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.True(outcome.Receipt.Payload.Baseline!.KukaSimComponentComparison.ExactC01NameMatch);
        Assert.True(outcome.Receipt.Payload.Baseline.KukaSimComponentComparison.ExactComponentPreferred);
        Assert.True(outcome.Receipt.Payload.Baseline.KukaSimComponentComparison.DifferingControllerPayloadEntryCount > 0);
        var verification = ControllerProjectBaselineReceiptVerifier.Verify(outcome.Receipt);
        Assert.True(verification.Succeeded, string.Join("; ", verification.Errors));
    }

    [Fact]
    public void Generic_component_cannot_be_promoted_as_the_exact_C01_component()
    {
        using var fixture = BaselineFixture.Create();

        var outcome = new ControllerProjectBaselineRunner().Run(
            fixture.Request with
            {
                VaultDirectory = fixture.VaultPath + "-negative",
                ExactKukaSimComponentPath = fixture.GenericComponentPath,
                GenericKukaSimComponentPath = fixture.ExactComponentPath
            },
            "test-controller-project-baseline-generic-negative");

        Assert.Equal(2, outcome.ExitCode);
        Assert.False(outcome.Receipt.Payload.VaultCreated);
        Assert.False(Directory.Exists(fixture.VaultPath + "-negative"));
        Assert.Contains(
            outcome.Receipt.Payload.Checks,
            check => check.Id == "kukasim-component-selection" && check.Status == EnvironmentCheckStatus.Failed);
        Assert.True(ControllerProjectBaselineReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Rehashed_summary_tampering_is_rejected_by_current_evidence_reparse()
    {
        using var fixture = BaselineFixture.Create();
        var receipt = fixture.Run("test-controller-project-baseline-tamper").Receipt;
        var tamperedBaseline = receipt.Payload.Baseline! with
        {
            Network = receipt.Payload.Baseline!.Network with { KliAddress = "192.0.2.99" }
        };
        var payload = receipt.Payload with { Baseline = tamperedBaseline };
        var tampered = receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var verification = ControllerProjectBaselineReceiptVerifier.Verify(tampered);

        Assert.False(verification.Succeeded);
        Assert.Contains(verification.Errors, error => error.Contains("does not match", StringComparison.Ordinal));
    }

    private sealed class BaselineFixture : IDisposable
    {
        private BaselineFixture(
            string root,
            bool includeProfinet,
            bool modelOnlyExactComponent,
            bool includeIoDescriptors)
        {
            Root = root;
            ProjectPath = Path.Combine(root, "source", "downloaded.wvs");
            ExtractorPath = Path.Combine(root, "tools", "ProjectExtractor.exe");
            ExactComponentPath = Path.Combine(root, "components", "KR 210 R2700-2 C01.vcmx");
            GenericComponentPath = Path.Combine(root, "components", "KR_210_R2700-2.vcmx");
            VaultPath = Path.Combine(root, "vault", "baseline");
            Directory.CreateDirectory(Path.GetDirectoryName(ProjectPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(ExtractorPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(ExactComponentPath)!);
            File.WriteAllBytes(ProjectPath, [0x57, 0x56, 0x53, 0x01, 0x02, 0x03]);
            File.WriteAllText(ExtractorPath, "synthetic ProjectExtractor", Encoding.UTF8);
            CreateComponent(
                ExactComponentPath,
                "KR 210 R2700-2 C01",
                "4.6.0.58",
                includeLogo: true,
                includeControllerPayload: !modelOnlyExactComponent);
            CreateComponent(GenericComponentPath, "KR 210 R2700-2", "3.1.1.58", includeLogo: false);
            var intake = new WorkVisualProjectIntakeRunner().Run(
                new WorkVisualProjectIntakeRequest
                {
                    ProjectPath = ProjectPath,
                    ReceiptOutputPath = Path.Combine(root, "intake", "receipt.json"),
                    CaptureReference = "test-controller-project"
                },
                "test-controller-project-baseline-intake").Receipt;
            ExtractionReceipt = new WorkVisualProjectExtractionRunner(
                new FakeExtractorPlatform(includeProfinet, includeIoDescriptors),
                TimeProvider.System).Run(
                new WorkVisualProjectExtractionRequest
                {
                    ProjectPath = ProjectPath,
                    ProjectIntakeReceipt = intake,
                    ExtractorPath = ExtractorPath,
                    EvidenceDirectory = Path.Combine(root, "extraction", "evidence"),
                    TimeoutSeconds = 10
                },
                "test-controller-project-baseline-extraction").Receipt;
            Request = new ControllerProjectBaselineRequest
            {
                ProjectPath = ProjectPath,
                ExtractionReceipt = ExtractionReceipt,
                VaultDirectory = VaultPath,
                ExactKukaSimComponentPath = ExactComponentPath,
                GenericKukaSimComponentPath = GenericComponentPath
            };
        }

        public string Root { get; }

        public string ProjectPath { get; }

        public string ExtractorPath { get; }

        public string ExactComponentPath { get; }

        public string GenericComponentPath { get; }

        public string VaultPath { get; }

        public WorkVisualProjectExtractionReceipt ExtractionReceipt { get; }

        public ControllerProjectBaselineRequest Request { get; }

        public static BaselineFixture Create(
            bool includeProfinet = true,
            bool modelOnlyExactComponent = false,
            bool includeIoDescriptors = true) =>
            new(
                Path.Combine(Path.GetTempPath(), $"kuka-lab-controller-project-baseline-{Guid.NewGuid():N}"),
                includeProfinet,
                modelOnlyExactComponent,
                includeIoDescriptors);

        public ControllerProjectBaselineOutcome Run(string attemptId) =>
            new ControllerProjectBaselineRunner().Run(Request, attemptId);

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }

        private static void CreateComponent(
            string path,
            string name,
            string revision,
            bool includeLogo,
            bool includeControllerPayload = true)
        {
            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            WriteEntry(
                archive,
                "model.xml",
                $"<VcModel xmlns=\"http://schemas.visualcomponents.com/2017/01/component/componentxml\"><Properties><Property name=\"Name\">{name}</Property><Property name=\"DetailedRevision\">{revision}</Property></Properties></VcModel>");
            WriteEntry(archive, "component.rsc", name);
            if (includeControllerPayload)
            {
                WriteEntry(archive, "packfolder/__rsimrrsrobotcontroller_001/config/controller.xml", "same-controller-payload");
            }
            if (includeLogo) WriteEntry(archive, "logo.tga", "logo");
        }

        private static void WriteEntry(ZipArchive archive, string name, string content)
        {
            var entry = archive.CreateEntry(name);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }
    }

    private sealed class FakeExtractorPlatform(bool includeProfinet, bool includeIoDescriptors) : IWorkVisualRunnerPlatform
    {
        public WorkVisualProcessResult Run(
            string executablePath,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TimeSpan timeout)
        {
            var source = Value(arguments, "sourceproject=");
            var target = Value(arguments, "targetdir=");
            var logs = Value(arguments, "logdir=");
            Directory.CreateDirectory(logs);
            if (Path.GetFileName(source).Equals("intentional-malformed.wvs", StringComparison.Ordinal))
            {
                File.WriteAllText(Path.Combine(logs, "ProjectExtractor.log"), "invalid", Encoding.UTF8);
                return new WorkVisualProcessResult(2, false, true, 1, string.Empty, "invalid");
            }

            CreateProject(target, includeProfinet, includeIoDescriptors);
            File.WriteAllText(Path.Combine(logs, "ProjectExtractor.log"), "complete", Encoding.UTF8);
            return new WorkVisualProcessResult(0, false, true, 1, "complete", string.Empty);
        }

        private static string Value(IReadOnlyList<string> arguments, string prefix) =>
            arguments.Single(argument => argument.StartsWith(prefix, StringComparison.Ordinal))[prefix.Length..];

        private static void CreateProject(string root, bool includeProfinet, bool includeIoDescriptors)
        {
            var mada = Path.Combine(root, "KRC", "R1", "Mada");
            var system = Path.Combine(root, "KRC", "R1", "System");
            var program = Path.Combine(root, "KRC", "R1", "Program");
            var common = Path.Combine(root, "Config", "User", "Common");
            var ngAxis = Path.Combine(common, "Mada", "NGAxis");
            Directory.CreateDirectory(mada);
            Directory.CreateDirectory(system);
            Directory.CreateDirectory(program);
            Directory.CreateDirectory(ngAxis);
            Directory.CreateDirectory(Path.Combine(root, "KRC", "R1", "TP", "BrakeTest"));
            var machine = new List<string> { "$TRAFONAME[]=\"#KR210R2700_2 C01 FLR\"" };
            foreach (var index in Enumerable.Range(1, 6))
            {
                machine.Add($"$SOFTN_END[{index}]={-180 - index}.0");
                machine.Add($"$SOFTP_END[{index}]={180 + index}.0");
                machine.Add($"$AXIS_DIR[{index}]=1");
                machine.Add($"$MAMES[{index}]=0.0");
                machine.Add($"$RAT_MOT_AX[{index}]={{N 100,D 1}}");
                machine.Add($"$VEL_AXIS_MA[{index}]=2000.0");
            }

            File.WriteAllText(Path.Combine(mada, "$machine.dat"), string.Join("\r\n", machine), Encoding.Latin1);
            File.WriteAllText(
                Path.Combine(mada, "$robcor.dat"),
                "$MODEL_NAME[]=\"#KR210R2700_2 C01 FLR\"\r\n$DYN_DAT[1]=1.0\r\n$DYN_DAT[2]=2.0\r\n",
                Encoding.Latin1);
            var config = string.Join(
                "\r\n",
                Enumerable.Range(1, 16).Select(index => $"TOOL_DATA[{index}]={{X 0.0,Y 0.0,Z 0.0,A 0.0,B 0.0,C 0.0}}")
                    .Concat(Enumerable.Range(1, 32).Select(index => $"BASE_DATA[{index}]={{X 0.0,Y 0.0,Z 0.0,A 0.0,B 0.0,C 0.0}}"))
                    .Concat(Enumerable.Range(1, 16).Select(index => $"LOAD_DATA[{index}]={{M -1.0,CM {{X 0.0,Y 0.0,Z 0.0,A 0.0,B 0.0,C 0.0}},J {{X 0.0,Y 0.0,Z 0.0}}}}")));
            File.WriteAllText(Path.Combine(system, "$config.dat"), config, Encoding.Latin1);
            File.WriteAllText(
                Path.Combine(common, "CabCtrl.xml"),
                "<!--XML file automatically generated by WorkVisual V6.0.31_Build3247.--><CabCtrl><Version><Info Version=\"KUKA V8.7\" /></Version><CabinetInformation><Cabinet kind=\"KRC5\" /></CabinetInformation></CabCtrl>",
                Encoding.UTF8);
            foreach (var index in Enumerable.Range(1, 6))
            {
                File.WriteAllText(
                    Path.Combine(ngAxis, $"A{index}.xml"),
                    $"<Axis><Machine Name=\"#KR210R2700_2 C01 FLR\"/><AxisData Simulation=\"Off\"/><ToolMotor MotorFile=\"Motor\\M{index}.xml\" ServoFile=\"ServoFile\\S{index}.xml\"/><Mastering Type=\"EMD\"/></Axis>",
                    Encoding.UTF8);
            }

            File.WriteAllText(Path.Combine(common, "KLIConfig.xml"), NetworkXml("KLI", true, "192.0.2.147", "ffffff00", "<NATRule>rdr x port 49003 -&gt; y</NATRule>"), Encoding.UTF8);
            File.WriteAllText(Path.Combine(common, "KONIConfig.xml"), NetworkXml("KONI", false, "203.0.113.1.147", "ffff0000", string.Empty), Encoding.UTF8);
            if (includeIoDescriptors)
            {
                File.WriteAllText(Path.Combine(common, "KRC_IO.xml"), "<Application><ProcessData><Segment/></ProcessData><Mapping><Input><Digital/><Analog/></Input><Output><Digital/><Analog/></Output></Mapping></Application>", Encoding.UTF8);
                File.WriteAllText(Path.Combine(common, "KrcIoSignals.xml"), "<KrcIoSignals><Signal type=\"$IN\"/><Signal type=\"$OUT\"/></KrcIoSignals>", Encoding.UTF8);
            }
            if (includeProfinet)
            {
                File.WriteAllText(Path.Combine(common, "PNIODriver.xml"), "<Driver><PROFINET PNIODeviceName=\"krc5\"/></Driver>", Encoding.UTF8);
                File.WriteAllText(Path.Combine(common, "IPPNIO.xml"), "<IPPNIO><ARs/></IPPNIO>", Encoding.UTF8);
                File.WriteAllText(Path.Combine(common, "Modules_PNIODriver.xml"), "<ModuleList><Module isActive=\"true\" Name=\"PNIODriver.o\" DisplayName=\"PNIODriver\" Config=\"PNIODriver.xml\"/></ModuleList>", Encoding.UTF8);
            }
            File.WriteAllText(Path.Combine(common, "SAFEDEV.SAF"), "opaque-safety-artifact", Encoding.UTF8);
            File.WriteAllText(Path.Combine(program, "MAIN.src"), "DEF MAIN()\r\nPTP XHOME\r\nLIN XP1\r\nEND\r\n", Encoding.Latin1);
            File.WriteAllText(Path.Combine(program, "MAIN.dat"), "DEFDAT MAIN\r\nENDDAT\r\n", Encoding.Latin1);
            File.WriteAllText(Path.Combine(root, "KRC", "R1", "cell.src"), "DEF CELL()\r\nEND\r\n", Encoding.Latin1);
            File.WriteAllText(Path.Combine(root, "KRC", "R1", "TP", "BrakeTest", "BrakeTest.src"), "DEF BrakeTest()\r\nEND\r\n", Encoding.Latin1);
        }

        private static string NetworkXml(string name, bool active, string ip, string mask, string nat) =>
            $"<KagaConfig><BusConfig isActive=\"{active.ToString().ToLowerInvariant()}\"><VirtualNetworkDevice><IfConfig Name=\"{name}\" Ip=\"{ip}\" Netmask=\"{mask}\" IpConfigType=\"IpnetStatic\"/></VirtualNetworkDevice></BusConfig><NAT>{nat}</NAT></KagaConfig>";
    }
}
