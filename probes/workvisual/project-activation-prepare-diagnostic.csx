using KukaRoboter.Contracts.DeviceInfoService;
using KukaRoboter.OnlineServicesFacade;
using Kuka.WorkVisual.Scripting.KrcOnline;
using Kuka.WorkVisual.Scripting.KrcOnline.ScriptingObjects;
using System.Reflection;

var address = ScriptRunnerEnvironment.GetArgument<string>("address");
var projectName = ScriptRunnerEnvironment.GetArgument<string>("projectname");
var authorizationRef = ScriptRunnerEnvironment.GetArgument<string>("authorizationref");

Logger.Info("KUKA Lab client-watchdog-isolation diagnostic");
Logger.Info("ACTIVATION_DIAGNOSTIC_ONLY=true");
Logger.Info("ControllerAddress=" + address);
Logger.Info("ProjectName=" + projectName);
Logger.Info("AuthorizationRef=" + authorizationRef);

if (string.IsNullOrWhiteSpace(authorizationRef))
{
    Logger.Error("PREPARE_DIAGNOSTIC_FAILED: authorization reference is required.");
    return 41;
}

var prepareSucceeded = false;
var rollbackSucceeded = false;

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

        using (var activation = new ActivationFacade(project, deviceInfo))
        {
            activation.ProgressChanged += (sender, progress) =>
                Logger.Info("PREPARE_DIAGNOSTIC_PROGRESS=" + progress.ProgressPercentage + ";" + Convert.ToString(progress.UserState));

            var targetInfoField = typeof(ActivationFacade).GetField(
                "targetInfo",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (targetInfoField == null)
            {
                throw new MissingFieldException(typeof(ActivationFacade).FullName, "targetInfo");
            }

            var targetInfo = targetInfoField.GetValue(activation);
            if (targetInfo == null)
            {
                throw new InvalidOperationException("ActivationFacade targetInfo was null.");
            }

            var keepAliveProperty = targetInfo.GetType().GetProperty(
                "KeepAliveMonitoringEnabled",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (keepAliveProperty == null || !keepAliveProperty.CanRead || !keepAliveProperty.CanWrite)
            {
                throw new MissingMemberException(targetInfo.GetType().FullName, "KeepAliveMonitoringEnabled");
            }

            var keepAliveBefore = Convert.ToBoolean(keepAliveProperty.GetValue(targetInfo, null));
            Logger.Info("CLIENT_KEEPALIVE_MONITOR_BEFORE=" + keepAliveBefore);
            keepAliveProperty.SetValue(targetInfo, false, null);
            Logger.Info("CLIENT_KEEPALIVE_MONITOR_DISABLED="
                + !Convert.ToBoolean(keepAliveProperty.GetValue(targetInfo, null)));

            try
            {
                Logger.Info("PREPARE_DIAGNOSTIC_PHASE=PrepareInstallation");
                var modification = activation.PrepareInstallation();
                Logger.Info("PREPARE_DIAGNOSTIC_STATE=" + activation.State);
                Logger.Info("PREPARE_DIAGNOSTIC_MODIFICATION=" + Convert.ToString(modification));
                prepareSucceeded = activation.State == ProjectState.ReadyForInstallation;
                if (!prepareSucceeded)
                {
                    Logger.Error("PREPARE_DIAGNOSTIC_FAILED: PrepareInstallation did not reach ReadyForInstallation.");
                }
            }
            catch (Exception prepareError)
            {
                Logger.Error("PREPARE_DIAGNOSTIC_EXCEPTION=" + prepareError.GetType().FullName + ": " + prepareError.Message);
                Logger.Info("PREPARE_DIAGNOSTIC_FAILURE_STATE=" + activation.State);
                if (activation.Error != null)
                {
                    Logger.Error("PREPARE_DIAGNOSTIC_FACADE_ERROR="
                        + activation.Error.GetType().FullName + ": " + activation.Error.Message);
                }
            }
            finally
            {
                try
                {
                    Logger.Info("PREPARE_DIAGNOSTIC_PHASE=Rollback");
                    activation.Rollback();
                    rollbackSucceeded = true;
                    Logger.Info("PREPARE_DIAGNOSTIC_ROLLBACK_STATE=" + activation.State);
                }
                catch (Exception rollbackError)
                {
                    Logger.Error("PREPARE_DIAGNOSTIC_ROLLBACK_FAILED="
                        + rollbackError.GetType().FullName + ": " + rollbackError.Message);
                }
            }
        }
    }
}
catch (Exception ex)
{
    Logger.Error("PREPARE_DIAGNOSTIC_FAILED: " + ex.GetType().FullName + ": " + ex.Message);
}

Logger.Info("PREPARE_DIAGNOSTIC_SUCCEEDED=" + prepareSucceeded);
Logger.Info("PREPARE_DIAGNOSTIC_ROLLBACK_SUCCEEDED=" + rollbackSucceeded);
return prepareSucceeded && rollbackSucceeded ? 0 : 42;
