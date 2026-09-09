using System.Reflection;

namespace KukaLab.Core.Tests;

public sealed class ExactProfileDeploymentScriptContractTests
{
    [Fact]
    public void Virtual_motion_profile_builder_is_create_new_hash_pinned_and_offline()
    {
        var labRoot = FindLabRoot();
        var script = File.ReadAllText(Path.Combine(
            labRoot,
            "probes",
            "workvisual",
            "exact-c01-virtual-motion-profile.csx"));

        Assert.Contains("0FFFA24D535BD8435A2F6E40E6C0605C6BA386B82DA92361D92F0917792B5A3F", script, StringComparison.Ordinal);
        Assert.Contains("557DF5B281E08E508CB2FFD8304256D293D51E2402061A9B24DA7ED0D5A91DEE", script, StringComparison.Ordinal);
        Assert.Contains("72ACC5FBB7B1F1C39822B7B373523AF5BCB0AE5546D3281FAC2848E633309A73", script, StringComparison.Ordinal);
        Assert.Contains("File.Exists(outputPath)", script, StringComparison.Ordinal);
        Assert.Contains("File.Copy(inputPath, outputPath, false)", script, StringComparison.Ordinal);
        Assert.Contains("SIGNAL $MOVE_ENABLE $IN[505]", script, StringComparison.Ordinal);
        Assert.Contains("SIGNAL $MOVE_ENABLE $IN[1025]", script, StringComparison.Ordinal);
        Assert.Contains("LoadDataDetermination", script, StringComparison.Ordinal);
        Assert.Contains("KUKA.PROFINET S", script, StringComparison.Ordinal);
        Assert.Contains("foreach (var option in controller.Options.ToArray()) option.Remove();", script, StringComparison.Ordinal);
        Assert.Contains("POST_PROCESS_STABLE_HASH_REQUIRED=true", script, StringComparison.Ordinal);
        Assert.DoesNotContain("GetOnlineController", script, StringComparison.Ordinal);
        Assert.DoesNotContain("GetDeployment", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ActivationFacade", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Credential", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Exact_profile_deployment_selects_local_only_names_and_accepts_only_pinned_target_option_reconciliation()
    {
        var script = ReadEmbeddedScript();

        Assert.Contains("SELECTED_RESOLUTION=", script, StringComparison.Ordinal);
        Assert.Contains("resolution.Resolve();", script, StringComparison.Ordinal);
        Assert.DoesNotContain("conflict.Resolve();", script, StringComparison.Ordinal);
        Assert.DoesNotContain("conflictsBefore.Length != 2", script, StringComparison.Ordinal);
        Assert.Contains("GroupBy", script, StringComparison.Ordinal);
        Assert.Contains("PROJECT_OPTION_COUNT_AFTER_EXECUTE=", script, StringComparison.Ordinal);
        Assert.Contains("KUKA.PROFINET S", script, StringComparison.Ordinal);
        Assert.Contains("6.0.0", script, StringComparison.Ordinal);
        Assert.Contains("TARGET_OPTION_RECONCILIATION_ACCEPTED=true", script, StringComparison.Ordinal);
        Assert.Contains("did not match the pinned OfficeLite target reconciliation contract", script, StringComparison.Ordinal);
        Assert.DoesNotContain("deployment injected an option", script, StringComparison.Ordinal);
    }

    private static string ReadEmbeddedScript()
    {
        const string resourceName = "KukaLab.Probes.WorkVisual.ExactProfileDeployment.csx";
        var assembly = typeof(OfficeLiteDeploymentPreflightRunner).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string FindLabRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "KukaLab.slnx"))) return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the KUKA Lab root.");
    }
}
