using KukaRoboter.OnlineServicesFacade;
using KukaRoboter.Contracts.RuntimeManager;
using KukaRoboter.Contracts.DataAccess;
using System.Text;
using System.Threading;

var address = ScriptRunnerEnvironment.GetArgument<string>("address");
var action = ScriptRunnerEnvironment.GetArgument<string>("action");
var source = ScriptRunnerEnvironment.GetArgument<string>("source");
var data = ScriptRunnerEnvironment.GetArgument<string>("data");
var transactionRoot = ScriptRunnerEnvironment.GetArgument<string>("transactionroot");
var sourceRemote = transactionRoot + "\\" + Path.GetFileName(source);
var dataRemote = transactionRoot + "\\" + Path.GetFileName(data);
var exitCode = 60;

string B64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
string ProgramStem(string value)
{
    var normalized = (value ?? string.Empty).Replace('\\', '/');
    var leaf = normalized.Split('/').LastOrDefault() ?? string.Empty;
    return leaf.EndsWith(".SRC", StringComparison.OrdinalIgnoreCase) ? leaf.Substring(0, leaf.Length - 4) : leaf;
}
bool SameProgram(string selected, string requested) => !string.IsNullOrWhiteSpace(selected)
    && string.Equals(ProgramStem(selected), ProgramStem(requested), StringComparison.OrdinalIgnoreCase);

bool IsRuntimeManagerWarmupTimeout(Exception exception)
{
    for (var current = exception; current != null; current = current.InnerException)
    {
        if (current is TimeoutException) return true;
        if (current is OnlineServiceFaultException
            && current.Message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0) return true;
    }
    return false;
}

RuntimeInterpreter GetRobotInterpreterAfterWarmup(string controllerAddress, out RuntimeManagerFacade runtime)
{
    var deadline = DateTime.UtcNow.AddSeconds(180);
    var attempt = 0;
    Exception lastTimeout = null;
    runtime = null;
    while (DateTime.UtcNow < deadline)
    {
        attempt++;
        RuntimeManagerFacade candidate = null;
        try
        {
            candidate = new RuntimeManagerFacade(controllerAddress, (ICredentialProvider)null);
            var robot = candidate.GetInterpreter(InterpreterType.Robot);
            runtime = candidate;
            Logger.Info("RUNTIME_MANAGER_READY_ATTEMPT=" + attempt);
            return robot;
        }
        catch (Exception exception)
        {
            if (candidate != null) candidate.Dispose();
            if (!IsRuntimeManagerWarmupTimeout(exception)) throw;
            lastTimeout = exception;
            Logger.Info("RUNTIME_MANAGER_WARMUP_RETRY=" + attempt + "|" + B64(exception.GetType().FullName)
                + "|" + B64(exception.Message));
            if (DateTime.UtcNow.AddSeconds(5) >= deadline) break;
            Thread.Sleep(5000);
        }
    }

    throw new TimeoutException("RuntimeManager did not answer GetInterpreter during the bounded 180-second application warmup.", lastTimeout);
}

string ReadValue(DataAccessFacade data, string name)
{
    try
    {
        var value = data.Read(new[] { new DataIdentifier(name) }).FirstOrDefault();
        if (value == null || !value.Exists || !value.IsAvailable || value.CurrentValue == null || value.CurrentValue.Value == null)
            return "<unavailable>";
        return value.CurrentValue.Value.ToString();
    }
    catch (Exception exception)
    {
        return "<failed:" + exception.GetType().Name + ">";
    }
}

bool EnsureT1(string controllerAddress)
{
    using (var modeData = new DataAccessFacade(controllerAddress))
    {
        var identifier = new DataIdentifier("$MODE_OP");
        var before = ReadValue(modeData, "$MODE_OP");
        Logger.Info("PREPARE_MODE_BEFORE=" + B64(before));
        if (!string.Equals(before, "#T1", StringComparison.OrdinalIgnoreCase))
        {
            modeData.Write(new DataValue
            {
                Identifier = identifier,
                Values = new[] { new TimestampedValue("#T1") }
            });
        }

        var after = before;
        for (var index = 0; index < 20; index++)
        {
            after = ReadValue(modeData, "$MODE_OP");
            if (string.Equals(after, "#T1", StringComparison.OrdinalIgnoreCase)) break;
            Thread.Sleep(250);
        }

        var verified = string.Equals(after, "#T1", StringComparison.OrdinalIgnoreCase);
        Logger.Info("PREPARE_MODE_AFTER=" + B64(after));
        Logger.Info("PREPARE_T1_VERIFIED=" + verified);
        return verified;
    }
}

Logger.Info("KSS_COMPOSITION_TRANSACTION_BEGIN=True");
Logger.Info("ACTION=" + action);
Logger.Info("TRANSACTION_ROOT=" + transactionRoot);

try
{
    using (var repository = new FileHandlingFacade(address))
    {
        if (string.Equals(action, "prepare", StringComparison.OrdinalIgnoreCase) && !EnsureT1(address)) return 69;
        RuntimeManagerFacade runtime;
        var robot = GetRobotInterpreterAfterWarmup(address, out runtime);
        using (runtime)
        {
            if (string.Equals(action, "prepare", StringComparison.OrdinalIgnoreCase))
            {
                var parent = Path.GetDirectoryName(transactionRoot);
                if (!repository.DirectoryExists(parent)) return 61;
                if (repository.DirectoryExists(transactionRoot)) return 62;
                Logger.Info("PREPARE_SELECTED_BEFORE=" + B64(robot.SelectedProgram));
                if (!string.IsNullOrWhiteSpace(robot.SelectedProgram))
                {
                    robot.Deselect();
                    for (var index = 0; index < 20; index++)
                    {
                        Thread.Sleep(250);
                        robot = runtime.GetInterpreter(InterpreterType.Robot);
                        if (string.IsNullOrWhiteSpace(robot.SelectedProgram)) break;
                    }
                    robot = runtime.GetInterpreter(InterpreterType.Robot);
                    Logger.Info("PREPARE_DESELECTED=" + string.IsNullOrWhiteSpace(robot.SelectedProgram));
                    if (!string.IsNullOrWhiteSpace(robot.SelectedProgram)) return 63;
                }

                repository.CreateDirectory(transactionRoot);
                repository.Upload(data, dataRemote, false);
                repository.Upload(source, sourceRemote, false);
                var uploaded = repository.FileExists(dataRemote) && repository.FileExists(sourceRemote);
                Logger.Info("UPLOAD_VERIFIED=" + uploaded);
                if (!uploaded) return 64;

                robot.Select(sourceRemote);
                for (var index = 0; index < 40; index++)
                {
                    Thread.Sleep(250);
                    robot = runtime.GetInterpreter(InterpreterType.Robot);
                    if (!string.IsNullOrWhiteSpace(robot.SelectedProgram) && robot.State != ProStates.Free) break;
                }

                robot = runtime.GetInterpreter(InterpreterType.Robot);
                robot.SetProgramMode(ProModes.Go);
                for (var index = 0; index < 20; index++)
                {
                    Thread.Sleep(250);
                    robot = runtime.GetInterpreter(InterpreterType.Robot);
                    if (robot.Mode == ProModes.Go) break;
                }

                robot = runtime.GetInterpreter(InterpreterType.Robot);
                var selected = SameProgram(robot.SelectedProgram, sourceRemote);
                var programStem = ProgramStem(sourceRemote);
                var noErrors = !runtime.GetModulesWithErrors()
                    .Any(module => module != null && module.IndexOf(programStem, StringComparison.OrdinalIgnoreCase) >= 0);
                var ready = selected && robot.Mode == ProModes.Go && noErrors;
                Logger.Info("SELECTED_PROGRAM=" + B64(robot.SelectedProgram));
                Logger.Info("GO_CONFIRMED=" + (robot.Mode == ProModes.Go));
                Logger.Info("NO_RELATED_ERRORS=" + noErrors);
                Logger.Info("PREPARE_VERIFIED=" + ready);
                exitCode = ready ? 0 : 65;
            }
            else if (string.Equals(action, "cleanup", StringComparison.OrdinalIgnoreCase))
            {
                if (SameProgram(robot.SelectedProgram, sourceRemote))
                {
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
                    Thread.Sleep(750);
                }

                if (repository.FileExists(sourceRemote)) repository.DeleteFile(sourceRemote);
                if (repository.FileExists(dataRemote)) repository.DeleteFile(dataRemote);
                if (repository.DirectoryExists(transactionRoot)) repository.DeleteDirectory(transactionRoot);

                robot = runtime.GetInterpreter(InterpreterType.Robot);
                var clean = !repository.DirectoryExists(transactionRoot)
                    && !repository.FileExists(sourceRemote)
                    && !repository.FileExists(dataRemote)
                    && !SameProgram(robot.SelectedProgram, sourceRemote);
                Logger.Info("CLEANUP_VERIFIED=" + clean);
                exitCode = clean ? 0 : 66;
            }
            else if (string.Equals(action, "diagnose", StringComparison.OrdinalIgnoreCase))
            {
                using (var data = new DataAccessFacade(address))
                using (var messages = new RuntimeMessageWindowFacade(address))
                {
                    robot = runtime.GetInterpreter(InterpreterType.Robot);
                    Logger.Info("DIAG_MODE=" + robot.Mode);
                    Logger.Info("DIAG_STATE=" + robot.State);
                    Logger.Info("DIAG_SELECTED_PROGRAM=" + B64(robot.SelectedProgram));
                    foreach (var name in new[] { "$MODE_OP", "$USER_LEVEL", "$DRIVES_ON", "$PERI_RDY", "$COULD_START_MOTION", "$ON_PATH", "$PRO_STATE1", "$PRO_MODE1", "$AXIS_ACT", "$POS_ACT" })
                        Logger.Info("DIAG_DATA=" + name + "|" + B64(ReadValue(data, name)));
                    var snapshot = messages.GetMessages().OrderBy(item => item.Id).ToArray();
                    Logger.Info("DIAG_MESSAGE_COUNT=" + snapshot.Length);
                    foreach (var message in snapshot)
                        Logger.Info("DIAG_MESSAGE=" + message.Id + "|" + B64(message.MessageCode) + "|" + message.MessageNumber
                            + "|" + B64(message.Type.ToString()) + "|" + B64(message.Text) + "|" + B64(message.ResourceMessage));
                }
                exitCode = 0;
            }
            else
            {
                Logger.Info("UNSUPPORTED_ACTION=" + B64(action));
                exitCode = 67;
            }
        }
    }
}
catch (Exception exception)
{
    Logger.Error("KSS_COMPOSITION_TRANSACTION_FAILED=" + B64(exception.ToString()));
    exitCode = 68;
}

return exitCode;
