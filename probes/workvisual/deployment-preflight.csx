using System.Linq;
using Kuka.WorkVisual.Scripting.KrcOnline;

var projectPath = ScriptRunnerEnvironment.GetArgument<string>("project");
var address = ScriptRunnerEnvironment.GetArgument<string>("address");

var container = ScriptRunner.GetType().GetProperty("Container").GetValue(ScriptRunner);
var resolveByType = container.GetType().GetMethod("Resolve", new[] { typeof(Type) });
var solution = (Kuka.WorkVisual.Scripting.IScriptingSolution)resolveByType.Invoke(
    container,
    new object[] { typeof(Kuka.WorkVisual.Scripting.IScriptingSolution) });

IScriptingControllerDeployment deployment = null;
try
{
    var cell = solution.Open(projectPath, false, name => Logger.Info("MISSING_CATALOG=" + name));
    var controller = cell.Controllers.First();
    Logger.Info("PROJECT=" + projectPath);
    Logger.Info("SOURCE_CONTROLLER=" + controller.DisplayName);
    Logger.Info("SOURCE_ADDRESS=" + controller.Address);
    Logger.Info("SOURCE_FIRMWARE=" + controller.FirmwareVersion);
    Logger.Info("SOURCE_ROBOT=" + controller.Machines.Robot.DisplayName);
    Logger.Info("TARGET_ADDRESS=" + address);
    Logger.Info("ACTIVE_OPTION_PROFILE=" + ScriptRunnerEnvironment.ActiveOptionProfile);
    Logger.Info("DOWNLOADED_OPTIONS_DIRECTORY=" + ScriptRunnerEnvironment.DownloadedOptionsDirectory);
    Logger.Info("PROJECT_OPTION_COUNT=" + controller.Options.Count);
    for (var index = 0; index < controller.Options.Count; index++)
    {
        var option = controller.Options[index];
        Logger.Info("PROJECT_OPTION_" + index + "=" + option.Name + "|" + option.Version);
    }
    Logger.Info("INSTALLED_OPTION_PACKAGE_COUNT=" + ScriptRunner.OptionPackages.Count);
    for (var index = 0; index < ScriptRunner.OptionPackages.Count; index++)
    {
        var package = ScriptRunner.OptionPackages[index];
        Logger.Info("INSTALLED_OPTION_PACKAGE_" + index + "=" + package.Name + "|" + package.Version);
    }

    deployment = controller.GetDeployment();
    deployment.ChangeTarget(address);
    var conflicts = deployment.GetConflicts().ToArray();
    Logger.Info("HAS_CONFLICTS=" + deployment.HasConflicts());
    Logger.Info("CAN_EXECUTE=" + deployment.CanExecute());
    Logger.Info("CONFLICT_COUNT=" + conflicts.Length);
    for (var index = 0; index < conflicts.Length; index++)
    {
        var conflict = conflicts[index];
        Logger.Info("CONFLICT_" + index + "_TYPE=" + conflict.ConflictType);
        Logger.Info("CONFLICT_" + index + "_TITLE=" + conflict.Title);
        Logger.Info("CONFLICT_" + index + "_DESCRIPTION=" + conflict.Description);
        Logger.Info("CONFLICT_" + index + "_RESOLUTION_COUNT=" + conflict.Resolutions.Count());
        Logger.Info("CONFLICT_" + index + "_HAS_DEFAULT=" + (conflict.DefaultResolution != null));
    }

    Logger.Info("DEPLOYMENT_EXECUTED=false");
    return 0;
}
catch (Exception exception)
{
    Logger.Error("DEPLOYMENT_PREFLIGHT_FAILED: " + exception.GetType().FullName + ": " + exception.GetBaseException().Message);
    Logger.Info("DEPLOYMENT_EXECUTED=false");
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
