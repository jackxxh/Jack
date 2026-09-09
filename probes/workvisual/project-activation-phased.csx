using KukaRoboter.Contracts.DeviceInfoService;
using KukaRoboter.OnlineServicesFacade;
using Kuka.WorkVisual.Scripting.KrcOnline;
using Kuka.WorkVisual.Scripting.KrcOnline.ScriptingObjects;
using System.Text;

var address = ScriptRunnerEnvironment.GetArgument<string>("address");
var projectName = ScriptRunnerEnvironment.GetArgument<string>("projectname");
var authorizationRef = ScriptRunnerEnvironment.GetArgument<string>("authorizationref");

string B64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));

void EmitFaultEvidence(string label, OnlineServiceFaultException fault)
{
    if (fault == null || fault.Error == null)
    {
        Logger.Info(label + "_FAULT_INFO=none");
        return;
    }

    var info = fault.Error;
    Logger.Info(label + "_FAULT_CODE=" + info.Code);
    Logger.Info(label + "_FAULT_SOURCE=" + info.Source);
    Logger.Info(label + "_FAULT_KEY_BASE64=" + B64(info.MessageKey));
    Logger.Info(label + "_FAULT_RESOURCE_BASE64=" + B64(info.MessageResource));
    Logger.Info(label + "_FAULT_MESSAGE_BASE64=" + B64(info.Message));
    Logger.Info(label + "_FAULT_ARGUMENTS_BASE64=" + B64(string.Join("\u001f", info.MessageArguments ?? new List<string>())));
}

void EmitActivationEvidence(string phase, ActivationFacade activation)
{
    Logger.Info(phase + "_FACADE_STATE=" + activation.State);
    if (activation.Error == null)
    {
        Logger.Info(phase + "_FACADE_ERROR=none");
    }
    else
    {
        Logger.Error(phase + "_FACADE_ERROR=" + activation.Error.GetType().FullName + ": " + activation.Error.Message);
        EmitFaultEvidence(phase, activation.Error);
    }

    try
    {
        var state = activation.GetActivationState();
        if (state == null)
        {
            Logger.Info(phase + "_CONTROLLER_STATE=");
            Logger.Info(phase + "_CONTROLLER_LOG_BASE64=");
        }
        else
        {
            Logger.Info(phase + "_CONTROLLER_STATE=" + state.State);
            Logger.Info(phase + "_CONTROLLER_LOG_BASE64=" + Convert.ToBase64String(
                Encoding.UTF8.GetBytes(state.ActivationLog ?? string.Empty)));
        }
    }
    catch (Exception evidenceError)
    {
        Logger.Error(phase + "_EVIDENCE_ERROR=" + evidenceError.GetType().FullName + ": " + evidenceError.Message);
    }
}

Logger.Info("KUKA Lab isolated phased project activation");
Logger.Info("ControllerAddress=" + address);
Logger.Info("ProjectName=" + projectName);
Logger.Info("AuthorizationRef=" + authorizationRef);

if (string.IsNullOrWhiteSpace(authorizationRef))
{
    Logger.Error("PROJECT_ACTIVATION_PHASED_FAILED: authorization reference is required.");
    return 41;
}

try
{
    IScriptingOnlineController onlineAccess = ScriptRunner.GetOnlineController(address);
    var scriptingController = (ScriptingOnlineController)onlineAccess;
    var deviceInfo = scriptingController.DeviceInfo;

    using (var deviceFacade = new DeviceInfoFacade(deviceInfo))
    {
        var project = deviceFacade.GetProjects().Projects.SingleOrDefault(
            candidate => string.Equals(candidate.Name, projectName, StringComparison.Ordinal));
        if (project == null)
        {
            throw new InvalidOperationException("Expected staged project was not found.");
        }

        Logger.Info("CurrentProjectName=" + deviceInfo.CurrentProjectName);
        Logger.Info("CurrentRobotType=" + deviceInfo.RoboterType);
        Logger.Info("FirmwareVersion=" + deviceInfo.FirmwareVersion);
        Logger.Info("TargetProjectId=" + project.ProjectId);
        Logger.Info("TargetProjectVersion=" + project.Version);

        if (string.Equals(deviceInfo.CurrentProjectName, projectName, StringComparison.Ordinal))
        {
            Logger.Info("ACTIVATION_ALREADY_ACTIVE=true");
            Logger.Info("PROJECT_ACTIVATION_PHASED_EXECUTED=true");
            return 0;
        }

        using (var activation = new ActivationFacade(project, deviceInfo))
        {
            activation.ProgressChanged += (sender, progress) =>
                Logger.Info("ACTIVATION_PROGRESS=" + progress.ProgressPercentage + ";" + Convert.ToString(progress.UserState));

            Logger.Info("ACTIVATION_PHASE=PrepareInstallation");
            object modification = null;
            try
            {
                modification = activation.PrepareInstallationTransactional();
            }
            catch (Exception phaseError)
            {
                Logger.Error("ACTIVATION_PREPARE_EXCEPTION=" + phaseError.GetType().FullName + ": " + phaseError.Message);
                EmitActivationEvidence("ACTIVATION_PREPARE_FAILURE", activation);
                throw;
            }
            Logger.Info("ACTIVATION_PREPARE_STATE=" + activation.State);
            Logger.Info("ACTIVATION_MODIFICATION=" + Convert.ToString(modification));
            if (activation.State != ProjectState.ReadyForInstallation)
            {
                throw new InvalidOperationException("PrepareInstallation did not finish in ReadyForInstallation state.");
            }

            Logger.Info("ACTIVATION_PHASE=Install");
            try
            {
                activation.InstallTransactional();
            }
            catch (Exception phaseError)
            {
                Logger.Error("ACTIVATION_INSTALL_EXCEPTION=" + phaseError.GetType().FullName + ": " + phaseError.Message);
                EmitActivationEvidence("ACTIVATION_INSTALL_FAILURE", activation);
                throw;
            }
            Logger.Info("ACTIVATION_INSTALL_STATE=" + activation.State);
            if (activation.State != ProjectState.ReadyForActivation)
            {
                throw new InvalidOperationException("Install did not finish in ReadyForActivation state.");
            }

            Logger.Info("ACTIVATION_PHASE=Activate");
            try
            {
                activation.ActivateTransactional();
            }
            catch (Exception phaseError)
            {
                Logger.Error("ACTIVATION_FINAL_EXCEPTION=" + phaseError.GetType().FullName + ": " + phaseError.Message);
                EmitActivationEvidence("ACTIVATION_FINAL_FAILURE", activation);
                throw;
            }
            Logger.Info("ACTIVATION_FINAL_STATE=" + activation.State);
            if (activation.Error == null)
            {
                Logger.Info("ACTIVATION_ERROR=none");
            }
            else
            {
                Logger.Error("ACTIVATION_ERROR=" + activation.Error.GetType().FullName + ": " + activation.Error.Message);
            }

            if (activation.State != ProjectState.Active || activation.Error != null)
            {
                throw new InvalidOperationException("Project activation did not finish in Active state.");
            }
        }
    }

    Logger.Info("PROJECT_ACTIVATION_PHASED_EXECUTED=true");
    return 0;
}
catch (Exception ex)
{
    Logger.Error("PROJECT_ACTIVATION_PHASED_FAILED: " + ex.GetType().FullName + ": " + ex.Message);
    var onlineFault = ex as OnlineServiceFaultException;
    if (onlineFault != null)
    {
        EmitFaultEvidence("ACTIVATION_OUTER_FAILURE", onlineFault);
    }
    Logger.Info("PROJECT_ACTIVATION_PHASED_EXECUTED=false");
    return 42;
}
