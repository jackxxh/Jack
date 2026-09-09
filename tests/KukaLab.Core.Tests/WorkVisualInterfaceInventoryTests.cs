using KukaLab.Core;

namespace Kuka.WorkVisual.Scripting.KrcOnline
{
    public interface IScriptingOnlineController
    {
        void GetProjects();

        void DownloadProject();
    }
}

namespace KukaRoboter.OnlineServicesFacade
{
    public interface IFileHandlingFacade
    {
        void Download();

        void DownloadStream();

        void GetFiles();

        void GetFileInfos();

        void FileExists();

        void Upload();

        void UploadStream();

        void CreateDirectory();

        void DeleteFile();

        void MoveFile();
    }

    public interface IRuntimeInterpreter
    {
        object Mode { get; }

        object State { get; }

        void Select();

        void Deselect();

        void Start();

        void Stop();

        void Reset();

        void SetProgramMode();

        void GetLinkedFiles();
    }

    public interface IRuntimeMessageWindowFacade
    {
        void GetMessages();

        void IsEndpointReachable();
    }

    public sealed class RuntimeManagerFacade
    {
        public void GetErrors() { }

        public void GetModulesWithErrors() { }

        public void GetPosition() { }

        public void Ping() { }
    }

    public sealed class MonitoringFacade
    {
        public void GetLogSources() { }

        public void ReadLogMessages() { }
    }
}

namespace KukaLab.Core.Tests
{
    public sealed class WorkVisualInterfaceInventoryTests
    {
        [Fact]
        public void Collect_confirms_expected_interfaces_without_execution()
        {
            using var installation = SyntheticWorkVisualInstallation.Create();

            var outcome = new WorkVisualInterfaceInventoryCollector().Collect(
                new WorkVisualInterfaceInventoryRequest { InstallRoot = installation.Root },
                "workvisual-interface-test-01");

            Assert.Equal(0, outcome.ExitCode);
            var payload = outcome.Receipt.Payload;
            Assert.Equal(EnvironmentTerminalClassification.Ready, payload.TerminalClassification);
            Assert.Equal(9, payload.Capabilities.Count);
            Assert.All(payload.Capabilities, capability =>
            {
                Assert.NotEqual(WorkVisualInterfaceEvidenceStatus.Missing, capability.Status);
                Assert.False(capability.Executed);
            });
            Assert.False(payload.NetworkTrafficSent);
            Assert.False(payload.ControllerMutationPerformed);
            Assert.Equal(NativeKssStatus.NotRun, payload.NativeKssStatus);
            Assert.Empty(payload.SideEffects);
            Assert.Contains(
                payload.Capabilities,
                capability => capability.Id == "controller-repository-write"
                    && capability.Effect == WorkVisualOperationEffect.ControllerWrite
                    && !capability.Executed);
            var programControl = Assert.Single(
                payload.Capabilities,
                capability => capability.Id == "program-control");
            Assert.Contains("SetProgramMode", programControl.ObservedMembers);
            Assert.Contains("Mode", programControl.ObservedMembers);
            Assert.Contains("State", programControl.ObservedMembers);

            var verification = WorkVisualInterfaceInventoryReceiptVerifier.Verify(
                outcome.Receipt,
                verifyCurrentInstall: true);
            Assert.True(verification.Succeeded, string.Join(Environment.NewLine, verification.Errors));
            Assert.True(verification.CurrentInstallVerified);
        }

        [Fact]
        public void Collect_blocks_when_required_ui_command_is_missing()
        {
            using var installation = SyntheticWorkVisualInstallation.Create();
            File.WriteAllText(
                Path.Combine(installation.Root, "wvsr.exe.WorkOnlineWorkVisual.config"),
                SyntheticWorkVisualInstallation.Configuration.Replace(
                    "KukaRoboter.ResetProgram",
                    "KukaRoboter.RebootThing",
                    StringComparison.Ordinal));

            var outcome = new WorkVisualInterfaceInventoryCollector().Collect(
                new WorkVisualInterfaceInventoryRequest { InstallRoot = installation.Root },
                "workvisual-interface-test-02");

            Assert.Equal(3, outcome.ExitCode);
            Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
            var capability = Assert.Single(
                outcome.Receipt.Payload.Capabilities,
                item => item.Id == "workonline-ui-commands");
            Assert.Equal(WorkVisualInterfaceEvidenceStatus.Missing, capability.Status);
            Assert.False(capability.Executed);
        }

        [Fact]
        public void Missing_install_root_creates_a_verifiable_blocked_receipt()
        {
            var missingRoot = Path.Combine(
                Path.GetTempPath(),
                $"kuka-lab-workvisual-interface-missing-{Guid.NewGuid():N}");

            var outcome = new WorkVisualInterfaceInventoryCollector().Collect(
                new WorkVisualInterfaceInventoryRequest { InstallRoot = missingRoot },
                "workvisual-interface-test-missing");
            var verification = WorkVisualInterfaceInventoryReceiptVerifier.Verify(outcome.Receipt);

            Assert.Equal(3, outcome.ExitCode);
            Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
            Assert.True(verification.Succeeded, string.Join(Environment.NewLine, verification.Errors));
            Assert.False(verification.CurrentInstallVerified);
        }

        [Fact]
        public void Verify_rejects_execution_claim_and_payload_tampering()
        {
            using var installation = SyntheticWorkVisualInstallation.Create();
            var receipt = new WorkVisualInterfaceInventoryCollector().Collect(
                new WorkVisualInterfaceInventoryRequest { InstallRoot = installation.Root },
                "workvisual-interface-test-03").Receipt;
            var capabilities = receipt.Payload.Capabilities.ToList();
            capabilities[0] = capabilities[0] with { Executed = true };
            var tampered = receipt with
            {
                Payload = receipt.Payload with { Capabilities = capabilities }
            };

            var verification = WorkVisualInterfaceInventoryReceiptVerifier.Verify(tampered);

            Assert.False(verification.Succeeded);
            Assert.False(verification.CurrentInstallVerified);
            Assert.Contains(
                verification.Errors,
                error => error.Contains("cannot be marked executed", StringComparison.Ordinal));
            Assert.Contains(
                verification.Errors,
                error => error.Contains("payloadSha256", StringComparison.Ordinal));
        }

        [Fact]
        public void Verify_current_install_detects_file_identity_change()
        {
            using var installation = SyntheticWorkVisualInstallation.Create();
            var receipt = new WorkVisualInterfaceInventoryCollector().Collect(
                new WorkVisualInterfaceInventoryRequest { InstallRoot = installation.Root },
                "workvisual-interface-test-04").Receipt;
            File.AppendAllText(
                Path.Combine(installation.Root, "Kuka.WorkVisual.Scripting.KrcOnline.xml"),
                "<!-- changed -->");

            var verification = WorkVisualInterfaceInventoryReceiptVerifier.Verify(
                receipt,
                verifyCurrentInstall: true);

            Assert.False(verification.Succeeded);
            Assert.False(verification.CurrentInstallVerified);
            Assert.Contains(
                verification.Errors,
                error => error.Contains("identity changed", StringComparison.Ordinal));
        }

        private sealed class SyntheticWorkVisualInstallation : IDisposable
        {
            public const string Configuration = """
                <Configuration>
                  <Parameter Name="FileExtensions" Value="KRC: .src .dat .sub" />
                  <SupportedKrcVersion KrcType="KR C" Versions="[8.5;" />
                  <CommandInfo SystemName="KukaRoboter.UploadRepositoryItems" />
                  <CommandInfo SystemName="KukaRoboter.DownloadRepositoryItems" />
                  <CommandInfo SystemName="KukaRoboter.SelectProgram" />
                  <CommandInfo SystemName="KukaRoboter.DeselectProgram" />
                  <CommandInfo SystemName="KukaRoboter.StartProgram" />
                  <CommandInfo SystemName="KukaRoboter.StopProgram" />
                  <CommandInfo SystemName="KukaRoboter.ResetProgram" />
                </Configuration>
                """;

            private SyntheticWorkVisualInstallation(string root)
            {
                Root = root;
            }

            public string Root { get; }

            public static SyntheticWorkVisualInstallation Create()
            {
                var root = Path.Combine(
                    Path.GetTempPath(),
                    $"kuka-lab-workvisual-interface-{Guid.NewGuid():N}");
                Directory.CreateDirectory(root);
                var assembly = typeof(WorkVisualInterfaceInventoryTests).Assembly.Location;
                foreach (var file in new[]
                {
                    "wvsr.exe",
                    "Kuka.WorkVisual.Scripting.KrcOnline.dll",
                    "KukaRoboter.OnlineServicesFacade.dll",
                    "KukaRoboter.Contracts.dll"
                })
                {
                    File.Copy(assembly, Path.Combine(root, file));
                }

                File.WriteAllText(
                    Path.Combine(root, "Kuka.WorkVisual.Scripting.KrcOnline.xml"),
                    "<doc><members>"
                    + "<member name=\"M:Kuka.WorkVisual.Scripting.KrcOnline.IScriptingOnlineController.GetProjects\" />"
                    + "<member name=\"M:Kuka.WorkVisual.Scripting.KrcOnline.IScriptingOnlineController.DownloadProject\" />"
                    + "</members></doc>");
                File.WriteAllText(
                    Path.Combine(root, "wvsr.exe.WorkOnlineWorkVisual.config"),
                    Configuration);
                return new SyntheticWorkVisualInstallation(root);
            }

            public void Dispose()
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
        }
    }
}
