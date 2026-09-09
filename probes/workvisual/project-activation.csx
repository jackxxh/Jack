using KukaRoboter.Contracts.DeviceInfoService;
using KukaRoboter.OnlineServicesFacade;
using Kuka.WorkVisual.Scripting.KrcOnline;
using Kuka.WorkVisual.Scripting.KrcOnline.ScriptingObjects;

var address = ScriptRunnerEnvironment.GetArgument<string>("address");
var projectName = ScriptRunnerEnvironment.GetArgument<string>("projectname");
var execute = ScriptRunnerEnvironment.GetArgument<bool>("execute");
var authorizationRef = ScriptRunnerEnvironment.GetArgument<string>("authorizationref");
Logger.Info("KUKA Lab isolated project activation");
Logger.Info("ControllerAddress=" + address);
Logger.Info("ProjectName=" + projectName);
Logger.Info("ExecuteRequested=" + execute);
Logger.Info("AuthorizationRef=" + authorizationRef);

try
{
    IScriptingOnlineController onlineAccess = ScriptRunner.GetOnlineController(address);
    var scriptingController = (ScriptingOnlineController)onlineAccess;
    var deviceInfo = scriptingController.DeviceInfo;

    using (var deviceFacade = new DeviceInfoFacade(deviceInfo))
    {
        var projects = deviceFacade.GetProjects();
        var project = projects.Projects.SingleOrDefault(
            candidate => string.Equals(candidate.Name, projectName, StringComparison.Ordinal));
        if (project == null)
        {
            throw new InvalidOperationException("Expected staged project was not found.");
        }

        Logger.Info("CurrentProjectName=" + deviceInfo.CurrentProjectName);
        Logger.Info("CurrentRobotType=" + deviceInfo.RoboterType);
        Logger.Info("FirmwareVersion=" + deviceInfo.FirmwareVersion);
        Logger.Info("TargetProjectId=" + project.ProjectId);
        Logger.Info("TargetProjectVersion=" + project.Version);
        Logger.Info("TargetProjectType=" + project.ProjectType);
        Logger.Info("ACTIVATION_CAN_EXECUTE=true");

        if (!execute)
        {
            Logger.Info("PROJECT_ACTIVATION_EXECUTED=false");
            return 0;
        }

        if (string.IsNullOrWhiteSpace(authorizationRef))
        {
            throw new InvalidOperationException("An authorization reference is required for project activation.");
        }

        using (var activation = new ActivationFacade(project, deviceInfo))
        {
            activation.ProgressChanged += (sender, progress) =>
                Logger.Info("ACTIVATION_PROGRESS=" + progress.ProgressPercentage + ";" + Convert.ToString(progress.UserState));
            var modification = activation.ActivateComplete();
            Logger.Info("ACTIVATION_STATE=" + activation.State);
            Logger.Info("ACTIVATION_MODIFICATION=" + Convert.ToString(modification));
            if (activation.Error == null)
            {
                Logger.Info("ACTIVATION_ERROR=none");
            }
            else
            {
                Logger.Error("ACTIVATION_ERROR=" + activation.Error.GetType().FullName + ": " + activation.Error.Message);
            }

            if (activation.State != ProjectState.Active || activation.Error != null)
            {
                throw new InvalidOperationException("Project activation did not finish in Active state.");
            }
        }

        Logger.Info("PROJECT_ACTIVATION_EXECUTED=true");
        return 0;
    }
}
catch (Exception ex)
{
    Logger.Error("PROJECT_ACTIVATION_FAILED: " + ex.GetType().FullName + ": " + ex.Message);
    Logger.Info("PROJECT_ACTIVATION_EXECUTED=false");
    return 42;
}
