using System.Linq;
using Kuka.WorkVisual.Scripting.KrcOnline;

var projectPath = ScriptRunnerEnvironment.GetArgument<string>("project");
var address = ScriptRunnerEnvironment.GetArgument<string>("address");
var executeText = ScriptRunnerEnvironment.GetArgument<string>("execute");
var authorizationRef = ScriptRunnerEnvironment.GetArgument<string>("authorizationref");
var execute = string.Equals(executeText, "true", StringComparison.OrdinalIgnoreCase);
const string reconciledTargetOptionName = "KUKA.PROFINET S";
const string reconciledTargetOptionVersion = "6.0.0";
var allowedConflictTypes = new[]
{
    ScriptingDeploymentConflictType.CellNameMismatch,
    ScriptingDeploymentConflictType.ControllerNameMismatch
};

var container = ScriptRunner.GetType().GetProperty("Container").GetValue(ScriptRunner);
var resolveByType = container.GetType().GetMethod("Resolve", new[] { typeof(Type) });
var solution = (Kuka.WorkVisual.Scripting.IScriptingSolution)resolveByType.Invoke(
    container,
    new object[] { typeof(Kuka.WorkVisual.Scripting.IScriptingSolution) });

IScriptingControllerDeployment deployment = null;
var deploymentExecuted = false;
try
{
    if (execute && string.IsNullOrWhiteSpace(authorizationRef))
    {
        Logger.Error("EXACT_PROFILE_DEPLOYMENT_FAILED: execution requires a non-empty authorization reference.");
        return 41;
    }

    var cell = solution.Open(projectPath, false, name => Logger.Info("MISSING_CATALOG=" + name));
    var controller = cell.Controllers.Single();
    Logger.Info("PROJECT=" + projectPath);
    Logger.Info("SOURCE_CONTROLLER=" + controller.DisplayName);
    Logger.Info("SOURCE_ADDRESS=" + controller.Address);
    Logger.Info("SOURCE_FIRMWARE=" + controller.FirmwareVersion);
    Logger.Info("SOURCE_ROBOT=" + controller.Machines.Robot.DisplayName);
    Logger.Info("TARGET_ADDRESS=" + address);
    Logger.Info("EXECUTE_REQUESTED=" + execute);
    Logger.Info("AUTHORIZATION_REF=" + (execute ? authorizationRef : "not-required-for-resolve-only"));
    Logger.Info("PROJECT_OPTION_COUNT=" + controller.Options.Count);

    if (!string.Equals(controller.FirmwareVersion, "8.7.8", StringComparison.Ordinal)
        || !string.Equals(controller.Machines.Robot.DisplayName, "KR 210 R2700-2 C01", StringComparison.Ordinal)
        || controller.Options.Count != 0)
    {
        Logger.Error("EXACT_PROFILE_DEPLOYMENT_FAILED: project identity is not the zero-option KR 210 R2700-2 C01 / 8.7.8 contract.");
        return 43;
    }

    deployment = controller.GetDeployment();
    deployment.ChangeTarget(address);
    var conflictsBefore = deployment.GetConflicts().ToArray();
    Logger.Info("CONFLICT_BEFORE_COUNT=" + conflictsBefore.Length);
    for (var index = 0; index < conflictsBefore.Length; index++)
    {
        var conflict = conflictsBefore[index];
        var resolutions = conflict.Resolutions.ToArray();
        Logger.Info("CONFLICT_BEFORE_" + index + "_TYPE=" + conflict.ConflictType);
        Logger.Info("CONFLICT_BEFORE_" + index + "_TITLE=" + conflict.Title);
        Logger.Info("CONFLICT_BEFORE_" + index + "_RESOLUTION_COUNT=" + resolutions.Length);
        for (var resolutionIndex = 0; resolutionIndex < resolutions.Length; resolutionIndex++)
        {
            var resolution = resolutions[resolutionIndex];
            Logger.Info("CONFLICT_BEFORE_" + index + "_RESOLUTION_" + resolutionIndex + "_DEFAULT=" + resolution.IsDefault);
            Logger.Info("CONFLICT_BEFORE_" + index + "_RESOLUTION_" + resolutionIndex + "_TITLE=" + resolution.Title);
            Logger.Info("CONFLICT_BEFORE_" + index + "_RESOLUTION_" + resolutionIndex + "_DESCRIPTION=" + resolution.Description);
        }

        if (!allowedConflictTypes.Contains(conflict.ConflictType) || conflict.DefaultResolution == null)
        {
            Logger.Error("EXACT_PROFILE_DEPLOYMENT_FAILED: a conflict is outside the two-name-conflict allowlist or has no default resolution.");
            return 44;
        }
    }

    if (conflictsBefore
        .GroupBy(conflict => conflict.ConflictType)
        .Any(group => group.Count() != 1))
    {
        Logger.Error("EXACT_PROFILE_DEPLOYMENT_FAILED: the pre-resolution conflict set contains a duplicate name-conflict type.");
        return 45;
    }

    foreach (var conflict in conflictsBefore)
    {
        var resolutions = conflict.Resolutions.ToArray();
        IScriptingDeploymentConflictResolution resolution = null;
        if (conflict.ConflictType == ScriptingDeploymentConflictType.CellNameMismatch)
        {
            resolution = resolutions.Single(candidate => candidate.IsDefault);
        }
        else if (conflict.ConflictType == ScriptingDeploymentConflictType.ControllerNameMismatch)
        {
            resolution = resolutions.Single(candidate => !candidate.IsDefault);
        }

        if (resolution == null)
        {
            Logger.Error("EXACT_PROFILE_DEPLOYMENT_FAILED: no local-only name resolution was selected.");
            return 48;
        }

        Logger.Info("SELECTED_RESOLUTION=" + conflict.ConflictType + "|" + resolution.Title);
        resolution.Resolve();
    }

    var conflictsAfter = deployment.GetConflicts().ToArray();
    var canExecuteAfter = deployment.CanExecute();
    Logger.Info("CONFLICT_AFTER_COUNT=" + conflictsAfter.Length);
    for (var index = 0; index < conflictsAfter.Length; index++)
    {
        Logger.Info("CONFLICT_AFTER_" + index + "_TYPE=" + conflictsAfter[index].ConflictType);
        Logger.Info("CONFLICT_AFTER_" + index + "_TITLE=" + conflictsAfter[index].Title);
    }
    Logger.Info("CAN_EXECUTE_AFTER=" + canExecuteAfter);

    if (conflictsAfter.Length != 0 || deployment.HasConflicts() || !canExecuteAfter)
    {
        Logger.Error("EXACT_PROFILE_DEPLOYMENT_FAILED: conflict resolution did not produce a conflict-free executable deployment.");
        return 46;
    }

    if (execute)
    {
        deployment.Execute();
        deploymentExecuted = true;
        Logger.Info("PROJECT_OPTION_COUNT_AFTER_EXECUTE=" + controller.Options.Count);
        for (var index = 0; index < controller.Options.Count; index++)
        {
            var option = controller.Options[index];
            Logger.Info("PROJECT_OPTION_AFTER_EXECUTE_" + index + "=" + option.Name + "|" + option.Version);
        }

        var reconciledTargetOptions = controller.Options
            .Select(option => new { option.Name, Version = option.Version.ToString() })
            .ToArray();
        if (reconciledTargetOptions.Length != 1
            || !string.Equals(reconciledTargetOptions[0].Name, reconciledTargetOptionName, StringComparison.Ordinal)
            || !string.Equals(reconciledTargetOptions[0].Version, reconciledTargetOptionVersion, StringComparison.Ordinal))
        {
            Logger.Error("EXACT_PROFILE_DEPLOYMENT_FAILED: deployment output options did not match the pinned OfficeLite target reconciliation contract.");
            return 47;
        }

        Logger.Info("TARGET_OPTION_RECONCILIATION_ACCEPTED=true");
    }

    Logger.Info("CONFLICT_RESOLUTION_PERFORMED=true");
    Logger.Info("DEPLOYMENT_EXECUTED=" + deploymentExecuted.ToString().ToLowerInvariant());
    return 0;
}
catch (Exception exception)
{
    Logger.Error("EXACT_PROFILE_DEPLOYMENT_FAILED: " + exception.GetType().FullName + ": " + exception.GetBaseException().Message);
    Logger.Info("DEPLOYMENT_EXECUTED=" + deploymentExecuted.ToString().ToLowerInvariant());
    return 42;
}
finally
{
    var disposable = deployment as IDisposable;
    if (disposable != null)
    {
        disposable.Dispose();
    }
    if (solution.IsOpen())
    {
        solution.Close(false);
    }
}
