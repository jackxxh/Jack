using KukaRoboter.OnlineServicesFacade;
using System.Security.Cryptography;

var address = ScriptRunnerEnvironment.GetArgument<string>("address");
var sourceDirectory = Path.GetFullPath(ScriptRunnerEnvironment.GetArgument<string>("sourcedirectory"));
var evidenceDirectory = Path.GetFullPath(ScriptRunnerEnvironment.GetArgument<string>("evidencedirectory"));
var authorizationRef = ScriptRunnerEnvironment.GetArgument<string>("authorizationref");
var remoteDirectory = @"C:\KRC\Roboter\Config\User\Common";
var names = new[] { "ecatms_config.xml", "ecatms_sys_X48_config.xml", "KRC_IO.xml" };

string Sha256(string path)
{
    using (var stream = File.OpenRead(path))
    using (var hash = SHA256.Create())
        return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
}

if (string.IsNullOrWhiteSpace(authorizationRef)) return 41;
if (!Directory.Exists(sourceDirectory)) return 42;
Directory.CreateDirectory(evidenceDirectory);

try
{
    using (var repository = new FileHandlingFacade(address))
    {
        foreach (var name in names)
        {
            var source = Path.Combine(sourceDirectory, name);
            var remote = remoteDirectory + "\\" + name;
            var backup = Path.Combine(evidenceDirectory, name + ".before.bin");
            var readback = Path.Combine(evidenceDirectory, name + ".readback.bin");
            if (!File.Exists(source) || File.Exists(backup) || File.Exists(readback)) return 43;

            var existed = repository.FileExists(remote);
            Logger.Info("CONFIG_BEFORE=" + name + "|Exists=" + existed);
            if (existed)
            {
                repository.Download(remote, backup);
                Logger.Info("CONFIG_BACKUP=" + name + "|" + Sha256(backup) + "|" + new FileInfo(backup).Length);
            }

            Logger.Info("CONFIG_SOURCE=" + name + "|" + Sha256(source) + "|" + new FileInfo(source).Length);
            repository.Upload(source, remote, existed);
            repository.Download(remote, readback);
            Logger.Info("CONFIG_READBACK=" + name + "|" + Sha256(readback) + "|" + new FileInfo(readback).Length);
            if (!string.Equals(Sha256(source), Sha256(readback), StringComparison.OrdinalIgnoreCase)) return 44;
        }
    }

    Logger.Info("EXACT_C01_RUNTIME_CONFIG_OVERLAY_VERIFIED=true");
    return 0;
}
catch (Exception exception)
{
    Logger.Error("EXACT_C01_RUNTIME_CONFIG_OVERLAY_FAILED=" + exception.GetType().FullName + ": " + exception.GetBaseException().Message);
    return 45;
}
