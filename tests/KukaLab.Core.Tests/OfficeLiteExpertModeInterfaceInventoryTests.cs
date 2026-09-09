using KukaLab.Core;

namespace KUKARoboter.LogOn.Service
{
    public sealed class LogonService
    {
    }
}

namespace KUKARoboter.KrcSecurity.AuthenticationManager
{
    public interface IAuthenticationManager
    {
        void LogonDefaultUser();

        void LogonAnnouncedUser();

        void LogonWellKnownUser();

        void LogonUser();

        void LogOff();

        void LockSystem();

        void ResetLease();
    }
}

namespace KukaRoboter.Contracts.UserAccess
{
    public interface IUserAccessService
    {
        void RequestSpoc();

        void GetSpocStateInfo();

        void HasSpoc();

        void Connect();

        void Disconnect();

        void StartRemoteOperating();

        void Operate();

        void StopRemoteOperating();

        void Ping();
    }
}

namespace KukaRoboter.Services.Security
{
    public sealed class BasicAuthenticationValidator
    {
        public void Validate()
        {
        }
    }
}

namespace KukaRoboter.Services.Implementation.KrcControllerEnvironment
{
    public sealed class UserAccessLogicKrcController
    {
        public void RequestSpoc() { }

        public void GetSpocStateInfo() { }

        public void HasSpoc() { }

        public void Connect() { }

        public void Disconnect() { }

        public void StartRemoteOperating() { }

        public void Operate() { }

        public void StopRemoteOperating() { }

        public void Ping() { }
    }
}

namespace KukaLab.Core.Tests
{
    public sealed class OfficeLiteExpertModeInterfaceInventoryTests
    {
        [Fact]
        public void Collect_proves_guest_local_transition_and_spoc_only_remote_boundary()
        {
            using var installation = SyntheticInterfaceInstallation.Create();

            var outcome = new OfficeLiteExpertModeInterfaceInventoryCollector().Collect(
                installation.CreateRequest(),
                "expert-interface-test-01");

            Assert.Equal(0, outcome.ExitCode);
            var payload = outcome.Receipt.Payload;
            Assert.Equal(EnvironmentTerminalClassification.Ready, payload.TerminalClassification);
            Assert.True(payload.SmartHmiLogonPluginConfirmed);
            Assert.True(payload.GuestLocalTransitionInterfaceConfirmed);
            Assert.True(payload.RemoteUserAccessSpocOnly);
            Assert.True(payload.EndpointAuthenticationSeparated);
            Assert.False(payload.RemoteExpertModeTransitionAvailable);
            Assert.True(payload.AttendedOwnerActionRequired);
            Assert.Empty(payload.RemoteTransitionMembers);
            Assert.Contains("LogonUser", payload.LocalTransitionMembers);
            Assert.Contains("RequestSpoc", payload.RemoteUserAccessMembers);
            Assert.Contains("Operate", payload.RemoteImplementationMembers);
            Assert.False(payload.NetworkTrafficSent);
            Assert.False(payload.VmStarted);
            Assert.False(payload.ControllerMutationPerformed);
            Assert.False(payload.SensitiveConfigurationRead);
            Assert.False(payload.ProtectedLicenseRead);
            Assert.Equal(NativeKssStatus.NotRun, payload.NativeKssStatus);
            Assert.Empty(payload.SideEffects);

            var verification = OfficeLiteExpertModeInterfaceInventoryReceiptVerifier.Verify(
                outcome.Receipt,
                verifyCurrentFiles: true);
            Assert.True(verification.Succeeded, string.Join(Environment.NewLine, verification.Errors));
            Assert.True(verification.CurrentFilesVerified);
        }

        [Fact]
        public void Missing_required_file_creates_blocked_nonaccepted_receipt()
        {
            using var installation = SyntheticInterfaceInstallation.Create();
            File.Delete(installation.KrcSecurityAssemblyPath);

            var outcome = new OfficeLiteExpertModeInterfaceInventoryCollector().Collect(
                installation.CreateRequest(),
                "expert-interface-test-02");
            var verification = OfficeLiteExpertModeInterfaceInventoryReceiptVerifier.Verify(outcome.Receipt);

            Assert.Equal(3, outcome.ExitCode);
            Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
            Assert.False(verification.Succeeded);
            Assert.Contains(
                verification.Errors,
                error => error.Contains("terminalClassification", StringComparison.Ordinal));
        }

        [Fact]
        public void Verify_rejects_remote_transition_claim_and_payload_tampering()
        {
            using var installation = SyntheticInterfaceInstallation.Create();
            var receipt = new OfficeLiteExpertModeInterfaceInventoryCollector().Collect(
                installation.CreateRequest(),
                "expert-interface-test-03").Receipt;
            var tampered = receipt with
            {
                Payload = receipt.Payload with
                {
                    RemoteTransitionMembers = ["LogonUser"],
                    RemoteExpertModeTransitionAvailable = true,
                    AttendedOwnerActionRequired = false
                }
            };

            var verification = OfficeLiteExpertModeInterfaceInventoryReceiptVerifier.Verify(tampered);

            Assert.False(verification.Succeeded);
            Assert.Contains(
                verification.Errors,
                error => error.Contains("payloadSha256", StringComparison.Ordinal));
            Assert.Contains(
                verification.Errors,
                error => error.Contains("remoteTransitionMembers", StringComparison.Ordinal));
            Assert.Contains(
                verification.Errors,
                error => error.Contains("interface-boundary booleans", StringComparison.Ordinal));
        }

        [Fact]
        public void Verify_current_files_detects_identity_change()
        {
            using var installation = SyntheticInterfaceInstallation.Create();
            var receipt = new OfficeLiteExpertModeInterfaceInventoryCollector().Collect(
                installation.CreateRequest(),
                "expert-interface-test-04").Receipt;
            File.AppendAllText(installation.ServiceHostConfigurationPath, "<!-- changed -->");

            var verification = OfficeLiteExpertModeInterfaceInventoryReceiptVerifier.Verify(
                receipt,
                verifyCurrentFiles: true);

            Assert.False(verification.Succeeded);
            Assert.False(verification.CurrentFilesVerified);
            Assert.Contains(
                verification.Errors,
                error => error.Contains("identity changed", StringComparison.Ordinal));
        }

        [Fact]
        public void Verify_rejects_nonwhitelisted_path_before_current_file_readback()
        {
            using var installation = SyntheticInterfaceInstallation.Create();
            var receipt = new OfficeLiteExpertModeInterfaceInventoryCollector().Collect(
                installation.CreateRequest(),
                "expert-interface-test-05").Receipt;
            var files = receipt.Payload.Files.ToList();
            files[0] = files[0] with
            {
                Path = Path.Combine(installation.Root, "lservrc.dat")
            };
            var payload = receipt.Payload with { Files = files };
            var tampered = receipt with
            {
                Payload = payload,
                PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
            };

            var verification = OfficeLiteExpertModeInterfaceInventoryReceiptVerifier.Verify(
                tampered,
                verifyCurrentFiles: true);

            Assert.False(verification.Succeeded);
            Assert.False(verification.CurrentFilesVerified);
            Assert.Contains(
                verification.Errors,
                error => error.Contains("file-name whitelist", StringComparison.Ordinal));
            Assert.DoesNotContain(
                verification.Errors,
                error => error.Contains("current evidence file is missing", StringComparison.Ordinal));
        }

        private sealed class SyntheticInterfaceInstallation : IDisposable
        {
            private SyntheticInterfaceInstallation(string root)
            {
                Root = root;
                SmartHmiLogonAssemblyPath = Path.Combine(root, "LogOn.dll");
                KrcSecurityAssemblyPath = Path.Combine(root, "KUKARoboter.KrcSecurity.dll");
                UserAccessContractsAssemblyPath = Path.Combine(root, "KukaRoboter.Contracts.dll");
                UserAccessServiceAssemblyPath = Path.Combine(root, "KukaRoboter.Services.dll");
                UserAccessImplementationAssemblyPath = Path.Combine(
                    root,
                    "KukaRoboter.Services.Implementation.dll");
                ServiceHostConfigurationPath = Path.Combine(root, "WorkVisualServiceHost.exe.config");
            }

            public string Root { get; }

            public string SmartHmiLogonAssemblyPath { get; }

            public string KrcSecurityAssemblyPath { get; }

            public string UserAccessContractsAssemblyPath { get; }

            public string UserAccessServiceAssemblyPath { get; }

            public string UserAccessImplementationAssemblyPath { get; }

            public string ServiceHostConfigurationPath { get; }

            public static SyntheticInterfaceInstallation Create()
            {
                var root = Path.Combine(
                    Path.GetTempPath(),
                    $"kuka-lab-expert-interface-{Guid.NewGuid():N}");
                Directory.CreateDirectory(root);
                var installation = new SyntheticInterfaceInstallation(root);
                var sourceAssembly = typeof(OfficeLiteExpertModeInterfaceInventoryTests).Assembly.Location;
                foreach (var target in new[]
                {
                    installation.SmartHmiLogonAssemblyPath,
                    installation.KrcSecurityAssemblyPath,
                    installation.UserAccessContractsAssemblyPath,
                    installation.UserAccessServiceAssemblyPath,
                    installation.UserAccessImplementationAssemblyPath
                })
                {
                    File.Copy(sourceAssembly, target);
                }

                File.WriteAllText(
                    installation.ServiceHostConfigurationPath,
                    """
                    <configuration>
                      <system.serviceModel>
                        <services>
                          <service>
                            <endpoint address="UserAccess" contract="KukaRoboter.Contracts.UserAccess.IUserAccessService" />
                            <endpoint address="UserAccessBasicAuthentication" contract="KukaRoboter.Contracts.UserAccess.IUserAccessService" />
                            <endpoint address="UserAccessCore" contract="KukaRoboter.Contracts.UserAccess.IUserAccessService" />
                          </service>
                        </services>
                      </system.serviceModel>
                    </configuration>
                    """);
                return installation;
            }

            public OfficeLiteExpertModeInterfaceInventoryRequest CreateRequest() => new()
            {
                SmartHmiLogonAssemblyPath = SmartHmiLogonAssemblyPath,
                KrcSecurityAssemblyPath = KrcSecurityAssemblyPath,
                UserAccessContractsAssemblyPath = UserAccessContractsAssemblyPath,
                UserAccessServiceAssemblyPath = UserAccessServiceAssemblyPath,
                UserAccessImplementationAssemblyPath = UserAccessImplementationAssemblyPath,
                WorkVisualServiceHostConfigurationPath = ServiceHostConfigurationPath
            };

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
