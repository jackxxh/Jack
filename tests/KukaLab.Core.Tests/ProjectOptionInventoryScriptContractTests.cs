using System.Reflection;

namespace KukaLab.Core.Tests;

public sealed class ProjectOptionInventoryScriptContractTests
{
    [Fact]
    public void Project_option_inventory_updates_only_an_ephemeral_copy_and_never_deploys_or_saves()
    {
        var script = ReadEmbeddedScript();

        Assert.Contains("File.Copy(projectPath, workingCopyPath, false);", script, StringComparison.Ordinal);
        Assert.Contains("solution.Open(workingCopyPath, true", script, StringComparison.Ordinal);
        Assert.Contains("File.Delete(workingCopyPath);", script, StringComparison.Ordinal);
        Assert.Contains("PROJECT_SOURCE_BYTES_UNCHANGED=", script, StringComparison.Ordinal);
        Assert.Contains("WORKING_COPY_CLEANED=", script, StringComparison.Ordinal);
        Assert.Contains("PROJECT_OPTION_COUNT=", script, StringComparison.Ordinal);
        Assert.Contains("INSTALLED_OPTION_PACKAGE_COUNT=", script, StringComparison.Ordinal);
        Assert.Contains("solution.Close(false);", script, StringComparison.Ordinal);
        Assert.DoesNotContain("GetDeployment", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ChangeTarget", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Execute", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Save", script, StringComparison.Ordinal);
    }

    private static string ReadEmbeddedScript()
    {
        const string resourceName = "KukaLab.Probes.WorkVisual.ProjectOptionInventory.csx";
        var assembly = typeof(OfficeLiteDeploymentPreflightRunner).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
