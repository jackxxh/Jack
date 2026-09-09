using KukaRoboter.Contracts.DeviceInfoService;
using KukaRoboter.OnlineServicesFacade;
using Kuka.WorkVisual.Scripting.KrcOnline;
using Kuka.WorkVisual.Scripting.KrcOnline.ScriptingObjects;
using System.Text;

var address = ScriptRunnerEnvironment.GetArgument<string>("address");

string B64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));

Logger.Info("KUKA Lab read-only controller profile readback");
Logger.Info("CONTROLLER_PROFILE_READBACK_BEGIN=True");
Logger.Info("ControllerAddress=" + address);

try
{
    IScriptingOnlineController onlineAccess = ScriptRunner.GetOnlineController(address);
    var scriptingController = (ScriptingOnlineController)onlineAccess;
    using (var deviceFacade = new DeviceInfoFacade(scriptingController.DeviceInfo))
    {
        var deviceInfo = deviceFacade.GetDeviceInfo();
        var projects = onlineAccess.GetProjects();
        Logger.Info("PROFILE_ROBOT_B64=" + B64(deviceInfo.RoboterType));
        Logger.Info("PROFILE_KSS_B64=" + B64(deviceInfo.FirmwareVersion));
        Logger.Info("PROFILE_PROJECT_B64=" + B64(deviceInfo.CurrentProjectName));
        Logger.Info("ACTIVE_PROJECT_B64=" + B64(Convert.ToString(projects.ActiveProject)));
        Logger.Info("BASE_PROJECT_B64=" + B64(Convert.ToString(projects.BaseProject)));
        Logger.Info("INITIAL_PROJECT_B64=" + B64(Convert.ToString(projects.InitialProject)));
        Logger.Info("PROJECT_COUNT=" + projects.Projects.Count());
    }

    return 0;
}
catch (Exception ex)
{
    Logger.Error("CONTROLLER_PROFILE_READBACK_FAILED: " + ex.GetType().FullName + ": " + ex.Message);
    return 42;
}
