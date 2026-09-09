using KukaRoboter.OnlineServicesFacade;
using KukaRoboter.Contracts.RuntimeManager;
using KukaRoboter.Contracts.DataAccess;
using System.Diagnostics;
using System.Text;
using System.Threading;

var address = ScriptRunnerEnvironment.GetArgument<string>("address");
var validSrcLocal = ScriptRunnerEnvironment.GetArgument<string>("validsrc");
var validDatLocal = ScriptRunnerEnvironment.GetArgument<string>("validdat");
var invalidSrcLocal = ScriptRunnerEnvironment.GetArgument<string>("invalidsrc");
var invalidDatLocal = ScriptRunnerEnvironment.GetArgument<string>("invaliddat");
var transactionRoot = ScriptRunnerEnvironment.GetArgument<string>("transactionroot");
var validSrcRemote = transactionRoot + "\\" + Path.GetFileName(validSrcLocal);
var validDatRemote = transactionRoot + "\\" + Path.GetFileName(validDatLocal);
var invalidSrcRemote = transactionRoot + "\\" + Path.GetFileName(invalidSrcLocal);
var invalidDatRemote = transactionRoot + "\\" + Path.GetFileName(invalidDatLocal);
var createdRoot = false;
var selected = false;
var cleanupVerified = false;
var validCompleted = false;
var invalidRejected = false;
var startCount = 0;
var exitCode = 50;

string B64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));

string LineText(RuntimeInterpreter robot) => robot == null || robot.Line == null
    ? "<null>"
    : robot.Line.File + ":" + robot.Line.Line;

string ReadValue(DataAccessFacade data, string name)
{
    try
    {
        var value = data.Read(new[] { new DataIdentifier(name) }).FirstOrDefault();
        if (value == null || !value.Exists || !value.IsAvailable
            || value.CurrentValue == null || value.CurrentValue.Value == null)
        {
            return "<unavailable>";
        }

        return value.CurrentValue.Value.ToString();
    }
    catch (Exception ex)
    {
        return "<failed:" + ex.GetType().Name + ">";
    }
}

void EmitTrace(string label, int sequence, RuntimeInterpreter robot, DataAccessFacade data)
{
    Logger.Info("TRACE=" + sequence + "|" + label + "|" + robot.Mode + "|" + robot.State + "|" +
        B64(LineText(robot)) + "|" + B64(ReadValue(data, "$AXIS_ACT")) + "|" +
        B64(ReadValue(data, "$POS_ACT")) + "|" + B64(ReadValue(data, "$PRO_STATE1")) + "|" +
        B64(ReadValue(data, "$PERI_RDY")) + "|" + B64(ReadValue(data, "$COULD_START_MOTION")));
}

int EmitRelatedErrors(string label, RuntimeManagerFacade runtime, string stem)
{
    var errors = new List<ErrorInformation>();
    foreach (var module in runtime.GetModulesWithErrors()
        .Where(item => item != null && item.IndexOf(stem, StringComparison.OrdinalIgnoreCase) >= 0)
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
        Logger.Info(label + "_ERROR=" + B64(error.KrlModule) + "|" + error.ErrorNumber + "|" +
            error.Line + "|" + error.Column + "|" + B64(error.Description) + "|" + B64(error.Parameter));
    }

    Logger.Info(label + "_ERROR_COUNT=" + unique.Length);
    return unique.Length;
}

Logger.Info("KSS_BOUNDED_EXECUTION_BEGIN=True");
Logger.Info("ADDRESS=" + address);
Logger.Info("TRANSACTION_ROOT=" + transactionRoot);

try
{
    using (var repository = new FileHandlingFacade(address))
    using (var runtime = new RuntimeManagerFacade(address, (ICredentialProvider)null))
    using (var data = new DataAccessFacade(address))
    {
        var parent = Path.GetDirectoryName(transactionRoot);
        if (!repository.DirectoryExists(parent)) return 43;
        if (repository.DirectoryExists(transactionRoot)) return 44;

        var robot = runtime.GetInterpreter(InterpreterType.Robot);
        Logger.Info("ROBOT_STATE_BEFORE=" + robot.State);
        Logger.Info("ROBOT_SELECTED_BEFORE=" + B64(robot.SelectedProgram));
        if (!string.IsNullOrWhiteSpace(robot.SelectedProgram)) return 45;

        repository.CreateDirectory(transactionRoot);
        createdRoot = true;
        repository.Upload(validDatLocal, validDatRemote, false);
        repository.Upload(validSrcLocal, validSrcRemote, false);
        repository.Upload(invalidDatLocal, invalidDatRemote, false);
        repository.Upload(invalidSrcLocal, invalidSrcRemote, false);
        var uploaded = new[] { validDatRemote, validSrcRemote, invalidDatRemote, invalidSrcRemote }
            .All(repository.FileExists);
        Logger.Info("UPLOAD_VERIFIED=" + uploaded);
        if (!uploaded) return 46;

        robot.Select(validSrcRemote);
        selected = true;
        for (var i = 0; i < 40; i++)
        {
            Thread.Sleep(250);
            robot = runtime.GetInterpreter(InterpreterType.Robot);
            if (!string.IsNullOrWhiteSpace(robot.SelectedProgram) && robot.State != ProStates.Free) break;
        }

        robot = runtime.GetInterpreter(InterpreterType.Robot);
        var validSelectSucceeded = !string.IsNullOrWhiteSpace(robot.SelectedProgram)
            && robot.State != ProStates.Free;
        var validErrors = EmitRelatedErrors("VALID", runtime, "LAB_OL_KR3");
        Logger.Info("VALID_SELECT_SUCCEEDED=" + validSelectSucceeded);
        Logger.Info("VALID_ACCEPTED=" + (validSelectSucceeded && validErrors == 0));
        if (!validSelectSucceeded || validErrors != 0) return 47;

        robot.SetProgramMode(ProModes.Go);
        for (var i = 0; i < 20; i++)
        {
            Thread.Sleep(250);
            robot = runtime.GetInterpreter(InterpreterType.Robot);
            if (robot.Mode == ProModes.Go) break;
        }

        robot = runtime.GetInterpreter(InterpreterType.Robot);
        var goConfirmed = robot.Mode == ProModes.Go;
        Logger.Info("GO_CONFIRMED=" + goConfirmed);
        if (!goConfirmed) return 48;

        var traceSequence = 0;
        EmitTrace("before-start", traceSequence++, robot, data);
        for (var pulse = 1; pulse <= 2; pulse++)
        {
            robot = runtime.GetInterpreter(InterpreterType.Robot);
            if (robot.State == ProStates.End)
            {
                validCompleted = true;
                break;
            }

            if (robot.State != ProStates.Reset && robot.State != ProStates.Stop)
            {
                Logger.Info("STATE_BLOCK=" + robot.State);
                break;
            }

            var beforeLine = LineText(robot);
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

            var observedActive = false;
            var poll = Stopwatch.StartNew();
            var sample = 0;
            while (poll.Elapsed < TimeSpan.FromSeconds(20))
            {
                Thread.Sleep(100);
                robot = runtime.GetInterpreter(InterpreterType.Robot);
                if (robot.State == ProStates.Active) observedActive = true;
                sample++;
                if (robot.State == ProStates.End)
                {
                    validCompleted = true;
                    break;
                }

                if (sample > 2 && robot.State == ProStates.Stop
                    && (observedActive || LineText(robot) != beforeLine))
                {
                    break;
                }
            }

            robot = runtime.GetInterpreter(InterpreterType.Robot);
            EmitTrace("after-start-" + startCount, traceSequence++, robot, data);
            if (validCompleted) break;
            Thread.Sleep(1000);
        }

        robot = runtime.GetInterpreter(InterpreterType.Robot);
        validCompleted = validCompleted || robot.State == ProStates.End;
        Logger.Info("START_COUNT=" + startCount);
        Logger.Info("VALID_FINAL_MODE=" + robot.Mode);
        Logger.Info("VALID_FINAL_STATE=" + robot.State);
        Logger.Info("VALID_COMPLETED=" + validCompleted);

        robot.Deselect();
        selected = false;
        Thread.Sleep(750);
        robot = runtime.GetInterpreter(InterpreterType.Robot);

        try
        {
            robot.Select(invalidSrcRemote);
            selected = true;
            Thread.Sleep(1000);
            invalidRejected = EmitRelatedErrors("INVALID", runtime, "LAB_MISSING_TARGET") > 0;
        }
        catch (Exception ex)
        {
            Logger.Info("INVALID_SELECT_EXCEPTION=" + B64(ex.ToString()));
            invalidRejected = EmitRelatedErrors("INVALID", runtime, "LAB_MISSING_TARGET") > 0;
        }

        Logger.Info("INVALID_REJECTED=" + invalidRejected);
        exitCode = validCompleted && invalidRejected ? 0 : 49;
    }
}
catch (Exception ex)
{
    Logger.Error("KSS_BOUNDED_EXECUTION_FAILED=" + B64(ex.ToString()));
    exitCode = 50;
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
                foreach (var path in new[] { validDatRemote, validSrcRemote, invalidDatRemote, invalidSrcRemote })
                {
                    if (repository.FileExists(path)) repository.DeleteFile(path);
                }

                if (repository.DirectoryExists(transactionRoot)) repository.DeleteDirectory(transactionRoot);
                cleanupVerified = !repository.DirectoryExists(transactionRoot)
                    && !new[] { validDatRemote, validSrcRemote, invalidDatRemote, invalidSrcRemote }
                        .Any(repository.FileExists);
            }
        }
        else
        {
            cleanupVerified = true;
        }
    }
    catch (Exception ex)
    {
        Logger.Error("CLEANUP_FAILED=" + B64(ex.ToString()));
        cleanupVerified = false;
    }

    Logger.Info("VALID_COMPLETED_FINAL=" + validCompleted);
    Logger.Info("INVALID_REJECTED_FINAL=" + invalidRejected);
    Logger.Info("CLEANUP_VERIFIED=" + cleanupVerified);
}

if (!cleanupVerified) return 51;
return exitCode;
