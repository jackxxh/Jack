using System.Diagnostics;
using System.Text.RegularExpressions;

namespace KukaLab.Core;

internal sealed record KukaSimInstallationLayout(
    string InstallRoot,
    string EnginePath,
    string LauncherPath,
    string ScriptStarterPath,
    string BootstrapPluginPath,
    string Create3DSharedPath,
    string ComponentPath);

internal static partial class KukaSimInstallationDiscovery
{
    internal const string PreferredProductDirectoryName = "KUKA.Sim 4.10";
    internal const string ExactC01ComponentFileName = "KR 210 R2700-2 C01.vcmx";
    internal const string BootstrapPluginFileName = "Plugin.KukaLab.KukaSim410.Bootstrap.dll";
    private const string EngineFileName = "VisualComponents.Engine.exe";
    private const string LauncherFileName = "VisualComponents.Engine.Launcher.exe";

    internal static KukaSimInstallationLayout ResolveFromEngine(
        string? enginePath,
        string? componentPath,
        string componentFileName = ExactC01ComponentFileName,
        string? programFilesRoot = null,
        string? commonDocumentsRoot = null)
    {
        var root = enginePath is null
            ? ResolveInstallRoot(programFilesRoot, EngineFileName)
            : Path.GetDirectoryName(Path.GetFullPath(enginePath))
                ?? throw new ArgumentException("KUKA.Sim engine path has no parent directory.", nameof(enginePath));
        var engine = Path.GetFullPath(enginePath ?? Path.Combine(root, EngineFileName));
        return CreateLayout(root, engine, Path.Combine(root, LauncherFileName), componentPath, componentFileName, commonDocumentsRoot);
    }

    internal static KukaSimInstallationLayout ResolveFromLauncher(
        string? launcherPath,
        string? componentPath,
        string componentFileName = ExactC01ComponentFileName,
        string? programFilesRoot = null,
        string? commonDocumentsRoot = null)
    {
        var root = launcherPath is null
            ? ResolveInstallRoot(programFilesRoot, LauncherFileName)
            : Path.GetDirectoryName(Path.GetFullPath(launcherPath))
                ?? throw new ArgumentException("KUKA.Sim launcher path has no parent directory.", nameof(launcherPath));
        var launcher = Path.GetFullPath(launcherPath ?? Path.Combine(root, LauncherFileName));
        return CreateLayout(root, Path.Combine(root, EngineFileName), launcher, componentPath, componentFileName, commonDocumentsRoot);
    }

    internal static bool TryClassifyInstalled410Release(
        string executablePath,
        out string detail)
    {
        var fullPath = Path.GetFullPath(executablePath);
        if (!File.Exists(fullPath))
        {
            detail = $"KUKA.Sim executable is missing: {fullPath}";
            return false;
        }

        var versionInfo = FileVersionInfo.GetVersionInfo(fullPath);
        var declared = string.IsNullOrWhiteSpace(versionInfo.ProductVersion)
            ? versionInfo.FileVersion
            : versionInfo.ProductVersion;
        return TryClassifySupported410Release(declared, out detail);
    }

    internal static bool TryClassifySupported410Release(
        string? declaredVersion,
        out string detail)
    {
        var match = ProductVersionPattern().Match(declaredVersion ?? string.Empty);
        if (!match.Success
            || !int.TryParse(match.Groups["major"].Value, out var major)
            || !int.TryParse(match.Groups["minor"].Value, out var minor)
            || !int.TryParse(match.Groups["patch"].Value, out var patch))
        {
            detail = $"KUKA.Sim executable version cannot be proven as 4.10.1 or newer (reported: {declaredVersion ?? "<empty>"}).";
            return false;
        }

        if (major != 4 || minor != 10 || patch < 1)
        {
            detail = $"KUKA.Sim {major}.{minor}.{patch} is not accepted; the KUKA.Sim add-on first became available in 4.10.1.";
            return false;
        }

        detail = $"KUKA.Sim {major}.{minor}.{patch} satisfies the 4.10.1+ add-on baseline.";
        return true;
    }

    private static KukaSimInstallationLayout CreateLayout(
        string installRoot,
        string enginePath,
        string launcherPath,
        string? componentPath,
        string componentFileName,
        string? commonDocumentsRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentFileName);
        var root = Path.GetFullPath(installRoot);
        return new KukaSimInstallationLayout(
            root,
            Path.GetFullPath(enginePath),
            Path.GetFullPath(launcherPath),
            Path.Combine(root, "scriptStarter.dll"),
            Path.Combine(root, BootstrapPluginFileName),
            Path.Combine(root, "Create3D.Shared.dll"),
            Path.GetFullPath(componentPath ?? ResolveComponentPath(commonDocumentsRoot, componentFileName)));
    }

    private static string ResolveInstallRoot(string? programFilesRoot, string requiredExecutable)
    {
        var programFiles = Path.GetFullPath(programFilesRoot
            ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        var kukaRoot = Path.Combine(programFiles, "KUKA");
        if (Directory.Exists(kukaRoot))
        {
            var installed = Directory
                .EnumerateDirectories(kukaRoot, "KUKA.Sim 4.10*", SearchOption.TopDirectoryOnly)
                .Select(path => new
                {
                    Path = Path.GetFullPath(path),
                    Version = ParseDirectoryVersion(Path.GetFileName(path))
                })
                .Where(candidate => candidate.Version is not null
                    && candidate.Version.Major == 4
                    && candidate.Version.Minor == 10
                    && File.Exists(Path.Combine(candidate.Path, requiredExecutable)))
                .OrderByDescending(candidate => candidate.Version)
                .ThenByDescending(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (installed is not null)
            {
                return installed.Path;
            }
        }

        return Path.Combine(kukaRoot, PreferredProductDirectoryName);
    }

    private static string ResolveComponentPath(string? commonDocumentsRoot, string componentFileName)
    {
        var commonDocuments = Path.GetFullPath(commonDocumentsRoot
            ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments));
        var kukaPublicRoot = Path.Combine(commonDocuments, "KUKA Public");
        if (Directory.Exists(kukaPublicRoot))
        {
            var matches = Directory
                .EnumerateDirectories(kukaPublicRoot, "KUKA.Sim 4.10*", SearchOption.TopDirectoryOnly)
                .SelectMany(root => Directory.EnumerateFiles(root, componentFileName, SearchOption.AllDirectories))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (matches.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Multiple KUKA.Sim 4.10 component matches were found for {componentFileName}: {string.Join("; ", matches)}");
            }

            if (matches.Count == 1)
            {
                return matches[0];
            }
        }

        return Path.Combine(
            kukaPublicRoot,
            PreferredProductDirectoryName,
            "Models",
            "KUKA.Sim Library 4.10",
            "KUKA_ROBOTS",
            componentFileName);
    }

    private static Version? ParseDirectoryVersion(string directoryName)
    {
        var match = DirectoryVersionPattern().Match(directoryName);
        return match.Success && Version.TryParse(match.Groups["version"].Value, out var version)
            ? version
            : null;
    }

    [GeneratedRegex(@"^KUKA\.Sim\s+(?<version>\d+\.\d+(?:\.\d+){0,2})$", RegexOptions.CultureInvariant)]
    private static partial Regex DirectoryVersionPattern();

    [GeneratedRegex(@"(?<!\d)(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:\.\d+)?(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex ProductVersionPattern();
}
