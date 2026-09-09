using System.Reflection;

namespace KukaLab.Core.Tests;

public sealed class ProjectActivationRollbackScriptContractTests
{
    [Fact]
    public void Rollback_probe_is_authorized_idempotent_and_never_starts_activation_phases()
    {
        var script = ReadEmbeddedScript();

        Assert.Contains("authorization reference is required", script, StringComparison.Ordinal);
        Assert.Contains("IsActivationInProgress", script, StringComparison.Ordinal);
        Assert.Contains("activation.Rollback();", script, StringComparison.Ordinal);
        Assert.Contains("ACTIVATION_ROLLBACK_ALREADY_CLEAR=true", script, StringComparison.Ordinal);
        Assert.Contains("ACTIVATION_ROLLBACK_VERIFIED=true", script, StringComparison.Ordinal);
        Assert.DoesNotContain("PrepareInstallation", script, StringComparison.Ordinal);
        Assert.DoesNotContain("InstallTransactional", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ActivateTransactional", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ActivateComplete", script, StringComparison.Ordinal);
    }

    private static string ReadEmbeddedScript()
    {
        const string resourceName = "KukaLab.Probes.WorkVisual.ProjectActivationRollback.csx";
        var assembly = typeof(OfficeLiteDeploymentPreflightRunner).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
