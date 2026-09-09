using KukaRoboter.OnlineServicesFacade;
using KukaRoboter.Contracts.RuntimeManager;
using System.Text;
using System.Threading;

var address = ScriptRunnerEnvironment.GetArgument<string>("address");
var validSrcLocal = ScriptRunnerEnvironment.GetArgument<string>("validsrc");
var validDatLocal = ScriptRunnerEnvironment.GetArgument<string>("validdat");
var invalidSrcLocal = ScriptRunnerEnvironment.GetArgument<string>("invalidsrc");
var invalidDatLocal = ScriptRunnerEnvironment.GetArgument<string>("invaliddat");
var transactionRoot = ScriptRunnerEnvironment.GetArgument<string>("transactionroot");
var validStem = Path.GetFileNameWithoutExtension(validSrcLocal);
var invalidStem = Path.GetFileNameWithoutExtension(invalidSrcLocal);
var validSrcRemote = transactionRoot + "\\" + Path.GetFileName(validSrcLocal);
var validDatRemote = transactionRoot + "\\" + Path.GetFileName(validDatLocal);
var invalidSrcRemote = transactionRoot + "\\" + Path.GetFileName(invalidSrcLocal);
var invalidDatRemote = transactionRoot + "\\" + Path.GetFileName(invalidDatLocal);
var createdRoot = false;
var cleanupVerified = false;
var validAccepted = false;
var invalidRejected = false;
var validErrorCount = 0;
var invalidErrorCount = 0;
var exitCode = 46;
var selectionAttempted = false;
IRuntimeInterpreter robot = null;

string B64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));

int EmitRelatedErrors(string label, RuntimeManagerFacade runtime, string stem)
{
    var errors = new List<KukaRoboter.Contracts.RuntimeManager.ErrorInformation>();
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

void DeselectOwnProgram()
{
    if (robot == null || !selectionAttempted)
    {
        return;
    }

    robot.Deselect();
    Thread.Sleep(500);
    selectionAttempted = false;
}

Logger.Info("KRL_NATIVE_DIAGNOSTIC_BEGIN=True");
Logger.Info("ADDRESS=" + address);
Logger.Info("TRANSACTION_ROOT=" + transactionRoot);

try
{
    using (var repository = new FileHandlingFacade(address))
    using (var runtime = new RuntimeManagerFacade(address, (ICredentialProvider)null))
    using (var messages = new RuntimeMessageWindowFacade(address))
    {
        var parent = Path.GetDirectoryName(transactionRoot);
        if (!repository.DirectoryExists(parent))
        {
            Logger.Error("PARENT_MISSING=" + parent);
            return 43;
        }

        if (repository.DirectoryExists(transactionRoot))
        {
            Logger.Error("TRANSACTION_COLLISION=" + transactionRoot);
            return 44;
        }

        robot = runtime.GetInterpreter(InterpreterType.Robot);
        Logger.Info("ROBOT_STATE_BEFORE=" + robot.State);
        Logger.Info("ROBOT_SELECTED_BEFORE_BASE64=" + B64(robot.SelectedProgram));
        if (!string.IsNullOrWhiteSpace(robot.SelectedProgram))
        {
            Logger.Error("ROBOT_INTERPRETER_NOT_FREE=True");
            return 45;
        }

        var beforeMessageIds = new HashSet<int>();
        var messageWindowAvailable = false;
        try
        {
            beforeMessageIds = new HashSet<int>(messages.GetMessages().Select(item => item.Id));
            messageWindowAvailable = true;
        }
        catch (Exception messageException)
        {
            Logger.Info("MESSAGE_WINDOW_UNAVAILABLE=" + B64(messageException.GetType().FullName + ": " + messageException.Message));
        }

        repository.CreateDirectory(transactionRoot);
        createdRoot = true;
        repository.Upload(validSrcLocal, validSrcRemote, false);
        repository.Upload(validDatLocal, validDatRemote, false);
        repository.Upload(invalidSrcLocal, invalidSrcRemote, false);
        repository.Upload(invalidDatLocal, invalidDatRemote, false);
        var uploaded = new[] { validSrcRemote, validDatRemote, invalidSrcRemote, invalidDatRemote }.All(repository.FileExists);
        Logger.Info("UPLOAD_VERIFIED=" + uploaded);
        if (uploaded)
        {
            var validSelectSucceeded = false;
            try
            {
                selectionAttempted = true;
                robot.Select(validSrcRemote);
                Thread.Sleep(1000);
                validSelectSucceeded = true;
                Logger.Info("VALID_SELECTED_BASE64=" + B64(robot.SelectedProgram));
                validErrorCount = EmitRelatedErrors("VALID", runtime, validStem);
                validAccepted = validSelectSucceeded && validErrorCount == 0;
            }
            catch (Exception ex)
            {
                Logger.Info("VALID_SELECT_EXCEPTION=" + B64(ex.GetType().FullName + ": " + ex.Message));
                validErrorCount = EmitRelatedErrors("VALID", runtime, validStem);
            }
            finally
            {
                DeselectOwnProgram();
            }

            Logger.Info("VALID_SELECT_SUCCEEDED=" + validSelectSucceeded);
            Logger.Info("VALID_ACCEPTED=" + validAccepted);

            var invalidSelectSucceeded = false;
            var invalidSelectThrew = false;
            try
            {
                selectionAttempted = true;
                robot.Select(invalidSrcRemote);
                Thread.Sleep(1000);
                invalidSelectSucceeded = true;
                Logger.Info("INVALID_SELECTED_BASE64=" + B64(robot.SelectedProgram));
                invalidErrorCount = EmitRelatedErrors("INVALID", runtime, invalidStem);
            }
            catch (Exception ex)
            {
                invalidSelectThrew = true;
                Logger.Info("INVALID_SELECT_EXCEPTION=" + B64(ex.GetType().FullName + ": " + ex.Message));
                invalidErrorCount = EmitRelatedErrors("INVALID", runtime, invalidStem);
            }
            finally
            {
                DeselectOwnProgram();
            }

            invalidRejected = invalidSelectThrew || invalidErrorCount > 0;
            Logger.Info("INVALID_SELECT_SUCCEEDED=" + invalidSelectSucceeded);
            Logger.Info("INVALID_SELECT_THREW=" + invalidSelectThrew);
            Logger.Info("INVALID_REJECTED=" + invalidRejected);

            if (messageWindowAvailable)
            {
                try
                {
                    foreach (var message in messages.GetMessages().Where(item => !beforeMessageIds.Contains(item.Id)))
                    {
                        Logger.Info("NEW_MESSAGE=" + message.Id + "|" + B64(message.MessageCode) + "|" + message.MessageNumber + "|" +
                            B64(message.Type.ToString()) + "|" + B64(message.Text) + "|" + B64(message.ResourceMessage));
                    }
                }
                catch (Exception messageException)
                {
                    Logger.Info("MESSAGE_WINDOW_AFTER_UNAVAILABLE=" + B64(messageException.GetType().FullName + ": " + messageException.Message));
                }
            }

            exitCode = validAccepted && invalidRejected ? 0 : 46;
        }
    }
}
catch (Exception ex)
{
    Logger.Error("KRL_NATIVE_DIAGNOSTIC_FAILED=" + B64(ex.ToString()));
    exitCode = 42;
}
finally
{
    try
    {
        DeselectOwnProgram();
        if (!createdRoot)
        {
            cleanupVerified = true;
        }
        else
        {
            using (var cleanupRepository = new FileHandlingFacade(address))
            {
                foreach (var path in new[] { validSrcRemote, validDatRemote, invalidSrcRemote, invalidDatRemote })
                {
                    if (cleanupRepository.FileExists(path)) cleanupRepository.DeleteFile(path);
                }

                if (cleanupRepository.DirectoryExists(transactionRoot)) cleanupRepository.DeleteDirectory(transactionRoot);
                cleanupVerified = !cleanupRepository.DirectoryExists(transactionRoot) &&
                    !new[] { validSrcRemote, validDatRemote, invalidSrcRemote, invalidDatRemote }.Any(cleanupRepository.FileExists);
            }
        }
    }
    catch (Exception cleanupException)
    {
        Logger.Error("CLEANUP_EXCEPTION=" + B64(cleanupException.GetType().FullName + ": " + cleanupException.Message));
        cleanupVerified = false;
    }

    Logger.Info("VALID_ACCEPTED_FINAL=" + validAccepted);
    Logger.Info("INVALID_REJECTED_FINAL=" + invalidRejected);
    Logger.Info("CLEANUP_VERIFIED=" + cleanupVerified);
}

if (!cleanupVerified) return 47;
return exitCode;
