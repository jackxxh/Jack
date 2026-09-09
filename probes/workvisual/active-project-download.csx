using Kuka.WorkVisual.Scripting.KrcOnline;
using System.Text;

var address = ScriptRunnerEnvironment.GetArgument<string>("address");
var destinationFolder = ScriptRunnerEnvironment.GetArgument<string>("destinationfolder");
Logger.Info("KUKA Lab controller-to-PC active project download");
Logger.Info("ControllerAddress=" + address);

try
{
    IScriptingOnlineController onlineAccess = ScriptRunner.GetOnlineController(address);
    IScriptingDeviceProjects projects = onlineAccess.GetProjects();
    var activeProject = Convert.ToString(projects.ActiveProject);
    var projectFilePath = onlineAccess.DownloadProject(
        SpecialProjectType.ActiveProject,
        destinationFolder,
        false);

    Logger.Info("ActiveProjectBase64=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(activeProject ?? string.Empty)));
    Logger.Info("DownloadedProjectPathBase64=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(projectFilePath ?? string.Empty)));
    Logger.Info("ProjectCount=" + projects.Projects.Count());
    return 0;
}
catch (Exception ex)
{
    Logger.Error("ACTIVE_PROJECT_DOWNLOAD_FAILED: " + ex.GetType().FullName + ": " + ex.Message);
    return 42;
}
