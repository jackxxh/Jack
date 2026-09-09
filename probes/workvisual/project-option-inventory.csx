using System.IO;
using System.Linq;
using System.Security.Cryptography;

var projectPath = Path.GetFullPath(ScriptRunnerEnvironment.GetArgument<string>("project"));
var expectedSha256 = ScriptRunnerEnvironment.GetArgument<string>("expectedsha256");

string ComputeSha256(string path)
{
    using (var stream = File.OpenRead(path))
    using (var sha256 = SHA256.Create())
    {
        return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
    }
}

if (!File.Exists(projectPath)
    || !string.Equals(Path.GetExtension(projectPath), ".wvs", StringComparison.OrdinalIgnoreCase))
{
    Logger.Error("PROJECT_OPTION_INVENTORY_FAILED: project must be an existing WVS file.");
    return 41;
}

var hashBefore = ComputeSha256(projectPath);
Logger.Info("PROJECT=" + projectPath);
Logger.Info("PROJECT_SHA256_BEFORE=" + hashBefore);
Logger.Info("AUTOMATIC_UPDATE=true");
if (string.IsNullOrWhiteSpace(expectedSha256)
    || !string.Equals(hashBefore, expectedSha256, StringComparison.OrdinalIgnoreCase))
{
    Logger.Error("PROJECT_OPTION_INVENTORY_FAILED: project hash does not match the caller-pinned copy.");
    return 43;
}

var container = ScriptRunner.GetType().GetProperty("Container").GetValue(ScriptRunner);
var resolveByType = container.GetType().GetMethod("Resolve", new[] { typeof(Type) });
var solution = (Kuka.WorkVisual.Scripting.IScriptingSolution)resolveByType.Invoke(
    container,
    new object[] { typeof(Kuka.WorkVisual.Scripting.IScriptingSolution) });

var workingCopyPath = Path.Combine(
    Path.GetTempPath(),
    "kuka-lab-project-option-inventory-" + Guid.NewGuid().ToString("N") + ".wvs");
var exitCode = 0;
try
{
    File.Copy(projectPath, workingCopyPath, false);
    Logger.Info("WORKING_COPY_CREATED=true");
    Logger.Info("WORKING_COPY_SHA256_BEFORE=" + ComputeSha256(workingCopyPath));
    var cell = solution.Open(workingCopyPath, true, name => Logger.Info("MISSING_CATALOG=" + name));
    var controller = cell.Controllers.Single();
    Logger.Info("PROJECT_CONTROLLER=" + controller.DisplayName);
    Logger.Info("PROJECT_FIRMWARE=" + controller.FirmwareVersion);
    Logger.Info("PROJECT_ROBOT=" + controller.Machines.Robot.DisplayName);
    Logger.Info("PROJECT_OPTION_COUNT=" + controller.Options.Count);
    for (var index = 0; index < controller.Options.Count; index++)
    {
        var option = controller.Options[index];
        Logger.Info("PROJECT_OPTION_" + index + "=" + option.Name + "|" + option.Version);
    }

    Logger.Info("ACTIVE_OPTION_PROFILE=" + ScriptRunnerEnvironment.ActiveOptionProfile);
    Logger.Info("DOWNLOADED_OPTIONS_DIRECTORY=" + ScriptRunnerEnvironment.DownloadedOptionsDirectory);
    Logger.Info("INSTALLED_OPTION_PACKAGE_COUNT=" + ScriptRunner.OptionPackages.Count);
    for (var index = 0; index < ScriptRunner.OptionPackages.Count; index++)
    {
        var package = ScriptRunner.OptionPackages[index];
        Logger.Info("INSTALLED_OPTION_PACKAGE_" + index + "=" + package.Name + "|" + package.Version);
    }
}
catch (Exception exception)
{
    Logger.Error("PROJECT_OPTION_INVENTORY_FAILED: "
        + exception.GetType().FullName + ": " + exception.GetBaseException().Message);
    exitCode = 42;
}
finally
{
    if (solution.IsOpen())
    {
        solution.Close(false);
    }

    if (File.Exists(workingCopyPath))
    {
        Logger.Info("WORKING_COPY_SHA256_AFTER=" + ComputeSha256(workingCopyPath));
        File.Delete(workingCopyPath);
    }

    var hashAfter = ComputeSha256(projectPath);
    Logger.Info("PROJECT_SHA256_AFTER=" + hashAfter);
    Logger.Info("PROJECT_SOURCE_BYTES_UNCHANGED="
        + string.Equals(hashBefore, hashAfter, StringComparison.OrdinalIgnoreCase).ToString().ToLowerInvariant());
    Logger.Info("WORKING_COPY_CLEANED=" + (!File.Exists(workingCopyPath)).ToString().ToLowerInvariant());
    if (!string.Equals(hashBefore, hashAfter, StringComparison.OrdinalIgnoreCase))
    {
        Logger.Error("PROJECT_OPTION_INVENTORY_FAILED: WorkVisual changed the caller-pinned source project.");
        exitCode = 47;
    }
    else if (File.Exists(workingCopyPath))
    {
        Logger.Error("PROJECT_OPTION_INVENTORY_FAILED: the automatic-update working copy was not removed.");
        exitCode = 48;
    }
}

return exitCode;
