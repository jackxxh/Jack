using KukaRoboter.Contracts.DeviceInfoService;
using KukaRoboter.OnlineServicesFacade;
using Kuka.WorkVisual.Scripting.KrcOnline;
using Kuka.WorkVisual.Scripting.KrcOnline.ScriptingObjects;
using System.Linq;

var address = ScriptRunnerEnvironment.GetArgument<string>("address");
var projectName = ScriptRunnerEnvironment.GetArgument<string>("projectname");
var authorizationRef = ScriptRunnerEnvironment.GetArgument<string>("authorizationref");

Logger.Info("KUKA Lab isolated final-only project activation");
Logger.Info("ControllerAddress=" + address);
Logger.Info("ProjectName=" + projectName);
Logger.Info("AuthorizationRef=" + authorizationRef);

if (string.IsNullOrWhiteSpace(authorizationRef))
{
    Logger.Error("PROJECT_ACTIVATION_FINALIZE_FAILED: authorization reference is required.");
    return 41;
}

try
{
    var status = new ActivationInfoFacade(address);
    KukaRoboter.Contracts.ProjectInfo pendingProject;
    if (status.IsActivationInProgress(out pendingProject))
    {
        Logger.Error("PROJECT_ACTIVATION_FINALIZE_FAILED: another activation is already in progress: "
            + Convert.ToString(pendingProject));
        return 43;
    }

    IScriptingOnlineController onlineAccess = ScriptRunner.GetOnlineController(address);
    var scriptingController = (ScriptingOnlineController)onlineAccess;
    var deviceInfo = scriptingController.DeviceInfo;
    using (var deviceFacade = new DeviceInfoFacade(deviceInfo))
    {
        var project = deviceFacade.GetProjects().Projects.SingleOrDefault(
            candidate => string.Equals(candidate.Name, projectName, StringComparison.Ordinal));
        if (project == null)
        {
            throw new InvalidOperationException("Expected installed project was not found.");
        }

        Logger.Info("CurrentProjectName=" + deviceInfo.CurrentProjectName);
        Logger.Info("TargetProject=" + project);
        if (string.Equals(deviceInfo.CurrentProjectName, projectName, StringComparison.Ordinal))
        {
            Logger.Info("PROJECT_ACTIVATION_FINALIZE_ALREADY_ACTIVE=true");
            return 0;
        }

        using (var activation = new ActivationFacade(project, deviceInfo))
        {
            activation.ProgressChanged += (sender, progress) =>
                Logger.Info("ACTIVATION_PROGRESS=" + progress.ProgressPercentage + ";" + Convert.ToString(progress.UserState));
            Logger.Info("ACTIVATION_STATE_BEFORE=" + activation.State);
            activation.ActivateTransactional();
            Logger.Info("ACTIVATION_STATE_AFTER=" + activation.State);
            Logger.Info("ACTIVATION_ERROR=" + (activation.Error == null ? "none" : activation.Error.Message));
            if (activation.State != ProjectState.Active || activation.Error != null)
            {
                throw new InvalidOperationException("Final activation did not finish in Active state.");
            }
        }
    }

    Logger.Info("PROJECT_ACTIVATION_FINALIZE_EXECUTED=true");
    return 0;
}
catch (Exception exception)
{
    Logger.Error("PROJECT_ACTIVATION_FINALIZE_FAILED: "
        + exception.GetType().FullName + ": " + exception.GetBaseException().Message);
    Logger.Info("PROJECT_ACTIVATION_FINALIZE_EXECUTED=false");
    return 42;
}
