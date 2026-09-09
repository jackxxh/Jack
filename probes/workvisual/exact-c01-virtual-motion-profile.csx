using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

const string ExpectedSourceSha256 = "0FFFA24D535BD8435A2F6E40E6C0605C6BA386B82DA92361D92F0917792B5A3F";
const string ExpectedCleanKrcIoSha256 = "557DF5B281E08E508CB2FFD8304256D293D51E2402061A9B24DA7ED0D5A91DEE";
const string ExpectedVirtualMotionMachineDataSha256 = "72ACC5FBB7B1F1C39822B7B373523AF5BCB0AE5546D3281FAC2848E633309A73";
const string PhysicalMoveEnable = "SIGNAL $MOVE_ENABLE $IN[505]";
const string VirtualMoveEnable = "SIGNAL $MOVE_ENABLE $IN[1025]";

var inputPath = Path.GetFullPath(ScriptRunnerEnvironment.GetArgument<string>("input"));
var outputPath = Path.GetFullPath(ScriptRunnerEnvironment.GetArgument<string>("output"));
var cleanKrcIoPath = Path.GetFullPath(ScriptRunnerEnvironment.GetArgument<string>("cleankrcio"));
var machineDataPath = Path.GetFullPath(ScriptRunnerEnvironment.GetArgument<string>("machinedata"));
var removableFiles = new[]
{
    @"Config\User\Common\IPPNIO.xml",
    @"Config\User\Common\PDevConfig.bin",
    @"Config\User\Common\pndev1.xml",
    @"Config\User\Common\PNIODriver.xml",
    @"Config\User\Common\Modules_PNIODriver.xml"
};

string Sha256(string path)
{
    using (var stream = File.OpenRead(path))
    using (var hash = SHA256.Create())
        return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
}

int Count(string value, string token)
{
    var count = 0;
    var offset = 0;
    while ((offset = value.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
    {
        count++;
        offset += token.Length;
    }
    return count;
}

var container = ScriptRunner.GetType().GetProperty("Container").GetValue(ScriptRunner);
var resolveByType = container.GetType().GetMethod("Resolve", new[] { typeof(Type) });
var solution = (Kuka.WorkVisual.Scripting.IScriptingSolution)resolveByType.Invoke(
    container,
    new object[] { typeof(Kuka.WorkVisual.Scripting.IScriptingSolution) });

try
{
    if (!File.Exists(inputPath) || File.Exists(outputPath)
        || !File.Exists(cleanKrcIoPath) || !File.Exists(machineDataPath)) return 41;
    if (!string.Equals(Sha256(inputPath), ExpectedSourceSha256, StringComparison.OrdinalIgnoreCase)
        || !string.Equals(Sha256(cleanKrcIoPath), ExpectedCleanKrcIoSha256, StringComparison.OrdinalIgnoreCase)
        || !string.Equals(Sha256(machineDataPath), ExpectedVirtualMotionMachineDataSha256, StringComparison.OrdinalIgnoreCase)) return 42;

    var machineDataText = File.ReadAllText(machineDataPath, Encoding.GetEncoding(1252));
    if (Count(machineDataText, VirtualMoveEnable) != 1 || Count(machineDataText, PhysicalMoveEnable) != 0) return 47;

    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
    File.Copy(inputPath, outputPath, false);

    var cell = solution.Open(outputPath, false, name => Logger.Info("MISSING_CATALOG=" + name));
    var controller = cell.Controllers.Single();
    Logger.Info("SOURCE_ROBOT=" + controller.Machines.Robot.DisplayName);
    Logger.Info("SOURCE_FIRMWARE=" + controller.FirmwareVersion);
    Logger.Info("SOURCE_OPTIONS=" + string.Join(";", controller.Options.Select(x => x.Name + "|" + x.Version)));
    if (controller.Machines.Robot.DisplayName != "KR 210 R2700-2 C01"
        || controller.FirmwareVersion != "8.7.8"
        || controller.Options.Count != 2
        || !controller.Options.Any(x => x.Name == "LoadDataDetermination" && x.Version.ToString() == "7.2.10.285")
        || !controller.Options.Any(x => x.Name == "KUKA.PROFINET S" && x.Version.ToString() == "6.0.1.20")) return 43;

    foreach (var option in controller.Options.ToArray()) option.Remove();
    foreach (var path in removableFiles)
    {
        var file = controller.FileSystem.GetFile(path);
        Logger.Info("REMOVE_FIELD_BUS_FILE=" + path + "|exists=" + file.Exists);
        if (file.Exists) file.Remove();
    }

    var krcIo = controller.FileSystem.GetFile(@"Config\User\Common\KRC_IO.xml");
    var machineData = controller.FileSystem.GetFile(@"KRC\STEU\Mada\$machine.dat");
    if (!krcIo.Exists || !machineData.Exists) return 44;
    krcIo.WriteAllText(File.ReadAllText(cleanKrcIoPath, Encoding.UTF8), new UTF8Encoding(true));
    machineData.WriteAllText(machineDataText, Encoding.GetEncoding(1252));

    solution.Save();
    solution.Close(false);

    var generatedCell = solution.Open(outputPath, false, name => Logger.Info("MISSING_CATALOG_AFTER_SAVE=" + name));
    var generatedController = generatedCell.Controllers.Single();
    var accepted = generatedController.Machines.Robot.DisplayName == "KR 210 R2700-2 C01"
        && generatedController.FirmwareVersion == "8.7.8"
        && generatedController.Options.Count == 0
        && removableFiles.All(path => !generatedController.FileSystem.GetFile(path).Exists);
    solution.Close(false);

    if (!string.Equals(Sha256(inputPath), ExpectedSourceSha256, StringComparison.OrdinalIgnoreCase)) return 48;
    Logger.Info("SOURCE_UNCHANGED=true");
    Logger.Info("OUTPUT_IN_PROCESS_SHA256=" + Sha256(outputPath));
    Logger.Info("POST_PROCESS_STABLE_HASH_REQUIRED=true");
    return accepted ? 0 : 45;
}
catch (Exception exception)
{
    Logger.Error("CREATE_VIRTUAL_MOTION_PROFILE_FAILED=" + exception.GetType().FullName + ": " + exception.GetBaseException().Message);
    return 46;
}
finally
{
    if (solution.IsOpen()) solution.Close(false);
}
