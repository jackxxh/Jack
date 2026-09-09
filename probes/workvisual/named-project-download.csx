using Kuka.WorkVisual.Scripting.KrcOnline;
using System.Text;

var address = ScriptRunnerEnvironment.GetArgument<string>("address");
var projectName = ScriptRunnerEnvironment.GetArgument<string>("projectname");
var destinationFolder = ScriptRunnerEnvironment.GetArgument<string>("destinationfolder");
Logger.Info("KUKA Lab controller-to-PC named project download");
Logger.Info("ControllerAddress=" + address);
Logger.Info("ProjectNameBase64=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(projectName ?? string.Empty)));

try
{
    IScriptingOnlineController onlineAccess = ScriptRunner.GetOnlineController(address);
    IScriptingDeviceProjects projects = onlineAccess.GetProjects();
    var projectFilePath = onlineAccess.DownloadProject(projectName, destinationFolder, false);

    Logger.Info("ActiveProjectBase64=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Convert.ToString(projects.ActiveProject) ?? string.Empty)));
    Logger.Info("DownloadedProjectPathBase64=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(projectFilePath ?? string.Empty)));
    Logger.Info("ProjectCount=" + projects.Projects.Count());
    return 0;
}
catch (Exception ex)
{
    Logger.Error("NAMED_PROJECT_DOWNLOAD_FAILED: " + ex.GetType().FullName + ": " + ex.Message);
    return 42;
}
