using KukaRoboter.Contracts;
using KukaRoboter.Contracts.DeviceInfoService;
using KukaRoboter.OnlineServicesFacade;
using Kuka.WorkVisual.Scripting.KrcOnline;
using Kuka.WorkVisual.Scripting.KrcOnline.ScriptingObjects;
using System.Text;

var address = ScriptRunnerEnvironment.GetArgument<string>("address");
var projectName = ScriptRunnerEnvironment.GetArgument<string>("projectname");
Logger.Info("KUKA Lab project activation status");
Logger.Info("ControllerAddress=" + address);
Logger.Info("ProjectName=" + projectName);

try
{
    var facade = new ActivationInfoFacade(address);
    ProjectInfo project;
    var inProgress = facade.IsActivationInProgress(out project);
    Logger.Info("ActivationInProgress=" + inProgress);
    Logger.Info("ActivationProject=" + Convert.ToString(project));
    IScriptingOnlineController onlineAccess = ScriptRunner.GetOnlineController(address);
    var scriptingController = (ScriptingOnlineController)onlineAccess;
    if (project == null || !string.Equals(project.Name, projectName, StringComparison.Ordinal))
    {
        using (var deviceFacade = new DeviceInfoFacade(scriptingController.DeviceInfo))
        {
            project = deviceFacade.GetProjects().Projects.SingleOrDefault(
                candidate => string.Equals(candidate.Name, projectName, StringComparison.Ordinal));
        }

        Logger.Info("ActivationProjectResolvedFromCatalog=" + (project != null));
    }

    if (project != null)
    {
        using (var activation = new ActivationFacade(project, scriptingController.DeviceInfo))
        {
            var state = activation.GetActivationState();
            if (state == null)
            {
                Logger.Info("ActivationState=");
                Logger.Info("ActivationLogBase64=");
            }
            else
            {
                Logger.Info("ActivationState=" + state.State);
                Logger.Info("ActivationLogBase64=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(state.ActivationLog ?? string.Empty)));
            }
        }
    }
    return 0;
}
catch (Exception ex)
{
    Logger.Error("PROJECT_ACTIVATION_STATUS_FAILED: " + ex.GetType().FullName + ": " + ex.Message);
    return 42;
}
