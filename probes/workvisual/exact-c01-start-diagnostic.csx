using KukaRoboter.Contracts.DataAccess;
using KukaRoboter.Contracts.RuntimeManager;
using KukaRoboter.OnlineServicesFacade;
using System.Text;
using System.Threading;

var address = ScriptRunnerEnvironment.GetArgument<string>("address");
var source = ScriptRunnerEnvironment.GetArgument<string>("source");
var dataFile = ScriptRunnerEnvironment.GetArgument<string>("data");
var transactionRoot = ScriptRunnerEnvironment.GetArgument<string>("transactionroot");
var sourceRemote = transactionRoot + "\\" + Path.GetFileName(source);
var dataRemote = transactionRoot + "\\" + Path.GetFileName(dataFile);
var createdRoot = false;
var selected = false;
var exitCode = 40;

string B64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));

string ReadValue(DataAccessFacade facade, string name)
{
    try
    {
        var value = facade.Read(new[] { new DataIdentifier(name) }).FirstOrDefault();
        if (value == null || !value.Exists || !value.IsAvailable || value.CurrentValue == null || value.CurrentValue.Value == null)
            return "<unavailable>";
        return value.CurrentValue.Value.ToString();
    }
    catch (Exception exception)
    {
        return "<failed:" + exception.GetType().Name + ">";
    }
}

void EmitState(string label, RuntimeManagerFacade runtime, DataAccessFacade data)
{
    var robot = runtime.GetInterpreter(InterpreterType.Robot);
    Logger.Info("STATE=" + label + "|Mode=" + robot.Mode + "|State=" + robot.State
        + "|Selected=" + B64(robot.SelectedProgram)
        + "|Line=" + B64(robot.Line == null ? "<null>" : robot.Line.File + ":" + robot.Line.Line));
    foreach (var name in new[] { "$MODE_OP", "$USER_LEVEL", "$DRIVES_ON", "$PERI_RDY", "$COULD_START_MOTION", "$ON_PATH", "$PRO_STATE1", "$PRO_MODE1", "$AXIS_ACT", "$POS_ACT" })
        Logger.Info("DATA=" + label + "|" + name + "|" + B64(ReadValue(data, name)));
}

void EmitMessages(string label)
{
    using (var messages = new RuntimeMessageWindowFacade(address))
    {
        var snapshot = messages.GetMessages().OrderBy(item => item.Id).ToArray();
        Logger.Info("MESSAGE_COUNT=" + label + "|" + snapshot.Length);
        foreach (var message in snapshot)
            Logger.Info("MESSAGE=" + label + "|" + message.Id + "|" + B64(message.MessageCode) + "|" + message.MessageNumber
                + "|" + B64(message.Type.ToString()) + "|" + B64(message.Text) + "|" + B64(message.ResourceMessage));
    }
}

Logger.Info("EXACT_C01_START_DIAGNOSTIC_BEGIN=True");
Logger.Info("ADDRESS=" + address);

try
{
    using (var repository = new FileHandlingFacade(address))
    using (var runtime = new RuntimeManagerFacade(address, (ICredentialProvider)null))
    using (var data = new DataAccessFacade(address))
    {
        if (repository.DirectoryExists(transactionRoot)) return 41;
        if (!string.IsNullOrWhiteSpace(runtime.GetInterpreter(InterpreterType.Robot).SelectedProgram)) return 42;
        EmitState("baseline", runtime, data);
        EmitMessages("baseline");

        repository.CreateDirectory(transactionRoot);
        createdRoot = true;
        repository.Upload(dataFile, dataRemote, false);
        repository.Upload(source, sourceRemote, false);
        if (!repository.FileExists(dataRemote) || !repository.FileExists(sourceRemote)) return 43;

        var robot = runtime.GetInterpreter(InterpreterType.Robot);
        robot.Select(sourceRemote);
        selected = true;
        for (var index = 0; index < 40; index++)
        {
            Thread.Sleep(250);
            robot = runtime.GetInterpreter(InterpreterType.Robot);
            if (!string.IsNullOrWhiteSpace(robot.SelectedProgram) && robot.State != ProStates.Free) break;
        }
        robot.SetProgramMode(ProModes.Go);
        Thread.Sleep(500);
        EmitState("before-start", runtime, data);
        EmitMessages("before-start");

        try
        {
            runtime.GetInterpreter(InterpreterType.Robot).Start();
            Logger.Info("START_RESULT=Succeeded");
            Thread.Sleep(2000);
            exitCode = 0;
        }
        catch (Exception exception)
        {
            Logger.Info("START_RESULT=Failed|" + B64(exception.ToString()));
            exitCode = 44;
        }

        EmitState("after-start", runtime, data);
        EmitMessages("after-start");
    }
}
catch (Exception exception)
{
    Logger.Error("EXACT_C01_START_DIAGNOSTIC_FAILED=" + B64(exception.ToString()));
    exitCode = 45;
}
finally
{
    try
    {
        if (selected)
        {
            using (var runtime = new RuntimeManagerFacade(address, (ICredentialProvider)null))
            {
                runtime.GetInterpreter(InterpreterType.Robot).Deselect();
                Thread.Sleep(750);
            }
        }
        if (createdRoot)
        {
            using (var repository = new FileHandlingFacade(address))
            {
                if (repository.FileExists(sourceRemote)) repository.DeleteFile(sourceRemote);
                if (repository.FileExists(dataRemote)) repository.DeleteFile(dataRemote);
                if (repository.DirectoryExists(transactionRoot)) repository.DeleteDirectory(transactionRoot);
            }
        }
        Logger.Info("CLEANUP_VERIFIED=True");
    }
    catch (Exception exception)
    {
        Logger.Error("CLEANUP_FAILED=" + B64(exception.ToString()));
        exitCode = 46;
    }
}

return exitCode;
