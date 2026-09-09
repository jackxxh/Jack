using KukaRoboter.Contracts.DeviceInfoService;
using KukaRoboter.OnlineServicesFacade;
using Kuka.WorkVisual.Scripting.KrcOnline;
using Kuka.WorkVisual.Scripting.KrcOnline.ScriptingObjects;

var address = ScriptRunnerEnvironment.GetArgument<string>("address");
var expectedProjectName = ScriptRunnerEnvironment.GetArgument<string>("projectname");
Logger.Info("KUKA Lab project activation preflight");
Logger.Info("ControllerAddress=" + address);
Logger.Info("ExpectedProjectName=" + expectedProjectName);

try
{
    IScriptingOnlineController onlineAccess = ScriptRunner.GetOnlineController(address);
    var scriptingController = (ScriptingOnlineController)onlineAccess;
    using (var facade = new DeviceInfoFacade(scriptingController.DeviceInfo))
    {
        var deviceInfo = facade.GetDeviceInfo();
        var projects = facade.GetProjects();
        Logger.Info("DeviceName=" + deviceInfo.Name);
        Logger.Info("FirmwareVersion=" + deviceInfo.FirmwareVersion);
        Logger.Info("RobotType=" + deviceInfo.RoboterType);
        Logger.Info("CurrentProjectName=" + deviceInfo.CurrentProjectName);
        Logger.Info("ActiveProject=" + Convert.ToString(projects.ActiveProject));
        Logger.Info("NotActivatedPackage=" + Convert.ToString(projects.Package));
        Logger.Info("ProjectCount=" + projects.Projects.Length);

        var matched = false;
        for (var index = 0; index < projects.Projects.Length; index++)
        {
            var project = projects.Projects[index];
            Logger.Info("Project_" + index + "_Name=" + project.Name);
            Logger.Info("Project_" + index + "_Type=" + project.ProjectType);
            Logger.Info("Project_" + index + "_Id=" + project.ProjectId);
            Logger.Info("Project_" + index + "_Version=" + project.Version);
            if (string.Equals(project.Name, expectedProjectName, StringComparison.Ordinal))
            {
                matched = true;
            }
        }

        if (!matched)
        {
            throw new InvalidOperationException("Expected staged project was not found.");
        }

        Logger.Info("ACTIVATION_PREFLIGHT_READY=true");
        return 0;
    }
}
catch (Exception ex)
{
    Logger.Error("PROJECT_ACTIVATION_PREFLIGHT_FAILED: " + ex.GetType().FullName + ": " + ex.Message);
    return 42;
}
