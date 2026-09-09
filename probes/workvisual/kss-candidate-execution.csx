using KukaRoboter.Contracts.DataAccess;
using KukaRoboter.Contracts.DeviceInfoService;
using KukaRoboter.Contracts.RuntimeManager;
using KukaRoboter.OnlineServicesFacade;
using Kuka.WorkVisual.Scripting.KrcOnline;
using Kuka.WorkVisual.Scripting.KrcOnline.ScriptingObjects;
using System.Diagnostics;
using System.Text;
using System.Threading;

var address = ScriptRunnerEnvironment.GetArgument<string>("address");
var sourceLocal = ScriptRunnerEnvironment.GetArgument<string>("src");
var dataLocal = ScriptRunnerEnvironment.GetArgument<string>("dat");
var transactionRoot = ScriptRunnerEnvironment.GetArgument<string>("transactionroot");
var maximumStarts = ScriptRunnerEnvironment.GetArgument<int>("maxstarts");
var expectedRobot = Encoding.UTF8.GetString(Convert.FromBase64String(
    ScriptRunnerEnvironment.GetArgument<string>("expectedrobotb64")));
var expectedKss = Encoding.UTF8.GetString(Convert.FromBase64String(
    ScriptRunnerEnvironment.GetArgument<string>("expectedkssb64")));
var expectedProject = Encoding.UTF8.GetString(Convert.FromBase64String(
    ScriptRunnerEnvironment.GetArgument<string>("expectedprojectb64")));
var sourceRemote = transactionRoot + "\\" + Path.GetFileName(sourceLocal);
var dataRemote = transactionRoot + "\\" + Path.GetFileName(dataLocal);
var programStem = Path.GetFileNameWithoutExtension(sourceLocal);
var createdRoot = false;
var selected = false;
var cleanupVerified = false;
var completed = false;
var rejected = false;
var startCount = 0;
var mode = string.Empty;
var finalState = string.Empty;
var disposition = "InfrastructureFailed";
var exitCode = 50;

string B64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));

int EmitRelatedErrors(RuntimeManagerFacade runtime)
{
    var errors = new List<ErrorInformation>();
    foreach (var module in runtime.GetModulesWithErrors()
        .Where(item => item != null && item.IndexOf(programStem, StringComparison.OrdinalIgnoreCase) >= 0)
        .Distinct(StringComparer.OrdinalIgnoreCase))
    {
        errors.AddRange(runtime.GetErrors(module));
    }

    var unique = errors
        .GroupBy(error => string.Join("|", error.KrlModule, error.ErrorNumber, error.Line, error.Column, error.Description, error.Parameter), StringComparer.Ordinal)
        .Select(group => group.First())
        .ToArray();
    foreach (var error in unique)
    {
        Logger.Info("DIAGNOSTIC=" + B64(error.KrlModule) + "|" + error.ErrorNumber + "|" +
            error.Line + "|" + error.Column + "|" + B64(error.Description) + "|" + B64(error.Parameter));
    }

    Logger.Info("DIAGNOSTIC_COUNT=" + unique.Length);
    return unique.Length;
}

string ReadControllerValue(DataAccessFacade data, string name)
{
    try
    {
        var value = data.Read(new[] { new DataIdentifier(name) }).FirstOrDefault();
        if (value == null || !value.Exists || !value.IsAvailable || value.CurrentValue == null
            || value.CurrentValue.Value == null)
        {
            return "<unavailable>";
        }

        return value.CurrentValue.Value.ToString();
    }
    catch (Exception exception)
    {
        Logger.Info("DATA_READ_EXCEPTION=" + B64(name) + "|" + B64(exception.ToString()));
        return "<failed:" + exception.GetType().Name + ">";
    }
}

bool EnsureT1(string controllerAddress)
{
    using (var data = new DataAccessFacade(controllerAddress))
    {
        var before = ReadControllerValue(data, "$MODE_OP");
        Logger.Info("OPERATION_MODE_BEFORE=" + B64(before));
        if (!string.Equals(before, "#T1", StringComparison.OrdinalIgnoreCase))
        {
            data.Write(new DataValue
            {
                Identifier = new DataIdentifier("$MODE_OP"),
                Values = new[] { new TimestampedValue("#T1") }
            });
        }

        var after = before;
        for (var index = 0; index < 40; index++)
        {
            after = ReadControllerValue(data, "$MODE_OP");
            if (string.Equals(after, "#T1", StringComparison.OrdinalIgnoreCase)) break;
            Thread.Sleep(250);
        }

        var verified = string.Equals(after, "#T1", StringComparison.OrdinalIgnoreCase);
        Logger.Info("OPERATION_MODE_AFTER=" + B64(after));
        Logger.Info("T1_VERIFIED=" + verified);
        return verified;
    }
}

Logger.Info("KSS_CANDIDATE_EXECUTION_BEGIN=True");
Logger.Info("ADDRESS=" + address);
Logger.Info("TRANSACTION_ROOT=" + transactionRoot);

try
{
    IScriptingOnlineController onlineAccess = ScriptRunner.GetOnlineController(address);
    var scriptingController = (ScriptingOnlineController)onlineAccess;
    using (var deviceFacade = new DeviceInfoFacade(scriptingController.DeviceInfo))
    {
        var deviceInfo = deviceFacade.GetDeviceInfo();
        Logger.Info("PROFILE_ROBOT=" + B64(deviceInfo.RoboterType));
        Logger.Info("PROFILE_KSS=" + B64(deviceInfo.FirmwareVersion));
        Logger.Info("PROFILE_PROJECT=" + B64(deviceInfo.CurrentProjectName));
        if (!string.Equals(deviceInfo.RoboterType, expectedRobot, StringComparison.Ordinal)
            || !string.Equals(deviceInfo.FirmwareVersion, expectedKss, StringComparison.Ordinal)
            || !string.Equals(deviceInfo.CurrentProjectName, expectedProject, StringComparison.Ordinal))
        {
            Logger.Info("PROFILE_MATCHED=False");
            return 42;
        }

        Logger.Info("PROFILE_MATCHED=True");
    }

    using (var repository = new FileHandlingFacade(address))
    using (var runtime = new RuntimeManagerFacade(address, (ICredentialProvider)null))
    {
        if (!EnsureT1(address)) return 52;
        var parent = Path.GetDirectoryName(transactionRoot);
        if (!repository.DirectoryExists(parent)) return 43;
        if (repository.DirectoryExists(transactionRoot)) return 44;
        var robot = runtime.GetInterpreter(InterpreterType.Robot);
        if (!string.IsNullOrWhiteSpace(robot.SelectedProgram)) return 45;

        repository.CreateDirectory(transactionRoot);
        createdRoot = true;
        repository.Upload(dataLocal, dataRemote, false);
        repository.Upload(sourceLocal, sourceRemote, false);
        var uploaded = repository.FileExists(dataRemote) && repository.FileExists(sourceRemote);
        Logger.Info("UPLOAD_VERIFIED=" + uploaded);
        if (!uploaded) return 46;

        try
        {
            robot.Select(sourceRemote);
            selected = true;
            for (var i = 0; i < 40; i++)
            {
                Thread.Sleep(250);
                robot = runtime.GetInterpreter(InterpreterType.Robot);
                if (!string.IsNullOrWhiteSpace(robot.SelectedProgram) && robot.State != ProStates.Free) break;
            }
        }
        catch (Exception ex)
        {
            Logger.Info("SELECT_EXCEPTION=" + B64(ex.ToString()));
        }

        robot = runtime.GetInterpreter(InterpreterType.Robot);
        var selectedSucceeded = !string.IsNullOrWhiteSpace(robot.SelectedProgram)
            && robot.State != ProStates.Free;
        Logger.Info("SELECT_SUCCEEDED=" + selectedSucceeded);
        var diagnosticCount = EmitRelatedErrors(runtime);
        if (!selectedSucceeded || diagnosticCount > 0)
        {
            rejected = diagnosticCount > 0;
            disposition = rejected ? "RejectedByNativeKss" : "InfrastructureFailed";
            exitCode = rejected ? 0 : 47;
        }
        else
        {
            robot.SetProgramMode(ProModes.Go);
            for (var i = 0; i < 20; i++)
            {
                Thread.Sleep(250);
                robot = runtime.GetInterpreter(InterpreterType.Robot);
                if (robot.Mode == ProModes.Go) break;
            }

            robot = runtime.GetInterpreter(InterpreterType.Robot);
            mode = robot.Mode.ToString();
            Logger.Info("MODE=" + mode);
            if (robot.Mode != ProModes.Go) return 48;

            while (startCount < maximumStarts)
            {
                robot = runtime.GetInterpreter(InterpreterType.Robot);
                if (robot.State == ProStates.End)
                {
                    completed = true;
                    break;
                }

                if (robot.State != ProStates.Reset && robot.State != ProStates.Stop) break;
                try
                {
                    robot.Start();
                    startCount++;
                    Logger.Info("START_SUCCEEDED=" + startCount);
                }
                catch (Exception ex)
                {
                    Logger.Info("START_EXCEPTION=" + B64(ex.ToString()));
                    break;
                }

                var beforeLine = robot.Line == null ? string.Empty : robot.Line.File + ":" + robot.Line.Line;
                var observedActive = false;
                var poll = Stopwatch.StartNew();
                while (poll.Elapsed < TimeSpan.FromSeconds(20))
                {
                    Thread.Sleep(100);
                    robot = runtime.GetInterpreter(InterpreterType.Robot);
                    if (robot.State == ProStates.Active) observedActive = true;
                    if (robot.State == ProStates.End)
                    {
                        completed = true;
                        break;
                    }

                    var currentLine = robot.Line == null ? string.Empty : robot.Line.File + ":" + robot.Line.Line;
                    if (robot.State == ProStates.Stop && (observedActive || currentLine != beforeLine)) break;
                }

                if (completed) break;
                Thread.Sleep(500);
            }

            robot = runtime.GetInterpreter(InterpreterType.Robot);
            completed = completed || robot.State == ProStates.End;
            finalState = robot.State.ToString();
            disposition = completed ? "Executed" : "InfrastructureFailed";
            exitCode = completed ? 0 : 49;
        }
    }
}
catch (Exception ex)
{
    Logger.Error("KSS_CANDIDATE_EXECUTION_FAILED=" + B64(ex.ToString()));
    exitCode = 50;
}
finally
{
    var interpreterCleanupVerified = !selected;
    var repositoryCleanupVerified = !createdRoot;
    try
    {
        if (selected)
        {
            using (var runtime = new RuntimeManagerFacade(address, (ICredentialProvider)null))
            {
                var robot = runtime.GetInterpreter(InterpreterType.Robot);
                Logger.Info("CLEANUP_INITIAL_STATE=" + robot.State);
                if (robot.State == ProStates.Active)
                {
                    robot.Stop();
                    for (var index = 0; index < 40; index++)
                    {
                        Thread.Sleep(250);
                        robot = runtime.GetInterpreter(InterpreterType.Robot);
                        if (robot.State != ProStates.Active) break;
                    }
                }

                robot = runtime.GetInterpreter(InterpreterType.Robot);
                if (robot.State != ProStates.Free && robot.State != ProStates.End)
                {
                    robot.Reset();
                    for (var index = 0; index < 40; index++)
                    {
                        Thread.Sleep(250);
                        robot = runtime.GetInterpreter(InterpreterType.Robot);
                        if (robot.State == ProStates.Reset || robot.State == ProStates.End || robot.State == ProStates.Free) break;
                    }
                }

                robot = runtime.GetInterpreter(InterpreterType.Robot);
                if (!string.IsNullOrWhiteSpace(robot.SelectedProgram)) robot.Deselect();
                for (var index = 0; index < 40; index++)
                {
                    Thread.Sleep(250);
                    robot = runtime.GetInterpreter(InterpreterType.Robot);
                    if (string.IsNullOrWhiteSpace(robot.SelectedProgram) && robot.State == ProStates.Free) break;
                }

                robot = runtime.GetInterpreter(InterpreterType.Robot);
                interpreterCleanupVerified = string.IsNullOrWhiteSpace(robot.SelectedProgram)
                    && robot.State == ProStates.Free;
                Logger.Info("CLEANUP_FINAL_STATE=" + robot.State);
                Logger.Info("INTERPRETER_CLEANUP_VERIFIED=" + interpreterCleanupVerified);
            }
        }
    }
    catch (Exception ex)
    {
        Logger.Error("INTERPRETER_CLEANUP_FAILED=" + B64(ex.ToString()));
        interpreterCleanupVerified = false;
    }

    try
    {
        if (createdRoot)
        {
            using (var repository = new FileHandlingFacade(address))
            {
                if (repository.FileExists(dataRemote)) repository.DeleteFile(dataRemote);
                if (repository.FileExists(sourceRemote)) repository.DeleteFile(sourceRemote);
                if (repository.DirectoryExists(transactionRoot)) repository.DeleteDirectory(transactionRoot);
                repositoryCleanupVerified = !repository.DirectoryExists(transactionRoot)
                    && !repository.FileExists(dataRemote) && !repository.FileExists(sourceRemote);
            }
        }
    }
    catch (Exception ex)
    {
        Logger.Error("REPOSITORY_CLEANUP_FAILED=" + B64(ex.ToString()));
        repositoryCleanupVerified = false;
    }

    cleanupVerified = interpreterCleanupVerified && repositoryCleanupVerified;
    Logger.Info("REPOSITORY_CLEANUP_VERIFIED=" + repositoryCleanupVerified);
    Logger.Info("START_COUNT=" + startCount);
    Logger.Info("FINAL_STATE=" + finalState);
    Logger.Info("COMPLETED=" + completed);
    Logger.Info("REJECTED=" + rejected);
    Logger.Info("DISPOSITION=" + disposition);
    Logger.Info("CLEANUP_VERIFIED=" + cleanupVerified);
}

if (!cleanupVerified) return 51;
return exitCode;
