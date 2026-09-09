using KukaRoboter.OnlineServicesFacade;
using System.Text;

var address = ScriptRunnerEnvironment.GetArgument<string>("address");
var repositoryPath = ScriptRunnerEnvironment.GetArgument<string>("repositorypath");
Logger.Info("KUKA Lab read-only controller repository inventory");
Logger.Info("ControllerAddress=" + address);
Logger.Info("RepositoryPath=" + repositoryPath);

try
{
    using (var repository = new FileHandlingFacade(address))
    {
        var directoryExists = repository.DirectoryExists(repositoryPath);
        Logger.Info("DirectoryExists=" + directoryExists);
        if (!directoryExists)
        {
            Logger.Error("REPOSITORY_INVENTORY_PATH_MISSING: " + repositoryPath);
            return 43;
        }

        var directories = repository.GetDirectories(repositoryPath, false)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var files = repository.GetFiles(repositoryPath, false)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Logger.Info("DirectoryCount=" + directories.Length);
        foreach (var directory in directories)
        {
            Logger.Info("DirectoryBase64=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(directory)));
        }

        Logger.Info("FileCount=" + files.Length);
        foreach (var file in files)
        {
            Logger.Info("FileBase64=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(file)));
        }
    }

    return 0;
}
catch (Exception ex)
{
    Logger.Error("REPOSITORY_INVENTORY_FAILED: " + ex.GetType().FullName + ": " + ex.Message);
    return 42;
}
