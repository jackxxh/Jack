using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class KukaSimInstallationDiscoveryTests
{
    [Fact]
    public void Default_resolution_prefers_highest_installed_410_directory_and_exact_component()
    {
        using var fixture = InstallationFixture.Create();
        fixture.AddInstallation("KUKA.Sim 4.10", includeEngine: true);
        var preferred = fixture.AddInstallation("KUKA.Sim 4.10.2", includeEngine: true);
        var component = fixture.AddComponent("KUKA.Sim 4.10", KukaSimInstallationDiscovery.ExactC01ComponentFileName);

        var layout = KukaSimInstallationDiscovery.ResolveFromEngine(
            enginePath: null,
            componentPath: null,
            programFilesRoot: fixture.ProgramFiles,
            commonDocumentsRoot: fixture.CommonDocuments);

        Assert.Equal(preferred, layout.InstallRoot);
        Assert.Equal(Path.Combine(preferred, "VisualComponents.Engine.exe"), layout.EnginePath);
        Assert.Equal(component, layout.ComponentPath);
    }

    [Fact]
    public void Explicit_launcher_anchors_every_runtime_dependency_to_same_installation()
    {
        using var fixture = InstallationFixture.Create();
        var installRoot = fixture.AddInstallation("custom-kukasim", includeEngine: true);
        var launcher = Path.Combine(installRoot, "VisualComponents.Engine.Launcher.exe");
        var component = fixture.AddComponent("KUKA.Sim 4.10", KukaSimInstallationDiscovery.ExactC01ComponentFileName);

        var layout = KukaSimInstallationDiscovery.ResolveFromLauncher(
            launcher,
            component,
            programFilesRoot: fixture.ProgramFiles,
            commonDocumentsRoot: fixture.CommonDocuments);

        Assert.Equal(installRoot, layout.InstallRoot);
        Assert.Equal(Path.Combine(installRoot, "VisualComponents.Engine.exe"), layout.EnginePath);
        Assert.Equal(Path.Combine(installRoot, "scriptStarter.dll"), layout.ScriptStarterPath);
        Assert.Equal(
            Path.Combine(installRoot, KukaSimInstallationDiscovery.BootstrapPluginFileName),
            layout.BootstrapPluginPath);
        Assert.Equal(Path.Combine(installRoot, "Create3D.Shared.dll"), layout.Create3DSharedPath);
    }

    [Fact]
    public void Multiple_exact_410_components_are_rejected_as_ambiguous()
    {
        using var fixture = InstallationFixture.Create();
        fixture.AddInstallation("KUKA.Sim 4.10", includeEngine: true);
        fixture.AddComponent("KUKA.Sim 4.10", KukaSimInstallationDiscovery.ExactC01ComponentFileName, "one");
        fixture.AddComponent("KUKA.Sim 4.10.2", KukaSimInstallationDiscovery.ExactC01ComponentFileName, "two");

        var error = Assert.Throws<InvalidOperationException>(() =>
            KukaSimInstallationDiscovery.ResolveFromEngine(
                enginePath: null,
                componentPath: null,
                programFilesRoot: fixture.ProgramFiles,
                commonDocumentsRoot: fixture.CommonDocuments));

        Assert.Contains("Multiple KUKA.Sim 4.10 component matches", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_installation_resolves_to_preferred_410_post_install_location()
    {
        using var fixture = InstallationFixture.Create();

        var layout = KukaSimInstallationDiscovery.ResolveFromEngine(
            enginePath: null,
            componentPath: null,
            programFilesRoot: fixture.ProgramFiles,
            commonDocumentsRoot: fixture.CommonDocuments);

        Assert.Equal(
            Path.Combine(fixture.ProgramFiles, "KUKA", KukaSimInstallationDiscovery.PreferredProductDirectoryName),
            layout.InstallRoot);
        Assert.Contains("KUKA.Sim 4.10", layout.ComponentPath, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("4.10.0.100", false)]
    [InlineData("4.10.1.100", true)]
    [InlineData("KUKA.Sim 4.10.2 build 200", true)]
    [InlineData("4.11.0.100", false)]
    [InlineData("unknown", false)]
    public void Release_classifier_requires_4101_or_newer_within_410_line(string version, bool expected)
    {
        var accepted = KukaSimInstallationDiscovery.TryClassifySupported410Release(version, out var detail);

        Assert.Equal(expected, accepted);
        Assert.False(string.IsNullOrWhiteSpace(detail));
    }

    private sealed class InstallationFixture : IDisposable
    {
        private InstallationFixture(string root)
        {
            Root = root;
            ProgramFiles = Path.Combine(root, "Program Files");
            CommonDocuments = Path.Combine(root, "Public Documents");
            Directory.CreateDirectory(ProgramFiles);
            Directory.CreateDirectory(CommonDocuments);
        }

        private string Root { get; }
        public string ProgramFiles { get; }
        public string CommonDocuments { get; }

        public static InstallationFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"kuka-sim-discovery-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            return new InstallationFixture(root);
        }

        public string AddInstallation(string directoryName, bool includeEngine)
        {
            var root = Path.Combine(ProgramFiles, "KUKA", directoryName);
            Directory.CreateDirectory(root);
            if (includeEngine)
            {
                File.WriteAllText(Path.Combine(root, "VisualComponents.Engine.exe"), "engine");
                File.WriteAllText(Path.Combine(root, "VisualComponents.Engine.Launcher.exe"), "launcher");
            }
            return root;
        }

        public string AddComponent(string productDirectory, string fileName, string branch = "library")
        {
            var root = Path.Combine(CommonDocuments, "KUKA Public", productDirectory, "Models", branch);
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, fileName);
            File.WriteAllText(path, "component");
            return path;
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
