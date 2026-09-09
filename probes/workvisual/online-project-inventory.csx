using Kuka.WorkVisual.Scripting.KrcOnline;

var address = ScriptRunnerEnvironment.GetArgument<string>("address");
Logger.Info("KUKA Lab read-only online project inventory");
Logger.Info("Controller address: " + address);

try
{
    IScriptingOnlineController onlineAccess = ScriptRunner.GetOnlineController(address);
    IScriptingDeviceProjects projects = onlineAccess.GetProjects();
    Logger.Info("ActiveProject=" + Convert.ToString(projects.ActiveProject));
    Logger.Info("BaseProject=" + Convert.ToString(projects.BaseProject));
    Logger.Info("InitialProject=" + Convert.ToString(projects.InitialProject));
    Logger.Info("ProjectCount=" + projects.Projects.Count());
    var projectIndex = 0;
    foreach (var project in projects.Projects)
    {
        Logger.Info("Project_" + projectIndex + "=" + Convert.ToString(project));
        projectIndex++;
    }
    return 0;
}
catch (Exception ex)
{
    Logger.Error("ONLINE_PROJECT_INVENTORY_FAILED: " + ex.GetType().FullName + ": " + ex.Message);
    return 42;
}
