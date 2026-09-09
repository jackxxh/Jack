using KukaRoboter.Contracts;
using KukaRoboter.Contracts.DeviceInfoService;
using KukaRoboter.OnlineServicesFacade;
using Kuka.WorkVisual.Scripting.KrcOnline;
using Kuka.WorkVisual.Scripting.KrcOnline.ScriptingObjects;
using System.Linq;
using System.Threading;

var address = ScriptRunnerEnvironment.GetArgument<string>("address");
var projectName = ScriptRunnerEnvironment.GetArgument<string>("projectname");
var authorizationRef = ScriptRunnerEnvironment.GetArgument<string>("authorizationref");

Logger.Info("KUKA Lab isolated project activation rollback");
Logger.Info("ControllerAddress=" + address);
Logger.Info("ProjectName=" + projectName);
Logger.Info("AuthorizationRef=" + authorizationRef);

if (string.IsNullOrWhiteSpace(authorizationRef))
{
    Logger.Error("PROJECT_ACTIVATION_ROLLBACK_FAILED: authorization reference is required.");
    return 41;
}

try
{
    var statusFacade = new ActivationInfoFacade(address);
    ProjectInfo pendingProject;
    var inProgressBefore = statusFacade.IsActivationInProgress(out pendingProject);
    Logger.Info("ACTIVATION_IN_PROGRESS_BEFORE=" + inProgressBefore);
    Logger.Info("ACTIVATION_PROJECT_BEFORE=" + Convert.ToString(pendingProject));

    if (!inProgressBefore)
    {
        Logger.Info("ACTIVATION_ROLLBACK_ALREADY_CLEAR=true");
        Logger.Info("ACTIVATION_ROLLBACK_EXECUTED=false");
        Logger.Info("ACTIVATION_ROLLBACK_VERIFIED=true");
        return 0;
    }

    if (pendingProject == null
        || !string.Equals(pendingProject.Name, projectName, StringComparison.Ordinal))
    {
        Logger.Error("PROJECT_ACTIVATION_ROLLBACK_FAILED: the pending project does not match the authorized project name.");
        return 43;
    }

    IScriptingOnlineController onlineAccess = ScriptRunner.GetOnlineController(address);
    var scriptingController = (ScriptingOnlineController)onlineAccess;
    using (var deviceFacade = new DeviceInfoFacade(scriptingController.DeviceInfo))
    {
        var project = deviceFacade.GetProjects().Projects.SingleOrDefault(
            candidate => string.Equals(candidate.Name, projectName, StringComparison.Ordinal));
        if (project == null)
        {
            throw new InvalidOperationException("Expected staged project was not found.");
        }

        using (var activation = new ActivationFacade(project, scriptingController.DeviceInfo))
        {
            activation.Rollback();
            Logger.Info("ACTIVATION_ROLLBACK_STATE=" + activation.State);
        }
    }

    var inProgressAfter = true;
    ProjectInfo projectAfter = null;
    for (var attempt = 0; attempt < 20; attempt++)
    {
        var afterFacade = new ActivationInfoFacade(address);
        inProgressAfter = afterFacade.IsActivationInProgress(out projectAfter);
        if (!inProgressAfter)
        {
            break;
        }

        Thread.Sleep(500);
    }

    Logger.Info("ACTIVATION_IN_PROGRESS_AFTER=" + inProgressAfter);
    Logger.Info("ACTIVATION_PROJECT_AFTER=" + Convert.ToString(projectAfter));
    Logger.Info("ACTIVATION_ROLLBACK_EXECUTED=true");
    Logger.Info("ACTIVATION_ROLLBACK_VERIFIED=" + (!inProgressAfter).ToString().ToLowerInvariant());
    if (inProgressAfter)
    {
        Logger.Error("PROJECT_ACTIVATION_ROLLBACK_FAILED: the controller still reports an activation transaction.");
        return 44;
    }

    return 0;
}
catch (Exception exception)
{
    Logger.Error("PROJECT_ACTIVATION_ROLLBACK_FAILED: "
        + exception.GetType().FullName + ": " + exception.GetBaseException().Message);
    Logger.Info("ACTIVATION_ROLLBACK_VERIFIED=false");
    return 42;
}
